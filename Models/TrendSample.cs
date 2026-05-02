using System;

namespace AvaloniaApplication2.Models;

public sealed class TrendSample
{
    public DateTime Timestamp { get; init; }
    public double[] Values { get; init; } = [];
}
