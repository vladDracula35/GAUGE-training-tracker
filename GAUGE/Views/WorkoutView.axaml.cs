using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using GAUGE.ViewModels;

namespace GAUGE.Views;

public partial class WorkoutView : UserControl
{
    public WorkoutView()
    {
        InitializeComponent();

        var repSlider = this.FindControl<Slider>("RepSlider");
        var restSlider = this.FindControl<Slider>("RestSlider");

        // RoutingStrategies.Tunnel - перехоплює дотик в першу мілісекунду!
        if (repSlider != null)
        {
            repSlider.AddHandler(InputElement.PointerPressedEvent, (s, e) => { if (DataContext is MainWindowViewModel vm) vm.IsRepOverlayVisible = true; }, RoutingStrategies.Tunnel);
            repSlider.AddHandler(InputElement.PointerReleasedEvent, (s, e) => { if (DataContext is MainWindowViewModel vm) vm.IsRepOverlayVisible = false; }, RoutingStrategies.Tunnel);
            repSlider.AddHandler(InputElement.PointerCaptureLostEvent, (s, e) => { if (DataContext is MainWindowViewModel vm) vm.IsRepOverlayVisible = false; }, RoutingStrategies.Tunnel);
        }

        if (restSlider != null)
        {
            restSlider.AddHandler(InputElement.PointerPressedEvent, (s, e) => { if (DataContext is MainWindowViewModel vm) vm.IsRestOverlayVisible = true; }, RoutingStrategies.Tunnel);
            restSlider.AddHandler(InputElement.PointerReleasedEvent, (s, e) => { if (DataContext is MainWindowViewModel vm) vm.IsRestOverlayVisible = false; }, RoutingStrategies.Tunnel);
            restSlider.AddHandler(InputElement.PointerCaptureLostEvent, (s, e) => { if (DataContext is MainWindowViewModel vm) vm.IsRestOverlayVisible = false; }, RoutingStrategies.Tunnel);
        }
    }
}