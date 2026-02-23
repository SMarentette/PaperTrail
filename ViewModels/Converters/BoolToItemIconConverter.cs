using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace PaperTrail.ViewModels;

public sealed class BoolToItemIconConverter : IValueConverter
{
    public static readonly BoolToItemIconConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value is true ? "[DIR]" : "MD";
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
