namespace AvaloniaApplication2.Models;

public sealed class TrendWorkbenchPresentationSnapshot
{
    public string ZoomStatus { get; init; } = string.Empty;
    public string ViewportStatus { get; init; } = string.Empty;
    public string TimeRangeStatus { get; init; } = string.Empty;
    public string YAxisStatus { get; init; } = string.Empty;
    public string CrosshairStatus { get; init; } = string.Empty;
    public string LeftTimeText { get; init; } = string.Empty;
    public string RightTimeText { get; init; } = string.Empty;
    public string BottomLeftRangeText { get; init; } = string.Empty;
    public string BottomRightRangeText { get; init; } = string.Empty;
    public string SelectedDurationText { get; init; } = string.Empty;
    public string TrendModeText { get; init; } = string.Empty;
    public string DatabasePathText { get; init; } = string.Empty;
    public string SamplingIntervalText { get; init; } = string.Empty;
    public bool IsHistoricalMode { get; init; } = true;
    public bool IsRealtimeMode { get; init; }
    public TrendLayoutMode LayoutMode { get; init; } = TrendLayoutMode.DualAxis;
    public bool IsSingleAxisMode { get; init; }
    public bool IsDualAxisMode { get; init; } = true;
    public string[] LeftAxisOptions { get; init; } = [];
    public string[] RightAxisOptions { get; init; } = [];
    public AxisPanelPresentation LeftAxisPanel { get; init; } = new();
    public AxisPanelPresentation RightAxisPanel { get; init; } = new();
    public TimeHeaderPresentation[] TimeHeaders { get; init; } = [];
    public SeriesCardPresentation[] SeriesCards { get; init; } = [];
    public DurationButtonPresentation[] DurationButtons { get; init; } = [];
}
