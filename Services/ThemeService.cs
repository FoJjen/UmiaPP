using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;

namespace UmiaPP.Services;

public class ThemeService : IThemeService
{
    private readonly string _appThemesFolder;
    private ResourceDictionary? _activeThemeDictionary;

    public ThemeService()
    {
        _appThemesFolder = Path.Combine(
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "UmiaPP"),
            "themes");

        if (!Directory.Exists(_appThemesFolder)) Directory.CreateDirectory(_appThemesFolder);
    }

    public IEnumerable<string> GetAvailableThemes()
    {
        // Built-in themes from the Themes folder in the app plus custom themes in AppData
        var exeFolder = AppDomain.CurrentDomain.BaseDirectory;
        var builtIn = new[] { "Light", "Dark", "Umia" };

        var custom = Directory.Exists(_appThemesFolder)
            ? Directory.GetFiles(_appThemesFolder, "*.xaml").Select(Path.GetFileNameWithoutExtension)
            : Enumerable.Empty<string>();

        return builtIn.Concat(custom).Distinct();
    }

    public void ApplyTheme(string themeName)
    {
        try
        {
            var merged = Application.Current.Resources.MergedDictionaries;

            // Look for built-in theme in application Themes folder first
            var candidate = $"Themes/{themeName}.xaml";
            var uri = new System.Uri(candidate, System.UriKind.Relative);

            // Load successfully before changing the application's resources.
            var dict = new ResourceDictionary { Source = uri };
            // Keep Resources/Theme.xaml loaded for shared control styles.
            // Later merged dictionaries override earlier palette entries.
            merged.Add(dict);
            if (_activeThemeDictionary != null)
                merged.Remove(_activeThemeDictionary);
            _activeThemeDictionary = dict;

            // Save selection to settings
            var loc = new LocalizationService();
            var s = loc.LoadSettings();
            s.Theme = themeName;
            loc.SaveSettings(s);
        }
        catch (System.Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Could not apply theme: {ex.Message}");
        }
    }

    public void SaveCustomTheme(string themeName, string xamlContent)
    {
        var path = Path.Combine(_appThemesFolder, themeName + ".xaml");
        File.WriteAllText(path, xamlContent);
    }
}
