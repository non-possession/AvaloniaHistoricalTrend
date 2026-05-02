using System;
using System.Globalization;
using System.IO;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
using AvaloniaApplication2.Models;
using AvaloniaApplication2.Services;
using AvaloniaApplication2.ViewModels;
using AvaloniaApplication2.Views.Interaction;

namespace AvaloniaApplication2.Views;

// 主窗口 View 桥接层。
// 本文件保留必须依赖 Avalonia 控件实例、鼠标像素位置和 DispatcherTimer 的代码。
// 业务规则交给 Coordinator/ApplicationService/ViewModel，图表细节交给 ScottPlotTrendAdapter。
public partial class MainWindow : Window
{
    private readonly TrendWorkbenchCoordinator coordinator = new TrendWorkbenchCoordinator();
    private readonly ScottPlotTrendAdapter chartAdapter;
    private readonly TrendWorkbenchApplicationService applicationService;
    private readonly TrendWorkbenchSettingsService settingsService = new TrendWorkbenchSettingsService();
    private readonly TrendWorkbenchSettings settings;
    private readonly MainWindowViewModel viewModel;
    private readonly ViewControlSynchronizer controlSynchronizer;
    private readonly DispatcherTimer realtimeTimer = new DispatcherTimer();

    private bool isUpdatingViewportControls;
    private bool isInitialized;
    private bool isDraggingYBrush;
    private bool isDraggingUpperYBrush;
    private bool isHoveringUpperYBrush;
    private bool isHoveringLowerYBrush;
    private bool isDraggingRightYBrush;
    private bool isDraggingUpperRightYBrush;
    private bool isHoveringUpperRightYBrush;
    private bool isHoveringLowerRightYBrush;
    private bool isDraggingXBrush;
    private bool isDraggingRightXBrush;
    private bool isHoveringLeftXBrush;
    private bool isHoveringRightXBrush;
    private bool isEditingStartTime = true;
    private bool isUpdatingTimeEditorSliders;
    private DateTime timeEditorMinimum;
    private DateTime timeEditorMaximum;

    private readonly Border[] seriesCardBorders;
    private readonly SeriesCardItemViewModel[] seriesCardViewModels;
    private readonly TextBox[] seriesNameTextBoxes;
    private readonly TextBlock[] seriesValueTextBlocks;
    private readonly CheckBox[] seriesCheckBoxes;
    private readonly ComboBox[] axisGroupComboBoxes;

    public MainWindow()
    {
        InitializeComponent();

        // Initialize arrays of UI controls first so any events fired during DataContext assignment
        // won't observe null arrays (checked/unchecked handlers may run during binding updates).
        seriesCardBorders = new Border[]
        {
            Series1CardBorder,
            Series2CardBorder,
            Series3CardBorder,
            Series4CardBorder,
            Series5CardBorder,
            Series6CardBorder,
            Series7CardBorder,
            Series8CardBorder,
        };
        seriesCheckBoxes = new CheckBox[]
        {
            Series1CheckBox,
            Series2CheckBox,
            Series3CheckBox,
            Series4CheckBox,
            Series5CheckBox,
            Series6CheckBox,
            Series7CheckBox,
            Series8CheckBox,
        };
        axisGroupComboBoxes = new ComboBox[]
        {
            Series1AxisGroupComboBox,
            Series2AxisGroupComboBox,
            Series3AxisGroupComboBox,
            Series4AxisGroupComboBox,
            Series5AxisGroupComboBox,
            Series6AxisGroupComboBox,
            Series7AxisGroupComboBox,
            Series8AxisGroupComboBox,
        };
        seriesNameTextBoxes = new TextBox[]
        {
            Series1NameTextBox,
            Series2NameTextBox,
            Series3NameTextBox,
            Series4NameTextBox,
            Series5NameTextBox,
            Series6NameTextBox,
            Series7NameTextBox,
            Series8NameTextBox,
        };
        seriesValueTextBlocks = new TextBlock[]
        {
            Series1ValueTextBlock,
            Series2ValueTextBlock,
            Series3ValueTextBlock,
            Series4ValueTextBlock,
            Series5ValueTextBlock,
            Series6ValueTextBlock,
            Series7ValueTextBlock,
            Series8ValueTextBlock,
        };

        viewModel = DataContext as MainWindowViewModel ?? new MainWindowViewModel();
        DataContext = viewModel;

        // seriesCardViewModels depends on the viewModel, initialize after DataContext is set.
        seriesCardViewModels = new SeriesCardItemViewModel[]
        {
            viewModel.Series1,
            viewModel.Series2,
            viewModel.Series3,
            viewModel.Series4,
            viewModel.Series5,
            viewModel.Series6,
            viewModel.Series7,
            viewModel.Series8,
        };

        settings = settingsService.Load();
        string sqlitePath = Path.IsPathRooted(settings.SqliteDatabasePath)
            ? settings.SqliteDatabasePath
            : Path.Combine(Directory.GetCurrentDirectory(), settings.SqliteDatabasePath);
        applicationService = new TrendWorkbenchApplicationService(
            coordinator,
            new TrendCsvExportService(),
            new TrendPlotImageExportService(),
            new TrendPlotPrintService(),
            new SqliteTrendDataStore(sqlitePath),
            new RandomTrendDataCollector(),
            settingsService,
            settings);
        viewModel.Configure(applicationService);
        viewModel.WorkbenchChanged = () => ApplyViewport(updateControls: true);

        controlSynchronizer = new ViewControlSynchronizer(
            coordinator,
            seriesValueTextBlocks,
            seriesNameTextBoxes,
            seriesCardViewModels);

        chartAdapter = new ScottPlotTrendAdapter(AvaPlot1, settings.ShowLegend);
        chartAdapter.Initialize(coordinator.State);
        applicationService.SetLayoutMode(settings.LayoutMode);
        if (settings.DefaultVisibleDurationSeconds > 0)
            coordinator.SetTimeSpanWindow(TimeSpan.FromSeconds(settings.DefaultVisibleDurationSeconds));
        settingsService.Save(settings);

        realtimeTimer.Interval = TimeSpan.FromSeconds(Math.Max(1, settings.SamplingIntervalSeconds));
        realtimeTimer.Tick += RealtimeTimer_Tick;
        // 默认进入实时模式，定时器只负责触发 ViewModel 动作，不直接生成或写入数据。
        applicationService.SwitchToRealtime();
        realtimeTimer.Start();

        ApplyViewport(updateControls: true);
        isInitialized = true;
    }

    private TrendWorkbenchState State
    {
        get { return coordinator.State; }
    }

    private int LeftSeriesIndex
    {
        get { return State.AxisSelection.LeftSeriesIndex; }
    }

    private int RightSeriesIndex
    {
        get { return State.AxisSelection.RightSeriesIndex; }
    }

    private void UpdateLayoutModeVisuals()
    {
        TrendWorkbenchGrid.ColumnDefinitions = State.LayoutMode == TrendLayoutMode.SingleAxis
            ? new ColumnDefinitions("164,40,*,0,0")
            : new ColumnDefinitions("164,40,*,40,164");

        RightYBrushHostBorder.IsVisible = State.LayoutMode == TrendLayoutMode.DualAxis;
        RightYAxisPanelBorder.IsVisible = State.LayoutMode == TrendLayoutMode.DualAxis;
    }

    private void ApplyViewport(bool updateControls)
    {
        // View 刷新统一从这里进入，避免图表、ViewModel 和显式控件同步各走各的路径。
        coordinator.EnsureValidTimeRange();
        SyncSeriesVisibilityFromUi();
        coordinator.EnsureSelectedAxesVisible();
        coordinator.ApplyStoredRangeFilters();
        chartAdapter.ApplyState(State);
        viewModel.ApplySnapshot(coordinator.BuildPresentationSnapshot());
        SyncModeAndDurationTexts();
        SyncSeriesCardSelectionVisuals();
        controlSynchronizer.SyncSeriesValueTexts();
        UpdateLayoutModeVisuals();

        if (updateControls)
            UpdateControls();

        chartAdapter.Refresh();
    }

    private void SyncSeriesVisibilityFromUi()
    {
        // Defensive guards: some events or calls may occur during initialization
        // before the control fields/arrays are wired up. Skip in that case.
        if (seriesCheckBoxes == null || applicationService == null)
            return;

        for (int i = 0; i < seriesCheckBoxes.Length; i++)
        {
            bool isVisible = seriesCheckBoxes[i].IsChecked == true;
            // 显隐状态来自右侧变量卡，但最终仍通过应用服务进入领域状态。
            applicationService.SetSeriesVisible(i, isVisible);
        }

        controlSynchronizer?.SyncSeriesVisibilityViewModels();
    }

    private void UpdateControls()
    {
        isUpdatingViewportControls = true;
        SyncAxisGroupEditors();

        LeftTimeSlider.Minimum = 0;
        LeftTimeSlider.Maximum = 1;
        LeftTimeSlider.Value = coordinator.NormalizeTime(State.TimeWindow.VisibleStart);

        RightTimeSlider.Minimum = 0;
        RightTimeSlider.Maximum = 1;
        RightTimeSlider.Value = coordinator.NormalizeTime(State.TimeWindow.VisibleEnd);

        controlSynchronizer.SyncTimeTexts(
            LeftTimeText,
            RightTimeText,
            BottomLeftRangeText,
            BottomRightRangeText);
        controlSynchronizer.SyncAxisSelectors(
            YAxisSeriesSelector,
            RightYAxisSeriesSelector,
            LeftCurrentSeriesTextBlock,
            RightCurrentSeriesTextBlock);
        controlSynchronizer.SyncAxisPanelTexts(
            YAxisRangeText,
            RightYAxisRangeText);
        controlSynchronizer.SyncSeriesNameEditors();
        SyncModeAndDurationTexts();
        SyncSeriesCardSelectionVisuals();
        SyncSamplingIntervalTextBox(force: false);

        YAxisSeriesSelector.SelectedIndex = State.LayoutMode == TrendLayoutMode.SingleAxis
            ? LeftSeriesIndex
            : coordinator.GetAxisOptionIndex(isRightAxis: false, LeftSeriesIndex);

        if (State.LayoutMode == TrendLayoutMode.DualAxis)
            RightYAxisSeriesSelector.SelectedIndex = coordinator.GetAxisOptionIndex(isRightAxis: true, RightSeriesIndex);

        YLowerBrushSlider.Minimum = 0;
        YLowerBrushSlider.Maximum = 1;
        YLowerBrushSlider.Value = State.Series[LeftSeriesIndex].LowerBrushFraction;

        YUpperBrushSlider.Minimum = 0;
        YUpperBrushSlider.Maximum = 1;
        YUpperBrushSlider.Value = State.Series[LeftSeriesIndex].UpperBrushFraction;

        UpdateYBrushVisuals();
        UpdateRightYBrushVisuals();
        UpdateXBrushVisuals();
        isUpdatingViewportControls = false;
    }

    private void UpdateYBrushVisuals()
    {
        UpdateBrushVisuals(
            trackGrid: YBrushTrackGrid,
            topShade: YBrushTopShade,
            bottomShade: YBrushBottomShade,
            upperLine: YUpperBrushLine,
            lowerLine: YLowerBrushLine,
            seriesIndex: LeftSeriesIndex,
            isDragging: isDraggingYBrush,
            isDraggingUpper: isDraggingUpperYBrush,
            isHoveringUpper: isHoveringUpperYBrush,
            isHoveringLower: isHoveringLowerYBrush);
    }

    private void UpdateRightYBrushVisuals()
    {
        UpdateBrushVisuals(
            trackGrid: RightYBrushTrackGrid,
            topShade: RightYBrushTopShade,
            bottomShade: RightYBrushBottomShade,
            upperLine: RightYUpperBrushLine,
            lowerLine: RightYLowerBrushLine,
            seriesIndex: RightSeriesIndex,
            isDragging: isDraggingRightYBrush,
            isDraggingUpper: isDraggingUpperRightYBrush,
            isHoveringUpper: isHoveringUpperRightYBrush,
            isHoveringLower: isHoveringLowerRightYBrush);
    }

    private void UpdateBrushVisuals(
        Grid trackGrid,
        Border topShade,
        Border bottomShade,
        Avalonia.Controls.Shapes.Line upperLine,
        Avalonia.Controls.Shapes.Line lowerLine,
        int seriesIndex,
        bool isDragging,
        bool isDraggingUpper,
        bool isHoveringUpper,
        bool isHoveringLower)
    {
        double trackHeight = trackGrid.Bounds.Height;
        double trackWidth = trackGrid.Bounds.Width;
        if (trackHeight <= 0 || trackWidth <= 0)
            return;

        const double overlayWidth = 20;
        double overlayLeft = Math.Max(0, (trackWidth - overlayWidth) / 2);
        double upperY = GetBrushTrackPosition(State.Series[seriesIndex].UpperBrushFraction, trackHeight);
        double lowerY = GetBrushTrackPosition(State.Series[seriesIndex].LowerBrushFraction, trackHeight);
        var accentBrush = ParseBrush(State.Series[seriesIndex].ColorHex);

        topShade.Width = overlayWidth;
        topShade.Height = Math.Max(0, upperY);
        Canvas.SetLeft(topShade, overlayLeft);
        Canvas.SetTop(topShade, 0);

        bottomShade.Width = overlayWidth;
        bottomShade.Height = Math.Max(0, trackHeight - lowerY);
        Canvas.SetLeft(bottomShade, overlayLeft);
        Canvas.SetTop(bottomShade, lowerY);

        upperLine.Stroke = accentBrush;
        upperLine.StrokeThickness = isDragging && isDraggingUpper || isHoveringUpper ? 5 : 3;
        upperLine.StartPoint = new Point(overlayLeft - 6, upperY);
        upperLine.EndPoint = new Point(overlayLeft + overlayWidth + 6, upperY);

        lowerLine.Stroke = accentBrush;
        lowerLine.StrokeThickness = isDragging && !isDraggingUpper || isHoveringLower ? 5 : 3;
        lowerLine.StartPoint = new Point(overlayLeft - 6, lowerY);
        lowerLine.EndPoint = new Point(overlayLeft + overlayWidth + 6, lowerY);
    }

    private void UpdateXBrushVisuals()
    {
        double trackWidth = XBrushTrackGrid.Bounds.Width;
        double trackHeight = XBrushTrackGrid.Bounds.Height;
        if (trackWidth <= 0 || trackHeight <= 0)
            return;

        double leftX = coordinator.NormalizeTime(State.TimeWindow.VisibleStart) * trackWidth;
        double rightX = coordinator.NormalizeTime(State.TimeWindow.VisibleEnd) * trackWidth;
        double top = 2;
        double bottom = Math.Max(top, trackHeight - 2);

        XLeftBrushLine.StrokeThickness = isDraggingXBrush && !isDraggingRightXBrush || isHoveringLeftXBrush ? 5 : 3;
        XLeftBrushLine.StartPoint = new Point(leftX, top);
        XLeftBrushLine.EndPoint = new Point(leftX, bottom);

        XRightBrushLine.StrokeThickness = isDraggingXBrush && isDraggingRightXBrush || isHoveringRightXBrush ? 5 : 3;
        XRightBrushLine.StartPoint = new Point(rightX, top);
        XRightBrushLine.EndPoint = new Point(rightX, bottom);
    }

    private void UpdateBrushGuideLine(int seriesIndex, double brushFraction)
    {
        if (chartAdapter.TryGetBrushGuideLine(
                seriesIndex,
                brushFraction,
                CrosshairCanvas.Bounds.Size,
                coordinator.GetEngineeringRange(seriesIndex),
                out GuideLineOverlay overlay))
        {
            YBrushGuideLine.StartPoint = overlay.Start;
            YBrushGuideLine.EndPoint = overlay.End;
            YBrushGuideLine.Stroke = overlay.Brush;
            YBrushGuideLine.IsVisible = true;
        }
    }

    private void HideYBrushGuideLine()
    {
        YBrushGuideLine.IsVisible = false;
        isDraggingYBrush = false;
        isHoveringUpperYBrush = false;
        isHoveringLowerYBrush = false;
        isDraggingRightYBrush = false;
        isHoveringUpperRightYBrush = false;
        isHoveringLowerRightYBrush = false;
        UpdateYBrushVisuals();
        UpdateRightYBrushVisuals();
    }

    private static double GetBrushTrackPosition(double brushFraction, double trackHeight)
    {
        const double visualPadding = 2;
        double usableHeight = Math.Max(1, trackHeight - 2 * visualPadding);
        return visualPadding + (1 - Clamp(brushFraction, 0, 1)) * usableHeight;
    }

    private static double GetBrushFractionFromTrackPosition(double pointerY, double trackHeight)
    {
        const double visualPadding = 2;
        double usableHeight = Math.Max(1, trackHeight - 2 * visualPadding);
        double normalized = (Clamp(pointerY, visualPadding, trackHeight - visualPadding) - visualPadding) / usableHeight;
        return Clamp(1 - normalized, 0, 1);
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

    private static SolidColorBrush ParseBrush(string colorHex)
    {
        return new SolidColorBrush(Avalonia.Media.Color.Parse(colorHex));
    }

    private void SyncAxisGroupEditors()
    {
        for (int i = 0; i < axisGroupComboBoxes.Length; i++)
        {
            ComboBox comboBox = axisGroupComboBoxes[i];
            comboBox.ItemsSource = new string[] { "Y1", "Y2" };
            comboBox.SelectedIndex = State.AxisGroupAssignments[i] == TrendAxisGroup.Y1 ? 0 : 1;
            comboBox.IsEnabled = State.LayoutMode == TrendLayoutMode.DualAxis;
        }
    }

    private void SyncModeAndDurationTexts()
    {
        TrendModeTextBlock.Text = viewModel.TrendModeText;
        SelectedDurationText.Text = viewModel.SelectedDurationText;
    }

    private void SyncSeriesCardSelectionVisuals()
    {
        for (int i = 0; i < seriesCardBorders.Length; i++)
        {
            bool isLeftSelected = i == State.AxisSelection.LeftSeriesIndex;
            bool isRightSelected = State.LayoutMode == TrendLayoutMode.DualAxis && i == State.AxisSelection.RightSeriesIndex;
            bool isSelected = isLeftSelected || isRightSelected;
            string backgroundHex = isLeftSelected ? "#EAF4FF" : isRightSelected ? "#FFF6DE" : "#E3E3E3";
            string borderHex = isSelected ? State.Series[i].ColorHex : "#A2A2A2";

            seriesCardBorders[i].Background = new SolidColorBrush(Avalonia.Media.Color.Parse(backgroundHex));
            seriesCardBorders[i].BorderBrush = new SolidColorBrush(Avalonia.Media.Color.Parse(borderHex));
            seriesCardBorders[i].BorderThickness = isSelected ? new Thickness(2) : new Thickness(1);
            seriesCardBorders[i].Opacity = State.Series[i].IsVisible ? 1 : 0.62;
        }
    }

    private void YAxisSeriesSelector_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (!isInitialized || isUpdatingViewportControls)
            return;

        if (sender is not ComboBox comboBox || comboBox.SelectedIndex < 0)
            return;

        int seriesIndex = coordinator.GetSeriesIndexFromAxisOption(isRightAxis: false, comboBox.SelectedIndex);
        viewModel.RequestLeftSeries(seriesIndex);
    }

    private void RightYAxisSeriesSelector_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (!isInitialized || isUpdatingViewportControls)
            return;

        int index = sender is ComboBox comboBox ? comboBox.SelectedIndex : RightYAxisSeriesSelector.SelectedIndex;
        if (index < 0)
            return;

        int seriesIndex = coordinator.GetSeriesIndexFromAxisOption(isRightAxis: true, index);
        viewModel.RequestRightSeries(seriesIndex);
    }

    private void VariableNameEditor_LostFocus(object? sender, RoutedEventArgs e)
    {
        if (sender is not TextBox textBox)
            return;

        int index = Array.IndexOf(seriesNameTextBoxes, textBox);
        if (index < 0)
            return;

        coordinator.RenameSeries(index, textBox.Text ?? string.Empty);
        textBox.Text = coordinator.GetSeries(index).Name;
        ApplyViewport(updateControls: true);
    }

    private void PlotOverlay_PointerMoved(object? sender, PointerEventArgs e)
    {
        if (!isInitialized)
            return;

        if (chartAdapter.TryGetCrosshair(e.GetPosition(CrosshairCanvas), CrosshairCanvas.Bounds.Size, out CrosshairOverlay overlay))
        {
            CrosshairOverlayHelper.Apply(overlay, CrosshairVerticalLine, CrosshairHorizontalLine);
            State.Crosshair.IsActive = true;
            State.Crosshair.HoveredTime = overlay.HoveredTime;
            State.Crosshair.HoveredY = overlay.HoveredY;
            viewModel.ApplySnapshot(coordinator.BuildPresentationSnapshot());
            controlSynchronizer.SyncSeriesValueTexts();
            return;
        }

        State.Crosshair.IsActive = false;
        CrosshairOverlayHelper.Hide(CrosshairVerticalLine, CrosshairHorizontalLine);
        viewModel.ApplySnapshot(coordinator.BuildPresentationSnapshot());
        controlSynchronizer.SyncSeriesValueTexts();
    }

    private void PlotOverlay_PointerExited(object? sender, PointerEventArgs e)
    {
        State.Crosshair.IsActive = false;
        CrosshairOverlayHelper.Hide(CrosshairVerticalLine, CrosshairHorizontalLine);
        if (!isDraggingYBrush && !isDraggingRightYBrush)
            YBrushGuideLine.IsVisible = false;
        ApplyViewport(updateControls: false);
    }

    private void ApplyCustomYAxisRange_Click(object? sender, RoutedEventArgs e)
    {
        if (!double.TryParse(YAxisMinTextBox.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out double minValue))
            return;
        if (!double.TryParse(YAxisMaxTextBox.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out double maxValue))
            return;

        viewModel.RequestApplyCustomRange(isRightAxis: false, minValue, maxValue);
    }

    private void ResetDefaultYAxisRange_Click(object? sender, RoutedEventArgs e)
    {
        viewModel.RequestResetCustomRange(isRightAxis: false);
    }

    private void ApplyCustomRightYAxisRange_Click(object? sender, RoutedEventArgs e)
    {
        if (!double.TryParse(RightYAxisMinTextBox.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out double minValue))
            return;
        if (!double.TryParse(RightYAxisMaxTextBox.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out double maxValue))
            return;

        viewModel.RequestApplyCustomRange(isRightAxis: true, minValue, maxValue);
    }

    private void ResetDefaultRightYAxisRange_Click(object? sender, RoutedEventArgs e)
    {
        viewModel.RequestResetCustomRange(isRightAxis: true);
    }

    private void YLowerBrushSlider_ValueChanged(object? sender, RangeBaseValueChangedEventArgs e)
    {
        if (isUpdatingViewportControls)
            return;

        coordinator.SetBrushFraction(LeftSeriesIndex, isUpperBrush: false, e.NewValue);
        ApplyViewport(updateControls: true);
        if (isDraggingYBrush && !isDraggingUpperYBrush)
            UpdateBrushGuideLine(LeftSeriesIndex, State.Series[LeftSeriesIndex].LowerBrushFraction);
    }

    private void YUpperBrushSlider_ValueChanged(object? sender, RangeBaseValueChangedEventArgs e)
    {
        if (isUpdatingViewportControls)
            return;

        coordinator.SetBrushFraction(LeftSeriesIndex, isUpperBrush: true, e.NewValue);
        ApplyViewport(updateControls: true);
        if (isDraggingYBrush && isDraggingUpperYBrush)
            UpdateBrushGuideLine(LeftSeriesIndex, State.Series[LeftSeriesIndex].UpperBrushFraction);
    }

    private void LeftTimeSlider_ValueChanged(object? sender, RangeBaseValueChangedEventArgs e)
    {
        if (isUpdatingViewportControls)
            return;

        DateTime newStart = coordinator.DenormalizeTime(e.NewValue);
        newStart = TimeWindowInteractionHelper.ConstrainStart(newStart, State.TimeWindow.VisibleEnd, TrendWorkbenchCoordinator.MinimumTimeSpan);
        viewModel.RequestSetVisibleWindow(newStart, State.TimeWindow.VisibleEnd);
    }

    private void RightTimeSlider_ValueChanged(object? sender, RangeBaseValueChangedEventArgs e)
    {
        if (isUpdatingViewportControls)
            return;

        DateTime newEnd = coordinator.DenormalizeTime(e.NewValue);
        newEnd = TimeWindowInteractionHelper.ConstrainEnd(State.TimeWindow.VisibleStart, newEnd, TrendWorkbenchCoordinator.MinimumTimeSpan);
        viewModel.RequestSetVisibleWindow(State.TimeWindow.VisibleStart, newEnd);
    }

    private void MoveLeftBoundaryToStart_Click(object? sender, RoutedEventArgs e)
    {
        viewModel.RequestSetVisibleWindow(State.TimeWindow.TotalStart, State.TimeWindow.VisibleEnd);
    }

    private void MoveLeftBoundaryToEnd_Click(object? sender, RoutedEventArgs e)
    {
        viewModel.RequestSetVisibleWindow(State.TimeWindow.VisibleEnd - TrendWorkbenchCoordinator.MinimumTimeSpan, State.TimeWindow.VisibleEnd);
    }

    private void MoveRightBoundaryToStart_Click(object? sender, RoutedEventArgs e)
    {
        viewModel.RequestSetVisibleWindow(State.TimeWindow.VisibleStart, State.TimeWindow.VisibleStart + TrendWorkbenchCoordinator.MinimumTimeSpan);
    }

    private void MoveRightBoundaryToEnd_Click(object? sender, RoutedEventArgs e)
    {
        viewModel.RequestSetVisibleWindow(State.TimeWindow.VisibleStart, State.TimeWindow.TotalEnd);
    }

    private void JumpToLeftmost_Click(object? sender, RoutedEventArgs e)
    {
        TimeSpan span = TimeWindowInteractionHelper.CurrentSpan(State.TimeWindow.VisibleStart, State.TimeWindow.VisibleEnd);
        viewModel.RequestSetVisibleWindow(State.TimeWindow.TotalStart, State.TimeWindow.TotalStart.Add(span));
    }

    private void JumpToCurrent_Click(object? sender, RoutedEventArgs e)
    {
        viewModel.RequestRealtimeMode();
        realtimeTimer.Start();
    }

    private void HistoricalTrend_Click(object? sender, PointerPressedEventArgs e)
    {
        realtimeTimer.Stop();
        viewModel.RequestHistoricalMode();
    }

    private void RealTrend_Click(object? sender, PointerPressedEventArgs e)
    {
        viewModel.RequestRealtimeMode();
        realtimeTimer.Start();
    }

    private void RealtimeTimer_Tick(object? sender, EventArgs e)
    {
        if (State.Mode != TrendMode.Realtime)
            return;

        // Tick 只转发采集请求，随机采集和数据库写入都在服务层完成。
        viewModel.RequestCollectRealtimeSample();
    }

    private void LoadDatabase_Click(object? sender, RoutedEventArgs e)
    {
        viewModel.RequestLoadDatabase(DatabasePathTextBox.Text ?? string.Empty);
        SyncExportStatusText();
    }

    private void ApplySamplingInterval_Click(object? sender, RoutedEventArgs e)
    {
        viewModel.RequestApplySamplingInterval(SamplingIntervalTextBox.Text ?? string.Empty);
        realtimeTimer.Interval = State.SamplingInterval;
        SyncSamplingIntervalTextBox(force: true);
        SyncExportStatusText();
    }

    private void SyncSamplingIntervalTextBox(bool force)
    {
        if (!force && SamplingIntervalTextBox.IsFocused)
            return;

        SamplingIntervalTextBox.Text = viewModel.SamplingIntervalText;
    }

    private void PanLeft_Click(object? sender, RoutedEventArgs e)
    {
        viewModel.RequestShiftWindow(-TimeWindowInteractionHelper.CurrentSpan(State.TimeWindow.VisibleStart, State.TimeWindow.VisibleEnd));
    }

    private void PanLeftDouble_Click(object? sender, RoutedEventArgs e)
    {
        viewModel.RequestShiftWindow(-TimeWindowInteractionHelper.CurrentSpan(State.TimeWindow.VisibleStart, State.TimeWindow.VisibleEnd) * 2);
    }

    private void PanRight_Click(object? sender, RoutedEventArgs e)
    {
        viewModel.RequestShiftWindow(TimeWindowInteractionHelper.CurrentSpan(State.TimeWindow.VisibleStart, State.TimeWindow.VisibleEnd));
    }

    private void PanRightDouble_Click(object? sender, RoutedEventArgs e)
    {
        viewModel.RequestShiftWindow(TimeWindowInteractionHelper.CurrentSpan(State.TimeWindow.VisibleStart, State.TimeWindow.VisibleEnd) * 2);
    }

    private void ZoomTimeIn_Click(object? sender, RoutedEventArgs e)
    {
        realtimeTimer.Stop();
        viewModel.RequestTimeSpanWindow(TimeSpan.FromTicks(TimeWindowInteractionHelper.CurrentSpan(State.TimeWindow.VisibleStart, State.TimeWindow.VisibleEnd).Ticks / 2));
    }

    private void ZoomTimeOut_Click(object? sender, RoutedEventArgs e)
    {
        realtimeTimer.Stop();
        viewModel.RequestTimeSpanWindow(TimeSpan.FromTicks(TimeWindowInteractionHelper.CurrentSpan(State.TimeWindow.VisibleStart, State.TimeWindow.VisibleEnd).Ticks * 2));
    }

    private void Duration1Day_Click(object? sender, RoutedEventArgs e)
    {
        viewModel.RequestDurationPreset(TimeSpan.FromDays(1));
    }

    private void Duration12Hours_Click(object? sender, RoutedEventArgs e)
    {
        viewModel.RequestDurationPreset(TimeSpan.FromHours(12));
    }

    private void Duration6Hours_Click(object? sender, RoutedEventArgs e)
    {
        viewModel.RequestDurationPreset(TimeSpan.FromHours(6));
    }

    private void Duration3Hours_Click(object? sender, RoutedEventArgs e)
    {
        viewModel.RequestDurationPreset(TimeSpan.FromHours(3));
    }

    private void Duration2Hours_Click(object? sender, RoutedEventArgs e)
    {
        viewModel.RequestDurationPreset(TimeSpan.FromHours(2));
    }

    private void Duration1Hour_Click(object? sender, RoutedEventArgs e)
    {
        viewModel.RequestDurationPreset(TimeSpan.FromHours(1));
    }

    private void Duration30Minutes_Click(object? sender, RoutedEventArgs e)
    {
        viewModel.RequestDurationPreset(TimeSpan.FromMinutes(30));
    }

    private void Duration10Minutes_Click(object? sender, RoutedEventArgs e)
    {
        viewModel.RequestDurationPreset(TimeSpan.FromMinutes(10));
    }

    private void Duration5Minutes_Click(object? sender, RoutedEventArgs e)
    {
        viewModel.RequestDurationPreset(TimeSpan.FromMinutes(5));
    }

    private void Duration2Minutes_Click(object? sender, RoutedEventArgs e)
    {
        viewModel.RequestDurationPreset(TimeSpan.FromMinutes(2));
    }

    private void Duration1Minute_Click(object? sender, RoutedEventArgs e)
    {
        viewModel.RequestDurationPreset(TimeSpan.FromMinutes(1));
    }

    private void Duration30Seconds_Click(object? sender, RoutedEventArgs e)
    {
        viewModel.RequestDurationPreset(TimeSpan.FromSeconds(30));
    }

    private void Duration10Seconds_Click(object? sender, RoutedEventArgs e)
    {
        viewModel.RequestDurationPreset(TimeSpan.FromSeconds(10));
    }

    private void SingleAxisLayout_Click(object? sender, RoutedEventArgs e)
    {
        viewModel.RequestLayoutMode(TrendLayoutMode.SingleAxis);
    }

    private void DualAxisLayout_Click(object? sender, RoutedEventArgs e)
    {
        viewModel.RequestLayoutMode(TrendLayoutMode.DualAxis);
    }
    private void ExportCsv_Click(object? sender, RoutedEventArgs e)
    {
        viewModel.RequestExportCsv();
        SyncExportStatusText();
    }

    private void SavePlotImage_Click(object? sender, RoutedEventArgs e)
    {
        viewModel.RequestSavePlotImage(chartAdapter);
        SyncExportStatusText();
    }

    private void PrintPlot_Click(object? sender, RoutedEventArgs e)
    {
        viewModel.RequestPrintPlot(chartAdapter);
        SyncExportStatusText();
    }

    private void SyncExportStatusText()
    {
        ExportStatusTextBlock.Text = viewModel.ExportStatusText;
    }

    private void StartTimeEntry_Click(object? sender, RoutedEventArgs e)
    {
        OpenTimeEditor(isStartTime: true);
    }

    private void EndTimeEntry_Click(object? sender, RoutedEventArgs e)
    {
        OpenTimeEditor(isStartTime: false);
    }

    private void OpenTimeEditor(bool isStartTime)
    {
        isEditingStartTime = isStartTime;
        // 先计算可选范围，再设置滑块，避免用户滑到明显非法的时间点。
        UpdateTimeEditorAllowedRange();
        TimeEditorPopup.PlacementTarget = isStartTime ? BottomLeftRangeButton : BottomRightRangeButton;
        TimeEditorTitleTextBlock.Text = isStartTime ? "编辑起始时间" : "编辑结束时间";
        DateTime value = isStartTime ? State.TimeWindow.VisibleStart : State.TimeWindow.VisibleEnd;
        SetTimeEditorSliders(ClampDateTime(value, timeEditorMinimum, timeEditorMaximum));
        TimeEditorPopup.IsOpen = true;
    }

    private void SetTimeEditorSliders(DateTime value)
    {
        isUpdatingTimeEditorSliders = true;
        DateTime clampedValue = ClampDateTime(value, timeEditorMinimum, timeEditorMaximum);
        ApplyTimeEditorSliderBounds(clampedValue);
        TimeYearSlider.Value = clampedValue.Year;
        TimeMonthSlider.Value = clampedValue.Month;
        TimeDaySlider.Value = clampedValue.Day;
        TimeHourSlider.Value = clampedValue.Hour;
        TimeMinuteSlider.Value = clampedValue.Minute;
        TimeSecondSlider.Value = clampedValue.Second;
        isUpdatingTimeEditorSliders = false;
        UpdateTimeEditorText();
    }

    private void ApplyTimeEditorSliderBounds(DateTime value)
    {
        TimeYearSlider.Minimum = timeEditorMinimum.Year;
        TimeYearSlider.Maximum = timeEditorMaximum.Year;

        // 时间编辑器按年月日时分秒逐级收窄范围，提升交互时的约束感。
        int minMonth = value.Year == timeEditorMinimum.Year ? timeEditorMinimum.Month : 1;
        int maxMonth = value.Year == timeEditorMaximum.Year ? timeEditorMaximum.Month : 12;
        TimeMonthSlider.Minimum = minMonth;
        TimeMonthSlider.Maximum = maxMonth;

        int minDay = IsSameYearAndMonth(value, timeEditorMinimum) ? timeEditorMinimum.Day : 1;
        int maxDay = IsSameYearAndMonth(value, timeEditorMaximum) ? timeEditorMaximum.Day : DateTime.DaysInMonth(value.Year, value.Month);
        TimeDaySlider.Minimum = minDay;
        TimeDaySlider.Maximum = maxDay;

        int minHour = IsSameDate(value, timeEditorMinimum) ? timeEditorMinimum.Hour : 0;
        int maxHour = IsSameDate(value, timeEditorMaximum) ? timeEditorMaximum.Hour : 23;
        TimeHourSlider.Minimum = minHour;
        TimeHourSlider.Maximum = maxHour;

        int minMinute = IsSameDateAndHour(value, timeEditorMinimum) ? timeEditorMinimum.Minute : 0;
        int maxMinute = IsSameDateAndHour(value, timeEditorMaximum) ? timeEditorMaximum.Minute : 59;
        TimeMinuteSlider.Minimum = minMinute;
        TimeMinuteSlider.Maximum = maxMinute;

        int minSecond = IsSameDateHourAndMinute(value, timeEditorMinimum) ? timeEditorMinimum.Second : 0;
        int maxSecond = IsSameDateHourAndMinute(value, timeEditorMaximum) ? timeEditorMaximum.Second : 59;
        TimeSecondSlider.Minimum = minSecond;
        TimeSecondSlider.Maximum = maxSecond;
    }

    private void UpdateTimeEditorAllowedRange()
    {
        if (isEditingStartTime)
        {
            timeEditorMinimum = State.TimeWindow.TotalStart;
            // 起始时间必须早于结束时间，并保留最小窗口跨度。
            timeEditorMaximum = State.TimeWindow.VisibleEnd - TrendWorkbenchCoordinator.MinimumTimeSpan;
            if (timeEditorMaximum > State.TimeWindow.TotalEnd)
                timeEditorMaximum = State.TimeWindow.TotalEnd;
        }
        else
        {
            // 结束时间必须晚于起始时间，并保留最小窗口跨度。
            timeEditorMinimum = State.TimeWindow.VisibleStart + TrendWorkbenchCoordinator.MinimumTimeSpan;
            if (timeEditorMinimum < State.TimeWindow.TotalStart)
                timeEditorMinimum = State.TimeWindow.TotalStart;

            timeEditorMaximum = State.TimeWindow.TotalEnd;
        }

        if (timeEditorMaximum < timeEditorMinimum)
            timeEditorMaximum = timeEditorMinimum;
    }

    private void SetTimeEditorSliderValues(DateTime value)
    {
        TimeYearSlider.Value = value.Year;
        TimeMonthSlider.Value = value.Month;
        TimeDaySlider.Value = value.Day;
        TimeHourSlider.Value = value.Hour;
        TimeMinuteSlider.Value = value.Minute;
        TimeSecondSlider.Value = value.Second;
    }

    private void TimeEditorSlider_ValueChanged(object? sender, RangeBaseValueChangedEventArgs e)
    {
        if (isUpdatingTimeEditorSliders || !IsTimeEditorReady())
            return;

        UpdateTimeEditorText();
    }

    private void UpdateTimeEditorText()
    {
        if (!IsTimeEditorReady())
            return;

        DateTime value = ClampDateTime(GetTimeEditorValueFromSliders(), timeEditorMinimum, timeEditorMaximum);

        isUpdatingTimeEditorSliders = true;
        ApplyTimeEditorSliderBounds(value);
        SetTimeEditorSliderValues(value);
        isUpdatingTimeEditorSliders = false;

        int year = value.Year;
        int month = value.Month;
        int day = value.Day;
        int hour = value.Hour;
        int minute = value.Minute;
        int second = value.Second;

        TimeYearTextBox.Text = year.ToString(CultureInfo.InvariantCulture);
        TimeMonthTextBox.Text = month.ToString("00", CultureInfo.InvariantCulture);
        TimeDayTextBox.Text = day.ToString("00", CultureInfo.InvariantCulture);
        TimeHourTextBox.Text = hour.ToString("00", CultureInfo.InvariantCulture);
        TimeMinuteTextBox.Text = minute.ToString("00", CultureInfo.InvariantCulture);
        TimeSecondTextBox.Text = second.ToString("00", CultureInfo.InvariantCulture);
        TimeEditorPreviewTextBlock.Text = $"{year:0000}-{month:00}-{day:00} {hour:00}:{minute:00}:{second:00}";
    }

    private DateTime GetTimeEditorValue()
    {
        if (!IsTimeEditorReady())
            return isEditingStartTime ? State.TimeWindow.VisibleStart : State.TimeWindow.VisibleEnd;

        DateTime value = GetTimeEditorValueFromTextBoxes();
        return ClampDateTime(value, timeEditorMinimum, timeEditorMaximum);
    }

    private DateTime GetTimeEditorValueFromSliders()
    {
        int year = RoundSliderValue(TimeYearSlider);
        int month = RoundSliderValue(TimeMonthSlider);
        int day = RoundSliderValue(TimeDaySlider);
        int hour = RoundSliderValue(TimeHourSlider);
        int minute = RoundSliderValue(TimeMinuteSlider);
        int second = RoundSliderValue(TimeSecondSlider);
        return CreateSafeDateTime(year, month, day, hour, minute, second);
    }

    private DateTime GetTimeEditorValueFromTextBoxes()
    {
        int year = ReadTimePart(TimeYearTextBox, RoundSliderValue(TimeYearSlider), (int)TimeYearSlider.Minimum, (int)TimeYearSlider.Maximum);
        int month = ReadTimePart(TimeMonthTextBox, RoundSliderValue(TimeMonthSlider), (int)TimeMonthSlider.Minimum, (int)TimeMonthSlider.Maximum);
        int maxDay = DateTime.DaysInMonth(year, month);
        int day = ReadTimePart(TimeDayTextBox, RoundSliderValue(TimeDaySlider), (int)TimeDaySlider.Minimum, Math.Min((int)TimeDaySlider.Maximum, maxDay));
        int hour = ReadTimePart(TimeHourTextBox, RoundSliderValue(TimeHourSlider), (int)TimeHourSlider.Minimum, (int)TimeHourSlider.Maximum);
        int minute = ReadTimePart(TimeMinuteTextBox, RoundSliderValue(TimeMinuteSlider), (int)TimeMinuteSlider.Minimum, (int)TimeMinuteSlider.Maximum);
        int second = ReadTimePart(TimeSecondTextBox, RoundSliderValue(TimeSecondSlider), (int)TimeSecondSlider.Minimum, (int)TimeSecondSlider.Maximum);
        return CreateSafeDateTime(year, month, day, hour, minute, second);
    }

    private bool IsTimeEditorReady()
    {
        return TimeYearSlider != null
            && TimeMonthSlider != null
            && TimeDaySlider != null
            && TimeHourSlider != null
            && TimeMinuteSlider != null
            && TimeSecondSlider != null
            && TimeYearTextBox != null
            && TimeMonthTextBox != null
            && TimeDayTextBox != null
            && TimeHourTextBox != null
            && TimeMinuteTextBox != null
            && TimeSecondTextBox != null
            && TimeEditorPreviewTextBlock != null;
    }

    private static int RoundSliderValue(Slider slider)
    {
        return (int)Math.Round(slider.Value);
    }

    private static int ReadTimePart(TextBox textBox, int fallback, int min, int max)
    {
        if (!int.TryParse(textBox.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int value))
            value = fallback;

        return (int)Clamp(value, min, max);
    }

    private static DateTime CreateSafeDateTime(int year, int month, int day, int hour, int minute, int second)
    {
        int safeMonth = (int)Clamp(month, 1, 12);
        int maxDay = DateTime.DaysInMonth(year, safeMonth);
        int safeDay = (int)Clamp(day, 1, maxDay);
        int safeHour = (int)Clamp(hour, 0, 23);
        int safeMinute = (int)Clamp(minute, 0, 59);
        int safeSecond = (int)Clamp(second, 0, 59);
        return new DateTime(year, safeMonth, safeDay, safeHour, safeMinute, safeSecond, DateTimeKind.Local);
    }

    private static DateTime ClampDateTime(DateTime value, DateTime minimum, DateTime maximum)
    {
        if (maximum < minimum)
            return minimum;
        if (value < minimum)
            return minimum;
        if (value > maximum)
            return maximum;
        return value;
    }

    private static bool IsSameYearAndMonth(DateTime left, DateTime right)
    {
        return left.Year == right.Year && left.Month == right.Month;
    }

    private static bool IsSameDate(DateTime left, DateTime right)
    {
        return left.Year == right.Year
            && left.Month == right.Month
            && left.Day == right.Day;
    }

    private static bool IsSameDateAndHour(DateTime left, DateTime right)
    {
        return IsSameDate(left, right) && left.Hour == right.Hour;
    }

    private static bool IsSameDateHourAndMinute(DateTime left, DateTime right)
    {
        return IsSameDateAndHour(left, right) && left.Minute == right.Minute;
    }

    private void TimeEditorTextBox_LostFocus(object? sender, RoutedEventArgs e)
    {
        if (!IsTimeEditorReady())
            return;

        DateTime value = GetTimeEditorValue();
        SetTimeEditorSliders(value);
    }

    private void CancelTimeEditor_Click(object? sender, RoutedEventArgs e)
    {
        TimeEditorPopup.IsOpen = false;
    }

    private void ApplyTimeEditor_Click(object? sender, RoutedEventArgs e)
    {
        DateTime selectedTime = GetTimeEditorValue();
        bool ok = isEditingStartTime
            ? viewModel.RequestApplyEditedStartTime(selectedTime)
            : viewModel.RequestApplyEditedEndTime(selectedTime);
        SyncExportStatusText();
        if (ok)
            realtimeTimer.Stop();
        if (ok)
            TimeEditorPopup.IsOpen = false;
    }

    private void SeriesVisibilityChanged(object? sender, RoutedEventArgs e)
    {
        // Avoid handling visibility changes during initialization before controls are wired up.
        if (!isInitialized || seriesCheckBoxes == null)
            return;

        ApplyViewport(updateControls: true);
    }

    private void AxisGroupComboBox_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (!isInitialized || isUpdatingViewportControls || sender is not ComboBox comboBox || comboBox.SelectedIndex < 0)
            return;

        int index = Array.IndexOf(axisGroupComboBoxes, comboBox);
        if (index < 0)
            return;

        TrendAxisGroup group = comboBox.SelectedIndex == 0 ? TrendAxisGroup.Y1 : TrendAxisGroup.Y2;
        viewModel.RequestAxisGroupAssignment(index, group);
        SyncExportStatusText();
    }

    private void VariableCard_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!isInitialized || sender is not Border border)
            return;

        if (e.Source is CheckBox or TextBox)
            return;

        int index = Array.IndexOf(seriesCardBorders, border);
        if (index < 0)
            return;

        viewModel.RequestSeriesSelection(index);
    }

    private void YAxisRangeCardBorder_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!isInitialized || e.Source is TextBox or Button or Slider)
            return;

        viewModel.RequestCycleAxisSeries(isRightAxis: false);
        e.Handled = true;
    }

    private void RightYAxisPanelBorder_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!isInitialized || e.Source is TextBox or Button or Slider or ComboBoxItem or ComboBox)
            return;

        viewModel.RequestCycleAxisSeries(isRightAxis: true);
        e.Handled = true;
    }

    private void RightYAxisRangeCardBorder_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!isInitialized || e.Source is TextBox or Button or Slider)
            return;

        viewModel.RequestCycleAxisSeries(isRightAxis: true);
        e.Handled = true;
    }

    private void YBrushTrackGrid_SizeChanged(object? sender, SizeChangedEventArgs e)
    {
        UpdateYBrushVisuals();
    }

    private void RightYBrushTrackGrid_SizeChanged(object? sender, SizeChangedEventArgs e)
    {
        UpdateRightYBrushVisuals();
    }

    private void XBrushTrackGrid_SizeChanged(object? sender, SizeChangedEventArgs e)
    {
        UpdateXBrushVisuals();
    }

    private void YBrushTrackGrid_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        HandleYBrushPressed(
            sender,
            e,
            isRightAxis: false,
            getClosestUpper: GetClosestYBrushIsUpper,
            updateFromPointer: UpdateYBrushFromPointer);
    }

    private void YBrushTrackGrid_PointerEntered(object? sender, PointerEventArgs e)
    {
        UpdateYBrushHoverState(e);
    }

    private void YBrushTrackGrid_PointerMoved(object? sender, PointerEventArgs e)
    {
        if (!isDraggingYBrush)
        {
            UpdateYBrushHoverState(e);
            return;
        }

        HandleYBrushMoved(e, UpdateYBrushFromPointer);
    }

    private void YBrushTrackGrid_PointerExited(object? sender, PointerEventArgs e)
    {
        if (isDraggingYBrush)
            return;

        isHoveringUpperYBrush = false;
        isHoveringLowerYBrush = false;
        UpdateYBrushVisuals();
    }

    private void YBrushTrackGrid_PointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        HandleYBrushReleased(e);
    }

    private void YBrushTrackGrid_PointerCaptureLost(object? sender, PointerCaptureLostEventArgs e)
    {
        HideYBrushGuideLine();
    }

    private void RightYBrushTrackGrid_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        HandleYBrushPressed(
            sender,
            e,
            isRightAxis: true,
            getClosestUpper: GetClosestRightYBrushIsUpper,
            updateFromPointer: UpdateRightYBrushFromPointer);
    }

    private void RightYBrushTrackGrid_PointerEntered(object? sender, PointerEventArgs e)
    {
        UpdateRightYBrushHoverState(e);
    }

    private void RightYBrushTrackGrid_PointerMoved(object? sender, PointerEventArgs e)
    {
        if (!isDraggingRightYBrush)
        {
            UpdateRightYBrushHoverState(e);
            return;
        }

        HandleYBrushMoved(e, UpdateRightYBrushFromPointer);
    }

    private void RightYBrushTrackGrid_PointerExited(object? sender, PointerEventArgs e)
    {
        if (isDraggingRightYBrush)
            return;

        isHoveringUpperRightYBrush = false;
        isHoveringLowerRightYBrush = false;
        UpdateRightYBrushVisuals();
    }

    private void RightYBrushTrackGrid_PointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        HandleYBrushReleased(e);
    }

    private void RightYBrushTrackGrid_PointerCaptureLost(object? sender, PointerCaptureLostEventArgs e)
    {
        HideYBrushGuideLine();
    }

    private void XBrushTrackGrid_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        HandleXBrushPressed(sender, e);
    }

    private void XBrushTrackGrid_PointerMoved(object? sender, PointerEventArgs e)
    {
        if (!isDraggingXBrush)
        {
            UpdateXBrushHoverState(e);
            return;
        }

        HandleXBrushMoved(e);
    }

    private void XBrushTrackGrid_PointerEntered(object? sender, PointerEventArgs e)
    {
        UpdateXBrushHoverState(e);
    }

    private void XBrushTrackGrid_PointerExited(object? sender, PointerEventArgs e)
    {
        if (isDraggingXBrush)
            return;

        isHoveringLeftXBrush = false;
        isHoveringRightXBrush = false;
        UpdateXBrushVisuals();
    }

    private void XBrushTrackGrid_PointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        HandleXBrushReleased(e);
    }

    private void XBrushTrackGrid_PointerCaptureLost(object? sender, PointerCaptureLostEventArgs e)
    {
        isDraggingXBrush = false;
        isHoveringLeftXBrush = false;
        isHoveringRightXBrush = false;
        UpdateXBrushVisuals();
    }

    private bool GetClosestXBrushIsRight(PointerEventArgs e)
    {
        double width = XBrushTrackGrid.Bounds.Width;
        if (width <= 0)
            return true;

        double pointerX = Clamp(e.GetPosition(XBrushTrackGrid).X, 0, width);
        return XBrushInteractionHelper.GetClosestIsRight(pointerX, width, coordinator, State.TimeWindow);
    }

    private void UpdateXBrushHoverState(PointerEventArgs e)
    {
        double width = XBrushTrackGrid.Bounds.Width;
        if (width <= 0)
            return;

        double pointerX = Clamp(e.GetPosition(XBrushTrackGrid).X, 0, width);
        (isHoveringLeftXBrush, isHoveringRightXBrush) = XBrushInteractionHelper.GetHoverState(pointerX, width, coordinator, State.TimeWindow);
        UpdateXBrushVisuals();
    }

    private void UpdateXBrushFromPointer(PointerEventArgs e)
    {
        double width = XBrushTrackGrid.Bounds.Width;
        if (width <= 0)
            return;

        double normalized = Clamp(e.GetPosition(XBrushTrackGrid).X / width, 0, 1);
        if (isDraggingRightXBrush)
        {
            DateTime newEnd = coordinator.DenormalizeTime(normalized);
            newEnd = TimeWindowInteractionHelper.ConstrainEnd(State.TimeWindow.VisibleStart, newEnd, TrendWorkbenchCoordinator.MinimumTimeSpan);
            viewModel.RequestSetVisibleWindow(State.TimeWindow.VisibleStart, newEnd);
        }
        else
        {
            DateTime newStart = coordinator.DenormalizeTime(normalized);
            newStart = TimeWindowInteractionHelper.ConstrainStart(newStart, State.TimeWindow.VisibleEnd, TrendWorkbenchCoordinator.MinimumTimeSpan);
            viewModel.RequestSetVisibleWindow(newStart, State.TimeWindow.VisibleEnd);
        }
    }

    private bool GetClosestYBrushIsUpper(PointerEventArgs e)
    {
        double height = YBrushTrackGrid.Bounds.Height;
        if (height <= 0)
            return false;

        double pointerY = Clamp(e.GetPosition(YBrushTrackGrid).Y, 0, height);
        double upperY = GetBrushTrackPosition(State.Series[LeftSeriesIndex].UpperBrushFraction, height);
        double lowerY = GetBrushTrackPosition(State.Series[LeftSeriesIndex].LowerBrushFraction, height);
        return YBrushInteractionHelper.GetClosestIsUpper(pointerY, upperY, lowerY);
    }

    private void UpdateYBrushFromPointer(PointerEventArgs e)
    {
        double height = YBrushTrackGrid.Bounds.Height;
        if (height <= 0)
            return;

        double pointerY = Clamp(e.GetPosition(YBrushTrackGrid).Y, 0, height);
        double fraction = GetBrushFractionFromTrackPosition(pointerY, height);
        coordinator.SetBrushFraction(LeftSeriesIndex, isDraggingUpperYBrush, fraction);
        ApplyViewport(updateControls: true);
        UpdateBrushGuideLine(LeftSeriesIndex, isDraggingUpperYBrush ? State.Series[LeftSeriesIndex].UpperBrushFraction : State.Series[LeftSeriesIndex].LowerBrushFraction);
    }

    private void UpdateYBrushHoverState(PointerEventArgs e)
    {
        double height = YBrushTrackGrid.Bounds.Height;
        if (height <= 0)
            return;

        double pointerY = Clamp(e.GetPosition(YBrushTrackGrid).Y, 0, height);
        double upperY = GetBrushTrackPosition(State.Series[LeftSeriesIndex].UpperBrushFraction, height);
        double lowerY = GetBrushTrackPosition(State.Series[LeftSeriesIndex].LowerBrushFraction, height);
        (isHoveringUpperYBrush, isHoveringLowerYBrush) = YBrushInteractionHelper.GetHoverState(pointerY, upperY, lowerY);
        UpdateYBrushVisuals();
    }

    private bool GetClosestRightYBrushIsUpper(PointerEventArgs e)
    {
        double height = RightYBrushTrackGrid.Bounds.Height;
        if (height <= 0)
            return false;

        double pointerY = Clamp(e.GetPosition(RightYBrushTrackGrid).Y, 0, height);
        double upperY = GetBrushTrackPosition(State.Series[RightSeriesIndex].UpperBrushFraction, height);
        double lowerY = GetBrushTrackPosition(State.Series[RightSeriesIndex].LowerBrushFraction, height);
        return YBrushInteractionHelper.GetClosestIsUpper(pointerY, upperY, lowerY);
    }

    private void UpdateRightYBrushFromPointer(PointerEventArgs e)
    {
        double height = RightYBrushTrackGrid.Bounds.Height;
        if (height <= 0)
            return;

        double pointerY = Clamp(e.GetPosition(RightYBrushTrackGrid).Y, 0, height);
        double fraction = GetBrushFractionFromTrackPosition(pointerY, height);
        coordinator.SetBrushFraction(RightSeriesIndex, isDraggingUpperRightYBrush, fraction);
        ApplyViewport(updateControls: true);
        UpdateBrushGuideLine(RightSeriesIndex, isDraggingUpperRightYBrush ? State.Series[RightSeriesIndex].UpperBrushFraction : State.Series[RightSeriesIndex].LowerBrushFraction);
    }

    private void UpdateRightYBrushHoverState(PointerEventArgs e)
    {
        double height = RightYBrushTrackGrid.Bounds.Height;
        if (height <= 0)
            return;

        double pointerY = Clamp(e.GetPosition(RightYBrushTrackGrid).Y, 0, height);
        double upperY = GetBrushTrackPosition(State.Series[RightSeriesIndex].UpperBrushFraction, height);
        double lowerY = GetBrushTrackPosition(State.Series[RightSeriesIndex].LowerBrushFraction, height);
        (isHoveringUpperRightYBrush, isHoveringLowerRightYBrush) = YBrushInteractionHelper.GetHoverState(pointerY, upperY, lowerY);
        UpdateRightYBrushVisuals();
    }

    private void HandleYBrushPressed(
        object? sender,
        PointerPressedEventArgs e,
        bool isRightAxis,
        Func<PointerEventArgs, bool> getClosestUpper,
        Action<PointerEventArgs> updateFromPointer)
    {
        if (sender is not InputElement inputElement)
            return;

        if (isRightAxis)
        {
            isDraggingRightYBrush = true;
            isDraggingUpperRightYBrush = getClosestUpper(e);
            isHoveringUpperRightYBrush = isDraggingUpperRightYBrush;
            isHoveringLowerRightYBrush = !isDraggingUpperRightYBrush;
        }
        else
        {
            isDraggingYBrush = true;
            isDraggingUpperYBrush = getClosestUpper(e);
            isHoveringUpperYBrush = isDraggingUpperYBrush;
            isHoveringLowerYBrush = !isDraggingUpperYBrush;
        }

        e.Pointer.Capture(inputElement);
        e.Handled = true;
        updateFromPointer(e);
    }

    private static void HandleYBrushMoved(PointerEventArgs e, Action<PointerEventArgs> updateFromPointer)
    {
        updateFromPointer(e);
        e.Handled = true;
    }

    private void HandleYBrushReleased(PointerReleasedEventArgs e)
    {
        e.Pointer.Capture(null);
        e.Handled = true;
        HideYBrushGuideLine();
    }

    private void HandleXBrushPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is not InputElement inputElement)
            return;

        isDraggingXBrush = true;
        isDraggingRightXBrush = GetClosestXBrushIsRight(e);
        isHoveringLeftXBrush = !isDraggingRightXBrush;
        isHoveringRightXBrush = isDraggingRightXBrush;
        e.Pointer.Capture(inputElement);
        e.Handled = true;
        UpdateXBrushFromPointer(e);
    }

    private void HandleXBrushMoved(PointerEventArgs e)
    {
        UpdateXBrushFromPointer(e);
        e.Handled = true;
    }

    private void HandleXBrushReleased(PointerReleasedEventArgs e)
    {
        e.Pointer.Capture(null);
        isDraggingXBrush = false;
        isHoveringLeftXBrush = false;
        isHoveringRightXBrush = false;
        UpdateXBrushVisuals();
        e.Handled = true;
    }

}
