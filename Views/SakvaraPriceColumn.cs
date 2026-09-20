using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using UmiaPP.Models;

namespace UmiaPP.Views;

/// <summary>Displays an effective price but edits only the entered price.</summary>
public sealed class SakvaraPriceColumn : DataGridTextColumn
{
    public bool IsFinalPrice { get; set; }

    protected override FrameworkElement GenerateElement(DataGridCell cell, object dataItem)
    {
        var element = (TextBlock)base.GenerateElement(cell, dataItem);
        var priceBinding = new Binding(IsFinalPrice
            ? nameof(LubeSakvaraMangd.EffectiveSlutpris)
            : nameof(LubeSakvaraMangd.EffectiveOffertpris))
        {
            Mode = BindingMode.OneWay,
            StringFormat = "N2",
            ConverterCulture = CultureInfo.GetCultureInfo("sv-SE")
        };
        if (IsFinalPrice)
        {
            var displayBinding = new MultiBinding
            {
                Mode = BindingMode.OneWay,
                Converter = PackageQuotePriceConverter.Instance,
                ConverterCulture = CultureInfo.GetCultureInfo("sv-SE")
            };
            displayBinding.Bindings.Add(priceBinding);
            displayBinding.Bindings.Add(new Binding(nameof(LubeSakvaraMangd.HasPackageQuote)));
            displayBinding.Bindings.Add(new Binding(nameof(LubeSakvaraMangd.PackageQuoteLabel)));
            element.SetBinding(TextBlock.TextProperty, displayBinding);
            element.SetBinding(FrameworkElement.ToolTipProperty,
                new Binding(nameof(LubeSakvaraMangd.PackageQuoteLabel)));
        }
        else
        {
            element.SetBinding(TextBlock.TextProperty, priceBinding);
        }
        // ElementStyle supplies theme-aware normal/fallback colors. A local
        // hard-coded Foreground binding would override those style triggers.
        return element;
    }

    // GenerateEditingElement deliberately uses the base implementation and the
    // column's raw Offertpris/Slutpris Binding. Displaying a fallback never saves it.
    private sealed class PackageQuotePriceConverter : IMultiValueConverter
    {
        public static readonly PackageQuotePriceConverter Instance = new();

        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
            => values.Length > 1 && values[1] is true
                ? values.Length > 2 && values[2] is string label && !string.IsNullOrWhiteSpace(label)
                    ? label
                    : "Ingår i offert"
                : values.Length > 0 && values[0] is double amount
                    ? amount.ToString("N2", culture)
                    : string.Empty;

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
            => targetTypes.Select(_ => System.Windows.Data.Binding.DoNothing).ToArray();
    }

}
