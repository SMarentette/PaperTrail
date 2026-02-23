using System;
using System.IO;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Input;

namespace PaperTrail.ViewModels;

public partial class MainWindowViewModel
{
    private bool CanSaveMarkdown() => !string.IsNullOrWhiteSpace(MarkdownText);

    [RelayCommand(CanExecute = nameof(CanSaveMarkdown))]
    private async Task SaveMarkdown()
    {
        EnsureMarkdownRoot();

        var path = FilePath;
        if (string.IsNullOrWhiteSpace(path))
        {
            var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            path = Path.Combine(MarkdownRootPath, $"Note_{timestamp}.md");
            FilePath = path;
        }

        await File.WriteAllTextAsync(path, MarkdownText ?? string.Empty);
        IsDirty = false;
        LoadExplorer();
        SelectExplorerFile(path);
        StatusMessage = $"Saved: {Path.GetFileName(path)}";
    }

    [RelayCommand]
    private void SwitchToView()
    {
        CurrentMode = MarkdownMode.View;
        RenderMarkdown();
    }

    [RelayCommand]
    private void SwitchToEdit()
    {
        if (CurrentMode == MarkdownMode.Edit)
        {
            CurrentMode = MarkdownMode.View;
        }
        CurrentMode = MarkdownMode.Edit;
        EditorTextChangeRequested?.Invoke(MarkdownText ?? string.Empty);
    }

    [RelayCommand]
    private void ToggleTheme()
    {
        IsMarkdownLightMode = !IsMarkdownLightMode;
    }

    public async Task OpenFileAsync(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            StatusMessage = "File not found";
            return;
        }

        var extension = Path.GetExtension(path);
        if (!extension.Equals(".md", StringComparison.OrdinalIgnoreCase)
            && !extension.Equals(".markdown", StringComparison.OrdinalIgnoreCase))
        {
            StatusMessage = "Only markdown files are supported";
            return;
        }

        var content = await File.ReadAllTextAsync(path);

        _isUpdatingText = true;
        MarkdownText = content;
        _isUpdatingText = false;

        FilePath = path;
        IsDirty = false;
        CurrentMode = MarkdownMode.View;
        SaveMarkdownCommand.NotifyCanExecuteChanged();

        UpdateHeadings(content);
        RenderMarkdown();
        EditorTextChangeRequested?.Invoke(content);
        SelectExplorerFile(path);

        StatusMessage = $"Opened: {Path.GetFileName(path)}";
    }

    public async Task SaveFileAsync()
    {
        await SaveMarkdown();
    }

    public async Task SaveFileToPathAsync(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await File.WriteAllTextAsync(path, MarkdownText ?? string.Empty);
        FilePath = path;
        IsDirty = false;
        SaveMarkdownCommand.NotifyCanExecuteChanged();

        if (path.EndsWith(".md", StringComparison.OrdinalIgnoreCase)
            || path.EndsWith(".markdown", StringComparison.OrdinalIgnoreCase))
        {
            LoadExplorer();
            SelectExplorerFile(path);
        }

        StatusMessage = $"Saved: {Path.GetFileName(path)}";
    }
}
