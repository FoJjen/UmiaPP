using System.Configuration;
using System.Data;
using System.Windows;
using UmiaPP.Data;
using UmiaPP.Services;
using UmiaPP.ViewModels;

namespace UmiaPP
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        private void Application_Startup(object sender, StartupEventArgs e)
        {
            var localization = new LocalizationService();
            localization.SetLanguage(localization.CurrentLanguage);

            // Apply theme before creating windows
            var themeService = new ThemeService();
            var settings = localization.LoadSettings();
            themeService.ApplyTheme(settings.Theme ?? "Light");

            var dataService = new LocalDataService();
            var mainViewModel = new MainViewModel(
                dataService,
                localization,
                new DialogService(),
                new LubeImportService(),
                new BluebeamXmlParserService(),
                new ExportService(),
                themeService);

            var mainWindow = new MainWindow { DataContext = mainViewModel };
            MainWindow = mainWindow;
            mainWindow.Show();
        }
    }

}
