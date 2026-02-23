using System;
using System.Collections.ObjectModel;
using System.IO;
using Avalonia.Media;
using Avalonia.Threading;
using AvaloniaEdit.Document;
using CommunityToolkit.Mvvm.ComponentModel;
using PaperTrail.Config;

namespace PaperTrail.ViewModels;

public partial class MainWindowViewModel : ObservableObject
{
    private readonly DispatcherTimer _previewTimer;
    private bool _isUpdatingText;
    private bool _isUpdatingDocumentText;

    [ObservableProperty] private ObservableCollection<MarkdownExplorerItem> explorerItems = new();
    [ObservableProperty] private MarkdownExplorerItem? selectedExplorerItem;
    [ObservableProperty] private string markdownRootPath = MarkdownPathConfig.PaperTrailAppDataDirectory;
    [ObservableProperty] private TextDocument markdownDocument = new();
    [ObservableProperty] private string markdownText = string.Empty;
    [ObservableProperty] private string renderedHtml = string.Empty;
    [ObservableProperty] private ObservableCollection<HeadingItem> headings = new();
    [ObservableProperty] private bool isMarkdownLightMode;
    [ObservableProperty] private MarkdownMode currentMode = MarkdownMode.View;
    [ObservableProperty] private string? filePath;
    [ObservableProperty] private bool isDirty;
    [ObservableProperty] private string statusMessage = "Ready";

    public bool HasFile => !string.IsNullOrWhiteSpace(FilePath);
    public bool IsEditMode => CurrentMode == MarkdownMode.Edit;
    public bool IsViewMode => CurrentMode == MarkdownMode.View;

    public IBrush MarkdownWorkspaceBackground => IsMarkdownLightMode
        ? Brush.Parse("#F3F2EE")
        : Brush.Parse("#1E1E1E");

    public IBrush SidebarBackground => IsMarkdownLightMode
        ? Brush.Parse("#F3F2EE")
        : Brush.Parse("#202124");

    public IBrush SidebarBorderBrush => IsMarkdownLightMode
        ? Brush.Parse("#D2CFC7")
        : Brush.Parse("#3A3A3A");

    public IBrush SidebarHeaderForeground => IsMarkdownLightMode
        ? Brush.Parse("#51423A")
        : Brush.Parse("#B8BDC7");

    public IBrush TocForeground => IsMarkdownLightMode
        ? Brush.Parse("#3E2A23")
        : Brush.Parse("#D4D4D4");

    public IBrush MarkdownSplitBrush => IsMarkdownLightMode
        ? Brush.Parse("#D4CEC2")
        : Brush.Parse("#252526");

    public IBrush EditorBackground => IsMarkdownLightMode
        ? Brush.Parse("#F3F2EE")
        : Brush.Parse("#1E1E1E");

    public IBrush EditorForeground => IsMarkdownLightMode
        ? Brush.Parse("#1F0909")
        : Brush.Parse("#D4D4D4");

    public IBrush ChromeBackground => IsMarkdownLightMode
        ? Brush.Parse("#E7E2D7")
        : Brush.Parse("#2D2D30");

    public IBrush ChromeBorderBrush => IsMarkdownLightMode
        ? Brush.Parse("#CCC2B3")
        : Brush.Parse("#3F3F46");

    public IBrush ChromeStatusForeground => IsMarkdownLightMode
        ? Brush.Parse("#7A6B58")
        : Brush.Parse("#9CA3AF");

    public IBrush ExplorerPanelBackground => IsMarkdownLightMode
        ? Brush.Parse("#E6E0D4")
        : Brush.Parse("#252526");

    public IBrush ExplorerSectionBackground => IsMarkdownLightMode
        ? Brush.Parse("#DDD6C8")
        : Brush.Parse("#2D2D30");

    public IBrush ExplorerHeaderForeground => IsMarkdownLightMode
        ? Brush.Parse("#51423A")
        : Brush.Parse("#BBBBBB");

    public IBrush ExplorerItemForeground => IsMarkdownLightMode
        ? Brush.Parse("#3E2A23")
        : Brush.Parse("#CCCCCC");

    public IBrush ExplorerMetaForeground => IsMarkdownLightMode
        ? Brush.Parse("#6E5C4A")
        : Brush.Parse("#9CA3AF");

    public IBrush InputBackground => IsMarkdownLightMode
        ? Brush.Parse("#F6F2E9")
        : Brush.Parse("#1F1F23");

    public IBrush InputForeground => IsMarkdownLightMode
        ? Brush.Parse("#2F2218")
        : Brush.Parse("#D4D4D4");

    public IBrush InputBorderBrush => IsMarkdownLightMode
        ? Brush.Parse("#CFC5B5")
        : Brush.Parse("#3F3F46");

    public IBrush AccentForeground => Brush.Parse("#FCC419");

    // Theme-aware icon paths
    public string SaveIconSource => IsMarkdownLightMode
        ? "avares://PaperTrail/Assets/save_32.png"
        : "avares://PaperTrail/Assets/save_32_dark.png";

    public string NewMdIconSource => IsMarkdownLightMode
        ? "avares://PaperTrail/Assets/new_md_24.png"
        : "avares://PaperTrail/Assets/new_md_24_dark.png";

    public string EyeIconSource => IsMarkdownLightMode
        ? "avares://PaperTrail/Assets/eye_24.png"
        : "avares://PaperTrail/Assets/eye_24_dark.png";

    public string PencilIconSource => IsMarkdownLightMode
        ? "avares://PaperTrail/Assets/pencil_24.png"
        : "avares://PaperTrail/Assets/pencil_24_dark.png";

    public string RefreshIconSource => IsMarkdownLightMode
        ? "avares://PaperTrail/Assets/refresh_32.png"
        : "avares://PaperTrail/Assets/refresh_32_dark.png";

    public string FolderIconSource => IsMarkdownLightMode
        ? "avares://PaperTrail/Assets/folder_24.png"
        : "avares://PaperTrail/Assets/folder_24_dark.png";

    public string MarkdownIconSource => IsMarkdownLightMode
        ? "avares://PaperTrail/Assets/markdown_24.png"
        : "avares://PaperTrail/Assets/markdown_24_dark.png";

    public string OpenFileIconSource => IsMarkdownLightMode
        ? "avares://PaperTrail/Assets/openFile_24.png"
        : "avares://PaperTrail/Assets/openFile_24_dark.png";

    public string DeleteIconSource => IsMarkdownLightMode
        ? "avares://PaperTrail/Assets/delete_24.png"
        : "avares://PaperTrail/Assets/delete_24_dark.png";

    public string DirectoryIconSource => IsMarkdownLightMode
        ? "avares://PaperTrail/Assets/directory_24.png"
        : "avares://PaperTrail/Assets/directory_24_dark.png";

    public string WindowTitle
    {
        get
        {
            var fileName = HasFile ? Path.GetFileName(FilePath) : "No file";
            return IsDirty ? $"PaperTrail - {fileName} *" : $"PaperTrail - {fileName}";
        }
    }

    public event Action<string>? EditorTextChangeRequested;

    public MainWindowViewModel()
    {
        _previewTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(250) };
        _previewTimer.Tick += (_, _) =>
        {
            _previewTimer.Stop();
            RenderMarkdown();
        };

        MarkdownDocument.TextChanged += OnMarkdownDocumentTextChanged;

        MarkdownPathConfig.EnsureAppDataFiles();
        var settings = MarkdownPathConfig.LoadSettings();
        MarkdownRootPath = settings.MarkdownRootPath;
        EnsureMarkdownRoot();
        LoadExplorer();
    }

    partial void OnSelectedExplorerItemChanged(MarkdownExplorerItem? value)
    {
        OpenSelectedMarkdownCommand.NotifyCanExecuteChanged();
        DeleteSelectedItemCommand.NotifyCanExecuteChanged();
    }

    partial void OnMarkdownRootPathChanged(string value)
    {
        // Notify UI if needed
    }

    partial void OnCurrentModeChanged(MarkdownMode value)
    {
        OnPropertyChanged(nameof(IsEditMode));
        OnPropertyChanged(nameof(IsViewMode));
        StatusMessage = value == MarkdownMode.Edit ? "Markdown edit mode" : "Markdown view mode";
        if (value == MarkdownMode.Edit)
        {
            EditorTextChangeRequested?.Invoke(MarkdownText ?? string.Empty);
        }
    }

    partial void OnFilePathChanged(string? value)
    {
        OnPropertyChanged(nameof(HasFile));
        OnPropertyChanged(nameof(WindowTitle));
    }

    partial void OnIsDirtyChanged(bool value)
    {
        OnPropertyChanged(nameof(WindowTitle));
    }

    partial void OnMarkdownTextChanged(string value)
    {
        SyncDocumentFromMarkdownText(value);

        if (_isUpdatingText)
        {
            return;
        }

        IsDirty = true;
        SaveMarkdownCommand.NotifyCanExecuteChanged();
        UpdateHeadings(value);
        SchedulePreview();
    }

    partial void OnIsMarkdownLightModeChanged(bool value)
    {
        RenderMarkdown();
        OnPropertyChanged(nameof(MarkdownWorkspaceBackground));
        OnPropertyChanged(nameof(SidebarBackground));
        OnPropertyChanged(nameof(SidebarBorderBrush));
        OnPropertyChanged(nameof(SidebarHeaderForeground));
        OnPropertyChanged(nameof(TocForeground));
        OnPropertyChanged(nameof(MarkdownSplitBrush));
        OnPropertyChanged(nameof(EditorBackground));
        OnPropertyChanged(nameof(EditorForeground));
        OnPropertyChanged(nameof(ChromeBackground));
        OnPropertyChanged(nameof(ChromeBorderBrush));
        OnPropertyChanged(nameof(ChromeStatusForeground));
        OnPropertyChanged(nameof(ExplorerPanelBackground));
        OnPropertyChanged(nameof(ExplorerSectionBackground));
        OnPropertyChanged(nameof(ExplorerHeaderForeground));
        OnPropertyChanged(nameof(ExplorerItemForeground));
        OnPropertyChanged(nameof(ExplorerMetaForeground));
        OnPropertyChanged(nameof(InputBackground));
        OnPropertyChanged(nameof(InputForeground));
        OnPropertyChanged(nameof(InputBorderBrush));
        OnPropertyChanged(nameof(AccentForeground));
        // Icon sources
        OnPropertyChanged(nameof(SaveIconSource));
        OnPropertyChanged(nameof(NewMdIconSource));
        OnPropertyChanged(nameof(EyeIconSource));
        OnPropertyChanged(nameof(PencilIconSource));
        OnPropertyChanged(nameof(RefreshIconSource));
        OnPropertyChanged(nameof(FolderIconSource));
        OnPropertyChanged(nameof(MarkdownIconSource));
        OnPropertyChanged(nameof(OpenFileIconSource));
        OnPropertyChanged(nameof(DeleteIconSource));
        OnPropertyChanged(nameof(DirectoryIconSource));
        StatusMessage = value ? "Markdown light mode" : "Markdown dark mode";
    }

    private void OnMarkdownDocumentTextChanged(object? sender, EventArgs e)
    {
        if (_isUpdatingDocumentText)
        {
            return;
        }

        SetMarkdownTextFromEditor(MarkdownDocument.Text);
    }

    private void SyncDocumentFromMarkdownText(string value)
    {
        var text = value ?? string.Empty;
        if (string.Equals(MarkdownDocument.Text, text, StringComparison.Ordinal))
        {
            return;
        }

        _isUpdatingDocumentText = true;
        MarkdownDocument.Text = text;
        _isUpdatingDocumentText = false;
    }

    public void SetMarkdownTextFromEditor(string text)
    {
        if (_isUpdatingText || MarkdownText == text)
        {
            return;
        }

        MarkdownText = text;
    }

    private void SchedulePreview()
    {
        _previewTimer.Stop();
        _previewTimer.Start();
    }

    public void NewFile()
    {
        _ = CreateNewMarkdown();
    }
}
