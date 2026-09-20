using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;

namespace UmiaPP.Views;

/// <summary>Fixed totals aligned with the specification grid's displayed columns.</summary>
public sealed class SakvarorTotalsFooter : FrameworkElement
{
    public static readonly DependencyProperty TargetGridProperty = DependencyProperty.Register(
        nameof(TargetGrid), typeof(DataGrid), typeof(SakvarorTotalsFooter),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender, OnTargetChanged));
    public static readonly DependencyProperty PrisNtoProperty = TotalProperty(nameof(PrisNto));
    public static readonly DependencyProperty OffertprisProperty = TotalProperty(nameof(Offertpris));
    public static readonly DependencyProperty SlutprisProperty = TotalProperty(nameof(Slutpris));
    public static readonly DependencyProperty DifferensProperty = TotalProperty(nameof(Differens));
    public static readonly DependencyProperty SurfaceBrushProperty = BrushProperty(nameof(SurfaceBrush), Brushes.White);
    public static readonly DependencyProperty TextBrushProperty = BrushProperty(nameof(TextBrush), Brushes.Black);
    public static readonly DependencyProperty OutlineBrushProperty = BrushProperty(nameof(OutlineBrush), Brushes.LightGray);
    public static readonly DependencyProperty FontSizeProperty = System.Windows.Documents.TextElement.FontSizeProperty.AddOwner(
        typeof(SakvarorTotalsFooter), new FrameworkPropertyMetadata(13d, FrameworkPropertyMetadataOptions.AffectsRender));
    public static readonly DependencyProperty FontFamilyProperty = System.Windows.Documents.TextElement.FontFamilyProperty.AddOwner(
        typeof(SakvarorTotalsFooter), new FrameworkPropertyMetadata(new FontFamily("Segoe UI Variable, Segoe UI"), FrameworkPropertyMetadataOptions.AffectsRender));
    public static readonly DependencyProperty FontWeightProperty = System.Windows.Documents.TextElement.FontWeightProperty.AddOwner(
        typeof(SakvarorTotalsFooter), new FrameworkPropertyMetadata(FontWeights.SemiBold, FrameworkPropertyMetadataOptions.AffectsRender));

    public DataGrid? TargetGrid { get => (DataGrid?)GetValue(TargetGridProperty); set => SetValue(TargetGridProperty, value); }
    public double PrisNto { get => (double)GetValue(PrisNtoProperty); set => SetValue(PrisNtoProperty, value); }
    public double Offertpris { get => (double)GetValue(OffertprisProperty); set => SetValue(OffertprisProperty, value); }
    public double Slutpris { get => (double)GetValue(SlutprisProperty); set => SetValue(SlutprisProperty, value); }
    public double Differens { get => (double)GetValue(DifferensProperty); set => SetValue(DifferensProperty, value); }
    public Brush SurfaceBrush { get => (Brush)GetValue(SurfaceBrushProperty); set => SetValue(SurfaceBrushProperty, value); }
    public Brush TextBrush { get => (Brush)GetValue(TextBrushProperty); set => SetValue(TextBrushProperty, value); }
    public Brush OutlineBrush { get => (Brush)GetValue(OutlineBrushProperty); set => SetValue(OutlineBrushProperty, value); }
    public double FontSize { get => (double)GetValue(FontSizeProperty); set => SetValue(FontSizeProperty, value); }
    public FontFamily FontFamily { get => (FontFamily)GetValue(FontFamilyProperty); set => SetValue(FontFamilyProperty, value); }
    public FontWeight FontWeight { get => (FontWeight)GetValue(FontWeightProperty); set => SetValue(FontWeightProperty, value); }

    private DataGrid? _observedGrid;
    private Rect[] _lastBounds = [];
    private string[] _lastPaths = [];

    public SakvarorTotalsFooter()
    {
        Height = 38;
        SetResourceReference(SurfaceBrushProperty, "SurfaceBrush");
        SetResourceReference(TextBrushProperty, "TextPrimaryBrush");
        SetResourceReference(OutlineBrushProperty, "BorderBrush");
        ClipToBounds = true;
        ToolTip = "Summa för alla projektrader, oavsett filter";
        Loaded += (_, _) => ObserveGrid();
        Unloaded += (_, _) => StopObserving();
    }

    private static DependencyProperty TotalProperty(string name) => DependencyProperty.Register(
        name, typeof(double), typeof(SakvarorTotalsFooter),
        new FrameworkPropertyMetadata(0d, FrameworkPropertyMetadataOptions.AffectsRender));

    private static DependencyProperty BrushProperty(string name, Brush fallback) => DependencyProperty.Register(
        name, typeof(Brush), typeof(SakvarorTotalsFooter),
        new FrameworkPropertyMetadata(fallback, FrameworkPropertyMetadataOptions.AffectsRender));

    private static void OnTargetChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args)
    {
        var footer = (SakvarorTotalsFooter)sender;
        footer.StopObserving();
        if (footer.IsLoaded) footer.ObserveGrid();
    }

    private void ObserveGrid()
    {
        StopObserving();
        _observedGrid = TargetGrid;
        if (_observedGrid != null) _observedGrid.LayoutUpdated += OnGridLayoutUpdated;
        InvalidateVisual();
    }

    private void StopObserving()
    {
        if (_observedGrid != null) _observedGrid.LayoutUpdated -= OnGridLayoutUpdated;
        _observedGrid = null;
    }

    private void OnGridLayoutUpdated(object? sender, EventArgs args)
    {
        var cells = GetCells();
        var bounds = cells.Select(c => c.Bounds).ToArray();
        var paths = cells.Select(c => c.Path).ToArray();
        if (_lastBounds.SequenceEqual(bounds) && _lastPaths.SequenceEqual(paths)) return;
        _lastBounds = bounds;
        _lastPaths = paths;
        InvalidateVisual();
    }

    private List<(Rect Bounds, string Path)> GetCells()
    {
        var cells = new List<(Rect, string)>();
        var grid = TargetGrid;
        if (grid == null || !grid.IsLoaded) return cells;
        // Header positions already include resizing, reordering, frozen columns,
        // row headers and horizontal scrolling. No duplicate width calculations.
        foreach (var header in Descendants<System.Windows.Controls.Primitives.DataGridColumnHeader>(grid))
        {
            var column = header.Column;
            if (column == null || column.Visibility != Visibility.Visible || header.ActualWidth <= 0) continue;
            var path = (column as DataGridBoundColumn)?.Binding is Binding binding
                ? binding.Path?.Path ?? string.Empty : column.SortMemberPath;
            if (path is not ("PrisNto" or "Offertpris" or "Slutpris" or "Differens")) continue;
            if (PresentationSource.FromVisual(header) != PresentationSource.FromVisual(this)) continue;
            var x = header.TranslatePoint(new Point(0, 0), this).X;
            cells.Add((new Rect(x, 0, header.ActualWidth, ActualHeight), path));
        }
        return cells;
    }

    private static IEnumerable<T> Descendants<T>(DependencyObject parent) where T : DependencyObject
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is T match) yield return match;
            foreach (var nested in Descendants<T>(child)) yield return nested;
        }
    }

    protected override void OnRender(DrawingContext drawingContext)
    {
        base.OnRender(drawingContext);
        var outline = new Pen(OutlineBrush, 1);
        drawingContext.DrawRectangle(SurfaceBrush, null, new Rect(0, 0, ActualWidth, ActualHeight));
        foreach (var cell in GetCells())
        {
            var value = cell.Path switch
            {
                "PrisNto" => PrisNto,
                "Offertpris" => Offertpris,
                "Slutpris" => Slutpris,
                _ => Differens
            };
            var text = new FormattedText(value.ToString("N2", CultureInfo.GetCultureInfo("sv-SE")),
                CultureInfo.GetCultureInfo("sv-SE"), FlowDirection.LeftToRight,
                new Typeface(FontFamily, FontStyles.Normal, FontWeight, FontStretches.Normal),
                FontSize, TextBrush, VisualTreeHelper.GetDpi(this).PixelsPerDip);
            drawingContext.PushClip(new RectangleGeometry(cell.Bounds));
            drawingContext.DrawRectangle(SurfaceBrush, null, cell.Bounds);
            drawingContext.DrawText(text, new Point(cell.Bounds.Right - text.Width - 6,
                Math.Max(0, (ActualHeight - text.Height) / 2)));
            drawingContext.Pop();
        }
        drawingContext.DrawLine(outline, new Point(0, 0), new Point(ActualWidth, 0));
        drawingContext.DrawLine(outline, new Point(0, ActualHeight), new Point(ActualWidth, ActualHeight));
    }
}
