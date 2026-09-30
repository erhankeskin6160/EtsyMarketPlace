using EtsyMarketPlace.Domain.Banking;

namespace EtsyMarketPlace.Application.Banking;

/// <summary>
/// Etsy banka transferlerinin (Payouts / Deposits) hesaplanması, filtrelenmesi ve tarihsel dökümü için servis sözleşmesi.
/// </summary>
public interface IBankDepositService
{
    /// <summary>
    /// Verilen ham kayıtları filtreleyip döneme göre sıralayarak aylık/dönemsel banka yatırımı özetini ve transfer listesini üretir.
    /// </summary>
    MonthlyDepositSummary CalculateMonthlyDeposits(
        IEnumerable<RawDepositEntryInput> rawEntries,
        DateTimeOffset periodStart,
        DateTimeOffset periodEnd,
        Func<DateTime, decimal>? exchangeRateResolver = null,
        decimal defaultExchangeRate = 48.25m
    );
}

/// <summary>
/// Domain ve Infrastructure bağımsız ham kayıt girdi DTO'su.
/// </summary>
public sealed record RawDepositEntryInput(
    long EntryId,
    long ReferenceId,
    string Type,
    decimal Amount,
    decimal NetAmount,
    string Currency,
    string Description,
    DateTimeOffset CreatedAt,
    decimal PreResolvedRate = 0m
);
