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
    private readonly NumericUpDown _numProduction = new();
    private readonly NumericUpDown _numShipping = new();
    private readonly NumericUpDown _numPackaging = new();

    private readonly Label _lblInvoiceStatus = new();
    private readonly Button _btnViewInvoice = new();
    private readonly Button _btnRemoveInvoice = new();

    public decimal UnitCost => _numProduction.Value;
    public decimal UnitShippingCost => _numShipping.Value;
    public decimal UnitPackagingCost => _numPackaging.Value;
    public string? InvoiceFilePath { get; private set; }

    public CostDetailsPopupForm(ProductCostEntry? currentEntry)
    {
        Text = "Maliyet & Kargo Faturası Detayları";
        Size = new Size(460, 440);
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;

        InvoiceFilePath = currentEntry?.InvoiceFilePath;

        BuildLayout(currentEntry);
        UpdateInvoiceUi();
        UiStyle.ApplyTheme(this);
    }

    private void BuildLayout(ProductCostEntry? currentEntry)
    {
        var mainPanel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            RowCount = 6,
            ColumnCount = 1,
            Padding = new Padding(24, 16, 24, 16),
            BackColor = UiStyle.CardBackground
        };

        int row = 0;

        _numProduction.DecimalPlaces = 2;
        _numProduction.Maximum = 10000;
        _numProduction.Value = currentEntry?.UnitCost ?? 0m;
        AddRow(mainPanel, "Üretim Maliyeti ($):", _numProduction, row++);

        _numShipping.DecimalPlaces = 2;
        _numShipping.Maximum = 10000;
        _numShipping.Value = currentEntry?.UnitShippingCost ?? 0m;
        AddRow(mainPanel, "Kargo Maliyeti ($):", _numShipping, row++);

        _numPackaging.DecimalPlaces = 2;
        _numPackaging.Maximum = 10000;
        _numPackaging.Value = currentEntry?.UnitPackagingCost ?? 0m;
        AddRow(mainPanel, "Paketleme Maliyeti ($):", _numPackaging, row++);

        // Invoice Section Card
        var invoiceGroup = BuildInvoiceSection(currentEntry?.ListingId ?? "order");
        mainPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        mainPanel.Controls.Add(invoiceGroup, 0, row++);

        // Save Button
        var btnSave = new Button
        {
            Text = "💾 Maliyetleri & Faturayı Kaydet",
            Dock = DockStyle.Fill,
            BackColor = UiStyle.PrimaryColor,
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Margin = new Padding(0, 15, 0, 0),
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

    private Control BuildInvoiceSection(string listingId)
    {
        var card = new ModernCardPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(12, 10, 12, 10),
            Margin = new Padding(0, 5, 0, 10),
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
                string? savedPath = InvoiceStorageService.SaveInvoiceFile(ofd.FileName, listingId);
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

    private void AddRow(TableLayoutPanel panel, string labelText, NumericUpDown num, int row)
    {
        panel.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        var container = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            Margin = new Padding(0, 0, 0, 10),
            AutoSize = true
        };
        container.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 55));
        container.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 45));

        var lbl = new Label
        {
            Text = labelText,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            Font = new Font("Segoe UI", 10F),
            ForeColor = UiStyle.TextDark
        };

        num.Dock = DockStyle.Fill;
        num.Font = new Font("Segoe UI", 10.5F);
        num.BackColor = UiStyle.InputBackground;
        num.ForeColor = UiStyle.TextDark;
        num.BorderStyle = BorderStyle.FixedSingle;

        container.Controls.Add(lbl, 0, 0);
        container.Controls.Add(num, 1, 0);

        panel.Controls.Add(container, 0, row);
    }
}

