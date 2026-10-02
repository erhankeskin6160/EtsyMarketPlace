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
