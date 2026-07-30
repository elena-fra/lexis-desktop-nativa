using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace Lexis.Desktop.App.Converters;

/// <summary>BUY → green, SELL → red chip background.</summary>
public sealed class SideAccentConverter : IValueConverter
{
    public static readonly SideAccentConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var side = value?.ToString() ?? "";
        return side.Equals("BUY", StringComparison.OrdinalIgnoreCase)
            ? SolidColorBrush.Parse("#166534")
            : SolidColorBrush.Parse("#7F1D1D");
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
