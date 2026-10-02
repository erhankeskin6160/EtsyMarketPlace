export interface FinancialSummary {
  period: string;
  grossSales: number;
  etsyFees: number;
  netRevenue: number;
  productCosts: number;
  realNetProfit: number;
  bankPayoutsTotal: number;
}

export interface DailyBrief {
  shopId: string;
  shopName: string;
  todayOrders: number;
  todayGrossSalesUSD: number;
  todayGrossSalesTRY: number;
  todayNetProfitTRY: number;
  exchangeRateUsed: number;
  lastSyncAt: string;
}

export interface BankDeposit {
  id: number;
  receiptId: number;
  type: string;
  amount: number;
  net: number;
  currency: string;
  description: string;
  occurredAt: string;
  exchangeRateToTry: number;
}

export interface NavItem {
  id: string;
  title: string;
  icon: string;
  category: string;
  badge?: string;
  route: string;
}
