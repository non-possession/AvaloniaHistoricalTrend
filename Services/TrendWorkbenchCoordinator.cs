using System;
using System.Collections.Generic;
using System.Globalization;
using AvaloniaApplication2.Models;

namespace AvaloniaApplication2.Services;

// 趋势工作台的领域协调器。
// 本类集中处理业务规则：变量轴分组、Y1/Y2 选择、量程/刷子换算、
// 历史/实时窗口切换、采样数据装载和界面展示快照生成。
// 它不引用 Avalonia 控件、不读写文件、不直接调用 ScottPlot，便于单元测试和后续替换界面层。
public sealed class TrendWorkbenchCoordinator
{
    public const int PointCount = 50;
    public const double MinimumBrushGap = 0.02;
    public static readonly TimeSpan MinimumTimeSpan = TimeSpan.FromSeconds(10);

    private readonly NumericRange[] defaultEngineeringRanges =
    {
        new NumericRange(0, 2),
        new NumericRange(0, 12),
        new NumericRange(0, 14),
        new NumericRange(0, 16),
        new NumericRange(0, 18),
        new NumericRange(-20, 120),
        new NumericRange(0, 30),
        new NumericRange(0, 20),
    };

    public TrendWorkbenchState State { get; } = new TrendWorkbenchState();

    public TrendWorkbenchCoordinator()
    {
        string[] names =
        {
            "DPIT401PV",
            "P402EVAMP_PV",
            "P402BVAMP_PV",
            "P402AVAMP_PV",
            "P402DAMP_PV",
            "P402TEMP_PV",
            "P402FLOW_PV",
            "P402LEVEL_PV",
        };

        string[] colors =
        {
            "#6495ED",
            "#FFA500",
            "#3CB371",
            "#C71585",
            "#FFD700",
            "#008080",
            "#CD5C5C",
            "#9370DB",
        };

        for (int i = 0; i < names.Length; i++)
        {
            State.Series.Add(new TrendSeriesState
            {
                Index = i,
                Name = names[i],
                DefaultEngineeringRange = defaultEngineeringRanges[i],
                IsLeftAxisGroup = i < 4,
                ColorHex = colors[i],
            });
        }

        ApplyAxisGroupAssignments(CreateDefaultAxisGroups());

        State.TimePoints = GenerateTimePoints();
        State.RawSeriesYValues = new double[State.Series.Count][];
        State.DisplayedSeriesYValues = new double[State.Series.Count][];

        State.TimeWindow.TotalStart = State.TimePoints[0];
        State.TimeWindow.TotalEnd = State.TimePoints[State.TimePoints.Length - 1];
        State.TimeWindow.VisibleEnd = State.TimeWindow.TotalEnd;
        State.TimeWindow.VisibleStart = State.TimeWindow.TotalEnd - TimeSpan.FromDays(1);
        if (State.TimeWindow.VisibleStart < State.TimeWindow.TotalStart)
            State.TimeWindow.VisibleStart = State.TimeWindow.TotalStart;

        for (int i = 0; i < State.Series.Count; i++)
        {
            double[] values = GenerateSeries(i);
            State.RawSeriesYValues[i] = values;
            State.DisplayedSeriesYValues[i] = CopyValues(values);
        }

        ApplyStoredRangeFilters();
    }

    public void ConfigureRuntime(TimeSpan samplingInterval, TimeSpan realtimeWindow)
    {
        State.SamplingInterval = samplingInterval < TimeSpan.FromSeconds(1)
            ? TimeSpan.FromSeconds(1)
            : samplingInterval;
        State.RealtimeWindow = realtimeWindow < MinimumTimeSpan
            ? TimeSpan.FromHours(1)
            : realtimeWindow;
    }

    public void SetSamplingInterval(TimeSpan samplingInterval)
    {
        State.SamplingInterval = samplingInterval < TimeSpan.FromSeconds(1)
            ? TimeSpan.FromSeconds(1)
            : samplingInterval;
    }

    public void SetRealtimeWindow(TimeSpan realtimeWindow)
    {
        State.RealtimeWindow = realtimeWindow < MinimumTimeSpan
            ? MinimumTimeSpan
            : realtimeWindow;

        if (State.Mode == TrendMode.Realtime)
            SetRealtimeWindowToLatest();
    }

    public void ApplyAxisGroupAssignments(TrendAxisGroup[] assignments)
    {
        if (assignments.Length != State.Series.Count)
            assignments = CreateDefaultAxisGroups();

        bool hasY1 = false;
        bool hasY2 = false;
        for (int i = 0; i < assignments.Length; i++)
        {
            // 配置文件可能被人工改坏，这里必须保证双轴模式两侧都有变量可选。
            if (assignments[i] == TrendAxisGroup.Y1)
                hasY1 = true;
            if (assignments[i] == TrendAxisGroup.Y2)
                hasY2 = true;
        }

        if (!hasY1 || !hasY2)
            assignments = CreateDefaultAxisGroups();

        State.AxisGroupAssignments = (TrendAxisGroup[])assignments.Clone();
        for (int i = 0; i < State.Series.Count; i++)
            State.Series[i].IsLeftAxisGroup = State.AxisGroupAssignments[i] == TrendAxisGroup.Y1;

        EnsureSelectedAxesVisible();
    }

    public bool TrySetAxisGroupAssignment(int seriesIndex, TrendAxisGroup group, out string message)
    {
        message = string.Empty;
        if (seriesIndex < 0 || seriesIndex >= State.Series.Count)
        {
            message = "变量索引无效。";
            return false;
        }

        TrendAxisGroup currentGroup = State.AxisGroupAssignments[seriesIndex];
        if (currentGroup == group)
            return true;

        bool wouldHaveY1 = false;
        bool wouldHaveY2 = false;
        for (int i = 0; i < State.AxisGroupAssignments.Length; i++)
        {
            // 先模拟修改后的分组结果，防止用户把某一侧轴的变量全部移走。
            TrendAxisGroup nextGroup = i == seriesIndex ? group : State.AxisGroupAssignments[i];
            if (nextGroup == TrendAxisGroup.Y1)
                wouldHaveY1 = true;
            if (nextGroup == TrendAxisGroup.Y2)
                wouldHaveY2 = true;
        }

        if (!wouldHaveY1 || !wouldHaveY2)
        {
            message = "Y1 和 Y2 至少都要保留一个变量。";
            return false;
        }

        State.AxisGroupAssignments[seriesIndex] = group;
        State.Series[seriesIndex].IsLeftAxisGroup = group == TrendAxisGroup.Y1;
        EnsureSelectedAxesVisible();
        return true;
    }

    public TrendAxisGroup[] CreateDefaultAxisGroups()
    {
        TrendAxisGroup[] groups = new TrendAxisGroup[State.Series.Count];
        for (int i = 0; i < groups.Length; i++)
            groups[i] = i < 4 ? TrendAxisGroup.Y1 : TrendAxisGroup.Y2;

        return groups;
    }

    public int[] GetAxisCandidateIndices(bool isRightAxis)
    {
        TrendAxisGroup group = isRightAxis ? TrendAxisGroup.Y2 : TrendAxisGroup.Y1;
        List<int> indices = new List<int>();
        for (int i = 0; i < State.AxisGroupAssignments.Length; i++)
        {
            if (State.AxisGroupAssignments[i] == group)
                indices.Add(i);
        }

        int[] result = new int[indices.Count];
        for (int i = 0; i < indices.Count; i++)
            result[i] = indices[i];

        return result;
    }

    public int GetAxisOptionIndex(bool isRightAxis, int seriesIndex)
    {
        int[] indices = GetAxisCandidateIndices(isRightAxis);
        for (int i = 0; i < indices.Length; i++)
        {
            if (indices[i] == seriesIndex)
                return i;
        }

        return -1;
    }

    public int GetSeriesIndexFromAxisOption(bool isRightAxis, int optionIndex)
    {
        int[] indices = GetAxisCandidateIndices(isRightAxis);
        if (optionIndex < 0 || optionIndex >= indices.Length)
            return -1;

        return indices[optionIndex];
    }

    public DateTime[] GenerateTimePoints()
    {
        DateTime end = DateTime.Now;
        DateTime start = end.AddDays(-2);
        TimeSpan step = TimeSpan.FromTicks((end - start).Ticks / (PointCount - 1));
        DateTime[] points = new DateTime[PointCount];

        for (int i = 0; i < PointCount; i++)
            points[i] = start.AddTicks(step.Ticks * i);

        return points;
    }

    public double[] GenerateSeries(int seriesIndex)
    {
        double[] values = new double[PointCount];
        NumericRange range = defaultEngineeringRanges[seriesIndex];
        double midpoint = (range.Min + range.Max) / 2;
        double amplitude = range.Span * 0.32;
        double phase = seriesIndex * 0.38;

        for (int i = 0; i < PointCount; i++)
        {
            double wave = Math.Sin(i * 0.19 + phase) + Math.Cos(i * 0.07 + phase) * 0.25;
            values[i] = midpoint + amplitude * wave;
        }

        return values;
    }

    public List<TrendSample> CreateInitialSamples()
    {
        List<TrendSample> samples = new List<TrendSample>();
        for (int pointIndex = 0; pointIndex < State.TimePoints.Length; pointIndex++)
        {
            double[] values = new double[State.Series.Count];
            for (int seriesIndex = 0; seriesIndex < State.Series.Count; seriesIndex++)
                values[seriesIndex] = State.RawSeriesYValues[seriesIndex][pointIndex];

            samples.Add(new TrendSample
            {
                Timestamp = State.TimePoints[pointIndex],
                Values = values,
            });
        }

        return samples;
    }

    public void LoadSamples(List<TrendSample> samples)
    {
        if (samples.Count == 0)
            return;

        // 数据库返回的数据可能不是严格排序的，装载前统一按时间排序。
        samples.Sort((left, right) => left.Timestamp.CompareTo(right.Timestamp));

        State.TimePoints = new DateTime[samples.Count];
        State.RawSeriesYValues = new double[State.Series.Count][];
        State.DisplayedSeriesYValues = new double[State.Series.Count][];

        for (int seriesIndex = 0; seriesIndex < State.Series.Count; seriesIndex++)
        {
            State.RawSeriesYValues[seriesIndex] = new double[samples.Count];
            State.DisplayedSeriesYValues[seriesIndex] = new double[samples.Count];
        }

        for (int pointIndex = 0; pointIndex < samples.Count; pointIndex++)
        {
            TrendSample sample = samples[pointIndex];
            State.TimePoints[pointIndex] = sample.Timestamp;
            for (int seriesIndex = 0; seriesIndex < State.Series.Count; seriesIndex++)
            {
                double value = seriesIndex < sample.Values.Length ? sample.Values[seriesIndex] : double.NaN;
                State.RawSeriesYValues[seriesIndex][pointIndex] = value;
                State.DisplayedSeriesYValues[seriesIndex][pointIndex] = value;
            }
        }

        State.TimeWindow.TotalStart = State.TimePoints[0];
        State.TimeWindow.TotalEnd = State.TimePoints[State.TimePoints.Length - 1];
        EnsureValidTimeRange();
        ApplyStoredRangeFilters();
    }

    public void AppendRealtimeSample(TrendSample sample)
    {
        List<TrendSample> samples = CreateSamplesFromState();
        samples.Add(sample);
        // 复用 LoadSamples 可以保证实时追加和历史装载使用同一套数组重建逻辑。
        LoadSamples(samples);
        State.Mode = TrendMode.Realtime;
        SetRealtimeWindowToLatest();
    }

    public void SetHistoricalMode()
    {
        State.Mode = TrendMode.Historical;
        State.Crosshair.IsActive = false;
    }

    public void SetRealtimeMode()
    {
        State.Mode = TrendMode.Realtime;
        SetRealtimeWindowToLatest();
    }

    public void SetRealtimeWindowToLatest()
    {
        if (State.TimePoints.Length == 0)
            return;

        DateTime end = State.TimeWindow.TotalEnd;
        DateTime start = end - State.RealtimeWindow;
        if (start < State.TimeWindow.TotalStart)
            start = State.TimeWindow.TotalStart;

        SetVisibleWindow(start, end);
    }

    private List<TrendSample> CreateSamplesFromState()
    {
        List<TrendSample> samples = new List<TrendSample>();
        for (int pointIndex = 0; pointIndex < State.TimePoints.Length; pointIndex++)
        {
            double[] values = new double[State.Series.Count];
            for (int seriesIndex = 0; seriesIndex < State.Series.Count; seriesIndex++)
                values[seriesIndex] = State.RawSeriesYValues[seriesIndex][pointIndex];

            samples.Add(new TrendSample
            {
                Timestamp = State.TimePoints[pointIndex],
                Values = values,
            });
        }

        return samples;
    }

    public TrendSeriesState GetSeries(int seriesIndex)
    {
        return State.Series[seriesIndex];
    }

    public NumericRange GetEngineeringRange(int seriesIndex)
    {
        TrendSeriesState series = GetSeries(seriesIndex);
        return series.CustomEngineeringRange ?? series.DefaultEngineeringRange;
    }

    public NumericRange GetDisplayedYRange(int seriesIndex)
    {
        TrendSeriesState series = GetSeries(seriesIndex);
        NumericRange engineeringRange = GetEngineeringRange(seriesIndex);
        double lower = engineeringRange.Min + engineeringRange.Span * series.LowerBrushFraction;
        double upper = engineeringRange.Min + engineeringRange.Span * series.UpperBrushFraction;
        return new NumericRange(lower, upper);
    }

    public void ApplyStoredRangeFilters()
    {
        for (int seriesIndex = 0; seriesIndex < State.RawSeriesYValues.Length; seriesIndex++)
        {
            double[] raw = State.RawSeriesYValues[seriesIndex];
            double[] display = State.DisplayedSeriesYValues[seriesIndex];
            if (raw.Length == 0 || display.Length == 0)
                continue;

            NumericRange range = GetDisplayedYRange(seriesIndex);
            for (int i = 0; i < raw.Length; i++)
            {
                double value = raw[i];
                // 超出当前刷子显示量程的点用 NaN 截断，ScottPlot 会自动断开该段曲线。
                display[i] = value >= range.Min && value <= range.Max ? value : double.NaN;
            }
        }
    }

    public void EnsureValidTimeRange()
    {
        if (State.TimeWindow.VisibleStart < State.TimeWindow.TotalStart)
            State.TimeWindow.VisibleStart = State.TimeWindow.TotalStart;

        if (State.TimeWindow.VisibleEnd > State.TimeWindow.TotalEnd)
            State.TimeWindow.VisibleEnd = State.TimeWindow.TotalEnd;

        if (State.TimeWindow.VisibleEnd - State.TimeWindow.VisibleStart < MinimumTimeSpan)
        {
            // 任何窗口修改都必须保留最小跨度，否则图表和时间刷子会重叠失效。
            State.TimeWindow.VisibleEnd = State.TimeWindow.VisibleStart.Add(MinimumTimeSpan);
            if (State.TimeWindow.VisibleEnd > State.TimeWindow.TotalEnd)
            {
                State.TimeWindow.VisibleEnd = State.TimeWindow.TotalEnd;
                State.TimeWindow.VisibleStart = State.TimeWindow.VisibleEnd - MinimumTimeSpan;
            }
        }

        if (State.TimeWindow.VisibleStart < State.TimeWindow.TotalStart)
            State.TimeWindow.VisibleStart = State.TimeWindow.TotalStart;
    }

    public void SetSeriesVisible(int seriesIndex, bool isVisible)
    {
        GetSeries(seriesIndex).IsVisible = isVisible;
    }

    public void EnsureSelectedAxesVisible()
    {
        if (State.LayoutMode == TrendLayoutMode.SingleAxis)
        {
            int firstVisibleAny = FindVisibleSeriesIndex(GetAllSeriesIndices(), -1);
            if (firstVisibleAny < 0)
                return;

            if (!GetSeries(State.AxisSelection.LeftSeriesIndex).IsVisible)
                State.AxisSelection.LeftSeriesIndex = firstVisibleAny;

            return;
        }

        int firstVisibleLeft = FindVisibleSeriesIndex(GetAxisCandidateIndices(isRightAxis: false), -1);
        int firstVisibleRight = FindVisibleSeriesIndex(GetAxisCandidateIndices(isRightAxis: true), -1);

        if (firstVisibleLeft < 0 && firstVisibleRight < 0)
            return;

        if (!GetSeries(State.AxisSelection.LeftSeriesIndex).IsVisible || State.AxisGroupAssignments[State.AxisSelection.LeftSeriesIndex] != TrendAxisGroup.Y1)
            State.AxisSelection.LeftSeriesIndex = firstVisibleLeft >= 0 ? firstVisibleLeft : State.AxisSelection.LeftSeriesIndex;

        if (!GetSeries(State.AxisSelection.RightSeriesIndex).IsVisible || State.AxisGroupAssignments[State.AxisSelection.RightSeriesIndex] != TrendAxisGroup.Y2)
            State.AxisSelection.RightSeriesIndex = firstVisibleRight >= 0 ? firstVisibleRight : State.AxisSelection.RightSeriesIndex;
    }

    public int FindVisibleSeriesIndexInGroup(int startInclusive, int endExclusive, int excludedIndex)
    {
        for (int i = startInclusive; i < endExclusive; i++)
        {
            if (i != excludedIndex && GetSeries(i).IsVisible)
                return i;
        }

        return -1;
    }

    public int FindVisibleSeriesIndex(int[] candidateIndices, int excludedIndex)
    {
        for (int i = 0; i < candidateIndices.Length; i++)
        {
            int seriesIndex = candidateIndices[i];
            if (seriesIndex != excludedIndex && GetSeries(seriesIndex).IsVisible)
                return seriesIndex;
        }

        return -1;
    }

    public void SelectLeftSeries(int seriesIndex, bool resetBrushes = false)
    {
        if (seriesIndex < 0 || seriesIndex >= State.Series.Count)
            return;

        if (State.LayoutMode == TrendLayoutMode.DualAxis && State.AxisGroupAssignments[seriesIndex] != TrendAxisGroup.Y1)
            return;

        State.AxisSelection.LeftSeriesIndex = seriesIndex;
        if (resetBrushes)
            ResetBrushes(seriesIndex);
    }

    public void SelectRightSeries(int seriesIndex, bool resetBrushes = false)
    {
        if (seriesIndex < 0 || seriesIndex >= State.Series.Count)
            return;

        if (State.AxisGroupAssignments[seriesIndex] != TrendAxisGroup.Y2)
            return;

        State.AxisSelection.RightSeriesIndex = seriesIndex;
        if (resetBrushes)
            ResetBrushes(seriesIndex);
    }

    public void SetLayoutMode(TrendLayoutMode layoutMode)
    {
        State.LayoutMode = layoutMode;
    }

    public void CycleAxisSeries(bool isRightAxis)
    {
        if (!isRightAxis && State.LayoutMode == TrendLayoutMode.SingleAxis)
        {
            int singleAxisCurrentIndex = State.AxisSelection.LeftSeriesIndex;
            for (int offset = 1; offset <= State.Series.Count; offset++)
            {
                int candidate = (singleAxisCurrentIndex + offset) % State.Series.Count;
                if (!GetSeries(candidate).IsVisible)
                    continue;

                SelectLeftSeries(candidate);
                break;
            }

            return;
        }

        int currentIndex = isRightAxis ? State.AxisSelection.RightSeriesIndex : State.AxisSelection.LeftSeriesIndex;
        int[] candidates = GetAxisCandidateIndices(isRightAxis);
        if (candidates.Length == 0)
            return;

        int currentCandidateIndex = 0;
        for (int i = 0; i < candidates.Length; i++)
        {
            if (candidates[i] == currentIndex)
            {
                currentCandidateIndex = i;
                break;
            }
        }

        for (int offset = 1; offset <= candidates.Length; offset++)
        {
            int candidate = candidates[(currentCandidateIndex + offset) % candidates.Length];
            if (!GetSeries(candidate).IsVisible)
                continue;

            if (isRightAxis)
                SelectRightSeries(candidate);
            else
                SelectLeftSeries(candidate);

            break;
        }
    }

    private int[] GetAllSeriesIndices()
    {
        int[] indices = new int[State.Series.Count];
        for (int i = 0; i < indices.Length; i++)
            indices[i] = i;

        return indices;
    }

    public void RenameSeries(int index, string newName)
    {
        GetSeries(index).Name = string.IsNullOrWhiteSpace(newName)
            ? $"变量 {index + 1}"
            : newName.Trim();
    }

    public void ApplyCustomRange(bool isRightAxis, double min, double max)
    {
        if (min >= max)
            return;

        int index = isRightAxis ? State.AxisSelection.RightSeriesIndex : State.AxisSelection.LeftSeriesIndex;
        GetSeries(index).CustomEngineeringRange = new NumericRange(min, max);
        ResetBrushes(index);
    }

    public void ResetCustomRange(bool isRightAxis)
    {
        int index = isRightAxis ? State.AxisSelection.RightSeriesIndex : State.AxisSelection.LeftSeriesIndex;
        GetSeries(index).CustomEngineeringRange = null;
        ResetBrushes(index);
    }

    public void SetBrushFraction(int seriesIndex, bool isUpperBrush, double fraction)
    {
        TrendSeriesState series = GetSeries(seriesIndex);
        if (isUpperBrush)
            series.UpperBrushFraction = Clamp(fraction, series.LowerBrushFraction + MinimumBrushGap, 1);
        else
            series.LowerBrushFraction = Clamp(fraction, 0, series.UpperBrushFraction - MinimumBrushGap);
    }

    public void SetVisibleWindow(DateTime start, DateTime end)
    {
        State.TimeWindow.VisibleStart = start;
        State.TimeWindow.VisibleEnd = end;
    }

    public bool TrySetEditedStartTime(DateTime start, out string message)
    {
        return TrySetEditedWindow(start, State.TimeWindow.VisibleEnd, out message);
    }

    public bool TrySetEditedEndTime(DateTime end, out string message)
    {
        return TrySetEditedWindow(State.TimeWindow.VisibleStart, end, out message);
    }

    private bool TrySetEditedWindow(DateTime start, DateTime end, out string message)
    {
        message = string.Empty;
        if (start < State.TimeWindow.TotalStart || start > State.TimeWindow.TotalEnd)
        {
            message = $"起始时间不能超出数据范围 {State.TimeWindow.TotalStart:yyyy-MM-dd HH:mm:ss} ~ {State.TimeWindow.TotalEnd:yyyy-MM-dd HH:mm:ss}。";
            return false;
        }

        if (end < State.TimeWindow.TotalStart || end > State.TimeWindow.TotalEnd)
        {
            message = $"结束时间不能超出数据范围 {State.TimeWindow.TotalStart:yyyy-MM-dd HH:mm:ss} ~ {State.TimeWindow.TotalEnd:yyyy-MM-dd HH:mm:ss}。";
            return false;
        }

        if (start >= end)
        {
            message = "起始时间必须早于结束时间。";
            return false;
        }

        if (end - start < MinimumTimeSpan)
        {
            message = $"时间窗口不能小于 {FormatTimeSpan(MinimumTimeSpan)}。";
            return false;
        }

        // 手动编辑时间窗口表示用户正在查看历史数据，必须退出实时滚动语义。
        SetHistoricalMode();
        SetVisibleWindow(start, end);
        return true;
    }

    public void SetTimeSpanWindow(TimeSpan span)
    {
        if (span < MinimumTimeSpan)
            span = MinimumTimeSpan;

        DateTime center = State.TimeWindow.VisibleStart + TimeSpan.FromTicks((State.TimeWindow.VisibleEnd - State.TimeWindow.VisibleStart).Ticks / 2);
        DateTime newStart = center - TimeSpan.FromTicks(span.Ticks / 2);
        DateTime newEnd = newStart + span;

        if (newStart < State.TimeWindow.TotalStart)
        {
            newStart = State.TimeWindow.TotalStart;
            newEnd = newStart + span;
        }

        if (newEnd > State.TimeWindow.TotalEnd)
        {
            newEnd = State.TimeWindow.TotalEnd;
            newStart = newEnd - span;
        }

        SetVisibleWindow(newStart, newEnd);
    }

    public void SetDurationPreset(TimeSpan span)
    {
        if (State.Mode == TrendMode.Realtime)
        {
            SetRealtimeWindow(span);
            return;
        }

        SetTimeSpanWindow(span);
    }

    public void ShiftWindow(TimeSpan delta)
    {
        TimeSpan span = State.TimeWindow.VisibleEnd - State.TimeWindow.VisibleStart;
        DateTime newStart = State.TimeWindow.VisibleStart.Add(delta);
        DateTime newEnd = State.TimeWindow.VisibleEnd.Add(delta);

        if (newStart < State.TimeWindow.TotalStart)
        {
            newStart = State.TimeWindow.TotalStart;
            newEnd = newStart + span;
        }

        if (newEnd > State.TimeWindow.TotalEnd)
        {
            newEnd = State.TimeWindow.TotalEnd;
            newStart = newEnd - span;
        }

        SetVisibleWindow(newStart, newEnd);
    }

    public double NormalizeTime(DateTime value)
    {
        double totalTicks = (State.TimeWindow.TotalEnd - State.TimeWindow.TotalStart).Ticks;
        if (totalTicks <= 0)
            return 0;

        return (value - State.TimeWindow.TotalStart).Ticks / totalTicks;
    }

    public DateTime DenormalizeTime(double normalized)
    {
        normalized = Clamp(normalized, 0, 1);
        long ticks = (long)((State.TimeWindow.TotalEnd - State.TimeWindow.TotalStart).Ticks * normalized);
        return State.TimeWindow.TotalStart.AddTicks(ticks);
    }

    public double GetSeriesValueAtTime(int seriesIndex, DateTime targetTime)
    {
        if (State.RawSeriesYValues.Length <= seriesIndex || State.TimePoints.Length == 0)
            return 0;

        int nearestIndex = 0;
        long nearestDistance = long.MaxValue;

        for (int i = 0; i < State.TimePoints.Length; i++)
        {
            long distance = Math.Abs((State.TimePoints[i] - targetTime).Ticks);
            if (distance < nearestDistance)
            {
                nearestDistance = distance;
                nearestIndex = i;
            }
        }

        return State.RawSeriesYValues[seriesIndex][nearestIndex];
    }

    public string FormatTimeSpan(TimeSpan span)
    {
        if (span.TotalDays >= 1)
            return $"{span.TotalDays:0.#} Day";

        if (span.TotalHours >= 1)
            return $"{span.TotalHours:0.#} H";

        if (span.TotalMinutes >= 1)
            return $"{span.TotalMinutes:0} Min";

        return $"{span.TotalSeconds:0} S";
    }

    public string[] GetSeriesNames()
    {
        string[] names = new string[State.Series.Count];
        for (int i = 0; i < State.Series.Count; i++)
            names[i] = State.Series[i].Name;

        return names;
    }

    public string[] GetSeriesColorHexes()
    {
        string[] colors = new string[State.Series.Count];
        for (int i = 0; i < State.Series.Count; i++)
            colors[i] = State.Series[i].ColorHex;

        return colors;
    }

    public string GetAxisStatusText()
    {
        NumericRange leftEngineering = GetEngineeringRange(State.AxisSelection.LeftSeriesIndex);
        NumericRange leftDisplayed = GetDisplayedYRange(State.AxisSelection.LeftSeriesIndex);

        if (State.LayoutMode == TrendLayoutMode.SingleAxis)
            return $"当前量程 | 单轴 {leftEngineering.Min:0.##}~{leftEngineering.Max:0.##} -> {leftDisplayed.Min:0.##}~{leftDisplayed.Max:0.##}";

        NumericRange rightEngineering = GetEngineeringRange(State.AxisSelection.RightSeriesIndex);
        NumericRange rightDisplayed = GetDisplayedYRange(State.AxisSelection.RightSeriesIndex);

        return
            $"当前量程 | Y1 {leftEngineering.Min:0.##}~{leftEngineering.Max:0.##} -> {leftDisplayed.Min:0.##}~{leftDisplayed.Max:0.##} | " +
            $"Y2 {rightEngineering.Min:0.##}~{rightEngineering.Max:0.##} -> {rightDisplayed.Min:0.##}~{rightDisplayed.Max:0.##}";
    }

    public string GetZoomStatusText()
    {
        TimeSpan currentSpan = State.TimeWindow.VisibleEnd - State.TimeWindow.VisibleStart;
        if (State.LayoutMode == TrendLayoutMode.SingleAxis)
            return $"当前缩放 | 时间窗口 {FormatTimeSpan(currentSpan)} | 单轴模式 | Y={GetSeries(State.AxisSelection.LeftSeriesIndex).Name}";

        return $"当前缩放 | 时间窗口 {FormatTimeSpan(currentSpan)} | 双轴模式 | Y1={GetSeries(State.AxisSelection.LeftSeriesIndex).Name} | Y2={GetSeries(State.AxisSelection.RightSeriesIndex).Name}";
    }

    public string GetViewportStatusText()
    {
        return $"{State.TimeWindow.VisibleStart:MM-dd HH:mm:ss} -> {State.TimeWindow.VisibleEnd:MM-dd HH:mm:ss}";
    }

    public string GetTimeRangeStatusText()
    {
        return $"总范围 {State.TimeWindow.TotalStart:MM-dd HH:mm} ~ {State.TimeWindow.TotalEnd:MM-dd HH:mm}";
    }

    public string GetTrendModeText()
    {
        return State.Mode == TrendMode.Realtime ? "实时曲线" : "历史曲线";
    }

    public string GetCrosshairStatusText()
    {
        DateTime statusTime = State.Crosshair.IsActive ? State.Crosshair.HoveredTime : State.TimeWindow.VisibleEnd;
        int leftIndex = State.AxisSelection.LeftSeriesIndex;
        double leftValue = GetSeriesValueAtTime(leftIndex, statusTime);
        if (!State.Crosshair.IsActive)
            return string.Empty;

        string prefix = $"十字光标 {statusTime:MM-dd HH:mm:ss}";

        if (State.LayoutMode == TrendLayoutMode.SingleAxis)
            return $"{prefix} | Y {GetSeries(leftIndex).Name}={leftValue:0.##}";

        int rightIndex = State.AxisSelection.RightSeriesIndex;
        double rightValue = GetSeriesValueAtTime(rightIndex, statusTime);
        return $"{prefix} | Y1 {GetSeries(leftIndex).Name}={leftValue:0.##} | Y2 {GetSeries(rightIndex).Name}={rightValue:0.##}";
    }

    public TrendWorkbenchPresentationSnapshot BuildPresentationSnapshot()
    {
        // 快照是 ViewModel 的唯一输入，避免 ViewModel 重新计算领域规则。
        return new TrendWorkbenchPresentationSnapshot
        {
            ZoomStatus = GetZoomStatusText(),
            ViewportStatus = GetViewportStatusText(),
            TimeRangeStatus = GetTimeRangeStatusText(),
            YAxisStatus = GetAxisStatusText(),
            CrosshairStatus = GetCrosshairStatusText(),
            LeftTimeText = State.TimeWindow.VisibleStart.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
            RightTimeText = State.TimeWindow.VisibleEnd.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
            BottomLeftRangeText = State.TimeWindow.VisibleStart.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
            BottomRightRangeText = State.TimeWindow.VisibleEnd.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
            SelectedDurationText = FormatTimeSpan(State.TimeWindow.VisibleEnd - State.TimeWindow.VisibleStart),
            TrendModeText = GetTrendModeText(),
            DatabasePathText = string.Empty,
            SamplingIntervalText = $"{State.SamplingInterval.TotalSeconds:0}",
            IsHistoricalMode = State.Mode == TrendMode.Historical,
            IsRealtimeMode = State.Mode == TrendMode.Realtime,
            LayoutMode = State.LayoutMode,
            IsSingleAxisMode = State.LayoutMode == TrendLayoutMode.SingleAxis,
            IsDualAxisMode = State.LayoutMode == TrendLayoutMode.DualAxis,
            LeftAxisOptions = State.LayoutMode == TrendLayoutMode.SingleAxis ? GetSeriesNames() : GetAxisSeriesNames(isRightAxis: false),
            RightAxisOptions = GetAxisSeriesNames(isRightAxis: true),
            LeftAxisPanel = BuildAxisPanelPresentation(State.AxisSelection.LeftSeriesIndex),
            RightAxisPanel = BuildAxisPanelPresentation(State.AxisSelection.RightSeriesIndex),
            TimeHeaders = BuildTimeHeaders(),
            SeriesCards = BuildSeriesCards(),
            DurationButtons = BuildDurationButtons(),
        };
    }

    private AxisPanelPresentation BuildAxisPanelPresentation(int seriesIndex)
    {
        TrendSeriesState series = GetSeries(seriesIndex);
        NumericRange range = GetEngineeringRange(seriesIndex);
        string rangeMode = series.CustomEngineeringRange.HasValue ? "自定义" : "默认";

        return new AxisPanelPresentation
        {
            CurrentSeriesText = $"当前参考变量: {series.Name}",
            RangeText = $"{series.Name}{Environment.NewLine}{range.Min:0.##} ~ {range.Max:0.##} ({rangeMode})",
            AccentHex = series.ColorHex,
            SelectedIndex = State.LayoutMode == TrendLayoutMode.SingleAxis
                ? seriesIndex
                : GetAxisOptionIndex(seriesIndex >= 0 && State.AxisGroupAssignments[seriesIndex] == TrendAxisGroup.Y2, seriesIndex),
            MinRangeText = range.Min.ToString("0.##", CultureInfo.InvariantCulture),
            MaxRangeText = range.Max.ToString("0.##", CultureInfo.InvariantCulture),
        };
    }

    private string[] GetAxisSeriesNames(bool isRightAxis)
    {
        int[] indices = GetAxisCandidateIndices(isRightAxis);
        string[] names = new string[indices.Length];
        for (int i = 0; i < indices.Length; i++)
            names[i] = State.Series[indices[i]].Name;

        return names;
    }

    private TimeHeaderPresentation[] BuildTimeHeaders()
    {
        TimeHeaderPresentation[] headers = new TimeHeaderPresentation[5];
        TimeSpan span = State.TimeWindow.VisibleEnd - State.TimeWindow.VisibleStart;

        for (int i = 0; i < headers.Length; i++)
        {
            double fraction = headers.Length == 1 ? 0 : i / (double)(headers.Length - 1);
            DateTime tickTime = State.TimeWindow.VisibleStart.AddTicks((long)(span.Ticks * fraction));
            headers[i] = new TimeHeaderPresentation
            {
                DateText = tickTime.ToString("MM-dd", CultureInfo.InvariantCulture),
                TimeText = tickTime.ToString("HH:mm:ss", CultureInfo.InvariantCulture),
            };
        }

        return headers;
    }

    private SeriesCardPresentation[] BuildSeriesCards()
    {
        DateTime targetTime = State.Crosshair.IsActive ? State.Crosshair.HoveredTime : State.TimeWindow.VisibleEnd;
        string timeLabelPrefix = State.Crosshair.IsActive ? "光标" : "末端";
        SeriesCardPresentation[] cards = new SeriesCardPresentation[State.Series.Count];

        for (int i = 0; i < State.Series.Count; i++)
        {
            TrendSeriesState series = State.Series[i];
            double currentValue = GetSeriesValueAtTime(i, targetTime);
            string hiddenSuffix = series.IsVisible ? string.Empty : " (隐藏)";
            cards[i] = new SeriesCardPresentation
            {
                Name = series.Name,
                ValueText = $"{timeLabelPrefix} {currentValue:0.##}{hiddenSuffix}",
                IsVisible = series.IsVisible,
                AccentHex = series.ColorHex,
                IsLeftSelected = i == State.AxisSelection.LeftSeriesIndex,
                IsRightSelected = State.LayoutMode == TrendLayoutMode.DualAxis && i == State.AxisSelection.RightSeriesIndex && i != State.AxisSelection.LeftSeriesIndex,
            };
        }

        return cards;
    }

    private DurationButtonPresentation[] BuildDurationButtons()
    {
        TimeSpan[] presetDurations =
        {
            TimeSpan.FromDays(1),
            TimeSpan.FromHours(12),
            TimeSpan.FromHours(6),
            TimeSpan.FromHours(3),
            TimeSpan.FromHours(2),
            TimeSpan.FromHours(1),
            TimeSpan.FromMinutes(30),
            TimeSpan.FromMinutes(10),
            TimeSpan.FromMinutes(5),
            TimeSpan.FromMinutes(2),
            TimeSpan.FromMinutes(1),
            TimeSpan.FromSeconds(30),
            TimeSpan.FromSeconds(10),
        };

        TimeSpan currentSpan = State.TimeWindow.VisibleEnd - State.TimeWindow.VisibleStart;
        DurationButtonPresentation[] buttons = new DurationButtonPresentation[presetDurations.Length];

        for (int i = 0; i < presetDurations.Length; i++)
        {
            buttons[i] = new DurationButtonPresentation
            {
                IsActive = Math.Abs((currentSpan - presetDurations[i]).TotalSeconds) < 1,
            };
        }

        return buttons;
    }

    private void ResetBrushes(int seriesIndex)
    {
        TrendSeriesState series = GetSeries(seriesIndex);
        series.LowerBrushFraction = 0;
        series.UpperBrushFraction = 1;
    }

    private static double[] CopyValues(double[] source)
    {
        double[] copy = new double[source.Length];
        Array.Copy(source, copy, source.Length);
        return copy;
    }

    private static double Clamp(double value, double min, double max)
    {
        if (min > max)
            return value;

        if (value < min)
            return min;

        if (value > max)
            return max;

        return value;
    }
}
