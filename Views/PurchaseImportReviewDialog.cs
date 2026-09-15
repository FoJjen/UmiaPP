using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using UmiaPP.Models;

namespace UmiaPP.Views;

/// <summary>Reviews detached drafts before applying corrections to imported source items.</summary>
public sealed class PurchaseImportReviewDialog : Window
{
    private readonly List<ReviewRow> _rows;
    private readonly DataGrid _grid;
    private static readonly CategoryChoice[] CategoryChoices =
    [
        new("Okänd", "Ej klassificerade"),
        new("Kanal", "Kanaler (meter)"),
        new("Detalj", "Kanal-detaljer (styck)"),
        new("Sakvara/Don", "Sakvaror / övriga artiklar")
    ];

    public PurchaseImportReviewDialog(IReadOnlyList<BluebeamMarkupItem> items)
    {
        Title = "Granska artiklar före import";
        Width = 1300;
        Height = 600;
        MinWidth = 750;
        MinHeight = 380;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        _rows = items.Select(item => new ReviewRow(item)).ToList();

        var panel = new DockPanel { Margin = new Thickness(16) };
        var explanation = new TextBlock
        {
            Text = "Följande artiklar saknar dimension eller säker klassificering. Originaluppgifterna visas till vänster. " +
                "Välj kategori och redigera artikel, dimension och mängder till höger. " +
                "Exempel: Sadelavstick eller T-rör med dimension 315-200. " +
                "Detaljer räknas i styck. Kanaler använder längd + vertikalhöjd i meter; utan längd används antal × 3 m. " +
                "Sakvaror använder meter om längd finns, annars styck. " +
                "Avbryt fortsätter importen utan ändringar; okända artiklar behålls som Ej klassificerade.",
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, 12)
        };
        DockPanel.SetDock(explanation, Dock.Top);
        panel.Children.Add(explanation);

        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, 12, 0, 0)
        };
        var apply = new Button
        {
            Content = "Använd ändringar och importera", IsDefault = true,
            Padding = new Thickness(12, 6, 12, 6), Margin = new Thickness(0, 0, 8, 0)
        };
        apply.Click += Apply_Click;
        buttons.Children.Add(apply);
        buttons.Children.Add(new Button
        {
            Content = "Avbryt – importera utan ändringar", IsCancel = true,
            Padding = new Thickness(12, 6, 12, 6)
        });
        DockPanel.SetDock(buttons, Dock.Bottom);
        panel.Children.Add(buttons);

        _grid = new DataGrid
        {
            ItemsSource = _rows,
            AutoGenerateColumns = false,
            CanUserAddRows = false,
            CanUserDeleteRows = false,
            ColumnWidth = DataGridLength.Auto,
            SelectionUnit = DataGridSelectionUnit.Cell,
            Language = System.Windows.Markup.XmlLanguage.GetLanguage("sv-SE")
        };
        AddTextColumn("Ritning", "Source.SheetLabel", true);
        AddTextColumn("Ämne (original)", "Source.Subject", true);
        AddTextColumn("Lager (original)", "Source.Layer", true);
        AddTextColumn("Kommentarer (original)", "Source.Comments", true);
        AddTextColumn("Kategori (ursprunglig)", "Source.Category", true);
        _grid.Columns.Add(new DataGridComboBoxColumn
        {
            Header = "Välj kategori",
            ItemsSource = CategoryChoices,
            DisplayMemberPath = nameof(CategoryChoice.Label),
            SelectedValuePath = nameof(CategoryChoice.Value),
            SelectedValueBinding = EditBinding(nameof(ReviewRow.Category)),
            MinWidth = 160
        });
        AddTextColumn("Typ / Artikel", nameof(ReviewRow.ArticleType), false);
        AddTextColumn("Dimension (t.ex. 315-200)", nameof(ReviewRow.Dimension), false);
        AddTextColumn("Antal (st)", nameof(ReviewRow.Count), false, true);
        AddTextColumn("Längd (m)", nameof(ReviewRow.LengthMeters), false, true);
        AddTextColumn("Vertikalhöjd (m)", nameof(ReviewRow.VerticalHeightMeters), false, true);
        panel.Children.Add(_grid);
        Content = panel;
    }

    private static Binding EditBinding(string path) => new(path)
    {
        Mode = BindingMode.TwoWay,
        UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged,
        ValidatesOnExceptions = true,
        ConverterCulture = CultureInfo.GetCultureInfo("sv-SE")
    };

    private void AddTextColumn(string title, string path, bool readOnly, bool numeric = false)
    {
        var binding = readOnly ? new Binding(path) { Mode = BindingMode.OneWay } : EditBinding(path);
        if (numeric) binding.StringFormat = "0.##";
        _grid.Columns.Add(new DataGridTextColumn
        {
            Header = title, Binding = binding, IsReadOnly = readOnly,
            MinWidth = readOnly ? 70 : 110
        });
    }

    private void Apply_Click(object sender, RoutedEventArgs e)
    {
        if (!_grid.CommitEdit(DataGridEditingUnit.Cell, true) ||
            !_grid.CommitEdit(DataGridEditingUnit.Row, true)) return;

        foreach (var row in _rows)
        {
            if (!ValidQuantity(row.Count) || !ValidQuantity(row.LengthMeters) || !ValidQuantity(row.VerticalHeightMeters))
            {
                MessageBox.Show(this, "Ange giltiga mängder som är noll eller större.", "Kontrollera mängder",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            if (row.Category != "Okänd" && string.IsNullOrWhiteSpace(row.ArticleType))
            {
                MessageBox.Show(this, "Ange typ eller artikel för de artiklar som klassificeras.", "Kontrollera artikel",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
        }

        // Only after all rows validate do we update the imported source items.
        foreach (var row in _rows)
        {
            row.Source.Category = row.Category;
            row.Source.Subject = row.ArticleType.Trim();
            row.Source.Dimension = row.Dimension.Trim().Replace("Ø", string.Empty).Replace("ø", string.Empty)
                .Replace("–", "-").Replace(" - ", "-");
            row.Source.Count = row.Count;
            row.Source.LengthMeters = row.LengthMeters;
            row.Source.VerticalHeightMeters = row.VerticalHeightMeters;
        }
        DialogResult = true;
    }

    private static bool ValidQuantity(double value) => double.IsFinite(value) && value >= 0;

    public sealed record CategoryChoice(string Value, string Label);

    public sealed class ReviewRow
    {
        public ReviewRow(BluebeamMarkupItem source)
        {
            Source = source;
            Category = CategoryChoices.Any(choice => choice.Value == source.Category) ? source.Category : "Okänd";
            ArticleType = string.IsNullOrWhiteSpace(source.Subject) ? source.Layer : source.Subject;
            Dimension = source.Dimension is "-" or "Ospecificerad dim" ? string.Empty : source.Dimension ?? string.Empty;
            Count = source.Count;
            LengthMeters = source.LengthMeters;
            VerticalHeightMeters = source.VerticalHeightMeters;
        }

        public BluebeamMarkupItem Source { get; }
        public string Category { get; set; }
        public string ArticleType { get; set; }
        public string Dimension { get; set; }
        public double Count { get; set; }
        public double LengthMeters { get; set; }
        public double VerticalHeightMeters { get; set; }
    }
}
