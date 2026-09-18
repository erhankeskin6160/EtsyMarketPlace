namespace SimilarProductsWinForms;

using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using SimilarProductsWinForms.Controls;
using SimilarProductsWinForms.Models;
using SimilarProductsWinForms.Services;

/// <summary>
/// Sipariş veya ürün bazında maliyet girişleri ve kargo faturası yönetimini sağlayan modern diyalog formu.
/// </summary>
internal sealed class CostDetailsPopupForm : Form
{
    private readonly ModernNumericUpDown _numProduction = new();
    private readonly ModernNumericUpDown _numShipping = new();
    private readonly ModernNumericUpDown _numPackaging = new();
    private readonly ModernCheckBox _chkApplyToAll = new();

    private readonly Panel _pnlFileBadge = new();
    private readonly Label _lblFileIcon = new();
    private readonly Label _lblInvoiceStatus = new();
    private readonly Button _btnViewInvoice = new();
    private readonly Button _btnRemoveInvoice = new();
    private readonly OrderFinancialSummary? _order;

    public decimal UnitCost => _numProduction.Value;
    public decimal UnitShippingCost => _numShipping.Value;
    public decimal UnitPackagingCost => _numPackaging.Value;
    public bool ApplyToAllOrdersOfListing => _chkApplyToAll.Checked;
    public string? InvoiceFilePath { get; private set; }
    public string StorageKey { get; }

    public CostDetailsPopupForm(OrderFinancialSummary order, OrderCostEntry? currentEntry = null)
    {
        _order = order;
        StorageKey = order.ReceiptId.ToString();
        Text = $"💰 Sipariş #{order.ReceiptId} Maliyet & Kargo Faturası Yönetimi";
        Size = new Size(560, 610);
        MinimumSize = new Size(520, 580);
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;

        InvoiceFilePath = currentEntry?.InvoiceFilePath ?? order.InvoiceFilePath;

        BuildLayout(
            storageKey: StorageKey,
            title: order.ProductTitle,
            customerText: order.DisplayCustomer,
            orderNo: $"#{order.ReceiptId}",
            prodCost: currentEntry?.UnitCost ?? order.UnitProductionCost,
            shipCost: currentEntry?.UnitShippingCost ?? order.UnitShippingCost,
            packCost: currentEntry?.UnitPackagingCost ?? order.UnitPackagingCost);

        UpdateInvoiceUi();
        UiStyle.ApplyTheme(this);
    }

    public CostDetailsPopupForm(ProductCostEntry? currentEntry)
    {
        _order = null;
        StorageKey = currentEntry?.ListingId ?? "custom";
        Text = "Maliyet & Kargo Faturası Detayları";
        Size = new Size(540, 540);
        MinimumSize = new Size(500, 500);
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;

        InvoiceFilePath = currentEntry?.InvoiceFilePath;

        BuildLayout(
            storageKey: StorageKey,
            title: currentEntry?.Title ?? "",
            customerText: null,
            orderNo: null,
            prodCost: currentEntry?.UnitCost ?? 0m,
            shipCost: currentEntry?.UnitShippingCost ?? 0m,
            packCost: currentEntry?.UnitPackagingCost ?? 0m);

        UpdateInvoiceUi();
        UiStyle.ApplyTheme(this);
    }

    private void BuildLayout(
        string storageKey,
        string title,
        string? customerText,
        string? orderNo,
        decimal prodCost,
        decimal shipCost,
        decimal packCost)
    {
        var mainPanel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            RowCount = 5,
            ColumnCount = 1,
            Padding = new Padding(20, 16, 20, 16),
            BackColor = UiStyle.CardBackground
        };

        // Satır 0: Üst Bilgi Kartı (Sipariş & Müşteri)
        mainPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        if (!string.IsNullOrWhiteSpace(orderNo) || !string.IsNullOrWhiteSpace(customerText))
        {
            var pnlInfo = new ModernCardPanel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(14, 10, 14, 10),
                Margin = new Padding(0, 0, 0, 12),
                CornerRadius = 8,
                CardColor = Color.FromArgb(30, 41, 59),
                BorderColor = UiStyle.BorderColor,
                AutoSize = true
            };

            var infoFlow = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                AutoSize = true
            };

            if (!string.IsNullOrWhiteSpace(orderNo))
            {
                infoFlow.Controls.Add(new Label
                {
                    Text = $"📦 Sipariş: {orderNo}  |  👤 Müşteri: {customerText ?? "—"}",
                    Font = new Font("Segoe UI Semibold", 9.5F, FontStyle.Bold),
                    ForeColor = UiStyle.PrimaryHover,
                    AutoSize = true,
                    Margin = new Padding(0, 0, 0, 3)
                });
            }

            if (!string.IsNullOrWhiteSpace(title))
            {
                infoFlow.Controls.Add(new Label
                {
                    Text = title.Length > 70 ? title[..67] + "..." : title,
                    Font = new Font("Segoe UI", 8.5F),
                    ForeColor = UiStyle.TextMuted,
                    AutoSize = true
                });
            }

            pnlInfo.Controls.Add(infoFlow);
            mainPanel.Controls.Add(pnlInfo, 0, 0);
        }
        else
        {
            mainPanel.Controls.Add(new Panel { Height = 0 }, 0, 0);
        }

        // Satır 1: Maliyet Kalemleri Kartı
        mainPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        var costCard = new ModernCardPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(14, 12, 14, 12),
            Margin = new Padding(0, 0, 0, 12),
            CornerRadius = 8,
            CardColor = Color.FromArgb(24, 24, 34),
            BorderColor = UiStyle.BorderColor,
            AutoSize = true
        };

        var costLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 5,
            AutoSize = true
        };

        var lblCostHeader = new Label
        {
            Text = "📊 Birim Maliyet Kalemleri (USD $)",
            Font = new Font("Segoe UI Semibold", 9.5F, FontStyle.Bold),
            ForeColor = Color.White,
            AutoSize = true,
            Margin = new Padding(0, 0, 0, 8)
        };
        costLayout.Controls.Add(lblCostHeader, 0, 0);

        _numProduction.DecimalPlaces = 2;
        _numProduction.Maximum = 10000;
        _numProduction.Value = prodCost;
        AddRow(costLayout, "Üretim / Hammadde Maliyeti ($):", _numProduction, 1);

        _numShipping.DecimalPlaces = 2;
        _numShipping.Maximum = 10000;
        _numShipping.Value = shipCost;
        AddRow(costLayout, "Sipariş Kargo Maliyeti ($):", _numShipping, 2);

        _numPackaging.DecimalPlaces = 2;
        _numPackaging.Maximum = 10000;
        _numPackaging.Value = packCost;
        AddRow(costLayout, "Paketleme & Kutu Maliyeti ($):", _numPackaging, 3);

        if (_order != null && _order.ListingId > 0)
        {
            _chkApplyToAll.Text = "Bu ürünün (Listing) maliyeti girilmemiş tüm siparişlerine de uygula";
            _chkApplyToAll.Font = new Font("Segoe UI", 8.5F);
            _chkApplyToAll.ForeColor = UiStyle.TextDark;
            _chkApplyToAll.AutoSize = true;
            _chkApplyToAll.Margin = new Padding(0, 6, 0, 0);
            costLayout.Controls.Add(_chkApplyToAll, 0, 4);
        }

        costCard.Controls.Add(costLayout);
        mainPanel.Controls.Add(costCard, 0, 1);

        // Satır 2: Kargo Faturası & Resmi Belge Kartı
        mainPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        var invoiceGroup = BuildInvoiceSection(storageKey);
        mainPanel.Controls.Add(invoiceGroup, 0, 2);

        // Satır 3: Esnek Dikey Boşluk (Spacer)
        mainPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        mainPanel.Controls.Add(new Panel { Dock = DockStyle.Fill }, 0, 3);

        // Satır 4: Alt Buton Barı (Sabit 42px)
        mainPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 46));
        var bottomBar = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 3,
            RowCount = 1,
            Margin = new Padding(0, 6, 0, 0)
        };
        bottomBar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); // Esnek boşluk
        bottomBar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 110)); // İptal
        bottomBar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 220)); // Kaydet

        var btnCancel = new Button
        {
            Text = "İptal",
            Dock = DockStyle.Fill,
            BackColor = Color.FromArgb(51, 65, 85), // Slate-700
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI Semibold", 9.5F, FontStyle.Bold),
            Cursor = Cursors.Hand,
            Margin = new Padding(0, 0, 8, 0)
        };
        btnCancel.FlatAppearance.BorderSize = 0;
        btnCancel.Click += (s, e) =>
        {
            DialogResult = DialogResult.Cancel;
            Close();
        };

        var btnSave = new Button
        {
            Text = "💾 Değişiklikleri Kaydet",
            Dock = DockStyle.Fill,
            BackColor = UiStyle.PrimaryColor,
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI Semibold", 10F, FontStyle.Bold),
            Cursor = Cursors.Hand,
            Margin = new Padding(0)
        };
        btnSave.FlatAppearance.BorderSize = 0;
        btnSave.Click += (s, e) =>
        {
            DialogResult = DialogResult.OK;
            Close();
        };

        bottomBar.Controls.Add(new Panel(), 0, 0);
        bottomBar.Controls.Add(btnCancel, 1, 0);
        bottomBar.Controls.Add(btnSave, 2, 0);
        mainPanel.Controls.Add(bottomBar, 0, 4);

        Controls.Add(mainPanel);
    }

    private Control BuildInvoiceSection(string storageKey)
    {
        var card = new ModernCardPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(14, 12, 14, 12),
            Margin = new Padding(0, 0, 0, 8),
            CornerRadius = 8,
            CardColor = Color.FromArgb(24, 24, 34),
            BorderColor = UiStyle.BorderColor,
            AutoSize = true
        };

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            RowCount = 3,
            ColumnCount = 1,
            AutoSize = true
        };

        // Başlık
        var lblTitle = new Label
        {
            Text = "📦 Kargo Faturası & Resmi Belge Yönetimi",
            Dock = DockStyle.Fill,
            Font = new Font("Segoe UI Semibold", 9.5F, FontStyle.Bold),
            ForeColor = Color.White,
            AutoSize = true,
            Margin = new Padding(0, 0, 0, 4)
        };
        layout.Controls.Add(lblTitle, 0, 0);

        // Aksiyon Butonları
        var actionsPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            AutoSize = true,
            Margin = new Padding(0, 4, 0, 10)
        };

        // 1. ÖZEL KARGO FATURASI YÜKLEME BUTONU
        var btnUploadInvoice = new Button
        {
            Text = "📎 Kargo Faturası Yükle (PDF / Resim)",
            AutoSize = true,
            Height = 36,
            BackColor = Color.FromArgb(79, 70, 229), // Indigo #4F46E5
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI Semibold", 9F, FontStyle.Bold),
            Cursor = Cursors.Hand,
            Padding = new Padding(10, 0, 10, 0),
            Margin = new Padding(0, 0, 8, 0)
        };
        btnUploadInvoice.FlatAppearance.BorderSize = 0;
        btnUploadInvoice.Click += (s, e) =>
        {
            using var ofd = new OpenFileDialog
            {
                Title = "Kargo Faturası Seçin (PDF veya Resim)",
                Filter = "Kargo Faturaları (*.pdf;*.png;*.jpg;*.jpeg;*.webp)|*.pdf;*.png;*.jpg;*.jpeg;*.webp|Tüm Dosyalar (*.*)|*.*"
            };
            if (ofd.ShowDialog(this) == DialogResult.OK)
            {
                string? savedPath = InvoiceStorageService.SaveInvoiceFile(ofd.FileName, storageKey);
                InvoiceFilePath = savedPath ?? ofd.FileName;
                UpdateInvoiceUi();
            }
        };
        actionsPanel.Controls.Add(btnUploadInvoice);

        // 2. OTOMATİK FATURA OLUŞTUR BUTONU
        if (_order != null)
        {
            var btnAutoInvoice = new Button
            {
                Text = "⚡ Otomatik Fatura Oluştur (PDF)",
                AutoSize = true,
                Height = 36,
                BackColor = Color.FromArgb(16, 185, 129), // Emerald #10B981
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI Semibold", 9F, FontStyle.Bold),
                Cursor = Cursors.Hand,
                Padding = new Padding(10, 0, 10, 0),
                Margin = new Padding(0, 0, 8, 0)
            };
            btnAutoInvoice.FlatAppearance.BorderSize = 0;
            btnAutoInvoice.Click += (s, e) =>
            {
                try
                {
                    // Formdaki güncel maliyetleri anlık olarak siparişe aktar ki faturaya tam yansısın
                    var updatedOrder = _order with
                    {
                        UnitProductionCost = _numProduction.Value,
                        UnitShippingCost = _numShipping.Value,
                        UnitPackagingCost = _numPackaging.Value
                    };

                    string pdfPath = EtsyInvoicePdfService.GenerateInvoicePdf(updatedOrder);
                    InvoiceFilePath = pdfPath;
                    UpdateInvoiceUi();
                    MessageBox.Show(this, 
                        $"Sipariş #{_order.ReceiptId} için resmi sevk irsaliyesi ve maliyet dökümlü fatura PDF'i başarıyla oluşturuldu!\n\nDosya: {Path.GetFileName(pdfPath)}", 
                        "🧾 Fatura Oluşturuldu", 
                        MessageBoxButtons.OK, 
                        MessageBoxIcon.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show(this, $"Fatura oluşturulamadı: {ex.Message}", "Hata", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            };
            actionsPanel.Controls.Add(btnAutoInvoice);
        }

        layout.Controls.Add(actionsPanel, 0, 1);

        // 3. DOSYA DURUM ROZETİ (Görsel Durum Kartı)
        _pnlFileBadge.Dock = DockStyle.Fill;
        _pnlFileBadge.Height = 42;
        _pnlFileBadge.Padding = new Padding(10, 6, 10, 6);
        _pnlFileBadge.Margin = new Padding(0);

        var badgeLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 4,
            RowCount = 1
        };
        badgeLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 28)); // İkon
        badgeLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); // Durum Metni
        badgeLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 80));  // Aç
        badgeLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 85));  // Kaldır

        _lblFileIcon.Text = "📄";
        _lblFileIcon.Dock = DockStyle.Fill;
        _lblFileIcon.TextAlign = ContentAlignment.MiddleCenter;
        _lblFileIcon.Font = new Font("Segoe UI Emoji", 11F);

        _lblInvoiceStatus.Dock = DockStyle.Fill;
        _lblInvoiceStatus.TextAlign = ContentAlignment.MiddleLeft;
        _lblInvoiceStatus.Font = new Font("Segoe UI", 9F);
        _lblInvoiceStatus.AutoEllipsis = true;

        _btnViewInvoice.Text = "👁️ Aç";
        _btnViewInvoice.Dock = DockStyle.Fill;
        _btnViewInvoice.Height = 28;
        _btnViewInvoice.BackColor = Color.FromArgb(14, 165, 233); // Sky Blue #0EA5E9
        _btnViewInvoice.ForeColor = Color.White;
        _btnViewInvoice.FlatStyle = FlatStyle.Flat;
        _btnViewInvoice.Font = new Font("Segoe UI Semibold", 8.5F, FontStyle.Bold);
        _btnViewInvoice.Cursor = Cursors.Hand;
        _btnViewInvoice.Margin = new Padding(0, 0, 6, 0);
        _btnViewInvoice.FlatAppearance.BorderSize = 0;
        _btnViewInvoice.Click += (s, e) =>
        {
            if (!string.IsNullOrWhiteSpace(InvoiceFilePath))
            {
                if (!InvoiceStorageService.OpenInvoice(InvoiceFilePath))
                {
                    MessageBox.Show(this, "Fatura dosyası açılamadı veya dosya taşınmış/silinmiş.", "Uyarı", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            }
        };

        _btnRemoveInvoice.Text = "❌ Kaldır";
        _btnRemoveInvoice.Dock = DockStyle.Fill;
        _btnRemoveInvoice.Height = 28;
        _btnRemoveInvoice.BackColor = Color.FromArgb(239, 68, 68); // Red #EF4444
        _btnRemoveInvoice.ForeColor = Color.White;
        _btnRemoveInvoice.FlatStyle = FlatStyle.Flat;
        _btnRemoveInvoice.Font = new Font("Segoe UI Semibold", 8.5F, FontStyle.Bold);
        _btnRemoveInvoice.Cursor = Cursors.Hand;
        _btnRemoveInvoice.Margin = new Padding(0);
        _btnRemoveInvoice.FlatAppearance.BorderSize = 0;
        _btnRemoveInvoice.Click += (s, e) =>
        {
            InvoiceFilePath = null;
            UpdateInvoiceUi();
        };

        badgeLayout.Controls.Add(_lblFileIcon, 0, 0);
        badgeLayout.Controls.Add(_lblInvoiceStatus, 1, 0);
        badgeLayout.Controls.Add(_btnViewInvoice, 2, 0);
        badgeLayout.Controls.Add(_btnRemoveInvoice, 3, 0);

        _pnlFileBadge.Controls.Add(badgeLayout);
        layout.Controls.Add(_pnlFileBadge, 0, 2);

        card.Controls.Add(layout);
        return card;
    }

    private void UpdateInvoiceUi()
    {
        bool hasFile = !string.IsNullOrWhiteSpace(InvoiceFilePath) && File.Exists(InvoiceFilePath);
        _btnViewInvoice.Visible = hasFile;
        _btnRemoveInvoice.Visible = !string.IsNullOrWhiteSpace(InvoiceFilePath);

        if (hasFile)
        {
            long fileSize = 0;
            try
            {
                fileSize = new FileInfo(InvoiceFilePath!).Length;
            }
            catch { /* ignore */ }

            string sizeStr = fileSize > 1024 * 1024 
                ? $"{(fileSize / 1024f / 1024f):N1} MB" 
                : $"{(fileSize / 1024f):N0} KB";

            _lblFileIcon.Text = "✅";
            _lblInvoiceStatus.Text = $"{InvoiceStorageService.GetDisplayFileName(InvoiceFilePath)} ({sizeStr})";
            _lblInvoiceStatus.ForeColor = Color.FromArgb(52, 211, 153); // Emerald
            _pnlFileBadge.BackColor = Color.FromArgb(15, 43, 34); // Koyu zümrüt kart
        }
        else if (!string.IsNullOrWhiteSpace(InvoiceFilePath))
        {
            _lblFileIcon.Text = "⚠️";
            _lblInvoiceStatus.Text = $"Dosya bulunamadı: {Path.GetFileName(InvoiceFilePath)}";
            _lblInvoiceStatus.ForeColor = UiStyle.WarningColor;
            _pnlFileBadge.BackColor = Color.FromArgb(40, 30, 20); // Uyarı kart
        }
        else
        {
            _lblFileIcon.Text = "ℹ️";
            _lblInvoiceStatus.Text = "Henüz kargo faturası yüklenmedi (isteğe bağlı)";
            _lblInvoiceStatus.ForeColor = UiStyle.TextMuted;
            _pnlFileBadge.BackColor = Color.FromArgb(15, 23, 42); // Slate koyu kart
        }
    }

    private void AddRow(TableLayoutPanel panel, string labelText, ModernNumericUpDown num, int row)
    {
        var container = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            Margin = new Padding(0, 0, 0, 8),
            AutoSize = true
        };
        container.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 55));
        container.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 45));

        var lbl = new Label
        {
            Text = labelText,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            Font = new Font("Segoe UI", 9.5F),
            ForeColor = UiStyle.TextDark
        };

        num.Dock = DockStyle.Fill;
        num.Font = new Font("Segoe UI", 10F);
        num.BackColor = UiStyle.InputBackground;
        num.ForeColor = UiStyle.TextDark;
        num.BorderStyle = BorderStyle.FixedSingle;

        container.Controls.Add(lbl, 0, 0);
        container.Controls.Add(num, 1, 0);

        panel.Controls.Add(container, 0, row);
    }
}
