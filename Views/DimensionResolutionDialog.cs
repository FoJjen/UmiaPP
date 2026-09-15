using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using UmiaPP.Models;

namespace UmiaPP.Views;

/// <summary>Edits drafts so canceling never changes the imported markup items.</summary>
public sealed class DimensionResolutionDialog : Window
{
    private readonly List<DimensionDraft> _drafts;
    private readonly DataGrid _grid;

    public DimensionResolutionDialog(IReadOnlyList<BluebeamMarkupItem> items)
    {
        Title = "Ange ospecificerade dimensioner";
        Width = 1100;
        Height = 520;
        MinWidth = 650;
        MinHeight = 350;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        _drafts = items.Select(item => new DimensionDraft(item)).ToList();

        var panel = new DockPanel { Margin = new Thickness(16) };
        var instructions = new TextBlock
        {
            Text = "Dimension saknas för följande artiklar. Originaluppgifterna visas nedan. " +
                "Ange dimension, till exempel 160 eller 400x200, i sista kolumnen. " +
                "Tomma fält lämnas ospecificerade. Avbryt fortsätter importen utan dessa ändringar.",
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, 12)
        };
        DockPanel.SetDock(instructions, Dock.Top);
        panel.Children.Add(instructions);

        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, 12, 0, 0)
        };
        var apply = new Button
        {
            Content = "Använd angivna dimensioner", IsDefault = true,
            Padding = new Thickness(12, 6, 12, 6), Margin = new Thickness(0, 0, 8, 0)
        };
        apply.Click += Apply_Click;
        buttons.Children.Add(apply);
        buttons.Children.Add(new Button
        {
            Content = "Avbryt", IsCancel = true, Padding = new Thickness(12, 6, 12, 6)
        });
        DockPanel.SetDock(buttons, Dock.Bottom);
        panel.Children.Add(buttons);

        _grid = new DataGrid
        {
            ItemsSource = _drafts,
            AutoGenerateColumns = false,
            CanUserAddRows = false,
            CanUserDeleteRows = false,
            ColumnWidth = DataGridLength.Auto,
            SelectionUnit = DataGridSelectionUnit.Cell
        };
        AddColumn("Ritning", "Source.SheetLabel");
        AddColumn("Ämne (original)", "Source.Subject");
        AddColumn("Lager (original)", "Source.Layer");
        AddColumn("Kommentarer", "Source.Comments");
        AddColumn("Kategori", "Source.Category");
        AddColumn("Längd (m)", "Source.LengthMeters");
        AddColumn("Vertikalhöjd (m)", "Source.VerticalHeightMeters");
        AddColumn("Antal", "Source.Count");
        _grid.Columns.Add(new DataGridTextColumn
        {
            Header = "Ange dimension", MinWidth = 150,
            Binding = new Binding(nameof(DimensionDraft.Dimension))
            {
                Mode = BindingMode.TwoWay,
                UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged
            }
        });
        panel.Children.Add(_grid);
        Content = panel;
    }

    private void AddColumn(string header, string path) => _grid.Columns.Add(new DataGridTextColumn
    {
        Header = header, IsReadOnly = true,
        Binding = new Binding(path) { Mode = BindingMode.OneWay }
    });

    private void Apply_Click(object sender, RoutedEventArgs e)
    {
        if (!_grid.CommitEdit(DataGridEditingUnit.Cell, true) ||
            !_grid.CommitEdit(DataGridEditingUnit.Row, true)) return;

        foreach (var draft in _drafts)
        {
            // The summary formatter adds the diameter symbol itself.
            var dimension = draft.Dimension.Trim().TrimStart('Ø', 'ø').Trim();
            if (!string.IsNullOrWhiteSpace(dimension))
                draft.Source.Dimension = dimension;
        }
        DialogResult = true;
    }

    public sealed class DimensionDraft
    {
        public DimensionDraft(BluebeamMarkupItem source) => Source = source;
        public BluebeamMarkupItem Source { get; }
        public string Dimension { get; set; } = string.Empty;
    }
}
