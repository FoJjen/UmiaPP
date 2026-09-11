using System.Collections.ObjectModel;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using UmiaPP.Services;

namespace UmiaPP.ViewModels;

public partial class SettingsViewModel : ObservableObject
{
    private readonly ILocalizationService _localization;
    private readonly IThemeService _themeService;

    [ObservableProperty]
    private string _selectedLanguage;

    [ObservableProperty]
    private string _selectedTheme;

    [ObservableProperty]
    private string _cloudStatus = "Inaktiv (Lokal SQLite)";

    public ObservableCollection<string> Themes { get; } = new();

    public Action? CloseAction { get; set; }

    public SettingsViewModel(ILocalizationService localization, IThemeService themeService)
    {
        _localization = localization;
        _themeService = themeService;
        _selectedLanguage = _localization.CurrentLanguage;

        foreach (var t in _themeService.GetAvailableThemes()) Themes.Add(t);
        var settings = _localization.LoadSettings();
        _selectedTheme = settings.Theme ?? "Light";
    }

    [RelayCommand]
    private void Save()
    {
        if (_localization.CurrentLanguage != SelectedLanguage)
        {
            _localization.SetLanguage(SelectedLanguage);
        }

        if (!string.IsNullOrEmpty(SelectedTheme))
        {
            _themeService.ApplyTheme(SelectedTheme);
        }

        var settings = _localization.LoadSettings();
        settings.Theme = SelectedTheme;
        _localization.SaveSettings(settings);

        CloseAction?.Invoke();
    }

    [RelayCommand]
    private void OpenThemeEditor()
    {
        var dlg = new Views.ThemeEditorDialog(_themeService);
        dlg.Owner = Application.Current?.MainWindow;
        dlg.ShowDialog();

        Themes.Clear();
        foreach (var t in _themeService.GetAvailableThemes()) Themes.Add(t);
    }

    [RelayCommand]
    private void Close()
    {
        CloseAction?.Invoke();
    }
}
