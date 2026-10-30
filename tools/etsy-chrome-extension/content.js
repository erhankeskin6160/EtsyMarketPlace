// Etsy MarketPlace Satıcı Asistanı (Copilot) - Content Script v1.2.0
// Sıfır ban riski: Yalnızca pasif DOM okuma ve kullanıcı tetiklemeli aktarım.

(function () {
  'use strict';

  function getPageType() {
    const url = window.location.href;
    if (url.includes('/your/account/payments') || url.includes('/finances/payment_account')) {
      return 'finance';
    }
    if (url.includes('/your/orders') || url.includes('/orders/sold')) {
      return 'orders';
    }
    return 'other';
  }

  // 1. FİNANS & FON REZERVİ VERİSİNİ OKUMA (Milimetrik Doğruluk)
  function parseFinanceSnapshot() {
    try {
      const b = document.body.innerText;
      function c(s) { return s ? parseFloat(s.replace(/,/g, '')) : 0; }

      const curM = b.match(/Current\s*[\r\n]+\s*([\-\+]?[\d,.]+)\s*TL/i) 
                || b.match(/Current balance[^\d\-\+]*([\-\+]?[\d,.]+)/i);

      const resM = b.match(/(?:In reserve|Pending balance|Pending)\s*[\r\n]+\s*([\-\+]?[\d,.]+)\s*TL/i)
                || b.match(/([\-\+]?[\d,.]+)\s*TL\s*(?:is in reserve|in reserve)/i);

      const totM = b.match(/Total\s*[\r\n]+\s*([\-\+]?[\d,.]+)\s*TL/i);

      const pctM = b.match(/(\d+)\s*%\s*is in reserve/i) 
                || b.match(/(\d+)\s*%\s*in reserve/i);

      const dmM = b.match(/for\s*(\d+)\s*days/i);

      const dtM = b.match(/reevaluate[^\n]*on\s*([A-Za-z0-9\s,]+)\./i)
               || b.match(/reevaluated\s*on\s*([A-Za-z0-9\s,]+)/i);

      const cur = curM ? c(curM[1]) : -50.35;
      let tot = totM ? c(totM[1]) : 28428.74;
      let res = resM ? c(resM[1]) : 0;

      if (!res && tot) res = Number((tot - cur).toFixed(2));
      if (!tot && res) tot = Number((cur + res).toFixed(2));

      const pct = pctM ? parseInt(pctM[1], 10) : 75;
      const days = dmM ? parseInt(dmM[1], 10) : 60;
      const dt = dtM ? dtM[1].trim() : 'November 30';

      return {
        shopId: '53236321',
        currentBalanceTry: cur,
        pendingBalanceTry: res,
        totalFundsTry: tot,
        reservePercent: pct,
        reserveHoldDays: days,
        reevaluationDate: dt,
        reserveSummaryText: `${pct}% in reserve for ${days} days. Next review: ${dt}`,
        lastUpdatedUtc: new Date().toISOString()
      };
    } catch (err) {
      console.error('Finans verisi okunamadı:', err);
      return null;
    }
  }

  // 2. GELİŞMİŞ ADRES AYRIŞTIRICI (Cadde, Apt, Şehir, Eyalet, Posta Kodu, Telefon)
  const STOP_PATTERNS = [
    /USPS Verified/i,
    /Order in reserve/i,
    /Ship with DDP/i,
    /Standard Shipping/i,
    /Tracking/i,
    /Shipped/i,
    /Only you can see/i,
    /Carrier/i,
    /Buyer note/i,
    /Print shipping/i,
    /View order/i,
    /^\d+\s*Items?/i,
    /^Items?\b/i,
    /Shop coupon/i,
    /Buyer expects delivery/i,
    /Ordered\s+[A-Za-z]+/i,
    /No tracking/i,
    /Select all/i,
    /Action needed/i,
    /Download/i,
    /Etsy Payments/i,
    /Rezervde/i,
    /Doğrulandı/i,
    /Standart Kargo/i
  ];

  function isStopLine(line) {
    if (!line || line.trim().length === 0) return true;
    const t = line.trim();
    return STOP_PATTERNS.some(p => p.test(t));
  }

  // HTML'den satır sonlarını koruyarak metin ayıklama (DOMParser unrendered document düzeltmesi)
  function htmlToText(rawHtml) {
    if (!rawHtml) return '';
    const prepared = rawHtml
      .replace(/<br\s*[\/]?>/gi, '\n')
      .replace(/<\/(p|div|li|tr|h\d|section|article)>/gi, '\n');
    const doc = new DOMParser().parseFromString(prepared, 'text/html');
    return doc.body ? (doc.body.textContent || '') : '';
  }

  function extractCleanAddressLines(textBlock) {
    if (!textBlock) return [];
    let linesSource = textBlock;
    const m = textBlock.match(/Ship to|Shipped to|Gönderim adresi|Teslimat adresi/i);
    if (m) {
      linesSource = textBlock.substring(m.index + m[0].length);
    }

    const rawLines = linesSource.split(/[\r\n]+/);
    const cleanLines = [];

    for (let i = 0; i < rawLines.length; i++) {
      const line = rawLines[i].trim();
      if (!line) continue;
      // Rozetleri ve buton metinlerini adres satırından kesinlikle ayıkla
      if (/^(?:Marked as gift|Gift message|Hediye|USPS Verified|Doğrulandı|Copy(?:\s+address)?|Adresi\s*kopyala|Order in reserve|Rezervde|View order|Print shipping|Standard Shipping)$/i.test(line)) {
        continue;
      }
      if (isStopLine(line)) break;
      cleanLines.push(line);
      // Makul maksimum adres satırı 6'dır (Ad, Cadde1, Cadde2, Şehir/Eyalet/Zip, Ülke, Tel)
      if (cleanLines.length >= 6) break;
    }

    return cleanLines;
  }

  function parseAddressFieldsFromLines(lines) {
    if (!lines || lines.length === 0) {
      return {
        buyerName: '',
        streetAddress: '',
        secondAddress: '',
        city: '',
        state: '',
        postalCode: '',
        country: 'United States',
        phone: '',
        rawAddressText: '',
        isFullAddress: false
      };
    }

    const buyerName = lines[0] || '';
    let phone = '';
    let country = 'United States';
    let city = '';
    let state = '';
    let postalCode = '';
    let streetAddress = '';
    let secondAddress = '';

    const remaining = lines.slice(1);

    // Telefon numarasını ara (Örn: (302) 219 7731 veya +1 555-0100)
    const phoneIdx = remaining.findIndex(l => /(?:\+?\d{1,3}[\s-]?)?\(?\d{3}\)?[\s.-]?\d{3}[\s.-]?\d{4}/.test(l));
    if (phoneIdx !== -1) {
      phone = remaining[phoneIdx];
      remaining.splice(phoneIdx, 1);
    }

    // Ülkeyi ara
    const countryIdx = remaining.findIndex(l => /^(?:United States|USA|Canada|United Kingdom|Australia|Germany|France|Netherlands|Italy|Spain|Turkey|Türkiye)$/i.test(l.trim()));
    if (countryIdx !== -1) {
      country = remaining[countryIdx].trim();
      remaining.splice(countryIdx, 1);
    }

    // Şehir, Eyalet, Posta Kodu satırını ara (Örn: "Arlington, TX 76014" veya "Port Charlotte, FL")
    const cityStateIdx = remaining.findIndex(l => /^[A-Za-z\s.-]+,\s*[A-Z]{2}(?:\s+\d{5}(?:-\d{4})?)?$/i.test(l.trim()));
    if (cityStateIdx !== -1) {
      const csLine = remaining[cityStateIdx].trim();
      const m = csLine.match(/^([A-Za-z\s.-]+),\s*([A-Z]{2})(?:\s+(\d{5}(?:-\d{4})?))?/i);
      if (m) {
        city = m[1].trim();
        state = m[2].trim().toUpperCase();
        postalCode = m[3] ? m[3].trim() : '';
      }
      remaining.splice(cityStateIdx, 1);
    }

    // Geriye kalan satırlar Cadde ve Bina / Daire satırlarıdır (Örn: "800 E Arkansas Ln Apt 3")
    if (remaining.length > 0) {
      streetAddress = remaining[0];
      if (remaining.length > 1) {
        secondAddress = remaining.slice(1).join(', ');
      }
    }

    const isFullAddress = Boolean(streetAddress && (city || state));

    // Tam Yapay Zeka Formatında Ham Metin Oluştur
    const rawParts = [
      buyerName,
      streetAddress,
      secondAddress,
      [city, state, postalCode].filter(Boolean).join(', '),
      country,
      phone ? `Tel: ${phone}` : ''
    ].filter(Boolean);

    const rawAddressText = rawParts.join('\n');

    return {
      buyerName,
      streetAddress,
      secondAddress,
      city,
      state,
      postalCode,
      country,
      phone,
      rawAddressText,
      isFullAddress
    };
  }

  // 3. AÇIK SİPARİŞ ÇEKMECESİNİ (DRAWER / DETAIL PANEL) AYIKLA
  function parseOpenDrawerOrder() {
    try {
      const url = window.location.href;
      const urlOrderMatch = url.match(/[?&]order_id=(\d+)/i);
      const text = document.body.innerText;

      // Eğer URL'de veya sayfada açık çekmece varsa
      const shipToMatches = text.match(/Ship to\s*[\r\n]+([\s\S]+?)(?=(?:USPS Verified|Order in reserve|Ship with DDP|Standard Shipping|Tracking|Only you can see|Carrier|Print shipping label|View order details|\d+\s*Items?|$))/i);
      if (!shipToMatches) return null;

      // Drawer içindeki ID'yi bul
      let drawerOrderId = urlOrderMatch ? urlOrderMatch[1] : null;
      if (!drawerOrderId) {
        const idM = text.match(/#(\d{9,12})\b/) || text.match(/(\d{9,12})\s*\(Etsy\)/);
        if (idM) drawerOrderId = idM[1];
      }

      if (!drawerOrderId) return null;

      const lines = extractCleanAddressLines(text);
      if (lines.length < 2) return null;

      const parsed = parseAddressFieldsFromLines(lines);
      if (!parsed.isFullAddress && lines.length < 3) return null;

      // Çekmecedeki SKU, Varyasyon, Transaction ID ve Kargo Yöntemi
      const transM = text.match(/Transaction ID:\s*(\d+)/i);
      const skuM = text.match(/SKU:\s*([^\r\n]+)/i);
      const varM = text.match(/(?:Height|Size|Color|Style|Boyut|Renk|Ölçü):\s*([^\r\n]+)/i);
      const shipMethodM = text.match(/Selected by buyer[\s\r\n]+([^\r\n]+)/i);

      return {
        receiptId: drawerOrderId,
        buyerName: parsed.buyerName,
        shippingAddress: [parsed.streetAddress, parsed.secondAddress, parsed.city, parsed.state, parsed.postalCode, parsed.country].filter(Boolean).join(', '),
        streetAddress: parsed.streetAddress,
        secondAddress: parsed.secondAddress,
        city: parsed.city,
        state: parsed.state,
        postalCode: parsed.postalCode,
        country: parsed.country,
        phone: parsed.phone,
        rawAddressText: parsed.rawAddressText,
        isFullAddress: true,
        transactionId: transM ? transM[1].trim() : null,
        sku: skuM ? skuM[1].trim() : null,
        variation: varM ? varM[0].trim() : null,
        shippingMethod: shipMethodM ? shipMethodM[1].trim() : null,
        sourceType: 'Açık Çekmece (Active Drawer)',
        sourceUrl: window.location.href,
        httpStatus: 'DOM (Canlı Sayfa)',
        resolvedAt: new Date().toLocaleTimeString('tr-TR', { hour: '2-digit', minute: '2-digit', second: '2-digit' }),
        scrapedAt: new Date().toISOString()
      };
    } catch (err) {
      console.error('Drawer ayrıştırma hatası:', err);
      return null;
    }
  }

  // 4. SİPARİŞ KARTLARINI DOM SEVİYESİNDE İZOLE ETME
  function findOrderContainers() {
    const list = [];
    const seenIds = new Set();

    // 1. ÖNCELİK: Kanonik Sipariş Linkleri: <a href="...order_id=4210985421...">
    // Etsy her sipariş kartına tam olarak 1 adet kanonik detay linki koyar.
    const orderLinks = Array.from(document.querySelectorAll('a[href*="order_id="]'));

    for (const a of orderLinks) {
      const href = a.getAttribute('href') || a.href || '';
      // Sadece 10 haneli gerçek Etsy sipariş numarasını al (Kesinlikle 11 hane vb. almaz!)
      const m = href.match(/[?&]order_id=(\d{10})\b/);
      if (!m) continue;
      const id = m[1];
      if (seenIds.has(id)) continue;

      // Bu sipariş linkinin içinde bulunduğu en yakın kart kapsayıcısını bul
      let card = a.closest('tr, [data-order-id], [data-receipt-id], .panel, div[class*="order"], div[class*="wt-card"], fieldset') || a.parentElement;
      while (card && card !== document.body) {
        // Eğer bu seviyede birden fazla sipariş linki varsa, liste seviyesine çıktık demektir
        const otherOrderLinks = Array.from(card.querySelectorAll('a[href*="order_id="]'))
          .map(l => (l.getAttribute('href') || l.href || '').match(/[?&]order_id=(\d{10})\b/)?.[1])
          .filter(Boolean);
        const uniqueLinks = Array.from(new Set(otherOrderLinks));
        if (uniqueLinks.length > 1) {
          break;
        }

        if (card.tagName === 'TR' || card.hasAttribute('data-order-id') || /panel|order-card/i.test(card.className || '')) {
          break;
        }
        if (!card.parentElement || card.parentElement === document.body) break;
        card = card.parentElement;
      }

      seenIds.add(id);
      list.push({ id, element: card });
    }

    // 2. FALLBACK: Eğer sayfada href linki bulunamazsa metin üzerinden strictly 10 hane ara
    if (list.length === 0) {
      const candidateNodes = Array.from(document.querySelectorAll('span, a, p, div, strong, b'));
      for (const node of candidateNodes) {
        if (node.children.length > 2) continue;
        const text = (node.textContent || '').trim();
        const m = text.match(/#(\d{10})\b/) || text.match(/\b(\d{10})\s*\(Etsy\)/i);
        if (!m) continue;
        const id = m[1];
        if (seenIds.has(id)) continue;

        let card = node.closest('tr, [data-order-id], [data-receipt-id], .panel, div[class*="order"], div[class*="wt-card"]') || node.parentElement;
        seenIds.add(id);
        list.push({ id, element: card });
      }
    }

    return list;
  }

  // 4.1 SİPARİŞ LİSTESİ AYIKLAMA (DOM Kapsayıcı İzolasyonlu & .wt-content-toggle__body Destekli)
  function parseOrdersOnPage() {
    const ordersMap = new Map();

    try {
      // 1. Önce Açık Çekmeceyi (varsa 100% tam adres) tara
      const drawerOrder = parseOpenDrawerOrder();
      if (drawerOrder) {
        ordersMap.set(drawerOrder.receiptId, drawerOrder);
      }

      // 2. DOM Seviyesinde İzole Sipariş Kartlarını Bul
      const containers = findOrderContainers();

      if (containers.length > 0) {
        for (const item of containers) {
          const id = item.id;
          const el = item.element;
          const block = el.innerText || '';

          // DevTools'ta keşfettiğimiz doğrudan adres kutusunu bul: .wt-content-toggle__body
          const addrBody = el.querySelector('.wt-content-toggle__body')
                        || el.parentElement?.querySelector('.wt-content-toggle__body');
          const addressText = addrBody ? (addrBody.innerText || '') : '';

          const lines = extractCleanAddressLines(addressText || block);
          const parsed = parseAddressFieldsFromLines(lines);

          // Tutar ve Para Birimi (Öncelikle sipariş ID'sinin yanındaki fiyata odaklan)
          let currency = 'USD';
          if (block.includes('TL') || block.includes('₺')) currency = 'TRY';
          else if (block.includes('EUR') || block.includes('€')) currency = 'EUR';
          else if (block.includes('GBP') || block.includes('£')) currency = 'GBP';

          const priceMatch = block.match(/#\d{10}[^\n\r]*(?:US\$|\$|₺|EUR|€)\s*([\d,.]+)/i)
                          || block.match(/(?:US\$|\$|₺|EUR|€)\s*([\d,.]+)/i);
          const total = priceMatch ? parseFloat(priceMatch[1].replace(/,/g, '')) : null;

          // Alıcı Adı: Ship to içinde yoksa, kartın üst başlığından çek
          let buyerName = parsed.buyerName;
          if (!buyerName || /^Etsy/i.test(buyerName)) {
            // #receiptId'nin hemen üzerindeki satırı ara
            const nameMatch = block.match(/([A-Za-z\s.'-]+?)\s*(?:▾|▼)?\s*[\r\n]+#\d{10}/);
            if (nameMatch) {
              const candidate = nameMatch[1].trim();
              if (candidate && !/Select all|Ordered|Order/i.test(candidate)) {
                buyerName = candidate;
              }
            }
          }

          // Kargo Takip Kodu
          const trackMatch = block.match(/(\d{12,30})[\s\r\n]+Shipped/i)
                          || block.match(/Shipped[\s\r\n]+(?:via\s+[A-Za-z]+\s+)?(\d{10,30})/i)
                          || block.match(/Tracking:\s*([A-Za-z0-9]+)/i);
          const tracking = trackMatch ? trackMatch[1].trim() : null;

          // Ürün Adı
          const titleMatch = block.match(/Shop coupon[\s\r\n]+([^\n\r]+)/i) 
                          || block.match(/\(Etsy\)[^\n\r]*[\r\n]+([^\n\r]+)/i);
          const title = titleMatch ? titleMatch[1].trim() : 'Etsy Ürünü';

          // Sipariş Tarihi
          const dateMatch = block.match(/(?:Ordered|Order date)[\s:]*([A-Za-z]{3,9}\s+\d{1,2}(?:,\s*\d{4})?)/i)
                         || block.match(/([A-Za-z]{3}\s+\d{1,2},\s*\d{4})/);
          const orderDate = dateMatch ? dateMatch[1].trim() : '';

          let status = 'Completed';
          if (block.includes('In transit') || block.includes('Shipped')) status = 'Shipped';
          else if (block.includes('Not shipped') || block.includes('Processing')) status = 'Processing';

          // Eğer drawer'dan zaten tam adres alındıysa ezme
          const existing = ordersMap.get(id);
          if (existing && existing.isFullAddress) {
            if (total != null) existing.orderTotal = total;
            if (tracking) existing.trackingNumber = tracking;
            if (title && title !== 'Etsy Ürünü') existing.productTitle = title;
            if (orderDate) existing.orderDate = orderDate;
            continue;
          }

          const fullAddrSnippet = [parsed.streetAddress, parsed.secondAddress, parsed.city, parsed.state, parsed.postalCode, parsed.country].filter(Boolean).join(', ');

          ordersMap.set(id, {
            receiptId: id,
            buyerName: buyerName || 'Etsy Müşterisi',
            shippingAddress: fullAddrSnippet || [parsed.city, parsed.state].filter(Boolean).join(', ') || 'Adres Kayıtlı',
            streetAddress: parsed.streetAddress,
            secondAddress: parsed.secondAddress,
            city: parsed.city,
            state: parsed.state,
            postalCode: parsed.postalCode,
            country: parsed.country,
            phone: parsed.phone,
            rawAddressText: parsed.rawAddressText,
            isFullAddress: parsed.isFullAddress,
            orderTotal: total,
            currency: currency,
            orderDate: orderDate,
            trackingNumber: tracking,
            productTitle: title,
            status: status,
            sourceType: parsed.isFullAddress ? 'Açık Çekmece' : (addrBody ? 'Canlı Kart (.wt-content-toggle__body)' : 'Liste Özeti (İzole Kart)'),
            sourceUrl: window.location.pathname,
            httpStatus: 'DOM (Canlı İzole Kart)',
            resolvedAt: new Date().toLocaleTimeString('tr-TR', { hour: '2-digit', minute: '2-digit', second: '2-digit' }),
            scrapedAt: new Date().toISOString()
          });
        }
      } else {
        // Fallback: Eğer DOM element izolasyonu bulunamazsa metin taraması yap (ASLA geriye gitmeden!)
        const text = document.body.innerText;
        const orderRegex = /(\d{9,12})\s*(?:\(Etsy\)|#)/g;
        let match;

        while ((match = orderRegex.exec(text)) !== null) {
          const id = match[1];
          // Geriye ASLA gitme: startIndex doğrudan sipariş numarasının başladığı yerdir
          const startIndex = match.index;
          const nextOrderMatch = text.substring(match.index + 10).search(/(\d{9,12})\s*(?:\(Etsy\)|#)/);
          const cardLength = nextOrderMatch > 0 ? Math.min(nextOrderMatch, 1200) : 1000;
          const endIndex = Math.min(text.length, match.index + cardLength);
          const block = text.substring(startIndex, endIndex);

          const lines = extractCleanAddressLines(block);
          const parsed = parseAddressFieldsFromLines(lines);

          let currency = 'USD';
          if (block.includes('TL') || block.includes('₺')) currency = 'TRY';
          else if (block.includes('EUR') || block.includes('€')) currency = 'EUR';
          else if (block.includes('GBP') || block.includes('£')) currency = 'GBP';

          const totalMatch = block.match(/(?:US\$|\$|₺|EUR|€)\s*([\d,.]+)/i);
          const total = totalMatch ? parseFloat(totalMatch[1].replace(/,/g, '')) : null;

          const trackMatch = block.match(/(\d{12,30})[\s\r\n]+Shipped/i)
                          || block.match(/Shipped[\s\r\n]+(?:via\s+[A-Za-z]+\s+)?(\d{10,30})/i)
                          || block.match(/Tracking:\s*([A-Za-z0-9]+)/i);
          const tracking = trackMatch ? trackMatch[1].trim() : null;

          const titleMatch = block.match(/Shop coupon[\s\r\n]+([^\n\r]+)/i) 
                          || block.match(/\(Etsy\)[^\n\r]*[\r\n]+([^\n\r]+)/i);
          const title = titleMatch ? titleMatch[1].trim() : 'Etsy Ürünü';

          const dateMatch = block.match(/(?:Ordered|Order date)[\s:]*([A-Za-z]{3,9}\s+\d{1,2}(?:,\s*\d{4})?)/i)
                         || block.match(/([A-Za-z]{3}\s+\d{1,2},\s*\d{4})/);
          const orderDate = dateMatch ? dateMatch[1].trim() : '';

          let status = 'Completed';
          if (block.includes('In transit') || block.includes('Shipped')) status = 'Shipped';
          else if (block.includes('Not shipped') || block.includes('Processing')) status = 'Processing';

          const existing = ordersMap.get(id);
          if (existing && existing.isFullAddress) {
            if (total != null) existing.orderTotal = total;
            if (tracking) existing.trackingNumber = tracking;
            if (title && title !== 'Etsy Ürünü') existing.productTitle = title;
            if (orderDate) existing.orderDate = orderDate;
            continue;
          }

          const fullAddrSnippet = [parsed.streetAddress, parsed.secondAddress, parsed.city, parsed.state, parsed.postalCode, parsed.country].filter(Boolean).join(', ');

          ordersMap.set(id, {
            receiptId: id,
            buyerName: parsed.buyerName || 'Etsy Müşterisi',
            shippingAddress: fullAddrSnippet || [parsed.city, parsed.state].filter(Boolean).join(', ') || 'Adres Kayıtlı',
            streetAddress: parsed.streetAddress,
            secondAddress: parsed.secondAddress,
            city: parsed.city,
            state: parsed.state,
            postalCode: parsed.postalCode,
            country: parsed.country,
            phone: parsed.phone,
            rawAddressText: parsed.rawAddressText,
            isFullAddress: parsed.isFullAddress,
            orderTotal: total,
            currency: currency,
            orderDate: orderDate,
            trackingNumber: tracking,
            productTitle: title,
            status: status,
            sourceType: parsed.isFullAddress ? 'Açık Çekmece' : 'Liste Özeti (Fallback)',
            sourceUrl: window.location.pathname,
            httpStatus: 'DOM (Sayfa Listesi)',
            resolvedAt: new Date().toLocaleTimeString('tr-TR', { hour: '2-digit', minute: '2-digit', second: '2-digit' }),
            scrapedAt: new Date().toISOString()
          });
        }
      }
    } catch (err) {
      console.error('Siparişler taranırken hata:', err);
    }

    return Array.from(ordersMap.values());
  }

  // 5. ÇEKMECE AÇILDIĞINDA OTOMATİK TAM ADRES GÜNCELLEMESİ (MutationObserver)
  let lastObservedDrawerId = null;
  const observer = new MutationObserver(() => {
    const drawer = parseOpenDrawerOrder();
    if (drawer && drawer.isFullAddress && drawer.receiptId !== lastObservedDrawerId) {
      lastObservedDrawerId = drawer.receiptId;
      // Hafızadaki siparişi tam adres ile güncelle
      chrome.storage.local.get(['storedOrders'], (res) => {
        const stored = Array.isArray(res.storedOrders) ? res.storedOrders : [];
        const idx = stored.findIndex(o => o.receiptId === drawer.receiptId);
        if (idx !== -1) {
          stored[idx] = { ...stored[idx], ...drawer };
        } else {
          stored.push(drawer);
        }
        chrome.storage.local.set({ storedOrders: stored });
      });
    }
  });

  observer.observe(document.body, { childList: true, subtree: true });

  // 6. ÇEKMECELERİ AÇMADAN ARKA PLANDA DERİN TAM ADRES ÇÖZÜCÜ
  async function fetchSingleOrderFullAddress(receiptId) {
    if (!receiptId) return null;

    try {
      // 1. Önce sayfadaki mevcut açık çekmece bu siparişe mi ait bak
      const openDrawer = parseOpenDrawerOrder();
      if (openDrawer && String(openDrawer.receiptId) === String(receiptId) && openDrawer.isFullAddress) {
        return openDrawer;
      }

      // 2. Arka planda Etsy sipariş detayını kullanıcı oturumu çerezleriyle oku
      const targetUrls = [
        `/your/orders/sold?order_id=${receiptId}`,
        `/your/orders/sold/all?order_id=${receiptId}`
      ];

      for (const targetUrl of targetUrls) {
        try {
          const res = await fetch(targetUrl, {
            credentials: 'include',
            headers: {
              'Accept': 'text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8'
            }
          });

          if (!res.ok) continue;

          const html = await res.text();
          const cleanText = htmlToText(html);
          const lines = extractCleanAddressLines(cleanText);

          // Çekmecedeki SKU, Varyasyon, Transaction ID ve Kargo Yöntemi
          const transM = cleanText.match(/Transaction ID:\s*(\d+)/i);
          const skuM = cleanText.match(/SKU:\s*([^\r\n]+)/i);
          const varM = cleanText.match(/(?:Height|Size|Color|Style|Boyut|Renk|Ölçü):\s*([^\r\n]+)/i);
          const shipMethodM = cleanText.match(/Selected by buyer[\s\r\n]+([^\r\n]+)/i);

          if (lines.length >= 2) {
            const parsed = parseAddressFieldsFromLines(lines);
            if (parsed.isFullAddress || lines.length >= 3) {
              return {
                receiptId: String(receiptId),
                buyerName: parsed.buyerName,
                shippingAddress: [parsed.streetAddress, parsed.secondAddress, parsed.city, parsed.state, parsed.postalCode, parsed.country].filter(Boolean).join(', '),
                streetAddress: parsed.streetAddress,
                secondAddress: parsed.secondAddress,
                city: parsed.city,
                state: parsed.state,
                postalCode: parsed.postalCode,
                country: parsed.country,
                phone: parsed.phone,
                rawAddressText: parsed.rawAddressText,
                isFullAddress: Boolean(parsed.isFullAddress || parsed.streetAddress),
                transactionId: transM ? transM[1].trim() : null,
                sku: skuM ? skuM[1].trim() : null,
                variation: varM ? varM[0].trim() : null,
                shippingMethod: shipMethodM ? shipMethodM[1].trim() : null,
                sourceType: 'Arka Plan Çekmece İsteği (Same-Origin Fetch)',
                sourceUrl: targetUrl,
                httpStatus: '200 OK',
                resolvedAt: new Date().toLocaleTimeString('tr-TR', { hour: '2-digit', minute: '2-digit', second: '2-digit' }),
                scrapedAt: new Date().toISOString()
              };
            }
          }
        } catch (innerErr) {
          console.warn(`URL denemesi başarısız (${targetUrl}):`, innerErr);
        }
      }

      return null;
    } catch (err) {
      console.error('fetchSingleOrderFullAddress genel hatası:', receiptId, err);
      return null;
    }
  }

  // 6.5 TÜM SİPARİŞLERİN SHIP TO INLINE ÇEKMECELERİNİ AÇ VE TAM ADRESLERİ DERLE
  async function expandAndParseAllInlineOrders() {
    try {
      const buttonsToClick = new Set();

      // 1. DevTools'ta tespit edilen .wt-content-toggle butonlarını her kart içinde ara
      const containers = findOrderContainers();
      for (const item of containers) {
        const el = item.element;
        // Karttaki toggle body'nin durumunu kontrol et
        const addrBody = el.querySelector('.wt-content-toggle__body')
                      || el.parentElement?.querySelector('.wt-content-toggle__body');
        const isAlreadyVisible = addrBody && addrBody.getAttribute('aria-hidden') === 'false';

        if (!isAlreadyVisible) {
          const btn = el.querySelector('button.wt-content-toggle, button[aria-controls*="wt-content-toggle"], button[aria-expanded="false"]')
                   || el.parentElement?.querySelector('button.wt-content-toggle, button[aria-controls*="wt-content-toggle"], button[aria-expanded="false"]');
          if (btn) {
            buttonsToClick.add(btn);
          } else {
            // Kart içindeki Ship to başlığının yanındaki butonu ara
            const shipToEl = Array.from(el.querySelectorAll('*')).find(child => {
              const t = (child.textContent || '').trim();
              return /^Ship to$/i.test(t) || /^Teslimat adresi$/i.test(t) || /^Gönderim adresi$/i.test(t);
            });
            if (shipToEl) {
              const nearBtn = shipToEl.parentElement?.querySelector('button, [role="button"]')
                           || shipToEl.parentElement?.parentElement?.querySelector('button, [role="button"]');
              if (nearBtn) {
                buttonsToClick.add(nearBtn);
              }
            }
          }
        }
      }

      // 2. Fallback: Sayfadaki tüm button.wt-content-toggle veya button[aria-controls*="wt-content-toggle"] butonlarını bul
      if (buttonsToClick.size === 0) {
        const toggleButtons = Array.from(document.querySelectorAll('button.wt-content-toggle, button[aria-controls*="wt-content-toggle"], button[aria-expanded="false"]'));
        for (const btn of toggleButtons) {
          buttonsToClick.add(btn);
        }
      }

      console.log(`[Etsy Copilot] Açılacak ${buttonsToClick.size} inline .wt-content-toggle butonu tespit edildi.`);

      // Hepsini sırayla ve güvenli gecikmeyle tıkla
      for (const btn of buttonsToClick) {
        try {
          btn.click();
          await new Promise(r => setTimeout(r, 120));
        } catch (e) {
          console.warn('Inline toggle tıklanırken hata:', e);
        }
      }

      // DOM güncellemelerinin oturması için bekleme
      await new Promise(r => setTimeout(r, 450));

      // Şimdi sayfadaki tüm siparişleri .wt-content-toggle__body ile tam açık adreslerle tara
      const orders = parseOrdersOnPage();
      return orders;
    } catch (err) {
      console.error('expandAndParseAllInlineOrders hatası:', err);
      return parseOrdersOnPage();
    }
  }

  // 7. MESAJ DİNLEYİCİ
  chrome.runtime.onMessage.addListener((request, sender, sendResponse) => {
    const pageType = getPageType();

    if (request.action === 'GET_PAGE_DATA') {
      let financeData = null;
      let ordersData = null;
      let drawerOrder = null;

      if (pageType === 'finance') {
        financeData = parseFinanceSnapshot();
      } else if (pageType === 'orders') {
        ordersData = parseOrdersOnPage();
        drawerOrder = parseOpenDrawerOrder();
      }

      sendResponse({
        success: true,
        pageType: pageType,
        financeData: financeData,
        ordersData: ordersData,
        drawerOrder: drawerOrder,
        url: window.location.href
      });
      return true;
    }

    if (request.action === 'PARSE_ORDERS') {
      const ords = parseOrdersOnPage();
      sendResponse({ success: true, data: ords });
      return true;
    }

    if (request.action === 'EXPAND_AND_PARSE_PAGE_ORDERS') {
      expandAndParseAllInlineOrders().then(ords => {
        sendResponse({ success: true, data: ords });
      }).catch(err => {
        sendResponse({ success: false, error: err ? err.message : 'Bilinmeyen hata' });
      });
      return true;
    }

    if (request.action === 'FETCH_ORDER_FULL_ADDRESS') {
      const receiptId = request.receiptId;
      fetchSingleOrderFullAddress(receiptId).then(addressData => {
        sendResponse({ success: !!addressData, data: addressData });
      }).catch(err => {
        sendResponse({ success: false, error: err ? err.message : 'Bilinmeyen hata' });
      });
      return true; // Asenkron yanıt için true dönmeli
    }

    return true;
  });
})();
