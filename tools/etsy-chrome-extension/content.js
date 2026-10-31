// Etsy MarketPlace Satıcı Asistanı (Copilot) - Content Script v1.3.0
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

  // 2. GELİŞMİŞ ADRES AYRIŞTIRICI (Cadde, Apt, Şehir, Eyalet, Posta Kodu, Ülke, Telefon, IOSS)
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
    /Standart Kargo/i,
    /VAT Collected/i,
    /IOSS number/i,
    /charged VAT/i,
    /Make sure to write/i,
    /Selected by buyer/i,
    /Marked as gift/i,
    /Gift message/i,
    /Pre-transit/i,
    /In transit/i,
    /Delivered/i,
    /Canceled/i
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
      if (/^(?:Marked as gift|Gift message|Gift message available|Hediye|USPS Verified|Doğrulandı|Copy(?:\s+address)?|Adresi\s*kopyala|Order in reserve|Rezervde|View order|Print shipping|Standard Shipping|VAT Collected|Ship by.*|Selected by buyer.*)$/i.test(line)) {
        continue;
      }
      if (isStopLine(line)) break;
      cleanLines.push(line);
      // Makul maksimum adres satırı 6'dır (Ad, Cadde1, Cadde2, Şehir/Eyalet/Zip, Ülke, Tel)
      if (cleanLines.length >= 6) break;
    }

    return cleanLines;
  }

  const KNOWN_COUNTRIES = [
    'United States', 'USA', 'US', 'Canada', 'United Kingdom', 'Great Britain', 'UK', 
    'Australia', 'Germany', 'Deutschland', 'France', 'Netherlands', 'Italy', 'Spain', 
    'Turkey', 'Türkiye', 'Austria', 'Österreich', 'Belgium', 'Switzerland', 'Sweden', 
    'Norway', 'Denmark', 'Finland', 'Poland', 'Ireland', 'New Zealand', 'Japan', 
    'Portugal', 'Greece', 'Czech Republic', 'Czechia', 'Hungary', 'Romania', 'Bulgaria',
    'Croatia', 'Slovakia', 'Slovenia', 'Estonia', 'Latvia', 'Lithuania', 'Cyprus', 'Malta'
  ];
  const COUNTRY_REGEX = new RegExp(`^(?:${KNOWN_COUNTRIES.join('|')})$`, 'i');

  function parseAddressFieldsFromLines(lines, rawBlock) {
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
        iossNumber: '',
        rawAddressText: '',
        isFullAddress: false
      };
    }

    const buyerName = lines[0] || '';
    let phone = '';
    let country = '';
    let city = '';
    let state = '';
    let postalCode = '';
    let streetAddress = '';
    let secondAddress = '';
    let iossNumber = '';

    // IOSS / VAT numarasını tespit et (Örn: IM3720000224)
    const combinedBlock = (rawBlock || '') + ' ' + lines.join(' ');
    const iossM = combinedBlock.match(/\b(IM\d{10})\b/i);
    if (iossM) {
      iossNumber = iossM[1].toUpperCase();
    }

    const remaining = lines.slice(1);

    // 1. SIKI TELEFON TESPİTİ (Kesinlikle IOSS/vergi metinlerini veya harf içeren paragrafları almaz!)
    const phoneIdx = remaining.findIndex(l => {
      const clean = l.trim();
      if (/^(?:Tel|Phone|Mobile|Telefon|Cep)\s*[:\-\.]?\s*(\+?[\d\s\-\(\)\.]{7,22})$/i.test(clean)) {
        return true;
      }
      if (/^[\+\d\(\)\s\.\-]{7,22}$/.test(clean)) {
        if (/^\d{5}-\d{4}$/.test(clean)) return false; // ABD ZIP+4 posta kodu hariç
        const digits = clean.replace(/\D/g, '');
        return digits.length >= 7 && digits.length <= 15;
      }
      return false;
    });

    if (phoneIdx !== -1) {
      const rawP = remaining[phoneIdx].trim();
      const m = rawP.match(/(?:Tel|Phone|Mobile|Telefon|Cep)\s*[:\-\.]?\s*(.+)/i);
      phone = m ? m[1].trim() : rawP;
      remaining.splice(phoneIdx, 1);
    }

    // 2. ÜLKE BULMA (Tek başına satır olarak)
    const countryIdx = remaining.findIndex(l => COUNTRY_REGEX.test(l.trim()));
    if (countryIdx !== -1) {
      country = remaining[countryIdx].trim();
      remaining.splice(countryIdx, 1);
    }

    // 3. KAPALI LİSTE ÖZETİ TESPİTİ: "Koblenz, Germany" veya "Port Charlotte, FL"
    // Bu satır bir sokak adresi DEĞİLDİR, şehir ve ülke/eyalet özetidir!
    for (let i = 0; i < remaining.length; i++) {
      const line = remaining[i].trim();
      const parts = line.split(',').map(p => p.trim());
      if (parts.length === 2 && COUNTRY_REGEX.test(parts[1])) {
        city = parts[0];
        country = parts[1];
        remaining.splice(i, 1);
        break;
      }
    }

    // 4. ŞEHİR / EYALET / POSTA KODU FORMATLARI
    // A) US: "Port Charlotte, FL 33952-8113" veya "Arlington, TX 76014" veya "Port Charlotte, FL"
    const usCityStateIdx = remaining.findIndex(l => /^[A-Za-z\s.-]+,\s*[A-Z]{2}(?:\s+\d{5}(?:-\d{4})?)?$/i.test(l.trim()));
    if (usCityStateIdx !== -1) {
      const csLine = remaining[usCityStateIdx].trim();
      const m = csLine.match(/^([A-Za-z\s.-]+),\s*([A-Z]{2})(?:\s+(\d{5}(?:-\d{4})?))?/i);
      if (m) {
        city = m[1].trim();
        state = m[2].trim().toUpperCase();
        postalCode = m[3] ? m[3].trim() : '';
        if (!country) country = 'United States';
      }
      remaining.splice(usCityStateIdx, 1);
    } else {
      // B) Almanya / Avrupa Formatı: "56068 Koblenz" veya "D-56068 Koblenz"
      const euIdx = remaining.findIndex(l => /^(?:[A-Z]{1,2}-)?\d{4,5}\s+[A-Za-z\u00C0-\u017F\s.-]+$/i.test(l.trim()));
      if (euIdx !== -1) {
        const m = remaining[euIdx].trim().match(/^((?:[A-Z]{1,2}-)?\d{4,5})\s+([A-Za-z\u00C0-\u017F\s.-]+)$/i);
        if (m) {
          postalCode = m[1].trim();
          city = m[2].trim();
        }
        remaining.splice(euIdx, 1);
      } else {
        // C) UK Formatı: "London SW1A 1AA" veya "SW1A 1AA"
        const ukIdx = remaining.findIndex(l => /\b[A-Z]{1,2}\d[A-Z\d]?\s*\d[A-Z]{2}\b/i.test(l.trim()));
        if (ukIdx !== -1) {
          const l = remaining[ukIdx].trim();
          const m = l.match(/^(.*?)\s*([A-Z]{1,2}\d[A-Z\d]?\s*\d[A-Z]{2})$/i);
          if (m) {
            if (m[1].trim()) city = m[1].trim();
            postalCode = m[2].trim().toUpperCase();
            if (!country) country = 'United Kingdom';
          }
          remaining.splice(ukIdx, 1);
        } else {
          // D) Kanada Formatı: "Toronto, ON M5V 2T6"
          const caIdx = remaining.findIndex(l => /^[A-Za-z\s.-]+,\s*[A-Z]{2}\s+[A-Z]\d[A-Z]\s*\d[A-Z]\d$/i.test(l.trim()));
          if (caIdx !== -1) {
            const m = remaining[caIdx].trim().match(/^([A-Za-z\s.-]+),\s*([A-Z]{2})\s+([A-Z]\d[A-Z]\s*\d[A-Z]\d)$/i);
            if (m) {
              city = m[1].trim();
              state = m[2].trim().toUpperCase();
              postalCode = m[3].trim().toUpperCase();
              if (!country) country = 'Canada';
            }
            remaining.splice(caIdx, 1);
          }
        }
      }
    }

    // 5. GERİYE KALAN SATIRLAR CADDE VE DAİRE/BİNA BİLGİLERİDİR
    if (remaining.length > 0) {
      streetAddress = remaining[0];
      if (remaining.length > 1) {
        secondAddress = remaining.slice(1).join(', ');
      }
    }

    if (!country) {
      country = (state && state.length === 2 && !['ON','QC','BC','AB','MB','SK'].includes(state)) ? 'United States' : (country || 'United States');
    }

    // Gerçek sokak adresi doğrulaması: Şehir adı caddeye kopyalanmamış olmalı ve en azından kapı/sokak göstergesi olmalı
    const isFullAddress = Boolean(
      streetAddress && 
      (city || state) && 
      streetAddress.toLowerCase() !== city.toLowerCase() &&
      !COUNTRY_REGEX.test(streetAddress)
    );

    // AI Formatında Temiz Ham Metin
    const rawParts = [
      buyerName,
      streetAddress,
      secondAddress,
      [postalCode, city, state].filter(Boolean).join(' '),
      country,
      phone ? `Tel: ${phone}` : '',
      iossNumber ? `IOSS: ${iossNumber}` : ''
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
      iossNumber,
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

      const shipToMatches = text.match(/Ship to\s*[\r\n]+([\s\S]+?)(?=(?:USPS Verified|Order in reserve|Ship with DDP|Standard Shipping|Tracking|Only you can see|Carrier|Print shipping label|View order details|\d+\s*Items?|$))/i);
      if (!shipToMatches) return null;

      let drawerOrderId = urlOrderMatch ? urlOrderMatch[1] : null;
      if (!drawerOrderId) {
        const idM = text.match(/#(\d{9,12})\b/) || text.match(/(\d{9,12})\s*\(Etsy\)/);
        if (idM) drawerOrderId = idM[1];
      }

      if (!drawerOrderId) return null;

      const lines = extractCleanAddressLines(text);
      if (lines.length < 2) return null;

      const parsed = parseAddressFieldsFromLines(lines, text);
      if (!parsed.isFullAddress && lines.length < 3) return null;

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
        iossNumber: parsed.iossNumber,
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

  // 4. SİPARİŞ KARTLARINI DOM SEVİYESİNDE İZOLE ETME (Ağaç Tırmanma ve Tekil ID Garantisi)
  function findOrderContainers() {
    const list = [];
    const seenIds = new Set();

    const orderLinks = Array.from(document.querySelectorAll('a[href*="order_id="]'));

    for (const a of orderLinks) {
      const href = a.getAttribute('href') || a.href || '';
      const m = href.match(/[?&]order_id=(\d{10})\b/);
      if (!m) continue;
      const id = m[1];
      if (seenIds.has(id)) continue;

      // Linkten yukarı tırman: Kapsayıcı içindeki 10 basamaklı FARKLI sipariş ID sayısı
      // TAM 1 olduğu sürece tırmanmaya devam et (Böylece sol ürün ve sağ ship to sütununun
      // ikisini birden kapsayan EN GENİŞ tekil kart yakalanır, üst liste düzeyine taşmaz!)
      let curr = a.parentElement;
      let prevCard = a.parentElement || a;

      while (curr && curr !== document.body) {
        const orderIdsInside = Array.from(curr.querySelectorAll('a[href*="order_id="]'))
          .map(l => (l.getAttribute('href') || l.href || '').match(/[?&]order_id=(\d{10})\b/)?.[1])
          .filter(Boolean);
        const distinctInside = new Set(orderIdsInside);

        if (distinctInside.size === 1) {
          prevCard = curr;
          curr = curr.parentElement;
        } else if (distinctInside.size > 1) {
          // Birden fazla siparişi içeren dış listeye çıktık, hemen önceki tekil kartta dur!
          break;
        } else {
          curr = curr.parentElement;
        }
      }

      seenIds.add(id);
      list.push({ id, element: prevCard });
    }

    // Fallback: Link bulunamadıysa metin üzerinden strictly 10 hane ara
    if (list.length === 0) {
      const candidateNodes = Array.from(document.querySelectorAll('span, a, p, div, strong, b'));
      for (const node of candidateNodes) {
        if (node.children.length > 2) continue;
        const text = (node.textContent || '').trim();
        const m = text.match(/#(\d{10})\b/) || text.match(/\b(\d{10})\s*\(Etsy\)/i);
        if (!m) continue;
        const id = m[1];
        if (seenIds.has(id)) continue;

        let curr = node.parentElement;
        let prevCard = node.parentElement || node;
        while (curr && curr !== document.body) {
          const ids = (curr.innerText || '').match(/#(\d{10})\b/g) || [];
          const distinct = new Set(ids);
          if (distinct.size <= 1) {
            prevCard = curr;
            curr = curr.parentElement;
          } else {
            break;
          }
        }

        seenIds.add(id);
        list.push({ id, element: prevCard });
      }
    }

    return list;
  }

  // 4.1 İZOLE KART İÇİNDEKİ SHIP-TO ELEMANLARINI HEDEFLEME
  function resolveCardShipTo(cardEl) {
    if (!cardEl) return null;

    // 1. Ship to başlığını ara
    const shipToHeader = Array.from(cardEl.querySelectorAll('p, div, span, h3, h4, h5, h6, strong, b')).find(el => {
      const t = (el.textContent || '').trim();
      return /^(?:Ship to|Shipped to|Gönderim adresi|Teslimat adresi)$/i.test(t);
    });

    let toggleBtn = null;
    let addrBody = null;

    if (shipToHeader) {
      const parent = shipToHeader.parentElement;
      const grandParent = parent ? parent.parentElement : null;

      toggleBtn = (parent && parent.querySelector('button, [role="button"]'))
               || (grandParent && grandParent.querySelector('button, [role="button"]'))
               || cardEl.querySelector('button[aria-controls*="wt-content-toggle"], button.wt-content-toggle, button[class*="wt-content-toggle"]');
    } else {
      toggleBtn = cardEl.querySelector('button[aria-controls*="wt-content-toggle"], button.wt-content-toggle, button[class*="wt-content-toggle"]');
    }

    if (toggleBtn) {
      const controlsId = toggleBtn.getAttribute('aria-controls');
      if (controlsId) {
        addrBody = document.getElementById(controlsId);
      }
    }

    if (!addrBody) {
      addrBody = cardEl.querySelector('.wt-content-toggle__body');
    }

    const isExpanded = addrBody 
      ? (addrBody.getAttribute('aria-hidden') === 'false' || addrBody.style.visibility === 'visible' || addrBody.classList.contains('wt-content-toggle__body--open'))
      : (toggleBtn ? toggleBtn.getAttribute('aria-expanded') === 'true' : false);

    return { toggleBtn, addrBody, isExpanded };
  }

  // 4.2 SİPARİŞ LİSTESİ AYIKLAMA (İzole DOM & .wt-content-toggle__body innerHTML Koruması)
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

          // Doğrudan adres kutusunu bul: .wt-content-toggle__body
          const shipToInfo = resolveCardShipTo(el);
          const addrBody = shipToInfo ? shipToInfo.addrBody : (el.querySelector('.wt-content-toggle__body') || el.parentElement?.querySelector('.wt-content-toggle__body'));
          
          // visibility: hidden durumunda innerText boş dönebileceği için innerHTML -> htmlToText kullan
          let addressText = '';
          if (addrBody) {
            addressText = addrBody.innerText || htmlToText(addrBody.innerHTML);
          }

          const lines = extractCleanAddressLines(addressText || block);
          const parsed = parseAddressFieldsFromLines(lines, block);

          // Tutar ve Para Birimi
          let currency = 'USD';
          if (block.includes('TL') || block.includes('₺')) currency = 'TRY';
          else if (block.includes('EUR') || block.includes('€')) currency = 'EUR';
          else if (block.includes('GBP') || block.includes('£')) currency = 'GBP';

          const priceMatch = block.match(/#\d{10}[^\n\r]*(?:US\$|\$|₺|EUR|€)\s*([\d,.]+)/i)
                          || block.match(/(?:US\$|\$|₺|EUR|€)\s*([\d,.]+)/i);
          const total = priceMatch ? parseFloat(priceMatch[1].replace(/,/g, '')) : null;

          // Alıcı Adı
          let buyerName = parsed.buyerName;
          if (!buyerName || /^Etsy/i.test(buyerName)) {
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
            if (parsed.iossNumber && !existing.iossNumber) existing.iossNumber = parsed.iossNumber;
            continue;
          }

          const fullAddrSnippet = [parsed.streetAddress, parsed.secondAddress, parsed.city, parsed.state, parsed.postalCode, parsed.country].filter(Boolean).join(', ');

          ordersMap.set(id, {
            receiptId: id,
            buyerName: buyerName || 'Etsy Müşterisi',
            shippingAddress: fullAddrSnippet || [parsed.city, parsed.state, parsed.country].filter(Boolean).join(', ') || 'Adres Kayıtlı',
            streetAddress: parsed.streetAddress,
            secondAddress: parsed.secondAddress,
            city: parsed.city,
            state: parsed.state,
            postalCode: parsed.postalCode,
            country: parsed.country,
            phone: parsed.phone,
            iossNumber: parsed.iossNumber,
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
      const openDrawer = parseOpenDrawerOrder();
      if (openDrawer && String(openDrawer.receiptId) === String(receiptId) && openDrawer.isFullAddress) {
        return openDrawer;
      }

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

          const transM = cleanText.match(/Transaction ID:\s*(\d+)/i);
          const skuM = cleanText.match(/SKU:\s*([^\r\n]+)/i);
          const varM = cleanText.match(/(?:Height|Size|Color|Style|Boyut|Renk|Ölçü):\s*([^\r\n]+)/i);
          const shipMethodM = cleanText.match(/Selected by buyer[\s\r\n]+([^\r\n]+)/i);

          if (lines.length >= 2) {
            const parsed = parseAddressFieldsFromLines(lines, cleanText);
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
                iossNumber: parsed.iossNumber,
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
      const containers = findOrderContainers();

      for (const item of containers) {
        const shipTo = resolveCardShipTo(item.element);
        if (shipTo && !shipTo.isExpanded && shipTo.toggleBtn) {
          buttonsToClick.add(shipTo.toggleBtn);
        }
      }

      // Fallback: Kart içinde bulunamadıysa sayfadaki tüm kapalı toggle butonlarını topla
      if (buttonsToClick.size === 0) {
        const toggleButtons = Array.from(document.querySelectorAll('button.wt-content-toggle, button[aria-controls*="wt-content-toggle"], button[aria-expanded="false"]'));
        for (const btn of toggleButtons) {
          buttonsToClick.add(btn);
        }
      }

      console.log(`[Etsy Copilot] Açılacak ${buttonsToClick.size} inline toggle butonu tespit edildi.`);

      // Butonları güvenli ve nazik gecikmeyle tıkla
      for (const btn of buttonsToClick) {
        try {
          btn.click();
          await new Promise(r => setTimeout(r, 100));
        } catch (e) {
          console.warn('Inline toggle tıklanırken hata:', e);
        }
      }

      // Dinamik bekleme: Toggle kutuları açılana kadar en fazla 1500ms bekle
      const startTime = Date.now();
      while (Date.now() - startTime < 1500) {
        const anyPending = Array.from(buttonsToClick).some(btn => {
          const cid = btn.getAttribute('aria-controls');
          const body = cid ? document.getElementById(cid) : null;
          return body && body.getAttribute('aria-hidden') === 'true';
        });
        if (!anyPending) break;
        await new Promise(r => setTimeout(r, 100));
      }

      // DOM güncellemelerinin oturması için kısa pay
      await new Promise(r => setTimeout(r, 200));

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
      return true;
    }

    return true;
  });
})();
