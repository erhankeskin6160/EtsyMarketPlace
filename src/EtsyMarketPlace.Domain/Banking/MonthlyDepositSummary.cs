namespace EtsyMarketPlace.Domain.Banking;

/// <summary>
/// Belirli bir ay veya tarih aralığındaki Etsy banka transferlerinin özet bilançosu.
/// </summary>
public sealed record MonthlyDepositSummary(
    DateTimeOffset PeriodStart,
    DateTimeOffset PeriodEnd,
    decimal TotalAmount,
    decimal TotalAmountTRY,
    int DepositCount,
    decimal AverageAmount,
    decimal AverageAmountTRY,
    BankDepositRecord? LastDeposit,
    IReadOnlyList<BankDepositRecord> Deposits
)
{
    public bool HasDeposits => DepositCount > 0;
}
