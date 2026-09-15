namespace SimilarProductsWinForms;

using System;
using System.Globalization;
using System.Drawing;
using System.Windows.Forms;
using SimilarProductsWinForms.Controls;
using SimilarProductsWinForms.Models;
using SimilarProductsWinForms.Services;

internal sealed class OrderDetailsForm : Form
{
    private readonly SqliteOrderCostRepository _orderCostRepo = new();
    private readonly SqliteProductCostRepository _productCostRepo = new();
    private readonly OrderFinancialSummary _order;
    
    private decimal _productionCost;
    private decimal _shippingCost;
    private decimal _packagingCost;

    public OrderDetailsForm(OrderFinancialSummary order)
    {
        _order = order;
        Text = $"🧾 Sipariş Detayları: #{order.ReceiptId}  —  {order.DisplayCustomer}";
        Size = new Size(880, 660);
        MinimumSize = new Size(840, 620);
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.Sizable;
        MaximizeBox = true;
        MinimizeBox = false;

        Load += OrderDetailsForm_Load;

        BuildLayout();
        UiStyle.ApplyResponsiveTheme(this, new Size(840, 620));
    }

    private async void OrderDetailsForm_Load(object? sender, EventArgs e)
    {
        var orderEntry = await _orderCostRepo.GetByReceiptIdAsync(_order.ReceiptId.ToString());
        if (orderEntry != null)
        {
            _productionCost = orderEntry.UnitCost;
            _shippingCost = orderEntry.UnitShippingCost;
            _packagingCost = orderEntry.UnitPackagingCost;
        }
        else
        {
            var prodEntry = await _productCostRepo.GetByIdAsync(_order.ListingId.ToString());
            if (prodEntry != null)
            {
                _productionCost = prodEntry.UnitCost;
                _shippingCost = prodEntry.UnitShippingCost;
                _packagingCost = prodEntry.UnitPackagingCost;
            }
            else
            {
                _productionCost = _order.UnitProductionCost;
                _shippingCost = _order.UnitShippingCost;
                _packagingCost = _order.UnitPackagingCost;
            }
        }
        UpdateProfitLabel();
    }

    private void BuildLayout()
    {
        var mainLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            RowCount = 2,
            ColumnCount = 1,
            Padding = new Padding(20),
            BackColor = UiStyle.BackgroundColor
        };
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); // Main cards
        mainLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize)); // Bottom profit/cost section
        Controls.Add(mainLayout);

        // -- Top Section (Left: Earnings, Right: Fees)
        var topLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            BackColor = Color.Transparent
        };
        topLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        topLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        topLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        mainLayout.Controls.Add(topLayout, 0, 0);

        // Left Panel (Earnings)
        var pnlEarnings = new ModernCardPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(20),
            Margin = new Padding(0, 0, 10, 0),
            CardColor = UiStyle.CardBackground,
            BorderColor = UiStyle.BorderColor
        };
        BuildEarningsPanel(pnlEarnings);
        topLayout.Controls.Add(pnlEarnings, 0, 0);

        // Right Panel (Fees)
        var pnlFees = new ModernCardPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(20),
            Margin = new Padding(10, 0, 0, 0),
            CardColor = UiStyle.CardBackground,
            BorderColor = UiStyle.BorderColor
        };
        BuildFeesPanel(pnlFees);
        topLayout.Controls.Add(pnlFees, 1, 0);

        // -- Bottom Section (Profit & Cost)
        var pnlBottom = new ModernCardPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            Padding = new Padding(20, 16, 20, 16),
            Margin = new Padding(0, 16, 0, 0),
            CardColor = UiStyle.CardBackground,
            BorderColor = UiStyle.BorderColor
        };
        BuildBottomPanel(pnlBottom);
        mainLayout.Controls.Add(pnlBottom, 0, 1);
    }

    private void BuildEarningsPanel(Panel container)
    {
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 8 };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 60));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 40));

        int row = 0;
        
        // Header
        AddRow(layout, "Müşteri Ödemesi", $"${_order.GrandTotal:N2}", row++, true, UiStyle.TextDark);
        
        // Items
        // Etsy API'deki Subtotal indirim düşülmüş tutardır. Ham ürün fiyatı = Subtotal + DiscountAmt
        decimal originalItemPrice = _order.Subtotal + _order.DiscountAmt;
        AddRow(layout, "Ürün Fiyatı", $"${originalItemPrice:N2}", row++, false, UiStyle.TextMuted);
        
        if (_order.DiscountAmt > 0)
        {
            AddRow(layout, "Mağaza İndirimi", $"-${_order.DiscountAmt:N2}", row++, false, UiStyle.TextMuted);
        }

        AddRow(layout, "Kargo Ücreti", $"${_order.ShippingPrice:N2}", row++, false, UiStyle.TextMuted);
        
        // Vergi Öncesi Ara Toplam = Ürün Fiyatı - İndirim + Kargo (Etsy Fişiyle tam eşleşen Subtotal + Shipping)
        decimal calcSubtotal = _order.Subtotal + _order.ShippingPrice;
        AddRow(layout, "Vergi Öncesi Ara Toplam", $"${calcSubtotal:N2}", row++, false, UiStyle.TextDark);
        AddRow(layout, "Müşterinin Ödediği Vergi", $"${_order.TaxPaidByBuyer:N2}", row++, false, UiStyle.TextMuted);

        decimal netUsd = _order.GrandTotal - _order.EtsyFees - _order.OffsiteAdFee;
        decimal netTry = Math.Round(netUsd * _order.ExchangeRate, 2);

        // Check if shop has payment reserve
        var settings = EtsyApiSettingsStore.Load();
        if (settings.HasPaymentReserve && settings.PaymentReservePercent > 0)
        {
            var pnlReserve = BuildReservePanel(netUsd, settings.PaymentReservePercent);
            container.Controls.Add(pnlReserve);
        }

        // Total Earned Label at bottom
        var lblEarned = new Label
        {
            Text = $"Etsy Net Geliri: ${netUsd:N2}  (₺{netTry:N2})",
            Font = new Font("Segoe UI Semibold", 11.5F, FontStyle.Bold),
            ForeColor = UiStyle.SuccessColor,
            Dock = DockStyle.Bottom,
            AutoSize = true,
            Padding = new Padding(0, 8, 0, 4)
        };
        container.Controls.Add(lblEarned);
        container.Controls.Add(layout);
        layout.BringToFront();
    }

    private Control BuildReservePanel(decimal netUsd, decimal reservePct)
    {
        decimal releasedPct = Math.Max(0, 100m - reservePct);
        decimal netTry = Math.Round(netUsd * _order.ExchangeRate, 2);
        decimal lockedUsd = Math.Round(netUsd * (reservePct / 100m), 2);
        decimal releasedUsd = netUsd - lockedUsd;
        decimal lockedTry = Math.Round(lockedUsd * _order.ExchangeRate, 2);
        decimal releasedTry = netTry - lockedTry;

        var card = new Panel
        {
            Dock = DockStyle.Bottom,
            AutoSize = true,
            BackColor = Color.FromArgb(20, 27, 45),
            Padding = new Padding(12, 8, 12, 8),
            Margin = new Padding(0, 4, 0, 0)
        };

        card.Paint += (s, e) =>
        {
            e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            using var pen = new Pen(Color.FromArgb(51, 65, 85), 1f);
            var rect = card.ClientRectangle;
            rect.Width -= 1;
            rect.Height -= 1;
            e.Graphics.DrawRectangle(pen, rect);
        };

        var flow = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoSize = true,
            BackColor = Color.Transparent,
            Margin = new Padding(0)
        };

        // Title Header
        var lblTitle = new Label
        {
            Text = $"🔒 ETSY ÖDEME REZERVİ  (%{reservePct:N0} BLOKE)",
            Font = new Font("Segoe UI Semibold", 8.5F, FontStyle.Bold),
            ForeColor = UiStyle.WarningColor,
            AutoSize = true,
            Margin = new Padding(0, 0, 0, 4)
        };
        flow.Controls.Add(lblTitle);

        // Progress Bar showing proportion
        var bar = new Panel
        {
            Height = 5,
            Width = 320,
            BackColor = Color.FromArgb(30, 41, 59),
            Margin = new Padding(0, 0, 0, 5)
        };
        bar.Paint += (s, e) =>
        {
            int w = bar.Width;
            int h = bar.Height;
            float relRatio = (float)releasedPct / 100f;
            int relWidth = Math.Max(0, Math.Min(w, (int)(w * relRatio)));

            using var relBrush = new SolidBrush(UiStyle.SuccessColor);
            e.Graphics.FillRectangle(relBrush, 0, 0, relWidth, h);

            using var lockBrush = new SolidBrush(UiStyle.WarningColor);
            e.Graphics.FillRectangle(lockBrush, relWidth, 0, w - relWidth, h);
        };
        card.Layout += (s, e) =>
        {
            int targetW = Math.Max(100, card.ClientSize.Width - card.Padding.Horizontal);
            if (bar.Width != targetW)
            {
                bar.Width = targetW;
                bar.Invalidate();
            }
        };
        flow.Controls.Add(bar);

        // Released label
        var lblReleased = new Label
        {
            Text = $"🔓 Serbest Bırakılan (%{releasedPct:N0}):  ${releasedUsd:N2}  (₺{releasedTry:N2})",
            Font = new Font("Segoe UI Semibold", 8.5F),
            ForeColor = UiStyle.SuccessColor,
            AutoSize = true,
            Margin = new Padding(0, 0, 0, 2)
        };
        flow.Controls.Add(lblReleased);

        // Locked label
        var lblLocked = new Label
        {
            Text = $"🔒 Rezervde Tutulan (%{reservePct:N0}):  ${lockedUsd:N2}  (₺{lockedTry:N2})",
            Font = new Font("Segoe UI Semibold", 8.5F),
            ForeColor = UiStyle.WarningColor,
            AutoSize = true,
            Margin = new Padding(0, 0, 0, 3)
        };
        flow.Controls.Add(lblLocked);

        // Footnote
        var lblNote = new Label
        {
            Text = "* Takip no girilene veya 45 güne kadar Etsy tarafında tutulur.",
            Font = new Font("Segoe UI", 7.5F),
            ForeColor = UiStyle.TextMuted,
            AutoSize = true,
            Margin = new Padding(0, 1, 0, 0)
        };
        flow.Controls.Add(lblNote);

        card.Controls.Add(flow);
        return card;
    }

    private void BuildFeesPanel(Panel container)
    {
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 10 };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 70));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 30));

        int row = 0;
        
        // Header
        decimal totalFeesAndTax = _order.EtsyFees + _order.OffsiteAdFee;
        AddRow(layout, "Etsy Kesintileri ve Ücretler", $"-${totalFeesAndTax:N2}", row++, true, UiStyle.DangerColor);
        
        // Items
        if (_order.TaxPaidByBuyer > 0)
            AddRow(layout, "Alıcı Vergisi (Etsy alır)", $"-${_order.TaxPaidByBuyer:N2}", row++, false, UiStyle.TextMuted);
        
        if (_order.TransactionFee > 0)
            AddRow(layout, "İşlem Komisyonu (%6.5)", $"-${_order.TransactionFee:N2}", row++, false, UiStyle.TextMuted);
            
        if (_order.PaymentProcessingFee > 0)
            AddRow(layout, "Ödeme İşleme (%6.5 + Sabit Pay)", $"-${_order.PaymentProcessingFee:N2}", row++, false, UiStyle.TextMuted);
            
        if (_order.RegulatoryOperatingFee > 0)
            AddRow(layout, "Yasal İşlem Ücreti (%1.67)", $"-${_order.RegulatoryOperatingFee:N2}", row++, false, UiStyle.TextMuted);
            
        if (_order.OffsiteAdFee > 0)
            AddRow(layout, "Dış Reklam (Offsite Ads)", $"-${_order.OffsiteAdFee:N2}", row++, false, UiStyle.TextMuted);

        if (_order.ListingFee > 0)
            AddRow(layout, "İlan Yenileme", $"-${_order.ListingFee:N2}", row++, false, UiStyle.TextMuted);

        container.Controls.Add(layout);
    }

    private void BuildBottomPanel(Panel container)
    {
        var pnlProfit = new Panel { Dock = DockStyle.Fill, AutoSize = true, Name = "pnlProfit" };
        container.Controls.Add(pnlProfit);
        
        UpdateProfitLabel(pnlProfit);
    }
    
    private void UpdateProfitLabel(Panel? pnl = null)
    {
        pnl ??= Controls.Find("pnlProfit", true).FirstOrDefault() as Panel;
        if (pnl == null) return;
        
        pnl.Controls.Clear();
        
        decimal totalUnit = _productionCost + _shippingCost + _packagingCost;
        decimal totalProductCost = Math.Round(totalUnit * Math.Max(1, _order.Quantity), 2);
        decimal totalShipping = Math.Round(_shippingCost * Math.Max(1, _order.Quantity), 2);
        
        decimal netUSD = _order.GrandTotal - _order.EtsyFees - _order.OffsiteAdFee - totalProductCost;
        decimal netTRY = Math.Round(netUSD * _order.ExchangeRate, 2);
        
        var layout = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoSize = true,
            Padding = new Padding(0)
        };

        var lblProfit = new Label 
        { 
            Text = $"GERÇEK NET KÂR: ${netUSD:N2}  (₺{netTRY:N2})", 
            AutoSize = true,
            Font = new Font("Segoe UI", 15F, FontStyle.Bold),
            ForeColor = netUSD >= 0 ? UiStyle.SuccessColor : UiStyle.DangerColor,
            Margin = new Padding(0, 0, 0, 6)
        };
        layout.Controls.Add(lblProfit);

        var subInfo = new FlowLayoutPanel
        {
            AutoSize = true,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            Margin = new Padding(0, 0, 0, 4)
        };

        var lblCostBreakdown = new Label
        {
            Text = $"Üretim: ${_productionCost * _order.Quantity:N2} | 🚚 Kargo: ${totalShipping:N2} | Paketleme: ${_packagingCost * _order.Quantity:N2}",
            AutoSize = true,
            Font = new Font("Segoe UI", 9.5F),
            ForeColor = UiStyle.TextMuted,
            Padding = new Padding(0, 4, 12, 0)
        };
        subInfo.Controls.Add(lblCostBreakdown);

        var btnGenInvoice = new Button
        {
            Text = "⚡ Otomatik Fatura & Konşimento (PDF)",
            AutoSize = true,
            Height = 28,
            BackColor = Color.FromArgb(14, 165, 233), // Sky blue
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI Semibold", 8.5F, FontStyle.Bold),
            Cursor = Cursors.Hand,
            Padding = new Padding(8, 0, 8, 0),
            Margin = new Padding(0, 0, 6, 0)
        };
        btnGenInvoice.FlatAppearance.BorderSize = 0;
        btnGenInvoice.Click += async (_, _) =>
        {
            try
            {
                string pdfPath = EtsyInvoicePdfService.GenerateInvoicePdf(_order);
                await _orderCostRepo.SaveInvoicePathAsync(_order.ReceiptId.ToString(), pdfPath);
                InvoiceStorageService.OpenInvoice(pdfPath);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, $"Fatura oluşturulamadı: {ex.Message}", "Hata", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        };
        subInfo.Controls.Add(btnGenInvoice);

        if (_order.HasInvoice)
        {
            var btnInvoice = new Button
            {
                Text = "📄 Kargo Faturasını Aç",
                AutoSize = true,
                Height = 28,
                BackColor = Color.FromArgb(79, 70, 229),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI Semibold", 8.5F, FontStyle.Bold),
                Cursor = Cursors.Hand,
                Padding = new Padding(8, 0, 8, 0)
            };
            btnInvoice.FlatAppearance.BorderSize = 0;
            btnInvoice.Click += (_, _) => InvoiceStorageService.OpenInvoice(_order.InvoiceFilePath);
            subInfo.Controls.Add(btnInvoice);
        }

        layout.Controls.Add(subInfo);
        pnl.Controls.Add(layout);
    }

    private static void AddRow(TableLayoutPanel panel, string title, string value, int row, bool isHeader, Color color)
    {
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, isHeader ? 40 : 30));
        
        var lblTitle = new Label
        {
            Text = title,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            Font = new Font(isHeader ? "Segoe UI Semibold" : "Segoe UI", isHeader ? 11F : 9.5F),
            ForeColor = color
        };
        var lblValue = new Label
        {
            Text = value,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleRight,
            Font = new Font(isHeader ? "Segoe UI Semibold" : "Segoe UI", isHeader ? 11F : 9.5F),
            ForeColor = color
        };
        
        panel.Controls.Add(lblTitle, 0, row);
        panel.Controls.Add(lblValue, 1, row);
    }
}
