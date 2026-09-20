using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

namespace UmiaPP.Views;

/// <summary>Column visibility preferences, independent of display order and row data.</summary>
public static class DataGridColumnVisibility
{
    public static readonly DependencyProperty ColumnKeyProperty = DependencyProperty.RegisterAttached(
        "ColumnKey", typeof(string), typeof(DataGridColumnVisibility), new PropertyMetadata(null));

    public static string? GetColumnKey(DependencyObject element) => (string?)element.GetValue(ColumnKeyProperty);
    public static void SetColumnKey(DependencyObject element, string value) => element.SetValue(ColumnKeyProperty, value);

    public static IReadOnlyDictionary<DataGridColumn, string> GetColumnKeys(DataGrid grid)
    {
        var keys = new Dictionary<DataGridColumn, string>();
        var occurrences = new Dictionary<string, int>(StringComparer.Ordinal);
        // Columns stays in declaration order when DisplayIndex is changed.
        for (var index = 0; index < grid.Columns.Count; index++)
        {
            var column = grid.Columns[index];
            var key = GetBaseKey(column, index);
            occurrences.TryGetValue(key, out var occurrence);
            occurrences[key] = occurrence + 1;
            keys[column] = occurrence == 0 ? key : $"{key}#{occurrence + 1}";
        }
        return keys;
    }

    public static void Apply(DataGrid grid, IEnumerable<string>? hiddenKeys)
    {
        var hidden = new HashSet<string>(hiddenKeys ?? Array.Empty<string>(), StringComparer.Ordinal);
        foreach (var pair in GetColumnKeys(grid))
            pair.Key.SetCurrentValue(DataGridColumn.VisibilityProperty,
                hidden.Contains(pair.Value) ? Visibility.Collapsed : Visibility.Visible);

        // Recover safely from obsolete or manually edited all-hidden settings.
        if (grid.Columns.Count > 0 && grid.Columns.All(column => column.Visibility != Visibility.Visible))
            grid.Columns[0].SetCurrentValue(DataGridColumn.VisibilityProperty, Visibility.Visible);
    }

    public static bool SetVisible(DataGrid grid, DataGridColumn column, bool visible)
    {
        if (!grid.Columns.Contains(column)) return false;
        if (!visible && column.Visibility == Visibility.Visible &&
            grid.Columns.Count(item => item.Visibility == Visibility.Visible) <= 1)
            return false;
        column.SetCurrentValue(DataGridColumn.VisibilityProperty,
            visible ? Visibility.Visible : Visibility.Collapsed);
        return true;
    }

    public static List<string> GetHiddenKeys(DataGrid grid)
        => GetColumnKeys(grid).Where(pair => pair.Key.Visibility != Visibility.Visible)
            .Select(pair => pair.Value).OrderBy(key => key, StringComparer.Ordinal).ToList();

    public static void ShowAll(DataGrid grid)
    {
        foreach (var column in grid.Columns)
            column.SetCurrentValue(DataGridColumn.VisibilityProperty, Visibility.Visible);
    }

    private static string GetBaseKey(DataGridColumn column, int index)
    {
        var explicitKey = GetColumnKey(column);
        if (!string.IsNullOrWhiteSpace(explicitKey)) return "id:" + explicitKey;
        if (column is DataGridBoundColumn bound && BindingKey(bound.Binding) is { Length: > 0 } bindingKey)
            return "binding:" + bindingKey;
        if (!string.IsNullOrWhiteSpace(column.SortMemberPath)) return "path:" + column.SortMemberPath;
        if (column.Header is string header && !string.IsNullOrWhiteSpace(header)) return "header:" + header.Trim();
        return "column:" + index.ToString(System.Globalization.CultureInfo.InvariantCulture);
    }

    private static string? BindingKey(BindingBase? binding)
    {
        if (binding is Binding single) return single.Path?.Path;
        if (binding is MultiBinding multi)
        {
            var parts = multi.Bindings.Select(BindingKey).ToArray();
            if (parts.Length > 0 && parts.All(part => !string.IsNullOrWhiteSpace(part)))
                return string.Join("|", parts);
        }
        return null;
    }
}
