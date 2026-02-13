using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media.Imaging;

namespace AgiDemo.Converters;

public class Base64ToImageConverter : IValueConverter
{
    public static readonly Base64ToImageConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is byte[] bytes && bytes.Length > 0)
        {
            using var ms = new MemoryStream(bytes);
            return new Bitmap(ms);
        }
        return null;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
