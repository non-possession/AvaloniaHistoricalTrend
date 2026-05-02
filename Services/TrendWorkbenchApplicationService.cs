using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AvaloniaApplication2.Models;

namespace AvaloniaApplication2.Services;

// 页面级应用服务。
// ViewModel 调用这里完成“用户动作”，本服务再协调领域规则、数据库、采集器、配置和导出服务。
// 这样 ViewModel 不需要知道 SQLite/CSV/图片保存等外部依赖细节，View 也不直接写业务流程。
public sealed class TrendWorkbenchApplicationService
{
    private readonly TrendCsvExportService csvExportService;
    private readonly TrendPlotImageExportService imageExportService;
    private readonly TrendPlotPrintService printService;
    private ITrendDataStore dataStore;
    private readonly ITrendDataCollector dataCollector;
    private readonly TrendWorkbenchSettingsService settingsService;
    private readonly TrendWorkbenchSettings settings;
    private readonly string outputDirectory;

    public TrendWorkbenchApplicationService(
        TrendWorkbenchCoordinator coordinator,
        TrendCsvExportService csvExportService,
        TrendPlotImageExportService imageExportService,
        TrendPlotPrintService printService,
        ITrendDataStore dataStore,
        ITrendDataCollector dataCollector,
        TrendWorkbenchSettingsService settingsService,
        TrendWorkbenchSettings settings,
        string? outputDirectory = null)
    {
        Coordinator = coordinator;
        this.csvExportService = csvExportService;
        this.imageExportService = imageExportService;
        this.printService = printService;
        this.dataStore = dataStore;
        this.dataCollector = dataCollector;
        this.settingsService = settingsService;
        this.settings = settings;
        string selectedOutputDirectory = outputDirectory ?? Path.Combine(Environment.CurrentDirectory, "output");
        this.outputDirectory = Path.GetFullPath(selectedOutputDirectory);
        // 运行参数来自配置文件，Coordinator 只接收规范化后的领域状态。
        Coordinator.ConfigureRuntime(
            TimeSpan.FromSeconds(settings.SamplingIntervalSeconds),
            TimeSpan.FromSeconds(settings.RealtimeWindowSeconds));
        Coordinator.ApplyAxisGroupAssignments(settings.AxisGroupAssignments);
        InitializeDataStore();
    }

    public TrendWorkbenchCoordinator Coordinator { get; }

    public TrendWorkbenchSettings Settings => settings;

    public string OutputDirectory => outputDirectory;

    public ITrendDataStore DataStore => dataStore;

    public string DatabasePath => settings.SqliteDatabasePath;

    public int SamplingIntervalSeconds => settings.SamplingIntervalSeconds;

    public void SetLayoutMode(TrendLayoutMode mode)
    {
        Coordinator.SetLayoutMode(mode);
        settings.LayoutMode = mode;
        settingsService.Save(settings);
    }

    public void SetDurationPreset(TimeSpan span)
    {
        Coordinator.SetDurationPreset(span);
        // 时间跨度既影响历史窗口，也作为下次启动的默认显示跨度。
        settings.DefaultVisibleDurationSeconds = Math.Max((int)TrendWorkbenchCoordinator.MinimumTimeSpan.TotalSeconds, (int)Math.Round(span.TotalSeconds));
        settings.DefaultVisibleDurationMinutes = Math.Max(1, (int)Math.Round(span.TotalMinutes));
        if (Coordinator.State.Mode == TrendMode.Realtime)
        {
            // 实时模式下，跨度按钮表示实时滚动窗口长度。
            settings.RealtimeWindowSeconds = Math.Max((int)TrendWorkbenchCoordinator.MinimumTimeSpan.TotalSeconds, (int)Math.Round(span.TotalSeconds));
            settings.RealtimeWindowMinutes = Math.Max(1, (int)Math.Round(span.TotalMinutes));
        }
        settingsService.Save(settings);
    }

    public void SetTimeSpanWindow(TimeSpan span)
    {
        Coordinator.SetHistoricalMode();
        Coordinator.SetTimeSpanWindow(span);
    }

    public void SetVisibleWindow(DateTime start, DateTime end)
    {
        Coordinator.SetHistoricalMode();
        Coordinator.SetVisibleWindow(start, end);
    }

    public void ShiftWindow(TimeSpan delta)
    {
        Coordinator.SetHistoricalMode();
        Coordinator.ShiftWindow(delta);
    }

    public void SelectSeriesFromCard(int index)
    {
        if (Coordinator.State.LayoutMode == TrendLayoutMode.SingleAxis)
        {
            Coordinator.SelectLeftSeries(index);
            return;
        }

        // 双轴模式下变量卡选择必须遵守当前轴分组配置。
        if (Coordinator.State.AxisGroupAssignments[index] == TrendAxisGroup.Y1)
            Coordinator.SelectLeftSeries(index);
        else
            Coordinator.SelectRightSeries(index);
    }

    public void SelectLeftSeries(int seriesIndex)
    {
        Coordinator.SelectLeftSeries(seriesIndex);
    }

    public void SelectRightSeries(int seriesIndex)
    {
        Coordinator.SelectRightSeries(seriesIndex);
    }

    public void CycleAxisSeries(bool isRightAxis)
    {
        Coordinator.CycleAxisSeries(isRightAxis);
    }

    public void ApplyCustomRange(bool isRightAxis, double min, double max)
    {
        Coordinator.ApplyCustomRange(isRightAxis, min, max);
    }

    public void ResetCustomRange(bool isRightAxis)
    {
        Coordinator.ResetCustomRange(isRightAxis);
    }

    public void SetSeriesVisible(int seriesIndex, bool isVisible)
    {
        Coordinator.SetSeriesVisible(seriesIndex, isVisible);
    }

    public bool SetAxisGroupAssignment(int seriesIndex, TrendAxisGroup group, out string message)
    {
        bool changed = Coordinator.TrySetAxisGroupAssignment(seriesIndex, group, out message);
        if (!changed)
            return false;

        settings.AxisGroupAssignments = Coordinator.State.AxisGroupAssignments.ToArray();
        // 是否把用户修改的轴分组写回配置，由配置项控制。
        if (settings.PersistAxisGroupAssignments)
            settingsService.Save(settings);

        return true;
    }

    public string ExportCurrentWindowCsv()
    {
        return csvExportService.ExportCurrentWindow(Coordinator.State, outputDirectory);
    }

    public string SaveCurrentPlotImage(ScottPlotTrendAdapter chartAdapter)
    {
        return imageExportService.ExportCurrentPlotPng(Coordinator.State, chartAdapter, outputDirectory);
    }

    public string PrintCurrentPlot(ScottPlotTrendAdapter chartAdapter)
    {
        string imagePath = SaveCurrentPlotImage(chartAdapter);
        printService.OpenImageForPrinting(imagePath);
        return imagePath;
    }

    public void SwitchToHistorical()
    {
        Coordinator.SetHistoricalMode();
    }

    public void SwitchToRealtime()
    {
        Coordinator.SetRealtimeMode();
    }

    public void CollectRealtimeSample()
    {
        TrendSample sample = dataCollector.Collect(DateTime.Now, Coordinator);
        // 实时采集先落库，再更新内存曲线，保证切回历史时能查到刚采集的数据。
        dataStore.AppendSample(sample, Coordinator.State.Series);
        Coordinator.AppendRealtimeSample(sample);
    }

    public void ApplySamplingInterval(int seconds)
    {
        int normalizedSeconds = Math.Max(1, seconds);
        settings.SamplingIntervalSeconds = normalizedSeconds;
        Coordinator.SetSamplingInterval(TimeSpan.FromSeconds(normalizedSeconds));
        settingsService.Save(settings);
    }

    public string SwitchSqliteDatabase(string databasePath)
    {
        if (string.IsNullOrWhiteSpace(databasePath))
            throw new InvalidOperationException("数据库路径不能为空。");

        string fullPath = Path.GetFullPath(databasePath);
        ITrendDataStore previousStore = dataStore;
        string previousPath = settings.SqliteDatabasePath;

        try
        {
            SqliteTrendDataStore newStore = new SqliteTrendDataStore(fullPath);
            newStore.Initialize(Coordinator.State.Series);
            dataStore = newStore;
            settings.SqliteDatabasePath = fullPath;
            settingsService.Save(settings);
            List<TrendSample> samples = dataStore.QuerySamples(
                Coordinator.State.TimeWindow.VisibleStart,
                Coordinator.State.TimeWindow.VisibleEnd,
                Coordinator.State.Series.Count);
            if (samples.Count > 0)
                Coordinator.LoadSamples(samples);

            return fullPath;
        }
        catch
        {
            // 数据库切换失败时恢复旧数据源，避免工作台进入半切换状态。
            dataStore = previousStore;
            settings.SqliteDatabasePath = previousPath;
            throw;
        }
    }

    public void SetWindowFromText(DateTime start, DateTime end)
    {
        Coordinator.SetHistoricalMode();
        Coordinator.SetVisibleWindow(start, end);
    }

    public bool SetEditedStartTime(DateTime start, out string message)
    {
        return Coordinator.TrySetEditedStartTime(start, out message);
    }

    public bool SetEditedEndTime(DateTime end, out string message)
    {
        return Coordinator.TrySetEditedEndTime(end, out message);
    }

    private void InitializeDataStore()
    {
        dataStore.Initialize(Coordinator.State.Series);
        List<TrendSample> existingSamples = dataStore.QuerySamples(
            Coordinator.State.TimeWindow.TotalStart,
            Coordinator.State.TimeWindow.TotalEnd,
            Coordinator.State.Series.Count);
        if (existingSamples.Count > 0)
        {
            // 数据库已有历史数据时，以数据库为准恢复工作台。
            Coordinator.LoadSamples(existingSamples);
            return;
        }

        // 第一次启动没有数据库数据时，用 Demo 曲线种子填充一份初始数据。
        List<TrendSample> seedSamples = Coordinator.CreateInitialSamples();
        for (int i = 0; i < seedSamples.Count; i++)
            dataStore.AppendSample(seedSamples[i], Coordinator.State.Series);
    }

    private void LoadHistoricalWindow(DateTime start, DateTime end)
    {
        List<TrendSample> samples = dataStore.QuerySamples(start, end, Coordinator.State.Series.Count);
        if (samples.Count > 0)
            Coordinator.LoadSamples(samples);
    }
}
