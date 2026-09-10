using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace UmiaPP.Converters;

public sealed class DifferensRowBrushConverter : IValueConverter
{
    private static readonly SolidColorBrush NegativeBrush = CreateBrush("#D9F2D9");
    private static readonly SolidColorBrush PositiveBrush = CreateBrush("#F8D7DA");
    private static readonly SolidColorBrush NeutralBrush = Brushes.Transparent;

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not double differens || differens == 0)
            return NeutralBrush;

        return differens < 0 ? NegativeBrush : PositiveBrush;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();

    private static SolidColorBrush CreateBrush(string hex)
    {
        var brush = (SolidColorBrush)new BrushConverter().ConvertFromString(hex)!;
        brush.Freeze();
        return brush;
    }
}
