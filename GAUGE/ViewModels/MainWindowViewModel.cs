using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GAUGE.Models;
using GAUGE.Services;
using System.Collections.Generic;

namespace GAUGE.ViewModels;

public class WorkoutHistoryModel
{
    public string Title { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
}

public partial class ExportItem : ObservableObject
{
    public string FileName { get; set; } = string.Empty;
    [ObservableProperty] private bool _isSelected;
}

public partial class MainWindowViewModel : ViewModelBase
{
    // --- КЕРУВАННЯ ЕКРАНАМИ ---
    [ObservableProperty] private bool _isWorkoutActive = false;

    // --- ГОЛОВНИЙ ЕКРАН ---
    public ObservableCollection<WorkoutHistoryModel> PastWorkouts { get; } = new();
    [ObservableProperty] private string _aiQuote = "Сьогодні ти переміг лінь. Завтра ти переможеш вагу.";
    // Календар активності
    public ObservableCollection<CalendarDayModel> CurrentMonthDays { get; } = new();
    [ObservableProperty] private string _currentMonthTitle = string.Empty;
    private readonly string _activityLogFilePath;
    private DateTime _displayDate = DateTime.Now;

    [RelayCommand]
    public void PreviousMonth()
    {
        _displayDate = _displayDate.AddMonths(-1);
        BuildCalendar();
    }

    [RelayCommand]
    public void NextMonth()
    {
        _displayDate = _displayDate.AddMonths(1);
        BuildCalendar();
    }

    // --- ЕКРАН ТРЕНУВАННЯ ---
    public ObservableCollection<WorkoutSetModel> WorkoutSets { get; } = new();

    // Списки вправ: повний та відфільтрований
    public ObservableCollection<ExerciseModel> AllExercises { get; } = new();
    public ObservableCollection<ExerciseModel> FilteredExercises { get; } = new();

    [ObservableProperty] private ExerciseModel? _selectedExerciseItem;
    [ObservableProperty] private string _selectedExercise = "Виберіть вправу";
    [ObservableProperty] private string _searchExerciseText = string.Empty;
    [ObservableProperty] private string _selectedCategoryFilter = "Всі";
    [ObservableProperty] private string _newExerciseName = string.Empty;
    [ObservableProperty] private string _newExerciseCategory = "Верх";

    [ObservableProperty] private int _currentReps = 0;
    [ObservableProperty] private int _currentRest = 60;
    [ObservableProperty] private string _currentNotes = string.Empty;

    [ObservableProperty] private bool _isMenuOpen = false;
    [ObservableProperty] private bool _isRepOverlayVisible = false;
    [ObservableProperty] private bool _isRestOverlayVisible = false;
    [ObservableProperty] private bool _isFinishDialogVisible = false;
    
    [ObservableProperty] private bool _isWorkoutDetailsVisible = false;
    [ObservableProperty] private string _detailsTitle = string.Empty;
    public ObservableCollection<WorkoutSetModel> DetailsSets { get; } = new();
    private string _currentDetailsFilePath = string.Empty;
    
    // Таймер
    private DispatcherTimer _timer;
    private int _elapsedSeconds = 0;
    [ObservableProperty] private string _timerDisplay = "00:00";
    [ObservableProperty] private bool _isTimerRunning = false;
    [ObservableProperty] private string _playPauseIcon = "▶";

    private readonly string _dataDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), 
        "GAUGEData"
    );

    private readonly string _autosaveFilePath;
    private readonly string _filePath;

    [ObservableProperty] private bool _isAutosaveDialogVisible = false;

    public ObservableCollection<ExportItem> AvailableWorkouts { get; } = new();
    [ObservableProperty] private bool _isExportDialogVisible = false;

    public MainWindowViewModel()
    {
        _autosaveFilePath = Path.Combine(_dataDirectory, "autosave_workout.json");
        _filePath = Path.Combine(_dataDirectory, "exercises_list.txt");
        _activityLogFilePath = Path.Combine(_dataDirectory, "activity_log.json");

        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _timer.Tick += (s, e) =>
        {
            _elapsedSeconds++;
            TimerDisplay = TimeSpan.FromSeconds(_elapsedSeconds).ToString(@"mm\:ss");
        };

        LoadExercisesFromFile();
        LoadHistory();
        BuildCalendar();

        if (File.Exists(_autosaveFilePath))
        {
            try
            {
                string jsonString = File.ReadAllText(_autosaveFilePath);
                if (!string.IsNullOrWhiteSpace(jsonString) && jsonString != "[]")
                {
                    IsAutosaveDialogVisible = true;
                }
            }
            catch { }
        }
    }

    // --- РЕАКЦІЯ НА ЗМІНУ ВЛАСТИВОСТЕЙ ---
    partial void OnSelectedExerciseItemChanged(ExerciseModel? value)
    {
        if (value != null)
        {
            SelectedExercise = value.Name;
        }
    }

    partial void OnSearchExerciseTextChanged(string value) => ApplyExerciseFilter();
    partial void OnSelectedCategoryFilterChanged(string value) => ApplyExerciseFilter();

    [RelayCommand]
    public void SelectCategory(string category)
    {
        SelectedCategoryFilter = category;
    }

    public void ApplyExerciseFilter()
    {
        var query = AllExercises.AsEnumerable();

        if (!string.Equals(SelectedCategoryFilter, "Всі", StringComparison.OrdinalIgnoreCase))
        {
            query = query.Where(e => string.Equals(e.Category, SelectedCategoryFilter, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(SearchExerciseText))
        {
            query = query.Where(e => e.Name.Contains(SearchExerciseText, StringComparison.OrdinalIgnoreCase));
        }

        // Остання використана вправа піднімається на самий верх!
        var sorted = query
            .OrderByDescending(e => e.LastUsedAt)
            .ThenBy(e => e.Name)
            .ToList();

        FilteredExercises.Clear();
        foreach (var item in sorted)
        {
            FilteredExercises.Add(item);
        }
    }

    public void MarkExerciseAsUsed(string exerciseName)
    {
        var item = AllExercises.FirstOrDefault(e => e.Name.Equals(exerciseName, StringComparison.OrdinalIgnoreCase));
        if (item != null)
        {
            item.LastUsedAt = DateTime.Now;
            SaveExercisesToFile();
            ApplyExerciseFilter();
        }
    }

    private void LoadExercisesFromFile()
    {
        try
        {
            if (!Directory.Exists(_dataDirectory))
            {
                Directory.CreateDirectory(_dataDirectory);
            }

            if (!File.Exists(_filePath))
            {
                var defaults = new[]
                {
                    "Підтягування на турніку|Верх",
                    "Віджимання на брусах|Верх",
                    "Бій з тінню|Кор",
                    "Робота на мішку|Верх",
                    "Присідання|Ноги"
                };
                File.WriteAllLines(_filePath, defaults);
            }

            var lines = File.ReadAllLines(_filePath);
            AllExercises.Clear();
            foreach (var line in lines.Where(l => !string.IsNullOrWhiteSpace(l)))
            {
                var parts = line.Split('|');
                var name = parts[0].Trim();
                var cat = parts.Length > 1 ? parts[1].Trim() : "Верх";
                DateTime lastUsed = DateTime.MinValue;
                if (parts.Length > 2 && DateTime.TryParse(parts[2], out var dt))
                {
                    lastUsed = dt;
                }

                AllExercises.Add(new ExerciseModel
                {
                    Name = name,
                    Category = cat,
                    LastUsedAt = lastUsed
                });
            }

            ApplyExerciseFilter();
            SelectedExerciseItem = FilteredExercises.FirstOrDefault();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Помилка завантаження вправ: {ex.Message}");
        }
    }

    private void SaveExercisesToFile()
    {
        try
        {
            var lines = AllExercises.Select(e => $"{e.Name}|{e.Category}|{e.LastUsedAt:o}");
            File.WriteAllLines(_filePath, lines);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Помилка збереження вправ: {ex.Message}");
        }
    }

    [RelayCommand]
    public void AddCustomExercise()
    {
        if (string.IsNullOrWhiteSpace(NewExerciseName)) return;

        var name = NewExerciseName.Trim();
        if (!AllExercises.Any(e => e.Name.Equals(name, StringComparison.OrdinalIgnoreCase)))
        {
            var newItem = new ExerciseModel
            {
                Name = name,
                Category = NewExerciseCategory,
                LastUsedAt = DateTime.Now
            };

            AllExercises.Add(newItem);
            SaveExercisesToFile();
            NewExerciseName = string.Empty;
            ApplyExerciseFilter();
            SelectedExerciseItem = newItem;
        }
    }

    [RelayCommand]
    public void RemoveExercise(ExerciseModel? item)
    {
        if (item == null) return;

        if (AllExercises.Contains(item))
        {
            AllExercises.Remove(item);
            SaveExercisesToFile();
            ApplyExerciseFilter();

            if (SelectedExerciseItem == item || SelectedExercise == item.Name)
            {
                SelectedExerciseItem = FilteredExercises.FirstOrDefault();
            }
        }
    }

    // --- ЛОГІКА СЕТІВ ТРЕНУВАННЯ ---
    [RelayCommand]
    public void AddToCurrentSet()
    {
        if (string.IsNullOrWhiteSpace(SelectedExercise) || SelectedExercise == "Виберіть вправу") return;

        var currentSet = WorkoutSets.LastOrDefault(s => !s.IsClosed);
        if (currentSet == null)
        {
            currentSet = new WorkoutSetModel();
            WorkoutSets.Add(currentSet);
        }

        AddExerciseToSet(currentSet);
        MarkExerciseAsUsed(SelectedExercise);
        IsMenuOpen = false;
        SaveAutosave();
    }

    [RelayCommand]
    public void AddToNewSet()
    {
        if (string.IsNullOrWhiteSpace(SelectedExercise) || SelectedExercise == "Виберіть вправу") return;

        var currentSet = WorkoutSets.LastOrDefault(s => !s.IsClosed);
        if (currentSet != null) currentSet.IsClosed = true;

        var newSet = new WorkoutSetModel();
        WorkoutSets.Add(newSet);

        AddExerciseToSet(newSet);
        MarkExerciseAsUsed(SelectedExercise);
        IsMenuOpen = false;
        SaveAutosave();
    }

    private void AddExerciseToSet(WorkoutSetModel set)
    {
        set.Exercises.Add(new ExerciseModel
        {
            Name = SelectedExercise,
            Reps = CurrentReps,
            Notes = CurrentNotes,
            RestSeconds = CurrentRest,
            Category = SelectedExerciseItem?.Category ?? "Верх"
        });
        CurrentNotes = string.Empty;
    }

    [RelayCommand]
    public void ToggleTimer()
    {
        if (IsTimerRunning)
        {
            _timer.Stop();
            PlayPauseIcon = "▶";
        }
        else
        {
            _timer.Start();
            PlayPauseIcon = "॥";
        }
        IsTimerRunning = !IsTimerRunning;
    }

    [RelayCommand]
    public void SaveTimerToLastExercise()
    {
        _timer.Stop();
        IsTimerRunning = false;
        PlayPauseIcon = "▶";

        var currentSet = WorkoutSets.LastOrDefault(s => !s.IsClosed);
        var lastExercise = currentSet?.Exercises.LastOrDefault();
        if (lastExercise != null)
        {
            lastExercise.RestSeconds = _elapsedSeconds;
        }

        _elapsedSeconds = 0;
        TimerDisplay = "00:00";
    }

    [RelayCommand]
    public void RequestFinishWorkout()
    {
        if (WorkoutSets.Any()) IsFinishDialogVisible = true;
    }

    [RelayCommand]
    public void CancelFinishWorkout() => IsFinishDialogVisible = false;

private HashSet<string> LoadActivityDates()
    {
        try
        {
            if (File.Exists(_activityLogFilePath))
            {
                string json = File.ReadAllText(_activityLogFilePath);
                var set = JsonSerializer.Deserialize<HashSet<string>>(json);
                if (set != null) return set;
            }
        }
        catch { }
        return new HashSet<string>();
    }

    private void RecordActivityDate(DateTime date)
    {
        try
        {
            var dates = LoadActivityDates();
            dates.Add(date.ToString("yyyy-MM-dd"));
            if (!Directory.Exists(_dataDirectory)) Directory.CreateDirectory(_dataDirectory);
            string json = JsonSerializer.Serialize(dates, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_activityLogFilePath, json);
        }
        catch { }
    }

    public void BuildCalendar()
    {
        CurrentMonthDays.Clear();
        var now = DateTime.Now;
        var viewDate = _displayDate;

        CurrentMonthTitle = viewDate.ToString("MMMM yyyy").ToUpper();

        var workoutDates = LoadActivityDates();
        var firstDayOfMonth = new DateTime(viewDate.Year, viewDate.Month, 1);
        int daysInMonth = DateTime.DaysInMonth(viewDate.Year, viewDate.Month);

        // Зміщення для першого дня місяця (0 = Пн ... 6 = Нд)
        int leadOffset = ((int)firstDayOfMonth.DayOfWeek + 6) % 7;

        var prevMonth = firstDayOfMonth.AddMonths(-1);
        int daysInPrevMonth = DateTime.DaysInMonth(prevMonth.Year, prevMonth.Month);

        // Хвостик попереднього місяця (буде сірим)
        for (int i = leadOffset - 1; i >= 0; i--)
        {
            var d = new DateTime(prevMonth.Year, prevMonth.Month, daysInPrevMonth - i);
            CurrentMonthDays.Add(new CalendarDayModel
            {
                DayNumber = d.Day,
                Date = d,
                HasWorkout = workoutDates.Contains(d.ToString("yyyy-MM-dd")),
                IsToday = false,
                IsCurrentMonth = false
            });
        }

        // Дні обраного місяця
        for (int day = 1; day <= daysInMonth; day++)
        {
            var d = new DateTime(viewDate.Year, viewDate.Month, day);
            bool isRealToday = (viewDate.Year == now.Year && viewDate.Month == now.Month && day == now.Day);

            CurrentMonthDays.Add(new CalendarDayModel
            {
                DayNumber = day,
                Date = d,
                HasWorkout = workoutDates.Contains(d.ToString("yyyy-MM-dd")),
                IsToday = isRealToday,
                IsCurrentMonth = true
            });
        }

        // Хвостик наступного місяця (буде сірим)
        int remainder = CurrentMonthDays.Count % 7;
        if (remainder != 0)
        {
            int tailCount = 7 - remainder;
            var nextMonth = firstDayOfMonth.AddMonths(1);
            for (int day = 1; day <= tailCount; day++)
            {
                var d = new DateTime(nextMonth.Year, nextMonth.Month, day);
                CurrentMonthDays.Add(new CalendarDayModel
                {
                    DayNumber = day,
                    Date = d,
                    HasWorkout = workoutDates.Contains(d.ToString("yyyy-MM-dd")),
                    IsToday = false,
                    IsCurrentMonth = false
                });
            }
        }
    }
    [RelayCommand]
    public void ConfirmFinishWorkout()
    {
        if (!WorkoutSets.Any()) return;

        try
        {
            if (!Directory.Exists(_dataDirectory)) Directory.CreateDirectory(_dataDirectory);

            string fileName = $"Workout_{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.json";
            string filePath = Path.Combine(_dataDirectory, fileName);

            string json = JsonSerializer.Serialize(WorkoutSets, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(filePath, json);
            RecordActivityDate(DateTime.Now);
            BuildCalendar();

            DeleteAutosave();
            WorkoutSets.Clear();
            _elapsedSeconds = 0;
            TimerDisplay = "00:00";
            IsFinishDialogVisible = false;
            IsWorkoutActive = false;

            LoadHistory();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Помилка збереження: {ex.Message}");
        }
    }

    [RelayCommand]
    public void StartNewWorkout() => IsWorkoutActive = true;

    private void SaveAutosave()
    {
        try
        {
            if (WorkoutSets.Any())
            {
                string jsonString = JsonSerializer.Serialize(WorkoutSets, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(_autosaveFilePath, jsonString);
            }
            else
            {
                DeleteAutosave();
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Помилка автозбереження: {ex.Message}");
        }
    }

    private void DeleteAutosave()
    {
        if (File.Exists(_autosaveFilePath))
        {
            try { File.Delete(_autosaveFilePath); } catch { }
        }
    }

    [RelayCommand]
    public void ConfirmRestoreWorkout()
    {
        IsAutosaveDialogVisible = false;
        try
        {
            if (File.Exists(_autosaveFilePath))
            {
                string jsonString = File.ReadAllText(_autosaveFilePath);
                var restoredSets = JsonSerializer.Deserialize<ObservableCollection<WorkoutSetModel>>(jsonString);

                if (restoredSets != null)
                {
                    WorkoutSets.Clear();
                    foreach (var set in restoredSets)
                    {
                        WorkoutSets.Add(set);
                    }
                    IsWorkoutActive = true;
                }
            }
        }
        catch
        {
            DeleteAutosave();
        }
    }

    [RelayCommand]
    public void CancelRestoreWorkout()
    {
        IsAutosaveDialogVisible = false;
        DeleteAutosave();
    }

    private void LoadHistory()
    {
        PastWorkouts.Clear();
        if (!Directory.Exists(_dataDirectory)) return;

        var files = Directory.GetFiles(_dataDirectory, "Workout_*.json").OrderByDescending(f => f).Take(10);
        foreach (var file in files)
        {
            try
            {
                string jsonString = File.ReadAllText(file);
                var sets = JsonSerializer.Deserialize<ObservableCollection<WorkoutSetModel>>(jsonString);

                if (sets == null || !sets.Any()) continue;

                int totalSets = sets.Count;
                var exerciseTotals = sets.SelectMany(s => s.Exercises)
                                         .GroupBy(e => e.Name)
                                         .Select(g => $"{g.Key}: {g.Sum(e => e.Reps)}")
                                         .ToList();

                string summary = $"{totalSets} підходів • {string.Join(", ", exerciseTotals)}";

                PastWorkouts.Add(new WorkoutHistoryModel
                {
                    Title = "Тренування " + Path.GetFileNameWithoutExtension(file).Replace("Workout_", "").Replace("_", " "),
                    Summary = summary,
                    FilePath = file
                });
            }
            catch { }
        }
    }

    [RelayCommand]
    public void OpenWorkoutDetails(WorkoutHistoryModel historyItem)
    {
        if (historyItem == null || string.IsNullOrWhiteSpace(historyItem.FilePath) || !File.Exists(historyItem.FilePath))
            return;

        try
        {
            string json = File.ReadAllText(historyItem.FilePath);
            var sets = JsonSerializer.Deserialize<ObservableCollection<WorkoutSetModel>>(json);
            if (sets != null)
            {
                DetailsSets.Clear();
                foreach (var set in sets) DetailsSets.Add(set);
                DetailsTitle = historyItem.Title;
                _currentDetailsFilePath = historyItem.FilePath;
                IsWorkoutDetailsVisible = true;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Помилка завантаження деталей: {ex.Message}");
        }
    }

    [RelayCommand]
    public void CloseWorkoutDetails()
    {
        IsWorkoutDetailsVisible = false;
        DetailsSets.Clear();
    }

    [RelayCommand]
    public async Task ExportCurrentDetailsToMarkdown()
    {
        if (!DetailsSets.Any()) return;

        var storageProvider = GetStorageProvider();
        if (storageProvider == null) return;

        var file = await storageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Експорт тренування в Obsidian",
            DefaultExtension = "md",
            SuggestedFileName = $"{DetailsTitle.Replace(" ", "_")}.md",
            FileTypeChoices = new[]
            {
                new FilePickerFileType("Markdown") { Patterns = new[] { "*.md" } }
            }
        });

        if (file != null)
        {
            string md = MarkdownExportService.GenerateWorkoutMarkdown(DetailsTitle, DetailsSets);
            await using var stream = await file.OpenWriteAsync();
            await using var writer = new StreamWriter(stream, Encoding.UTF8);
            await writer.WriteAsync(md);
        }
    }

    private IStorageProvider? GetStorageProvider()
    {
        var app = Avalonia.Application.Current;
        if (app?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            return desktop.MainWindow?.StorageProvider;
        if (app?.ApplicationLifetime is ISingleViewApplicationLifetime singleView)
            return Avalonia.Controls.TopLevel.GetTopLevel(singleView.MainView)?.StorageProvider;
        return null;
    }

    // --- БАТЧ-ЕКСПОРТ (ВИПРАВЛЕНИЙ) ---
    [RelayCommand]
    public void OpenExportMenuCommand()
    {
        AvailableWorkouts.Clear();
        if (Directory.Exists(_dataDirectory))
        {
            var files = Directory.GetFiles(_dataDirectory, "Workout_*.json")
                                 .OrderByDescending(f => f)
                                 .ToArray();

            foreach (var file in files)
            {
                AvailableWorkouts.Add(new ExportItem
                {
                    FileName = Path.GetFileName(file),
                    IsSelected = true
                });
            }
        }
        IsExportDialogVisible = true;
    }

    [RelayCommand]
    public async Task ConfirmExportCommand()
    {
        var storageProvider = GetStorageProvider();
        if (storageProvider == null) return;

        var folders = await storageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "Виберіть папку для експорту вправ",
            AllowMultiple = false
        });

        if (folders != null && folders.Count > 0)
        {
            string targetFolderPath = folders[0].TryGetLocalPath() ?? folders[0].Path.LocalPath;

            foreach (var item in AvailableWorkouts.Where(i => i.IsSelected))
            {
                string sourceJsonPath = Path.Combine(_dataDirectory, item.FileName);
                if (File.Exists(sourceJsonPath))
                {
                    try
                    {
                        // Експортуємо і сирий JSON, і згенерований Markdown
                        string json = File.ReadAllText(sourceJsonPath);
                        var sets = JsonSerializer.Deserialize<ObservableCollection<WorkoutSetModel>>(json);

                        if (sets != null)
                        {
                            string title = Path.GetFileNameWithoutExtension(item.FileName).Replace("Workout_", "Тренування ");
                            string md = MarkdownExportService.GenerateWorkoutMarkdown(title, sets);

                            string targetMdPath = Path.Combine(targetFolderPath, $"{Path.GetFileNameWithoutExtension(item.FileName)}.md");
                            File.WriteAllText(targetMdPath, md, Encoding.UTF8);
                        }
                    }
                    catch { }
                }
            }
            IsExportDialogVisible = false;
        }
    }

    [RelayCommand]
    public void CancelExportCommand()
    {
        IsExportDialogVisible = false;
    }
[RelayCommand]
public void CycleExerciseCategory(ExerciseModel? item)
{
    if (item == null) return;

    // Циклічно змінюємо категорію при кліку на бейдж
    item.Category = item.Category switch
    {
        "Верх" => "Кор",
        "Кор" => "Ноги",
        _ => "Верх"
    };

    SaveExercisesToFile();
    ApplyExerciseFilter();
}

[RelayCommand]
public void SetNewExerciseCategory(string category)
{
    NewExerciseCategory = category;
}
}