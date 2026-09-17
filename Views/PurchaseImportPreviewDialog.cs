using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using UmiaPP.Services;

namespace UmiaPP.Views;

/// <summary>A preview close/escape returns to editing, not cancellation.</summary>
public sealed class PurchaseImportPreviewDialog : Window
{
    public PurchaseImportPreviewDecision Decision { get; private set; } = PurchaseImportPreviewDecision.Back;

    public PurchaseImportPreviewDialog(string previewText)
    {
        Title = "Förhandsgranska import";
        Width = 1100;
        Height = 700;
        MinWidth = 650;
        MinHeight = 350;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;

        var layout = new Grid { Margin = new Thickness(16) };
        layout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        layout.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        layout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        var description = new TextBlock
        {
            Text = "Kontrollera listorna innan de sparas. Tillbaka och redigera behåller dina tidigare ändringar. " +
                "Om du stänger rutan eller trycker Esc går du också tillbaka. Avbryt import avslutar utan att spara.",
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, 12)
        };
        Grid.SetRow(description, 0);
        layout.Children.Add(description);

        var buttons = new WrapPanel
        {
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, 12, 0, 0)
        };
        // IsCancel makes Escape close the dialog; the default decision is Back.
        buttons.Children.Add(new Button
        {
            Content = "Tillbaka och redigera", IsCancel = true,
            Padding = new Thickness(12, 6, 12, 6), Margin = new Thickness(0, 0, 8, 0)
        });
        var import = new Button
        {
            Content = "Importera", IsDefault = true,
            Padding = new Thickness(12, 6, 12, 6), Margin = new Thickness(0, 0, 8, 0)
        };
        import.Click += (_, _) =>
        {
            Decision = PurchaseImportPreviewDecision.Import;
            DialogResult = true;
        };
        buttons.Children.Add(import);
        var cancel = new Button
        {
            Content = "Avbryt import", Padding = new Thickness(12, 6, 12, 6)
        };
        cancel.Click += (_, _) =>
        {
            Decision = PurchaseImportPreviewDecision.Cancel;
            DialogResult = false;
        };
        buttons.Children.Add(cancel);
        Grid.SetRow(buttons, 2);
        layout.Children.Add(buttons);

        var preview = new TextBox
        {
            // Do not inherit the application's compact single-line input style.
            Style = null,
            Text = previewText,
            IsReadOnly = true,
            IsReadOnlyCaretVisible = true,
            AcceptsReturn = true,
            Height = double.NaN,
            MaxHeight = double.PositiveInfinity,
            VerticalAlignment = VerticalAlignment.Stretch,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalContentAlignment = VerticalAlignment.Top,
            FontFamily = new FontFamily("Consolas"),
            FontSize = 12,
            TextWrapping = TextWrapping.NoWrap,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto
        };
        Grid.SetRow(preview, 1);
        layout.Children.Add(preview);
        Content = layout;
    }
}
