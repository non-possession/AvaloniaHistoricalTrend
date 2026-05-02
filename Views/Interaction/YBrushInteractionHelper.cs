namespace AvaloniaApplication2.Views.Interaction;

public static class YBrushInteractionHelper
{
    public static bool GetClosestIsUpper(double pointerY, double upperY, double lowerY)
    {
        return !TrackInteractionHelper.IsCloserToEnd(pointerY, upperY, lowerY);
    }

    public static (bool NearUpper, bool NearLower) GetHoverState(double pointerY, double upperY, double lowerY)
    {
        return TrackInteractionHelper.GetHoverState(pointerY, upperY, lowerY, 10);
    }
}
