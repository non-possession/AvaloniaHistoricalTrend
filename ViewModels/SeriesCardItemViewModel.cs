using Avalonia;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;

namespace AvaloniaApplication2.ViewModels;

// 右侧变量卡的展示状态。
// 变量卡既显示变量名和值，也承载显隐和当前轴选择的视觉反馈。
public partial class SeriesCardItemViewModel : ViewModelBase
{
    [ObservableProperty]
    private string name = string.Empty;

    [ObservableProperty]
    private string valueText = string.Empty;

    [ObservableProperty]
    private bool isVisible = true;

    [ObservableProperty]
    private IBrush borderBrush = new SolidColorBrush(Color.Parse("#A2A2A2"));

    [ObservableProperty]
    private IBrush backgroundBrush = new SolidColorBrush(Color.Parse("#E3E3E3"));

    [ObservableProperty]
    private Thickness borderThickness = new Thickness(1);

    [ObservableProperty]
    private double opacity = 1;
}
