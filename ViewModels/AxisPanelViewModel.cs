using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;

namespace AvaloniaApplication2.ViewModels;

public partial class AxisPanelViewModel : ViewModelBase
{
    [ObservableProperty]
    private string currentSeriesText = string.Empty;

    [ObservableProperty]
    private string rangeText = string.Empty;

    [ObservableProperty]
    private IBrush accentBrush = new SolidColorBrush(Color.Parse("#000000"));

    [ObservableProperty]
    private int selectedIndex;

    [ObservableProperty]
    private string minRangeText = string.Empty;

    [ObservableProperty]
    private string maxRangeText = string.Empty;
}
