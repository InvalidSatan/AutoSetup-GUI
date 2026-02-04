using System.Collections.Generic;
using System.Windows;

namespace AutoSetupGUI.Infrastructure;

/// <summary>
/// Manages theme switching between light and dark modes.
/// </summary>
public static class ThemeManager
{
    // Light theme is AppStateColors.xaml, Dark theme is DarkTheme.xaml
    private static readonly Uri LightThemeUri = new("Themes/AppStateColors.xaml", UriKind.Relative);
    private static readonly Uri DarkThemeUri = new("Themes/DarkTheme.xaml", UriKind.Relative);
    private static readonly Uri LightThemeExtrasUri = new("Themes/LightTheme.xaml", UriKind.Relative);

    /// <summary>
    /// Gets or sets whether dark mode is currently enabled.
    /// </summary>
    public static bool IsDarkMode { get; private set; }

    /// <summary>
    /// Toggles between light and dark themes.
    /// </summary>
    public static void ToggleTheme()
    {
        SetTheme(!IsDarkMode);
    }

    /// <summary>
    /// Sets the theme to dark or light mode.
    /// </summary>
    public static void SetTheme(bool darkMode)
    {
        IsDarkMode = darkMode;

        var mergedDictionaries = Application.Current.Resources.MergedDictionaries;

        // Find and remove the current color themes (AppStateColors, DarkTheme, and LightTheme)
        var toRemove = new List<ResourceDictionary>();
        foreach (var dict in mergedDictionaries)
        {
            if (dict.Source != null &&
                (dict.Source.OriginalString.Contains("AppStateColors") ||
                 dict.Source.OriginalString.Contains("DarkTheme") ||
                 dict.Source.OriginalString.Contains("LightTheme")))
            {
                toRemove.Add(dict);
            }
        }

        foreach (var dict in toRemove)
        {
            mergedDictionaries.Remove(dict);
        }

        // Add the new theme at the beginning so SharedStyles can reference these colors
        var newTheme = new ResourceDictionary
        {
            Source = darkMode ? DarkThemeUri : LightThemeUri
        };

        // Insert at position 0 so other styles can reference these colors
        mergedDictionaries.Insert(0, newTheme);

        // For light mode, also add the LightTheme extras
        if (!darkMode)
        {
            var lightExtras = new ResourceDictionary
            {
                Source = LightThemeExtrasUri
            };
            mergedDictionaries.Insert(1, lightExtras);
        }

        // Save preference
        SaveThemePreference(darkMode);
    }

    /// <summary>
    /// Loads the saved theme preference on startup.
    /// </summary>
    public static void LoadSavedTheme()
    {
        var darkMode = LoadThemePreference();
        if (darkMode)
        {
            SetTheme(true);
        }
    }

    private static void SaveThemePreference(bool darkMode)
    {
        try
        {
            var settingsPath = GetSettingsPath();
            System.IO.File.WriteAllText(settingsPath, darkMode ? "dark" : "light");
        }
        catch
        {
            // Ignore settings save errors
        }
    }

    private static bool LoadThemePreference()
    {
        try
        {
            var settingsPath = GetSettingsPath();
            if (System.IO.File.Exists(settingsPath))
            {
                var content = System.IO.File.ReadAllText(settingsPath).Trim().ToLowerInvariant();
                return content == "dark";
            }
        }
        catch
        {
            // Ignore settings load errors
        }
        return false;
    }

    private static string GetSettingsPath()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var folder = System.IO.Path.Combine(appData, "UniversityAutoSetup");
        System.IO.Directory.CreateDirectory(folder);
        return System.IO.Path.Combine(folder, "theme.txt");
    }
}
