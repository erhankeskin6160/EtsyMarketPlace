export interface OrderItem {
  id: string;
  title: string;
  quantity: number;
  price: number;
  imageUrl?: string;
  sku?: string;
  variations?: string[];
}

export interface PackageSpecs {
  widthCm: number;
  lengthCm: number;
  heightCm: number;
  weightKg: number;
  desi: number;
}

export interface CarrierQuote {
  quoteId?: string;
  carrierKey: 'aras' | 'shipentegra' | 'navlungo' | 'shiptomore';
  carrierName: string;
  serviceType: string;
  serviceCode?: string;
  subCarrier?: string;
  logoUrl?: string;
  estimatedDays: string;
  priceUsd: number;
  priceTry: number;
  isRecommended: boolean;
  isLowestPrice?: boolean;
  isLive?: boolean;
  quoteSourceBadge?: 'Tahmini tarife' | 'Canlı teklif' | '🟢 Canlı API Teklifi' | string;
  notes?: string;
}

export interface CarrierAccountSession {
  id: 'aras' | 'shipentegra' | 'navlungo' | 'shiptomore';
  name: string;
  logoUrl: string;
  isConnected: boolean;
  statusLabel: string;
  autoLabel: string;
  portalUrl: string;
  tokenOrKey?: string;
  clientSecret?: string;
  lastUpdated?: string;
  notes?: string;
}

export interface GtipCodeItem {
  code: string;
  description: string;
  category?: string;
}

export interface OrderFulfillmentItem {
  orderId: string;
  orderNumber: string;
  buyerName: string;
  buyerEmail?: string;
  country: string;
  countryCode: string;
  city?: string;
  addressSnippet?: string;
  isAddressMissing?: boolean;
  addressWarning?: string;
  exportType?: string;
  orderDate: string;
  status: 'unfulfilled' | 'shipped' | 'delivered';
  currency: string;
  totalAmount: number;
  productCost: number;
  shippingCost: number;
  isCostMissing: boolean;
  netProfit: number;
  profitMarginPercent: number;
  packageSpecs: PackageSpecs;
  gtipCode?: string;
  gtipDescription?: string;
  invoicedWeightKg?: number;
  items: OrderItem[];
  selectedCarrier?: string;
  carrierServiceName?: string;
  trackingCode?: string;
  barcodeNumber?: string;
  shippedAt?: string;
}
