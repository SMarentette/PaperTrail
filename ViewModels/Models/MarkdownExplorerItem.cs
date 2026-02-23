using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace PaperTrail.ViewModels;

public partial class MarkdownExplorerItem : ObservableObject
{
    [ObservableProperty] private string name = string.Empty;
    [ObservableProperty] private string fullPath = string.Empty;
    [ObservableProperty] private bool isCategory;
    [ObservableProperty] private bool isExpanded = true;

    public ObservableCollection<MarkdownExplorerItem> Children { get; } = new();
}
