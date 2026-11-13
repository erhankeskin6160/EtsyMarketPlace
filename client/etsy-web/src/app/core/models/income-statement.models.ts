export type CommissionAccountTarget = 0 | 1; // 0: 653 Komisyon Giderleri (Seçenek B), 1: 631 Pazarlama
export type ShippingPackagingCostTarget = 0 | 1; // 0: 631 Pazarlama (Seçenek A), 1: 620 SMM
export type CanceledOrderCostTarget = 0 | 1; // 0: 689 Olağandışı Zarar, 1: 620 SMM
export type InventoryValuationMethod = 0 | 1; // 0: Devamlı Envanter, 1: Aralıklı Envanter
export type TaxRegimeType = 0 | 1 | 2; // 0: Şahıs GVK + Genç Girişimci, 1: Kurumlar %25, 2: Özel Oran
export type DepreciationMethod = 0 | 1; // 0: Normal, 1: Azalan Bakiyeler
export type ExpenseFrequency = 0 | 1; // 0: Tek Seferlik, 1: Aylık Tekrarlayan

export interface GvkBracketSliceDto {
  bracketLabel: string;
  taxableSliceTry: number;
  ratePct: number;
  taxAmountTry: number;
}

export interface IncomeStatementSourceItemDto {
  date: string;
  reference: string;
  description: string;
  sourceKind: string;
  amountTry: number;
  amountUsd: number;
  isTaxDeductible: boolean;
}

export interface TdhpAccountLineDto {
  accountCode: string;
  accountName: string;
  sectionCode: string;
  isDeduction: boolean;
  isSubtotal: boolean;
  isFinalResult: boolean;
  amountTry: number;
  amountUsd: number;
  signedAmountTry: number;
  signedAmountUsd: number;
  shareOfNetSalesPct: number;
  note?: string | null;
  sources: IncomeStatementSourceItemDto[];
}

export interface IncomeStatementSectionDto {
  sectionCode: string;
  title: string;
  isDeductionSection: boolean;
  totalTry: number;
  totalUsd: number;
  lines: TdhpAccountLineDto[];
}

export interface TaxBridgeSummaryDto {
  commercialProfit690Try: number;
  kkegAdditions950Try: number;
  exportSalesSharePct: number;
  eExportExemptionRatePct: number;
  eExportExemptionDeductionTry: number;
  bagkurDeductionTry: number;
  profitBeforeYoungEntrepreneurTry: number;
  youngEntrepreneurModeEnabled: boolean;
  youngEntrepreneurAnnualLimitTry: number;
  youngEntrepreneurPriorUsedTry: number;
  youngEntrepreneurAppliedInPeriodTry: number;
  youngEntrepreneurRemainingTry: number;
  taxableBaseTry: number;
  estimatedTax691Try: number;
  effectiveTaxRatePct: number;
  taxWithoutYoungEntrepreneurTry: number;
  youngEntrepreneurTaxSavingsTry: number;
  periodNetProfit692Try: number;
  taxRegimeLabel: string;
  bracketBreakdown: GvkBracketSliceDto[];
}

export interface ReconciliationBridgeDto {
  etsyContributionProfitTry: number;
  etsyGrossSalesTry: number;
  etsyDirectFeesAndAdsTry: number;
  etsyDirectOrderCostsTry: number;
  separatedCanceledCostLossTry: number;
  nonEtsyOperatingExpensesTry: number;
  depreciationExpenseTry: number;
  fxGainLossNetTry: number;
  extraordinaryNetTry: number;
  commercialProfit690Try: number;
  estimatedTax691Try: number;
  netProfitAfterTax692Try: number;
}

export interface ClosingJournalLineDto {
  accountCode: string;
  accountName: string;
  debitTry: number;
  creditTry: number;
  description: string;
}

export interface ClosingJournalVoucherDto {
  voucherNo: number;
  title: string;
  explanation: string;
  lines: ClosingJournalLineDto[];
  totalDebitTry: number;
  totalCreditTry: number;
  isBalanced: boolean;
}

export interface FixedAssetDepreciationLineDto {
  assetId: number;
  assetName: string;
  assetAccountCode: string;
  expenseTargetCategory: number;
  method: DepreciationMethod;
  acquisitionCostTry: number;
  usefulLifeYears: number;
  periodDepreciationTry: number;
  accumulatedDepreciationEndTry: number;
  netBookValueEndTry: number;
}

export interface ShopAccountingProfileDto {
  shopId: string;
  commissionTarget: CommissionAccountTarget;
  shippingTarget: ShippingPackagingCostTarget;
  canceledCostTarget: CanceledOrderCostTarget;
  inventoryMethod: InventoryValuationMethod;
  taxRegime: TaxRegimeType;
  youngEntrepreneurModeEnabled: boolean;
  youngEntrepreneurAnnualLimitTry: number;
  youngEntrepreneurPriorUsedTry: number;
  eExportExemptionRatePct: number;
  customOrCorporateTaxRatePct: number;
  customGvkBracketsJson?: string | null;
  includeFxSpreadAndExchangeDifference: boolean;
  updatedAt?: string;
}

export interface OperatingExpenseEntryDto {
  id: number;
  shopId: string;
  entryDate: string;
  documentNo: string;
  category: number;
  description: string;
  amount: number;
  currency: string;
  exchangeRateToTry: number;
  amountTry: number;
  isTaxDeductible: boolean;
  frequency: ExpenseFrequency;
  recurringEndDate?: string | null;
}

export interface FixedAssetItemDto {
  id: number;
  shopId: string;
  assetName: string;
  assetAccountCode: string;
  expenseTargetCategory: number;
  acquisitionDate: string;
  acquisitionCostTry: number;
  salvageValueTry: number;
  usefulLifeYears: number;
  method: DepreciationMethod;
  isActive: boolean;
}

export interface InventoryPeriodAdjustmentDto {
  shopId: string;
  year: number;
  month: number;
  openingInventoryTry: number;
  grossPurchasesTry: number;
  purchaseReturnsAndDiscountsTry: number;
  closingInventoryTry: number;
  notes?: string | null;
}

export interface IncomeStatementDocumentDto {
  shopId: string;
  startDate: string;
  endDate: string;
  averageExchangeRate: number;
  ledgerOk: boolean;
  warning?: string | null;
  appliedProfile: ShopAccountingProfileDto;
  grossSalesTry: number;
  salesDeductionsTry: number;
  netSalesTry: number;
  costOfSalesTry: number;
  grossProfitTry: number;
  operatingExpensesTry: number;
  operatingProfitTry: number;
  otherOrdinaryIncomeTry: number;
  otherOrdinaryExpensesTry: number;
  financingExpensesTry: number;
  ordinaryProfitTry: number;
  extraordinaryIncomeTry: number;
  extraordinaryExpensesTry: number;
  commercialProfit690Try: number;
  taxProvision691Try: number;
  periodNetProfit692Try: number;
  grossSalesUsd: number;
  netSalesUsd: number;
  grossProfitUsd: number;
  operatingProfitUsd: number;
  ordinaryProfitUsd: number;
  commercialProfit690Usd: number;
  taxProvision691Usd: number;
  periodNetProfit692Usd: number;
  sections: IncomeStatementSectionDto[];
  allLines: TdhpAccountLineDto[];
  taxBridge: TaxBridgeSummaryDto;
  reconciliation: ReconciliationBridgeDto;
  closingVouchers: ClosingJournalVoucherDto[];
  assetDepreciations: FixedAssetDepreciationLineDto[];
}

export interface IncomeStatementResponseDto {
  statement: IncomeStatementDocumentDto;
  configuredExpenses: OperatingExpenseEntryDto[];
  fixedAssets: FixedAssetItemDto[];
  inventoryAdjustments: InventoryPeriodAdjustmentDto[];
  dataCompletenessNotes: string[];
}

export const OPERATING_EXPENSE_CATEGORY_OPTIONS: Array<{ value: number; label: string; group: string }> = [
  { value: 632, label: '632 — Genel Yönetim Gideri (Kira, Muhasebe, Elektrik, İnternet, Yazılım)', group: 'Faaliyet Gideri' },
  { value: 631, label: '631 — Pazarlama, Satış ve Dağıtım (Dış Reklam, Kutu, Etiket, Fuar)', group: 'Faaliyet Gideri' },
  { value: 630, label: '630 — Araştırma ve Geliştirme (Prototip, 3D Modelleme, Ar-Ge)', group: 'Faaliyet Gideri' },
  { value: 620, label: '620 — Satılan Mamuller Maliyeti Düzeltmesi (Toplu Filament/Reçine Sarfı)', group: 'Maliyet' },
  { value: 653, label: '653 — Komisyon Giderleri (Banka POS / Payoneer / Aracı Komisyonları)', group: 'Diğer Olağan Gider' },
  { value: 660, label: '660 — Kısa Vadeli Borçlanma / Finansman Gideri (Kredi Kartı Taksit / Kredi Faizi)', group: 'Finansman' },
  { value: 689, label: '689 — Diğer Olağandışı Gider ve Zararlar (Kayıp Kargo, Hasar, Fire)', group: 'Olağandışı Gider' },
  { value: 680, label: '680 — Çalışmayan Kısım Gider ve Zararları (Arızalı Yazıcı / Boş Kapasite)', group: 'Olağandışı Gider' },
  { value: 681, label: '681 — Önceki Dönem Gider ve Zararları', group: 'Olağandışı Gider' },
  { value: 611, label: '611 — Satış İskontoları (-)', group: 'Satış İndirimi' },
  { value: 612, label: '612 — Diğer İndirimler (-)', group: 'Satış İndirimi' },
  { value: 642, label: '642 — Faiz / Nemalandırma Geliri (+)', group: 'Diğer Gelir' },
  { value: 649, label: '649 — Diğer Olağan Gelir ve Kârlar (+)', group: 'Diğer Gelir' },
  { value: 679, label: '679 — Diğer Olağandışı Gelir ve Kârlar (+)', group: 'Olağandışı Gelir' },
  { value: 890, label: '890 — Ödenen Bağ-Kur Primi (GVK Md. 89 Vergi Matrah İndirimi)', group: 'Vergi İndirimi' }
];

