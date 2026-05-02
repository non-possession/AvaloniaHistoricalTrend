using System;

namespace AvaloniaApplication2.Models;

public sealed class TimeWindowState
{
    public DateTime TotalStart { get; set; }
    public DateTime TotalEnd { get; set; }
    public DateTime VisibleStart { get; set; }
    public DateTime VisibleEnd { get; set; }
}
