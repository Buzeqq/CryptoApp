using System;
using System.Globalization;
using System.Net;
using Avalonia.Data.Converters;
using FluentAssertions;

namespace CryptoApp.Styles.Converters;

public class SenderToHorizontalAlignmentConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        value.Should().BeOfType<string>();
        return (string)value! == Dns.GetHostName() ? 
            Avalonia.Layout.HorizontalAlignment.Right : 
            Avalonia.Layout.HorizontalAlignment.Left;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}