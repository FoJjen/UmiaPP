using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using UmiaPP.Services;

namespace UmiaPP.Views;

/// <summary>Remembers specification column visibility for the current application user.</summary>
public sealed class LubeColumnPickerButton : Button
{
    public static readonly DependencyProperty TargetGridProperty = DependencyProperty.Register(
        nameof(TargetGrid), typeof(DataGrid), typeof(LubeColumnPickerButton),
        new PropertyMetadata(null, OnTargetGridChanged));

    private readonly ILocalizationService _settings;
    private DataGrid? _observedGrid;
    private ContextMenu? _menu;

    public DataGrid? TargetGrid
    {
        get => (DataGrid?)GetValue(TargetGridProperty);
        set => SetValue(TargetGridProperty, value);
    }

    public LubeColumnPickerButton() : this(new LocalizationService()) { }

    public LubeColumnPickerButton(ILocalizationService settings)
    {
        _settings = settings;
        MinWidth = 100;
        SetResourceReference(StyleProperty, "SecondaryButton");
        SetResourceReference(ContentProperty, "ChooseColumns");
        SetResourceReference(ToolTipProperty, "ChooseColumnsTooltip");
        SetResourceReference(System.Windows.Automation.AutomationProperties.NameProperty, "ChooseColumnsTooltip");
        Loaded += (_, _) => AttachGrid();
        Unloaded += (_, _) => DetachGrid();
        IsEnabled = false;
    }

    private static void OnTargetGridChanged(DependencyObject owner, DependencyPropertyChangedEventArgs args)
    {
        var button = (LubeColumnPickerButton)owner;
        if (button.IsLoaded) button.AttachGrid();
    }

    private void AttachGrid()
    {
        DetachGrid();
        _observedGrid = TargetGrid;
        if (_observedGrid != null)
        {
            _observedGrid.Columns.CollectionChanged += OnColumnsChanged;
            LoadPreferences();
        }
        IsEnabled = _observedGrid?.Columns.Count > 0;
    }

    private void DetachGrid()
    {
        if (_menu != null) _menu.IsOpen = false;
        if (_observedGrid != null) _observedGrid.Columns.CollectionChanged -= OnColumnsChanged;
        _observedGrid = null;
        IsEnabled = false;
    }

    private void OnColumnsChanged(object? sender, NotifyCollectionChangedEventArgs args)
    {
        if (_menu != null) _menu.IsOpen = false;
        LoadPreferences();
        IsEnabled = _observedGrid?.Columns.Count > 0;
    }

    private void LoadPreferences()
    {
        if (_observedGrid != null)
            DataGridColumnVisibility.Apply(_observedGrid, _settings.LoadSettings().HiddenLubeSpecificationColumns);
    }

    private void SavePreferences(DataGrid grid)
    {
        // Reload first so changing columns never overwrites language, theme or tab choices.
        var settings = _settings.LoadSettings();
        settings.HiddenLubeSpecificationColumns = DataGridColumnVisibility.GetHiddenKeys(grid);
        _settings.SaveSettings(settings);
    }

    protected override void OnClick()
    {
        base.OnClick();
        if (TargetGrid is not { } grid || grid.Columns.Count == 0) return;
        if (_menu != null) _menu.IsOpen = false;

        var menu = new ContextMenu
        {
            PlacementTarget = this,
            Placement = PlacementMode.Bottom,
            StaysOpen = false,
            MinWidth = 220,
            MaxHeight = 420,
            BorderThickness = new Thickness(1)
        };
        menu.SetResourceReference(BackgroundProperty, "SurfaceBrush");
        menu.SetResourceReference(ForegroundProperty, "TextPrimaryBrush");
        menu.SetResourceReference(BorderBrushProperty, "BorderBrush");
        ScrollViewer.SetVerticalScrollBarVisibility(menu, ScrollBarVisibility.Auto);

        var showAll = new MenuItem { StaysOpenOnClick = true };
        showAll.SetResourceReference(MenuItem.HeaderProperty, "ShowAllColumns");
        menu.Items.Add(showAll);
        menu.Items.Add(new Separator());
        var choices = new Dictionary<MenuItem, DataGridColumn>();

        void RefreshChecks()
        {
            var visibleCount = grid.Columns.Count(column => column.Visibility == Visibility.Visible);
            showAll.IsEnabled = visibleCount < grid.Columns.Count;
            foreach (var pair in choices)
            {
                pair.Key.IsChecked = pair.Value.Visibility == Visibility.Visible;
                pair.Key.IsEnabled = !pair.Key.IsChecked || visibleCount > 1;
                pair.Key.ToolTip = pair.Key.IsEnabled ? null : _settings.GetString("KeepOneColumnVisible");
            }
        }

        foreach (var column in grid.Columns.OrderBy(column => column.DisplayIndex))
        {
            var caption = column.Header as string ?? (column.Header as TextBlock)?.Text;
            if (string.IsNullOrWhiteSpace(caption))
                caption = string.Format(CultureInfo.CurrentUICulture, _settings.GetString("ColumnLabelFormat"), column.DisplayIndex + 1);
            var choice = new MenuItem { Header = caption, IsCheckable = true, StaysOpenOnClick = true };
            choices.Add(choice, column);
            choice.Click += (_, _) =>
            {
                if (DataGridColumnVisibility.SetVisible(grid, column, choice.IsChecked))
                    SavePreferences(grid);
                RefreshChecks();
            };
            menu.Items.Add(choice);
        }
        showAll.Click += (_, _) =>
        {
            DataGridColumnVisibility.ShowAll(grid);
            SavePreferences(grid);
            RefreshChecks();
        };
        RefreshChecks();
        _menu = menu;
        menu.Closed += (_, _) => { if (ReferenceEquals(_menu, menu)) _menu = null; };
        menu.IsOpen = true;
    }
}
