using System;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using AvaloniaEdit;
using AvaloniaEdit.Document;
using PaperTrail.ViewModels;

namespace PaperTrail.Views;

public partial class MainWindow : Window
{
    private readonly MainWindowViewModel _viewModel;
    private TextEditor? _markdownEditor;
    private ScrollViewer? _previewEditScrollViewer;
    private ScrollViewer? _previewViewScrollViewer;

    private bool _isUpdatingEditorFromVm;
    private bool _isSyncingScroll;
    private bool _isScrollSyncSetup;
    private bool _isEditorSetup;
    private double _lastScrollRatio;
    private TextEditor? _scrollSyncEditorTarget;
    private ScrollViewer? _scrollSyncPreviewEditTarget;
    private ScrollViewer? _scrollSyncPreviewViewTarget;

    public MainWindow() : this(new MainWindowViewModel())
    {
    }

    public MainWindow(MainWindowViewModel viewModel)
    {
        _viewModel = viewModel;
        DataContext = viewModel;
        InitializeComponent();

        _viewModel.EditorTextChangeRequested += OnEditorTextChangeRequested;
        _viewModel.PropertyChanged += OnViewModelPropertyChanged;

        Opened += OnWindowOpened;
        Closed += OnWindowClosed;

        DragDrop.SetAllowDrop(this, true);
        AddHandler(DragDrop.DropEvent, OnFileDrop);
    }

    private void OnWindowOpened(object? sender, EventArgs e)
    {
        EnsureMarkdownControlsReady();
        SetupKeyboardShortcuts();
    }

    private void OnWindowClosed(object? sender, EventArgs e)
    {
        TeardownScrollSync();
        _viewModel.EditorTextChangeRequested -= OnEditorTextChangeRequested;
        _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
    }

    private void SetupMarkdownEditor()
    {
        if (_markdownEditor == null)
        {
            return;
        }

        if (!ReferenceEquals(_markdownEditor.Document, _viewModel.MarkdownDocument))
        {
            _markdownEditor.Document = _viewModel.MarkdownDocument;
        }

        ApplyEditorTheme();

        // Only setup event handlers once
        if (!_isEditorSetup)
        {
            _markdownEditor.Document.TextChanged += (_, _) =>
            {
                if (_isUpdatingEditorFromVm || _markdownEditor.Document == null)
                {
                    return;
                }
            };

            // Sync and redraw whenever the editor becomes effectively visible
            // (e.g. switching from View mode to Edit mode for the first time)
            _markdownEditor.PropertyChanged += (_, e) =>
            {
                if (e.Property.Name == "IsEffectivelyVisible" && e.NewValue is true)
                {
                    Dispatcher.UIThread.Post(() =>
                    {
                        SyncEditorFromViewModel();
                        ApplyEditorTheme();
                        _markdownEditor?.TextArea?.TextView?.Redraw();
                    }, DispatcherPriority.Render);
                }
            };

            _isEditorSetup = true;
        }

        // Note: Don't call SyncEditorFromViewModel here to avoid recursion
        // The caller is responsible for syncing if needed
    }

    private void EnsureMarkdownControlsReady()
    {
        var foundEditor = this.FindControl<TextEditor>("MarkdownEditor");
        var foundPreviewEdit = this.FindControl<ScrollViewer>("PreviewEditScrollViewer");
        var foundPreviewView = this.FindControl<ScrollViewer>("PreviewViewScrollViewer");

        _markdownEditor ??= foundEditor;
        _previewEditScrollViewer ??= foundPreviewEdit;
        _previewViewScrollViewer ??= foundPreviewView;

        if (_markdownEditor == null && foundEditor != null)
        {
            _markdownEditor = foundEditor;
        }

        if (_previewEditScrollViewer == null && foundPreviewEdit != null)
        {
            _previewEditScrollViewer = foundPreviewEdit;
        }

        if (_previewViewScrollViewer == null && foundPreviewView != null)
        {
            _previewViewScrollViewer = foundPreviewView;
        }

        if (_markdownEditor != null)
        {
            SetupMarkdownEditor();
        }

        SetupScrollSync();
    }

    private void OnEditorTextChangeRequested(string text)
    {
        Dispatcher.UIThread.Post(() => EnsureMarkdownEditorReadyAndSync(text), DispatcherPriority.Render);
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainWindowViewModel.IsMarkdownLightMode)
            || e.PropertyName == nameof(MainWindowViewModel.EditorBackground))
        {
            Dispatcher.UIThread.Post(ApplyEditorTheme);
        }

        if (e.PropertyName == nameof(MainWindowViewModel.MarkdownText)
            || e.PropertyName == nameof(MainWindowViewModel.MarkdownDocument)
            || e.PropertyName == nameof(MainWindowViewModel.IsEditMode)
            || e.PropertyName == nameof(MainWindowViewModel.FilePath))
        {
            Dispatcher.UIThread.Post(() => EnsureMarkdownEditorReadyAndSync(), DispatcherPriority.Render);
        }

        if (e.PropertyName == nameof(MainWindowViewModel.IsEditMode)
            || e.PropertyName == nameof(MainWindowViewModel.IsViewMode))
        {
            Dispatcher.UIThread.Post(SyncModeOffsets, DispatcherPriority.Render);
        }
    }

    private void SyncEditorFromViewModel()
    {
        if (_markdownEditor?.Document == null)
        {
            return;
        }

        var viewModelText = _viewModel.MarkdownText ?? string.Empty;
        if (!string.Equals(_markdownEditor.Document.Text, viewModelText, StringComparison.Ordinal))
        {
            _isUpdatingEditorFromVm = true;
            _markdownEditor.Document.Text = viewModelText;
            _isUpdatingEditorFromVm = false;
        }

        _markdownEditor.TextArea?.TextView?.Redraw();
    }

    private void SyncEditorFromText(string text)
    {
        if (_markdownEditor?.Document == null)
        {
            return;
        }

        if (!string.Equals(_markdownEditor.Document.Text, text, StringComparison.Ordinal))
        {
            _isUpdatingEditorFromVm = true;
            _markdownEditor.Document.Text = text;
            _isUpdatingEditorFromVm = false;
        }

        _markdownEditor.TextArea?.TextView?.Redraw();
    }

    private void EnsureMarkdownEditorReadyAndSync(string? textOverride = null, int retriesRemaining = 12)
    {
        EnsureMarkdownControlsReady();

        if (_markdownEditor?.Document != null)
        {
            if (textOverride != null)
            {
                SyncEditorFromText(textOverride);
            }
            else
            {
                SyncEditorFromViewModel();
            }
            return;
        }

        if (retriesRemaining <= 0)
        {
            return;
        }

        Dispatcher.UIThread.Post(
            () => EnsureMarkdownEditorReadyAndSync(textOverride, retriesRemaining - 1),
            DispatcherPriority.Render);
    }

    private void ApplyEditorTheme()
    {
        if (_markdownEditor == null)
        {
            return;
        }

        var background = _viewModel.IsMarkdownLightMode
            ? Color.Parse("#F3F2EE")
            : Color.Parse("#1E1E1E");
        var foreground = _viewModel.IsMarkdownLightMode
            ? Color.Parse("#1F0909")
            : Color.Parse("#D4D4D4");

        _markdownEditor.Background = new SolidColorBrush(background);
        _markdownEditor.Foreground = new SolidColorBrush(foreground);
        _markdownEditor.TextArea.Background = new SolidColorBrush(background);
        _markdownEditor.TextArea.Foreground = new SolidColorBrush(foreground);
        _markdownEditor.TextArea.Caret.CaretBrush = new SolidColorBrush(foreground);
    }

    private void SetupScrollSync()
    {
        if (_markdownEditor == null || _previewEditScrollViewer == null)
        {
            return;
        }

        var sameTargets = _isScrollSyncSetup
            && ReferenceEquals(_scrollSyncEditorTarget, _markdownEditor)
            && ReferenceEquals(_scrollSyncPreviewEditTarget, _previewEditScrollViewer)
            && ReferenceEquals(_scrollSyncPreviewViewTarget, _previewViewScrollViewer);
        if (sameTargets)
        {
            return;
        }

        TeardownScrollSync();

        _markdownEditor.TextArea.TextView.ScrollOffsetChanged += OnEditorScrollOffsetChanged;
        _previewEditScrollViewer.ScrollChanged += OnPreviewEditScrollChanged;
        if (_previewViewScrollViewer != null)
        {
            _previewViewScrollViewer.ScrollChanged += OnPreviewViewScrollChanged;
        }

        _scrollSyncEditorTarget = _markdownEditor;
        _scrollSyncPreviewEditTarget = _previewEditScrollViewer;
        _scrollSyncPreviewViewTarget = _previewViewScrollViewer;
        _isScrollSyncSetup = true;
        Dispatcher.UIThread.Post(SyncModeOffsets, DispatcherPriority.Background);
    }

    private void TeardownScrollSync()
    {
        if (!_isScrollSyncSetup)
        {
            return;
        }

        if (_scrollSyncEditorTarget != null)
        {
            _scrollSyncEditorTarget.TextArea.TextView.ScrollOffsetChanged -= OnEditorScrollOffsetChanged;
        }

        if (_scrollSyncPreviewEditTarget != null)
        {
            _scrollSyncPreviewEditTarget.ScrollChanged -= OnPreviewEditScrollChanged;
        }

        if (_scrollSyncPreviewViewTarget != null)
        {
            _scrollSyncPreviewViewTarget.ScrollChanged -= OnPreviewViewScrollChanged;
        }

        _scrollSyncEditorTarget = null;
        _scrollSyncPreviewEditTarget = null;
        _scrollSyncPreviewViewTarget = null;
        _isScrollSyncSetup = false;
    }

    private void SyncModeOffsets()
    {
        if (!_viewModel.HasFile)
        {
            return;
        }

        _isSyncingScroll = true;
        try
        {
            SetPreviewScrollRatio(_previewViewScrollViewer, _lastScrollRatio);
            SetPreviewScrollRatio(_previewEditScrollViewer, _lastScrollRatio);

            if (_viewModel.IsEditMode)
            {
                ScrollEditorToRatio(_lastScrollRatio);
            }
        }
        finally
        {
            _isSyncingScroll = false;
        }
    }

    private void OnEditorScrollOffsetChanged(object? sender, EventArgs e)
    {
        if (_isSyncingScroll || !_viewModel.IsEditMode || !_viewModel.HasFile)
        {
            return;
        }

        var ratio = GetEditorScrollRatio();
        _lastScrollRatio = ratio;

        _isSyncingScroll = true;
        try
        {
            SetPreviewScrollRatio(_previewEditScrollViewer, ratio);
            SetPreviewScrollRatio(_previewViewScrollViewer, ratio);
        }
        finally
        {
            _isSyncingScroll = false;
        }
    }

    private void OnPreviewEditScrollChanged(object? sender, ScrollChangedEventArgs e)
    {
        if (_isSyncingScroll || !_viewModel.IsEditMode || !_viewModel.HasFile)
        {
            return;
        }

        var ratio = GetPreviewScrollRatio(_previewEditScrollViewer);
        _lastScrollRatio = ratio;

        _isSyncingScroll = true;
        try
        {
            SetPreviewScrollRatio(_previewViewScrollViewer, ratio);
            ScrollEditorToRatio(ratio);
        }
        finally
        {
            _isSyncingScroll = false;
        }
    }

    private void OnPreviewViewScrollChanged(object? sender, ScrollChangedEventArgs e)
    {
        if (_isSyncingScroll || !_viewModel.IsViewMode || !_viewModel.HasFile)
        {
            return;
        }

        var ratio = GetPreviewScrollRatio(_previewViewScrollViewer);
        _lastScrollRatio = ratio;

        _isSyncingScroll = true;
        try
        {
            SetPreviewScrollRatio(_previewEditScrollViewer, ratio);
        }
        finally
        {
            _isSyncingScroll = false;
        }
    }

    private static double GetPreviewScrollRatio(ScrollViewer? scrollViewer)
    {
        if (scrollViewer == null)
        {
            return 0d;
        }

        var extent = Math.Max(0d, scrollViewer.Extent.Height);
        if (extent <= 0d)
        {
            return 0d;
        }

        return Math.Clamp(scrollViewer.Offset.Y / extent, 0d, 1d);
    }

    private static bool SetPreviewScrollRatio(ScrollViewer? scrollViewer, double ratio)
    {
        if (scrollViewer == null)
        {
            return false;
        }

        var maxY = Math.Max(0d, scrollViewer.Extent.Height - scrollViewer.Viewport.Height);
        var extent = Math.Max(0d, scrollViewer.Extent.Height);
        if (maxY <= 0d || extent <= 0d)
        {
            return false;
        }

        var targetY = Math.Clamp(Math.Clamp(ratio, 0d, 1d) * extent, 0d, maxY);
        if (Math.Abs(scrollViewer.Offset.Y - targetY) > 0.5d)
        {
            scrollViewer.Offset = new Vector(scrollViewer.Offset.X, targetY);
        }

        return true;
    }

    private double GetEditorScrollRatio()
    {
        if (_markdownEditor == null)
        {
            return 0d;
        }

        var maxOffset = GetEditorMaxVerticalOffset();
        if (maxOffset <= 0d)
        {
            return 0d;
        }

        var viewport = GetEditorViewportHeight();
        var totalHeight = maxOffset + viewport;
        if (totalHeight <= 0d)
        {
            return 0d;
        }

        return Math.Clamp(_markdownEditor.VerticalOffset / totalHeight, 0d, 1d);
    }

    private double GetEditorViewportHeight()
    {
        if (_markdownEditor?.TextArea?.TextView == null)
        {
            return 0d;
        }

        return Math.Max(1d, _markdownEditor.TextArea.TextView.Bounds.Height);
    }

    private double GetEditorMaxVerticalOffset()
    {
        if (_markdownEditor?.Document == null)
        {
            return 0d;
        }

        var textView = _markdownEditor.TextArea.TextView;
        if (!textView.VisualLinesValid)
        {
            textView.EnsureVisualLines();
        }

        var lineHeight = Math.Max(1d, textView.DefaultLineHeight);
        var viewportHeight = GetEditorViewportHeight();
        var totalHeight = _markdownEditor.Document.LineCount * lineHeight;
        return Math.Max(0d, totalHeight - viewportHeight);
    }

    private void ScrollEditorToRatio(double ratio)
    {
        if (_markdownEditor == null)
        {
            return;
        }

        var maxOffset = GetEditorMaxVerticalOffset();
        if (maxOffset <= 0d)
        {
            _markdownEditor.ScrollToVerticalOffset(0d);
            return;
        }

        var viewport = GetEditorViewportHeight();
        var totalHeight = maxOffset + viewport;
        var targetOffset = Math.Clamp(Math.Clamp(ratio, 0d, 1d) * totalHeight, 0d, maxOffset);
        if (Math.Abs(_markdownEditor.VerticalOffset - targetOffset) > 0.5d)
        {
            _markdownEditor.ScrollToVerticalOffset(targetOffset);
        }
    }

    private void TocButton_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: HeadingItem heading })
        {
            return;
        }

        ScrollPreviewToRatio(heading.ScrollRatio);
    }

    private void ScrollPreviewToRatio(double ratio, int retriesRemaining = 2)
    {
        var clampedRatio = Math.Clamp(ratio, 0d, 1d);
        _lastScrollRatio = clampedRatio;

        if (!SetPreviewScrollRatio(_previewViewScrollViewer, clampedRatio)
            && !SetPreviewScrollRatio(_previewEditScrollViewer, clampedRatio))
        {
            if (retriesRemaining > 0)
            {
                Dispatcher.UIThread.Post(
                    () => ScrollPreviewToRatio(clampedRatio, retriesRemaining - 1),
                    DispatcherPriority.Background);
            }

            return;
        }

        if (_viewModel.IsEditMode)
        {
            ScrollEditorToRatio(clampedRatio);
        }
    }

    private async void Open_Click(object? sender, RoutedEventArgs e)
    {
        await PickAndOpenFileAsync();
    }

    private async void OpenFolder_Click(object? sender, RoutedEventArgs e)
    {
        var folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "Select Markdown Folder",
            AllowMultiple = false
        });

        if (folders.Count == 0)
        {
            return;
        }

        _viewModel.SetMarkdownRootPath(folders[0].Path.LocalPath);
    }

    private async void Save_Click(object? sender, RoutedEventArgs e)
    {
        await SaveCurrentFileAsync();
    }

    private void New_Click(object? sender, RoutedEventArgs e)
    {
        _viewModel.NewFile();
    }

    private void View_Click(object? sender, RoutedEventArgs e)
    {
        _viewModel.SwitchToViewCommand.Execute(null);
        Dispatcher.UIThread.Post(SyncModeOffsets, DispatcherPriority.Render);
    }

    private void Edit_Click(object? sender, RoutedEventArgs e)
    {
        // Toggle edit mode - if already in edit mode, switch to view mode
        if (_viewModel.IsEditMode)
        {
            _viewModel.SwitchToViewCommand.Execute(null);
            Dispatcher.UIThread.Post(SyncModeOffsets, DispatcherPriority.Render);
        }
        else
        {
            _viewModel.SwitchToEditCommand.Execute(null);
            // Post with Loaded priority to ensure the edit panel is visible and controls are ready
            Dispatcher.UIThread.Post(() =>
            {
                EnsureMarkdownEditorReadyAndSync();
                
                // Post a second time with Render priority to handle scroll sync after layout
                Dispatcher.UIThread.Post(() =>
                {
                    EnsureMarkdownEditorReadyAndSync();
                    SyncModeOffsets();
                    _markdownEditor?.TextArea?.TextView?.Redraw();
                }, DispatcherPriority.Render);
            }, DispatcherPriority.Loaded);
        }
    }

    private void ToggleTheme_Click(object? sender, RoutedEventArgs e)
    {
        _viewModel.ToggleThemeCommand.Execute(null);
    }

    private async Task PickAndOpenFileAsync()
    {
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Open Markdown File",
            AllowMultiple = false,
            FileTypeFilter =
            [
                new FilePickerFileType("Markdown Files") { Patterns = new[] { "*.md", "*.markdown" } },
                new FilePickerFileType("All Files") { Patterns = new[] { "*.*" } }
            ]
        });

        if (files.Count > 0)
        {
            await _viewModel.OpenFileAsync(files[0].Path.LocalPath);
        }
    }

    private async Task SaveCurrentFileAsync()
    {
        if (_markdownEditor?.Document != null && !_isUpdatingEditorFromVm)
        {
            _viewModel.SetMarkdownTextFromEditor(_markdownEditor.Document.Text);
        }

        if (_viewModel.HasFile && !string.IsNullOrWhiteSpace(_viewModel.FilePath))
        {
            await _viewModel.SaveFileAsync();
        }
        else
        {
            await SaveAsAsync();
        }
    }

    private async Task SaveAsAsync()
    {
        var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Save Markdown File",
            DefaultExtension = "md",
            FileTypeChoices =
            [
                new FilePickerFileType("Markdown Files") { Patterns = new[] { "*.md", "*.markdown" } }
            ]
        });

        if (file != null)
        {
            await _viewModel.SaveFileToPathAsync(file.Path.LocalPath);
        }
    }

    private async void OnFileDrop(object? sender, DragEventArgs e)
    {
        var files = e.Data.GetFiles()?.ToList();
        if (files == null || files.Count == 0)
        {
            return;
        }

        var markdownFile = files.FirstOrDefault(file =>
        {
            var ext = Path.GetExtension(file.Name);
            return ext.Equals(".md", StringComparison.OrdinalIgnoreCase)
                   || ext.Equals(".markdown", StringComparison.OrdinalIgnoreCase);
        });

        if (markdownFile != null)
        {
            await _viewModel.OpenFileAsync(markdownFile.Path.LocalPath);
        }
    }

    private async void ExplorerTreeView_DoubleTapped(object? sender, TappedEventArgs e)
    {
        if (_viewModel.SelectedExplorerItem is not { IsCategory: false } item)
        {
            return;
        }

        await _viewModel.OpenFileAsync(item.FullPath);
        e.Handled = true;
    }

    private async void ExplorerTreeView_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter || _viewModel.SelectedExplorerItem is not { IsCategory: false } item)
        {
            return;
        }

        await _viewModel.OpenFileAsync(item.FullPath);
        e.Handled = true;
    }

    private void SetupKeyboardShortcuts()
    {
        KeyDown += async (_, e) =>
        {
            if (e.KeyModifiers != KeyModifiers.Control)
            {
                return;
            }

            switch (e.Key)
            {
                case Key.O:
                    await PickAndOpenFileAsync();
                    e.Handled = true;
                    break;
                case Key.S:
                    await SaveCurrentFileAsync();
                    e.Handled = true;
                    break;
                case Key.N:
                    _viewModel.NewFile();
                    e.Handled = true;
                    break;
                case Key.E:
                    _viewModel.SwitchToEditCommand.Execute(null);
                    e.Handled = true;
                    break;
            }
        };
    }
}
