using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Windows.Data;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using UmiaPP.Models;

namespace UmiaPP.ViewModels;

public partial class PurchaseTabViewModel
{
    private readonly Dictionary<Drawing, List<PurchaseArticle>> _listRows = [];
    private readonly HashSet<Drawing> _unreadableLists = [];
    private readonly SemaphoreSlim _listSaveLock = new(1, 1);
    private const string AllFilter = "Alla";
    [ObservableProperty] private bool _isListsBusy;
    [ObservableProperty] private string _listStatus = string.Empty;
    [ObservableProperty] private string _newListName = string.Empty;
    [ObservableProperty] private string _categoryFilter = AllFilter;
    [ObservableProperty] private string _typeFilter = AllFilter;
    [ObservableProperty] private PurchaseArticle? _selectedArticle;
    [ObservableProperty] private ObservableCollection<PurchaseArticle> _articles = [];
    [ObservableProperty] private ICollectionView? _articleView;
    [ObservableProperty] private ObservableCollection<string> _categories = [AllFilter];
    [ObservableProperty] private ObservableCollection<string> _articleTypes = [AllFilter];

    private void InitializePurchaseLists()
    {
        _listRows.Clear();
        _unreadableLists.Clear();
        // Existing project markups are retained. Materialize their per-sheet lists
        // in memory; only a deliberate save/import writes the new representation.
        foreach (var drawing in Drawings.ToArray()) LoadListRows(drawing);
        foreach (var group in Markups.GroupBy(m => SheetKey(m.SheetLabel)))
        {
            var drawing = Drawings.FirstOrDefault(d => SheetKey(d.DrawingNumber) == group.Key || SheetKey(d.SheetTitle) == group.Key);
            if (drawing == null)
            {
                drawing = new Drawing
                {
                    ProjectId = Project.Id, DrawingNumber = group.Key, SheetTitle = group.Key,
                    PurchaseImportSource = "Bluebeam XML (tidigare import)"
                };
                Drawings.Add(drawing);
            }
            if (string.IsNullOrWhiteSpace(drawing.PurchaseItemsJson) && !_unreadableLists.Contains(drawing))
                _listRows[drawing] = ConvertArticles(group.ToList());
        }
        SelectedDrawing = Drawings.FirstOrDefault();
        SelectPurchaseList(SelectedDrawing);
    }

    private static string SheetKey(string? label) => string.IsNullOrWhiteSpace(label) ? "Utan sidetikett" : label.Trim();
    private static string Key(PurchaseArticle row) => JsonSerializer.Serialize(new[] { row.Category, row.Type, row.Dimension, row.Unit });

    private List<PurchaseArticle> ConvertArticles(List<BluebeamMarkupItem> items) =>
        _bluebeamParser.GroupAndSummarize(items).Select(row => new PurchaseArticle
        {
            Category = row.Category, Type = row.Type, Dimension = row.DimensionOrCode,
            Quantity = row.Quantity, Unit = row.Unit
        }).ToList();

    private List<PurchaseArticle> LoadListRows(Drawing drawing)
    {
        if (_listRows.TryGetValue(drawing, out var rows)) return rows;
        try
        {
            rows = string.IsNullOrWhiteSpace(drawing.PurchaseItemsJson) ? []
                : JsonSerializer.Deserialize<List<PurchaseArticle>>(drawing.PurchaseItemsJson)
                    ?? throw new JsonException("Artikellistan är tom eller ogiltig.");
        }
        catch (JsonException ex)
        {
            _unreadableLists.Add(drawing);
            rows = [];
            _dialogService.ShowError($"Listan {drawing.DrawingNumber} kunde inte läsas. Sparade data har inte ändrats.\n{ex.Message}", "Inköp");
        }
        _listRows[drawing] = rows;
        return rows;
    }

    private void SelectPurchaseList(Drawing? drawing, List<PurchaseArticle>? combinedRows = null)
    {
        IsCombinedSelection = combinedRows != null;
        Articles = new ObservableCollection<PurchaseArticle>(combinedRows ?? (drawing == null ? [] : LoadListRows(drawing)));
        SelectedArticle = null;
        CategoryFilter = AllFilter;
        TypeFilter = AllFilter;
        ArticleView = CollectionViewSource.GetDefaultView(Articles);
        ArticleView.Filter = value => value is PurchaseArticle row &&
            (CategoryFilter == AllFilter || row.Category == CategoryFilter) &&
            (TypeFilter == AllFilter || row.Type == TypeFilter);
        ArticleView.SortDescriptions.Add(new SortDescription(nameof(PurchaseArticle.Category), ListSortDirection.Ascending));
        ArticleView.SortDescriptions.Add(new SortDescription(nameof(PurchaseArticle.Type), ListSortDirection.Ascending));
        RefreshArticleFilters();
        LoadedPdfPath = string.Empty;
        LoadedPdfUri = null;
        if (drawing != null && !string.IsNullOrWhiteSpace(drawing.FilePath) && File.Exists(drawing.FilePath))
            ShowPdf(drawing.FilePath);
        ListStatus = drawing == null ? "Importera XML eller skapa en egen lista."
            : _unreadableLists.Contains(drawing) ? "Listan kan inte läsas. Spara inte över originaldata."
            : $"{drawing.DrawingNumber}: {Articles.Count} artikelrader. Ändringar sparas när raden bekräftas.";
    }

    partial void OnCategoryFilterChanged(string value)
    {
        TypeFilter = AllFilter;
        RefreshArticleFilters();
    }
    partial void OnTypeFilterChanged(string value) => ArticleView?.Refresh();

    private void RefreshArticleFilters()
    {
        Categories = new ObservableCollection<string>(new[] { AllFilter }.Concat(Articles.Select(a => a.Category).Distinct().OrderBy(s => s)));
        ArticleTypes = new ObservableCollection<string>(new[] { AllFilter }.Concat(Articles
            .Where(a => CategoryFilter == AllFilter || a.Category == CategoryFilter).Select(a => a.Type).Distinct().OrderBy(s => s)));
        ArticleView?.Refresh();
    }

    private static Drawing Snapshot(Drawing drawing, List<PurchaseArticle> rows) => new()
    {
        Id = drawing.Id, ProjectId = drawing.ProjectId, DrawingNumber = drawing.DrawingNumber,
        SheetTitle = drawing.SheetTitle, Revision = drawing.Revision, Date = drawing.Date,
        FilePath = drawing.FilePath, Notes = drawing.Notes,
        PurchaseImportSource = drawing.PurchaseImportSource,
        PurchaseItemsJson = JsonSerializer.Serialize(rows)
    };

    private async Task SaveListAsync(Drawing drawing, List<PurchaseArticle> rows)
    {
        var snapshot = Snapshot(drawing, rows);
        await _listSaveLock.WaitAsync();
        try
        {
            // An earlier queued save may have assigned a newly created list its ID.
            snapshot.Id = drawing.Id;
            await _dataService.Projects.SavePurchaseListsAsync(drawing.ProjectId, [snapshot]);
            drawing.Id = snapshot.Id;
            drawing.PurchaseItemsJson = snapshot.PurchaseItemsJson;
        }
        finally { _listSaveLock.Release(); }
    }

    public async Task SavePurchaseArticleAsync(PurchaseArticle row)
    {
        var drawing = _listRows.FirstOrDefault(pair => pair.Value.Contains(row)).Key;
        if (drawing == null || _unreadableLists.Contains(drawing)) return;
        if (!double.IsFinite(row.Quantity) || row.Quantity < 0 || string.IsNullOrWhiteSpace(row.Type) || string.IsNullOrWhiteSpace(row.Unit))
        {
            _dialogService.ShowError("Ange typ, enhet och en ändlig mängd på minst 0. Raden har inte sparats.", "Validering");
            return;
        }
        try
        {
            await SaveListAsync(drawing, _listRows[drawing]);
            if (SelectedDrawing == drawing)
            {
                RefreshArticleFilters();
                ListStatus = "Artikellistan sparad.";
            }
        }
        catch (Exception ex) { _dialogService.ShowError($"Kunde inte spara artikeln: {ex.Message}", "Inköp"); }
    }

    [RelayCommand]
    private async Task CreateManualListAsync()
    {
        if (IsListsBusy) return;
        var name = NewListName.Trim();
        if (name.Length == 0) { _dialogService.ShowError("Ange ett namn eller ritningsnummer.", "Inköp"); return; }
        if (Drawings.Any(d => string.Equals(d.DrawingNumber, name, StringComparison.OrdinalIgnoreCase)))
        { _dialogService.ShowError("Namnet finns redan i projektet. Välj ett annat namn.", "Inköp"); return; }
        IsListsBusy = true;
        try
        {
            var drawing = new Drawing { ProjectId = Project.Id, DrawingNumber = name, SheetTitle = name, PurchaseImportSource = "Manuell lista" };
            await SaveListAsync(drawing, []);
            _listRows[drawing] = [];
            Drawings.Add(drawing);
            SelectedDrawing = drawing;
            NewListName = string.Empty;
        }
        catch (Exception ex) { _dialogService.ShowError(ex.Message, "Inköp"); }
        finally { IsListsBusy = false; }
    }

    [RelayCommand]
    private async Task AddPurchaseArticleAsync()
    {
        var drawing = SelectedDrawing;
        if (IsListsBusy || drawing == null || _unreadableLists.Contains(drawing)) return;
        IsListsBusy = true;
        try
        {
            var row = new PurchaseArticle { IsManual = true, Category = "Övrigt", Type = "Ny artikel", Quantity = 1 };
            var rows = LoadListRows(drawing).Append(row).ToList();
            await SaveListAsync(drawing, rows);
            _listRows[drawing] = rows;
            SelectPurchaseList(drawing);
            SelectedArticle = row;
        }
        catch (Exception ex) { _dialogService.ShowError(ex.Message, "Inköp"); }
        finally { IsListsBusy = false; }
    }

    [RelayCommand]
    private async Task LinkListPdfAsync()
    {
        var drawing = SelectedDrawing;
        if (IsListsBusy || drawing == null || _unreadableLists.Contains(drawing)) return;
        var path = _dialogService.OpenFileDialog("PDF-filer (*.pdf)|*.pdf", "Koppla PDF till vald ritning/lista");
        if (string.IsNullOrWhiteSpace(path)) return;
        IsListsBusy = true;
        var previous = drawing.FilePath;
        try
        {
            drawing.FilePath = path;
            await SaveListAsync(drawing, LoadListRows(drawing));
            CollectionViewSource.GetDefaultView(Drawings).Refresh();
            if (SelectedDrawing == drawing) ShowPdf(path);
        }
        catch (Exception ex) { drawing.FilePath = previous; _dialogService.ShowError(ex.Message, "PDF"); }
        finally { IsListsBusy = false; }
    }

    [RelayCommand]
    private async Task ImportDrawingListsAsync()
    {
        if (IsListsBusy) return;
        var path = _dialogService.OpenFileDialog("Bluebeam XML (*.xml)|*.xml", "Importera ritningslistor från Bluebeam");
        if (string.IsNullOrWhiteSpace(path)) return;
        IsListsBusy = true;
        try
        {
            var project = Project;
            var result = await ParsePurchaseMarkupXmlAsync(path);
            if (!result.Success) throw new InvalidOperationException(result.ErrorMessage ?? "Importen misslyckades.");
            if (!ReferenceEquals(project, Project)) return;
            if (result.Items.Count == 0) throw new InvalidOperationException("XML-filen innehåller inga läsbara markeringar.");
            var plans = new List<(Drawing? Old, Drawing Saved, List<PurchaseArticle> Rows)>();
            var preview = new StringBuilder("Importförhandsgranskning\n\n");
            foreach (var group in result.Items.GroupBy(m => SheetKey(m.SheetLabel)))
            {
                var matches = Drawings.Where(d => string.Equals(SheetKey(d.DrawingNumber), group.Key, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(SheetKey(d.SheetTitle), group.Key, StringComparison.OrdinalIgnoreCase)).Distinct().ToArray();
                if (matches.Length > 1) throw new InvalidOperationException($"Flera listor matchar {group.Key}. Byt namn innan import.");
                var existing = matches.SingleOrDefault();
                if (existing != null && _unreadableLists.Contains(existing)) throw new InvalidOperationException($"Kan inte ersätta den oläsbara listan {group.Key}.");
                var imported = ConvertArticles(group.ToList());
                var oldRows = existing == null ? new List<PurchaseArticle>() : LoadListRows(existing);
                var importedOld = oldRows.Where(r => !r.IsManual).GroupBy(Key).ToDictionary(g => g.Key, g => g.ToArray());
                foreach (var row in imported)
                {
                    if (!importedOld.TryGetValue(Key(row), out var candidates)) continue;
                    if (candidates.Length != 1) throw new InvalidOperationException($"Dubbla gamla artiklar i {group.Key}; kan inte säkert bevara Produktkod/Leverantör.");
                    row.RowId = candidates[0].RowId;
                    row.ProductCode = candidates[0].ProductCode;
                    row.Supplier = candidates[0].Supplier;
                }
                var retainedManual = oldRows.Where(r => r.IsManual).ToList();
                imported.AddRange(retainedManual);
                var drawing = existing ?? new Drawing { ProjectId = project.Id, DrawingNumber = group.Key, SheetTitle = group.Key };
                var saved = Snapshot(drawing, imported);
                saved.PurchaseImportSource = Path.GetFileName(path);
                saved.Date = DateTime.Today;
                plans.Add((existing, saved, imported));
                preview.AppendLine($"{group.Key}: {(existing == null ? "ny lista" : "ersätt importerade rader")} – {oldRows.Count} → {imported.Count} rader ({retainedManual.Count} manuella behålls).");
                foreach (var row in imported.Where(r => !r.IsManual))
                    preview.AppendLine($"  {row.Category} | {row.Type} | {row.Dimension} | {row.Quantity.ToString("N2", CultureInfo.GetCultureInfo("sv-SE"))} {row.Unit}");
            }
            preview.AppendLine("\nImporterade mängder ersätts, inte adderas. Produktkod och Leverantör bevaras för exakt matchande kategori/typ/dimension/enhet. Manuella rader behålls. Importerade rader som saknas i nya filen tas bort. Övriga listor och PDF-länkar lämnas oförändrade.");
            if (!ConfirmImportPreview(preview.ToString())) return;
            await _listSaveLock.WaitAsync();
            try { await _dataService.Projects.SavePurchaseListsAsync(project.Id, plans.Select(p => p.Saved).ToList()); }
            finally { _listSaveLock.Release(); }
            if (!ReferenceEquals(project, Project)) return;
            foreach (var plan in plans)
            {
                if (plan.Old != null) { Drawings.Remove(plan.Old); _listRows.Remove(plan.Old); }
                Drawings.Add(plan.Saved);
                _listRows[plan.Saved] = plan.Rows;
            }
            SelectedDrawing = plans[0].Saved;
            ListStatus = $"Import sparad: {plans.Count} ritningslistor.";
        }
        catch (Exception ex) { _dialogService.ShowError(ex.Message, "Bluebeam-import"); }
        finally { IsListsBusy = false; }
    }

    private static bool ConfirmImportPreview(string text)
    {
        return ShowImportPreview(text);
    }

    [RelayCommand]
    private async Task ExportSelectedListAsync()
    {
        if (IsListsBusy || SelectedDrawing == null || ArticleView == null) return;
        var drawingName = SelectedDrawing.DrawingNumber;
        var rows = ArticleView.Cast<PurchaseArticle>().Select(row => new PurchaseArticle
        {
            Category = row.Category, Type = row.Type, Dimension = row.Dimension,
            Quantity = row.Quantity, Unit = row.Unit, ProductCode = row.ProductCode,
            Supplier = row.Supplier
        }).ToArray();
        var path = _dialogService.SaveFileDialog("xlsx", "Excel (*.xlsx)|*.xlsx|CSV (*.csv)|*.csv", "Inköpslista");
        if (string.IsNullOrWhiteSpace(path)) return;
        IsListsBusy = true;
        try
        {
            await Task.Run(() =>
            {
                string[] headers = ["Kategori", "Typ / Artikel", "Dimension", "Mängd", "Enhet", "Produktkod", "Leverantör", "Ritning / Lista"];
                if (Path.GetExtension(path).Equals(".csv", StringComparison.OrdinalIgnoreCase))
                {
                    static string Csv(string text)
                    {
                        if (text.Length > 0 && "=+-@\t\r".Contains(text[0])) text = "'" + text;
                        return "\"" + text.Replace("\"", "\"\"") + "\"";
                    }
                    var text = new StringBuilder();
                    text.AppendLine(string.Join(";", headers.Select(Csv)));
                    foreach (var row in rows)
                        text.AppendLine(string.Join(";", new[] { row.Category, row.Type, row.Dimension,
                            row.Quantity.ToString(CultureInfo.GetCultureInfo("sv-SE")), row.Unit,
                            row.ProductCode, row.Supplier, drawingName }.Select(Csv)));
                    File.WriteAllText(path, text.ToString(), new UTF8Encoding(true));
                }
                else if (Path.GetExtension(path).Equals(".xlsx", StringComparison.OrdinalIgnoreCase))
                {
                    using var workbook = new ClosedXML.Excel.XLWorkbook();
                    var sheet = workbook.AddWorksheet("Inköpslista");
                    for (var column = 0; column < headers.Length; column++) sheet.Cell(1, column + 1).Value = headers[column];
                    sheet.Row(1).Style.Font.Bold = true;
                    for (var index = 0; index < rows.Length; index++)
                    {
                        var row = rows[index];
                        var number = index + 2;
                        sheet.Cell(number, 1).Value = row.Category;
                        sheet.Cell(number, 2).Value = row.Type;
                        sheet.Cell(number, 3).Value = row.Dimension;
                        sheet.Cell(number, 4).Value = row.Quantity;
                        sheet.Cell(number, 5).Value = row.Unit;
                        sheet.Cell(number, 6).Value = row.ProductCode;
                        sheet.Cell(number, 7).Value = row.Supplier;
                        sheet.Cell(number, 8).Value = drawingName;
                    }
                    sheet.Columns().AdjustToContents();
                    workbook.SaveAs(path);
                }
                else throw new InvalidOperationException("Välj filtypen .xlsx eller .csv.");
            });
            ListStatus = $"Exporterade {rows.Length} synliga artikelrader.";
        }
        catch (Exception ex) { _dialogService.ShowError(ex.Message, "Export"); }
        finally { IsListsBusy = false; }
    }

    private static bool ShowImportPreview(string text)
    {
        var window = new System.Windows.Window
        {
            Title = "Förhandsgranska import",
            Width = Math.Min(1200, System.Windows.SystemParameters.WorkArea.Width),
            Height = Math.Min(800, System.Windows.SystemParameters.WorkArea.Height),
            ResizeMode = System.Windows.ResizeMode.CanResize,
            WindowStartupLocation = System.Windows.WindowStartupLocation.CenterOwner
        };
        if (System.Windows.Application.Current?.MainWindow is { } owner) window.Owner = owner;
        var panel = new System.Windows.Controls.DockPanel { Margin = new System.Windows.Thickness(12) };
        var buttons = new System.Windows.Controls.StackPanel { Orientation = System.Windows.Controls.Orientation.Horizontal, HorizontalAlignment = System.Windows.HorizontalAlignment.Right };
        var accept = new System.Windows.Controls.Button { Content = "Bekräfta import", Padding = new System.Windows.Thickness(12, 6, 12, 6), Margin = new System.Windows.Thickness(6) };
        var cancel = new System.Windows.Controls.Button { Content = "Avbryt", IsCancel = true, Margin = new System.Windows.Thickness(6) };
        accept.Click += (_, _) => window.DialogResult = true;
        buttons.Children.Add(cancel); buttons.Children.Add(accept);
        System.Windows.Controls.DockPanel.SetDock(buttons, System.Windows.Controls.Dock.Bottom);
        panel.Children.Add(buttons);
        panel.Children.Add(new System.Windows.Controls.TextBox
        {
            Text = text,
            IsReadOnly = true,
            AcceptsReturn = true,
            Height = double.NaN,
            MaxHeight = double.PositiveInfinity,
            Width = double.NaN,
            MaxWidth = double.PositiveInfinity,
            HorizontalAlignment = System.Windows.HorizontalAlignment.Stretch,
            VerticalAlignment = System.Windows.VerticalAlignment.Stretch,
            VerticalContentAlignment = System.Windows.VerticalAlignment.Top,
            HorizontalContentAlignment = System.Windows.HorizontalAlignment.Left,
            TextWrapping = System.Windows.TextWrapping.NoWrap,
            VerticalScrollBarVisibility = System.Windows.Controls.ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = System.Windows.Controls.ScrollBarVisibility.Auto,
            FontFamily = new System.Windows.Media.FontFamily("Consolas"),
            FontSize = 13,
            Padding = new System.Windows.Thickness(10)
        });
        window.Content = panel;
        return window.ShowDialog() == true;
    }
}
