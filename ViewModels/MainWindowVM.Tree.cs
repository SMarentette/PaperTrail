using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Input;
using PaperTrail.Config;

namespace PaperTrail.ViewModels;

public partial class MainWindowViewModel
{
    private bool CanOpenSelectedMarkdown() => SelectedExplorerItem is { IsCategory: false };

    [RelayCommand(CanExecute = nameof(CanOpenSelectedMarkdown))]
    private async Task OpenSelectedMarkdown()
    {
        if (SelectedExplorerItem is not { IsCategory: false } item)
        {
            return;
        }

        await OpenFileAsync(item.FullPath);
    }

    [RelayCommand]
    private void RefreshExplorer()
    {
        LoadExplorer();
        StatusMessage = "Explorer refreshed";
    }

    public void SetMarkdownRootPath(string folderPath)
    {
        if (string.IsNullOrWhiteSpace(folderPath))
        {
            return;
        }

        MarkdownRootPath = Environment.ExpandEnvironmentVariables(folderPath.Trim());
        EnsureMarkdownRoot();
        PersistMarkdownRootPath();
        LoadExplorer();
        SelectedExplorerItem = null;
        StatusMessage = $"Folder opened: {MarkdownRootPath}";
    }

    [RelayCommand]
    private async Task CreateNewMarkdown()
    {
        EnsureMarkdownRoot();

        var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        var fileName = $"Prompt_{timestamp}.md";
        var path = Path.Combine(MarkdownRootPath, fileName);
        var template = MarkdownPathConfig.LoadMarkdownTemplate();

        await File.WriteAllTextAsync(path, template);
        LoadExplorer();
        await OpenFileAsync(path);
        CurrentMode = MarkdownMode.Edit;
        StatusMessage = $"Created markdown: {fileName}";
    }

    [RelayCommand]
    private void CreateDirectory()
    {
        EnsureMarkdownRoot();

        var parentPath = MarkdownRootPath;
        if (SelectedExplorerItem != null)
        {
            if (SelectedExplorerItem.IsCategory)
            {
                parentPath = SelectedExplorerItem.FullPath;
            }
            else
            {
                var selectedFileDirectory = Path.GetDirectoryName(SelectedExplorerItem.FullPath);
                if (!string.IsNullOrWhiteSpace(selectedFileDirectory))
                {
                    parentPath = selectedFileDirectory;
                }
            }
        }

        try
        {
            const string baseFolderName = "NewFolder";
            var candidate = Path.Combine(parentPath, baseFolderName);
            var suffix = 1;
            while (Directory.Exists(candidate) || File.Exists(candidate))
            {
                candidate = Path.Combine(parentPath, $"{baseFolderName}_{suffix++}");
            }

            Directory.CreateDirectory(candidate);
            LoadExplorer();
            SelectExplorerFile(candidate);
            StatusMessage = $"Created folder: {Path.GetFileName(candidate)}";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Create folder failed: {ex.Message}";
        }
    }

    private bool CanDeleteSelectedItem() => SelectedExplorerItem != null;

    [RelayCommand(CanExecute = nameof(CanDeleteSelectedItem))]
    private void DeleteSelectedItem()
    {
        if (SelectedExplorerItem == null)
        {
            return;
        }

        var item = SelectedExplorerItem;

        try
        {
            if (item.IsCategory)
            {
                if (Directory.Exists(item.FullPath))
                {
                    Directory.Delete(item.FullPath, true);
                }
            }
            else
            {
                if (File.Exists(item.FullPath))
                {
                    File.Delete(item.FullPath);
                }
            }

            if (!string.IsNullOrWhiteSpace(FilePath)
                && (PathEquals(FilePath, item.FullPath)
                    || (item.IsCategory && IsPathInsideDirectory(FilePath, item.FullPath))))
            {
                FilePath = null;
                _isUpdatingText = true;
                MarkdownText = string.Empty;
                _isUpdatingText = false;
                RenderedHtml = string.Empty;
                Headings.Clear();
                IsDirty = false;
                CurrentMode = MarkdownMode.View;
                EditorTextChangeRequested?.Invoke(string.Empty);
            }

            LoadExplorer();
            SelectedExplorerItem = null;
            StatusMessage = item.IsCategory
                ? $"Deleted folder: {item.Name}"
                : $"Deleted file: {item.Name}";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Delete failed: {ex.Message}";
        }
    }

    private void EnsureMarkdownRoot()
    {
        if (string.IsNullOrWhiteSpace(MarkdownRootPath))
        {
            MarkdownRootPath = MarkdownPathConfig.PaperTrailAppDataDirectory;
        }

        MarkdownRootPath = Environment.ExpandEnvironmentVariables(MarkdownRootPath.Trim());
        Directory.CreateDirectory(MarkdownRootPath);
    }

    private void PersistMarkdownRootPath()
    {
        MarkdownPathConfig.SaveSettings(new PaperTrailSettings
        {
            MarkdownRootPath = MarkdownRootPath
        });
    }

    private void LoadExplorer()
    {
        ExplorerItems.Clear();

        if (!Directory.Exists(MarkdownRootPath))
        {
            return;
        }

        BuildExplorerTree(MarkdownRootPath, ExplorerItems);
    }

    private static void BuildExplorerTree(string directoryPath, ObservableCollection<MarkdownExplorerItem> parent)
    {
        foreach (var dir in Directory
                     .EnumerateDirectories(directoryPath)
                     .OrderBy(Path.GetFileName, StringComparer.OrdinalIgnoreCase))
        {
            var folder = new MarkdownExplorerItem
            {
                Name = Path.GetFileName(dir),
                FullPath = dir,
                IsCategory = true
            };

            BuildExplorerTree(dir, folder.Children);
            parent.Add(folder);
        }

        var files = Directory
            .EnumerateFiles(directoryPath)
            .Where(file =>
            {
                var ext = Path.GetExtension(file);
                return ext.Equals(".md", StringComparison.OrdinalIgnoreCase)
                    || ext.Equals(".markdown", StringComparison.OrdinalIgnoreCase);
            })
            .OrderBy(Path.GetFileName, StringComparer.OrdinalIgnoreCase);

        foreach (var file in files)
        {
            parent.Add(new MarkdownExplorerItem
            {
                Name = Path.GetFileName(file),
                FullPath = file,
                IsCategory = false
            });
        }
    }

    private void SelectExplorerFile(string fullPath)
    {
        if (string.IsNullOrWhiteSpace(fullPath))
        {
            return;
        }

        foreach (var rootItem in ExplorerItems)
        {
            if (TryFindExplorerItem(rootItem, fullPath, out var match))
            {
                SelectedExplorerItem = match;
                return;
            }
        }
    }

    private static bool TryFindExplorerItem(MarkdownExplorerItem item, string targetPath, out MarkdownExplorerItem match)
    {
        if (PathEquals(item.FullPath, targetPath))
        {
            match = item;
            return true;
        }

        foreach (var child in item.Children)
        {
            if (TryFindExplorerItem(child, targetPath, out match))
            {
                return true;
            }
        }

        match = null!;
        return false;
    }

    private static bool PathEquals(string? left, string? right)
    {
        if (string.IsNullOrWhiteSpace(left) || string.IsNullOrWhiteSpace(right))
        {
            return false;
        }

        return string.Equals(
            Path.GetFullPath(left),
            Path.GetFullPath(right),
            StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsPathInsideDirectory(string path, string directory)
    {
        if (string.IsNullOrWhiteSpace(path) || string.IsNullOrWhiteSpace(directory))
        {
            return false;
        }

        var normalizedPath = Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var normalizedDirectory = Path.GetFullPath(directory).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        return normalizedPath.StartsWith(normalizedDirectory + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
    }
}

