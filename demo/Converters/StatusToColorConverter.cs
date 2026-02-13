using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace AgiDemo.Converters;

public class StatusToColorConverter : IValueConverter
{
    public static readonly StatusToColorConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is string status)
        {
            return status.ToLowerInvariant() switch
            {
                "running" => new SolidColorBrush(Color.Parse("#2ecc71")),
                "paused" => new SolidColorBrush(Color.Parse("#f39c12")),
                "error" => new SolidColorBrush(Color.Parse("#e74c3c")),
                "finished" or "done" => new SolidColorBrush(Color.Parse("#3498db")),
                "ready" => new SolidColorBrush(Color.Parse("#95a5a6")),
                _ => new SolidColorBrush(Color.Parse("#888888")),
            };
        }
        return new SolidColorBrush(Color.Parse("#888888"));
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
