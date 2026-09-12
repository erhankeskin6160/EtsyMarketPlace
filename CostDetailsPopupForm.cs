namespace SimilarProductsWinForms;

using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using SimilarProductsWinForms.Controls;
using SimilarProductsWinForms.Models;
using SimilarProductsWinForms.Services;

internal sealed class CostDetailsPopupForm : Form
{
    private readonly ModernNumericUpDown _numProduction = new();
    private readonly ModernNumericUpDown _numShipping = new();
    private readonly ModernNumericUpDown _numPackaging = new();
    private readonly ModernCheckBox _chkApplyToAll = new();

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
        Text = $"💰 Sipariş #{order.ReceiptId} Maliyet & Fatura Düzenle";
        Size = new Size(540, 560);
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
        Size = new Size(500, 480);
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
            RowCount = 8,
            ColumnCount = 1,
            Padding = new Padding(20, 14, 20, 14),
            BackColor = UiStyle.CardBackground
        };

        int row = 0;

        // Üst Bilgi Kartı (Sipariş & Müşteri Bilgisi)
        if (!string.IsNullOrWhiteSpace(orderNo) || !string.IsNullOrWhiteSpace(customerText))
        {
            var pnlInfo = new ModernCardPanel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(12, 8, 12, 8),
                Margin = new Padding(0, 0, 0, 10),
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
                    Margin = new Padding(0, 0, 0, 2)
                });
            }
            if (!string.IsNullOrWhiteSpace(title))
            {
                infoFlow.Controls.Add(new Label
                {
                    Text = title.Length > 65 ? title[..65] + "..." : title,
                    Font = new Font("Segoe UI", 8.5F),
                    ForeColor = UiStyle.TextMuted,
                    AutoSize = true
                });
            }
            pnlInfo.Controls.Add(infoFlow);
            mainPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            mainPanel.Controls.Add(pnlInfo, 0, row++);
        }

        _numProduction.DecimalPlaces = 2;
        _numProduction.Maximum = 10000;
        _numProduction.Value = prodCost;
        AddRow(mainPanel, "Üretim / Hammadde Maliyeti ($):", _numProduction, row++);

        _numShipping.DecimalPlaces = 2;
        _numShipping.Maximum = 10000;
        _numShipping.Value = shipCost;
        AddRow(mainPanel, "Sipariş Kargo Maliyeti ($):", _numShipping, row++);

        _numPackaging.DecimalPlaces = 2;
        _numPackaging.Maximum = 10000;
        _numPackaging.Value = packCost;
        AddRow(mainPanel, "Paketleme & Kutu Maliyeti ($):", _numPackaging, row++);

        // Bulk apply to all orders of listing checkbox
        if (_order != null && _order.ListingId > 0)
        {
            _chkApplyToAll.Text = "Bu ürünün (Listing) maliyeti girilmemiş tüm siparişlerine de uygula";
            _chkApplyToAll.Font = new Font("Segoe UI", 8.5F);
            _chkApplyToAll.ForeColor = UiStyle.TextDark;
            _chkApplyToAll.AutoSize = true;
            _chkApplyToAll.Margin = new Padding(0, 4, 0, 6);
            mainPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            mainPanel.Controls.Add(_chkApplyToAll, 0, row++);
        }

        // Invoice Section Card
        var invoiceGroup = BuildInvoiceSection(storageKey);
        mainPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        mainPanel.Controls.Add(invoiceGroup, 0, row++);

        // Save Button
        var btnSave = new Button
        {
            Text = "💾 Sipariş Maliyetini & Faturayı Kaydet",
            Dock = DockStyle.Fill,
            BackColor = UiStyle.PrimaryColor,
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Margin = new Padding(0, 10, 0, 0),
            Font = new Font("Segoe UI Semibold", 10F, FontStyle.Bold),
            Cursor = Cursors.Hand
        };
        btnSave.FlatAppearance.BorderSize = 0;
        btnSave.Click += (s, e) =>
        {
            DialogResult = DialogResult.OK;
            Close();
        };

        mainPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); // Spacer
        mainPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 45)); // Save button height
        mainPanel.Controls.Add(btnSave, 0, row);

        Controls.Add(mainPanel);
    }

    private Control BuildInvoiceSection(string storageKey)
    {
        var card = new ModernCardPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(12, 10, 12, 10),
            Margin = new Padding(0, 4, 0, 8),
            CornerRadius = 8,
            CardColor = Color.FromArgb(24, 24, 32),
            BorderColor = UiStyle.BorderColor
        };

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            RowCount = 3,
            ColumnCount = 1,
            AutoSize = true
        };

        var lblTitle = new Label
        {
            Text = "📄 İsteğe Bağlı Kargo Faturası (PDF / Resim):",
            Dock = DockStyle.Fill,
            Font = new Font("Segoe UI Semibold", 9F, FontStyle.Bold),
            ForeColor = UiStyle.TextDark,
            AutoSize = true,
            Margin = new Padding(0, 0, 0, 6)
        };
        layout.Controls.Add(lblTitle, 0, 0);

        var actionsPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            AutoSize = true,
            Margin = new Padding(0, 0, 0, 6)
        };

        if (_order != null)
        {
            var btnAutoInvoice = new Button
            {
                Text = "⚡ Otomatik Fatura (PDF)",
                AutoSize = true,
                Height = 30,
                BackColor = Color.FromArgb(14, 165, 233), // Sky Blue
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 8.5F, FontStyle.Bold),
                Cursor = Cursors.Hand,
                Margin = new Padding(0, 0, 6, 0)
            };
            btnAutoInvoice.FlatAppearance.BorderSize = 0;
            btnAutoInvoice.Click += (s, e) =>
            {
                try
                {
                    string pdfPath = EtsyInvoicePdfService.GenerateInvoicePdf(_order);
                    InvoiceFilePath = pdfPath;
                    UpdateInvoiceUi();
                    MessageBox.Show(this, $"Sipariş #{_order.ReceiptId} için resmi fatura ve sevk irsaliyesi PDF'i başarıyla oluşturuldu!\n\nDosya: {pdfPath}", "🧾 Fatura Oluşturuldu", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show(this, $"Fatura oluşturulamadı: {ex.Message}", "Hata", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            };
            actionsPanel.Controls.Add(btnAutoInvoice);
        }

        var btnPick = new Button
        {
            Text = "📎 Fatura Seç...",
            AutoSize = true,
            Height = 30,
            BackColor = Color.FromArgb(79, 70, 229),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 8.5F, FontStyle.Bold),
            Cursor = Cursors.Hand,
            Margin = new Padding(0, 0, 6, 0)
        };
        btnPick.FlatAppearance.BorderSize = 0;
        btnPick.Click += (s, e) =>
        {
            using var ofd = new OpenFileDialog
            {
                Title = "Kargo Faturası Seçin",
                Filter = "Fatura Dosyaları (*.pdf;*.png;*.jpg;*.jpeg;*.webp)|*.pdf;*.png;*.jpg;*.jpeg;*.webp|Tüm Dosyalar (*.*)|*.*"
            };
            if (ofd.ShowDialog(this) == DialogResult.OK)
            {
                // Invoices klasörüne güvenle kopyala
                string? savedPath = InvoiceStorageService.SaveInvoiceFile(ofd.FileName, storageKey);
                InvoiceFilePath = savedPath ?? ofd.FileName;
                UpdateInvoiceUi();
            }
        };

        _btnViewInvoice.Text = "👁️ Aç";
        _btnViewInvoice.AutoSize = true;
        _btnViewInvoice.Height = 30;
        _btnViewInvoice.BackColor = Color.FromArgb(16, 185, 129);
        _btnViewInvoice.ForeColor = Color.White;
        _btnViewInvoice.FlatStyle = FlatStyle.Flat;
        _btnViewInvoice.Font = new Font("Segoe UI", 8.5F, FontStyle.Bold);
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
        _btnRemoveInvoice.AutoSize = true;
        _btnRemoveInvoice.Height = 30;
        _btnRemoveInvoice.BackColor = Color.FromArgb(239, 68, 68);
        _btnRemoveInvoice.ForeColor = Color.White;
        _btnRemoveInvoice.FlatStyle = FlatStyle.Flat;
        _btnRemoveInvoice.Font = new Font("Segoe UI", 8.5F, FontStyle.Bold);
        _btnRemoveInvoice.Cursor = Cursors.Hand;
        _btnRemoveInvoice.FlatAppearance.BorderSize = 0;
        _btnRemoveInvoice.Click += (s, e) =>
        {
            InvoiceFilePath = null;
            UpdateInvoiceUi();
        };

        actionsPanel.Controls.Add(btnPick);
        actionsPanel.Controls.Add(_btnViewInvoice);
        actionsPanel.Controls.Add(_btnRemoveInvoice);
        layout.Controls.Add(actionsPanel, 0, 1);

        _lblInvoiceStatus.Dock = DockStyle.Fill;
        _lblInvoiceStatus.Font = new Font("Segoe UI", 8F);
        _lblInvoiceStatus.ForeColor = UiStyle.TextMuted;
        _lblInvoiceStatus.AutoEllipsis = true;
        _lblInvoiceStatus.AutoSize = true;
        layout.Controls.Add(_lblInvoiceStatus, 0, 2);

        card.Controls.Add(layout);
        return card;
    }

    private void UpdateInvoiceUi()
    {
        bool hasFile = !string.IsNullOrWhiteSpace(InvoiceFilePath) && File.Exists(InvoiceFilePath);
        _btnViewInvoice.Enabled = hasFile;
        _btnRemoveInvoice.Enabled = !string.IsNullOrWhiteSpace(InvoiceFilePath);

        if (hasFile)
        {
            _lblInvoiceStatus.Text = $"✅ {InvoiceStorageService.GetDisplayFileName(InvoiceFilePath)}";
            _lblInvoiceStatus.ForeColor = Color.FromArgb(52, 211, 153); // Emerald
        }
        else if (!string.IsNullOrWhiteSpace(InvoiceFilePath))
        {
            _lblInvoiceStatus.Text = $"⚠️ Dosya bulunamadı: {Path.GetFileName(InvoiceFilePath)}";
            _lblInvoiceStatus.ForeColor = UiStyle.WarningColor;
        }
        else
        {
            _lblInvoiceStatus.Text = "ℹ️ Henüz kargo faturası yüklenmedi (isteğe bağlı)";
            _lblInvoiceStatus.ForeColor = UiStyle.TextMuted;
        }
    }

    private void AddRow(TableLayoutPanel panel, string labelText, ModernNumericUpDown num, int row)
    {
        panel.RowStyles.Add(new RowStyle(SizeType.AutoSize));

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
