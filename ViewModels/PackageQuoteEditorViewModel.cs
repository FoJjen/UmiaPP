using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using UmiaPP.Models;

namespace UmiaPP.ViewModels;

/// <summary>Edits a detached quote draft with explicit save and confirmed delete actions.</summary>
public partial class PackageQuoteEditorViewModel : ObservableObject, IDisposable
{
    private static readonly CultureInfo AmountCulture = CultureInfo.GetCultureInfo("sv-SE");
    private readonly int _quoteId;
    private readonly int _projectId;
    private readonly Func<LubeSakvaraOffert, Task> _save;
    private readonly Func<Task>? _delete;
    private readonly Func<bool>? _confirmDelete;

    [ObservableProperty] private string _supplier;
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DialogTitle))]
    private string _quoteNumber;
    [ObservableProperty] private DateTime? _quoteDate;
    [ObservableProperty] private string _quotedAmountInput;
    [ObservableProperty] private string _finalAmountInput;
    [ObservableProperty] private string _filePath;
    [ObservableProperty] private string _comment;
    [ObservableProperty] private string _errorMessage = string.Empty;
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
    [NotifyCanExecuteChangedFor(nameof(CancelCommand))]
    [NotifyCanExecuteChangedFor(nameof(BrowseCommand))]
    [NotifyCanExecuteChangedFor(nameof(DeleteCommand))]
    private bool _isSaving;

    public IReadOnlyList<LubeSakvaraMangd> Articles { get; }
    public string Currency { get; }
    public string DialogTitle => string.IsNullOrWhiteSpace(QuoteNumber)
        ? $"Redigera offert #{_quoteId}" : $"Redigera offert {QuoteNumber} (#{_quoteId})";
    public double CalculatedNetAmount => Articles.Sum(article => article.PrisNto);
    public double? QuoteDifference
    {
        get
        {
            if (!TryAmount(QuotedAmountInput, out var amount)) return null;
            var difference = amount - CalculatedNetAmount;
            return double.IsFinite(difference)
                ? Math.Round(difference, 2, MidpointRounding.AwayFromZero)
                : null;
        }
    }
    public bool IsQuoteCheaper => QuoteDifference is < 0;
    public bool IsQuoteMoreExpensive => QuoteDifference is > 0;
    public string ComparisonDescription => QuoteDifference switch
    {
        < 0 => "Offerten är billigare än kalkylerat Pris NTO.",
        > 0 => "Offerten är dyrare än kalkylerat Pris NTO.",
        0 => "Offerten motsvarar kalkylerat Pris NTO.",
        _ => "Ange ett giltigt offererat belopp för att jämföra med kalkylen."
    };
    public bool IsSaved { get; private set; }
    public bool IsDeleted { get; private set; }
    public bool SupportsDeletion => _quoteId > 0 && _delete != null && _confirmDelete != null;
    public Action? CloseAction { get; set; }
    public Action? BrowseFileAction { get; set; }

    public PackageQuoteEditorViewModel(LubeSakvaraOffert quote,
        IReadOnlyList<LubeSakvaraMangd> articles, Func<LubeSakvaraOffert, Task> save,
        Func<Task>? delete = null, Func<bool>? confirmDelete = null)
    {
        _quoteId = quote.Id;
        _projectId = quote.ProjectId;
        _save = save;
        _delete = delete;
        _confirmDelete = confirmDelete;
        Articles = articles;
        Currency = quote.Valuta;
        _supplier = quote.Leverantor;
        _quoteNumber = quote.Produktgrupp;
        _quoteDate = quote.QuoteDate;
        _quotedAmountInput = quote.OffereratPris.ToString("G", AmountCulture);
        _finalAmountInput = quote.FinalAmount == 0 ? string.Empty : quote.FinalAmount.ToString("G", AmountCulture);
        _filePath = quote.FilePath;
        _comment = quote.Kommentar ?? string.Empty;
        foreach (var article in Articles)
            article.PropertyChanged += OnArticlePropertyChanged;
    }

    partial void OnQuotedAmountInputChanged(string value) => NotifyComparison();

    private void OnArticlePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (string.IsNullOrEmpty(e.PropertyName) || e.PropertyName == nameof(LubeSakvaraMangd.PrisNto))
        {
            OnPropertyChanged(nameof(CalculatedNetAmount));
            NotifyComparison();
        }
    }

    private void NotifyComparison()
    {
        OnPropertyChanged(nameof(QuoteDifference));
        OnPropertyChanged(nameof(IsQuoteCheaper));
        OnPropertyChanged(nameof(IsQuoteMoreExpensive));
        OnPropertyChanged(nameof(ComparisonDescription));
    }

    private bool CanEdit() => !IsSaving && !IsDeleted;

    private bool CanDelete() => CanEdit() && SupportsDeletion;

    [RelayCommand(CanExecute = nameof(CanDelete))]
    private async Task DeleteAsync()
    {
        if (!CanDelete()) return;
        ErrorMessage = string.Empty;
        IsSaving = true;
        try
        {
            if (!_confirmDelete!()) return;
            await _delete!();
            IsDeleted = true;
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Kunde inte ta bort offerten: {ex.Message}";
        }
        finally
        {
            IsSaving = false;
        }
        if (IsDeleted) CloseAction?.Invoke();
    }

    [RelayCommand(CanExecute = nameof(CanEdit))]
    private void Browse() => BrowseFileAction?.Invoke();

    [RelayCommand(CanExecute = nameof(CanEdit))]
    private void Cancel()
    {
        if (!IsSaving) CloseAction?.Invoke();
    }

    [RelayCommand(CanExecute = nameof(CanEdit))]
    private async Task SaveAsync()
    {
        if (!CanEdit()) return;
        ErrorMessage = string.Empty;
        if (!TryAmount(QuotedAmountInput, out var quoted))
        {
            ErrorMessage = "Ange ett offererat belopp på minst 0. Använd komma som decimaltecken.";
            return;
        }
        var final = 0d;
        if (!string.IsNullOrWhiteSpace(FinalAmountInput) && !TryAmount(FinalAmountInput, out final))
        {
            ErrorMessage = "Ange ett slutpris på minst 0, eller lämna tomt för att använda offertpriset.";
            return;
        }
        if (QuoteDate is not DateTime date)
        {
            ErrorMessage = "Ange ett giltigt offertdatum.";
            return;
        }

        var draft = new LubeSakvaraOffert
        {
            Id = _quoteId,
            ProjectId = _projectId,
            Leverantor = Supplier.Trim(),
            Produktgrupp = string.IsNullOrWhiteSpace(QuoteNumber)
                ? $"Paketoffert {Supplier.Trim()}".Trim() : QuoteNumber.Trim(),
            QuoteDate = date.Date,
            OffereratPris = quoted,
            FinalAmount = final,
            FilePath = FilePath.Trim(),
            Kommentar = Comment.Trim(),
            Valuta = Currency,
            IsPackageQuote = true
        };
        IsSaving = true;
        try
        {
            await _save(draft);
            IsSaved = true;
        }
        catch (Exception ex)
        {
            // Keep the draft open so input is not lost if persistence fails.
            ErrorMessage = $"Kunde inte spara offerten: {ex.Message}";
        }
        finally
        {
            IsSaving = false;
        }
        if (IsSaved) CloseAction?.Invoke();
    }

    private static bool TryAmount(string input, out double amount)
    {
        amount = 0;
        if (string.IsNullOrWhiteSpace(input)) return false;
        var text = input.Trim().Replace("\u00a0", "").Replace("\u202f", "").Replace(" ", "");
        return double.TryParse(text, NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingSign | NumberStyles.AllowExponent,
                AmountCulture, out amount) && double.IsFinite(amount) && amount >= 0;
    }

    public void Dispose()
    {
        foreach (var article in Articles)
            article.PropertyChanged -= OnArticlePropertyChanged;
        CloseAction = null;
        BrowseFileAction = null;
    }
}
