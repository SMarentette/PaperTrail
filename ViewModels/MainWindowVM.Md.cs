using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Text.RegularExpressions;
using Markdig;
using Markdown.ColorCode;

namespace PaperTrail.ViewModels;

public partial class MainWindowViewModel
{
    private static readonly MarkdownPipeline DarkMarkdownPipeline = new MarkdownPipelineBuilder()
        .UseAdvancedExtensions()
        .UseColorCode()
        .Build();

    private static readonly MarkdownPipeline LightMarkdownPipeline = new MarkdownPipelineBuilder()
        .UseAdvancedExtensions()
        .Build();

    private static readonly Regex HeadingRegex = new(
        @"^\s{0,3}(#{1,6})\s+(.*?)(\s+#+\s*)?$",
        RegexOptions.Compiled);

    private static readonly Regex InlineStyleInCodeTagRegex = new(
        @"<(?<tag>[a-zA-Z][a-zA-Z0-9:-]*)\b(?<before>[^>]*?)\sstyle\s*=\s*(?:""[^""]*""|'[^']*')(?<after>[^>]*)>",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex EmbeddedStyleBlockRegex = new(
        @"<style\b[^>]*>[\s\S]*?</style>",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly string DarkCss = LoadCss("atom-dark.css");
    private static readonly string LightCss = LoadCss("newsprint.css");

    public void RenderMarkdown()
    {
        var html = ToHtml(MarkdownText);
        RenderedHtml = BuildStyledHtml(html);
    }

    private void UpdateHeadings(string markdown)
    {
        var normalized = (markdown ?? string.Empty).Replace("\r\n", "\n");
        var lines = normalized.Split('\n');
        var maxLineIndex = Math.Max(1, lines.Length - 1);
        var parsedHeadings = new List<HeadingItem>();

        for (var i = 0; i < lines.Length; i++)
        {
            var match = HeadingRegex.Match(lines[i]);
            if (!match.Success)
            {
                continue;
            }

            var level = match.Groups[1].Value.Length;
            var text = match.Groups[2].Value.Trim();
            if (string.IsNullOrWhiteSpace(text))
            {
                continue;
            }

            var ratio = (double)i / maxLineIndex;
            parsedHeadings.Add(new HeadingItem(text, level, ratio));
        }

        Headings = new ObservableCollection<HeadingItem>(parsedHeadings);
    }

    private string ToHtml(string markdown)
    {
        var pipeline = IsMarkdownLightMode ? LightMarkdownPipeline : DarkMarkdownPipeline;
        return Markdig.Markdown.ToHtml(markdown ?? string.Empty, pipeline);
    }

    private string BuildStyledHtml(string bodyHtml)
    {
        return IsMarkdownLightMode
            ? BuildLightStyledHtml(bodyHtml)
            : BuildDarkStyledHtml(bodyHtml);
    }

    private static string BuildDarkStyledHtml(string bodyHtml)
    {
        return $$"""
            <html>
            <head>
            <meta charset="utf-8" />
            <style>
            :root {
              color-scheme: dark;
            }

            html, body {
              margin: 0;
              padding: 0;
              background: #1e1e1e;
              color: #d7dce4;
              font-family: "Segoe UI Variable", "Segoe UI", Arial, sans-serif;
              font-size: 15px;
              line-height: 1.62;
            }

            {{DarkCss}}

            .markdown-root {
              width: 940px;
              max-width: 940px;
              margin: 0 auto;
              padding: 0 0 36px;
            }

            .markdown-root > :first-child {
              margin-top: 0;
            }

            h1, h2, h3, h4, h5, h6 {
              color: #f5f8ff;
              line-height: 1.24;
              font-weight: 700;
              margin: 1.25em 0 0.55em;
            }

            h1 {
              font-size: 2.15em;
              padding-bottom: 0.18em;
              border-bottom: 1px solid #3a3f49;
              letter-spacing: 0.01em;
            }

            h2 {
              font-size: 1.65em;
              padding-bottom: 0.15em;
              border-bottom: 1px solid #333842;
            }

            h3 {
              font-size: 1.32em;
            }

            h4 {
              font-size: 1.12em;
            }

            p {
              margin: 0 0 0.95em;
            }

            strong {
              color: #ffffff;
              font-weight: 700;
            }

            em {
              color: #e6eaf2;
            }

            a {
              color: #5ab8ff;
              text-decoration: none;
              border-bottom: 1px dotted #5ab8ff66;
            }

            a:hover {
              color: #8ed0ff;
              border-bottom-color: #8ed0ffaa;
            }

            hr {
              border: none;
              height: 1px;
              background: #3a3f49;
              margin: 1.35em 0;
            }

            ul, ol {
              margin: 0.2em 0 1em 1.35em;
              padding: 0;
            }

            li {
              margin: 0.22em 0;
              padding-left: 0.1em;
            }

            blockquote {
              margin: 1.1em 0;
              padding: 0.8em 1em;
              border-left: 4px solid #3d7fff;
              background: #1d2433;
              color: #d4def5;
              border-radius: 0 8px 8px 0;
            }

            code {
              font-family: "Cascadia Code", Consolas, "Courier New", monospace;
              font-size: 0.93em;
              background: #2b3038;
              color: #ffd892;
              border: 1px solid #3b414d;
              border-radius: 6px;
              padding: 0.1em 0.38em;
            }

            pre {
              margin: 1em 0 1.25em;
              padding: 14px 16px;
              background: #151922;
              border: 1px solid #303745;
              border-radius: 12px;
              overflow-x: auto;
              box-shadow: inset 0 1px 0 #ffffff08;
            }

            pre code {
              display: block;
              white-space: pre;
              color: #d7e5ff;
              background: transparent;
              border: none;
              padding: 0;
              border-radius: 0;
            }

            table {
              width: 100%;
              border-collapse: separate;
              border-spacing: 0;
              margin: 1.05em 0 1.3em;
              border: 1px solid #343c4a;
              border-radius: 10px;
              overflow: hidden;
            }

            th, td {
              padding: 9px 11px;
              border-right: 1px solid #343c4a;
              border-bottom: 1px solid #343c4a;
              text-align: left;
              vertical-align: top;
            }

            th {
              background: #232b3a;
              color: #e7eeff;
              font-weight: 700;
            }

            td {
              background: #1f232b;
            }

            tr:nth-child(even) td {
              background: #1b2028;
            }

            tr:last-child td {
              border-bottom: none;
            }

            th:last-child,
            td:last-child {
              border-right: none;
            }

            img {
              max-width: 100%;
              height: auto;
              border-radius: 8px;
              border: 1px solid #3b414d;
              box-shadow: 0 6px 16px #00000040;
            }
            </style>
            </head>
            <body>
            <div class="markdown-root">
            {{bodyHtml}}
            </div>
            </body>
            </html>
            """;
    }

    private static string BuildLightStyledHtml(string bodyHtml)
    {
        var normalizedBodyHtml = StripCodeInlineStyles(bodyHtml);

        return $$"""
            <html>
            <head>
            <meta charset="utf-8" />
            <style>
            :root {
              color-scheme: light;
            }

            html, body {
              margin: 0;
              padding: 0;
              background: #f3f2ee;
              color: #1f0909;
            }

            {{LightCss}}

            .markdown-root {
              width: 940px;
              max-width: 940px;
              margin: 0 auto;
              padding: 0 0 36px;
            }

            .markdown-root > :first-child {
              margin-top: 0;
            }

            .markdown-root .highlight,
            .markdown-root .codehilite,
            .markdown-root div[class*="highlight"],
            .markdown-root div[class*="code"] {
              background: #ece8df !important;
              border: 1px solid #d6cdbf !important;
              border-radius: 6px !important;
              color: #1f3557 !important;
              box-shadow: none !important;
            }

            .markdown-root table[class*="highlight"],
            .markdown-root table[class*="highlight"] tr,
            .markdown-root table[class*="highlight"] td,
            .markdown-root td[class*="code"],
            .markdown-root td[class*="gutter"] {
              background: #ece8df !important;
              border-color: #d6cdbf !important;
              color: #1f3557 !important;
            }

            .markdown-root pre,
            .markdown-root pre[style] {
              background: #ece8df !important;
              border: 1px solid #d6cdbf !important;
              border-radius: 6px !important;
              color: #1f3557 !important;
              padding: 12px 14px !important;
              box-shadow: none !important;
              overflow-x: auto;
            }

            .markdown-root pre code {
              background: transparent !important;
              border: none !important;
              color: #1f3557 !important;
              padding: 0 !important;
              border-radius: 0 !important;
              display: block;
              line-height: 1.5;
            }

            .markdown-root pre span {
              background: transparent !important;
              color: #1f3557 !important;
              border: none !important;
              text-shadow: none !important;
            }

            .markdown-root code {
              background: #e7e0d3;
              border: 1px solid #d1c5b2;
              color: #3d2b20;
              border-radius: 6px;
              padding: 0.08em 0.36em;
            }
            </style>
            </head>
            <body>
            <div class="markdown-root">
            {{normalizedBodyHtml}}
            </div>
            </body>
            </html>
            """;
    }

    private static string StripCodeInlineStyles(string bodyHtml)
    {
        if (string.IsNullOrWhiteSpace(bodyHtml))
        {
            return string.Empty;
        }

        var withoutEmbeddedStyles = EmbeddedStyleBlockRegex.Replace(bodyHtml, string.Empty);
        return InlineStyleInCodeTagRegex.Replace(
            withoutEmbeddedStyles,
            match => $"<{match.Groups["tag"].Value}{match.Groups["before"].Value}{match.Groups["after"].Value}>");
    }

    private static string LoadCss(string filename)
    {
        try
        {
            var path = Path.Combine(AppContext.BaseDirectory, "Styles", filename);
            if (File.Exists(path))
            {
                return File.ReadAllText(path);
            }
        }
        catch
        {
            // ignore and use fallback embedded styles
        }

        return string.Empty;
    }
}
