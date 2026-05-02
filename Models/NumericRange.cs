namespace AvaloniaApplication2.Models;

public readonly record struct NumericRange(double Min, double Max)
{
    public double Span => Max - Min;
}
