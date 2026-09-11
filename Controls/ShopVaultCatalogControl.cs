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

    // Toolbar Controls
    private readonly ComboBox _cboSessions = new()
    {
        DropDownStyle = ComboBoxStyle.DropDownList,
        Width = 350,
        DropDownWidth = 520,
        Height = 32,
        Font = new Font("Segoe UI", 9.2F)
    };

    private readonly TextBox _txtSearch = new()
    {
        Width = 240,
        Height = 32,
        PlaceholderText = "🔍 Başlık, etiket veya ID ara...",
        Font = new Font("Segoe UI", 9.2F)
    };

    private readonly ModernButtonControl _btnSelectAll = new()
    {
        Text = "✅ Tümünü Seç",
        Width = 120,
        Height = 34,
        BackColor = UiStyle.CardBackground,
        ForeColor = UiStyle.TextDark,
        Font = new Font("Segoe UI Semibold", 9F, FontStyle.Bold),
        Cursor = Cursors.Hand
    };

    private readonly ModernButtonControl _btnClearSelection = new()
    {
        Text = "❌ Temizle",
        Width = 90,
        Height = 34,
        BackColor = UiStyle.CardBackground,
        ForeColor = UiStyle.TextDark,
        Font = new Font("Segoe UI Semibold", 9F, FontStyle.Bold),
        Cursor = Cursors.Hand
    };

    private readonly Label _lblSelectionCount = new()
    {
        AutoSize = true,
        Font = new Font("Segoe UI Semibold", 9.5F, FontStyle.Bold),
        ForeColor = Color.FromArgb(129, 140, 248),
        TextAlign = ContentAlignment.MiddleLeft,
        Margin = new Padding(8, 7, 8, 0)
    };

    private readonly ModernButtonControl _btnSendToTransfer = new()
    {
        Text = "🚀 Seçilenleri Transfer Et (0)",
        Width = 260,
        Height = 36,
        BackColor = UiStyle.PrimaryColor,
        ForeColor = Color.White,
        Font = new Font("Segoe UI Semibold", 9.5F, FontStyle.Bold),
        Cursor = Cursors.Hand
    };

    // Main Grid
    private readonly DataGridView _grid = new();

    // Right Preview Drawer Controls
    private readonly PictureBox _picPreview = new()
    {
        Size = new Size(310, 210),
        SizeMode = PictureBoxSizeMode.Zoom,
        BackColor = Color.FromArgb(15, 23, 42),
        BorderStyle = BorderStyle.None
    };

    private readonly Label _lblPillPrice = new()
    {
        AutoSize = true,
        Font = new Font("Segoe UI Semibold", 9.5F, FontStyle.Bold),
        ForeColor = Color.FromArgb(52, 211, 153),
        BackColor = Color.FromArgb(20, 35, 45),
        Padding = new Padding(8, 4, 8, 4)
    };

    private readonly Label _lblPillStock = new()
    {
        AutoSize = true,
        Font = new Font("Segoe UI Semibold", 9.5F, FontStyle.Bold),
        ForeColor = Color.FromArgb(56, 189, 248),
        BackColor = Color.FromArgb(20, 35, 55),
        Padding = new Padding(8, 4, 8, 4)
    };

    private readonly Label _lblDetailTitle = new()
    {
        AutoSize = true,
        Font = new Font("Segoe UI Semibold", 10.5F, FontStyle.Bold),
        ForeColor = Color.White,
        MaximumSize = new Size(310, 0),
        Margin = new Padding(0, 6, 0, 6)
    };

    private readonly TextBox _txtDetailDescription = new()
    {
        Multiline = true,
        ReadOnly = true,
        Height = 110,
        Width = 310,
        ScrollBars = ScrollBars.Vertical,
        Font = new Font("Segoe UI", 9F),
        BackColor = Color.FromArgb(15, 23, 42),
        ForeColor = Color.FromArgb(226, 232, 240),
        BorderStyle = BorderStyle.FixedSingle
    };

    private readonly FlowLayoutPanel _pnlVariations = new()
    {
        Width = 310,
        AutoSize = true,
        MaximumSize = new Size(310, 140),
        AutoScroll = true,
        FlowDirection = FlowDirection.TopDown,
        WrapContents = false
    };

    private readonly FlowLayoutPanel _galleryStrip = new()
    {
        Height = 84,
        Width = 310,
        AutoScroll = true,
        FlowDirection = FlowDirection.LeftToRight,
        WrapContents = false,
        BackColor = Color.FromArgb(15, 23, 42),
        Padding = new Padding(4)
    };

    public event Action<IReadOnlyList<VaultListing>>? TransferRequested;

    public ShopVaultCatalogControl(IShopVaultRepository repository)
    {
        _repository = repository;
        Dock = DockStyle.Fill;
        BuildLayout();
        HookEvents();
    }

    private void BuildLayout()
    {
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            RowCount = 2,
            Padding = new Padding(6),
            BackColor = UiStyle.BackgroundColor
        };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 50)); // Toolbar
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); // Content split (Grid + Drawer)

        // 1. TOOLBAR
        var toolbar = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            Padding = new Padding(0, 4, 0, 4)
        };

        toolbar.Controls.Add(new Label
        {
            Text = "Yedek Seç:",
            AutoSize = true,
            Font = new Font("Segoe UI Semibold", 9.2F),
            ForeColor = UiStyle.TextDark,
            Margin = new Padding(0, 8, 4, 0)
        });

        toolbar.Controls.Add(_cboSessions);

        _txtSearch.Margin = new Padding(10, 2, 8, 0);
        toolbar.Controls.Add(_txtSearch);

        toolbar.Controls.Add(_btnSelectAll);
        toolbar.Controls.Add(_btnClearSelection);
        toolbar.Controls.Add(_lblSelectionCount);
        toolbar.Controls.Add(_btnSendToTransfer);

        root.Controls.Add(toolbar, 0, 0);

        // 2. MAIN CONTENT SPLIT: Grid (Left) + Preview Drawer (Right)
        var split = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            Padding = new Padding(0)
        };
        split.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        split.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 340));

        ConfigureGrid();
        split.Controls.Add(_grid, 0, 0);

        // 3. PREVIEW DRAWER
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

        // Hero Image inside rounded container
        var heroBoxContainer = new Panel
        {
            Size = new Size(310, 210),
            BackColor = Color.FromArgb(15, 23, 42),
            Padding = new Padding(2)
        };
        heroBoxContainer.Controls.Add(_picPreview);
        drawerStack.Controls.Add(heroBoxContainer);

        // Pill Badges (Price & Stock)
        var pillStack = new FlowLayoutPanel
        {
            Width = 310,
            Height = 36,
            FlowDirection = FlowDirection.LeftToRight,
            Margin = new Padding(0, 6, 0, 4)
        };
        pillStack.Controls.Add(_lblPillPrice);
        pillStack.Controls.Add(_lblPillStock);
        drawerStack.Controls.Add(pillStack);

        // Title
        drawerStack.Controls.Add(_lblDetailTitle);

        // Description Section
        drawerStack.Controls.Add(new Label
        {
            Text = "📝 Ürün Açıklaması:",
            Font = new Font("Segoe UI Semibold", 8.8F, FontStyle.Bold),
            ForeColor = UiStyle.PrimaryColor,
            Margin = new Padding(0, 6, 0, 2)
        });
        drawerStack.Controls.Add(_txtDetailDescription);

        // Variations Section
        drawerStack.Controls.Add(new Label
        {
            Text = "🎨 Varyasyon Seçenekleri:",
            Font = new Font("Segoe UI Semibold", 8.8F, FontStyle.Bold),
            ForeColor = UiStyle.AccentColor,
            Margin = new Padding(0, 8, 0, 2)
        });
        drawerStack.Controls.Add(_pnlVariations);

        // Gallery Strip Section
        drawerStack.Controls.Add(new Label
        {
            Text = "🖼️ Arşivdeki Görseller (Tıkla & İncele):",
            Font = new Font("Segoe UI Semibold", 8.8F, FontStyle.Bold),
            ForeColor = UiStyle.SuccessColor,
            Margin = new Padding(0, 8, 0, 2)
        });
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
        _grid.DefaultCellStyle.Font = new Font("Segoe UI", 9.2F);
        _grid.DefaultCellStyle.SelectionBackColor = Color.FromArgb(79, 70, 229);
        _grid.DefaultCellStyle.SelectionForeColor = Color.White;
        _grid.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(30, 41, 59);
        _grid.ColumnHeadersDefaultCellStyle.ForeColor = Color.FromArgb(148, 163, 184);
        _grid.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI Semibold", 9.2F, FontStyle.Bold);
        _grid.ColumnHeadersHeight = 38;
        _grid.EnableHeadersVisualStyles = false;

        // Double buffer grid for silky smooth rendering
        typeof(DataGridView).InvokeMember(
            "DoubleBuffered",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.SetProperty,
            null,
            _grid,
            new object[] { true });

        var colSelect = new DataGridViewCheckBoxColumn { Name = "colSelect", HeaderText = "Seç", Width = 46 };
        var colId = new DataGridViewTextBoxColumn { Name = "colId", HeaderText = "Listing ID", Width = 115, ReadOnly = true };
        var colTitle = new DataGridViewTextBoxColumn { Name = "colTitle", HeaderText = "Ürün Başlığı", AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill, MinimumWidth = 240, ReadOnly = true };
        var colPrice = new DataGridViewTextBoxColumn { Name = "colPrice", HeaderText = "Fiyat", Width = 120, ReadOnly = true };
        var colQuantity = new DataGridViewTextBoxColumn { Name = "colQuantity", HeaderText = "Stok", Width = 75, ReadOnly = true };
        var colImages = new DataGridViewTextBoxColumn { Name = "colImages", HeaderText = "Görsel", Width = 105, ReadOnly = true };
        var colTags = new DataGridViewTextBoxColumn { Name = "colTags", HeaderText = "Etiket", Width = 100, ReadOnly = true };

        colPrice.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
        colQuantity.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
        colImages.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
        colTags.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;

        _grid.Columns.AddRange(colSelect, colId, colTitle, colPrice, colQuantity, colImages, colTags);

        // Highlight cells with colors
        _grid.CellFormatting += (_, e) =>
        {
            if (e.RowIndex < 0) return;
            if (e.ColumnIndex == 3) // Price
            {
                e.CellStyle.ForeColor = Color.FromArgb(52, 211, 153); // Emerald
                e.CellStyle.Font = new Font("Segoe UI Semibold", 9.2F, FontStyle.Bold);
            }
            else if (e.ColumnIndex == 5) // Images
            {
                e.CellStyle.ForeColor = Color.FromArgb(56, 189, 248); // Cyan
            }
            else if (e.ColumnIndex == 6) // Tags
            {
                e.CellStyle.ForeColor = Color.FromArgb(167, 139, 250); // Violet
            }
        };

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

    private void HookEvents()
    {
        _cboSessions.SelectedIndexChanged += async (_, _) => await OnSessionChangedAsync();
        _txtSearch.TextChanged += (_, _) => FilterListings();
        _btnSelectAll.Click += (_, _) => SelectAll(true);
        _btnClearSelection.Click += (_, _) => SelectAll(false);

        _btnSendToTransfer.Click += (_, _) =>
        {
            var selected = GetSelectedListings();
            if (selected.Count == 0)
            {
                MessageBox.Show(
                    this,
                    "Lütfen yeni mağazaya aktarmak için listeden en az bir ürün seçin veya 'Tümünü Seç' butonuna tıklayın.",
                    "Ürün Seçilmedi",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                return;
            }

            TransferRequested?.Invoke(selected);
        };
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
            _selectedListingIds.Add(l.ListingId); // Default select all for 1-click convenience
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
        var filtered = string.IsNullOrWhiteSpace(term) ? _allListings : _allListings.Where(l =>
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
                $"${l.Price:N2} {l.Currency}",
                l.Quantity,
                $"🖼️ {l.Images.Count} Görsel",
                $"🏷️ {l.Tags.Count} Tag"
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
        _btnSendToTransfer.Text = $"🚀 Seçilenleri Transfer Et ({_selectedListingIds.Count})";
        _btnSendToTransfer.Enabled = _selectedListingIds.Count > 0;
    }

    private void OnGridRowSelected()
    {
        if (_grid.CurrentRow?.Tag is VaultListing l)
        {
            _lblDetailTitle.Text = l.Title;
            _lblPillPrice.Text = $"💵 ${l.Price:N2} {l.Currency}";
            _lblPillStock.Text = $"📦 {l.Quantity} Adet Stok";
            _txtDetailDescription.Text = l.Description;

            // Render variation pills
            _pnlVariations.Controls.Clear();
            if (l.Variations.Count > 0)
            {
                foreach (var v in l.Variations)
                {
                    var pill = new Label
                    {
                        Text = $"• {v.PropertyName}: {v.ValueName}{(v.PriceDifference != 0 ? $" (+${v.PriceDifference:N2})" : "")}",
                        AutoSize = true,
                        Font = new Font("Segoe UI", 8.5F),
                        ForeColor = Color.FromArgb(226, 232, 240),
                        BackColor = Color.FromArgb(30, 41, 59),
                        Padding = new Padding(6, 3, 6, 3),
                        Margin = new Padding(0, 2, 0, 2)
                    };
                    _pnlVariations.Controls.Add(pill);
                }
            }
            else
            {
                var emptyPill = new Label
                {
                    Text = "Tekil ürün (Varyasyon bulunmuyor)",
                    AutoSize = true,
                    Font = new Font("Segoe UI", 8.5F),
                    ForeColor = UiStyle.TextMuted,
                    Margin = new Padding(0, 2, 0, 2)
                };
                _pnlVariations.Controls.Add(emptyPill);
            }

            // Load primary photo thumbnail (safely via stream to avoid Windows file locks)
            SafeLoadHeroImage(l);

            // Load gallery strip thumbnails
            _galleryStrip.Controls.Clear();
            foreach (var img in l.Images.OrderBy(i => i.Rank))
            {
                string fullPath = VaultPathHelper.ResolveFullPath(img.LocalRelativePath);
                if (File.Exists(fullPath))
                {
                    try
                    {
                        var thumbImg = LoadImageWithoutLock(fullPath);
                        if (thumbImg != null)
                        {
                            var thumbBox = new PictureBox
                            {
                                Size = new Size(60, 60),
                                SizeMode = PictureBoxSizeMode.Zoom,
                                Margin = new Padding(3),
                                BorderStyle = BorderStyle.FixedSingle,
                                Image = thumbImg,
                                Cursor = Cursors.Hand,
                                Tag = fullPath
                            };

                            thumbBox.Click += (s, _) =>
                            {
                                if (s is PictureBox clickedThumb && clickedThumb.Tag is string clickedPath)
                                {
                                    SafeSetHeroImageFromPath(clickedPath);
                                    HighlightThumbnail(clickedThumb);
                                }
                            };

                            _galleryStrip.Controls.Add(thumbBox);
                        }
                    }
                    catch { }
                }
            }

            if (_galleryStrip.Controls.Count > 0 && _galleryStrip.Controls[0] is PictureBox firstThumb)
            {
                HighlightThumbnail(firstThumb);
            }
        }
    }

    private void HighlightThumbnail(PictureBox selectedThumb)
    {
        foreach (Control c in _galleryStrip.Controls)
        {
            if (c is PictureBox pb)
            {
                pb.BackColor = pb == selectedThumb ? Color.FromArgb(99, 102, 241) : Color.Transparent;
                pb.Padding = pb == selectedThumb ? new Padding(2) : Padding.Empty;
            }
        }
    }

    private void SafeLoadHeroImage(VaultListing l)
    {
        if (l.Images.Count > 0)
        {
            var firstImg = l.Images.OrderBy(i => i.Rank).First();
            string fullPath = VaultPathHelper.ResolveFullPath(firstImg.LocalRelativePath);
            SafeSetHeroImageFromPath(fullPath);
        }
        else
        {
            _picPreview.Image?.Dispose();
            _picPreview.Image = null;
        }
    }

    private void SafeSetHeroImageFromPath(string fullPath)
    {
        if (File.Exists(fullPath))
        {
            try
            {
                var newImg = LoadImageWithoutLock(fullPath);
                var oldImg = _picPreview.Image;
                _picPreview.Image = newImg;
                oldImg?.Dispose();
            }
            catch { }
        }
    }

    private static Image? LoadImageWithoutLock(string path)
    {
        try
        {
            byte[] bytes = File.ReadAllBytes(path);
            using var ms = new MemoryStream(bytes);
            return Image.FromStream(ms);
        }
        catch
        {
            return null;
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
