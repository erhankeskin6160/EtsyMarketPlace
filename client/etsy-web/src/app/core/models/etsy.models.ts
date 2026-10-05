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
  date: string;
  healthScore: number;
  healthStatus: string;
  pendingShipments: number;
  dailyGrossSales: number;
  dailyNetProfit: number;
  bankPayouts: number;
  unfulfilledCostAlertCount: number;
  dailyGrossSalesTRY?: number | null;
  dailyNetProfitTRY?: number | null;
  exchangeRateUsed?: number | null;
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
