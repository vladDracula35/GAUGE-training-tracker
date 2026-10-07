using System;
using CommunityToolkit.Mvvm.ComponentModel;

namespace GAUGE.Models;

public partial class ExerciseModel : ObservableObject
{
    [ObservableProperty] private string _name = string.Empty;
    [ObservableProperty] private int _reps;
    [ObservableProperty] private string _notes = string.Empty;
    [ObservableProperty] private int _restSeconds;

    // Нові поля для категорій, пошуку та підняття нагору списку:
    [ObservableProperty] private string _category = "Верх"; // "Верх", "Кор", "Ноги"
    [ObservableProperty] private DateTime _lastUsedAt = DateTime.MinValue;

    public override string ToString() => Name;
}