using System;
using System.IO;
using Avalonia;
using Avalonia.Media;
using AvaloniaApplication2.Models;
using ScottPlot;
using ScottPlot.AxisPanels;
using ScottPlot.Avalonia;
using ScottPlot.Plottables;
using ScottPlotColor = ScottPlot.Color;
using ScottPlotColors = ScottPlot.Colors;

namespace AvaloniaApplication2.Services;

// ScottPlot 图表适配器。
// 本类隔离 ScottPlot 的曲线、坐标轴、像素坐标换算和图片保存细节。
// View 只把控件和鼠标坐标交给它，Coordinator/ViewModel 不直接依赖 ScottPlot API。
public sealed class ScottPlotTrendAdapter
{
    private readonly AvaPlot avaPlot;
    private readonly bool showLegend;
    private Scatter[] scatters = Array.Empty<Scatter>();
    private IYAxis[] seriesYAxes = Array.Empty<IYAxis>();
    private IYAxis[] leftSeriesYAxes = Array.Empty<IYAxis>();
    private IYAxis[] rightSeriesYAxes = Array.Empty<IYAxis>();
    private TrendLayoutMode currentLayoutMode = TrendLayoutMode.DualAxis;
    private int currentPointCount;

    private readonly MarkerShape[] markerShapes =
    {
        MarkerShape.FilledCircle,
        MarkerShape.OpenCircle,
        MarkerShape.FilledSquare,
        MarkerShape.OpenSquare,
        MarkerShape.FilledDiamond,
        MarkerShape.OpenDiamond,
        MarkerShape.FilledTriangleUp,
        MarkerShape.OpenTriangleDown,
    };

    private readonly ScottPlotColor[] seriesColors =
    {
        ScottPlotColors.CornflowerBlue,
        ScottPlotColors.Orange,
        ScottPlotColors.MediumSeaGreen,
        ScottPlotColors.MediumVioletRed,
        ScottPlotColors.Gold,
        ScottPlotColors.Teal,
        ScottPlotColors.IndianRed,
        ScottPlotColors.MediumPurple,
    };

    public ScottPlotTrendAdapter(AvaPlot avaPlot, bool showLegend)
    {
        this.avaPlot = avaPlot;
        this.showLegend = showLegend;
    }

    public void Initialize(TrendWorkbenchState state)
    {
        avaPlot.Plot.Clear();
        scatters = new Scatter[state.Series.Count];
        seriesYAxes = new IYAxis[state.Series.Count];
        leftSeriesYAxes = new IYAxis[state.Series.Count];
        rightSeriesYAxes = new IYAxis[state.Series.Count];
        currentPointCount = state.TimePoints.Length;

        ConfigurePlotFonts();
        InitializePerSeriesAxes();

        RebuildScatters(state);

        avaPlot.Plot.Axes.DateTimeTicksBottom();
        avaPlot.Plot.Title("历史曲线 Demo");
        avaPlot.Plot.XLabel("时间");
        avaPlot.Plot.Axes.Right.IsVisible = true;

        if (showLegend)
            avaPlot.Plot.ShowLegend();
        else
            avaPlot.Plot.HideLegend();
    }

    public void ApplyState(TrendWorkbenchState state)
    {
        if (currentPointCount != state.TimePoints.Length)
            RebuildScatters(state);

        // 渲染顺序很重要：先决定曲线使用哪根轴，再更新样式、可见性和坐标范围。
        currentLayoutMode = state.LayoutMode;
        UpdateAxisAssignments(state);
        UpdateScatterMetadata(state);
        UpdateVisibility(state);
        UpdateSeriesEmphasis(state);
        UpdatePerSeriesAxisLimits(state);
        UpdateVisibleAxisPresentation(state);
        avaPlot.Plot.Axes.SetLimitsX(
            left: state.TimeWindow.VisibleStart.ToOADate(),
            right: state.TimeWindow.VisibleEnd.ToOADate());
    }

    public void Refresh()
    {
        avaPlot.Refresh();
    }

    public void SavePng(string filePath, int width, int height)
    {
        avaPlot.Plot.SavePng(filePath, width, height);
    }

    public bool TryGetCrosshair(Point position, Size canvasSize, out CrosshairOverlay overlay)
    {
        overlay = default;
        ScottPlot.PixelRect dataRect = avaPlot.Plot.RenderManager.LastRender.DataRect;
        double left = Clamp(dataRect.Left, 0, canvasSize.Width);
        double right = Clamp(dataRect.Right, 0, canvasSize.Width);
        double top = Clamp(dataRect.Top, 0, canvasSize.Height);
        double bottom = Clamp(dataRect.Bottom, 0, canvasSize.Height);

        if (right <= left || bottom <= top)
            return false;

        if (position.X < left || position.X > right || position.Y < top || position.Y > bottom)
            return false;

        // 十字光标只能画在 ScottPlot 的数据矩形内，避免线段穿出画布干扰用户。
        double x = Clamp(position.X, left, right);
        double y = Clamp(position.Y, top, bottom);
        Coordinates coordinates = avaPlot.Plot.GetCoordinates(
            (float)x,
            (float)y,
            avaPlot.Plot.Axes.Bottom,
            avaPlot.Plot.Axes.Left);

        overlay = new CrosshairOverlay(
            new Point(x, top),
            new Point(x, bottom),
            new Point(left, y),
            new Point(right, y),
            DateTime.FromOADate(coordinates.X),
            coordinates.Y);
        return true;
    }

    public bool TryGetBrushGuideLine(int seriesIndex, double brushFraction, Size canvasSize, NumericRange engineeringRange, out GuideLineOverlay overlay)
    {
        overlay = default;
        ScottPlot.PixelRect dataRect = avaPlot.Plot.RenderManager.LastRender.DataRect;
        double left = Clamp(dataRect.Left, 0, canvasSize.Width);
        double right = Clamp(dataRect.Right, 0, canvasSize.Width);
        double top = Clamp(dataRect.Top, 0, canvasSize.Height);
        double bottom = Clamp(dataRect.Bottom, 0, canvasSize.Height);

        if (right <= left || bottom <= top)
            return false;

        // brushFraction 是工程量程内的比例，需要通过对应 Y 轴换算成屏幕像素。
        double yValue = engineeringRange.Min + engineeringRange.Span * brushFraction;
        IYAxis guideAxis = currentLayoutMode == TrendLayoutMode.SingleAxis
            ? avaPlot.Plot.Axes.Left
            : seriesYAxes[seriesIndex];
        Pixel pixel = avaPlot.Plot.GetPixel(
            new Coordinates(avaPlot.Plot.Axes.Bottom.Min, yValue),
            avaPlot.Plot.Axes.Bottom,
            guideAxis);

        double y = Clamp(pixel.Y, top, bottom);
        overlay = new GuideLineOverlay(new Point(left, y), new Point(right, y), ToMediaBrush(seriesColors[seriesIndex]));
        return true;
    }

    private void UpdateScatterMetadata(TrendWorkbenchState state)
    {
        for (int i = 0; i < state.Series.Count; i++)
            scatters[i].LegendText = state.Series[i].Name;
    }

    private void RebuildScatters(TrendWorkbenchState state)
    {
        for (int i = 0; i < scatters.Length; i++)
        {
            if (scatters[i] != null)
                avaPlot.Plot.Remove(scatters[i]);
        }

        double[] dataX = BuildDateTimeXs(state.TimePoints);
        scatters = new Scatter[state.Series.Count];
        currentPointCount = state.TimePoints.Length;

        for (int i = 0; i < state.Series.Count; i++)
        {
            TrendSeriesState series = state.Series[i];
            Scatter scatter = avaPlot.Plot.Add.Scatter(dataX, state.DisplayedSeriesYValues[i]);
            scatter.LegendText = series.Name;
            scatter.MarkerShape = markerShapes[i];
            scatter.MarkerSize = 8;
            scatter.LineWidth = 2;
            scatter.Color = seriesColors[i];
            scatter.Axes.YAxis = seriesYAxes[i];
            scatters[i] = scatter;
        }
    }

    private void UpdateVisibility(TrendWorkbenchState state)
    {
        for (int i = 0; i < scatters.Length; i++)
            scatters[i].IsVisible = state.Series[i].IsVisible;
    }

    private void UpdateSeriesEmphasis(TrendWorkbenchState state)
    {
        for (int i = 0; i < scatters.Length; i++)
        {
            bool isSelected = i == state.AxisSelection.LeftSeriesIndex ||
                (state.LayoutMode == TrendLayoutMode.DualAxis && i == state.AxisSelection.RightSeriesIndex);
            scatters[i].LineWidth = isSelected ? 2.6f : 1.6f;
            scatters[i].MarkerSize = isSelected ? 8 : 5;
        }
    }

    private void UpdateAxisAssignments(TrendWorkbenchState state)
    {
        if (state.LayoutMode == TrendLayoutMode.SingleAxis)
        {
            IYAxis sharedAxis = avaPlot.Plot.Axes.Left;
            for (int i = 0; i < scatters.Length; i++)
                scatters[i].Axes.YAxis = sharedAxis;
            return;
        }

        for (int i = 0; i < scatters.Length; i++)
        {
            // 双轴模式下每条曲线仍保留自己的轴对象，避免切换参考变量时其他曲线跳动。
            seriesYAxes[i] = state.AxisGroupAssignments[i] == TrendAxisGroup.Y1
                ? leftSeriesYAxes[i]
                : rightSeriesYAxes[i];
            scatters[i].Axes.YAxis = seriesYAxes[i];
        }
    }

    private void UpdatePerSeriesAxisLimits(TrendWorkbenchState state)
    {
        if (state.LayoutMode == TrendLayoutMode.SingleAxis)
        {
            NumericRange range = GetDisplayedYRange(state, state.AxisSelection.LeftSeriesIndex);
            avaPlot.Plot.Axes.SetLimitsY(range.Min, range.Max, avaPlot.Plot.Axes.Left);
            return;
        }

        for (int i = 0; i < seriesYAxes.Length; i++)
        {
            NumericRange range = GetDisplayedYRange(state, i);
            avaPlot.Plot.Axes.SetLimitsY(range.Min, range.Max, seriesYAxes[i]);
        }
    }

    private void UpdateVisibleAxisPresentation(TrendWorkbenchState state)
    {
        if (state.LayoutMode == TrendLayoutMode.SingleAxis)
        {
            HideAllPerSeriesAxes();
            ConfigureAxisAppearance(
                axis: avaPlot.Plot.Axes.Left,
                labelText: $"Y {state.Series[state.AxisSelection.LeftSeriesIndex].Name}",
                axisColor: seriesColors[state.AxisSelection.LeftSeriesIndex],
                isVisibleAxis: true);
            return;
        }

        HideAllPerSeriesAxes();

        int leftIndex = state.AxisSelection.LeftSeriesIndex;
        int rightIndex = state.AxisSelection.RightSeriesIndex;
        ConfigureAxisAppearance(
            axis: leftSeriesYAxes[leftIndex],
            labelText: $"Y1 {state.Series[leftIndex].Name}",
            axisColor: seriesColors[leftIndex],
            isVisibleAxis: true);
        ConfigureAxisAppearance(
            axis: rightSeriesYAxes[rightIndex],
            labelText: $"Y2 {state.Series[rightIndex].Name}",
            axisColor: seriesColors[rightIndex],
            isVisibleAxis: true);
    }

    private void HideAllPerSeriesAxes()
    {
        for (int i = 0; i < leftSeriesYAxes.Length; i++)
        {
            ConfigureAxisAppearance(leftSeriesYAxes[i], string.Empty, seriesColors[i], isVisibleAxis: false);
            ConfigureAxisAppearance(rightSeriesYAxes[i], string.Empty, seriesColors[i], isVisibleAxis: false);
        }
    }

    private NumericRange GetDisplayedYRange(TrendWorkbenchState state, int seriesIndex)
    {
        TrendSeriesState series = state.Series[seriesIndex];
        NumericRange engineeringRange = series.CustomEngineeringRange ?? series.DefaultEngineeringRange;
        double lower = engineeringRange.Min + engineeringRange.Span * series.LowerBrushFraction;
        double upper = engineeringRange.Min + engineeringRange.Span * series.UpperBrushFraction;
        return new NumericRange(lower, upper);
    }

    private void ConfigurePlotFonts()
    {
        RegisterWindowsChineseFonts();

        string[] fontCandidates =
        {
            "Microsoft YaHei",
            "SimHei",
            "PingFang SC",
            "Hiragino Sans GB",
            "Noto Sans CJK SC",
        };

        string selectedFont = Fonts.Default;
        for (int i = 0; i < fontCandidates.Length; i++)
        {
            string fontName = fontCandidates[i];
            if (Fonts.GetTypeface(fontName, false, false) is not null)
            {
                selectedFont = fontName;
                break;
            }
        }

        avaPlot.Plot.Font.Set(selectedFont);
    }

    private static void RegisterWindowsChineseFonts()
    {
        RegisterFontFile("Microsoft YaHei", @"C:\Windows\Fonts\msyh.ttc");
        RegisterFontFile("SimHei", @"C:\Windows\Fonts\simhei.ttf");
    }

    private static void RegisterFontFile(string fontName, string fontPath)
    {
        if (!File.Exists(fontPath))
            return;

        try
        {
            // ScottPlot uses its own Skia font resolver, so Avalonia's FontFamily
            // setting does not guarantee Chinese text can render inside the plot.
            Fonts.AddFontFile(fontName, fontPath, bold: false, italic: false);
        }
        catch
        {
            // Font registration is a compatibility helper. If a font file is not
            // accepted by the current platform, the next candidate can still work.
        }
    }

    private void InitializePerSeriesAxes()
    {
        // 第 0 根左/右轴复用 ScottPlot 默认轴，后续变量再按需新增轴对象。
        leftSeriesYAxes[0] = avaPlot.Plot.Axes.Left;
        rightSeriesYAxes[0] = avaPlot.Plot.Axes.Right;
        seriesYAxes[0] = leftSeriesYAxes[0];

        for (int i = 1; i < seriesYAxes.Length; i++)
        {
            leftSeriesYAxes[i] = avaPlot.Plot.Axes.AddLeftAxis();
            rightSeriesYAxes[i] = avaPlot.Plot.Axes.AddRightAxis();
            seriesYAxes[i] = leftSeriesYAxes[i];
        }
    }

    private static void ConfigureAxisAppearance(IAxis axis, string labelText, ScottPlotColor axisColor, bool isVisibleAxis)
    {
        if (axis is not AxisBase axisBase)
            return;

        axisBase.MinimumSize = isVisibleAxis ? 42 : 0;
        axisBase.IsVisible = isVisibleAxis;
        axisBase.LabelText = labelText;
        axisBase.LabelFontColor = axisColor;
        axisBase.MajorTickStyle.Color = axisColor;
        axisBase.MinorTickStyle.Color = axisColor;
        axisBase.TickLabelStyle.ForeColor = axisColor;
        axisBase.FrameLineStyle.Color = axisColor;
    }

    private static SolidColorBrush ToMediaBrush(ScottPlotColor color)
    {
        return new SolidColorBrush(Avalonia.Media.Color.FromArgb(color.Alpha, color.Red, color.Green, color.Blue));
    }

    private static double[] BuildDateTimeXs(DateTime[] timePoints)
    {
        double[] values = new double[timePoints.Length];
        for (int i = 0; i < timePoints.Length; i++)
            values[i] = timePoints[i].ToOADate();

        return values;
    }

    private static double Clamp(double value, double min, double max)
    {
        if (value < min)
            return min;
        if (value > max)
            return max;
        return value;
    }
}

public readonly struct CrosshairOverlay
{
    public CrosshairOverlay(
        Point verticalStart,
        Point verticalEnd,
        Point horizontalStart,
        Point horizontalEnd,
        DateTime hoveredTime,
        double hoveredY)
    {
        VerticalStart = verticalStart;
        VerticalEnd = verticalEnd;
        HorizontalStart = horizontalStart;
        HorizontalEnd = horizontalEnd;
        HoveredTime = hoveredTime;
        HoveredY = hoveredY;
    }

    public Point VerticalStart { get; }
    public Point VerticalEnd { get; }
    public Point HorizontalStart { get; }
    public Point HorizontalEnd { get; }
    public DateTime HoveredTime { get; }
    public double HoveredY { get; }
}

public readonly struct GuideLineOverlay
{
    public GuideLineOverlay(Point start, Point end, IBrush brush)
    {
        Start = start;
        End = end;
        Brush = brush;
    }

    public Point Start { get; }
    public Point End { get; }
    public IBrush Brush { get; }
}
