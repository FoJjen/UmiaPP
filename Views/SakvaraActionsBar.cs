using System.Collections;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using UmiaPP.ViewModels;

namespace UmiaPP.Views;

/// <summary>Keeps specification actions in one horizontal, non-overlapping container.</summary>
public sealed class SakvaraActionsBar : StackPanel
{
    public static readonly DependencyProperty SelectedArticlesProperty = DependencyProperty.Register(
        nameof(SelectedArticles), typeof(IList), typeof(SakvaraActionsBar), new PropertyMetadata(null));

    public static readonly DependencyProperty TargetGridProperty = DependencyProperty.Register(
        nameof(TargetGrid), typeof(DataGrid), typeof(SakvaraActionsBar), new PropertyMetadata(null));

    public DataGrid? TargetGrid
    {
        get => (DataGrid?)GetValue(TargetGridProperty);
        set => SetValue(TargetGridProperty, value);
    }

    public IList? SelectedArticles
    {
        get => (IList?)GetValue(SelectedArticlesProperty);
        set => SetValue(SelectedArticlesProperty, value);
    }

    public SakvaraActionsBar()
    {
        Orientation = Orientation.Horizontal;
        HorizontalAlignment = HorizontalAlignment.Left;
        Margin = new Thickness(0, 0, 0, 8);

        var addRow = new Button
        {
            Content = "Ny rad",
            MinWidth = 100,
            VerticalAlignment = VerticalAlignment.Center
        };
        addRow.SetResourceReference(FrameworkElement.StyleProperty, "PrimaryButton");
        addRow.SetBinding(Button.CommandProperty,
            new Binding(nameof(LubeCalculationTabViewModel.AddSakvaraRowCommand)));

        var linkQuote = new Button
        {
            Content = "Koppla till offert",
            MinWidth = 150,
            Margin = new Thickness(8, 0, 0, 0),
            VerticalAlignment = VerticalAlignment.Center,
            ToolTip = "Markera artiklar med Ctrl eller Shift och koppla dem till en ny offert"
        };
        linkQuote.SetResourceReference(FrameworkElement.StyleProperty, "SecondaryButton");
        linkQuote.SetBinding(Button.CommandProperty,
            new Binding(nameof(LubeCalculationTabViewModel.LinkSelectedToQuoteCommand)));
        linkQuote.SetBinding(Button.CommandParameterProperty,
            new Binding(nameof(SelectedArticles)) { Source = this, Mode = BindingMode.OneWay });

        var columns = new LubeColumnPickerButton
        {
            Margin = new Thickness(8, 0, 0, 0),
            VerticalAlignment = VerticalAlignment.Center
        };
        columns.SetBinding(LubeColumnPickerButton.TargetGridProperty,
            new Binding(nameof(TargetGrid)) { Source = this, Mode = BindingMode.OneWay });

        Children.Add(addRow);
        Children.Add(linkQuote);
        Children.Add(columns);
    }
}
