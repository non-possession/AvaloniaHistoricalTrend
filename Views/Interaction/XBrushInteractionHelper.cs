using AvaloniaApplication2.Services;

namespace AvaloniaApplication2.Views.Interaction;

// X 轴时间刷子的像素判断 helper。
// 只负责“鼠标离哪个刷子更近”和 hover 状态，不直接修改时间窗口。
public static class XBrushInteractionHelper
{
    public static bool GetClosestIsRight(double pointerX, double width, TrendWorkbenchCoordinator coordinator, Models.TimeWindowState timeWindow)
    {
        if (width <= 0)
            return true;

        double leftX = coordinator.NormalizeTime(timeWindow.VisibleStart) * width;
        double rightX = coordinator.NormalizeTime(timeWindow.VisibleEnd) * width;
        return TrackInteractionHelper.IsCloserToEnd(pointerX, leftX, rightX);
    }

    public static (bool NearLeft, bool NearRight) GetHoverState(double pointerX, double width, TrendWorkbenchCoordinator coordinator, Models.TimeWindowState timeWindow)
    {
        if (width <= 0)
            return default;

        double leftX = coordinator.NormalizeTime(timeWindow.VisibleStart) * width;
        double rightX = coordinator.NormalizeTime(timeWindow.VisibleEnd) * width;
        return TrackInteractionHelper.GetHoverState(pointerX, leftX, rightX, 10);
    }
}
