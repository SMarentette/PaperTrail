using System;
using System.IO;
using System.Text.Json;

namespace PaperTrail.Config;

public static class MarkdownPathConfig
{
    public static string PaperTrailAppDataDirectory { get; } =
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "PaperTrail");

    public static string SettingsFilePath { get; } =
        Path.Combine(PaperTrailAppDataDirectory, "settings.json");

    public static string MarkdownTemplateFilePath { get; } =
        Path.Combine(PaperTrailAppDataDirectory, "markdown.md");

    private const string DefaultTemplate = """
        # AI Prompt

        ## Role / Persona
        You are a helpful AI assistant.

        ## Context
        Provide relevant background information here.

        ## Task / Instructions
        Describe what you want the AI to do.

        ## Input
        ```
        [Your input data here]
        ```

        ## Output Format
        Describe the expected output format (e.g., JSON, markdown, bullet points).

        ## Constraints
        - Constraint 1
        - Constraint 2

        ## Examples (Optional)

        ### Example Input
        ```
        [Example input]
        ```

        ### Example Output
        ```
        [Example output]
        ```

        ---

        ## Notes
        Additional notes or considerations.
        """;

    public static void EnsureAppDataFiles()
    {
        Directory.CreateDirectory(PaperTrailAppDataDirectory);

        if (!File.Exists(SettingsFilePath))
        {
            var defaultJson = JsonSerializer.Serialize(
                new PaperTrailSettings(),
                new JsonSerializerOptions
                {
                    WriteIndented = true
                });
            File.WriteAllText(SettingsFilePath, defaultJson);
        }

        if (!File.Exists(MarkdownTemplateFilePath))
        {
            File.WriteAllText(MarkdownTemplateFilePath, DefaultTemplate);
        }
    }

    public static PaperTrailSettings LoadSettings()
    {
        EnsureAppDataFiles();

        try
        {
            var json = File.ReadAllText(SettingsFilePath);
            var loaded = JsonSerializer.Deserialize<PaperTrailSettings>(json);
            if (loaded == null || string.IsNullOrWhiteSpace(loaded.MarkdownRootPath))
            {
                return new PaperTrailSettings();
            }

            loaded.MarkdownRootPath = Environment.ExpandEnvironmentVariables(loaded.MarkdownRootPath.Trim());
            return loaded;
        }
        catch
        {
            return new PaperTrailSettings();
        }
    }

    public static void SaveSettings(PaperTrailSettings settings)
    {
        EnsureAppDataFiles();

        var safeSettings = settings ?? new PaperTrailSettings();
        if (string.IsNullOrWhiteSpace(safeSettings.MarkdownRootPath))
        {
            // Default to app data directory tto start with, user can change it later
            safeSettings.MarkdownRootPath = PaperTrailAppDataDirectory;
        }

        var json = JsonSerializer.Serialize(
            safeSettings,
            new JsonSerializerOptions
            {
                WriteIndented = true
            });

        File.WriteAllText(SettingsFilePath, json);
    }

    public static string LoadMarkdownTemplate()
    {
        EnsureAppDataFiles();

        try
        {
            var template = File.ReadAllText(MarkdownTemplateFilePath);
            if (!string.IsNullOrWhiteSpace(template))
            {
                return template;
            }
        }
        catch
        {
            // ignored
        }

        return DefaultTemplate;
    }
}

public sealed class PaperTrailSettings
{
    public string MarkdownRootPath { get; set; } = MarkdownPathConfig.PaperTrailAppDataDirectory;
}
