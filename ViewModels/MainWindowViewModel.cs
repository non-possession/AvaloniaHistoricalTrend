using System;
using System.IO;
using Avalonia;
using Avalonia.Media;
using AvaloniaApplication2.Models;
using AvaloniaApplication2.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.ObjectModel;

namespace AvaloniaApplication2.ViewModels;

// 主窗口 ViewModel。
// 这里承接页面级动作入口，并把 Coordinator 生成的快照转换为可绑定属性。
// ViewModel 不直接操作 Avalonia 控件，也不直接读写数据库或调用 ScottPlot。
public partial class MainWindowViewModel : ViewModelBase
{
    private TrendWorkbenchApplicationService? applicationService;

    public string Greeting { get; } = "当前支持 8 条曲线的独立显隐、X/Y 轴缩放，以及通过水平/竖直滑块查看当前缩放下的数据窗口。";

    [ObservableProperty]
    private string zoomStatus = "当前缩放: X=1x, Y=1x";

    [ObservableProperty]
    private string viewportStatus = string.Empty;

    [ObservableProperty]
    private string timeRangeStatus = string.Empty;

    [ObservableProperty]
    private string yAxisStatus = "Y 量程 0 ~ 1";

    [ObservableProperty]
    private string crosshairStatus = string.Empty;

    [ObservableProperty]
    private string leftTimeText = "--";

    [ObservableProperty]
    private string rightTimeText = "--";

    [ObservableProperty]
    private string bottomLeftRangeText = "--";

    [ObservableProperty]
    private string bottomRightRangeText = "--";

    [ObservableProperty]
    private string selectedDurationText = "1 Day";

    [ObservableProperty]
    private string trendModeText = "实时曲线";

    [ObservableProperty]
    private string databasePathText = string.Empty;

    [ObservableProperty]
    private string samplingIntervalText = "1";

    [ObservableProperty]
    private string exportStatusText = "导出、保存图片、打印结果会显示在这里。";

    [ObservableProperty]
    private bool isSingleAxisMode;

    [ObservableProperty]
    private bool isDualAxisMode = true;

    public AxisPanelViewModel LeftAxisPanel { get; } = new AxisPanelViewModel();
    public AxisPanelViewModel RightAxisPanel { get; } = new AxisPanelViewModel();
    public ObservableCollection<string> LeftAxisOptions { get; } = new ObservableCollection<string>();
    public ObservableCollection<string> RightAxisOptions { get; } = new ObservableCollection<string>();

    public TimeHeaderItemViewModel TimeHeader1 { get; } = new TimeHeaderItemViewModel();
    public TimeHeaderItemViewModel TimeHeader2 { get; } = new TimeHeaderItemViewModel();
    public TimeHeaderItemViewModel TimeHeader3 { get; } = new TimeHeaderItemViewModel();
    public TimeHeaderItemViewModel TimeHeader4 { get; } = new TimeHeaderItemViewModel();
    public TimeHeaderItemViewModel TimeHeader5 { get; } = new TimeHeaderItemViewModel();

    public SeriesCardItemViewModel Series1 { get; } = new SeriesCardItemViewModel();
    public SeriesCardItemViewModel Series2 { get; } = new SeriesCardItemViewModel();
    public SeriesCardItemViewModel Series3 { get; } = new SeriesCardItemViewModel();
    public SeriesCardItemViewModel Series4 { get; } = new SeriesCardItemViewModel();
    public SeriesCardItemViewModel Series5 { get; } = new SeriesCardItemViewModel();
    public SeriesCardItemViewModel Series6 { get; } = new SeriesCardItemViewModel();
    public SeriesCardItemViewModel Series7 { get; } = new SeriesCardItemViewModel();
    public SeriesCardItemViewModel Series8 { get; } = new SeriesCardItemViewModel();

    public DurationButtonItemViewModel Duration1Day { get; } = new DurationButtonItemViewModel();
    public DurationButtonItemViewModel Duration12Hours { get; } = new DurationButtonItemViewModel();
    public DurationButtonItemViewModel Duration6Hours { get; } = new DurationButtonItemViewModel();
    public DurationButtonItemViewModel Duration3Hours { get; } = new DurationButtonItemViewModel();
    public DurationButtonItemViewModel Duration2Hours { get; } = new DurationButtonItemViewModel();
    public DurationButtonItemViewModel Duration1Hour { get; } = new DurationButtonItemViewModel();
    public DurationButtonItemViewModel Duration30Minutes { get; } = new DurationButtonItemViewModel();
    public DurationButtonItemViewModel Duration10Minutes { get; } = new DurationButtonItemViewModel();
    public DurationButtonItemViewModel Duration5Minutes { get; } = new DurationButtonItemViewModel();
    public DurationButtonItemViewModel Duration2Minutes { get; } = new DurationButtonItemViewModel();
    public DurationButtonItemViewModel Duration1Minute { get; } = new DurationButtonItemViewModel();
    public DurationButtonItemViewModel Duration30Seconds { get; } = new DurationButtonItemViewModel();
    public DurationButtonItemViewModel Duration10Seconds { get; } = new DurationButtonItemViewModel();
    public DurationButtonItemViewModel SingleAxisModeButton { get; } = new DurationButtonItemViewModel();
    public DurationButtonItemViewModel DualAxisModeButton { get; } = new DurationButtonItemViewModel();
    public DurationButtonItemViewModel HistoricalTrendButton { get; } = new DurationButtonItemViewModel();
    public DurationButtonItemViewModel RealtimeTrendButton { get; } = new DurationButtonItemViewModel();

    public Action? WorkbenchChanged { get; set; }

    public void Configure(TrendWorkbenchApplicationService service)
    {
        applicationService = service;
        // 启动时把配置/服务中的外部状态同步到界面输入框。
        DatabasePathText = service.DatabasePath;
        SamplingIntervalText = service.SamplingIntervalSeconds.ToString();
    }

    public void RequestLayoutMode(TrendLayoutMode mode)
    {
        if (applicationService == null)
            return;

        applicationService.SetLayoutMode(mode);
        NotifyWorkbenchChanged();
    }

    public void RequestDurationPreset(TimeSpan span)
    {
        if (applicationService == null)
            return;

        applicationService.SetDurationPreset(span);
        NotifyWorkbenchChanged();
    }

    public void RequestLoadDatabase(string databasePath)
    {
        if (applicationService == null)
            return;

        try
        {
            string loadedPath = applicationService.SwitchSqliteDatabase(databasePath);
            DatabasePathText = loadedPath;
            ExportStatusText = $"数据库切换成功: {loadedPath}";
            NotifyWorkbenchChanged();
        }
        catch (Exception ex)
        {
            ExportStatusText = $"数据库切换失败: {ex.Message}";
        }
    }

    public void RequestApplySamplingInterval(string secondsText)
    {
        if (applicationService == null)
            return;

        if (!int.TryParse(secondsText, out int seconds))
        {
            ExportStatusText = "采集频率必须是整数秒。";
            return;
        }

        // 采集频率由应用服务规范化并保存配置，ViewModel 只更新展示文本。
        applicationService.ApplySamplingInterval(seconds);
        SamplingIntervalText = applicationService.SamplingIntervalSeconds.ToString();
        ExportStatusText = $"采集频率已更新为 {SamplingIntervalText} 秒。";
        NotifyWorkbenchChanged();
    }

    public void RequestSeriesSelection(int seriesIndex)
    {
        if (applicationService == null)
            return;

        applicationService.SelectSeriesFromCard(seriesIndex);
        NotifyWorkbenchChanged();
    }

    public void RequestExportCsv()
    {
        if (applicationService == null)
            return;

        try
        {
            string filePath = applicationService.ExportCurrentWindowCsv();
            ExportStatusText = BuildSuccessStatus("CSV 导出成功", filePath);
        }
        catch (Exception ex)
        {
            ExportStatusText = BuildFailureStatus("CSV 导出失败", ex);
        }
    }

    public void RequestSavePlotImage(ScottPlotTrendAdapter chartAdapter)
    {
        if (applicationService == null)
            return;

        try
        {
            string filePath = applicationService.SaveCurrentPlotImage(chartAdapter);
            ExportStatusText = BuildSuccessStatus("图片保存成功", filePath);
        }
        catch (Exception ex)
        {
            ExportStatusText = BuildFailureStatus("图片保存失败", ex);
        }
    }

    public void RequestPrintPlot(ScottPlotTrendAdapter chartAdapter)
    {
        if (applicationService == null)
            return;

        try
        {
            string filePath = applicationService.PrintCurrentPlot(chartAdapter);
            ExportStatusText = BuildSuccessStatus("打印图片已生成并打开", filePath);
        }
        catch (Exception ex)
        {
            ExportStatusText = BuildFailureStatus("打印失败", ex);
        }
    }

    private string BuildSuccessStatus(string actionName, string filePath)
    {
        string fullPath = Path.GetFullPath(filePath);
        return $"{actionName}。保存位置: {fullPath}";
    }

    private string BuildFailureStatus(string actionName, Exception exception)
    {
        if (applicationService == null)
            return $"{actionName}: {exception.Message}";

        return $"{actionName}: {exception.Message}。目标目录: {applicationService.OutputDirectory}";
    }

    public void RequestSetVisibleWindow(DateTime start, DateTime end)
    {
        if (applicationService == null)
            return;

        applicationService.SetVisibleWindow(start, end);
        NotifyWorkbenchChanged();
    }

    public void RequestSetWindowFromText(DateTime start, DateTime end)
    {
        if (applicationService == null)
            return;

        applicationService.SetWindowFromText(start, end);
        NotifyWorkbenchChanged();
    }

    public bool RequestApplyEditedStartTime(DateTime start)
    {
        if (applicationService == null)
            return false;

        bool ok = applicationService.SetEditedStartTime(start, out string message);
        ExportStatusText = ok ? "起始时间已更新。" : message;
        if (ok)
            NotifyWorkbenchChanged();

        return ok;
    }

    public bool RequestApplyEditedEndTime(DateTime end)
    {
        if (applicationService == null)
            return false;

        bool ok = applicationService.SetEditedEndTime(end, out string message);
        ExportStatusText = ok ? "结束时间已更新。" : message;
        if (ok)
            NotifyWorkbenchChanged();

        return ok;
    }

    public void RequestAxisGroupAssignment(int seriesIndex, TrendAxisGroup group)
    {
        if (applicationService == null)
            return;

        bool ok = applicationService.SetAxisGroupAssignment(seriesIndex, group, out string message);
        ExportStatusText = ok ? "变量轴分组已更新。" : message;
        NotifyWorkbenchChanged();
    }

    public void RequestHistoricalMode()
    {
        if (applicationService == null)
            return;

        applicationService.SwitchToHistorical();
        NotifyWorkbenchChanged();
    }

    public void RequestRealtimeMode()
    {
        if (applicationService == null)
            return;

        applicationService.SwitchToRealtime();
        NotifyWorkbenchChanged();
    }

    public void RequestCollectRealtimeSample()
    {
        if (applicationService == null)
            return;

        applicationService.CollectRealtimeSample();
        NotifyWorkbenchChanged();
    }

    public void RequestShiftWindow(TimeSpan delta)
    {
        if (applicationService == null)
            return;

        applicationService.ShiftWindow(delta);
        NotifyWorkbenchChanged();
    }

    public void RequestTimeSpanWindow(TimeSpan span)
    {
        if (applicationService == null)
            return;

        applicationService.SetTimeSpanWindow(span);
        NotifyWorkbenchChanged();
    }

    public void RequestLeftSeries(int seriesIndex)
    {
        if (applicationService == null)
            return;

        applicationService.SelectLeftSeries(seriesIndex);
        NotifyWorkbenchChanged();
    }

    public void RequestRightSeries(int seriesIndex)
    {
        if (applicationService == null)
            return;

        applicationService.SelectRightSeries(seriesIndex);
        NotifyWorkbenchChanged();
    }

    public void RequestCycleAxisSeries(bool isRightAxis)
    {
        if (applicationService == null)
            return;

        applicationService.CycleAxisSeries(isRightAxis);
        NotifyWorkbenchChanged();
    }

    public void RequestApplyCustomRange(bool isRightAxis, double min, double max)
    {
        if (applicationService == null)
            return;

        applicationService.ApplyCustomRange(isRightAxis, min, max);
        NotifyWorkbenchChanged();
    }

    public void RequestResetCustomRange(bool isRightAxis)
    {
        if (applicationService == null)
            return;

        applicationService.ResetCustomRange(isRightAxis);
        NotifyWorkbenchChanged();
    }

    private void NotifyWorkbenchChanged()
    {
        WorkbenchChanged?.Invoke();
    }

    public void ApplySnapshot(TrendWorkbenchPresentationSnapshot snapshot)
    {
        // Snapshot 是领域层给界面的完整展示结果，避免 ViewModel 重复业务计算。
        ZoomStatus = snapshot.ZoomStatus;
        ViewportStatus = snapshot.ViewportStatus;
        TimeRangeStatus = snapshot.TimeRangeStatus;
        YAxisStatus = snapshot.YAxisStatus;
        CrosshairStatus = snapshot.CrosshairStatus;
        LeftTimeText = snapshot.LeftTimeText;
        RightTimeText = snapshot.RightTimeText;
        BottomLeftRangeText = snapshot.BottomLeftRangeText;
        BottomRightRangeText = snapshot.BottomRightRangeText;
        SelectedDurationText = snapshot.SelectedDurationText;
        TrendModeText = snapshot.TrendModeText;
        if (applicationService != null)
        {
            DatabasePathText = applicationService.DatabasePath;
            SamplingIntervalText = applicationService.SamplingIntervalSeconds.ToString();
        }
        IsSingleAxisMode = snapshot.IsSingleAxisMode;
        IsDualAxisMode = snapshot.IsDualAxisMode;
        ReplaceItems(LeftAxisOptions, snapshot.LeftAxisOptions);
        ReplaceItems(RightAxisOptions, snapshot.RightAxisOptions);

        ApplyAxisPanel(LeftAxisPanel, snapshot.LeftAxisPanel);
        ApplyAxisPanel(RightAxisPanel, snapshot.RightAxisPanel);

        ApplyTimeHeader(TimeHeader1, snapshot.TimeHeaders, 0);
        ApplyTimeHeader(TimeHeader2, snapshot.TimeHeaders, 1);
        ApplyTimeHeader(TimeHeader3, snapshot.TimeHeaders, 2);
        ApplyTimeHeader(TimeHeader4, snapshot.TimeHeaders, 3);
        ApplyTimeHeader(TimeHeader5, snapshot.TimeHeaders, 4);

        ApplySeriesCard(Series1, snapshot.SeriesCards, 0);
        ApplySeriesCard(Series2, snapshot.SeriesCards, 1);
        ApplySeriesCard(Series3, snapshot.SeriesCards, 2);
        ApplySeriesCard(Series4, snapshot.SeriesCards, 3);
        ApplySeriesCard(Series5, snapshot.SeriesCards, 4);
        ApplySeriesCard(Series6, snapshot.SeriesCards, 5);
        ApplySeriesCard(Series7, snapshot.SeriesCards, 6);
        ApplySeriesCard(Series8, snapshot.SeriesCards, 7);

        ApplyDurationButton(Duration1Day, snapshot.DurationButtons, 0);
        ApplyDurationButton(Duration12Hours, snapshot.DurationButtons, 1);
        ApplyDurationButton(Duration6Hours, snapshot.DurationButtons, 2);
        ApplyDurationButton(Duration3Hours, snapshot.DurationButtons, 3);
        ApplyDurationButton(Duration2Hours, snapshot.DurationButtons, 4);
        ApplyDurationButton(Duration1Hour, snapshot.DurationButtons, 5);
        ApplyDurationButton(Duration30Minutes, snapshot.DurationButtons, 6);
        ApplyDurationButton(Duration10Minutes, snapshot.DurationButtons, 7);
        ApplyDurationButton(Duration5Minutes, snapshot.DurationButtons, 8);
        ApplyDurationButton(Duration2Minutes, snapshot.DurationButtons, 9);
        ApplyDurationButton(Duration1Minute, snapshot.DurationButtons, 10);
        ApplyDurationButton(Duration30Seconds, snapshot.DurationButtons, 11);
        ApplyDurationButton(Duration10Seconds, snapshot.DurationButtons, 12);
        ApplyModeButton(SingleAxisModeButton, snapshot.IsSingleAxisMode);
        ApplyModeButton(DualAxisModeButton, snapshot.IsDualAxisMode);
        ApplyTrendModeButton(HistoricalTrendButton, snapshot.IsHistoricalMode, "#1FD400");
        ApplyTrendModeButton(RealtimeTrendButton, snapshot.IsRealtimeMode, "#FF2C14");
    }

    private static void ApplyAxisPanel(AxisPanelViewModel target, AxisPanelPresentation source)
    {
        target.CurrentSeriesText = source.CurrentSeriesText;
        target.RangeText = source.RangeText;
        target.AccentBrush = new SolidColorBrush(Color.Parse(source.AccentHex));
        target.SelectedIndex = source.SelectedIndex;
        target.MinRangeText = source.MinRangeText;
        target.MaxRangeText = source.MaxRangeText;
    }

    private static void ApplyTimeHeader(TimeHeaderItemViewModel target, TimeHeaderPresentation[] source, int index)
    {
        if (index >= source.Length)
            return;

        target.DateText = source[index].DateText;
        target.TimeText = source[index].TimeText;
    }

    private static void ApplySeriesCard(SeriesCardItemViewModel target, SeriesCardPresentation[] source, int index)
    {
        if (index >= source.Length)
            return;

        target.Name = source[index].Name;
        target.ValueText = source[index].ValueText;
        target.IsVisible = source[index].IsVisible;

        bool isSelected = source[index].IsLeftSelected || source[index].IsRightSelected;
        string borderColor = isSelected ? source[index].AccentHex : "#A2A2A2";
        string backgroundColor = "#E3E3E3";
        if (source[index].IsLeftSelected)
            backgroundColor = "#EAF4FF";
        else if (source[index].IsRightSelected)
            backgroundColor = "#FFF6DE";

        target.Opacity = source[index].IsVisible ? 1 : 0.62;
        target.BorderThickness = isSelected ? new Thickness(2) : new Thickness(1);
        target.BorderBrush = new SolidColorBrush(Color.Parse(borderColor));
        target.BackgroundBrush = new SolidColorBrush(Color.Parse(backgroundColor));
    }

    private static void ApplyDurationButton(DurationButtonItemViewModel target, DurationButtonPresentation[] source, int index)
    {
        if (index >= source.Length)
            return;

        bool isActive = source[index].IsActive;
        target.BackgroundBrush = new SolidColorBrush(Color.Parse(isActive ? "#244CDA" : "#BFBFBF"));
        target.ForegroundBrush = new SolidColorBrush(Color.Parse(isActive ? "#FFFFFF" : "#101010"));
        target.FontWeight = isActive ? FontWeight.SemiBold : FontWeight.Normal;
    }

    private static void ReplaceItems(ObservableCollection<string> target, string[] source)
    {
        target.Clear();
        foreach (string item in source)
            target.Add(item);
    }

    private static void ApplyModeButton(DurationButtonItemViewModel target, bool isActive)
    {
        target.BackgroundBrush = new SolidColorBrush(Color.Parse(isActive ? "#244CDA" : "#BFBFBF"));
        target.ForegroundBrush = new SolidColorBrush(Color.Parse(isActive ? "#FFFFFF" : "#101010"));
        target.FontWeight = isActive ? FontWeight.SemiBold : FontWeight.Normal;
    }

    private static void ApplyTrendModeButton(DurationButtonItemViewModel target, bool isActive, string activeColor)
    {
        target.BackgroundBrush = new SolidColorBrush(Color.Parse(isActive ? activeColor : "#BFBFBF"));
        target.ForegroundBrush = new SolidColorBrush(Color.Parse("#101010"));
        target.FontWeight = isActive ? FontWeight.SemiBold : FontWeight.Normal;
    }
}
