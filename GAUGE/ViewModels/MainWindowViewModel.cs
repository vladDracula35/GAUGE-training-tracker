using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GAUGE.Models;



namespace GAUGE.ViewModels;

// Спеціальна міні-модель для карток історії на головному екрані
public class WorkoutHistoryModel
{
    public string Title { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
}

public partial class MainWindowViewModel : ViewModelBase
{
    // --- КЕРУВАННЯ ЕКРАНАМИ ---
    [ObservableProperty] private bool _isWorkoutActive = false; // Якщо false - показуємо Головне меню

    // --- ГОЛОВНИЙ ЕКРАН ---
    public ObservableCollection<WorkoutHistoryModel> PastWorkouts { get; } = new();
    [ObservableProperty] private string _aiQuote = "Сьогодні ти переміг лінь. Завтра ти переможеш вагу.";

    // --- ЕКРАН ТРЕНУВАННЯ ---
    // --- ЕКРАН ТРЕНУВАННЯ ---
    public ObservableCollection<WorkoutSetModel> WorkoutSets { get; } = new();
    

    [ObservableProperty] private string _selectedExercise;
    [ObservableProperty] private int _currentReps = 0;
    [ObservableProperty] private int _currentRest = 60;
    [ObservableProperty] private string _currentNotes = "";

    [ObservableProperty] private bool _isMenuOpen = false;
    [ObservableProperty] private bool _isRepOverlayVisible = false;
    [ObservableProperty] private bool _isRestOverlayVisible = false;
    [ObservableProperty] private bool _isFinishDialogVisible = false;

    // Таймер
    private DispatcherTimer _timer;
    private int _elapsedSeconds = 0;
    [ObservableProperty] private string _timerDisplay = "00:00";
    [ObservableProperty] private bool _isTimerRunning = false;
    [ObservableProperty] private string _playPauseIcon = "▶";

    private readonly string _autosaveFilePath = Path.Combine(
    Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), 
    "GAUGEData", 
    "autosave_workout.json"
);

// Властивість для показу вікна відновлення
[ObservableProperty] private bool _isAutosaveDialogVisible = false;

 public MainWindowViewModel()
{
    _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
    _timer.Tick += (s, e) =>
    {
        _elapsedSeconds++;
        TimerDisplay = TimeSpan.FromSeconds(_elapsedSeconds).ToString(@"mm\:ss");
    };

    LoadExercisesFromFile();
    LoadHistory();

    // ПЕРЕВІРКА НА АВТОЗБЕРЕЖЕННЯ ПРИ СТАРТІ
    if (File.Exists(_autosaveFilePath))
    {
        try
        {
            string jsonString = File.ReadAllText(_autosaveFilePath);
            if (!string.IsNullOrWhiteSpace(jsonString) && jsonString != "[]")
            {
                // Показуємо діалогове вікно відновлення
                IsAutosaveDialogVisible = true;
            }
        }
        catch { }
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
                IsWorkoutActive = true; // Перемикаємо екран на тренування
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
    DeleteAutosave(); // Видаляємо стару чернетку, бо юзер відмовився
}

   private void LoadHistory()
    {
        PastWorkouts.Clear();
        string path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "GAUGEData");
        if (!Directory.Exists(path)) return;

        // Беремо 10 останніх тренувань (бо тепер у нас є скрол)
        var files = Directory.GetFiles(path, "*.json").OrderByDescending(f => f).Take(10);
        foreach (var file in files)
        {
            try 
            {
                // Читаємо файл
                string jsonString = File.ReadAllText(file);
                var sets = JsonSerializer.Deserialize<ObservableCollection<WorkoutSetModel>>(jsonString);
                
                if (sets == null || !sets.Any()) continue;

                int totalSets = sets.Count;
                
                // Рахуємо суму повторень для кожної вправи (Магія LINQ)
                var exerciseTotals = sets.SelectMany(s => s.Exercises)
                                         .GroupBy(e => e.Name)
                                         .Select(g => $"{g.Key}: {g.Sum(e => e.Reps)}")
                                         .ToList();

                // Формуємо красивий текст (напр: "3 підходи • Віджимання: 13, Прес: 20")
                string summary = $"{totalSets} підходів • {string.Join(", ", exerciseTotals)}";

                PastWorkouts.Add(new WorkoutHistoryModel { 
                    Title = "Тренування " + Path.GetFileNameWithoutExtension(file).Replace("Workout_", "").Replace("_", " "),
                    Summary = summary
                });
            } 
            catch 
            {
                // Якщо файл пошкоджений - просто пропускаємо його
            }
        }
    }

    public void RemoveExercise(string exerciseName)
{
    if (Exercises.Contains(exerciseName))
    {
        try
        {
            Exercises.Remove(exerciseName); // Видаляємо з екрану
            
            // Перезаписуємо файл повністю, але вже без цієї вправи
            File.WriteAllLines(_filePath, Exercises);
            
            // Якщо видалили вправу, яка була вибрана, скидаємо селектор на першу доступну
            if (SelectedExercise == exerciseName)
            {
                SelectedExercise = Exercises.FirstOrDefault() ?? string.Empty;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Не вдалося видалити вправу: {ex.Message}");
        }
    }
}

    private void LoadExercisesFromFile()
{
    try
    {
        // ПЕРЕВІРКА: Спочатку створюємо папку GAUGEData, якщо її нема
        string directoryPath = Path.GetDirectoryName(_filePath)!;
        if (!Directory.Exists(directoryPath))
        {
            Directory.CreateDirectory(directoryPath);
        }

        // Якщо файлу ще немає, створюємо базу
        if (!File.Exists(_filePath))
        {
            var defaultExercises = new[] { "Підтягування на турніку", "Віджимання на брусах", "Бій з тінню", "Робота на мішку", "Присідання" };
            File.WriteAllLines(_filePath, defaultExercises);
        }

        var lines = File.ReadAllLines(_filePath);
        Exercises.Clear();
        foreach (var line in lines.Where(l => !string.IsNullOrWhiteSpace(l)))
        {
            Exercises.Add(line.Trim());
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Помилка сховища: {ex.Message}");
    }
}
 public void AddCustomExercise()
 {
    if (string.IsNullOrWhiteSpace(NewExerciseName)) return;

    var name = NewExerciseName.Trim();
    if (!Exercises.Contains(name))
    {
        try
        {
            Exercises.Add(name); // Додаємо на екран
            File.AppendAllLines(_filePath, new[] { name }); // Зберігаємо у файл
            NewExerciseName = string.Empty; // Очищаємо поле
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Не вдалося записати: {ex.Message}");
        }
    }
 }

    // Шлях до ізольованого файлу на телефоні/Маку
 // Тепер список вправ живе в тій самій спільній папці, що й історія!
private readonly string _filePath = Path.Combine(
    Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), 
    "GAUGEData", 
    "exercises_list.txt"
);

 // Колекція вправ для випадаючого списку
 public ObservableCollection<string> Exercises { get; set; } = new ObservableCollection<string>();

 private string _newExerciseName = string.Empty;
 public string NewExerciseName
 {
    get => _newExerciseName;
    set 
    { 
        _newExerciseName = value; 
        // Зверни увагу: якщо OnPropertyChanged() підкреслить червоним, 
        // заміни його на this.RaiseAndSetIfChanged(ref _newExerciseName, value); 
        // (залежить від того, що написано в твоєму ViewModelBase.cs)
        OnPropertyChanged(nameof(NewExerciseName)); 
    }
 }

    // --- ЛОГІКА ТРЕНУВАННЯ ---
    [RelayCommand]
    public void ToggleTimer()
    {
        if (IsTimerRunning) { _timer.Stop(); PlayPauseIcon = "▶"; }
        else { _timer.Start(); PlayPauseIcon = "॥"; }
        IsTimerRunning = !IsTimerRunning;
    }

    [RelayCommand]
    public void SaveTimerToLastExercise()
    {
        _timer.Stop(); IsTimerRunning = false; PlayPauseIcon = "▶";
        var currentSet = WorkoutSets.LastOrDefault(s => !s.IsClosed);
        var lastExercise = currentSet?.Exercises.LastOrDefault();
        if (lastExercise != null) lastExercise.RestSeconds = _elapsedSeconds;
        _elapsedSeconds = 0; TimerDisplay = "00:00";
    }

    [RelayCommand]
    public void AddToCurrentSet()
    {
        var currentSet = WorkoutSets.LastOrDefault(s => !s.IsClosed);
        if (currentSet == null) { currentSet = new WorkoutSetModel(); WorkoutSets.Add(currentSet); }
        AddExerciseToSet(currentSet); IsMenuOpen = false; 

        SaveAutosave();
    }

    [RelayCommand]
    public void AddToNewSet()
    {
        var currentSet = WorkoutSets.LastOrDefault(s => !s.IsClosed);
        if (currentSet != null) currentSet.IsClosed = true; 
        var newSet = new WorkoutSetModel(); WorkoutSets.Add(newSet);
        AddExerciseToSet(newSet); IsMenuOpen = false; 
        SaveAutosave();
    }

    private void AddExerciseToSet(WorkoutSetModel set)
    {
        set.Exercises.Add(new ExerciseModel { Name = SelectedExercise, Reps = CurrentReps, Notes = CurrentNotes, RestSeconds = CurrentRest });
        CurrentNotes = string.Empty; 
    }

    [RelayCommand] public void RequestFinishWorkout() { if (WorkoutSets.Any()) IsFinishDialogVisible = true; }
    [RelayCommand] public void CancelFinishWorkout() => IsFinishDialogVisible = false;

    [RelayCommand]
    public void ConfirmFinishWorkout()
    {
        IsFinishDialogVisible = false;
        if (!WorkoutSets.Any()) return;

        string appFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "GAUGEData");
        Directory.CreateDirectory(appFolder);
        string timestamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm");

        // Зберігаємо JSON та Markdown
        File.WriteAllText(Path.Combine(appFolder, $"Workout_{timestamp}.json"), JsonSerializer.Serialize(WorkoutSets, new JsonSerializerOptions { WriteIndented = true }));
        
        var sb = new StringBuilder();
        sb.AppendLine($"# Тренування - {DateTime.Now:dd.MM.yyyy HH:mm}\n");
        int setNum = 1;
        foreach (var set in WorkoutSets)
        {
            if (!set.Exercises.Any()) continue;
            sb.AppendLine($"## Підхід {setNum++}");
            foreach (var ex in set.Exercises)
            {
                sb.AppendLine($"- **{ex.Name}**: {ex.Reps} повт. (Відпочинок: {ex.RestSeconds}s)");
                if (!string.IsNullOrWhiteSpace(ex.Notes)) sb.AppendLine($"  > *Відчуття: {ex.Notes}*");
            }
            sb.AppendLine();
        }
        File.WriteAllText(Path.Combine(appFolder, $"Workout_{timestamp}.md"), sb.ToString());

        WorkoutSets.Clear(); 
        DeleteAutosave();
        IsWorkoutActive = false; // ПОВЕРТАЄМОСЬ НА ГОЛОВНИЙ ЕКРАН
        LoadHistory(); // Оновлюємо список

    }
}