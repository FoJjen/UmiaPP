using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using UmiaPP.Models;

namespace UmiaPP.ViewModels;

public partial class LinkQuoteDialogViewModel : ObservableObject
{
    [ObservableProperty] private string _supplier = string.Empty;
    [ObservableProperty] private string _quoteNumber = string.Empty;
    [ObservableProperty] private DateTime _quoteDate = DateTime.Today;
    [ObservableProperty] private string _quotedAmountInput = string.Empty;
    [ObservableProperty] private string _finalAmountInput = string.Empty;
    [ObservableProperty] private string _filePath = string.Empty;
    [ObservableProperty] private string _errorMessage = string.Empty;

    public IReadOnlyList<LubeSakvaraMangd> Articles { get; }
    public bool IsConfirmed { get; private set; }
    public Action? CloseAction { get; set; }
    public Action<string>? BrowseFile { get; set; }

    public double QuotedAmount { get; private set; }
    public double FinalAmount { get; private set; }

    public LinkQuoteDialogViewModel(IReadOnlyList<LubeSakvaraMangd> articles)
    {
        Articles = articles;
        Supplier = articles.Select(a => a.Leverantor).FirstOrDefault(s => !string.IsNullOrWhiteSpace(s)) ?? string.Empty;
        QuotedAmountInput = articles.Where(a => a.Offertpris > 0).Sum(a => a.Offertpris)
            .ToString("0.##", System.Globalization.CultureInfo.GetCultureInfo("sv-SE"));
    }

    [RelayCommand]
    private void Browse() => BrowseFile?.Invoke("PDF-filer (*.pdf)|*.pdf|Alla filer (*.*)|*.*");

    [RelayCommand]
    private void Cancel()
    {
        IsConfirmed = false;
        CloseAction?.Invoke();
    }

    [RelayCommand]
    private void Save()
    {
        ErrorMessage = string.Empty;
        if (!TryAmount(QuotedAmountInput, out var quoted) || quoted < 0)
        {
            ErrorMessage = "Ange ett offererat belopp på minst 0 kr. Använd komma som decimaltecken.";
            return;
        }
        if (!string.IsNullOrWhiteSpace(FinalAmountInput) && (!TryAmount(FinalAmountInput, out var final) || final < 0))
        {
            ErrorMessage = "Ange ett giltigt slutpris på minst 0 kr, eller lämna tomt.";
            return;
        }
        QuotedAmount = quoted;
        FinalAmount = string.IsNullOrWhiteSpace(FinalAmountInput) ? 0 : ParseAmount(FinalAmountInput);
        IsConfirmed = true;
        CloseAction?.Invoke();
    }

    private static bool TryAmount(string input, out double amount)
    {
        amount = 0;
        if (string.IsNullOrWhiteSpace(input)) return false;
        var text = input.Trim().Replace("\u00a0", "").Replace("\u202f", "").Replace(" ", "");
        return double.TryParse(text, System.Globalization.NumberStyles.AllowDecimalPoint,
            System.Globalization.CultureInfo.GetCultureInfo("sv-SE"), out amount)
            && double.IsFinite(amount);
    }

    private static double ParseAmount(string input)
        => TryAmount(input, out var amount) ? amount : 0;
}
