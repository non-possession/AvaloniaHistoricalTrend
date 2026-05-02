using System;
using System.Globalization;
using Avalonia.Controls;
using Avalonia.Media;
using AvaloniaApplication2.Models;
using AvaloniaApplication2.Services;
using AvaloniaApplication2.ViewModels;

namespace AvaloniaApplication2.Views.Interaction;

// 显式控件同步器。
// 当前项目仍有部分控件没有完全绑定化，本类把这些容易遗漏的赋值集中起来，
// 避免 MainWindow.axaml.cs 到处散落“设置文本框/下拉框/变量卡”的代码。
public sealed class ViewControlSynchronizer
{
    private readonly TrendWorkbenchCoordinator coordinator;
    private readonly TextBlock[] seriesValueTextBlocks;
    private readonly TextBox[] seriesNameTextBoxes;
    private readonly SeriesCardItemViewModel[] seriesCardViewModels;

    public ViewControlSynchronizer(
        TrendWorkbenchCoordinator coordinator,
        TextBlock[] seriesValueTextBlocks,
        TextBox[] seriesNameTextBoxes,
        SeriesCardItemViewModel[] seriesCardViewModels)
    {
        this.coordinator = coordinator;
        this.seriesValueTextBlocks = seriesValueTextBlocks;
        this.seriesNameTextBoxes = seriesNameTextBoxes;
        this.seriesCardViewModels = seriesCardViewModels;
    }

    public void SyncTimeTexts(
        TextBlock leftTimeText,
        TextBlock rightTimeText,
        TextBlock bottomLeftRangeText,
        TextBlock bottomRightRangeText)
    {
        TrendWorkbenchState state = coordinator.State;
        leftTimeText.Text = state.TimeWindow.VisibleStart.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
        rightTimeText.Text = state.TimeWindow.VisibleEnd.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
        bottomLeftRangeText.Text = leftTimeText.Text;
        bottomRightRangeText.Text = rightTimeText.Text;
    }

    public void SyncAxisSelectors(
        ComboBox leftSelector,
        ComboBox rightSelector,
        TextBlock leftCurrentSeriesText,
        TextBlock rightCurrentSeriesText)
    {
        TrendWorkbenchState state = coordinator.State;
        string[] leftItems = state.LayoutMode == TrendLayoutMode.SingleAxis
            ? coordinator.GetSeriesNames()
            : GetAxisSeriesNames(isRightAxis: false);
        UpdateComboBoxItems(leftSelector, leftItems);
        UpdateCurrentSeriesText(leftCurrentSeriesText, state.AxisSelection.LeftSeriesIndex);

        if (state.LayoutMode == TrendLayoutMode.DualAxis)
        {
            UpdateComboBoxItems(rightSelector, GetAxisSeriesNames(isRightAxis: true));
            UpdateCurrentSeriesText(rightCurrentSeriesText, state.AxisSelection.RightSeriesIndex);
        }
        else
        {
            UpdateComboBoxItems(rightSelector, Array.Empty<string>());
            rightCurrentSeriesText.Text = string.Empty;
        }
    }

    public void SyncAxisPanelTexts(TextBlock leftRangeText, TextBlock rightRangeText)
    {
        TrendWorkbenchState state = coordinator.State;
        SyncRangeText(leftRangeText, state.AxisSelection.LeftSeriesIndex);

        if (state.LayoutMode == TrendLayoutMode.DualAxis)
            SyncRangeText(rightRangeText, state.AxisSelection.RightSeriesIndex);
        else
            rightRangeText.Text = string.Empty;
    }

    public void SyncSeriesNameEditors()
    {
        TrendWorkbenchState state = coordinator.State;
        for (int i = 0; i < seriesNameTextBoxes.Length; i++)
            seriesNameTextBoxes[i].Text = state.Series[i].Name;
    }

    public void SyncSeriesValueTexts()
    {
        TrendWorkbenchState state = coordinator.State;
        DateTime targetTime = state.Crosshair.IsActive ? state.Crosshair.HoveredTime : state.TimeWindow.VisibleEnd;
        string prefix = state.Crosshair.IsActive ? "光标" : "末端";

        for (int i = 0; i < seriesValueTextBlocks.Length; i++)
        {
            double value = coordinator.GetSeriesValueAtTime(i, targetTime);
            string hiddenSuffix = state.Series[i].IsVisible ? string.Empty : " (隐藏)";
            // 右侧变量卡的当前值跟随十字光标；未激活时显示当前窗口右端值。
            seriesValueTextBlocks[i].Text = $"{prefix} {value:0.##}{hiddenSuffix}";
        }
    }

    public void SyncSeriesVisibilityViewModels()
    {
        TrendWorkbenchState state = coordinator.State;
        for (int i = 0; i < seriesCardViewModels.Length; i++)
            seriesCardViewModels[i].IsVisible = state.Series[i].IsVisible;
    }

    private void SyncRangeText(TextBlock target, int seriesIndex)
    {
        TrendSeriesState series = coordinator.State.Series[seriesIndex];
        NumericRange range = coordinator.GetEngineeringRange(seriesIndex);
        string rangeMode = series.CustomEngineeringRange.HasValue ? "自定义" : "默认";
        target.Text = $"{range.Min:0.##} ~ {range.Max:0.##} ({rangeMode})";
        target.Foreground = ParseBrush(series.ColorHex);
    }

    private void UpdateCurrentSeriesText(TextBlock target, int seriesIndex)
    {
        TrendSeriesState series = coordinator.State.Series[seriesIndex];
        target.Text = $"当前参考变量: {series.Name}";
        target.Foreground = ParseBrush(series.ColorHex);
    }

    private string[] GetAxisSeriesNames(bool isRightAxis)
    {
        int[] indices = coordinator.GetAxisCandidateIndices(isRightAxis);
        string[] names = new string[indices.Length];
        for (int i = 0; i < indices.Length; i++)
            names[i] = coordinator.State.Series[indices[i]].Name;

        return names;
    }

    private static void UpdateComboBoxItems(ComboBox comboBox, string[] items)
    {
        comboBox.SelectedIndex = -1;
        comboBox.ItemsSource = null;
        // 先清空再赋值，可避免 Avalonia ComboBox 在集合替换时保留无效 SelectedIndex。
        comboBox.ItemsSource = items;
    }

    private static SolidColorBrush ParseBrush(string colorHex)
    {
        return new SolidColorBrush(Color.Parse(colorHex));
    }
}
