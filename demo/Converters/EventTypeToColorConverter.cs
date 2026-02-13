using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace AgiDemo.Converters;

public class EventTypeToColorConverter : IValueConverter
{
    public static readonly EventTypeToColorConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is string color)
        {
            try { return new SolidColorBrush(Color.Parse(color)); }
            catch { /* fall through */ }
        }
        return new SolidColorBrush(Color.Parse("#888888"));
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
