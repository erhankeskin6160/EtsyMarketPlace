using EtsyMarketPlace.Domain.Banking;

namespace EtsyMarketPlace.Application.Banking;

/// <summary>
/// Etsy banka transferlerini analiz eden ve Clean Architecture domain modellerine dönüştüren servis.
/// </summary>
public sealed class BankDepositService : IBankDepositService
{
    public MonthlyDepositSummary CalculateMonthlyDeposits(
        IEnumerable<RawDepositEntryInput> rawEntries,
        DateTimeOffset periodStart,
        DateTimeOffset periodEnd,
        Func<DateTime, decimal>? exchangeRateResolver = null,
        decimal defaultExchangeRate = 48.25m)
    {
        var depositList = new List<BankDepositRecord>();

        foreach (var entry in rawEntries)
        {
            if (!IsDepositType(entry.Type, entry.Description))
                continue;

            // Tarih kontrolü (dönem içi)
            if (entry.CreatedAt < periodStart || entry.CreatedAt > periodEnd)
                continue;

            decimal rate = entry.PreResolvedRate > 0 
                ? entry.PreResolvedRate 
                : (exchangeRateResolver != null ? exchangeRateResolver(entry.CreatedAt.DateTime) : defaultExchangeRate);

            if (rate <= 0) rate = defaultExchangeRate;

            decimal amount = Math.Abs(entry.Amount != 0 ? entry.Amount : entry.NetAmount);
            decimal amountTRY = Math.Round(amount * rate, 2);

            string status = "Yatırıldı";
            string hint = "Etsy Payout (Banka Transferi)";
            string desc = !string.IsNullOrWhiteSpace(entry.Description) && entry.Description != "deposit"
                ? entry.Description
                : "Etsy Payment Account Payout";

            depositList.Add(new BankDepositRecord(
                entry.EntryId,
                entry.ReferenceId,
                entry.CreatedAt,
                amount,
                string.IsNullOrWhiteSpace(entry.Currency) ? "USD" : entry.Currency.ToUpperInvariant(),
                rate,
                amountTRY,
                status,
                hint,
                desc
            ));
        }

        // Kronolojik sıralama: En son yapılan transfer en üstte
        depositList.Sort((a, b) => b.DepositDate.CompareTo(a.DepositDate));

        decimal totalAmount = depositList.Sum(d => d.Amount);
        decimal totalAmountTRY = depositList.Sum(d => d.AmountTRY);
        int count = depositList.Count;
        decimal avgAmount = count > 0 ? totalAmount / count : 0m;
        decimal avgAmountTRY = count > 0 ? totalAmountTRY / count : 0m;
        var lastDeposit = depositList.FirstOrDefault();

        return new MonthlyDepositSummary(
            periodStart,
            periodEnd,
            totalAmount,
            totalAmountTRY,
            count,
            avgAmount,
            avgAmountTRY,
            lastDeposit,
            depositList
        );
    }

    private static bool IsDepositType(string? type, string? description)
    {
        string t = (type ?? "").ToLowerInvariant();
        string d = (description ?? "").ToLowerInvariant();

        if (t.Contains("fee") || d.Contains("fee")) return false;

        return t.Contains("deposit") || t.Contains("payout") || t.Contains("disbursement") || t.Contains("transfer") ||
               d.Contains("deposit") || d.Contains("payout") || d.Contains("disbursement") || d.Contains("transfer") ||
               d.Contains("yatırılan") || d.Contains("banka") || d.Contains("hesaba");
    }
}
