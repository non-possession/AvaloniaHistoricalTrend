using System;

namespace AvaloniaApplication2.Models;

public sealed class CrosshairState
{
    public bool IsActive { get; set; }
    public DateTime HoveredTime { get; set; }
    public double HoveredY { get; set; }
}
