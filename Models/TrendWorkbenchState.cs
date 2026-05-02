using System;
using System.Collections.Generic;

namespace AvaloniaApplication2.Models;

// 工作台的纯状态模型。
// 这个类型只保存数据，不做界面刷新、文件读写或 ScottPlot 调用。
// Coordinator 会修改这里的状态，ViewModel 再把这些状态转换为界面展示文本。
public sealed class TrendWorkbenchState
{
    public TrendMode Mode { get; set; } = TrendMode.Historical;
    public TrendLayoutMode LayoutMode { get; set; } = TrendLayoutMode.DualAxis;
    public List<TrendSeriesState> Series { get; } = new List<TrendSeriesState>();
    public AxisSelectionState AxisSelection { get; } = new AxisSelectionState();
    public TimeWindowState TimeWindow { get; } = new TimeWindowState();
    public CrosshairState Crosshair { get; } = new CrosshairState();
    public TrendAxisGroup[] AxisGroupAssignments { get; set; } = Array.Empty<TrendAxisGroup>();
    public TimeSpan RealtimeWindow { get; set; } = TimeSpan.FromHours(1);
    public TimeSpan SamplingInterval { get; set; } = TimeSpan.FromSeconds(1);
    public DateTime[] TimePoints { get; set; } = Array.Empty<DateTime>();
    public double[][] RawSeriesYValues { get; set; } = Array.Empty<double[]>();
    public double[][] DisplayedSeriesYValues { get; set; } = Array.Empty<double[]>();
}
