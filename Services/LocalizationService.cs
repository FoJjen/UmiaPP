using System.Globalization;
using System.IO;
using System.Text.Json;
using System.Windows;

namespace UmiaPP.Services;

public class AppSettings
{
    public string Language { get; set; } = "sv-SE";
    public string Theme { get; set; } = "Light";
    public List<string> HiddenLubeTabs { get; set; } = [];
    public List<string> HiddenLubeSpecificationColumns { get; set; } = [];
}

public interface ILocalizationService
{
    string CurrentLanguage { get; }
    void SetLanguage(string cultureCode);
    AppSettings LoadSettings();
    void SaveSettings(AppSettings settings);
    string GetString(string key);
}

public class LocalizationService : ILocalizationService
{
    private static readonly string SettingsFilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "UmiaPP",
        "settings.json");

    private string _currentLanguage = "sv-SE";
    public string CurrentLanguage => _currentLanguage;

    public LocalizationService()
    {
        var settings = LoadSettings();
        _currentLanguage = string.IsNullOrEmpty(settings.Language) ? "sv-SE" : settings.Language;
    }

    public void SetLanguage(string cultureCode)
    {
        _currentLanguage = cultureCode;

        var dictUri = cultureCode switch
        {
            "en-US" => new Uri("Resources/Strings.en-US.xaml", UriKind.Relative),
            _ => new Uri("Resources/Strings.sv-SE.xaml", UriKind.Relative)
        };

        try
        {
            var dict = new ResourceDictionary { Source = dictUri };

            var mergedDicts = Application.Current.Resources.MergedDictionaries;
            var oldDict = mergedDicts.FirstOrDefault(d => d.Source != null && d.Source.OriginalString.Contains("Strings."));
            if (oldDict != null)
            {
                mergedDicts.Remove(oldDict);
            }
            mergedDicts.Add(dict);

            var settings = LoadSettings();
            settings.Language = cultureCode;
            SaveSettings(settings);

            Thread.CurrentThread.CurrentCulture = new CultureInfo(cultureCode);
            Thread.CurrentThread.CurrentUICulture = new CultureInfo(cultureCode);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Kunde inte ladda språkresurs: {ex.Message}");
        }
    }

    public AppSettings LoadSettings()
    {
        try
        {
            if (File.Exists(SettingsFilePath))
            {
                var json = File.ReadAllText(SettingsFilePath);
                var settings = JsonSerializer.Deserialize<AppSettings>(json);
                if (settings != null) return settings;
            }
        }
        catch
        {
            // Ignorera och returnera default vid läsfel
        }

        return new AppSettings();
    }

    public void SaveSettings(AppSettings settings)
    {
        try
        {
            var folder = Path.GetDirectoryName(SettingsFilePath);
            if (!string.IsNullOrEmpty(folder) && !Directory.Exists(folder))
            {
                Directory.CreateDirectory(folder);
            }

            var json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(SettingsFilePath, json);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Fel vid sparning av inställningar: {ex.Message}");
        }
    }

    public string GetString(string key)
    {
        if (Application.Current != null && Application.Current.TryFindResource(key) is string val)
        {
            return val;
        }
        return key;
    }
}
