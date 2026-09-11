using System;
using System.Text;
using System.Windows;
using System.Windows.Media;
using UmiaPP.Services;

namespace UmiaPP.Views;

public partial class ThemeEditorDialog : Window
{
    private readonly IThemeService _themeService;
    private ResourceDictionary? _previewDict;

    public ThemeEditorDialog(IThemeService themeService)
    {
        InitializeComponent();
        _themeService = themeService;

        // Provide some defaults
        SetPrimaryColor(System.Windows.Media.Color.FromRgb(0x12, 0x4E, 0x78));
        SetBackgroundColor(System.Windows.Media.Color.FromRgb(0xF4, 0xF6, 0xF9));
        SetTextColor(System.Windows.Media.Color.FromRgb(0x20, 0x24, 0x28));

        ApplyPreview();
    }

    private void PrimaryColorButton_Click(object sender, RoutedEventArgs e)
    {
        var picker = new ColorPickerWindow(GetPrimaryColor());
        picker.Owner = Application.Current?.MainWindow;
        var ok = picker.ShowDialog();
        if (ok == true)
        {
            SetPrimaryColor(picker.SelectedColor);
            ApplyPreview();
        }
    }

    private void BackgroundColorButton_Click(object sender, RoutedEventArgs e)
    {
        var picker = new ColorPickerWindow(GetBackgroundColor());
        picker.Owner = Application.Current?.MainWindow;
        var ok = picker.ShowDialog();
        if (ok == true)
        {
            SetBackgroundColor(picker.SelectedColor);
            ApplyPreview();
        }
    }

    private void TextPrimaryColorButton_Click(object sender, RoutedEventArgs e)
    {
        var picker = new ColorPickerWindow(GetTextColor());
        picker.Owner = Application.Current?.MainWindow;
        var ok = picker.ShowDialog();
        if (ok == true)
        {
            SetTextColor(picker.SelectedColor);
            ApplyPreview();
        }
    }

    // Color picker replaced with WPF-based ColorPickerWindow

    private void SetPrimaryColor(System.Windows.Media.Color c)
    {
        PrimaryColorButton.Content = c.ToString();
        PrimaryColorButton.Background = new SolidColorBrush(c);
        PrimarySwatch.Background = new SolidColorBrush(c);
    }

    private void SetBackgroundColor(System.Windows.Media.Color c)
    {
        BackgroundColorButton.Content = c.ToString();
        BackgroundColorButton.Background = new SolidColorBrush(c);
        BackgroundSwatch.Background = new SolidColorBrush(c);
    }

    private void SetTextColor(System.Windows.Media.Color c)
    {
        TextPrimaryColorButton.Content = c.ToString();
        TextPrimaryColorButton.Background = new SolidColorBrush(c);
        TextSwatch.Background = new SolidColorBrush(c);
    }

    private System.Windows.Media.Color GetPrimaryColor()
    {
        if (PrimarySwatch.Background is SolidColorBrush sb) return sb.Color;
        return System.Windows.Media.Colors.Black;
    }

    private System.Windows.Media.Color GetBackgroundColor()
    {
        if (BackgroundSwatch.Background is SolidColorBrush sb) return sb.Color;
        return System.Windows.Media.Colors.White;
    }

    private System.Windows.Media.Color GetTextColor()
    {
        if (TextSwatch.Background is SolidColorBrush sb) return sb.Color;
        return System.Windows.Media.Colors.Black;
    }

    private void ApplyPreview()
    {
        try
        {
            RemovePreview();

            var dict = new ResourceDictionary();
            var primary = GetPrimaryColor();
            var background = GetBackgroundColor();
            var text = GetTextColor();

            dict["PrimaryColor"] = primary;
            dict["BackgroundColor"] = background;
            dict["TextPrimaryColor"] = text;

            dict["PrimaryBrush"] = new SolidColorBrush(primary);
            dict["BackgroundBrush"] = new SolidColorBrush(background);
            dict["TextPrimaryBrush"] = new SolidColorBrush(text);

            _previewDict = dict;
            Application.Current.Resources.MergedDictionaries.Insert(0, dict);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Preview apply failed: {ex.Message}");
        }
    }

    private void RemovePreview()
    {
        if (_previewDict != null)
        {
            var merged = Application.Current.Resources.MergedDictionaries;
            if (merged.Contains(_previewDict)) merged.Remove(_previewDict);
            _previewDict = null;
        }
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        RemovePreview();
        Close();
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        var name = ThemeNameBox.Text?.Trim();
        if (string.IsNullOrEmpty(name))
        {
            MessageBox.Show("Ange ett namn för temat.", "Fel", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var primary = GetPrimaryColor();
        var background = GetBackgroundColor();
        var text = GetTextColor();

        // Build a minimal ResourceDictionary XAML with required keys
        var sb = new StringBuilder();
        sb.AppendLine("<ResourceDictionary xmlns=\"http://schemas.microsoft.com/winfx/2006/xaml/presentation\" xmlns:x=\"http://schemas.microsoft.com/winfx/2006/xaml\">\n");
        sb.AppendLine($"  <Color x:Key=\"PrimaryColor\">#{primary.R:X2}{primary.G:X2}{primary.B:X2}</Color>");
        sb.AppendLine($"  <Color x:Key=\"BackgroundColor\">#{background.R:X2}{background.G:X2}{background.B:X2}</Color>");
        sb.AppendLine($"  <Color x:Key=\"TextPrimaryColor\">#{text.R:X2}{text.G:X2}{text.B:X2}</Color>");
        sb.AppendLine();
        sb.AppendLine($"  <SolidColorBrush x:Key=\"PrimaryBrush\" Color=\"{{StaticResource PrimaryColor}}\"/>");
        sb.AppendLine($"  <SolidColorBrush x:Key=\"BackgroundBrush\" Color=\"{{StaticResource BackgroundColor}}\"/>");
        sb.AppendLine($"  <SolidColorBrush x:Key=\"TextPrimaryBrush\" Color=\"{{StaticResource TextPrimaryColor}}\"/>");
        sb.AppendLine("</ResourceDictionary>");

        var xaml = sb.ToString();

        try
        {
            _themeService.SaveCustomTheme(name, xaml);
            RemovePreview();
            _themeService.ApplyTheme(name);
            Close();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Kunde inte spara temat: {ex.Message}", "Fel", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}
