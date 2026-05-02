using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;

namespace AvaloniaApplication2.ViewModels;

public partial class DurationButtonItemViewModel : ViewModelBase
{
    [ObservableProperty]
    private IBrush backgroundBrush = new SolidColorBrush(Color.Parse("#BFBFBF"));

    [ObservableProperty]
    private IBrush foregroundBrush = new SolidColorBrush(Color.Parse("#101010"));

    [ObservableProperty]
    private FontWeight fontWeight = FontWeight.Normal;
}
