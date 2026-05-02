namespace AvaloniaApplication2.Models;

public sealed class AxisPanelPresentation
{
    public string CurrentSeriesText { get; init; } = string.Empty;
    public string RangeText { get; init; } = string.Empty;
    public string AccentHex { get; init; } = "#000000";
    public int SelectedIndex { get; init; }
    public string MinRangeText { get; init; } = string.Empty;
    public string MaxRangeText { get; init; } = string.Empty;
}
