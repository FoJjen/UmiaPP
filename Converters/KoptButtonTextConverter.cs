using System.Globalization;
using System.Windows.Data;

namespace UmiaPP.Converters;

public sealed class KoptButtonTextConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is true ? "Köpt" : "Markera";

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
