using System;
using System.IO;
using System.Linq;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using PaperTrail.Config;
using PaperTrail.ViewModels;
using PaperTrail.Views;

namespace PaperTrail;

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            MarkdownPathConfig.EnsureAppDataFiles();
            var viewModel = new MainWindowViewModel();
            var mainWindow = new MainWindow(viewModel);
            desktop.MainWindow = mainWindow;

            // Handle double-click on .md file (passed as command-line arg)
            var args = desktop.Args ?? Array.Empty<string>();
            var filePath = args.FirstOrDefault(a =>
                !string.IsNullOrWhiteSpace(a) &&
                File.Exists(a) &&
                (a.EndsWith(".md", StringComparison.OrdinalIgnoreCase) ||
                 a.EndsWith(".markdown", StringComparison.OrdinalIgnoreCase)));

            if (filePath != null)
            {
                mainWindow.Opened += async (_, _) => await viewModel.OpenFileAsync(filePath, persistOpenedFileFolder: false);
            }
        }

        base.OnFrameworkInitializationCompleted();
    }
}
