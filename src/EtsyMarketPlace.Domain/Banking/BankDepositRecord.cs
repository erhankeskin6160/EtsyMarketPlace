namespace EtsyMarketPlace.Domain.Banking;

/// <summary>
/// Etsy tarafından mağaza sahibinin banka hesabına yatırılan (Deposit / Payout) münferit transfer kaydı.
/// </summary>
public sealed record BankDepositRecord(
    long EntryId,
    long ReferenceId,
    DateTimeOffset DepositDate,
    decimal Amount,
    string Currency,
    decimal ExchangeRate,
    decimal AmountTRY,
    string Status = "Completed",
    string BankDetailsHint = "Etsy Otomatik Payout / Banka Hesabı",
    string Description = "Banka Hesabına Transfer"
)
{
    public string FormattedDate => DepositDate.ToString("dd.MM.yyyy HH:mm");
    public string ShortDate => DepositDate.ToString("dd.MM.yy");
    public string ReferenceDisplay => ReferenceId > 0 ? $"#{ReferenceId}" : $"#{EntryId}";
}
