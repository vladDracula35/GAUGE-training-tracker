using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace GAUGE.Models;

// ObservableObject дозволяє інтерфейсу миттєво реагувати на зміну кольору (статусу)
public partial class WorkoutSetModel : ObservableObject
{
    public ObservableCollection<ExerciseModel> Exercises { get; set; } = new();

    [ObservableProperty]
    private bool _isClosed; // Коли true - блок стає напівпрозорим
}