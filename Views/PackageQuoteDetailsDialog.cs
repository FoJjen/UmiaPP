using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Markup;
using UmiaPP.Models;

namespace UmiaPP.Views;

/// <summary>Quote details with read-only and editable dialog modes.</summary>
public sealed partial class PackageQuoteDetailsDialog : Window
{
    public PackageQuoteDetailsDialog(LubeSakvaraOffert quote, IReadOnlyList<LubeSakvaraMangd> articles)
    {
        var culture = CultureInfo.GetCultureInfo("sv-SE");
        var reference = string.IsNullOrWhiteSpace(quote.Produktgrupp)
            ? $"#{quote.Id}" : $"{quote.Produktgrupp} (#{quote.Id})";
        Title = $"Offert {reference}";
        Width = 800;
        Height = 640;
        MinWidth = 560;
        MinHeight = 400;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Language = XmlLanguage.GetLanguage("sv-SE");
        FontFamily = new System.Windows.Media.FontFamily("Segoe UI Variable, Segoe UI");
        FontSize = 13;
        SetResourceReference(BackgroundProperty, "BackgroundBrush");
        SetResourceReference(ForegroundProperty, "TextPrimaryBrush");

        var layout = new Grid { Margin = new Thickness(24) };
        layout.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        layout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        Content = layout;

        var content = new StackPanel();
        var scroll = new ScrollViewer
        {
            Content = content,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled
        };
        layout.Children.Add(scroll);
        content.Children.Add(new TextBlock
        {
            Text = $"Offert {reference}",
            FontSize = 22,
            FontWeight = FontWeights.SemiBold,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, 16)
        });
        AddDetail(content, "Leverantör", quote.Leverantor);
        AddDetail(content, "Datum", quote.QuoteDate.ToString("yyyy-MM-dd", culture));
        AddDetail(content, "Offererat belopp", $"{quote.OffereratPris.ToString("N2", culture)} {quote.Valuta}");
        AddDetail(content, "Slutpris", $"{quote.EffectiveAmount.ToString("N2", culture)} {quote.Valuta}"
            + (quote.FinalAmount == 0 ? " (offererat belopp används)" : string.Empty));
        AddDetail(content, "Fil", quote.FilePath);
        if (!string.IsNullOrWhiteSpace(quote.Kommentar))
            AddDetail(content, "Kommentar", quote.Kommentar);

        content.Children.Add(new TextBlock
        {
            Text = $"Kopplade artiklar ({articles.Count})",
            FontSize = 16,
            FontWeight = FontWeights.SemiBold,
            Margin = new Thickness(0, 16, 0, 8)
        });
        var articleGrid = new DataGrid
        {
            ItemsSource = articles,
            AutoGenerateColumns = false,
            IsReadOnly = true,
            CanUserAddRows = false,
            CanUserDeleteRows = false,
            Height = 240
        };
        articleGrid.Columns.Add(new DataGridTextColumn
        {
            Header = "Beteckning",
            Binding = new Binding(nameof(LubeSakvaraMangd.Beteckning)),
            Width = new DataGridLength(1, DataGridLengthUnitType.Star)
        });
        articleGrid.Columns.Add(new DataGridTextColumn
        {
            Header = "Produkt",
            Binding = new Binding(nameof(LubeSakvaraMangd.Produkt)),
            Width = new DataGridLength(1, DataGridLengthUnitType.Star)
        });
        articleGrid.Columns.Add(new DataGridTextColumn
        {
            Header = "Antal",
            Binding = new Binding(nameof(LubeSakvaraMangd.Antal)) { StringFormat = "N2", ConverterCulture = culture },
            ElementStyle = TryFindResource("NumericCellText") as Style,
            Width = 90
        });
        articleGrid.Columns.Add(new DataGridTextColumn
        {
            Header = "Enhet",
            Binding = new Binding(nameof(LubeSakvaraMangd.Enhet)),
            Width = 70
        });
        content.Children.Add(articleGrid);

        var close = new Button
        {
            Content = "Stäng",
            IsCancel = true,
            IsDefault = true,
            MinWidth = 90,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, 16, 0, 0)
        };
        close.SetResourceReference(StyleProperty, "SecondaryButton");
        close.Click += (_, _) => Close();
        Grid.SetRow(close, 1);
        layout.Children.Add(close);
    }

    private static void AddDetail(Panel parent, string label, string? value)
    {
        var line = new Grid { Margin = new Thickness(0, 0, 0, 8) };
        line.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(145) });
        line.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        var caption = new TextBlock { Text = label, TextWrapping = TextWrapping.Wrap };
        caption.SetResourceReference(TextBlock.ForegroundProperty, "TextSecondaryBrush");
        var text = new TextBlock
        {
            Text = string.IsNullOrWhiteSpace(value) ? "—" : value,
            TextWrapping = TextWrapping.Wrap,
            ToolTip = value
        };
        Grid.SetColumn(text, 1);
        line.Children.Add(caption);
        line.Children.Add(text);
        parent.Children.Add(line);
    }
}
