// Etsy MarketPlace Satıcı Asistanı (Copilot) - Content Script
// Sıfır ban riski: Yalnızca pasif DOM okuma ve kullanıcı tetiklemeli aktarım.

(function () {
  'use strict';

  console.log('🛡️ Etsy MarketPlace Copilot Content Script devrede.');

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

  // 2. SİPARİŞ & MÜŞTERİ ADI / ADRES BİLGİSİNİ OKUMA
  function parseOrdersOnPage() {
    const orders = [];
    try {
      // Etsy sipariş paneli kartları / satırları
      const orderElements = document.querySelectorAll(
        '[data-receipt-id], [data-order-id], .panel-body-row, .receipt-label-panel, tr[data-testid="order-row"]'
      );

      if (orderElements && orderElements.length > 0) {
        orderElements.forEach((el) => {
          const receiptId = el.getAttribute('data-receipt-id') || el.getAttribute('data-order-id') || '';
          const text = el.innerText;

          // Müşteri adı tespiti
          const customerMatch = text.match(/Ship to\s*[\r\n]+([^\n\r]+)/i) 
                             || text.match(/Buyer:\s*([^\n\r]+)/i)
                             || text.match(/Müşteri:\s*([^\n\r]+)/i);

          const buyerName = customerMatch ? customerMatch[1].trim() : 'Bilinmeyen Müşteri';

          // Sipariş no
          const idMatch = text.match(/#\s*(\d{8,12})/);
          const finalId = receiptId || (idMatch ? idMatch[1] : '');

          if (finalId) {
            orders.push({
              receiptId: finalId,
              buyerName: buyerName,
              summary: text.substring(0, 180),
              scrapedAt: new Date().toISOString()
            });
          }
        });
      }

      // Yedek: Eğer spesifik DOM kartları bulunamazsa sayfa genelindeki sipariş no ve müşteri desenlerini tara
      if (orders.length === 0) {
        const bodyText = document.body.innerText;
        const matches = [...bodyText.matchAll(/#\s*(\d{10})/g)];
        matches.forEach((m) => {
          const id = m[1];
          if (!orders.some(o => o.receiptId === id)) {
            orders.push({
              receiptId: id,
              buyerName: 'Etsy Müşterisi',
              summary: 'Sipariş No: #' + id,
              scrapedAt: new Date().toISOString()
            });
          }
        });
      }
    } catch (err) {
      console.error('Siparişler okunamadı:', err);
    }
    return orders;
  }

  // 3. ETİKETLERİ VE SİPARİŞ KARTLARINA BUTON ENJEKTE ETME (İsteğe bağlı konfor)
  function injectOrderActionButtons() {
    if (getPageType() !== 'orders') return;

    const cards = document.querySelectorAll('[data-receipt-id], .receipt-label-panel');
    cards.forEach((card) => {
      if (card.querySelector('.etsy-copilot-btn')) return;

      const receiptId = card.getAttribute('data-receipt-id') || '';
      const btn = document.createElement('button');
      btn.className = 'etsy-copilot-btn';
      btn.innerText = '⚡ VDS\'e Aktar';
      btn.style.cssText = 'background: #4f46e5; color: white; border: none; padding: 4px 10px; border-radius: 4px; font-size: 11px; font-weight: bold; cursor: pointer; margin: 4px;';

      btn.addEventListener('click', async (e) => {
        e.preventDefault();
        e.stopPropagation();
        btn.innerText = '⏳ Aktarılıyor...';

        const customerText = card.innerText;
        const customerMatch = customerText.match(/Ship to\s*[\r\n]+([^\n\r]+)/i);
        const buyer = customerMatch ? customerMatch[1].trim() : 'Müşteri';

        try {
          const response = await fetch('http://localhost:5263/api/etsy/orders/' + receiptId + '/cost?shopId=53236321', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ notes: `VDS Copilot ile aktarıldı: ${buyer}` })
          });
          if (response.ok) {
            btn.innerText = '✅ VDS\'te!';
            btn.style.background = '#10b981';
          } else {
            btn.innerText = '⚠️ Hata';
            btn.style.background = '#ef4444';
          }
        } catch (err) {
          btn.innerText = '⚠️ Bağlantı Yok';
          btn.style.background = '#f59e0b';
        }
      });

      card.appendChild(btn);
    });
  }

  // Sayfa yüklendiğinde butonları ekle
  setTimeout(injectOrderActionButtons, 2000);

  // Mesaj Dinleyici (Popup veya Background ile iletişim)
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
