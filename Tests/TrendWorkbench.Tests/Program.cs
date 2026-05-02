using AvaloniaApplication2.Models;
using AvaloniaApplication2.Services;

var runner = new TestRunner();
runner.Run("Engineering range and displayed range respect custom range + brush fractions", TestEngineeringAndDisplayedRange);
runner.Run("Brush fractions clamp to minimum gap", TestBrushFractionClamp);
runner.Run("Time span window enforces minimum span", TestMinimumTimeSpanProtection);
runner.Run("Left/right axis cycling respects group visibility in dual-axis mode", TestDualAxisCycling);
runner.Run("Single-axis mode can select across all visible series", TestSingleAxisSelectionAcrossAllSeries);
runner.Run("Changing selected series does not reset other series brush state", TestSeriesSelectionKeepsOtherSeriesState);
runner.Run("Settings service normalizes missing or invalid values", TestSettingsNormalization);
runner.Run("CSV export writes current window and visible series only", TestCsvExportCurrentWindow);
runner.Run("Application service handles layout, time window, and export actions", TestApplicationServiceActions);
runner.Run("SQLite store writes and reads trend samples", TestSqliteStoreRoundTrip);
runner.Run("Random collector respects engineering ranges", TestRandomCollectorRange);
runner.Run("Realtime mode appends sample and keeps rolling window", TestRealtimeAppendAndWindow);
runner.Run("Axis group assignments drive dual-axis candidates", TestAxisGroupAssignments);
runner.Run("Axis group edit rejects empty side and updates candidates", TestAxisGroupEditRules);
runner.Run("Text time window action switches to historical mode", TestTextTimeWindowSwitchesHistorical);
runner.Run("Edited time boundaries are validated by coordinator", TestEditedTimeBoundaryValidation);
runner.Run("Presentation snapshot exposes historical and realtime mode state", TestPresentationSnapshotModeState);
runner.Run("Small duration presets can use 10 second window", TestSmallDurationPreset);
runner.Run("Realtime duration preset keeps realtime mode and rolling span", TestRealtimeDurationPreset);
runner.Run("Sampling interval update changes settings and runtime state", TestSamplingIntervalUpdate);
runner.Run("Database switch uses new SQLite path for subsequent samples", TestDatabaseSwitch);
runner.Finish();

static void TestEngineeringAndDisplayedRange()
{
    var coordinator = new TrendWorkbenchCoordinator();
    coordinator.ApplyCustomRange(isRightAxis: false, 10, 20);
    coordinator.SetBrushFraction(0, isUpperBrush: false, 0.25);
    coordinator.SetBrushFraction(0, isUpperBrush: true, 0.75);

    NumericRange engineering = coordinator.GetEngineeringRange(0);
    NumericRange displayed = coordinator.GetDisplayedYRange(0);

    TestRunner.AssertEqual(10, engineering.Min, "engineering min");
    TestRunner.AssertEqual(20, engineering.Max, "engineering max");
    TestRunner.AssertEqual(12.5, displayed.Min, "displayed min");
    TestRunner.AssertEqual(17.5, displayed.Max, "displayed max");
}

static void TestBrushFractionClamp()
{
    var coordinator = new TrendWorkbenchCoordinator();
    coordinator.SetBrushFraction(0, isUpperBrush: false, 0.99);
    coordinator.SetBrushFraction(0, isUpperBrush: true, 0.01);

    TrendSeriesState series = coordinator.GetSeries(0);
    TestRunner.AssertTrue(series.LowerBrushFraction <= series.UpperBrushFraction - TrendWorkbenchCoordinator.MinimumBrushGap + 1e-9, "brush gap maintained");
    TestRunner.AssertTrue(series.UpperBrushFraction >= series.LowerBrushFraction + TrendWorkbenchCoordinator.MinimumBrushGap - 1e-9, "upper brush clamped");
}

static void TestMinimumTimeSpanProtection()
{
    var coordinator = new TrendWorkbenchCoordinator();
    coordinator.SetTimeSpanWindow(TimeSpan.FromSeconds(1));
    TimeSpan span = coordinator.State.TimeWindow.VisibleEnd - coordinator.State.TimeWindow.VisibleStart;
    TestRunner.AssertEqual(TrendWorkbenchCoordinator.MinimumTimeSpan.TotalSeconds, span.TotalSeconds, "minimum span enforced");
}

static void TestDualAxisCycling()
{
    var coordinator = new TrendWorkbenchCoordinator();
    coordinator.SetLayoutMode(TrendLayoutMode.DualAxis);
    coordinator.SetSeriesVisible(1, false);
    coordinator.SelectLeftSeries(0);
    coordinator.CycleAxisSeries(isRightAxis: false);
    TestRunner.AssertEqual(2, coordinator.State.AxisSelection.LeftSeriesIndex, "dual-axis left cycle skips hidden series");

    coordinator.SetSeriesVisible(5, false);
    coordinator.SelectRightSeries(4);
    coordinator.CycleAxisSeries(isRightAxis: true);
    TestRunner.AssertEqual(6, coordinator.State.AxisSelection.RightSeriesIndex, "dual-axis right cycle skips hidden series");
}

static void TestSingleAxisSelectionAcrossAllSeries()
{
    var coordinator = new TrendWorkbenchCoordinator();
    coordinator.SetLayoutMode(TrendLayoutMode.SingleAxis);
    coordinator.SelectLeftSeries(6);
    TestRunner.AssertEqual(6, coordinator.State.AxisSelection.LeftSeriesIndex, "single-axis allows selecting right-group series");

    coordinator.CycleAxisSeries(isRightAxis: false);
    TestRunner.AssertEqual(7, coordinator.State.AxisSelection.LeftSeriesIndex, "single-axis cycles across all series");
}

static void TestSeriesSelectionKeepsOtherSeriesState()
{
    var coordinator = new TrendWorkbenchCoordinator();
    coordinator.SetBrushFraction(0, isUpperBrush: false, 0.2);
    coordinator.SetBrushFraction(0, isUpperBrush: true, 0.8);
    double lowerBefore = coordinator.GetSeries(0).LowerBrushFraction;
    double upperBefore = coordinator.GetSeries(0).UpperBrushFraction;

    coordinator.SelectLeftSeries(1);

    TestRunner.AssertEqual(lowerBefore, coordinator.GetSeries(0).LowerBrushFraction, "other series lower brush unchanged");
    TestRunner.AssertEqual(upperBefore, coordinator.GetSeries(0).UpperBrushFraction, "other series upper brush unchanged");
}

static void TestSettingsNormalization()
{
    string tempDir = Path.Combine(Path.GetTempPath(), $"trend-settings-test-{Guid.NewGuid():N}");
    Directory.CreateDirectory(tempDir);
    string settingsPath = Path.Combine(tempDir, "trend-workbench.settings.json");
    File.WriteAllText(settingsPath, """
{
  "LayoutMode": 999,
  "ShowLegend": true,
  "DefaultVisibleDurationMinutes": 0
}
""");

    var service = new TrendWorkbenchSettingsService(settingsPath);
    TrendWorkbenchSettings settings = service.Load();

    TestRunner.AssertEqual(TrendLayoutMode.DualAxis, settings.LayoutMode, "invalid layout falls back");
    TestRunner.AssertTrue(settings.ShowLegend, "existing bool preserved");
    TestRunner.AssertEqual(1440, settings.DefaultVisibleDurationMinutes, "invalid duration normalized");
}

static void TestCsvExportCurrentWindow()
{
    TrendWorkbenchCoordinator coordinator = new TrendWorkbenchCoordinator();
    coordinator.SetSeriesVisible(1, false);
    DateTime start = coordinator.State.TimePoints[10];
    DateTime end = coordinator.State.TimePoints[15];
    coordinator.SetVisibleWindow(start, end);

    string tempDir = Path.Combine(Path.GetTempPath(), $"trend-csv-test-{Guid.NewGuid():N}");
    TrendCsvExportService service = new TrendCsvExportService();
    string filePath = service.ExportCurrentWindow(coordinator.State, tempDir);

    string[] lines = File.ReadAllLines(filePath);
    TestRunner.AssertTrue(lines.Length > 1, "csv should contain header and data rows");
    TestRunner.AssertTrue(lines[0].Contains("DPIT401PV"), "visible series should be exported");
    TestRunner.AssertTrue(!lines[0].Contains("P402EVAMP_PV"), "hidden series should not be exported");
    TestRunner.AssertTrue(lines[1].StartsWith(start.ToString("yyyy-MM-dd HH:mm:ss")), "first row should match visible start");
}

static void TestApplicationServiceActions()
{
    string tempDir = Path.Combine(Path.GetTempPath(), $"trend-app-service-test-{Guid.NewGuid():N}");
    string settingsPath = Path.Combine(tempDir, "trend-workbench.settings.json");

    TrendWorkbenchCoordinator coordinator = new TrendWorkbenchCoordinator();
    TrendWorkbenchSettings settings = new TrendWorkbenchSettings();
    TrendWorkbenchSettingsService settingsService = new TrendWorkbenchSettingsService(settingsPath);
    TrendCsvExportService csvExportService = new TrendCsvExportService();
    TrendWorkbenchApplicationService service = new TrendWorkbenchApplicationService(
        coordinator,
        csvExportService,
        new TrendPlotImageExportService(),
        new TrendPlotPrintService(),
        new SqliteTrendDataStore(Path.Combine(tempDir, "trend.db")),
        new RandomTrendDataCollector(),
        settingsService,
        settings,
        tempDir);

    service.SetLayoutMode(TrendLayoutMode.SingleAxis);
    TestRunner.AssertEqual(TrendLayoutMode.SingleAxis, coordinator.State.LayoutMode, "layout mode updated");

    service.SetDurationPreset(TimeSpan.FromHours(12));
    TimeSpan span = coordinator.State.TimeWindow.VisibleEnd - coordinator.State.TimeWindow.VisibleStart;
    TestRunner.AssertEqual(12, span.TotalHours, "duration preset updated");

    coordinator.SetSeriesVisible(1, false);
    string exportPath = service.ExportCurrentWindowCsv();
    string header = File.ReadLines(exportPath).First();
    TestRunner.AssertTrue(header.Contains("DPIT401PV"), "app service export includes visible variable");
    TestRunner.AssertTrue(!header.Contains("P402EVAMP_PV"), "app service export excludes hidden variable");
}

static void TestSqliteStoreRoundTrip()
{
    string tempDir = Path.Combine(Path.GetTempPath(), $"trend-sqlite-test-{Guid.NewGuid():N}");
    string dbPath = Path.Combine(tempDir, "trend.db");
    TrendWorkbenchCoordinator coordinator = new TrendWorkbenchCoordinator();
    SqliteTrendDataStore store = new SqliteTrendDataStore(dbPath);
    store.Initialize(coordinator.State.Series);

    DateTime timestamp = DateTime.Now;
    double[] values = new double[coordinator.State.Series.Count];
    for (int i = 0; i < values.Length; i++)
        values[i] = i + 0.5;

    store.AppendSample(new TrendSample { Timestamp = timestamp, Values = values }, coordinator.State.Series);
    List<TrendSample> samples = store.QuerySamples(timestamp.AddSeconds(-1), timestamp.AddSeconds(1), values.Length);

    TestRunner.AssertEqual(1, samples.Count, "sqlite round trip sample count");
    TestRunner.AssertEqual(3.5, samples[0].Values[3], "sqlite round trip value");
}

static void TestRandomCollectorRange()
{
    TrendWorkbenchCoordinator coordinator = new TrendWorkbenchCoordinator();
    RandomTrendDataCollector collector = new RandomTrendDataCollector();

    TrendSample sample = collector.Collect(DateTime.Now, coordinator);
    for (int i = 0; i < sample.Values.Length; i++)
    {
        NumericRange range = coordinator.GetEngineeringRange(i);
        TestRunner.AssertTrue(sample.Values[i] >= range.Min && sample.Values[i] <= range.Max, "random value within range");
    }
}

static void TestRealtimeAppendAndWindow()
{
    string tempDir = Path.Combine(Path.GetTempPath(), $"trend-realtime-test-{Guid.NewGuid():N}");
    TrendWorkbenchCoordinator coordinator = new TrendWorkbenchCoordinator();
    TrendWorkbenchSettings settings = new TrendWorkbenchSettings
    {
        RealtimeWindowMinutes = 60,
        SamplingIntervalSeconds = 1,
    };
    TrendWorkbenchApplicationService service = new TrendWorkbenchApplicationService(
        coordinator,
        new TrendCsvExportService(),
        new TrendPlotImageExportService(),
        new TrendPlotPrintService(),
        new SqliteTrendDataStore(Path.Combine(tempDir, "trend.db")),
        new RandomTrendDataCollector(),
        new TrendWorkbenchSettingsService(Path.Combine(tempDir, "settings.json")),
        settings,
        tempDir);

    service.SwitchToRealtime();
    service.CollectRealtimeSample();

    TestRunner.AssertEqual(TrendMode.Realtime, coordinator.State.Mode, "realtime mode is active");
    TestRunner.AssertTrue(coordinator.State.TimeWindow.VisibleEnd == coordinator.State.TimeWindow.TotalEnd, "realtime window ends at latest sample");
    TestRunner.AssertTrue(coordinator.State.TimeWindow.VisibleEnd - coordinator.State.TimeWindow.VisibleStart <= TimeSpan.FromMinutes(60), "realtime window span");
}

static void TestAxisGroupAssignments()
{
    TrendWorkbenchCoordinator coordinator = new TrendWorkbenchCoordinator();
    coordinator.ApplyAxisGroupAssignments(
    [
        TrendAxisGroup.Y1,
        TrendAxisGroup.Y2,
        TrendAxisGroup.Y1,
        TrendAxisGroup.Y2,
        TrendAxisGroup.Y1,
        TrendAxisGroup.Y2,
        TrendAxisGroup.Y1,
        TrendAxisGroup.Y2,
    ]);
    coordinator.SetLayoutMode(TrendLayoutMode.DualAxis);

    int[] leftCandidates = coordinator.GetAxisCandidateIndices(isRightAxis: false);
    int[] rightCandidates = coordinator.GetAxisCandidateIndices(isRightAxis: true);

    TestRunner.AssertEqual(4, leftCandidates.Length, "left candidate count");
    TestRunner.AssertEqual(1, rightCandidates[0], "right candidate first item follows config");

    coordinator.SelectLeftSeries(1);
    TestRunner.AssertTrue(coordinator.State.AxisSelection.LeftSeriesIndex != 1, "left selection rejects y2 candidate");

    coordinator.SelectRightSeries(1);
    TestRunner.AssertEqual(1, coordinator.State.AxisSelection.RightSeriesIndex, "right selection accepts configured y2 candidate");
}

static void TestAxisGroupEditRules()
{
    TrendWorkbenchCoordinator coordinator = new TrendWorkbenchCoordinator();
    bool moved0 = coordinator.TrySetAxisGroupAssignment(0, TrendAxisGroup.Y2, out string _);
    bool moved1 = coordinator.TrySetAxisGroupAssignment(1, TrendAxisGroup.Y2, out string _);
    bool moved2 = coordinator.TrySetAxisGroupAssignment(2, TrendAxisGroup.Y2, out string _);
    bool moved3 = coordinator.TrySetAxisGroupAssignment(3, TrendAxisGroup.Y2, out string message);
    bool rejected = moved0 && moved1 && moved2 && !moved3;

    TestRunner.AssertTrue(rejected, "last y1 variable cannot be moved away");
    TestRunner.AssertTrue(!string.IsNullOrWhiteSpace(message), "rejection message provided");

    bool updated = coordinator.TrySetAxisGroupAssignment(4, TrendAxisGroup.Y1, out string updateMessage);
    TestRunner.AssertTrue(updated, $"axis group edit should succeed: {updateMessage}");
    TestRunner.AssertEqual(TrendAxisGroup.Y1, coordinator.State.AxisGroupAssignments[4], "series moved to y1");
    TestRunner.AssertTrue(coordinator.GetAxisCandidateIndices(isRightAxis: false).Contains(4), "left candidates include moved series");
}

static void TestTextTimeWindowSwitchesHistorical()
{
    string tempDir = Path.Combine(Path.GetTempPath(), $"trend-time-input-test-{Guid.NewGuid():N}");
    TrendWorkbenchCoordinator coordinator = new TrendWorkbenchCoordinator();
    TrendWorkbenchApplicationService service = CreateApplicationService(coordinator, tempDir);

    service.SwitchToRealtime();
    DateTime start = coordinator.State.TimeWindow.TotalStart.AddHours(2);
    DateTime end = start.AddHours(3);
    service.SetWindowFromText(start, end);

    TestRunner.AssertEqual(TrendMode.Historical, coordinator.State.Mode, "time text action switches historical");
    TestRunner.AssertEqual(start, coordinator.State.TimeWindow.VisibleStart, "text start applied");
    TestRunner.AssertEqual(end, coordinator.State.TimeWindow.VisibleEnd, "text end applied");
}

static void TestEditedTimeBoundaryValidation()
{
    TrendWorkbenchCoordinator coordinator = new TrendWorkbenchCoordinator();
    DateTime originalStart = coordinator.State.TimeWindow.VisibleStart;
    DateTime originalEnd = coordinator.State.TimeWindow.VisibleEnd;

    bool invalidOrder = coordinator.TrySetEditedStartTime(originalEnd, out string orderMessage);
    TestRunner.AssertTrue(!invalidOrder, "start cannot equal or exceed end");
    TestRunner.AssertTrue(orderMessage.Contains("早于"), "order validation message");

    bool beforeTotal = coordinator.TrySetEditedStartTime(coordinator.State.TimeWindow.TotalStart.AddSeconds(-1), out string rangeMessage);
    TestRunner.AssertTrue(!beforeTotal, "start cannot be before total range");
    TestRunner.AssertTrue(rangeMessage.Contains("数据范围"), "range validation message");

    DateTime validStart = originalStart.AddMinutes(10);
    bool valid = coordinator.TrySetEditedStartTime(validStart, out string validMessage);
    TestRunner.AssertTrue(valid, $"valid edited start should be accepted: {validMessage}");
    TestRunner.AssertEqual(validStart, coordinator.State.TimeWindow.VisibleStart, "edited start applied");
    TestRunner.AssertEqual(TrendMode.Historical, coordinator.State.Mode, "edited start switches to historical mode");

    DateTime validEnd = originalEnd.AddMinutes(-10);
    bool validEndApplied = coordinator.TrySetEditedEndTime(validEnd, out string validEndMessage);
    TestRunner.AssertTrue(validEndApplied, $"valid edited end should be accepted: {validEndMessage}");
    TestRunner.AssertEqual(validStart, coordinator.State.TimeWindow.VisibleStart, "edited start stays while editing end");
    TestRunner.AssertEqual(validEnd, coordinator.State.TimeWindow.VisibleEnd, "edited end applied");
}

static void TestPresentationSnapshotModeState()
{
    TrendWorkbenchCoordinator coordinator = new TrendWorkbenchCoordinator();
    coordinator.SetRealtimeMode();
    TrendWorkbenchPresentationSnapshot realtimeSnapshot = coordinator.BuildPresentationSnapshot();
    TestRunner.AssertTrue(realtimeSnapshot.IsRealtimeMode, "snapshot realtime active");
    TestRunner.AssertTrue(!realtimeSnapshot.IsHistoricalMode, "snapshot historical inactive");

    coordinator.SetHistoricalMode();
    TrendWorkbenchPresentationSnapshot historicalSnapshot = coordinator.BuildPresentationSnapshot();
    TestRunner.AssertTrue(historicalSnapshot.IsHistoricalMode, "snapshot historical active");
    TestRunner.AssertTrue(!historicalSnapshot.IsRealtimeMode, "snapshot realtime inactive");
}

static void TestSmallDurationPreset()
{
    TrendWorkbenchCoordinator coordinator = new TrendWorkbenchCoordinator();
    coordinator.SetTimeSpanWindow(TimeSpan.FromSeconds(10));
    TimeSpan span = coordinator.State.TimeWindow.VisibleEnd - coordinator.State.TimeWindow.VisibleStart;

    TestRunner.AssertEqual(10, span.TotalSeconds, "10 second duration is allowed");
}

static void TestRealtimeDurationPreset()
{
    string tempDir = Path.Combine(Path.GetTempPath(), $"trend-realtime-duration-test-{Guid.NewGuid():N}");
    TrendWorkbenchCoordinator coordinator = new TrendWorkbenchCoordinator();
    TrendWorkbenchApplicationService service = CreateApplicationService(coordinator, tempDir);

    service.SwitchToRealtime();
    service.SetDurationPreset(TimeSpan.FromSeconds(10));
    service.CollectRealtimeSample();

    TimeSpan span = coordinator.State.TimeWindow.VisibleEnd - coordinator.State.TimeWindow.VisibleStart;
    TestRunner.AssertEqual(TrendMode.Realtime, coordinator.State.Mode, "duration preset keeps realtime mode");
    TestRunner.AssertTrue(span <= TimeSpan.FromSeconds(10.5), "realtime span follows 10 second preset");
    TestRunner.AssertEqual(10, service.Settings.RealtimeWindowSeconds, "settings stores realtime seconds");
}

static void TestSamplingIntervalUpdate()
{
    string tempDir = Path.Combine(Path.GetTempPath(), $"trend-sampling-test-{Guid.NewGuid():N}");
    TrendWorkbenchCoordinator coordinator = new TrendWorkbenchCoordinator();
    TrendWorkbenchApplicationService service = CreateApplicationService(coordinator, tempDir);

    service.ApplySamplingInterval(3);

    TestRunner.AssertEqual(3, service.Settings.SamplingIntervalSeconds, "settings sampling interval");
    TestRunner.AssertEqual(3, coordinator.State.SamplingInterval.TotalSeconds, "runtime sampling interval");
}

static void TestDatabaseSwitch()
{
    string tempDir = Path.Combine(Path.GetTempPath(), $"trend-db-switch-test-{Guid.NewGuid():N}");
    TrendWorkbenchCoordinator coordinator = new TrendWorkbenchCoordinator();
    TrendWorkbenchApplicationService service = CreateApplicationService(coordinator, tempDir);

    string firstDb = Path.Combine(tempDir, "first.db");
    string secondDb = Path.Combine(tempDir, "second.db");
    service.SwitchSqliteDatabase(firstDb);
    service.CollectRealtimeSample();
    service.SwitchSqliteDatabase(secondDb);
    service.CollectRealtimeSample();

    SqliteTrendDataStore firstStore = new SqliteTrendDataStore(firstDb);
    SqliteTrendDataStore secondStore = new SqliteTrendDataStore(secondDb);
    DateTime start = DateTime.Now.AddMinutes(-5);
    DateTime end = DateTime.Now.AddMinutes(5);
    List<TrendSample> firstSamples = firstStore.QuerySamples(start, end, coordinator.State.Series.Count);
    List<TrendSample> secondSamples = secondStore.QuerySamples(start, end, coordinator.State.Series.Count);

    TestRunner.AssertTrue(firstSamples.Count > 0, "first database received initial realtime sample");
    TestRunner.AssertTrue(secondSamples.Count > 0, "second database received sample after switch");
    TestRunner.AssertEqual(Path.GetFullPath(secondDb), service.Settings.SqliteDatabasePath, "settings stores switched database");
}

static TrendWorkbenchApplicationService CreateApplicationService(TrendWorkbenchCoordinator coordinator, string tempDir)
{
    TrendWorkbenchSettings settings = new TrendWorkbenchSettings();
    return new TrendWorkbenchApplicationService(
        coordinator,
        new TrendCsvExportService(),
        new TrendPlotImageExportService(),
        new TrendPlotPrintService(),
        new SqliteTrendDataStore(Path.Combine(tempDir, "trend.db")),
        new RandomTrendDataCollector(),
        new TrendWorkbenchSettingsService(Path.Combine(tempDir, "settings.json")),
        settings,
        tempDir);
}

internal sealed class TestRunner
{
    private int passed;
    private int failed;

    public void Run(string name, Action test)
    {
        try
        {
            test();
            Console.WriteLine($"[PASS] {name}");
            passed++;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[FAIL] {name}");
            Console.WriteLine(ex.Message);
            failed++;
        }
    }

    public void Finish()
    {
        Console.WriteLine($"Passed: {passed}, Failed: {failed}");
        if (failed > 0)
            Environment.Exit(1);
    }

    public static void AssertEqual<T>(T expected, T actual, string message)
        where T : notnull
    {
        if (!Equals(expected, actual))
            throw new InvalidOperationException($"{message}: expected {expected}, actual {actual}");
    }

    public static void AssertEqual(double expected, double actual, string message, double tolerance = 1e-9)
    {
        if (Math.Abs(expected - actual) > tolerance)
            throw new InvalidOperationException($"{message}: expected {expected}, actual {actual}");
    }

    public static void AssertTrue(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
