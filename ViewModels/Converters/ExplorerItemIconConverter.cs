using System;
using System.Collections.Generic;
using System.Globalization;
using Avalonia;
using Avalonia.Data.Converters;
using Avalonia.Media.Imaging;
using Avalonia.Platform;

namespace PaperTrail.ViewModels;

public sealed class ExplorerItemIconConverter : IMultiValueConverter
{
    public static readonly ExplorerItemIconConverter Instance = new();

    public object? Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
    {
        if (values.Count < 3)
        {
            return null;
        }

        var isCategory = values[0] is true;
        var folderIconSource = values[1] as string;
        var markdownIconSource = values[2] as string;

        var iconSource = isCategory ? folderIconSource : markdownIconSource;

        if (string.IsNullOrWhiteSpace(iconSource))
        {
            return null;
        }

        try
        {
            var uri = new Uri(iconSource);
            var assets = AssetLoader.Open(uri);
            return new Bitmap(assets);
        }
        catch
        {
            return null;
        }
    }
}
