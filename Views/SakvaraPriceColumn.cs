using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using UmiaPP.Models;

namespace UmiaPP.Views;

/// <summary>Displays an effective price but edits only the entered price.</summary>
public sealed class SakvaraPriceColumn : DataGridTextColumn
{
    public bool IsFinalPrice { get; set; }

    protected override FrameworkElement GenerateElement(DataGridCell cell, object dataItem)
    {
        var element = (TextBlock)base.GenerateElement(cell, dataItem);
        element.SetBinding(TextBlock.TextProperty, new Binding(IsFinalPrice
            ? nameof(LubeSakvaraMangd.EffectiveSlutpris)
            : nameof(LubeSakvaraMangd.EffectiveOffertpris))
        {
            Mode = BindingMode.OneWay,
            StringFormat = "N2",
            ConverterCulture = CultureInfo.GetCultureInfo("sv-SE")
        });
        element.SetBinding(TextBlock.ForegroundProperty, new Binding(IsFinalPrice
            ? nameof(LubeSakvaraMangd.IsSlutprisFallback)
            : nameof(LubeSakvaraMangd.IsOffertprisFallback))
        {
            Mode = BindingMode.OneWay,
            Converter = FallbackBrushConverter.Instance
        });
        return element;
    }

    // GenerateEditingElement deliberately uses the base implementation and the
    // column's raw Offertpris/Slutpris Binding. Displaying a fallback never saves it.
    private sealed class FallbackBrushConverter : IValueConverter
    {
        public static readonly FallbackBrushConverter Instance = new();
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            => value is true ? Brushes.Gray : Brushes.Black;
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => System.Windows.Data.Binding.DoNothing;
    }
}
