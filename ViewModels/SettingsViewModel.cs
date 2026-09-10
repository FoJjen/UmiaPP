using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using UmiaPP.Services;

namespace UmiaPP.ViewModels;

public partial class SettingsViewModel : ObservableObject
{
    private readonly ILocalizationService _localization;

    [ObservableProperty]
    private string _selectedLanguage;

    [ObservableProperty]
    private string _cloudStatus = "Inaktiv (Lokal SQLite)";

    public Action? CloseAction { get; set; }

    public SettingsViewModel(ILocalizationService localization)
    {
        _localization = localization;
        _selectedLanguage = _localization.CurrentLanguage;
    }

    [RelayCommand]
    private void Save()
    {
        if (_localization.CurrentLanguage != SelectedLanguage)
        {
            _localization.SetLanguage(SelectedLanguage);
        }

        CloseAction?.Invoke();
    }

    [RelayCommand]
    private void Close()
    {
        CloseAction?.Invoke();
    }
}
