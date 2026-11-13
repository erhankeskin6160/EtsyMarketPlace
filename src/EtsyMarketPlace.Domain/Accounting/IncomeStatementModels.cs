using System;
using System.Collections.Generic;
using EtsyMarketPlace.Domain.Accounting.Entities;

namespace EtsyMarketPlace.Domain.Accounting;

public sealed record GvkTaxBracket(decimal? UpperLimitTry, decimal RatePct);

public sealed record GvkBracketSlice(
    string BracketLabel,
    decimal TaxableSliceTry,
    decimal RatePct,
    decimal TaxAmountTry);

public sealed record IncomeStatementSourceItem(
    DateTimeOffset Date,
    string Reference,
    string Description,
    string SourceKind,
    decimal AmountTry,
    decimal AmountUsd,
    bool IsTaxDeductible = true);

public sealed record TdhpAccountLine(
    string AccountCode,
    string AccountName,
    string SectionCode,
    bool IsDeduction,
    bool IsSubtotal,
    bool IsFinalResult,
    decimal AmountTry,
    decimal AmountUsd,
    decimal SignedAmountTry,
    decimal SignedAmountUsd,
    decimal ShareOfNetSalesPct,
    string? Note,
    IReadOnlyList<IncomeStatementSourceItem> Sources);

public sealed record TaxBridgeSummary(
    decimal CommercialProfit690Try,
    decimal KkegAdditions950Try,
    decimal ExportSalesSharePct,
    decimal EExportExemptionRatePct,
    decimal EExportExemptionDeductionTry,
    decimal BagkurDeductionTry,
    decimal ProfitBeforeYoungEntrepreneurTry,
    bool YoungEntrepreneurModeEnabled,
    decimal YoungEntrepreneurAnnualLimitTry,
    decimal YoungEntrepreneurPriorUsedTry,
    decimal YoungEntrepreneurAppliedInPeriodTry,
    decimal YoungEntrepreneurRemainingTry,
    decimal TaxableBaseTry,
    decimal EstimatedTax691Try,
    decimal EffectiveTaxRatePct,
    decimal TaxWithoutYoungEntrepreneurTry,
    decimal YoungEntrepreneurTaxSavingsTry,
    decimal PeriodNetProfit692Try,
    string TaxRegimeLabel,
    IReadOnlyList<GvkBracketSlice> BracketBreakdown);

public sealed record ReconciliationBridge(
    decimal EtsyContributionProfitTry,
    decimal EtsyGrossSalesTry,
    decimal EtsyDirectFeesAndAdsTry,
    decimal EtsyDirectOrderCostsTry,
    decimal SeparatedCanceledCostLossTry,
    decimal NonEtsyOperatingExpensesTry,
    decimal DepreciationExpenseTry,
    decimal FxGainLossNetTry,
    decimal ExtraordinaryNetTry,
    decimal CommercialProfit690Try,
    decimal EstimatedTax691Try,
    decimal NetProfitAfterTax692Try);

public sealed record ClosingJournalLine(
    string AccountCode,
    string AccountName,
    decimal DebitTry,
    decimal CreditTry,
    string Description);

public sealed record ClosingJournalVoucher(
    int VoucherNo,
    string Title,
    string Explanation,
    IReadOnlyList<ClosingJournalLine> Lines,
    decimal TotalDebitTry,
    decimal TotalCreditTry,
    bool IsBalanced);

public sealed record FixedAssetDepreciationLine(
    long AssetId,
    string AssetName,
    string AssetAccountCode,
    OperatingExpenseCategory ExpenseTargetCategory,
    DepreciationMethod Method,
    decimal AcquisitionCostTry,
    decimal UsefulLifeYears,
    decimal PeriodDepreciationTry,
    decimal AccumulatedDepreciationEndTry,
    decimal NetBookValueEndTry);

public sealed record IncomeStatementSection(
    string SectionCode,
    string Title,
    bool IsDeductionSection,
    decimal TotalTry,
    decimal TotalUsd,
    IReadOnlyList<TdhpAccountLine> Lines);

public sealed record IncomeStatementDocument(
    string ShopId,
    DateTimeOffset StartDate,
    DateTimeOffset EndDate,
    decimal AverageExchangeRate,
    bool LedgerOk,
    string? Warning,
    ShopAccountingProfile AppliedProfile,
    decimal GrossSalesTry,
    decimal SalesDeductionsTry,
    decimal NetSalesTry,
    decimal CostOfSalesTry,
    decimal GrossProfitTry,
    decimal OperatingExpensesTry,
    decimal OperatingProfitTry,
    decimal OtherOrdinaryIncomeTry,
    decimal OtherOrdinaryExpensesTry,
    decimal FinancingExpensesTry,
    decimal OrdinaryProfitTry,
    decimal ExtraordinaryIncomeTry,
    decimal ExtraordinaryExpensesTry,
    decimal CommercialProfit690Try,
    decimal TaxProvision691Try,
    decimal PeriodNetProfit692Try,
    decimal GrossSalesUsd,
    decimal NetSalesUsd,
    decimal GrossProfitUsd,
    decimal OperatingProfitUsd,
    decimal OrdinaryProfitUsd,
    decimal CommercialProfit690Usd,
    decimal TaxProvision691Usd,
    decimal PeriodNetProfit692Usd,
    IReadOnlyList<IncomeStatementSection> Sections,
    IReadOnlyList<TdhpAccountLine> AllLines,
    TaxBridgeSummary TaxBridge,
    ReconciliationBridge Reconciliation,
    IReadOnlyList<ClosingJournalVoucher> ClosingVouchers,
    IReadOnlyList<FixedAssetDepreciationLine> AssetDepreciations);

public sealed record AccountBucketInput(
    decimal AmountTry = 0m,
    decimal AmountUsd = 0m,
    string? Note = null,
    IReadOnlyList<IncomeStatementSourceItem>? Sources = null)
{
    public static AccountBucketInput Empty { get; } = new(0m, 0m, null, Array.Empty<IncomeStatementSourceItem>());
}

public sealed record IncomeStatementInput
{
    public string ShopId { get; init; } = string.Empty;
    public DateTimeOffset StartDate { get; init; }
    public DateTimeOffset EndDate { get; init; }
    public decimal ExchangeRate { get; init; } = 49.0m;
    public bool LedgerOk { get; init; } = true;
    public string? Warning { get; init; }
    public required ShopAccountingProfile Profile { get; init; }

    // A. Brüt Satışlar
    public AccountBucketInput DomesticSales600 { get; init; } = AccountBucketInput.Empty;
    public AccountBucketInput ExportSales601 { get; init; } = AccountBucketInput.Empty;
    public AccountBucketInput OtherSalesIncome602 { get; init; } = AccountBucketInput.Empty;

    // B. Satış İndirimleri (-)
    public AccountBucketInput SalesReturns610 { get; init; } = AccountBucketInput.Empty;
    public AccountBucketInput SalesDiscounts611 { get; init; } = AccountBucketInput.Empty;
    public AccountBucketInput OtherSalesDeductions612 { get; init; } = AccountBucketInput.Empty;

    // D. Satışların Maliyeti (-)
    public AccountBucketInput CostOfManufacturedGoods620 { get; init; } = AccountBucketInput.Empty;
    public AccountBucketInput CostOfMerchandise621 { get; init; } = AccountBucketInput.Empty;
    public AccountBucketInput CostOfServices622 { get; init; } = AccountBucketInput.Empty;

    // E. Faaliyet Giderleri (-)
    public AccountBucketInput ResearchAndDev630 { get; init; } = AccountBucketInput.Empty;
    public AccountBucketInput MarketingSelling631 { get; init; } = AccountBucketInput.Empty;
    public AccountBucketInput GeneralManagement632 { get; init; } = AccountBucketInput.Empty;

    // F. Diğer Faaliyetlerden Olağan Gelir ve Kârlar
    public AccountBucketInput InterestIncome642 { get; init; } = AccountBucketInput.Empty;
    public AccountBucketInput FxGains646 { get; init; } = AccountBucketInput.Empty;
    public AccountBucketInput OtherOrdinaryIncome649 { get; init; } = AccountBucketInput.Empty;

    // G. Diğer Faaliyetlerden Olağan Gider ve Zararlar (-)
    public AccountBucketInput CommissionExpenses653 { get; init; } = AccountBucketInput.Empty;
    public AccountBucketInput ProvisionExpenses654 { get; init; } = AccountBucketInput.Empty;
    public AccountBucketInput FxLosses656 { get; init; } = AccountBucketInput.Empty;
    public AccountBucketInput OtherOrdinaryExpenses659 { get; init; } = AccountBucketInput.Empty;

    // H. Finansman Giderleri (-)
    public AccountBucketInput ShortTermFinancing660 { get; init; } = AccountBucketInput.Empty;
    public AccountBucketInput LongTermFinancing661 { get; init; } = AccountBucketInput.Empty;

    // I. Olağandışı Gelir ve Kârlar
    public AccountBucketInput PriorPeriodIncome671 { get; init; } = AccountBucketInput.Empty;
    public AccountBucketInput ExtraordinaryIncome679 { get; init; } = AccountBucketInput.Empty;

    // J. Olağandışı Gider ve Zararlar (-)
    public AccountBucketInput IdleCapacityLoss680 { get; init; } = AccountBucketInput.Empty;
    public AccountBucketInput PriorPeriodExpense681 { get; init; } = AccountBucketInput.Empty;
    public AccountBucketInput ExtraordinaryExpenses689 { get; init; } = AccountBucketInput.Empty;

    // Vergi Köprüsü & Mutabakat Meta
    public decimal KkegAdditions950Try { get; init; }
    public decimal BagkurDeductionTry { get; init; }
    public decimal EtsyContributionProfitTry { get; init; }
    public decimal EtsyDirectFeesAndAdsTry { get; init; }
    public decimal EtsyDirectOrderCostsTry { get; init; }
    public decimal SeparatedCanceledCostLossTry { get; init; }
    public decimal NonEtsyOperatingExpensesTry { get; init; }
    public decimal DepreciationExpenseTry { get; init; }
    public IReadOnlyList<FixedAssetDepreciationLine> AssetDepreciations { get; init; } = Array.Empty<FixedAssetDepreciationLine>();
}

