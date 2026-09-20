using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Markup;
using UmiaPP.Models;
using UmiaPP.ViewModels;

namespace UmiaPP.Views;

public sealed partial class PackageQuoteDetailsDialog
{
    public PackageQuoteDetailsDialog(PackageQuoteEditorViewModel editor)
    {
        DataContext = editor;
        SetBinding(TitleProperty, new Binding(nameof(editor.DialogTitle)) { Mode = BindingMode.OneWay });
        Width = 860;
        Height = 780;
        MinWidth = 640;
        MinHeight = 520;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Language = XmlLanguage.GetLanguage("sv-SE");
        FontFamily = new System.Windows.Media.FontFamily("Segoe UI Variable, Segoe UI");
        FontSize = 13;
        SetResourceReference(BackgroundProperty, "BackgroundBrush");
        SetResourceReference(ForegroundProperty, "TextPrimaryBrush");

        var root = new Grid { Margin = new Thickness(24) };
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        Content = root;
        var delete = new Button
        {
            Content = "Ta bort offert",
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 16, 16, 0),
            ToolTip = "Ta bort offerten och koppla loss artiklarna. Artiklarna behålls."
        };
        delete.SetResourceReference(StyleProperty, "DangerButton");
        delete.SetBinding(Button.CommandProperty, new Binding(nameof(editor.DeleteCommand)));
        delete.SetBinding(VisibilityProperty, new Binding(nameof(editor.SupportsDeletion))
        {
            Converter = new BooleanToVisibilityConverter()
        });
        System.Windows.Automation.AutomationProperties.SetName(delete, "Ta bort offert");
        Grid.SetRow(delete, 1);
        root.Children.Add(delete);
        var body = new StackPanel();
        root.Children.Add(new ScrollViewer
        {
            Content = body,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled
        });
        var heading = new TextBlock
        {
            FontSize = 22,
            FontWeight = FontWeights.SemiBold,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, 16)
        };
        heading.SetBinding(TextBlock.TextProperty, new Binding(nameof(editor.DialogTitle)) { Mode = BindingMode.OneWay });
        body.Children.Add(heading);

        var form = new StackPanel();
        var formStyle = new Style(typeof(StackPanel));
        var savingTrigger = new DataTrigger { Binding = new Binding(nameof(editor.IsSaving)), Value = true };
        savingTrigger.Setters.Add(new Setter(IsEnabledProperty, false));
        formStyle.Triggers.Add(savingTrigger);
        form.Style = formStyle;
        body.Children.Add(form);
        AddEditorField(form, "Leverantör", QuoteEditorInput(nameof(editor.Supplier)));
        AddEditorField(form, "Offertnummer / namn", QuoteEditorInput(nameof(editor.QuoteNumber)));
        var date = new DatePicker { HorizontalAlignment = HorizontalAlignment.Left, Width = 200 };
        date.SetBinding(DatePicker.SelectedDateProperty, new Binding(nameof(editor.QuoteDate))
        {
            Mode = BindingMode.TwoWay,
            UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged
        });
        date.DateValidationError += (_, args) =>
        {
            args.ThrowException = false;
            editor.QuoteDate = null;
            editor.ErrorMessage = "Ange ett giltigt offertdatum.";
        };
        AddEditorField(form, "Datum", date);
        AddEditorField(form, $"Offererat belopp ({editor.Currency})", QuoteEditorInput(nameof(editor.QuotedAmountInput), true));
        AddEditorField(form, $"Slutpris ({editor.Currency})", QuoteEditorInput(nameof(editor.FinalAmountInput), true));
        var finalHelp = new TextBlock
        {
            Text = "Lämna slutpris tomt eller ange 0 för att använda offertpriset.",
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(160, 0, 0, 10),
            FontSize = 12
        };
        finalHelp.SetResourceReference(TextBlock.ForegroundProperty, "TextSecondaryBrush");
        form.Children.Add(finalHelp);

        var fileField = new DockPanel();
        var browse = new Button { Content = "Välj fil…", Margin = new Thickness(8, 0, 0, 0) };
        browse.SetResourceReference(StyleProperty, "SecondaryButton");
        browse.SetBinding(Button.CommandProperty, new Binding(nameof(editor.BrowseCommand)));
        DockPanel.SetDock(browse, Dock.Right);
        fileField.Children.Add(browse);
        fileField.Children.Add(QuoteEditorInput(nameof(editor.FilePath)));
        AddEditorField(form, "Offertfil", fileField);
        var comment = QuoteEditorInput(nameof(editor.Comment));
        comment.AcceptsReturn = true;
        comment.TextWrapping = TextWrapping.Wrap;
        comment.MinHeight = 60;
        comment.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
        AddEditorField(form, "Kommentar", comment);

        var comparison = new StackPanel();
        var comparisonCard = new Border { Child = comparison, Margin = new Thickness(0, 12, 0, 16), Padding = new Thickness(14) };
        comparisonCard.SetResourceReference(StyleProperty, "ModernCard");
        body.Children.Add(comparisonCard);
        AddEditorField(comparison, "Kalkylerat Pris NTO", QuoteAmountText(nameof(editor.CalculatedNetAmount), editor.Currency));
        var difference = QuoteAmountText(nameof(editor.QuoteDifference), editor.Currency);
        var differenceStyle = new Style(typeof(TextBlock));
        differenceStyle.Setters.Add(new Setter(TextBlock.ForegroundProperty, new DynamicResourceExtension("TextPrimaryBrush")));
        var cheaper = new DataTrigger { Binding = new Binding(nameof(editor.IsQuoteCheaper)), Value = true };
        cheaper.Setters.Add(new Setter(TextBlock.ForegroundProperty, new DynamicResourceExtension("SuccessBrush")));
        differenceStyle.Triggers.Add(cheaper);
        var dearer = new DataTrigger { Binding = new Binding(nameof(editor.IsQuoteMoreExpensive)), Value = true };
        dearer.Setters.Add(new Setter(TextBlock.ForegroundProperty, new DynamicResourceExtension("DangerBrush")));
        differenceStyle.Triggers.Add(dearer);
        difference.Style = differenceStyle;
        AddEditorField(comparison, "Offert − kalkyl", difference);
        var explanation = new TextBlock { TextWrapping = TextWrapping.Wrap };
        explanation.SetBinding(TextBlock.TextProperty,
            new Binding(nameof(editor.ComparisonDescription)) { Mode = BindingMode.OneWay });
        comparison.Children.Add(explanation);

        body.Children.Add(new TextBlock
        {
            Text = $"Kopplade artiklar ({editor.Articles.Count})",
            FontSize = 16,
            FontWeight = FontWeights.SemiBold,
            Margin = new Thickness(0, 0, 0, 8)
        });
        var articles = new DataGrid
        {
            ItemsSource = editor.Articles,
            AutoGenerateColumns = false,
            IsReadOnly = true,
            CanUserAddRows = false,
            CanUserDeleteRows = false,
            Height = 220
        };
        articles.Columns.Add(new DataGridTextColumn
        {
            Header = "Beteckning", Binding = new Binding(nameof(LubeSakvaraMangd.Beteckning)),
            Width = new DataGridLength(1, DataGridLengthUnitType.Star)
        });
        articles.Columns.Add(new DataGridTextColumn
        {
            Header = "Produkt", Binding = new Binding(nameof(LubeSakvaraMangd.Produkt)),
            Width = new DataGridLength(1, DataGridLengthUnitType.Star)
        });
        articles.Columns.Add(new DataGridTextColumn
        {
            Header = "Antal", Binding = QuoteNumberBinding(nameof(LubeSakvaraMangd.Antal)),
            ElementStyle = TryFindResource("NumericCellText") as Style, Width = 75
        });
        articles.Columns.Add(new DataGridTextColumn
        {
            Header = "Pris NTO", Binding = QuoteNumberBinding(nameof(LubeSakvaraMangd.PrisNto)),
            ElementStyle = TryFindResource("NumericCellText") as Style, Width = 125
        });
        body.Children.Add(articles);

        var footer = new StackPanel { Margin = new Thickness(0, 12, 0, 0) };
        Grid.SetRow(footer, 1);
        root.Children.Add(footer);
        var error = new TextBlock { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 8) };
        error.SetResourceReference(TextBlock.ForegroundProperty, "DangerBrush");
        error.SetBinding(TextBlock.TextProperty, new Binding(nameof(editor.ErrorMessage)));
        footer.Children.Add(error);
        var busy = new TextBlock { Text = "Sparar offert…", Margin = new Thickness(0, 0, 0, 8) };
        busy.SetBinding(VisibilityProperty, new Binding(nameof(editor.IsSaving)) { Converter = new BooleanToVisibilityConverter() });
        footer.Children.Add(busy);
        var actions = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        var cancel = new Button { Content = "Avbryt", IsCancel = true, MinWidth = 100, Margin = new Thickness(0, 0, 8, 0) };
        cancel.SetResourceReference(StyleProperty, "SecondaryButton");
        cancel.SetBinding(Button.CommandProperty, new Binding(nameof(editor.CancelCommand)));
        var save = new Button { Content = "Spara offert", IsDefault = true, MinWidth = 120 };
        save.SetResourceReference(StyleProperty, "PrimaryButton");
        save.SetBinding(Button.CommandProperty, new Binding(nameof(editor.SaveCommand)));
        actions.Children.Add(cancel);
        actions.Children.Add(save);
        footer.Children.Add(actions);

        editor.BrowseFileAction = () =>
        {
            var picker = new Microsoft.Win32.OpenFileDialog
            {
                Title = "Välj offertfil", Filter = "PDF-filer (*.pdf)|*.pdf|Alla filer (*.*)|*.*"
            };
            if (picker.ShowDialog(this) == true) editor.FilePath = picker.FileName;
        };
        editor.CloseAction = () => DialogResult = editor.IsSaved;
        Closing += (_, args) => { if (editor.IsSaving) args.Cancel = true; };
        Closed += (_, _) => editor.Dispose();
    }

    private static Binding QuoteNumberBinding(string path) => new(path)
    {
        Mode = BindingMode.OneWay,
        StringFormat = "N2",
        ConverterCulture = CultureInfo.GetCultureInfo("sv-SE")
    };

    private static TextBlock QuoteAmountText(string path, string currency)
    {
        var text = new TextBlock { FontWeight = FontWeights.SemiBold, TextAlignment = TextAlignment.Right };
        var binding = QuoteNumberBinding(path);
        binding.StringFormat = "{0:N2} " + currency;
        binding.TargetNullValue = "—";
        text.SetBinding(TextBlock.TextProperty, binding);
        return text;
    }

    private static TextBox QuoteEditorInput(string path, bool amount = false)
    {
        var input = new TextBox
        {
            MinHeight = 34,
            HorizontalContentAlignment = amount ? HorizontalAlignment.Right : HorizontalAlignment.Left
        };
        input.SetBinding(TextBox.TextProperty, new Binding(path)
        {
            Mode = BindingMode.TwoWay,
            UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged
        });
        return input;
    }

    private static void AddEditorField(Panel parent, string caption, FrameworkElement input)
    {
        var row = new Grid { Margin = new Thickness(0, 0, 0, 8) };
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(160) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        var label = new TextBlock { Text = caption, TextWrapping = TextWrapping.Wrap, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 12, 0) };
        label.SetResourceReference(TextBlock.ForegroundProperty, "TextSecondaryBrush");
        Grid.SetColumn(input, 1);
        row.Children.Add(label);
        row.Children.Add(input);
        parent.Children.Add(row);
    }
}
