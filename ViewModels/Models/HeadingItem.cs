using System;

namespace PaperTrail.ViewModels;

public sealed class HeadingItem
{
    public string Text { get; }
    public int Level { get; }
    public double ScrollRatio { get; }

    public HeadingItem(string text, int level, double scrollRatio)
    {
        Text = text;
        Level = Math.Clamp(level, 1, 6);
        ScrollRatio = Math.Clamp(scrollRatio, 0d, 1d);
    }
}
