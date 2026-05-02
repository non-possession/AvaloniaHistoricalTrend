namespace AvaloniaApplication2.Models;

// 单条趋势变量的状态。
// 每个变量独立保存默认量程、自定义量程和刷子位置，
// 因此切换 Y1/Y2 当前变量时，未选中的变量曲线可以保持自己的显示尺度。
public sealed class TrendSeriesState
{
    public int Index { get; init; }
    public string Name { get; set; } = string.Empty;
    public NumericRange DefaultEngineeringRange { get; init; }
    public NumericRange? CustomEngineeringRange { get; set; }
    public double LowerBrushFraction { get; set; }
    public double UpperBrushFraction { get; set; } = 1;
    public bool IsVisible { get; set; } = true;
    public bool IsLeftAxisGroup { get; set; }
    public string ColorHex { get; init; } = "#FFFFFF";
}
