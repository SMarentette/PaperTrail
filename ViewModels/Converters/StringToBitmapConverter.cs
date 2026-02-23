using System;
using System.Globalization;
using Avalonia;
using Avalonia.Data.Converters;
using Avalonia.Media.Imaging;
using Avalonia.Platform;

namespace PaperTrail.ViewModels;

public sealed class StringToBitmapConverter : IValueConverter
{
    public static readonly StringToBitmapConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not string uriString || string.IsNullOrWhiteSpace(uriString))
        {
            return null;
        }

        try
        {
            var uri = new Uri(uriString);
            var assets = AssetLoader.Open(uri);
            return new Bitmap(assets);
        }
        catch
        {
            return null;
        }
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
