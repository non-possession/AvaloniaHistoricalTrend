using System;

namespace AvaloniaApplication2.Views.Interaction;

public static class TimeWindowInteractionHelper
{
    public static DateTime ConstrainStart(DateTime candidateStart, DateTime currentEnd, TimeSpan minimumSpan)
    {
        return candidateStart > currentEnd - minimumSpan
            ? currentEnd - minimumSpan
            : candidateStart;
    }

    public static DateTime ConstrainEnd(DateTime currentStart, DateTime candidateEnd, TimeSpan minimumSpan)
    {
        return candidateEnd < currentStart + minimumSpan
            ? currentStart + minimumSpan
            : candidateEnd;
    }

    public static TimeSpan CurrentSpan(DateTime visibleStart, DateTime visibleEnd) => visibleEnd - visibleStart;
}
