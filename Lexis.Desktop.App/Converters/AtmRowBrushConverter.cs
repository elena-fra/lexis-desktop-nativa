using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace Lexis.Desktop.App.Converters;
/// <summary>Row background: flow-focus > ATM > default. Pass IsFlowFocus as ConverterParameter via MultiBinding, or use FlowFocusRowBrushConverter.</summary>
public sealed class AtmRowBrushConverter : IValueConverter
{
    public static readonly AtmRowBrushConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var atm = value is true;
        return atm
            ? SolidColorBrush.Parse("#1A2F4A")
            : SolidColorBrush.Parse("#0B1220");
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

public sealed class FlowFocusRowBrushConverter : IMultiValueConverter
{
    public static readonly FlowFocusRowBrushConverter Instance = new();

    public object? Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
    {
        static bool Flag(object? v) => v is bool b && b;
        if (values.Count > 0 && Flag(values[0])) return SolidColorBrush.Parse("#3B2F1A");
        if (values.Count > 1 && Flag(values[1])) return SolidColorBrush.Parse("#1A2F4A");
        return SolidColorBrush.Parse("#0B1220");
    }
}
