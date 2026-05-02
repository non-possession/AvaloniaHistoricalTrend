using Avalonia.Controls.Shapes;
using AvaloniaApplication2.Services;

namespace AvaloniaApplication2.Views.Interaction;

public static class CrosshairOverlayHelper
{
    public static void Apply(CrosshairOverlay overlay, Line verticalLine, Line horizontalLine)
    {
        verticalLine.StartPoint = overlay.VerticalStart;
        verticalLine.EndPoint = overlay.VerticalEnd;
        horizontalLine.StartPoint = overlay.HorizontalStart;
        horizontalLine.EndPoint = overlay.HorizontalEnd;
        verticalLine.IsVisible = true;
        horizontalLine.IsVisible = true;
    }

    public static void Hide(Line verticalLine, Line horizontalLine)
    {
        verticalLine.IsVisible = false;
        horizontalLine.IsVisible = false;
    }
}
