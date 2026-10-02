export interface PaymentLedgerEntry {
  id: string;
  entryDate: string;
  transactionType: 'sale' | 'fee' | 'refund' | 'ad' | 'listing' | 'payout';
  typeDisplay: string;
  title: string;
  orderNumber?: string;
  grossAmount: number;
  feeAmount: number;
  netAmount: number;
  currency: string;
  netAmountTry: number;
  runningBalance: number;
}

export interface BankPayoutRecord {
  payoutId: string;
  initiatedDate: string;
  completedDate?: string;
  status: 'completed' | 'processing' | 'pending';
  bankName: string;
  ibanEnding: string;
  amountUsd: number;
  exchangeRate: number;
  amountTry: number;
  referenceNumber: string;
}

export interface FinancialKpiSummary {
  period: string;
  currency: string;
  grossSales: number;
  etsyFees: number;
  innerAds: number;
  offsiteAds: number;
  refunds: number;
  netRevenue: number;
  productCosts: number;
  shippingCosts: number;
  realNetProfit: number;
  profitMarginPercent: number;
  bankPayoutsTotal: number;
}

export interface ExpenseBreakdownItem {
  name: string;
  amountUsd: number;
  amountTry: number;
  percentage: number;
  color: string;
}

export interface OrderFinancialRow {
  orderDate: string;
  receiptId: string;
  orderStatus: 'completed' | 'canceled' | 'refunded';
  displayStatus: string;
  buyerName: string;
  productTitle: string;
  quantity: number;
  grandTotalUsd: number;
  etsyFeesUsd: number;
  offsiteAdFeeUsd: number;
  productCostUsd: number | null;
  netProfitUsd: number;
  exchangeRate: number;
  netProfitTry: number;
  hasCostData: boolean;
  hasInvoice: boolean;
  invoiceName?: string;
}

export interface ForecastKpiSummary {
  expectedGrossUsd: number;
  expectedGrossTry: number;
  minGrossUsd: number;
  maxGrossUsd: number;
  expectedProfitUsd: number;
  expectedProfitTry: number;
  profitMarginPct: number;
  estimatedOrders: number;
  monthlyGrowthPct: number;
  filamentKg: number;
  shippingBoxesCount: number;
}
