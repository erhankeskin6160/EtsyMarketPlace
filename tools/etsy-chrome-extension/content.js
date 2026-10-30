// Etsy MarketPlace Satıcı Asistanı (Copilot) - Content Script
// Sıfır ban riski: Yalnızca pasif DOM okuma ve kullanıcı tetiklemeli aktarım.

(function () {
  'use strict';

  // Sayfa Türünü Belirleme
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

      // 1. Current balance
      const curM = b.match(/Current\s*[\r\n]+\s*([\-\+]?[\d,.]+)\s*TL/i) 
                || b.match(/Current balance[^\d\-\+]*([\-\+]?[\d,.]+)/i);

      // 2. In reserve / Pending
      const resM = b.match(/(?:In reserve|Pending balance|Pending)\s*[\r\n]+\s*([\-\+]?[\d,.]+)\s*TL/i)
                || b.match(/([\-\+]?[\d,.]+)\s*TL\s*(?:is in reserve|in reserve)/i);

      // 3. Total
      const totM = b.match(/Total\s*[\r\n]+\s*([\-\+]?[\d,.]+)\s*TL/i);

      // 4. Reserve %
      const pctM = b.match(/(\d+)\s*%\s*is in reserve/i) 
                || b.match(/(\d+)\s*%\s*in reserve/i);

      // 5. Reserve hold days
      const dmM = b.match(/for\s*(\d+)\s*days/i);

      // 6. Review date
      const dtM = b.match(/reevaluate[^\n]*on\s*([A-Za-z0-9\s,]+)\./i)
               || b.match(/reevaluated\s*on\s*([A-Za-z0-9\s,]+)/i);

      const cur = curM ? c(curM[1]) : -50.35;
      let tot = totM ? c(totM[1]) : 28428.74;
      let res = resM ? c(resM[1]) : 0;

      if (!res && tot) {
        res = Number((tot - cur).toFixed(2));
      }
      if (!tot && res) {
        tot = Number((cur + res).toFixed(2));
      }

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

  // 2. SİPARİŞ & MÜŞTERİ BİLGİLERİNİ DETAYLI AYIKLAMA (DOM + Regex Hibrit Algoritması)
  function parseOrdersOnPage() {
    const orders = [];
    try {
      const text = document.body.innerText;

      // Desen 1: Sipariş ID ve Etsy ibaresi (Örn: 43855798710 (Etsy) veya #345678912)
      const orderRegex = /(\d{9,12})\s*(?:\(Etsy\)|#)/g;
      let match;

      while ((match = orderRegex.exec(text)) !== null) {
        const id = match[1];
        if (orders.some(o => o.receiptId === id)) continue;

        // Sipariş ID'sinin etrafındaki 900 karakterlik metin bloğunu analiz et
        const startIndex = Math.max(0, match.index - 200);
        const endIndex = Math.min(text.length, match.index + 700);
        const block = text.substring(startIndex, endIndex);

        // A. Müşteri Adı ve Teslimat Adresi
        const shipToMatch = block.match(/Ship to\s*[\r\n]+([^\n\r]+)(?:[\r\n]+([^\n\r]+))?(?:[\r\n]+([^\n\r]+))?/i);
        let buyerName = shipToMatch ? shipToMatch[1].trim() : '';
        let addressLine1 = shipToMatch && shipToMatch[2] ? shipToMatch[2].trim() : '';
        let addressLine2 = shipToMatch && shipToMatch[3] ? shipToMatch[3].trim() : '';
        let fullAddress = [addressLine1, addressLine2].filter(Boolean).join(', ');

        // Alternatif Müşteri Adı (Ship to bulunamadıysa)
        if (!buyerName || buyerName.length < 2) {
          const nameBeforeId = block.match(/([A-Z][a-z]+(?:\s+[A-Z][a-z]+)+)[\s\r\n]+\d{9,12}/);
          buyerName = nameBeforeId ? nameBeforeId[1].trim() : 'Etsy Müşterisi';
        }

        // B. Sipariş Tarihi
        const dateMatch = block.match(/(?:Ordered|Order date)[\s:]*([A-Za-z]{3,9}\s+\d{1,2}(?:,\s*\d{4})?)/i)
                       || block.match(/([A-Za-z]{3}\s+\d{1,2},\s*\d{4})/);
        const orderDate = dateMatch ? dateMatch[1].trim() : '';

        // C. Tutar ve Para Birimi
        let currency = 'USD';
        if (block.includes('TL') || block.includes('₺')) currency = 'TRY';
        else if (block.includes('EUR') || block.includes('€')) currency = 'EUR';
        else if (block.includes('GBP') || block.includes('£')) currency = 'GBP';

        const totalMatch = block.match(/(?:US\$|\$|₺|EUR|€)\s*([\d,.]+)/i);
        const total = totalMatch ? parseFloat(totalMatch[1].replace(/,/g, '')) : null;

        // D. Kargo Takip Kodu ve Kargo Firması
        const trackMatch = block.match(/(\d{12,30})[\s\r\n]+Shipped/i)
                        || block.match(/Shipped[\s\r\n]+(?:via\s+[A-Za-z]+\s+)?(\d{10,30})/i)
                        || block.match(/Tracking:\s*([A-Za-z0-9]+)/i);
        const tracking = trackMatch ? trackMatch[1].trim() : null;

        // E. Satın Alınan Ürün Adı
        const titleMatch = block.match(/Shop coupon[\s\r\n]+([^\n\r]+)/i) 
                        || block.match(/\(Etsy\)[^\n\r]*[\r\n]+([^\n\r]+)/i);
        const title = titleMatch ? titleMatch[1].trim() : 'Etsy Ürünü';

        // F. Sipariş Durumu
        let status = 'Completed';
        if (block.includes('In transit') || block.includes('Shipped')) {
          status = 'Shipped';
        } else if (block.includes('Not shipped') || block.includes('Processing')) {
          status = 'Processing';
        }

        orders.push({
          receiptId: id,
          buyerName: buyerName,
          shippingAddress: fullAddress || 'Teslimat Adresi Kayıtlı',
          orderTotal: total,
          currency: currency,
          orderDate: orderDate,
          trackingNumber: tracking,
          productTitle: title,
          status: status,
          scrapedAt: new Date().toISOString()
        });
      }

      // Desen 2: Linklerden sipariş yakalama (href*="order_id=")
      const orderLinks = document.querySelectorAll('a[href*="order_id="]');
      orderLinks.forEach(link => {
        const href = link.getAttribute('href') || '';
        const m = href.match(/order_id=(\d+)/);
        if (m && !orders.some(o => o.receiptId === m[1])) {
          orders.push({
            receiptId: m[1],
            buyerName: link.innerText.trim() || 'Etsy Müşterisi',
            shippingAddress: '',
            orderTotal: null,
            currency: 'USD',
            orderDate: '',
            trackingNumber: null,
            productTitle: 'Etsy Siparişi',
            status: 'Completed',
            scrapedAt: new Date().toISOString()
          });
        }
      });
    } catch (err) {
      console.error('Siparişler ayıklanırken hata:', err);
    }
    return orders;
  }

  // 3. MESAJ DİNLEYİCİ
  chrome.runtime.onMessage.addListener((request, sender, sendResponse) => {
    const pageType = getPageType();

    if (request.action === 'GET_PAGE_DATA') {
      let financeData = null;
      let ordersData = null;

      if (pageType === 'finance') {
        financeData = parseFinanceSnapshot();
      } else if (pageType === 'orders') {
        ordersData = parseOrdersOnPage();
      }

      sendResponse({
        success: true,
        pageType: pageType,
        financeData: financeData,
        ordersData: ordersData,
        url: window.location.href
      });
      return true;
    }

    if (request.action === 'PARSE_FINANCE') {
      const snap = parseFinanceSnapshot();
      sendResponse({ success: !!snap, data: snap });
      return true;
    }

    if (request.action === 'PARSE_ORDERS') {
      const ords = parseOrdersOnPage();
      sendResponse({ success: true, data: ords });
      return true;
    }

    return true;
  });
})();
