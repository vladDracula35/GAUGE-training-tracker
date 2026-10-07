using System;
using CommunityToolkit.Mvvm.ComponentModel;

namespace GAUGE.Models;

public partial class CalendarDayModel : ObservableObject
{
    [ObservableProperty] private int _dayNumber;
    [ObservableProperty] private DateTime _date;
    [ObservableProperty] private bool _hasWorkout;
    [ObservableProperty] private bool _isToday;
    [ObservableProperty] private bool _isCurrentMonth;

    public string TextColor
    {
        get
        {
            if (!IsCurrentMonth) return "#444444";
            if (HasWorkout) return "#85E085";
            return "#E3DAD1";
        }
    }

    public string CellBackground
    {
        get
        {
            if (HasWorkout) return "#162B16";
            if (IsToday) return "#2D1515";
            return "Transparent";
        }
    }

    public string CellBorder
    {
        get
        {
            if (IsToday) return "#800020";
            if (HasWorkout) return "#2B4E2B";
            return "Transparent";
        }
    }
}
