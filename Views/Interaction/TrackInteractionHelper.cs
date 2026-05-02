using System;

namespace AvaloniaApplication2.Views.Interaction;

public static class TrackInteractionHelper
{
    public static bool IsCloserToEnd(double pointer, double start, double end)
    {
        return Math.Abs(pointer - end) <= Math.Abs(pointer - start);
    }

    public static (bool NearStart, bool NearEnd) GetHoverState(double pointer, double start, double end, double hoverDistance)
    {
        return (Math.Abs(pointer - start) <= hoverDistance, Math.Abs(pointer - end) <= hoverDistance);
    }
}
