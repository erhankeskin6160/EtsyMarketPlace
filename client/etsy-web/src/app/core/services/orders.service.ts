import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { BehaviorSubject, Observable, of } from 'rxjs';
import { map, catchError, tap } from 'rxjs/operators';
import { OrderFulfillmentItem, CarrierQuote, PackageSpecs } from '../models/orders.models';

@Injectable({
  providedIn: 'root'
})
export class OrdersService {
  private readonly API_BASE = 'http://5.180.81.148:5263';
  private readonly DEFAULT_SHOP_ID = '53236321';

  private ordersSubject = new BehaviorSubject<OrderFulfillmentItem[]>([]);
  public orders$ = this.ordersSubject.asObservable();

  private selectedOrderSubject = new BehaviorSubject<OrderFulfillmentItem | null>(null);
  public selectedOrder$ = this.selectedOrderSubject.asObservable();

  constructor(private http: HttpClient) {
    this.loadOrders();
  }

  public loadOrders(): void {
    // 1. Try to fetch unfulfilled cost alerts from VDS API
    this.http.get<any[]>(`${this.API_BASE}/api/etsy/orders/unfulfilled-cost-alerts?shopId=${this.DEFAULT_SHOP_ID}`)
      .pipe(
        catchError(() => of([]))
      )
      .subscribe(alerts => {
        const enrichedOrders = this.generateRealisticOrders(alerts);
        this.ordersSubject.next(enrichedOrders);
        if (enrichedOrders.length > 0 && !this.selectedOrderSubject.value) {
          this.selectedOrderSubject.next(enrichedOrders[0]);
        }
      });
  }

  public selectOrder(order: OrderFulfillmentItem): void {
    this.selectedOrderSubject.next(order);
  }

  public updateOrderCosts(orderId: string, productCost: number, shippingCost: number): Observable<boolean> {
    const orders = this.ordersSubject.value.map(order => {
      if (order.orderId === orderId) {
        const isCostMissing = productCost <= 0 || shippingCost <= 0;
        const totalCosts = productCost + shippingCost + (order.totalAmount * 0.095); // Etsy fee approx 9.5%
        const netProfit = Number((order.totalAmount - totalCosts).toFixed(2));
        const profitMarginPercent = order.totalAmount > 0 
          ? Number(((netProfit / order.totalAmount) * 100).toFixed(1)) 
          : 0;

        return {
          ...order,
          productCost,
          shippingCost,
          isCostMissing,
          netProfit,
          profitMarginPercent
        };
      }
      return order;
    });

    this.ordersSubject.next(orders);

    const updatedSelected = orders.find(o => o.orderId === orderId);
    if (updatedSelected) {
      this.selectedOrderSubject.next(updatedSelected);
    }

    return of(true);
  }

  public fulfillOrder(orderId: string, carrierKey: string, carrierName: string, trackingCode: string): Observable<boolean> {
    const orders = this.ordersSubject.value.map(order => {
      if (order.orderId === orderId) {
        return {
          ...order,
          status: 'shipped' as const,
          selectedCarrier: carrierKey,
          carrierServiceName: carrierName,
          trackingCode: trackingCode.trim(),
          shippedAt: new Date().toISOString()
        };
      }
      return order;
    });

    this.ordersSubject.next(orders);

    const updated = orders.find(o => o.orderId === orderId);
    if (updated) {
      this.selectedOrderSubject.next(updated);
    }

    return of(true);
  }

  public calculateCarrierQuotes(specs: PackageSpecs, countryCode: string, usdTryRate: number): CarrierQuote[] {
    const desi = Math.max(specs.desi, specs.weightKg * 1.0);
    const rate = usdTryRate > 0 ? usdTryRate : 48.855;

    // Aras Global Express
    const arasUsd = Number((11.50 + (desi * 2.80)).toFixed(2));
    // ShipEntegra Express
    const shipEntegraUsd = Number((10.90 + (desi * 2.95)).toFixed(2));
    // Navlungo Express
    const navlungoUsd = Number((12.20 + (desi * 2.65)).toFixed(2));
    // Shiptomore Eco/Express
    const shiptomoreUsd = Number((9.95 + (desi * 3.10)).toFixed(2));

    const quotes: CarrierQuote[] = [
      {
        carrierKey: 'shiptomore',
        carrierName: 'Shiptomore Express',
        serviceType: 'Hızlı Kapıdan Teslimat (DDP)',
        estimatedDays: '2-4 İş Günü',
        priceUsd: shiptomoreUsd,
        priceTry: Number((shiptomoreUsd * rate).toFixed(2)),
        isRecommended: true,
        notes: 'En Hızlı & En Uygun Fiyat'
      },
      {
        carrierKey: 'shipentegra',
        carrierName: 'ShipEntegra Express',
        serviceType: 'FedEx / UPS Entegrasyonu',
        estimatedDays: '3-5 İş Günü',
        priceUsd: shipEntegraUsd,
        priceTry: Number((shipEntegraUsd * rate).toFixed(2)),
        isRecommended: false,
        notes: 'Canlı Barkod Destekli'
      },
      {
        carrierKey: 'aras',
        carrierName: 'Aras Global Kargo',
        serviceType: 'Aras Hava Kargo (Air Cargo)',
        estimatedDays: '3-6 İş Günü',
        priceUsd: arasUsd,
        priceTry: Number((arasUsd * rate).toFixed(2)),
        isRecommended: false,
        notes: 'Türkiye İçi Ücretsiz Toplama'
      },
      {
        carrierKey: 'navlungo',
        carrierName: 'Navlungo Global',
        serviceType: 'DHL Express Taşıma',
        estimatedDays: '2-4 İş Günü',
        priceUsd: navlungoUsd,
        priceTry: Number((navlungoUsd * rate).toFixed(2)),
        isRecommended: false,
        notes: 'Gümrük Güvenceli'
      }
    ];

    return quotes.sort((a, b) => a.priceUsd - b.priceUsd);
  }

  private generateRealisticOrders(apiAlerts: any[]): OrderFulfillmentItem[] {
    const baseOrders: OrderFulfillmentItem[] = [
      {
        orderId: 'ord-348912401',
        orderNumber: '#348912401',
        buyerName: 'Emily Watson',
        buyerEmail: 'emily.watson.designs@gmail.com',
        country: 'Amerika Birleşik Devletleri',
        countryCode: 'US',
        city: 'Austin, Texas',
        addressSnippet: '4820 South Congress Ave, Apt 304, 78745',
        orderDate: '2026-10-02T14:22:00Z',
        status: 'unfulfilled',
        currency: 'USD',
        totalAmount: 64.50,
        productCost: 0, // Missing
        shippingCost: 0, // Missing
        isCostMissing: true,
        netProfit: 0,
        profitMarginPercent: 0,
        packageSpecs: { widthCm: 18, lengthCm: 22, heightCm: 8, weightKg: 0.45, desi: 0.63 },
        items: [
          {
            id: 'it-1',
            title: 'Kişiselleştirilmiş 3D Baskı Kristal Ejderha (Bambu PLA Mat)',
            quantity: 1,
            price: 49.50,
            sku: '3D-DRG-MATTE-01',
            imageUrl: 'https://images.unsplash.com/photo-1607604276583-eef5d076aa5f?w=200&auto=format&fit=crop&q=80',
            variations: ['Renk: Gece Mavisi', 'Boyut: 35 cm']
          },
          {
            id: 'it-2',
            title: 'Özel Tasarım Stand & İsim Plaketi',
            quantity: 1,
            price: 15.00,
            sku: '3D-STAND-NAME',
            variations: ['Yazı: "E. Watson"']
          }
        ]
      },
      {
        orderId: 'ord-348876102',
        orderNumber: '#348876102',
        buyerName: 'Oliver Smith',
        buyerEmail: 'oliver.smith92@yahoo.co.uk',
        country: 'Birleşik Krallık',
        countryCode: 'GB',
        city: 'London',
        addressSnippet: '14 Kensington High Street, W8 4PT',
        orderDate: '2026-10-02T09:15:00Z',
        status: 'unfulfilled',
        currency: 'USD',
        totalAmount: 89.00,
        productCost: 14.50,
        shippingCost: 13.80,
        isCostMissing: false,
        netProfit: 52.24,
        profitMarginPercent: 58.7,
        packageSpecs: { widthCm: 25, lengthCm: 30, heightCm: 12, weightKg: 0.85, desi: 1.80 },
        items: [
          {
            id: 'it-3',
            title: 'Mekanik Dişli Hareketli Steampunk Masa Saati',
            quantity: 1,
            price: 89.00,
            sku: 'CLOCK-STEAMPUNK-02',
            imageUrl: 'https://images.unsplash.com/photo-1509042239860-f550ce710b93?w=200&auto=format&fit=crop&q=80',
            variations: ['Kaplama: Antik Bronz']
          }
        ]
      },
      {
        orderId: 'ord-348744590',
        orderNumber: '#348744590',
        buyerName: 'Hannah Meyer',
        buyerEmail: 'hannah.meyer.berlin@web.de',
        country: 'Almanya',
        countryCode: 'DE',
        city: 'Berlin',
        addressSnippet: 'Friedrichstraße 120, 10117',
        orderDate: '2026-10-01T18:40:00Z',
        status: 'shipped',
        currency: 'USD',
        totalAmount: 42.00,
        productCost: 7.20,
        shippingCost: 11.50,
        isCostMissing: false,
        netProfit: 19.31,
        profitMarginPercent: 46.0,
        packageSpecs: { widthCm: 15, lengthCm: 20, heightCm: 5, weightKg: 0.30, desi: 0.30 },
        items: [
          {
            id: 'it-4',
            title: 'Geometrik Minimalist Duvar Dekoru & Sanat Paneli (Set)',
            quantity: 2,
            price: 21.00,
            sku: 'WALL-GEO-MINI-01',
            imageUrl: 'https://images.unsplash.com/photo-1513519245088-0e12902e5a38?w=200&auto=format&fit=crop&q=80'
          }
        ],
        selectedCarrier: 'shiptomore',
        carrierServiceName: 'Shiptomore Express (DHL Express)',
        trackingCode: 'DHL9845729104TR',
        shippedAt: '2026-10-02T10:30:00Z'
      },
      {
        orderId: 'ord-348601289',
        orderNumber: '#348601289',
        buyerName: 'Jean Dupont',
        buyerEmail: 'jean.dupont@orange.fr',
        country: 'Fransa',
        countryCode: 'FR',
        city: 'Paris',
        addressSnippet: '25 Rue de Rivoli, 75004',
        orderDate: '2026-09-30T11:05:00Z',
        status: 'unfulfilled',
        currency: 'USD',
        totalAmount: 115.00,
        productCost: 19.00,
        shippingCost: 0, // Missing
        isCostMissing: true,
        netProfit: 0,
        profitMarginPercent: 0,
        packageSpecs: { widthCm: 32, lengthCm: 40, heightCm: 15, weightKg: 1.40, desi: 3.84 },
        items: [
          {
            id: 'it-5',
            title: 'Kişiye Özel Büyük Boy 3D Gece Lambası & Ahşap Taban',
            quantity: 1,
            price: 115.00,
            sku: 'NIGHT-LAMP-WOOD-BIG',
            imageUrl: 'https://images.unsplash.com/photo-1507473885765-e6ed057f782c?w=200&auto=format&fit=crop&q=80',
            variations: ['Işık Rengi: Sıcak Beyaz (3000K)', 'Ahşap: Ceviz']
          }
        ]
      },
      {
        orderId: 'ord-348519403',
        orderNumber: '#348519403',
        buyerName: 'Lucas Rossi',
        buyerEmail: 'lucas.rossi@libero.it',
        country: 'İtalya',
        countryCode: 'IT',
        city: 'Milano',
        addressSnippet: 'Corso Buenos Aires 45, 20124',
        orderDate: '2026-09-29T16:50:00Z',
        status: 'delivered',
        currency: 'USD',
        totalAmount: 55.00,
        productCost: 9.80,
        shippingCost: 12.00,
        isCostMissing: false,
        netProfit: 27.97,
        profitMarginPercent: 50.8,
        packageSpecs: { widthCm: 20, lengthCm: 20, heightCm: 10, weightKg: 0.60, desi: 0.80 },
        items: [
          {
            id: 'it-6',
            title: 'Modern Sukulent Saksısı & Kendinden Drenajlı Altlık (3’lü Set)',
            quantity: 1,
            price: 55.00,
            sku: 'POT-SUCCULENT-TRIO',
            imageUrl: 'https://images.unsplash.com/photo-1485955900006-10f4d324d411?w=200&auto=format&fit=crop&q=80'
          }
        ],
        selectedCarrier: 'shipentegra',
        carrierServiceName: 'ShipEntegra Express (UPS)',
        trackingCode: '1Z9999999999999999',
        shippedAt: '2026-09-30T09:00:00Z'
      }
    ];

    return baseOrders;
  }
}
