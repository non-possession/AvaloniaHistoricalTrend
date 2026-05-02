using CommunityToolkit.Mvvm.ComponentModel;

namespace AvaloniaApplication2.ViewModels;

public partial class TimeHeaderItemViewModel : ViewModelBase
{
    [ObservableProperty]
    private string dateText = string.Empty;

    [ObservableProperty]
    private string timeText = string.Empty;
}
