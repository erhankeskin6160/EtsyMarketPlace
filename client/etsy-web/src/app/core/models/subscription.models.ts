export type SubscriptionPlanId = 'starter' | 'growth' | 'pro' | 'enterprise';
export type BillingCycle = 'Monthly' | 'Yearly';

export interface SubscriptionPlan {
  id: SubscriptionPlanId | string;
  name: string;
  tagline: string;
  badgeText: string;
  accentColor: string;
  monthlyPriceUsd: number;
  yearlyPriceUsd: number;
  monthlyPriceTry: number;
  yearlyPriceTry: number;
  maxShops: number;
  monthlyAiTokenQuota: number;
  maxMonthlyAiListings: number;
  maxBatchQueueSize: number;
  allowedFeatures: string[];
  isPopular: boolean;
  isActive: boolean;
  displayOrder: number;
}

export interface UserSubscriptionSummary {
  subscriptionId: string;
  userId: string;
  username: string;
  role: string;
  planId: SubscriptionPlanId | string;
  planName: string;
  badgeText: string;
  accentColor: string;
  status: string;
  billingCycle: BillingCycle | string;
  startDateUtc: string;
  currentPeriodStartUtc: string;
  currentPeriodEndUtc: string;
  daysRemaining: number;
  autoRenew: boolean;
  isAdminExempt: boolean;
  maxShops: number;
  assignedShopsCount: number;
  remainingShopSlots: number;
  monthlyAiTokenQuota: number;
  usedAiTokens: number;
  remainingAiTokens: number;
  aiUsagePercentage: number;
  maxMonthlyAiListings: number;
  maxBatchQueueSize: number;
  allowedFeatures: string[];
}

export interface SubscriptionTransaction {
  id: number;
  sessionId: string;
  userId: string;
  username: string;
  planId: string;
  planName: string;
  billingCycle: string;
  amountUsd: number;
  amountTry: number;
  currency: string;
  exchangeRate: number;
  status: string;
  paymentProvider: string;
  cardMaskedNumber?: string;
  cardHolderName?: string;
  invoiceNumber: string;
  createdAtUtc: string;
  paidAtUtc?: string;
}

export interface CreateCheckoutSessionRequest {
  planId: string;
  billingCycle: BillingCycle | string;
  currency: 'USD' | 'TRY' | string;
  returnBaseUrl?: string;
}

export interface CheckoutSessionResponse {
  sessionId: string;
  checkoutUrl: string;
  mode: 'VirtualPos' | 'StripeLive' | string;
  provider: string;
  planId: string;
  planName: string;
  billingCycle: string;
  amountUsd: number;
  amountTry: number;
  currency: string;
  invoiceNumber: string;
  expiresAtUtc: string;
}

export interface ConfirmVirtualPosPaymentRequest {
  sessionId: string;
  cardHolderName: string;
  cardNumber: string;
  expiryMonth: string;
  expiryYear: string;
  cvc: string;
}

export interface ConfirmPaymentResponse {
  success: boolean;
  message: string;
  transaction: SubscriptionTransaction;
  subscription: UserSubscriptionSummary;
  user?: import('./auth.models').User;
  refreshedToken?: string;
}

export interface AdminAssignSubscriptionRequest {
  planId: string;
  billingCycle?: string;
  status?: string;
  durationDays?: number;
  customMaxShopsOverride?: number;
  customAiTokenQuotaOverride?: number;
}

export interface AdminUpdateSubscriptionPlanRequest {
  name: string;
  tagline: string;
  badgeText: string;
  monthlyPriceUsd: number;
  yearlyPriceUsd: number;
  monthlyPriceTry: number;
  yearlyPriceTry: number;
  maxShops: number;
  monthlyAiTokenQuota: number;
  maxMonthlyAiListings: number;
  maxBatchQueueSize: number;
  allowedFeatures?: string[];
  isPopular: boolean;
  isActive: boolean;
}

export interface FeatureAccessCheck {
  allowed: boolean;
  featureKey: string;
  normalizedFeatureKey: string;
  currentPlanId: string;
  requiredPlanId: string;
  reason: string;
}
