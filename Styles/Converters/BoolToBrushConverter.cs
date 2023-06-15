using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace CryptoApp.Styles.Converters;

public class BoolToBrushConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value is true ? new SolidColorBrush(new Color(0xFF, 0xAD, 0xFF, 0x2F)) : new SolidColorBrush(Colors.SlateGray);
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}