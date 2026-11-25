using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace KEYCE_KODER.App.Services;

public sealed class AppSettings
{
    private static readonly string SettingsFolder = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "KEYCE_KODER");

    private static readonly string SettingsPath = Path.Combine(SettingsFolder, "settings.json");

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    public static AppSettings Current { get; set; } = Load();

    // 1. Apparence
    public string ThemeName { get; set; } = "midnight-orange";

    /// <summary>
    /// Couleur d'arrière-plan personnalisée pour la zone d'édition du code (null = fond du thème actif).
    /// </summary>
    public string? CustomEditorBackground { get; set; }

    // 2. Police de l'éditeur
    public string EditorFontFamily { get; set; } = "Cascadia Code, Consolas";
    public double EditorFontSize { get; set; } = 14.0;
    public double EditorLineHeight { get; set; } = 1.4;
    public int TabSize { get; set; } = 4;
    public bool InsertSpaces { get; set; } = true;

    // 3. Options de l'éditeur
    public bool ShowLineNumbers { get; set; } = true;
    public bool HighlightCurrentLine { get; set; } = true;
    public bool HighlightMatchingBrackets { get; set; } = true;
    public bool ShowIndentGuides { get; set; } = true;
    public bool WordWrap { get; set; } = false;
    public bool ShowWhitespace { get; set; } = false;
    public bool AutoCloseBrackets { get; set; } = true;
    public bool AutoIndent { get; set; } = true;
    public bool AutoSave { get; set; } = false;
    public int AutoSaveDelaySeconds { get; set; } = 5;

    // 4. Coloration syntaxique par langage (catégorie -> couleur hex)
    public Dictionary<string, Dictionary<string, string>> SyntaxColors { get; set; } = new(StringComparer.OrdinalIgnoreCase)
    {
        ["C#"] = new Dictionary<string, string>
        {
            ["Mots-clés"] = "#569CD6",
            ["Variables"] = "#9CDCFE",
            ["Fonctions"] = "#DCDCAA",
            ["Classes et types"] = "#4EC9B0",
            ["Chaînes de caractères"] = "#CE9178",
            ["Nombres"] = "#B5CEA8",
            ["Commentaires"] = "#6A9955",
            ["Opérateurs"] = "#D4D4D4"
        },
        ["Python"] = new Dictionary<string, string>
        {
            ["Mots-clés"] = "#C586C0",
            ["Variables"] = "#9CDCFE",
            ["Fonctions"] = "#DCDCAA",
            ["Classes et types"] = "#4EC9B0",
            ["Chaînes de caractères"] = "#CE9178",
            ["Nombres"] = "#B5CEA8",
            ["Commentaires"] = "#6A9955",
            ["Opérateurs"] = "#D4D4D4"
        },
        ["JavaScript"] = new Dictionary<string, string>
        {
            ["Mots-clés"] = "#569CD6",
            ["Variables"] = "#9CDCFE",
            ["Fonctions"] = "#DCDCAA",
            ["Classes et types"] = "#4EC9B0",
            ["Chaînes de caractères"] = "#CE9178",
            ["Nombres"] = "#B5CEA8",
            ["Commentaires"] = "#6A9955",
            ["Opérateurs"] = "#D4D4D4"
        }
    };

    // 5. Terminal
    public string DefaultShell { get; set; } = "PowerShell";
    public string DefaultWorkingDirectory { get; set; } = "";
    public string TerminalFontFamily { get; set; } = "Consolas";
    public double TerminalFontSize { get; set; } = 13.0;

    // 6. Comportement IDE
    public bool RestoreTabs { get; set; } = true;
    public bool RestorePanels { get; set; } = true;
    public bool ConfirmOnExit { get; set; } = true;

    // 7. Intelligence Artificielle
    public string AiModel { get; set; } = "qwen3.5:latest";
    public bool AiThinkingEnabled { get; set; } = false;
    public int AiContextSize { get; set; } = 4096;

    // 8. Projets récents
    public List<string> RecentProjects { get; set; } = new();

    // 9. Exécution multi-langages & Débogage
    public bool EnableColoredExecutionOutput { get; set; } = true;
    public bool AutoStopPreviousRun { get; set; } = true;

    public void AddRecentProject(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return;
        RecentProjects.RemoveAll(p => string.Equals(p, path, StringComparison.OrdinalIgnoreCase));
        RecentProjects.Insert(0, path);
        if (RecentProjects.Count > 10)
            RecentProjects.RemoveRange(10, RecentProjects.Count - 10);
        Save();
    }

    public static AppSettings Load()
    {
        try
        {
            if (File.Exists(SettingsPath))
            {
                string json = File.ReadAllText(SettingsPath);
                var loaded = JsonSerializer.Deserialize<AppSettings>(json, JsonOptions);
                if (loaded != null)
                {
                    if (loaded.AiModel == "Gemini 1.5 Pro")
                    {
                        loaded.AiModel = "qwen3.5:latest";
                        loaded.Save();
                    }

                    return loaded;
                }
            }
        }
        catch
        {
            // En cas d'erreur de lecture, utiliser les réglages par défaut
        }

        return new AppSettings();
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(SettingsFolder);
            string json = JsonSerializer.Serialize(this, JsonOptions);
            File.WriteAllText(SettingsPath, json);
        }
        catch
        {
            // Ignorer si échec d'écriture
        }
    }
}
