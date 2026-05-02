namespace AvaloniaApplication2.Models;

public sealed class SeriesCardPresentation
{
    public string Name { get; init; } = string.Empty;
    public string ValueText { get; init; } = string.Empty;
    public bool IsVisible { get; init; }
    public string AccentHex { get; init; } = "#000000";
    public bool IsLeftSelected { get; init; }
    public bool IsRightSelected { get; init; }
}
