namespace SimilarProductsWinForms.Controls;

using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using EtsyMarketPlace.Application.ShopVault.Services;
using EtsyMarketPlace.Domain.ShopVault.Entities;
using EtsyMarketPlace.Domain.ShopVault.Interfaces;

internal sealed class ShopVaultCatalogControl : UserControl
{
    private readonly IShopVaultRepository _repository;
    private List<VaultListing> _allListings = [];
    private readonly HashSet<long> _selectedListingIds = [];
    private string? _currentSessionId;

    private readonly ComboBox _cboSessions = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 280 };
    private readonly TextBox _txtSearch = new() { Width = 240, PlaceholderText = "🔍 Ürün başlığı veya etiket ara..." };
    private readonly Label _lblSelectionCount = new() { AutoSize = true, Font = new Font("Segoe UI Semibold", 9.2F, FontStyle.Bold), ForeColor = Color.FromArgb(129, 140, 248) };
    private readonly DataGridView _grid = new();

    // Right Preview Drawer
    private readonly PictureBox _picPreview = new() { Size = new Size(180, 180), SizeMode = PictureBoxSizeMode.Zoom, BorderStyle = BorderStyle.FixedSingle };
    private readonly Label _lblDetailTitle = new() { AutoSize = true, Font = new Font("Segoe UI Semibold", 10F, FontStyle.Bold), ForeColor = Color.White, MaximumSize = new Size(260, 0) };
    private readonly Label _lblDetailPrice = new() { AutoSize = true, Font = new Font("Segoe UI Semibold", 9.5F, FontStyle.Bold), ForeColor = Color.FromArgb(52, 211, 153) };
    private readonly TextBox _txtDetailDescription = new() { Multiline = true, ReadOnly = true, Height = 110, Width = 260, ScrollBars = ScrollBars.Vertical, Font = new Font("Segoe UI", 8.8F) };
    private readonly ListBox _lstDetailVariations = new() { Height = 90, Width = 260, Font = new Font("Segoe UI", 8.5F) };
    private readonly FlowLayoutPanel _galleryStrip = new() { Height = 90, Width = 260, AutoScroll = true, FlowDirection = FlowDirection.LeftToRight, WrapContents = false };

    public event Action<IReadOnlyList<VaultListing>>? TransferRequested;

    public ShopVaultCatalogControl(IShopVaultRepository repository)
    {
        _repository = repository;
        Dock = DockStyle.Fill;
        BuildLayout();
    }

    private void BuildLayout()
    {
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            RowCount = 2,
            Padding = new Padding(8)
        };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 46)); // Toolbar
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); // Content split (Grid + Drawer)

        // TOOLBAR
        var toolbar = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            Padding = new Padding(0, 4, 0, 4)
        };

        toolbar.Controls.Add(new Label { Text = "Yedek Seç:", AutoSize = true, Font = new Font("Segoe UI Semibold", 9F), ForeColor = UiStyle.TextDark, Margin = new Padding(0, 6, 4, 0) });
        _cboSessions.SelectedIndexChanged += async (_, _) => await OnSessionChangedAsync();
        toolbar.Controls.Add(_cboSessions);

        _txtSearch.Font = new Font("Segoe UI", 9F);
        _txtSearch.Margin = new Padding(12, 2, 8, 0);
        _txtSearch.TextChanged += (_, _) => FilterListings();
        toolbar.Controls.Add(_txtSearch);

        var btnSelectAll = UiStyle.CreateButton("☑️ Tümünü Seç", isSecondary: true);
        btnSelectAll.Height = 30;
        btnSelectAll.Click += (_, _) => SelectAll(true);
        toolbar.Controls.Add(btnSelectAll);

        var btnDeselectAll = UiStyle.CreateButton("⏹️ Temizle", isSecondary: true);
        btnDeselectAll.Height = 30;
        btnDeselectAll.Click += (_, _) => SelectAll(false);
        toolbar.Controls.Add(btnDeselectAll);

        _lblSelectionCount.Margin = new Padding(12, 6, 12, 0);
        toolbar.Controls.Add(_lblSelectionCount);

        var btnSendToTransfer = UiStyle.CreateButton("🚀 Seçilenleri Transfere Gönder");
        btnSendToTransfer.Height = 32;
        btnSendToTransfer.BackColor = Color.FromArgb(79, 70, 229);
        btnSendToTransfer.ForeColor = Color.White;
        btnSendToTransfer.Font = new Font("Segoe UI Semibold", 9F, FontStyle.Bold);
        btnSendToTransfer.Click += (_, _) =>
        {
            var selected = GetSelectedListings();
            if (selected.Count == 0)
            {
                MessageBox.Show(this, "Lütfen yeni mağazaya aktarmak için en az bir ürün seçin.", "Seçim Yapılmadı", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            TransferRequested?.Invoke(selected);
        };
        toolbar.Controls.Add(btnSendToTransfer);

        root.Controls.Add(toolbar, 0, 0);

        // MAIN CONTENT SPLIT: Grid (Left) + Preview Drawer (Right)
        var split = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            Padding = new Padding(0)
        };
        split.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        split.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 300));

        ConfigureGrid();
        split.Controls.Add(_grid, 0, 0);

        // PREVIEW DRAWER
        var drawer = new ModernCardPanel
        {
            Dock = DockStyle.Fill,
            CardColor = UiStyle.CardBackground,
            BorderColor = UiStyle.BorderColor,
            Padding = new Padding(12),
            Margin = new Padding(8, 0, 0, 0)
        };

        var drawerStack = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoScroll = true
        };

        _picPreview.BackColor = Color.FromArgb(20, 27, 45);
        drawerStack.Controls.Add(_picPreview);

        drawerStack.Controls.Add(_lblDetailTitle);
        drawerStack.Controls.Add(_lblDetailPrice);

        drawerStack.Controls.Add(new Label { Text = "Ürün Açıklaması:", Font = new Font("Segoe UI Semibold", 8.5F), ForeColor = UiStyle.TextMuted, Margin = new Padding(0, 6, 0, 2) });
        _txtDetailDescription.BackColor = Color.FromArgb(20, 27, 45);
        _txtDetailDescription.ForeColor = Color.FromArgb(226, 232, 240);
        drawerStack.Controls.Add(_txtDetailDescription);

        drawerStack.Controls.Add(new Label { Text = "Varyasyonlar & Fiyatlar:", Font = new Font("Segoe UI Semibold", 8.5F), ForeColor = UiStyle.TextMuted, Margin = new Padding(0, 6, 0, 2) });
        _lstDetailVariations.BackColor = Color.FromArgb(20, 27, 45);
        _lstDetailVariations.ForeColor = Color.FromArgb(226, 232, 240);
        drawerStack.Controls.Add(_lstDetailVariations);

        drawerStack.Controls.Add(new Label { Text = "Arşivdeki Fotoğraflar:", Font = new Font("Segoe UI Semibold", 8.5F), ForeColor = UiStyle.TextMuted, Margin = new Padding(0, 6, 0, 2) });
        drawerStack.Controls.Add(_galleryStrip);

        drawer.Controls.Add(drawerStack);
        split.Controls.Add(drawer, 1, 0);

        root.Controls.Add(split, 0, 1);
        Controls.Add(root);
    }

    private void ConfigureGrid()
    {
        _grid.Dock = DockStyle.Fill;
        _grid.BackgroundColor = Color.FromArgb(20, 27, 45);
        _grid.BorderStyle = BorderStyle.None;
        _grid.CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;
        _grid.GridColor = Color.FromArgb(45, 55, 75);
        _grid.RowHeadersVisible = false;
        _grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        _grid.MultiSelect = false;
        _grid.AllowUserToAddRows = false;
        _grid.AllowUserToDeleteRows = false;
        _grid.RowTemplate.Height = 44;
        _grid.DefaultCellStyle.BackColor = Color.FromArgb(20, 27, 45);
        _grid.DefaultCellStyle.ForeColor = Color.White;
        _grid.DefaultCellStyle.Font = new Font("Segoe UI", 9F);
        _grid.DefaultCellStyle.SelectionBackColor = Color.FromArgb(79, 70, 229);
        _grid.DefaultCellStyle.SelectionForeColor = Color.White;
        _grid.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(30, 41, 59);
        _grid.ColumnHeadersDefaultCellStyle.ForeColor = Color.FromArgb(148, 163, 184);
        _grid.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI Semibold", 9F, FontStyle.Bold);
        _grid.ColumnHeadersHeight = 36;
        _grid.EnableHeadersVisualStyles = false;

        var colSelect = new DataGridViewCheckBoxColumn { Name = "colSelect", HeaderText = "Seç", Width = 46 };
        var colId = new DataGridViewTextBoxColumn { Name = "colId", HeaderText = "Listing ID", Width = 110, ReadOnly = true };
        var colTitle = new DataGridViewTextBoxColumn { Name = "colTitle", HeaderText = "Ürün Başlığı", AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill, ReadOnly = true };
        var colPrice = new DataGridViewTextBoxColumn { Name = "colPrice", HeaderText = "Fiyat", Width = 85, ReadOnly = true };
        var colQuantity = new DataGridViewTextBoxColumn { Name = "colQuantity", HeaderText = "Stok", Width = 65, ReadOnly = true };
        var colImages = new DataGridViewTextBoxColumn { Name = "colImages", HeaderText = "Görsel", Width = 65, ReadOnly = true };
        var colTags = new DataGridViewTextBoxColumn { Name = "colTags", HeaderText = "Etiket Sayısı", Width = 85, ReadOnly = true };

        _grid.Columns.AddRange(colSelect, colId, colTitle, colPrice, colQuantity, colImages, colTags);

        _grid.CellValueChanged += (_, e) =>
        {
            if (e.RowIndex >= 0 && e.ColumnIndex == 0)
            {
                if (_grid.Rows[e.RowIndex].Tag is VaultListing l)
                {
                    bool isChecked = Convert.ToBoolean(_grid.Rows[e.RowIndex].Cells[0].Value);
                    if (isChecked) _selectedListingIds.Add(l.ListingId);
                    else _selectedListingIds.Remove(l.ListingId);
                    UpdateSelectionSummary();
                }
            }
        };

        _grid.CurrentCellDirtyStateChanged += (_, _) =>
        {
            if (_grid.IsCurrentCellDirty) _grid.CommitEdit(DataGridViewDataErrorContexts.Commit);
        };

        _grid.SelectionChanged += (_, _) => OnGridRowSelected();
    }

    public async Task RefreshSessionsAsync(string? selectSessionId = null)
    {
        _cboSessions.Items.Clear();
        var sessions = await _repository.GetAllSessionsAsync();
        foreach (var s in sessions)
        {
            _cboSessions.Items.Add(new SessionComboItem(s));
        }

        if (_cboSessions.Items.Count > 0)
        {
            if (!string.IsNullOrWhiteSpace(selectSessionId))
            {
                for (int i = 0; i < _cboSessions.Items.Count; i++)
                {
                    if (((SessionComboItem)_cboSessions.Items[i]).Session.SessionId == selectSessionId)
                    {
                        _cboSessions.SelectedIndex = i;
                        return;
                    }
                }
            }

            _cboSessions.SelectedIndex = 0;
        }
    }

    public async Task LoadSessionListingsAsync(string sessionId)
    {
        _currentSessionId = sessionId;
        _allListings = await _repository.GetListingsBySessionIdAsync(sessionId);
        _selectedListingIds.Clear();
        foreach (var l in _allListings)
        {
            _selectedListingIds.Add(l.ListingId); // Default to all selected
        }
        FilterListings();
    }

    private async Task OnSessionChangedAsync()
    {
        if (_cboSessions.SelectedItem is SessionComboItem item)
        {
            await LoadSessionListingsAsync(item.Session.SessionId);
        }
    }

    private void FilterListings()
    {
        string term = _txtSearch.Text.Trim().ToLowerInvariant();
        var filtered = _allListings.Where(l =>
            string.IsNullOrWhiteSpace(term) ||
            l.Title.ToLowerInvariant().Contains(term) ||
            l.Tags.Any(t => t.ToLowerInvariant().Contains(term)) ||
            l.ListingId.ToString().Contains(term)
        ).ToList();

        _grid.Rows.Clear();
        foreach (var l in filtered)
        {
            int rowIdx = _grid.Rows.Add(
                _selectedListingIds.Contains(l.ListingId),
                l.ListingId,
                l.Title,
                $"{l.Price:N2} {l.Currency}",
                l.Quantity,
                $"{l.Images.Count} Adet",
                $"{l.Tags.Count} Tag"
            );
            _grid.Rows[rowIdx].Tag = l;
        }

        UpdateSelectionSummary();
        if (_grid.Rows.Count > 0)
        {
            _grid.Rows[0].Selected = true;
            OnGridRowSelected();
        }
    }

    private void SelectAll(bool select)
    {
        foreach (DataGridViewRow row in _grid.Rows)
        {
            row.Cells[0].Value = select;
            if (row.Tag is VaultListing l)
            {
                if (select) _selectedListingIds.Add(l.ListingId);
                else _selectedListingIds.Remove(l.ListingId);
            }
        }
        UpdateSelectionSummary();
    }

    private void UpdateSelectionSummary()
    {
        _lblSelectionCount.Text = $"Seçili: {_selectedListingIds.Count} / {_allListings.Count} Ürün";
    }

    private void OnGridRowSelected()
    {
        if (_grid.CurrentRow?.Tag is VaultListing l)
        {
            _lblDetailTitle.Text = l.Title;
            _lblDetailPrice.Text = $"Fiyat: {l.Price:N2} {l.Currency} • Stok: {l.Quantity}";
            _txtDetailDescription.Text = l.Description;

            _lstDetailVariations.Items.Clear();
            if (l.Variations.Count > 0)
            {
                foreach (var v in l.Variations)
                {
                    _lstDetailVariations.Items.Add($"• {v.PropertyName}: {v.ValueName} (+{v.PriceDifference:N2})");
                }
            }
            else
            {
                _lstDetailVariations.Items.Add("(Varyasyon yok - tekil ürün)");
            }

            // Load primary photo thumbnail
            _picPreview.Image?.Dispose();
            _picPreview.Image = null;

            if (l.Images.Count > 0)
            {
                var firstImg = l.Images.OrderBy(i => i.Rank).First();
                string fullPath = VaultPathHelper.ResolveFullPath(firstImg.LocalRelativePath);
                if (File.Exists(fullPath))
                {
                    try { _picPreview.Image = Image.FromFile(fullPath); } catch { }
                }
            }

            // Load gallery strip
            _galleryStrip.Controls.Clear();
            foreach (var img in l.Images.OrderBy(i => i.Rank))
            {
                string fullPath = VaultPathHelper.ResolveFullPath(img.LocalRelativePath);
                if (File.Exists(fullPath))
                {
                    try
                    {
                        var thumb = new PictureBox
                        {
                            Size = new Size(50, 50),
                            SizeMode = PictureBoxSizeMode.Zoom,
                            Margin = new Padding(2),
                            BorderStyle = BorderStyle.FixedSingle,
                            Image = Image.FromFile(fullPath)
                        };
                        _galleryStrip.Controls.Add(thumb);
                    }
                    catch { }
                }
            }
        }
    }

    public IReadOnlyList<VaultListing> GetSelectedListings()
    {
        return _allListings.Where(l => _selectedListingIds.Contains(l.ListingId)).ToList();
    }

    private sealed class SessionComboItem
    {
        public VaultBackupSession Session { get; }
        public SessionComboItem(VaultBackupSession session) => Session = session;
        public override string ToString() => $"📦 {Session.ShopName} ({Session.CreatedAtUtc.ToLocalTime():g}) - {Session.TotalListingsCount} Ürün";
    }
}
