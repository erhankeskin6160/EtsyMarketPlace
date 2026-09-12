namespace SimilarProductsWinForms;

using System.Globalization;
using SimilarProductsWinForms.Controls;
using SimilarProductsWinForms.Models;
using SimilarProductsWinForms.Services;

internal sealed class ProductCostManagerForm : Form
{
    private readonly SqliteProductCostRepository _repository = new();
    private readonly DataGridView _grid = new();
    private readonly TextBox _txtListingId = new();
    private readonly TextBox _txtTitle = new();
    private readonly ModernNumericUpDown _numUnitCost = new();
    private readonly ModernNumericUpDown _numShippingCost = new();
    private readonly Button _btnSave = new();
    private readonly Button _btnDelete = new();

    public ProductCostManagerForm()
    {
        Text = "🏷️ Ürün Üretim & Maliyet Yönetimi (COGS)";
        Size = new Size(920, 580);
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;

        BuildLayout();
        UiStyle.ApplyTheme(this);
        LoadData();
    }

    /// <summary>
    /// Finansal rapordaki siparişten çift tıklanarak açılınca listing_id ve başlığı önceden doldurur.
    /// </summary>
    public ProductCostManagerForm(string listingId, string title) : this()
    {
        if (!string.IsNullOrWhiteSpace(listingId))
        {
            _txtListingId.Text = listingId;
            _txtTitle.Text = title;

            // Eğer bu listing için mevcut kayıt varsa grid'de seç
            foreach (DataGridViewRow row in _grid.Rows)
            {
                if (row.Cells["ListingId"].Value?.ToString() == listingId)
                {
                    _grid.ClearSelection();
                    row.Selected = true;
                    _grid.FirstDisplayedScrollingRowIndex = row.Index;
                    break;
                }
            }
        }
    }


    private void BuildLayout()
    {
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            RowCount = 3,
            Padding = new Padding(12),
        };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 45));  // Başlık
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); // Grid
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 110)); // Form Paneli
        Controls.Add(root);

        root.Controls.Add(new Label
        {
            Dock = DockStyle.Fill,
            Text = "🏷️ Ürün Maliyetleri (Üretim / Tedarik & Kargo Ambalaj)",
            Font = UiStyle.TitleFont,
            ForeColor = UiStyle.TextDark,
            TextAlign = ContentAlignment.MiddleLeft,
        }, 0, 0);

        UiStyle.ConfigureBaseGrid(_grid);
        _grid.Dock = DockStyle.Fill;
        _grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        _grid.MultiSelect = false;
        _grid.SelectionChanged += OnGridSelectionChanged;

        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "ListingId", HeaderText = "İlan / Ürün ID", Width = 130 });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Title", HeaderText = "Ürün Adı / Açıklama", AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "UnitCost", HeaderText = "Birim İmalat ($)", Width = 130 });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "UnitShipping", HeaderText = "Birim Kargo ($)", Width = 130 });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "TotalCost", HeaderText = "Toplam Maliyet ($)", Width = 140 });

        root.Controls.Add(_grid, 0, 1);

        // Form Paneli
        var formCard = new ModernCardPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(10),
            CornerRadius = 10,
            CardColor = UiStyle.CardBackground,
            BorderColor = UiStyle.BorderColor,
        };

        var formLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 5,
            RowCount = 2,
        };
        formLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 140)); // ID
        formLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); // Title
        formLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 130)); // Cost
        formLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 130)); // Shipping
        formLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 140)); // Buttons

        // Satır 1: Etiketler
        formLayout.Controls.Add(CreateLabel("İlan / Ürün ID:"), 0, 0);
        formLayout.Controls.Add(CreateLabel("Ürün Adı / Açıklama:"), 1, 0);
        formLayout.Controls.Add(CreateLabel("Birim İmalat ($):"), 2, 0);
        formLayout.Controls.Add(CreateLabel("Birim Kargo ($):"), 3, 0);

        // Satır 2: İnputlar & Butonlar
        _txtListingId.Dock = DockStyle.Fill;
        _txtTitle.Dock = DockStyle.Fill;

        _numUnitCost.Dock = DockStyle.Fill;
        _numUnitCost.DecimalPlaces = 2;
        _numUnitCost.Maximum = 10000;
        _numUnitCost.Increment = 0.50m;

        _numShippingCost.Dock = DockStyle.Fill;
        _numShippingCost.DecimalPlaces = 2;
        _numShippingCost.Maximum = 10000;
        _numShippingCost.Increment = 0.50m;

        formLayout.Controls.Add(_txtListingId, 0, 1);
        formLayout.Controls.Add(_txtTitle, 1, 1);
        formLayout.Controls.Add(_numUnitCost, 2, 1);
        formLayout.Controls.Add(_numShippingCost, 3, 1);

        var btnPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
        };

        _btnSave.Text = "💾 Kaydet";
        _btnSave.AutoSize = true;
        _btnSave.Click += OnSaveClicked;

        _btnDelete.Text = "🗑️ Sil";
        _btnDelete.AutoSize = true;
        _btnDelete.Click += OnDeleteClicked;

        btnPanel.Controls.Add(_btnSave);
        btnPanel.Controls.Add(_btnDelete);

        formLayout.Controls.Add(btnPanel, 4, 1);

        formCard.Controls.Add(formLayout);
        root.Controls.Add(formCard, 0, 2);
    }

    private static Label CreateLabel(string text)
    {
        return new Label
        {
            Text = text,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.BottomLeft,
            Font = new Font("Segoe UI", 8.5F, FontStyle.Bold),
            ForeColor = UiStyle.TextMuted,
        };
    }

    private async void LoadData()
    {
        try
        {
            var list = await _repository.GetAllAsync();
            _grid.Rows.Clear();

            foreach (var item in list)
            {
                _grid.Rows.Add(
                    item.ListingId,
                    item.Title,
                    $"${item.UnitCost:N2}",
                    $"${item.UnitShippingCost:N2}",
                    $"${item.TotalUnitCost:N2}");
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Maliyetler yüklenirken hata oluştu: {ex.Message}", "Hata", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void OnGridSelectionChanged(object? sender, EventArgs e)
    {
        if (_grid.SelectedRows.Count == 0) return;
        var row = _grid.SelectedRows[0];

        _txtListingId.Text = Convert.ToString(row.Cells["ListingId"].Value) ?? "";
        _txtTitle.Text = Convert.ToString(row.Cells["Title"].Value) ?? "";

        var costStr = Convert.ToString(row.Cells["UnitCost"].Value)?.Replace("$", "").Trim() ?? "0";
        var shipStr = Convert.ToString(row.Cells["UnitShipping"].Value)?.Replace("$", "").Trim() ?? "0";

        if (decimal.TryParse(costStr, NumberStyles.Any, CultureInfo.InvariantCulture, out var c)) _numUnitCost.Value = c;
        if (decimal.TryParse(shipStr, NumberStyles.Any, CultureInfo.InvariantCulture, out var s)) _numShippingCost.Value = s;
    }

    private async void OnSaveClicked(object? sender, EventArgs e)
    {
        var id = _txtListingId.Text.Trim();
        var title = _txtTitle.Text.Trim();

        if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(title))
        {
            MessageBox.Show("Lütfen İlan / Ürün ID ve Ürün Adını doldurun.", "Eksik Bilgi", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        var entry = new ProductCostEntry(id, title, _numUnitCost.Value, _numShippingCost.Value, 0m, DateTimeOffset.UtcNow);
        await _repository.SaveAsync(entry);
        LoadData();
    }

    private async void OnDeleteClicked(object? sender, EventArgs e)
    {
        var id = _txtListingId.Text.Trim();
        if (string.IsNullOrWhiteSpace(id)) return;

        if (MessageBox.Show($"'{id}' kimlikli maliyet kaydını silmek istediğinize emin misiniz?", "Onay", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
        {
            await _repository.DeleteAsync(id);
            _txtListingId.Clear();
            _txtTitle.Clear();
            _numUnitCost.Value = 0;
            _numShippingCost.Value = 0;
            LoadData();
        }
    }
}
