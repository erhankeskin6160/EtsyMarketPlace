using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EtsyMarketPlace.Application.Accounting;
using EtsyMarketPlace.Application.Banking;
using EtsyMarketPlace.Application.EtsyIntegration;
using EtsyMarketPlace.Application.Features.Accounting;
using EtsyMarketPlace.Domain.Accounting;
using EtsyMarketPlace.Domain.Accounting.Entities;
using EtsyMarketPlace.Domain.Banking;
using Xunit;

namespace EtsyMarketPlace.Application.Tests.Accounting;

public sealed class GetIncomeStatementHandlerTests
{
    [Fact]
    public async Task Handle_LiveLedgerWithSecenekBAndCanceledLoss_ReconcilesExactKpiParityAndTaxBridge()
    {
        var day = new DateTimeOffset(2026, 11, 5, 10, 0, 0, TimeSpan.FromHours(3));
        var start = new DateTimeOffset(2026, 11, 1, 0, 0, 0, TimeSpan.FromHours(3)).ToUniversalTime();
        var end = new DateTimeOffset(2026, 11, 15, 23, 59, 59, TimeSpan.FromHours(3)).ToUniversalTime();
        var fx = HistoricalExchangeRateProvider.GetRateForDate(day.UtcDateTime.Date, 50.0m);

        decimal TryOf(decimal usd) => Math.Round(usd * fx, 2, MidpointRounding.AwayFromZero);

        var dataClient = new TestEtsyDataClient
        {
            Receipts =
            {
                // Yurt içi (TR): $100, aktif
                MakeReceipt(101, day, true, false, 100m, "Ali Kaya", "TR"),
                // Yurt dışı (US): $200, aktif
                MakeReceipt(102, day, true, false, 200m, "John Doe", "US")
            },
            Entries =
            {
                MakeEntry("sale", day, 300m, fx, TryOf(300m), 101, 1),
                MakeEntry("transaction_fee", day, -19.50m, fx, -TryOf(19.50m), 101, 2),
                MakeEntry("listing_fee", day, -0.40m, fx, -TryOf(0.40m), 101, 3),
                MakeEntry("payment_processing", day, -12.10m, fx, -TryOf(12.10m), 101, 4),
                MakeEntry("etsy_tax_fee", day, -6.00m, fx, -TryOf(6.00m), 101, 5),
                MakeEntry("ad_fee", day, -10.00m, fx, -TryOf(10.00m), 0, 6),
                MakeEntry("offsite_ads", day, -15.00m, fx, -TryOf(15.00m), 0, 7),
                // Çapraz dönem iade: $50 iade + önceki dönem #900 siparişi
                MakeEntry("refund", day, -50.00m, fx, -TryOf(50.00m), 900, 8)
            }
        };

        var etsyRepo = new TestIntegrationRepo();
        etsyRepo.Costs[101] = new EtsyDashboardOrderCost(20m, 10m, 2m); // Toplam $32
        etsyRepo.Costs[102] = new EtsyDashboardOrderCost(40m, 15m, 3m); // Toplam $58
        etsyRepo.Costs[900] = new EtsyDashboardOrderCost(25m, 12m, 3m); // İade kaybı Toplam $40

        var ledgerService = new EtsyLedgerReportService(dataClient, etsyRepo);
        var dashboardService = new EtsyDashboardService(new StubReportingService(), ledgerService, dataClient, etsyRepo, new BankDepositService());

        var accountingRepo = new InMemoryAccountingRepository
        {
            Profile = new ShopAccountingProfile
            {
                ShopId = "53236321",
                CommissionTarget = CommissionAccountTarget.OtherCommission_653,
                ShippingTarget = ShippingPackagingCostTarget.MarketingSelling_631,
                CanceledCostTarget = CanceledOrderCostTarget.ExtraordinaryLoss_689,
                InventoryMethod = InventoryValuationMethod.PerpetualPerOrder,
                TaxRegime = TaxRegimeType.SoleProprietorshipGvk,
                YoungEntrepreneurModeEnabled = true,
                YoungEntrepreneurAnnualLimitTry = 400_000m,
                IncludeFxSpreadAndExchangeDifference = true
            }
        };
        accountingRepo.Expenses.Add(new OperatingExpenseEntry
        {
            Id = 1,
            ShopId = "53236321",
            EntryDate = new DateTime(2026, 11, 2, 0, 0, 0, DateTimeKind.Utc),
            DocumentNo = "ATOLYE-01",
            Category = OperatingExpenseCategory.GeneralManagement_632,
            Description = "Atölye Kirası",
            Amount = 1_000m,
            Currency = "TRY",
            ExchangeRateToTry = 1m,
            AmountTry = 1_000m,
            IsTaxDeductible = true,
            Frequency = ExpenseFrequency.MonthlyRecurring
        });
        accountingRepo.Expenses.Add(new OperatingExpenseEntry
        {
            Id = 2,
            ShopId = "53236321",
            EntryDate = new DateTime(2026, 11, 3, 0, 0, 0, DateTimeKind.Utc),
            DocumentNo = "CEZA-01",
            Category = OperatingExpenseCategory.GeneralManagement_632,
            Description = "Trafik Cezası (KKEG)",
            Amount = 500m,
            Currency = "TRY",
            ExchangeRateToTry = 1m,
            AmountTry = 500m,
            IsTaxDeductible = false,
            Frequency = ExpenseFrequency.OneTime
        });

        var handler = new GetIncomeStatementHandler(ledgerService, dashboardService, accountingRepo);
        var response = await handler.Handle(new GetIncomeStatementQuery("53236321", "custom", start, end), CancellationToken.None);

        Assert.True(response.Success);
        var dto = Assert.IsType<IncomeStatementResponseDto>(response.Data);
        var doc = dto.Statement;

        // 1. 600 vs 601 ayrımı ($100 TR vs $200 US)
        var expectedGross = TryOf(300m);
        var expectedDom = Math.Round(expectedGross / 3m, 2);
        var expectedExp = expectedGross - expectedDom;
        Assert.Equal(expectedDom, doc.AllLines.Single(l => l.AccountCode == "600").AmountTry);
        Assert.Equal(expectedExp, doc.AllLines.Single(l => l.AccountCode == "601").AmountTry);
        Assert.Equal(expectedGross, doc.GrossSalesTry);
        Assert.Equal(TryOf(50m), doc.SalesDeductionsTry); // 610 İadeler

        // 2. Seçenek A -> 620 sadece aktif ürün maliyeti (20+40 = $60)
        Assert.Equal(TryOf(60m), doc.CostOfSalesTry);

        // 3. Seçenek B -> 653 = 19.50 + 0.40 + 12.10 USD komisyonlar
        Assert.Equal(TryOf(19.50m) + TryOf(0.40m) + TryOf(12.10m), doc.AllLines.Single(l => l.AccountCode == "653").AmountTry);

        // 4. 689 = Çapraz dönem #900 iptal/iade maliyet zararı ($40)
        Assert.Equal(TryOf(40m), doc.AllLines.Single(l => l.AccountCode == "689").AmountTry);
        Assert.Equal(TryOf(40m), doc.Reconciliation.SeparatedCanceledCostLossTry);

        // 5. Mutabakat: Canlı KPI NetProfitTRY == EtsyContributionProfitTry; ve 690 = EtsyKatkı - 1.500 (kira + KKEG)!
        var liveCheck = await ledgerService.GetLiveReportAsync("53236321", start, end, CancellationToken.None);
        Assert.Equal(liveCheck.Performance.NetProfitTRY, doc.Reconciliation.EtsyContributionProfitTry);
        Assert.Equal(doc.Reconciliation.EtsyContributionProfitTry - 1_500m, doc.CommercialProfit690Try);

        // 6. Genç Girişimci açıkken vergi 0; preview override ile kapatıldığında matrah = 690 + 500 (KKEG) -> %15!
        Assert.Equal(0m, doc.TaxProvision691Try);
        var previewNoYoung = await handler.Handle(
            new GetIncomeStatementQuery("53236321", "custom", start, end, YoungEntrepreneurModeOverride: false),
            CancellationToken.None);
        var previewDto = Assert.IsType<IncomeStatementResponseDto>(previewNoYoung.Data);
        var expectedTax = Math.Round((doc.CommercialProfit690Try + 500m) * 0.15m, 2, MidpointRounding.AwayFromZero);
        Assert.Equal(expectedTax, previewDto.Statement.TaxProvision691Try);
        Assert.Equal(doc.CommercialProfit690Try - expectedTax, previewDto.Statement.PeriodNetProfit692Try);

        Assert.All(doc.ClosingVouchers, v => Assert.True(v.IsBalanced));
    }

    [Fact]
    public async Task Handle_PeriodicInventoryMode_UsesPeriodCountFormulaFor620()
    {
        var day = new DateTimeOffset(2026, 11, 5, 10, 0, 0, TimeSpan.FromHours(3));
        var start = new DateTimeOffset(2026, 11, 1, 0, 0, 0, TimeSpan.FromHours(3)).ToUniversalTime();
        var end = new DateTimeOffset(2026, 11, 30, 23, 59, 59, TimeSpan.FromHours(3)).ToUniversalTime();

        var dataClient = new TestEtsyDataClient
        {
            Receipts = { MakeReceipt(101, day, true, false, 100m, "Buyer", "US") },
            Entries = { MakeEntry("sale", day, 100m, 50m, 5_000m, 101, 1) }
        };
        var etsyRepo = new TestIntegrationRepo();
        var ledgerService = new EtsyLedgerReportService(dataClient, etsyRepo);
        var dashboardService = new EtsyDashboardService(new StubReportingService(), ledgerService, dataClient, etsyRepo, new BankDepositService());

        var accountingRepo = new InMemoryAccountingRepository
        {
            Profile = new ShopAccountingProfile
            {
                ShopId = "53236321",
                InventoryMethod = InventoryValuationMethod.PeriodicCount,
                YoungEntrepreneurModeEnabled = false
            }
        };
        accountingRepo.InventoryAdjustments.Add(new InventoryPeriodAdjustment
        {
            ShopId = "53236321",
            Year = 2026,
            Month = 11,
            OpeningInventoryTry = 1_200m,
            GrossPurchasesTry = 2_000m,
            PurchaseReturnsAndDiscountsTry = 200m,
            ClosingInventoryTry = 900m
        }); // STMM = 1200 + (2000 - 200) - 900 = 2.100 TL

        var handler = new GetIncomeStatementHandler(ledgerService, dashboardService, accountingRepo);
        var response = await handler.Handle(new GetIncomeStatementQuery("53236321", "custom", start, end), CancellationToken.None);

        Assert.True(response.Success);
        var dto = Assert.IsType<IncomeStatementResponseDto>(response.Data);
        Assert.Equal(2_100m, dto.Statement.CostOfSalesTry);
        Assert.Equal(2_900m, dto.Statement.GrossProfitTry);
        Assert.Contains(dto.DataCompletenessNotes, n => n.Contains("Aralıklı Envanter", StringComparison.Ordinal));
    }

    private static EtsyDashboardReceipt MakeReceipt(long id, DateTimeOffset date, bool paid, bool canceled, decimal grandTotal, string buyer, string country) =>
        new(
            ReceiptId: id,
            CreatedAt: date,
            IsPaid: paid,
            IsCanceled: canceled,
            IsFromOffsiteAds: false,
            CurrencyCode: "USD",
            GrandTotal: grandTotal,
            Subtotal: grandTotal,
            ShippingCost: 0m,
            DiscountAmt: 0m,
            TaxCost: 0m,
            RefundedAmount: 0m,
            Items: Array.Empty<EtsyDashboardReceiptItem>(),
            BuyerName: buyer,
            BuyerUserId: id,
            BuyerEmail: "buyer@example.com",
            Status: canceled ? "canceled" : "completed",
            CountryCode: country);

    private static EtsyLedgerEntryDetail MakeEntry(string type, DateTimeOffset date, decimal amount, decimal rate, decimal amountTry, long refId, long entryId) =>
        new(type, type, type, type, amount, amount, amountTry, "USD", date, rate, refId, entryId);

    private sealed class StubReportingService : IEtsyReportingService
    {
        public Task<IReadOnlyList<EtsyBankPayout>> GetBankPayoutsAsync(string shopId, DateTimeOffset startDate, DateTimeOffset endDate, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<EtsyBankPayout>>(Array.Empty<EtsyBankPayout>());
        public Task<FinancialPerformance> GetFinancialPerformanceAsync(string shopId, DateTimeOffset startDate, DateTimeOffset endDate, CancellationToken ct = default) =>
            Task.FromResult(new FinancialPerformance(startDate, endDate, "USD", 0, 0, 0, 0, 0, 0, 0, 0, 0));
        public Task<DailyShopBrief> GetDailyShopBriefAsync(string shopId, DateTimeOffset date, CancellationToken ct = default) =>
            Task.FromResult(new DailyShopBrief(date, 90, "İyi", 0, 0, 0, 0, 0));
        public Task<IReadOnlyList<EtsyOrderCostAlert>> GetUnfulfilledCostAlertsAsync(string shopId, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<EtsyOrderCostAlert>>(Array.Empty<EtsyOrderCostAlert>());
    }

    private sealed class TestEtsyDataClient : IEtsyDataClient
    {
        public List<EtsyDashboardReceipt> Receipts { get; } = new();
        public List<EtsyLedgerEntryDetail> Entries { get; } = new();

        public Task<IReadOnlyList<EtsyBankPayout>> GetBankPayoutsAsync(string shopId, DateTimeOffset startDate, DateTimeOffset endDate, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<EtsyBankPayout>>(Array.Empty<EtsyBankPayout>());
        public Task<IReadOnlyList<EtsyFinancialTransaction>> GetFinancialTransactionsAsync(string shopId, DateTimeOffset startDate, DateTimeOffset endDate, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<EtsyFinancialTransaction>>(Array.Empty<EtsyFinancialTransaction>());
        public Task<IReadOnlyList<EtsyOrderCostAlert>> GetUnfulfilledCostAlertsAsync(string shopId, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<EtsyOrderCostAlert>>(Array.Empty<EtsyOrderCostAlert>());
        public Task<IReadOnlyList<EtsyDashboardReceipt>> GetShopReceiptsAsync(string shopId, DateTimeOffset startDate, DateTimeOffset endDate, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<EtsyDashboardReceipt>>(Receipts);
        public Task<IReadOnlyList<EtsyDashboardLedgerFee>> GetPaymentAccountLedgerEntriesAsync(string shopId, DateTimeOffset startDate, DateTimeOffset endDate, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<EtsyDashboardLedgerFee>>(Array.Empty<EtsyDashboardLedgerFee>());
        public Task<IReadOnlyList<EtsyLedgerEntryDetail>> GetLedgerEntriesDetailedAsync(string shopId, DateTimeOffset startDate, DateTimeOffset endDate, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<EtsyLedgerEntryDetail>>(Entries);
    }

    private sealed class TestIntegrationRepo : IEtsyIntegrationRepository
    {
        public Dictionary<long, EtsyDashboardOrderCost> Costs { get; } = new();
        public Task<DateTimeOffset?> GetLastCursorAsync(string shopId, string dataType, CancellationToken ct = default) => Task.FromResult<DateTimeOffset?>(null);
        public Task SavePayoutsAsync(IReadOnlyCollection<EtsyBankPayout> payouts, CancellationToken ct = default) => Task.CompletedTask;
        public Task<IReadOnlyList<EtsyBankPayout>> GetBankPayoutsAsync(string shopId, DateTimeOffset startDate, DateTimeOffset endDate, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<EtsyBankPayout>>(Array.Empty<EtsyBankPayout>());
        public Task SaveTransactionsAsync(IReadOnlyCollection<EtsyFinancialTransaction> transactions, CancellationToken ct = default) => Task.CompletedTask;
        public Task SaveOrderAlertsAsync(IReadOnlyCollection<EtsyOrderCostAlert> alerts, CancellationToken ct = default) => Task.CompletedTask;
        public Task SaveSyncStateAsync(string shopId, string dataType, DateTimeOffset cursor, string? error = null, CancellationToken ct = default) => Task.CompletedTask;
        public Task SaveMonthlyOrderSummariesAsync(IReadOnlyCollection<EtsyMonthlyOrderSummary> summaries, CancellationToken ct = default) => Task.CompletedTask;
        public Task<IReadOnlyList<EtsyMonthlyOrderSummary>> GetMonthlyOrderSummariesAsync(string shopId, int months = 12, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<EtsyMonthlyOrderSummary>>(Array.Empty<EtsyMonthlyOrderSummary>());
        public Task SaveListingTrafficDailyAsync(IReadOnlyCollection<EtsyListingTrafficRecord> records, CancellationToken ct = default) => Task.CompletedTask;
        public Task<IReadOnlyList<EtsyListingTrafficRecord>> GetListingTrafficAnalyticsAsync(string shopId, string? snapshotDate = null, int limit = 50, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<EtsyListingTrafficRecord>>(Array.Empty<EtsyListingTrafficRecord>());
        public Task SaveChartSnapshotAsync(EtsyChartSnapshot snapshot, CancellationToken ct = default) => Task.CompletedTask;
        public Task<EtsyChartSnapshot?> GetChartSnapshotAsync(string shopId, string chartType, CancellationToken ct = default) => Task.FromResult<EtsyChartSnapshot?>(null);
        public Task<IReadOnlyDictionary<long, EtsyDashboardOrderCost>> GetOrderCostsAsync(string shopId, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyDictionary<long, EtsyDashboardOrderCost>>(Costs);
        public Task UpsertOrderCostAsync(string shopId, string orderId, decimal? productCost, decimal? shippingCost, decimal? packagingCost, string? notes, CancellationToken ct = default) => Task.CompletedTask;
    }

    private sealed class InMemoryAccountingRepository : IAccountingRepository
    {
        public ShopAccountingProfile Profile { get; set; } = new() { ShopId = "53236321" };
        public List<OperatingExpenseEntry> Expenses { get; } = new();
        public List<FixedAssetItem> Assets { get; } = new();
        public List<InventoryPeriodAdjustment> InventoryAdjustments { get; } = new();

        public Task<ShopAccountingProfile> GetOrDefaultProfileAsync(string shopId, CancellationToken ct = default) => Task.FromResult(Profile);
        public Task<ShopAccountingProfile> UpsertProfileAsync(ShopAccountingProfile profile, CancellationToken ct = default)
        {
            Profile = profile;
            return Task.FromResult(profile);
        }
        public Task<IReadOnlyList<OperatingExpenseEntry>> GetOperatingExpensesAsync(string shopId, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<OperatingExpenseEntry>>(Expenses);
        public Task<OperatingExpenseEntry?> GetOperatingExpenseByIdAsync(long id, string shopId, CancellationToken ct = default) =>
            Task.FromResult(Expenses.FirstOrDefault(e => e.Id == id));
        public Task<OperatingExpenseEntry> AddOperatingExpenseAsync(OperatingExpenseEntry entry, CancellationToken ct = default)
        {
            entry.Id = Expenses.Count + 1;
            Expenses.Add(entry);
            return Task.FromResult(entry);
        }
        public Task<OperatingExpenseEntry?> UpdateOperatingExpenseAsync(OperatingExpenseEntry entry, CancellationToken ct = default) =>
            Task.FromResult<OperatingExpenseEntry?>(entry);
        public Task<bool> DeleteOperatingExpenseAsync(long id, string shopId, CancellationToken ct = default) =>
            Task.FromResult(Expenses.RemoveAll(e => e.Id == id) > 0);
        public Task<IReadOnlyList<FixedAssetItem>> GetFixedAssetsAsync(string shopId, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<FixedAssetItem>>(Assets);
        public Task<FixedAssetItem?> GetFixedAssetByIdAsync(long id, string shopId, CancellationToken ct = default) =>
            Task.FromResult(Assets.FirstOrDefault(a => a.Id == id));
        public Task<FixedAssetItem> AddFixedAssetAsync(FixedAssetItem asset, CancellationToken ct = default)
        {
            asset.Id = Assets.Count + 1;
            Assets.Add(asset);
            return Task.FromResult(asset);
        }
        public Task<FixedAssetItem?> UpdateFixedAssetAsync(FixedAssetItem asset, CancellationToken ct = default) =>
            Task.FromResult<FixedAssetItem?>(asset);
        public Task<bool> DeleteFixedAssetAsync(long id, string shopId, CancellationToken ct = default) =>
            Task.FromResult(Assets.RemoveAll(a => a.Id == id) > 0);
        public Task<IReadOnlyList<InventoryPeriodAdjustment>> GetInventoryAdjustmentsAsync(string shopId, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<InventoryPeriodAdjustment>>(InventoryAdjustments);
        public Task<InventoryPeriodAdjustment?> GetInventoryAdjustmentAsync(string shopId, int year, int month, CancellationToken ct = default) =>
            Task.FromResult(InventoryAdjustments.FirstOrDefault(a => a.Year == year && a.Month == month));
        public Task<InventoryPeriodAdjustment> UpsertInventoryAdjustmentAsync(InventoryPeriodAdjustment adjustment, CancellationToken ct = default)
        {
            InventoryAdjustments.Add(adjustment);
            return Task.FromResult(adjustment);
        }
    }
}
