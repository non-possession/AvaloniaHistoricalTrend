namespace AvaloniaApplication2.Models;

public sealed class TrendWorkbenchSettings
{
    public TrendLayoutMode LayoutMode { get; set; } = TrendLayoutMode.DualAxis;
    public bool ShowLegend { get; set; }
    public int DefaultVisibleDurationMinutes { get; set; } = 1440;
    public int DefaultVisibleDurationSeconds { get; set; }
    public string StorageProvider { get; set; } = "SQLite";
    public string SqliteDatabasePath { get; set; } = "trend-workbench.db";
    public int SamplingIntervalSeconds { get; set; } = 1;
    public int RealtimeWindowMinutes { get; set; } = 60;
    public int RealtimeWindowSeconds { get; set; }
    public bool PersistAxisGroupAssignments { get; set; }
    public TrendAxisGroup[] AxisGroupAssignments { get; set; } =
    [
        TrendAxisGroup.Y1,
        TrendAxisGroup.Y1,
        TrendAxisGroup.Y1,
        TrendAxisGroup.Y1,
        TrendAxisGroup.Y2,
        TrendAxisGroup.Y2,
        TrendAxisGroup.Y2,
        TrendAxisGroup.Y2,
    ];
}
