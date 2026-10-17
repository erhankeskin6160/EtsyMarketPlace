import { Injectable } from '@angular/core';
import { BehaviorSubject, Observable, of } from 'rxjs';
import { EtsyApiService } from './etsy-api.service';
import { OrderFulfillmentItem, CarrierQuote, PackageSpecs, CarrierAccountSession, GtipCodeItem } from '../models/orders.models';

const STORAGE_KEY_CARRIER_SESSIONS = 'etsy_carrier_sessions_v2';

@Injectable({
  providedIn: 'root'
})
export class OrdersService {
  private ordersSubject = new BehaviorSubject<OrderFulfillmentItem[]>([]);
  public orders$ = this.ordersSubject.asObservable();
  public ordersLoading = false;
  public ordersLoadError: string | null = null;

  private selectedOrderSubject = new BehaviorSubject<OrderFulfillmentItem | null>(null);
  public selectedOrder$ = this.selectedOrderSubject.asObservable();

  private sessionsSubject = new BehaviorSubject<CarrierAccountSession[]>(this.loadInitialSessions());
  public sessions$ = this.sessionsSubject.asObservable();

  constructor(private etsyApi: EtsyApiService) {
    this.syncCarrierSessionsFromServer();
    this.loadOrders();
  }

  /** Sunucudaki Aras Global, ShipEntegra, Navlungo, ShipToMore tokenlarını Web UI ile senkronize eder. */
  public syncCarrierSessionsFromServer(): void {
    this.etsyApi.getShippingCarrierSessions().subscribe({
      next: (res) => {
        if (!res || !Array.isArray(res.results)) return;
        const current = [...this.sessionsSubject.value];
        let changed = false;

        for (const srv of res.results) {
          const idx = current.findIndex(c => c.id === srv.carrierKey);
          if (idx < 0) continue;

          const item = { ...current[idx] };
          if (srv.bearerToken && srv.bearerToken.length > 20) {
            item.tokenOrKey = srv.bearerToken;
            item.isConnected = true;
            item.statusLabel = '🟢 Bağlı';
            if (srv.updatedAtUtc) {
              const dt = new Date(srv.updatedAtUtc);
              item.lastUpdated = dt.toLocaleDateString('tr-TR') + ' ' + dt.toLocaleTimeString('tr-TR', { hour: '2-digit', minute: '2-digit' });
            }
            current[idx] = item;
            changed = true;
          } else if (srv.carrierKey === 'aras' && !srv.isConnected) {
            // Sunucuda Aras tokenı geçersizse yerel süresi dolmuş tokenı kontrol et
            const localValid = this.validateJwt(item.tokenOrKey);
            if (!localValid.isValid || localValid.expired) {
              item.isConnected = false;
              item.statusLabel = '⚠️ Giriş Yap';
              current[idx] = item;
              changed = true;
            }
          }
        }

        if (changed) {
          this.persistSessions(current);
        }
      },
      error: () => {}
    });
  }

  /** Tarayıcı üzerinden veya telefon/şifre ile Aras Global / taşıyıcı Bearer Token'ını otomatik yakalar ve kaydeder. */
  public triggerCarrierBrowserLogin(carrierId: string, email?: string, password?: string, openBrowser: boolean = true): Observable<any> {
    return new Observable(observer => {
      this.etsyApi.connectCarrier({
        carrierKey: carrierId,
        email: email,
        password: password,
        openBrowser: openBrowser
      }).subscribe({
        next: (res) => {
          if (res && res.bearerToken) {
            const list = this.sessionsSubject.value.map(s => {
              if (s.id === carrierId) {
                return {
                  ...s,
                  isConnected: true,
                  statusLabel: '🟢 Bağlı',
                  tokenOrKey: res.bearerToken,
                  lastUpdated: new Date().toLocaleDateString('tr-TR') + ' ' + new Date().toLocaleTimeString('tr-TR', { hour: '2-digit', minute: '2-digit' })
                };
              }
              return s;
            });
            this.persistSessions(list);
          }
          observer.next(res);
          observer.complete();
        },
        error: (err) => {
          observer.error(err);
        }
      });
    });
  }

  // --- CARRIER ACCOUNT SESSIONS MANAGEMENT ---
  private getDefaultCarrierSessions(): CarrierAccountSession[] {
    return [
      {
        id: 'aras',
        name: 'Aras Global',
        logoUrl: 'assets/shipping/aras_global.png',
        isConnected: false,
        statusLabel: '⚠️ Giriş Yap',
        autoLabel: '⚡ Oturum Aç',
        portalUrl: 'https://panel.arasglobalcargo.com/auth',
        tokenOrKey: '',
        lastUpdated: '-',
        notes: 'Aras Hava Kargo API oturumunuzu güvenle bağlayın'
      },
      {
        id: 'shipentegra',
        name: 'ShipEntegra',
        logoUrl: 'assets/shipping/shipentegra.jpg',
        isConnected: true,
        statusLabel: '🟢 API Bağlı',
        autoLabel: '⚡ Oturum Aç',
        portalUrl: 'https://app.shipentegra.com/login',
        tokenOrKey: '',
        apiKey: '8501f5b8c5bbbe241d6d35eccdcdcfc3',
        apiSecret: 'f8515b70e86b0671ca653c2702b8a2295157b32b',
        lastUpdated: '14.10.2026',
        notes: 'Resmi API Key & Secret Entegrasyonu (UPS, FedEx, Widect)'
      },
      {
        id: 'navlungo',
        name: 'Navlungo',
        logoUrl: 'assets/shipping/navlungo.png',
        isConnected: false,
        statusLabel: '⚠️ Giriş Yap',
        autoLabel: '⚡ Oturum Aç',
        portalUrl: 'https://ship.navlungo.com/',
        tokenOrKey: '',
        lastUpdated: '-',
        notes: 'DHL Express rota fiyatlaması'
      },
      {
        id: 'shiptomore',
        name: 'Shiptomore',
        logoUrl: 'assets/shipping/shiptomore.png',
        isConnected: true,
        statusLabel: '🟢 API Bağlı',
        autoLabel: '⚙️ Bağlantı',
        portalUrl: 'https://dev.shiptomore.com/portal/keys',
        tokenOrKey: 'NhuCndOc1smCm0uBInrLByukAoV5JNRj',
        apiKey: 'NhuCndOc1smCm0uBInrLByukAoV5JNRj',
        clientSecret: '',
        lastUpdated: '14.10.2026',
        notes: 'Resmî REST API Entegrasyonu (dev.shiptomore.com)'
      }
    ];
  }

  private loadInitialSessions(): CarrierAccountSession[] {
    const defaults = this.getDefaultCarrierSessions();
    const saved = localStorage.getItem(STORAGE_KEY_CARRIER_SESSIONS);
    if (saved) {
      try {
        const list: CarrierAccountSession[] = JSON.parse(saved);
        let upgraded = false;
        // Upgrade any placeholder tokens containing '...' or empty and inject defaults
        list.forEach(item => {
          const def = defaults.find(d => d.id === item.id);
          if (def) {
            if (!item.apiKey && def.apiKey) { item.apiKey = def.apiKey; upgraded = true; }
            if (!item.apiSecret && def.apiSecret) { item.apiSecret = def.apiSecret; upgraded = true; }
            if (item.id === 'shipentegra' && item.apiKey && item.apiSecret) {
              item.isConnected = true;
              item.statusLabel = '🟢 API Bağlı';
              upgraded = true;
            }
          }
          if (!item.tokenOrKey || item.tokenOrKey.includes('...') || (item.id === 'aras' && !this.validateJwt(item.tokenOrKey).isValid)) {
            if (def && def.tokenOrKey) {
              item.tokenOrKey = def.tokenOrKey;
              item.isConnected = true;
              item.statusLabel = '● Bağlı';
              upgraded = true;
            }
          }
        });
        if (upgraded) {
          try { localStorage.setItem(STORAGE_KEY_CARRIER_SESSIONS, JSON.stringify(list)); } catch {}
        }
        return list;
      } catch {
        // fallback to defaults
      }
    }

    return defaults;
  }

  private persistSessions(sessions: CarrierAccountSession[]): void {
    this.sessionsSubject.next(sessions);
    try {
      localStorage.setItem(STORAGE_KEY_CARRIER_SESSIONS, JSON.stringify(sessions));
    } catch {
      // Storage fallback
    }
  }

  public updateCarrierSession(updated: Partial<CarrierAccountSession> & { id: string }): void {
    const list = this.sessionsSubject.value.map(s => {
      if (s.id === updated.id) {
        return {
          ...s,
          ...updated,
          lastUpdated: new Date().toLocaleDateString('tr-TR') + ' ' + new Date().toLocaleTimeString('tr-TR', { hour: '2-digit', minute: '2-digit' })
        };
      }
      return s;
    });
    this.persistSessions(list);
  }

  public disconnectCarrier(carrierId: string): void {
    const list = this.sessionsSubject.value.map(s => {
      if (s.id === carrierId) {
        return {
          ...s,
          isConnected: false,
          statusLabel: '⚠️ Giriş Gerekli',
          tokenOrKey: '',
          lastUpdated: new Date().toLocaleDateString('tr-TR') + ' ' + new Date().toLocaleTimeString('tr-TR', { hour: '2-digit', minute: '2-digit' })
        };
      }
      return s;
    });
    this.persistSessions(list);
  }

  public connectCarrier(carrierId: string, token: string, secret?: string, apiKey?: string, apiSecret?: string): void {
    const list = this.sessionsSubject.value.map(s => {
      if (s.id === carrierId) {
        return {
          ...s,
          isConnected: true,
          statusLabel: '🟢 Bağlı',
          tokenOrKey: token,
          clientSecret: secret || s.clientSecret,
          apiKey: apiKey || s.apiKey,
          apiSecret: apiSecret || s.apiSecret,
          lastUpdated: new Date().toLocaleDateString('tr-TR') + ' ' + new Date().toLocaleTimeString('tr-TR', { hour: '2-digit', minute: '2-digit' })
        };
      }
      return s;
    });
    this.persistSessions(list);

    // Call backend API to persist credentials in SQLite / store
    this.etsyApi.connectCarrier({
      carrierKey: carrierId,
      apiKey: apiKey,
      apiSecret: apiSecret,
      bearerToken: token
    }).subscribe({
      next: (res) => {
        if (res && res.bearerToken && !token) {
          this.updateCarrierSession({ id: carrierId as any, isConnected: true, statusLabel: '🟢 Bağlı', tokenOrKey: res.bearerToken });
        }
      },
      error: () => {}
    });
  }

  public validateJwt(token: string | null | undefined): { isValid: boolean; expired: boolean; expDate?: Date; timeLeftStr?: string; reason?: string } {
    if (!token || typeof token !== 'string') {
      return { isValid: false, expired: true, reason: 'Token girilmedi veya boş' };
    }
    const clean = token.trim();
    if (clean.includes('...') || clean.length < 30) {
      return { isValid: false, expired: true, reason: 'Token taslak veya eksik (...)' };
    }
    const parts = clean.split('.');
    if (parts.length < 2) {
      return { isValid: false, expired: true, reason: 'Geçersiz JWT formatı' };
    }
    try {
      let base64 = parts[1].replace(/-/g, '+').replace(/_/g, '/');
      while (base64.length % 4) {
        base64 += '=';
      }
      const jsonStr = decodeURIComponent(
        Array.prototype.map.call(atob(base64), (c: string) => {
          return '%' + ('00' + c.charCodeAt(0).toString(16)).slice(-2);
        }).join('')
      );
      const payload = JSON.parse(jsonStr);

      if (!payload || typeof payload !== 'object') {
        return { isValid: false, expired: true, reason: 'JWT payload içeriği okunamadı' };
      }

      if (payload.exp === undefined || payload.exp === null) {
        return { isValid: true, expired: false, timeLeftStr: 'Süresiz / Uzun Vadeli Token' };
      }

      const expMs = Number(payload.exp) * 1000;
      const nowMs = Date.now();
      const expDate = new Date(expMs);

      if (nowMs >= expMs) {
        const expStr = expDate.toLocaleString('tr-TR', { day: '2-digit', month: '2-digit', year: 'numeric', hour: '2-digit', minute: '2-digit' });
        return { 
          isValid: false, 
          expired: true, 
          expDate, 
          reason: `Süresi doldu (${expStr})` 
        };
      }

      const diffMin = Math.max(1, Math.round((expMs - nowMs) / 60000));
      const hours = Math.floor(diffMin / 60);
      const mins = diffMin % 60;
      const timeLeftStr = hours > 0 ? `${hours} saat ${mins} dakika` : `${mins} dakika`;

      return { 
        isValid: true, 
        expired: false, 
        expDate, 
        timeLeftStr 
      };
    } catch {
      return { isValid: false, expired: true, reason: 'JWT çözümlenirken hata oluştu' };
    }
  }

  public isCarrierSessionLive(carrierId: string): boolean {
    const session = this.sessionsSubject.value.find(s => s.id === carrierId);
    if (!session || !session.isConnected) {
      return false;
    }
    if (carrierId === 'shipentegra') {
      if (session.apiKey && session.apiSecret) return true;
      return Boolean(session.tokenOrKey && !session.tokenOrKey.includes('...') && session.tokenOrKey.length > 10);
    }
    if (carrierId === 'shiptomore') {
      if (session.apiKey && session.clientSecret) return true;
      return Boolean(session.tokenOrKey && !session.tokenOrKey.includes('...') && session.tokenOrKey.length > 10);
    }
    if (carrierId === 'aras') {
      if (!session.tokenOrKey) return false;
      const val = this.validateJwt(session.tokenOrKey);
      return val.isValid && !val.expired;
    }
    return Boolean(session.tokenOrKey && !session.tokenOrKey.includes('...') && session.tokenOrKey.length > 10);
  }

  // --- GTIP / HS CODE CATALOG & SEARCH ENGINE ---
  private readonly gtipCatalog: (GtipCodeItem & { keywords: string })[] = [
    { code: '3926400000', category: '3D Baskı & Figür', description: 'Plastikten / PLA / Reçineden 3D Baskı Heykelcik, Biblolar ve Süs Eşyası', keywords: '3d print pla resin figure figurine statue decor miniature dragon bust sculpture' },
    { code: '3926909990', category: '3D Baskı & Aparat', description: 'Plastikten Diğer Eşyalar (Stand, Tutucu, Organizer, Yedek Parça)', keywords: 'plastic holder stand mount organizer clip bracket custom 3d headset controller' },
    { code: '9503007000', category: 'Oyuncak & Hobi', description: 'Plastik Oyuncaklar, Eklemli 3D Figürler (Articulated Fidget / Toy)', keywords: 'toy fidget articulated flexi game puzzle dice tower' },
    { code: '9405210000', category: 'Aydınlatma & Lamba', description: 'LED Masa Lambası, Gece Lambası, Lithophane / 3D Baskı Aydınlatma', keywords: 'lamp led light nightlight lithophane lampshade chandelier' },
    { code: '6912002510', category: 'Seramik & Kupa', description: 'Seramik / Porselen Kahve Kupası, Bardak ve Sofra Eşyası (Mug)', keywords: 'mug cup ceramic coffee tea tableware bowl plate' },
    { code: '4420190000', category: 'Ahşap & Dekor', description: 'Ahşap Dekoratif Süs Eşyası, Lazer Kesim Duvar Dekoru ve Heykelcik', keywords: 'wood wooden laser cut sign wall art plaque box' },
    { code: '7117199000', category: 'Takı & Aksesuar', description: 'Bijuteri, Kolye, Küpe, Bileklik ve Metal/Plastik Takılar', keywords: 'jewelry necklace earring bracelet ring pendant charm keychain' },
    { code: '7113110000', category: 'Gümüş Takı', description: '925 Ayar Gümüş Takı ve Mücevherat (Kolye, Yüzük, Küpe)', keywords: 'silver 925 sterling ring necklace jewelry gold' },
    { code: '6109100012', category: 'Tekstil & Giyim', description: 'Pamuklu Baskılı Tişört, Sweatshirt ve Örme Giyim Eşyası', keywords: 't-shirt shirt tee hoodie sweatshirt apparel cotton clothing' },
    { code: '4202229000', category: 'Çanta & Cüzdan', description: 'Bez Çanta (Tote Bag), Deri/Tekstil Cüzdan ve Kılıf', keywords: 'bag tote wallet pouch purse leather case backpack' },
    { code: '4911910040', category: 'Tablo & Poster', description: 'Basılı Sanat Posteri, Kanvas Tablo, Fotoğraf ve Sticker', keywords: 'poster print canvas art sticker decal paper bookmark card' },
    { code: '8306290000', category: 'Metal Dekor', description: 'Adi Metalden Duvar Sanatı, Dekoratif Metal Tablo ve Heykelcik', keywords: 'metal wall art iron steel decor sculpture sign' },
    { code: '3406000000', category: 'Mum & Kokulu Ürün', description: 'El Yapımı Soya Mumu, Dekoratif Kokulu Mum', keywords: 'candle soy wax scented aroma holder' },
    { code: '6307909891', category: 'Ev Tekstili', description: 'Dekoratif Yastık Kılıfı, Makrome ve Dokuma Ev Tekstili', keywords: 'pillow cushion macrame tapestry textile woven rug towel' },
    { code: '3924104000', category: 'Mutfak & Sunum', description: 'Plastik Mutfak Gereçleri, Kurabiye Kalıbı (Cookie Cutter)', keywords: 'cookie cutter kitchen coaster tray planter pot vase' }
  ];

  public searchGtip(term: string): GtipCodeItem[] {
    let clean = (term || '').trim().toLowerCase();
    const dash = clean.indexOf(' - ');
    if (dash > 0) {
      clean = clean.substring(0, dash).trim();
    }
    if (!clean) {
      return this.gtipCatalog.slice(0, 8).map(({ keywords, ...rest }) => rest);
    }

    const digits = clean.replace(/\D/g, '');
    const tokens = clean.split(/\s+/).filter(t => t.length >= 2);

    return this.gtipCatalog
      .filter(item => {
        if (digits.length >= 2 && item.code.includes(digits)) return true;
        if (item.description.toLowerCase().includes(clean)) return true;
        if ((item.category ?? '').toLowerCase().includes(clean)) return true;
        if (item.keywords.includes(clean)) return true;
        return tokens.some(tok =>
          item.description.toLowerCase().includes(tok) ||
          (item.category ?? '').toLowerCase().includes(tok) ||
          item.keywords.includes(tok)
        );
      })
      .map(({ keywords, ...rest }) => rest);
  }

  public searchGtipRemote(term: string): Observable<GtipCodeItem[]> {
    let clean = (term || '').trim();
    const dash = clean.indexOf(' - ');
    if (dash > 0) {
      clean = clean.substring(0, dash).trim();
    }
    return new Observable<GtipCodeItem[]>(observer => {
      const localMatches = this.searchGtip(clean);
      this.etsyApi.searchHsCodes(clean, 20).subscribe({
        next: (resp) => {
          const apiItems: GtipCodeItem[] = (resp?.results || []).map((r: any) => ({
            code: r.code,
            category: r.category || 'Gümrük Tarife',
            description: r.description || `${r.formattedCode || r.code} Gümrük Kodu`
          }));
          const merged = [...localMatches];
          apiItems.forEach(a => {
            if (!merged.some(m => m.code === a.code)) {
              merged.push(a);
            }
          });
          observer.next(merged.slice(0, 20));
          observer.complete();
        },
        error: () => {
          observer.next(localMatches);
          observer.complete();
        }
      });
    });
  }

  public suggestGtipForOrder(order: OrderFulfillmentItem | null): GtipCodeItem {
    if (!order || !order.items || order.items.length === 0) {
      return { code: '3926400000', category: '3D Baskı & Figür', description: 'Plastikten / PLA / Reçineden 3D Baskı Heykelcik, Biblolar ve Süs Eşyası' };
    }
    const combinedTitle = order.items.map(i => i.title || '').join(' ').toLowerCase();
    const match = this.searchGtip(combinedTitle)[0];
    return match || { code: '3926400000', category: '3D Baskı & Figür', description: 'Plastikten / PLA / Reçineden 3D Baskı Heykelcik, Biblolar ve Süs Eşyası' };
  }

  // --- ORDER RETRIEVAL & QUEUE MANAGEMENT ---
  public loadOrders(shopIdOverride?: string): void {
    this.ordersLoading = true;
    this.ordersLoadError = null;
    const shopId = shopIdOverride || this.etsyApi.activeShopId();

    this.etsyApi.getFulfillmentQueue(shopId, 60)
      .subscribe({
        next: (resp) => {
          this.ordersLoading = false;
          const rawItems = resp?.results || [];

          if (rawItems.length === 0) {
            this.fallbackToCostAlerts(shopId);
            return;
          }

          const liveOrders: OrderFulfillmentItem[] = rawItems.map((item: any) => {
            const hasAddress = Boolean(
              item.streetAddress && item.streetAddress.trim().length > 0 &&
              item.city && item.city.trim().length > 0
            );
            const addressParts = [
              item.streetAddress,
              item.secondAddress,
              item.city,
              item.state,
              item.postalCode,
              item.countryName || item.countryCode
            ].filter((p: any) => Boolean(p && String(p).trim().length > 0));

            const total = Number(item.totalPrice || item.totalAmount || 0);
            const pCost = Number(item.productCostUsd ?? item.productCost ?? 0);
            const sCost = Number(item.shippingCostUsd ?? item.shippingCost ?? 0);
            const netProfit = Number((total - (pCost + sCost + (total * 0.095))).toFixed(2));
            const profitMargin = total > 0 ? Number(((netProfit / total) * 100).toFixed(1)) : 0;

            const orderItems = (item.items || []).map((it: any, idx: number) => ({
              id: it.id || `it-${item.receiptId}-${idx}`,
              title: it.title || 'Etsy Ürünü',
              quantity: it.quantity || 1,
              price: it.price || 0,
              sku: it.sku || `SKU-${it.listingId || item.receiptId}`,
              imageUrl: it.imageUrl || ''
            }));

            const widthCm = Number(item.widthCm || item.packageSpecs?.widthCm || 15);
            const lengthCm = Number(item.lengthCm || item.packageSpecs?.lengthCm || 20);
            const heightCm = Number(item.heightCm || item.packageSpecs?.heightCm || 10);
            const weightKg = Number(item.weightKg || item.totalWeightKg || item.packageSpecs?.weightKg || 0.4);
            const desi = Number(((widthCm * lengthCm * heightCm) / 5000).toFixed(2));
            const invoicedWeightKg = Number(Math.max(weightKg, desi).toFixed(2));

            const rawGtip = item.gtipCode || (item.items && item.items[0]?.hsCode) || '';
            const gtipMatch = rawGtip ? this.searchGtip(rawGtip)[0] : undefined;

            const tracking = item.trackingCode || item.trackingNumber || '';
            const isShipped = item.status === 'shipped' || item.status === 'Shipped' || item.status === 'Fulfilled' || Boolean(tracking);

            return {
              orderId: String(item.receiptId),
              orderNumber: `#${item.receiptId}`,
              buyerName: item.buyerName || 'Etsy Alıcısı',
              buyerEmail: item.buyerEmail || '',
              phone: item.phone || '',
              streetAddress: item.streetAddress || '',
              secondAddress: item.secondAddress || '',
              city: item.city || '',
              state: item.state || '',
              postalCode: item.postalCode || '',
              country: item.countryName || item.countryCode || 'Amerika Birleşik Devletleri',
              countryCode: item.countryCode || 'US',
              formattedAddress: addressParts.join(', '),
              addressSnippet: addressParts.join(', '),
              isAddressMissing: !hasAddress,
              addressWarning: !hasAddress ? 'Alıcı sokak veya şehir bilgisi eksik. Lütfen düzenleyin.' : undefined,
              iossNumber: item.iossNumber || '',
              isVatCollected: Boolean(item.isVatCollected || item.iossNumber),
              exportType: item.exportType || (item.countryCode === 'TR' ? 'Yurtiçi' : 'Mikro İhracat (ETGB)'),
              orderDate: item.orderDate || '',
              status: isShipped ? 'shipped' : 'unfulfilled',
              currency: item.currency || 'USD',
              totalAmount: total,
              productCost: pCost,
              shippingCost: sCost,
              isCostMissing: pCost <= 0 || sCost <= 0,
              netProfit: netProfit,
              profitMarginPercent: profitMargin,
              packageSpecs: { widthCm, lengthCm, heightCm, weightKg, desi },
              invoicedWeightKg,
              gtipCode: rawGtip,
              gtipDescription: item.gtipDescription || gtipMatch?.description || '',
              items: orderItems.length > 0 ? orderItems : [
                {
                  id: `it-${item.receiptId}`,
                  title: 'Etsy Siparişi',
                  quantity: 1,
                  price: total,
                  sku: 'LIVE-SKU'
                }
              ],
              selectedCarrier: item.selectedCarrier || '',
              carrier: item.selectedCarrier || '',
              carrierServiceName: item.carrierServiceName || '',
              trackingCode: tracking,
              trackingNumber: tracking,
              barcodeNumber: tracking || String(item.receiptId),
              labelUrl: item.labelUrl || ''
            };
          });

          this.ordersSubject.next(liveOrders);
          const selected = this.selectedOrderSubject.value;
          this.selectedOrderSubject.next((selected && liveOrders.find(order => order.orderId === selected.orderId)) || liveOrders[0] || null);
        },
        error: () => {
          this.fallbackToCostAlerts(shopId);
        }
      });
  }

  private fallbackToCostAlerts(shopId: string): void {
    this.etsyApi.getUnfulfilledCostAlerts(shopId)
      .subscribe({
        next: (apiAlerts) => {
          this.ordersLoading = false;
          const liveOrders: OrderFulfillmentItem[] = (apiAlerts || []).map(a => ({
            orderId: a.orderId,
            orderNumber: `#${a.orderId}`,
            buyerName: 'Etsy Siparişi #' + a.orderId,
            buyerEmail: '',
            country: 'Amerika Birleşik Devletleri',
            countryCode: 'US',
            city: '',
            addressSnippet: '',
            isAddressMissing: true,
            addressWarning: 'Alıcı teslimat adresi için lütfen Adres Düzenle düğmesini kullanın.',
            exportType: 'Mikro İhracat (ETGB)',
            orderDate: a.createdAt || '',
            status: 'unfulfilled',
            currency: a.currency || 'USD',
            totalAmount: a.orderTotal || 0,
            productCost: a.productCost || 0,
            shippingCost: a.shippingCost || 0,
            isCostMissing: (a.productCost || 0) <= 0 || (a.shippingCost || 0) <= 0,
            netProfit: Number(((a.orderTotal || 0) - ((a.productCost || 0) + (a.shippingCost || 0) + ((a.orderTotal || 0) * 0.095))).toFixed(2)),
            profitMarginPercent: a.orderTotal > 0 ? Number(((((a.orderTotal || 0) - ((a.productCost || 0) + (a.shippingCost || 0))) / a.orderTotal) * 100).toFixed(1)) : 0,
            packageSpecs: { widthCm: 15, lengthCm: 20, heightCm: 10, weightKg: 0.4, desi: 0.6 },
            invoicedWeightKg: 0.4,
            gtipCode: '',
            gtipDescription: '',
            items: [
              {
                id: `it-${a.orderId}`,
                title: a.reason || 'Sipariş ayrıntısı',
                quantity: 1,
                price: a.orderTotal || 0,
                sku: 'LIVE-SKU'
              }
            ]
          }));

          this.ordersSubject.next(liveOrders);
          const selected = this.selectedOrderSubject.value;
          this.selectedOrderSubject.next((selected && liveOrders.find(order => order.orderId === selected.orderId)) || liveOrders[0] || null);
        },
        error: () => {
          this.ordersLoading = false;
          this.ordersLoadError = 'Siparişler API’den yüklenemedi.';
          this.ordersSubject.next([]);
          this.selectedOrderSubject.next(null);
        }
      });
  }

  public executeCreateShipment(payload: {
    receiptId: number;
    carrierKey: string;
    serviceCode: string;
    serviceType?: string;
    subCarrier?: string;
    weightKg: number;
    widthCm: number;
    lengthCm: number;
    heightCm: number;
    hsCode?: string;
    cargoPrice?: number;
    notifyEtsy?: boolean;
    recipientAddress?: any;
  }): Observable<any> {
    const shopId = this.etsyApi.activeShopId();
    return this.etsyApi.createCarrierShipment({
      ...payload,
      shopId
    });
  }

  public updateOrderAddress(receiptId: number, address: Partial<OrderFulfillmentItem>): Observable<any> {
    const list = this.ordersSubject.value.map(o => {
      if (String(o.orderId) === String(receiptId)) {
        const updated = { ...o, ...address };
        const hasAddress = Boolean(
          updated.streetAddress && updated.streetAddress.trim().length > 0 &&
          updated.city && updated.city.trim().length > 0
        );
        updated.isAddressMissing = !hasAddress;
        if (hasAddress) {
          updated.addressWarning = undefined;
        }
        const addressParts = [
          updated.streetAddress,
          updated.secondAddress,
          updated.city,
          updated.state,
          updated.postalCode,
          updated.countryName || updated.countryCode || updated.country
        ].filter(p => Boolean(p && String(p).trim().length > 0));
        updated.formattedAddress = addressParts.join(', ');
        updated.addressSnippet = addressParts.join(', ');
        return updated;
      }
      return o;
    });

    this.ordersSubject.next(list);
    const selected = list.find(o => String(o.orderId) === String(receiptId));
    if (selected) this.selectedOrderSubject.next(selected);

    return this.etsyApi.saveOrderAddress(receiptId, selected || address);
  }

  public aiParseAddress(rawAddress: string): Observable<any> {
    return this.etsyApi.aiParseAddress(rawAddress);
  }

  public uploadOrdersCsv(file: File): Observable<any> {
    const formData = new FormData();
    formData.append('file', file, file.name);
    return this.etsyApi.uploadOrdersCsv(formData);
  }

  public selectOrder(order: OrderFulfillmentItem): void {
    this.selectedOrderSubject.next(order);
  }

  public updateOrderGtip(orderId: string, gtipCode: string, gtipDesc?: string): void {
    const list = this.ordersSubject.value.map(o => {
      if (o.orderId === orderId) {
        return { ...o, gtipCode, gtipDescription: gtipDesc || o.gtipDescription };
      }
      return o;
    });
    this.ordersSubject.next(list);
    const updated = list.find(o => o.orderId === orderId);
    if (updated) this.selectedOrderSubject.next(updated);
  }

  public updatePackageSpecs(orderId: string, specs: PackageSpecs): void {
    const desi = Number(((specs.widthCm * specs.lengthCm * specs.heightCm) / 5000).toFixed(2));
    const invoicedWeight = Math.max(desi, specs.weightKg);

    const list = this.ordersSubject.value.map(o => {
      if (o.orderId === orderId) {
        return {
          ...o,
          packageSpecs: { ...specs, desi },
          invoicedWeightKg: Number(invoicedWeight.toFixed(2))
        };
      }
      return o;
    });
    this.ordersSubject.next(list);
    const updated = list.find(o => o.orderId === orderId);
    if (updated) this.selectedOrderSubject.next(updated);
  }

  public updateOrderCosts(orderId: string, productCost: number, shippingCost: number): Observable<boolean> {
    const orders = this.ordersSubject.value.map(order => {
      if (order.orderId === orderId) {
        const isCostMissing = productCost <= 0 || shippingCost <= 0;
        const totalCosts = productCost + shippingCost + (order.totalAmount * 0.095);
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
    const updated = orders.find(o => o.orderId === orderId);
    if (updated) {
      this.selectedOrderSubject.next(updated);
      return new Observable<boolean>(observer => {
        this.persistOrderFulfillmentMeta(updated).subscribe({
          next: () => {
            observer.next(true);
            observer.complete();
          },
          error: () => {
            observer.next(false);
            observer.complete();
          }
        });
      });
    }
    return of(true);
  }

  public persistOrderFulfillmentMeta(order: OrderFulfillmentItem): Observable<any> {
    const receiptId = Number(String(order.orderId).replace(/\D/g, ''));
    if (!receiptId || receiptId <= 0) {
      return of({ success: false });
    }
    const payload = {
      receiptId,
      buyerName: order.buyerName || '',
      buyerEmail: order.buyerEmail || '',
      phone: order.phone || '',
      streetAddress: order.streetAddress || '',
      secondAddress: order.secondAddress || '',
      city: order.city || '',
      state: order.state || '',
      postalCode: order.postalCode || '',
      countryCode: order.countryCode || 'US',
      countryName: order.country || '',
      iossNumber: order.iossNumber || '',
      productCostUsd: Number(order.productCost || 0),
      shippingCostUsd: Number(order.shippingCost || 0),
      weightKg: Number(order.packageSpecs?.weightKg || 0.4),
      widthCm: Number(order.packageSpecs?.widthCm || 15),
      lengthCm: Number(order.packageSpecs?.lengthCm || 20),
      heightCm: Number(order.packageSpecs?.heightCm || 10),
      gtipCode: order.gtipCode || '',
      selectedCarrier: order.selectedCarrier || '',
      carrierServiceName: order.carrierServiceName || '',
      trackingCode: order.trackingNumber || order.trackingCode || '',
      labelUrl: order.labelUrl || ''
    };
    return this.etsyApi.saveOrderFulfillmentMeta(receiptId, payload);
  }

  public fulfillOrder(orderId: string, carrierKey: string, carrierName: string, trackingCode: string, barcodeNumber?: string): Observable<boolean> {
    const orders = this.ordersSubject.value.map(order => {
      if (order.orderId === orderId) {
        return {
          ...order,
          status: 'shipped' as const,
          selectedCarrier: carrierKey,
          carrier: carrierName,
          carrierServiceName: carrierName,
          trackingCode: trackingCode.trim(),
          trackingNumber: trackingCode.trim(),
          barcodeNumber: barcodeNumber || order.barcodeNumber || trackingCode.trim(),
          shippedAt: new Date().toISOString()
        };
      }
      return order;
    });

    this.ordersSubject.next(orders);
    const updated = orders.find(o => o.orderId === orderId);
    if (updated) {
      this.selectedOrderSubject.next(updated);
      this.persistOrderFulfillmentMeta(updated).subscribe({ next: () => {}, error: () => {} });
    }
    return of(true);
  }

  public fetchLiveCarrierQuotes(
    specs: PackageSpecs,
    countryCode: string,
    usdTryRate: number,
    postalCode?: string,
    city?: string,
    state?: string
  ): Observable<{ quotes: CarrierQuote[]; warningMessage?: string }> {
    const logoMap: Record<string, string> = {
      aras: 'assets/shipping/aras_global.png',
      shipentegra: 'assets/shipping/shipentegra.jpg',
      navlungo: 'assets/shipping/navlungo.png',
      shiptomore: 'assets/shipping/shiptomore.png'
    };

    return new Observable(observer => {
      this.etsyApi.getCarrierQuotes({
        countryCode: countryCode || 'US',
        postalCode: postalCode || '10001',
        city: city || '',
        state: state || '',
        weightKg: specs.weightKg || 0.4,
        widthCm: specs.widthCm || 15,
        lengthCm: specs.lengthCm || 20,
        heightCm: specs.heightCm || 10,
        usdTryRate: usdTryRate > 0 ? usdTryRate : 36.25
      }).subscribe({
        next: (resp) => {
          const rawQuotes = resp?.quotes || [];
          if (rawQuotes.length === 0) {
            observer.next({
              quotes: this.calculateCarrierQuotes(specs, countryCode, usdTryRate),
              warningMessage: resp?.warningMessage
            });
            observer.complete();
            return;
          }

          const mapped: CarrierQuote[] = rawQuotes.map((q: any, idx: number) => ({
            quoteId: q.serviceCode || `${q.carrierKey}-${idx}`,
            carrierKey: q.carrierKey,
            carrierName: q.carrierName,
            serviceType: q.serviceType,
            subCarrier: q.subCarrier || q.carrierName,
            logoUrl: logoMap[q.carrierKey] || 'assets/shipping/shipentegra.jpg',
            estimatedDays: q.estimatedDays || '3-6 İş Günü',
            priceUsd: Number(q.priceUsd || 0),
            priceTry: Number(q.priceTry || 0),
            isRecommended: Boolean(q.isRecommended),
            isLowestPrice: Boolean(q.isLowestPrice),
            isLive: Boolean(q.isLive),
            quoteSourceBadge: q.quoteSourceBadge || (q.isLive ? 'Canlı teklif' : 'Tahmini tarife')
          }));

          observer.next({
            quotes: mapped,
            warningMessage: resp?.warningMessage
          });
          observer.complete();
        },
        error: () => {
          observer.next({
            quotes: this.calculateCarrierQuotes(specs, countryCode, usdTryRate)
          });
          observer.complete();
        }
      });
    });
  }

  public calculateCarrierQuotes(specs: PackageSpecs, countryCode: string, usdTryRate: number): CarrierQuote[] {
    const rate = usdTryRate > 0 ? usdTryRate : 36.25;
    const desi = Number(((specs.widthCm * specs.lengthCm * specs.heightCm) / 5000).toFixed(2));
    const billable = Math.max(desi, specs.weightKg || 0.40);
    const weightStep = Math.max(1.0, Math.ceil(billable * 2.0) / 2.0);
    const extraUnits = Math.max(0, (weightStep - 1.0) / 0.5);
    const cleanCountry = (countryCode || 'US').trim().toUpperCase();
    const isEu = ['DE', 'FR', 'IT', 'ES', 'NL', 'BE', 'AT', 'PL', 'SE', 'DK', 'FI', 'GB', 'UK'].includes(cleanCountry);

    const quotes: CarrierQuote[] = [];

    // ==========================================
    // 1. ARAS GLOBAL (2 TEKLİF)
    // ==========================================
    const isArasLive = this.isCarrierSessionLive('aras');
    let arasWidectUsd = Number((13.13 + (extraUnits * 2.40)).toFixed(2));
    let arasUpsUsd = Number((21.16 + (extraUnits * 3.80)).toFixed(2));
    if (isEu) {
      arasWidectUsd = Number((arasWidectUsd * 0.85).toFixed(2));
      arasUpsUsd = Number((arasUpsUsd * 0.90).toFixed(2));
    }

    quotes.push({
      quoteId: 'aras-widect-eco',
      carrierKey: 'aras',
      carrierName: 'Aras Global',
      serviceType: 'Widect Eco Express',
      subCarrier: 'Widect',
      logoUrl: 'assets/shipping/aras_global.png',
      estimatedDays: '7-10 Gün',
      priceUsd: arasWidectUsd,
      priceTry: Number((arasWidectUsd * rate).toFixed(2)),
      isRecommended: false,
      isLive: isArasLive,
      quoteSourceBadge: isArasLive ? 'Canlı teklif' : 'Tahmini tarife',
      notes: isArasLive ? 'Canlı Aras Global API Fiyatlandırması (Widect Eco Express)' : 'Tahmini tarife (Sözleşmeli Aras Hub)'
    });

    quotes.push({
      quoteId: 'aras-ups-express',
      carrierKey: 'aras',
      carrierName: 'Aras Global',
      serviceType: 'UPS Express',
      subCarrier: 'UPS',
      logoUrl: 'assets/shipping/aras_global.png',
      estimatedDays: '2-4 Gün',
      priceUsd: arasUpsUsd,
      priceTry: Number((arasUpsUsd * rate).toFixed(2)),
      isRecommended: false,
      isLive: isArasLive,
      quoteSourceBadge: isArasLive ? 'Canlı teklif' : 'Tahmini tarife',
      notes: isArasLive ? 'Canlı Aras Global API Fiyatlandırması (UPS Express)' : 'Hızlı UPS Hattı & Doğrudan Teslimat'
    });

    // ==========================================
    // 2. SHIPENTEGRA (6 TEKLİF)
    // ==========================================
    const isShipEntegraLive = this.isCarrierSessionLive('shipentegra');
    let seEkoPlus = Number((12.96 + (extraUnits * 2.10)).toFixed(2));
    let seSmartExpress = Number((19.01 + (extraUnits * 2.80)).toFixed(2));
    let seWidect = Number((19.55 + (extraUnits * 2.90)).toFixed(2));
    let seExpedited = Number((22.40 + (extraUnits * 3.10)).toFixed(2));
    let seExpress = Number((24.80 + (extraUnits * 3.30)).toFixed(2));
    let seUpsExpress = Number((28.50 + (extraUnits * 3.60)).toFixed(2));

    if (isEu) {
      seEkoPlus = Number((seEkoPlus * 0.90).toFixed(2));
      seSmartExpress = Number((seSmartExpress * 0.90).toFixed(2));
    }

    quotes.push({
      quoteId: 'se-eko-plus',
      carrierKey: 'shipentegra',
      carrierName: 'ShipEntegra',
      serviceCode: 'shipentegra-amerika-eko-plus',
      serviceType: 'Amerika Eko Plus',
      subCarrier: 'ShipEntegra',
      logoUrl: 'assets/shipping/shipentegra.jpg',
      estimatedDays: '3-6 iş günü',
      priceUsd: seEkoPlus,
      priceTry: Number((seEkoPlus * rate).toFixed(2)),
      isRecommended: false,
      isLowestPrice: true,
      isLive: isShipEntegraLive,
      quoteSourceBadge: isShipEntegraLive ? 'Canlı teklif' : 'Tahmini tarife',
      notes: 'Amerika içi geri iade ücretsiz'
    });

    quotes.push({
      quoteId: 'se-smart-express',
      carrierKey: 'shipentegra',
      carrierName: 'ShipEntegra',
      serviceCode: 'shipentegra-smart-express',
      serviceType: 'Smart Express',
      subCarrier: 'ShipEntegra',
      logoUrl: 'assets/shipping/shipentegra.jpg',
      estimatedDays: '2-5 iş günü',
      priceUsd: seSmartExpress,
      priceTry: Number((seSmartExpress * rate).toFixed(2)),
      isRecommended: false,
      isLive: isShipEntegraLive,
      quoteSourceBadge: isShipEntegraLive ? 'Canlı teklif' : 'Tahmini tarife',
      notes: '100 $\'a kadar sigortalı'
    });

    quotes.push({
      quoteId: 'se-widect',
      carrierKey: 'shipentegra',
      carrierName: 'ShipEntegra',
      serviceCode: 'shipentegra-widect',
      serviceType: 'Widect',
      subCarrier: 'ShipEntegra',
      logoUrl: 'assets/shipping/shipentegra.jpg',
      estimatedDays: '4-9 iş günü',
      priceUsd: seWidect,
      priceTry: Number((seWidect * rate).toFixed(2)),
      isRecommended: false,
      isLive: isShipEntegraLive,
      quoteSourceBadge: isShipEntegraLive ? 'Canlı teklif' : 'Tahmini tarife'
    });

    quotes.push({
      quoteId: 'se-expedited',
      carrierKey: 'shipentegra',
      carrierName: 'ShipEntegra',
      serviceCode: 'shipentegra-expedited',
      serviceType: 'Expedited',
      subCarrier: 'ShipEntegra',
      logoUrl: 'assets/shipping/shipentegra.jpg',
      estimatedDays: '3-5 iş günü',
      priceUsd: seExpedited,
      priceTry: Number((seExpedited * rate).toFixed(2)),
      isRecommended: false,
      isLive: isShipEntegraLive,
      quoteSourceBadge: isShipEntegraLive ? 'Canlı teklif' : 'Tahmini tarife'
    });

    quotes.push({
      quoteId: 'se-express',
      carrierKey: 'shipentegra',
      carrierName: 'ShipEntegra',
      serviceCode: 'shipentegra-express',
      serviceType: 'Express',
      subCarrier: 'ShipEntegra',
      logoUrl: 'assets/shipping/shipentegra.jpg',
      estimatedDays: '2-4 iş günü',
      priceUsd: seExpress,
      priceTry: Number((seExpress * rate).toFixed(2)),
      isRecommended: false,
      isLive: isShipEntegraLive,
      quoteSourceBadge: isShipEntegraLive ? 'Canlı teklif' : 'Tahmini tarife'
    });

    quotes.push({
      quoteId: 'se-ups-express',
      carrierKey: 'shipentegra',
      carrierName: 'ShipEntegra',
      serviceCode: 'shipentegra-ups-express',
      serviceType: 'Ups Express',
      subCarrier: 'ShipEntegra',
      logoUrl: 'assets/shipping/shipentegra.jpg',
      estimatedDays: '1-4 iş günü',
      priceUsd: seUpsExpress,
      priceTry: Number((seUpsExpress * rate).toFixed(2)),
      isRecommended: false,
      isLive: isShipEntegraLive,
      quoteSourceBadge: isShipEntegraLive ? 'Canlı teklif' : 'Tahmini tarife'
    });

    // ==========================================
    // 3. NAVLUNGO (4 TEKLİF)
    // ==========================================
    const isNavlungoLive = this.isCarrierSessionLive('navlungo');
    const b = billable;
    const navWidect = Number((10.50 + (b * 7.55)).toFixed(2));
    const navFedEx = Number((14.50 + (b * 10.42)).toFixed(2));
    const navUpsExpress = Number((24.00 + (b * 16.67)).toFixed(2));
    const navUpsSaver = Number((27.50 + (b * 18.92)).toFixed(2));

    quotes.push({
      quoteId: 'nav-widect',
      carrierKey: 'navlungo',
      carrierName: 'Navlungo',
      serviceType: 'Widect',
      subCarrier: 'Widect',
      logoUrl: 'assets/shipping/navlungo.png',
      estimatedDays: '4-8 Gün',
      priceUsd: navWidect,
      priceTry: Number((navWidect * rate).toFixed(2)),
      isRecommended: false,
      isLive: isNavlungoLive,
      quoteSourceBadge: isNavlungoLive ? 'Canlı teklif' : 'Tahmini tarife'
    });

    quotes.push({
      quoteId: 'nav-fedex',
      carrierKey: 'navlungo',
      carrierName: 'Navlungo',
      serviceType: 'FedEx Priority',
      subCarrier: 'FedEx',
      logoUrl: 'assets/shipping/navlungo.png',
      estimatedDays: '2-4 Gün',
      priceUsd: navFedEx,
      priceTry: Number((navFedEx * rate).toFixed(2)),
      isRecommended: false,
      isLive: isNavlungoLive,
      quoteSourceBadge: isNavlungoLive ? 'Canlı teklif' : 'Tahmini tarife'
    });

    quotes.push({
      quoteId: 'nav-ups-express',
      carrierKey: 'navlungo',
      carrierName: 'Navlungo',
      serviceType: 'UPS Express',
      subCarrier: 'UPS',
      logoUrl: 'assets/shipping/navlungo.png',
      estimatedDays: '1-3 Gün',
      priceUsd: navUpsExpress,
      priceTry: Number((navUpsExpress * rate).toFixed(2)),
      isRecommended: false,
      isLive: isNavlungoLive,
      quoteSourceBadge: isNavlungoLive ? 'Canlı teklif' : 'Tahmini tarife'
    });

    quotes.push({
      quoteId: 'nav-ups-saver',
      carrierKey: 'navlungo',
      carrierName: 'Navlungo',
      serviceType: 'UPS Saver',
      subCarrier: 'UPS',
      logoUrl: 'assets/shipping/navlungo.png',
      estimatedDays: '2-4 Gün',
      priceUsd: navUpsSaver,
      priceTry: Number((navUpsSaver * rate).toFixed(2)),
      isRecommended: false,
      isLive: isNavlungoLive,
      quoteSourceBadge: isNavlungoLive ? 'Canlı teklif' : 'Tahmini tarife'
    });

    // ==========================================
    // 4. SHIPTOMORE (2 TEKLİF: 1 CANLI, 1 TAHMİNİ)
    // ==========================================
    const isShiptomoreLive = this.isCarrierSessionLive('shiptomore');
    const stmDdp = Number((11.81 + (extraUnits * 1.50)).toFixed(2));
    const stmFedEx = Number((18.55 + (extraUnits * 2.20)).toFixed(2));

    quotes.push({
      quoteId: 'stm-air-priority',
      carrierKey: 'shiptomore',
      carrierName: 'Shiptomore',
      serviceType: 'Air Priority (DDP)',
      subCarrier: 'Shiptomore',
      logoUrl: 'assets/shipping/shiptomore.png',
      estimatedDays: '2-4 Gün',
      priceUsd: stmDdp,
      priceTry: Number((stmDdp * rate).toFixed(2)),
      isRecommended: true,
      isLive: isShiptomoreLive,
      quoteSourceBadge: isShiptomoreLive ? 'Canlı teklif' : 'Tahmini tarife',
      notes: 'Hızlı Kapıdan Teslimat (DDP Gümrük Dahil)'
    });

    quotes.push({
      quoteId: 'stm-fedex-priority',
      carrierKey: 'shiptomore',
      carrierName: 'Shiptomore',
      serviceType: 'FedEx Priority Express',
      subCarrier: 'FedEx',
      logoUrl: 'assets/shipping/shiptomore.png',
      estimatedDays: '2-4 Gün',
      priceUsd: stmFedEx,
      priceTry: Number((stmFedEx * rate).toFixed(2)),
      isRecommended: false,
      isLive: isShiptomoreLive,
      quoteSourceBadge: isShiptomoreLive ? 'Canlı teklif' : 'Tahmini tarife',
      notes: 'Doğrudan FedEx Entegrasyonu'
    });

    // Identify lowest price
    let minPrice = Math.min(...quotes.map(q => q.priceUsd));
    quotes.forEach(q => {
      if (q.priceUsd === minPrice) {
        q.isLowestPrice = true;
      }
    });

    return quotes.sort((a, b) => a.priceUsd - b.priceUsd);
  }
}
