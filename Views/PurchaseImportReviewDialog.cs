using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using CommunityToolkit.Mvvm.ComponentModel;
using UmiaPP.Models;
using UmiaPP.Services;

namespace UmiaPP.Views;

/// <summary>Reviews detached drafts before applying corrections to imported source items.</summary>
public sealed class PurchaseImportReviewDialog : Window
{
    private readonly List<ReviewRow> _rows;
    private readonly DataGrid _grid;
    private readonly Dictionary<string, List<ReviewRow>> _rowsBySubject;
    private bool _synchronizingFormatting;
    private readonly HashSet<string> _changedCategorySubjects = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> _categorySelections = new(StringComparer.Ordinal);

    public IReadOnlyDictionary<string, string> CategorySelections => _categorySelections;

    private static readonly CategoryChoice[] CategoryChoices =
    [
        new("Okänd", "Ej klassificerade"),
        new("Kanal", "Kanaler (meter)"),
        new("Detalj", "Kanal-detaljer (styck)"),
        new("Sakvara/Don", "Sakvaror / övriga artiklar")
    ];

    public PurchaseImportReviewDialog(IReadOnlyList<BluebeamMarkupItem> items,
        IReadOnlyDictionary<BluebeamMarkupItem, string>? originalSubjects = null)
    {
        Title = "Granska artiklar före import";
        Width = 1300;
        Height = 600;
        MinWidth = 750;
        MinHeight = 380;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        _rows = items.Select(item => new ReviewRow(item,
            originalSubjects != null && originalSubjects.TryGetValue(item, out var originalSubject)
                ? originalSubject : null)).ToList();
        // Match the immutable original subject, never the description being edited.
        // Blank subjects must not cause unrelated unnamed articles to be linked.
        _rowsBySubject = _rows.Where(row => !string.IsNullOrWhiteSpace(row.OriginalSubject))
            .GroupBy(row => row.OriginalSubject, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.ToList(), StringComparer.Ordinal);
        foreach (var group in _rowsBySubject.Values)
            foreach (var row in group) row.MatchingSubjectCount = group.Count;
        foreach (var row in _rows) row.PropertyChanged += ReviewRow_PropertyChanged;

        var panel = new DockPanel { Margin = new Thickness(16) };
        var explanation = new TextBlock
        {
            Text = "Följande artiklar saknar dimension eller säker klassificering. Originaluppgifterna visas till vänster. " +
                "Välj kategori och redigera artikel, dimension och mängder till höger. " +
                "Kategori, Typ / Artikel och Dimension ändras samtidigt på alla rader med samma ursprungliga Ämne, även på andra ritningar. " +
                "Antal, längd och vertikalhöjd behålls per rad. Tomma ämnen kopplas inte ihop. " +
                "Manuellt ändrade kategorival sparas efter bekräftad import och återanvänds för samma Ämne i alla projekt på den här datorn. " +
                "Exempel: Sadelavstick eller T-rör med dimension 315-200. " +
                "Detaljer räknas i styck. Kanaler använder längd + vertikalhöjd i meter; utan längd används antal × 3 m. " +
                "Sakvaror använder meter om längd finns, annars styck. " +
                "Till förhandsgranskning utan fler ändringar behåller tidigare uppgifter. " +
                "Inget sparas förrän du väljer Importera i förhandsgranskningen; okända artiklar behålls som Ej klassificerade.",
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
        var help = new Button
        {
            Content = PurchaseImportFormatHelp.Title,
            Padding = new Thickness(12, 6, 12, 6), Margin = new Thickness(0, 0, 8, 0)
        };
        help.Click += (_, _) => MessageBox.Show(this, PurchaseImportFormatHelp.Text,
            PurchaseImportFormatHelp.Title, MessageBoxButton.OK, MessageBoxImage.Information);
        buttons.Children.Add(help);
        var apply = new Button
        {
            Content = "Använd ändringar och fortsätt", IsDefault = true,
            Padding = new Thickness(12, 6, 12, 6), Margin = new Thickness(0, 0, 8, 0)
        };
        apply.Click += Apply_Click;
        buttons.Children.Add(apply);
        buttons.Children.Add(new Button
        {
            Content = "Till förhandsgranskning utan fler ändringar", IsCancel = true,
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
        AddTextColumn("Ämne (original)", nameof(ReviewRow.OriginalSubject), true);
        AddTextColumn("Rader med samma ämne", nameof(ReviewRow.MatchingSubjectCount), true);
        AddTextColumn("Lager (original)", "Source.Layer", true);
        AddTextColumn("Kommentarer (original)", "Source.Comments", true);
        AddTextColumn("Kategori (ursprunglig)", "Source.Category", true);
        _grid.Columns.Add(new DataGridComboBoxColumn
        {
            Header = "Välj kategori",
            // Matching subjects may include already-classified rows. Preserve
            // their original category unless the user deliberately changes it.
            ItemsSource = CategoryChoices.Concat(items.Where(item => !string.IsNullOrWhiteSpace(item.Category) &&
                    !string.Equals(item.Category.Trim(), "Byggmaterial", StringComparison.OrdinalIgnoreCase))
                .Select(item => new CategoryChoice(item.Category, item.Category)))
                .DistinctBy(choice => choice.Value).ToArray(),
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

    private void ReviewRow_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (_synchronizingFormatting || sender is not ReviewRow edited) return;
        if (e.PropertyName is not (nameof(ReviewRow.Category) or nameof(ReviewRow.ArticleType) or nameof(ReviewRow.Dimension)))
            return;
        if (!_rowsBySubject.TryGetValue(edited.OriginalSubject, out var matching)) return;

        if (e.PropertyName == nameof(ReviewRow.Category))
            _changedCategorySubjects.Add(edited.OriginalSubject);

        _synchronizingFormatting = true;
        try
        {
            foreach (var row in matching)
            {
                if (ReferenceEquals(row, edited)) continue;
                switch (e.PropertyName)
                {
                    case nameof(ReviewRow.Category): row.Category = edited.Category; break;
                    case nameof(ReviewRow.ArticleType): row.ArticleType = edited.ArticleType; break;
                    case nameof(ReviewRow.Dimension): row.Dimension = edited.Dimension; break;
                }
            }
        }
        finally
        {
            _synchronizingFormatting = false;
        }
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
            if (string.IsNullOrWhiteSpace(row.Category))
            {
                MessageBox.Show(this, "Välj en kategori för varje artikel.", "Kontrollera kategori",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
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
        // Return only deliberate category edits. Defaults and inherited choices
        // are not learned, and closing/canceling this draft never returns edits.
        _categorySelections.Clear();
        foreach (var subject in _changedCategorySubjects)
            _categorySelections[subject] = _rowsBySubject[subject][0].Category;
        DialogResult = true;
    }

    private static bool ValidQuantity(double value) => double.IsFinite(value) && value >= 0;

    public sealed record CategoryChoice(string Value, string Label);

    public sealed class ReviewRow : ObservableObject
    {
        private string _category;
        private string _articleType;
        private string _dimension;

        public ReviewRow(BluebeamMarkupItem source, string? originalSubject = null)
        {
            Source = source;
            OriginalSubject = originalSubject ?? source.Subject ?? string.Empty;
            _category = string.Equals(source.Category?.Trim(), "Byggmaterial", StringComparison.OrdinalIgnoreCase)
                ? "Sakvara/Don"
                : string.IsNullOrWhiteSpace(source.Category) ? "Okänd" : source.Category;
            _articleType = string.IsNullOrWhiteSpace(source.Subject) ? source.Layer ?? string.Empty : source.Subject;
            _dimension = source.Dimension is "-" or "Ospecificerad dim" ? string.Empty : source.Dimension ?? string.Empty;
            Count = source.Count;
            LengthMeters = source.LengthMeters;
            VerticalHeightMeters = source.VerticalHeightMeters;
        }

        public BluebeamMarkupItem Source { get; }
        public string OriginalSubject { get; }
        public int MatchingSubjectCount { get; internal set; } = 1;
        public string Category { get => _category; set => SetProperty(ref _category, value ?? string.Empty); }
        public string ArticleType { get => _articleType; set => SetProperty(ref _articleType, value ?? string.Empty); }
        public string Dimension { get => _dimension; set => SetProperty(ref _dimension, value ?? string.Empty); }
        public double Count { get; set; }
        public double LengthMeters { get; set; }
        public double VerticalHeightMeters { get; set; }
    }
}
