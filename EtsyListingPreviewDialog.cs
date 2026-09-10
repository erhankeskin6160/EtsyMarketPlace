namespace SimilarProductsWinForms;

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Windows.Forms;

internal sealed class EtsyListingPreviewDialog : Form
{
    private readonly string _title;
    private readonly decimal _price;
    private readonly string _description;
    private readonly IReadOnlyList<string> _tags;
    private readonly IReadOnlyList<string> _materials;
    private readonly IReadOnlyList<string> _imagePaths;
    private readonly IReadOnlyList<(string Name, IReadOnlyList<string> Values)> _variations;
    private readonly IReadOnlyDictionary<string, decimal>? _variationPrices;

    private PictureBox _picMain = null!;
    private FlowLayoutPanel _pnlThumbnails = null!;
    private Image? _currentMainImage;

    public EtsyListingPreviewDialog(
        string title,
        decimal price,
        string description,
        IReadOnlyList<string> tags,
        IReadOnlyList<string> materials,
        IReadOnlyList<string> imagePaths,
        IReadOnlyList<(string Name, IReadOnlyList<string> Values)> variations,
        IReadOnlyDictionary<string, decimal>? variationPrices = null)
    {
        _title = string.IsNullOrWhiteSpace(title) ? "Örnek Ürün Başlığı (Etsy Listing Title)" : title;
        _price = price > 0 ? price : 24.99m;
        _description = description;
        _tags = tags;
        _materials = materials;
        _imagePaths = imagePaths;
        _variations = variations;
        _variationPrices = variationPrices;

        InitializeComponent();
    }

    private void InitializeComponent()
    {
        Text = "👁️ Etsy Canlı İlan & Arama Kartı Önizleme";
        Size = new Size(1100, 750);
        MinimumSize = new Size(950, 650);
        StartPosition = FormStartPosition.CenterParent;
        BackColor = Color.FromArgb(248, 249, 251);
        Font = new Font("Segoe UI", 9.5f, FontStyle.Regular);

        // Header Panel
        var pnlHeader = new Panel
        {
            Dock = DockStyle.Top,
            Height = 55,
            BackColor = Color.White,
            Padding = new Padding(20, 10, 20, 10)
        };
        pnlHeader.Paint += (s, e) =>
        {
            using var pen = new Pen(Color.FromArgb(230, 233, 238), 1);
            e.Graphics.DrawLine(pen, 0, pnlHeader.Height - 1, pnlHeader.Width, pnlHeader.Height - 1);
        };

        var lblHeaderTitle = new Label
        {
            Text = "🛍️ Etsy Alıcı Gözünden Önizleme",
            Font = new Font("Segoe UI", 12.5f, FontStyle.Bold),
            ForeColor = Color.FromArgb(34, 34, 34),
            AutoSize = true,
            Location = new Point(20, 15)
        };

        var lblHeaderSubtitle = new Label
        {
            Text = "Bu önizleme, ilanınızın Etsy arama sonuçlarında ve ürün detay sayfasında alıcılara nasıl görüneceğini simüle eder.",
            Font = new Font("Segoe UI", 9f),
            ForeColor = Color.FromArgb(100, 110, 120),
            AutoSize = true,
            Location = new Point(295, 18)
        };

        pnlHeader.Controls.AddRange([lblHeaderTitle, lblHeaderSubtitle]);

        // Main Container Split
        var split = new SplitContainer
        {
            Dock = DockStyle.Fill,
            Orientation = Orientation.Vertical,
            SplitterDistance = 380,
            SplitterWidth = 6,
            BackColor = Color.FromArgb(235, 238, 243)
        };

        // LEFT: Etsy Search Result Card Mockup
        var pnlSearchCardContainer = CreateSearchCardPreview();
        split.Panel1.Controls.Add(pnlSearchCardContainer);
        split.Panel1.BackColor = Color.FromArgb(248, 249, 251);

        // RIGHT: Etsy Listing Detail Page Mockup
        var pnlDetailPageContainer = CreateDetailPagePreview();
        split.Panel2.Controls.Add(pnlDetailPageContainer);
        split.Panel2.BackColor = Color.White;

        // Bottom Bar
        var pnlBottom = new Panel
        {
            Dock = DockStyle.Bottom,
            Height = 50,
            BackColor = Color.White,
            Padding = new Padding(15, 8, 20, 8)
        };
        pnlBottom.Paint += (s, e) =>
        {
            using var pen = new Pen(Color.FromArgb(230, 233, 238), 1);
            e.Graphics.DrawLine(pen, 0, 0, pnlBottom.Width, 0);
        };

        var btnClose = new Button
        {
            Text = "Kapat",
            Size = new Size(110, 34),
            Location = new Point(pnlBottom.Width - 130, 8),
            Anchor = AnchorStyles.Right | AnchorStyles.Top,
            BackColor = Color.FromArgb(240, 242, 245),
            FlatStyle = FlatStyle.Flat,
            Cursor = Cursors.Hand,
            ForeColor = Color.FromArgb(40, 40, 40)
        };
        btnClose.FlatAppearance.BorderColor = Color.FromArgb(210, 215, 222);
        btnClose.Click += (s, e) => Close();
        pnlBottom.Controls.Add(btnClose);

        Controls.Add(split);
        Controls.Add(pnlBottom);
        Controls.Add(pnlHeader);
    }

    private Panel CreateSearchCardPreview()
    {
        var root = new Panel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(20),
            AutoScroll = true
        };

        var lblCardSection = new Label
        {
            Text = "🔍 Arama Sonucu Kartı (Etsy Grid View)",
            Font = new Font("Segoe UI", 10.5f, FontStyle.Bold),
            ForeColor = Color.FromArgb(50, 50, 60),
            AutoSize = true,
            Location = new Point(20, 15)
        };
        root.Controls.Add(lblCardSection);

        var card = new Panel
        {
            Size = new Size(320, 440),
            Location = new Point(20, 50),
            BackColor = Color.White
        };
        card.Paint += (s, e) =>
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using var pen = new Pen(Color.FromArgb(220, 224, 230), 1);
            e.Graphics.DrawRectangle(pen, 0, 0, card.Width - 1, card.Height - 1);
        };

        // Thumbnail
        var picThumb = new PictureBox
        {
            Size = new Size(320, 240),
            Location = new Point(0, 0),
            SizeMode = PictureBoxSizeMode.Zoom,
            BackColor = Color.FromArgb(242, 244, 247)
        };
        if (_imagePaths.Count > 0 && File.Exists(_imagePaths[0]))
        {
            try { picThumb.Image = Image.FromFile(_imagePaths[0]); } catch { }
        }

        // Heart favorite button badge
        var lblHeart = new Label
        {
            Text = "🤍",
            Font = new Font("Segoe UI", 12f),
            Size = new Size(32, 32),
            Location = new Point(280, 8),
            BackColor = Color.White,
            TextAlign = ContentAlignment.MiddleCenter,
            Cursor = Cursors.Hand
        };
        picThumb.Controls.Add(lblHeart);

        // Shop Name
        var lblShop = new Label
        {
            Text = "Mağazanız (Etsy Star Seller)",
            Font = new Font("Segoe UI", 8.5f),
            ForeColor = Color.FromArgb(120, 120, 130),
            Location = new Point(12, 248),
            AutoSize = true
        };

        // Title (truncated)
        var lblTitle = new Label
        {
            Text = _title,
            Font = new Font("Segoe UI", 9.5f, FontStyle.Regular),
            ForeColor = Color.FromArgb(34, 34, 34),
            Location = new Point(12, 270),
            Size = new Size(296, 42)
        };

        // Rating
        var lblRating = new Label
        {
            Text = "★★★★★ (4.9)  |  128 satış",
            Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
            ForeColor = Color.FromArgb(235, 120, 20),
            Location = new Point(12, 318),
            AutoSize = true
        };

        // Price
        string priceText;
        if (_variationPrices != null && _variationPrices.Count > 0)
        {
            var validPrices = _variationPrices.Values.Where(p => p > 0).ToList();
            if (validPrices.Count > 0)
            {
                var min = validPrices.Min();
                var max = validPrices.Max();
                priceText = min != max ? $"USD {min:0.00} - {max:0.00}" : $"USD {min:0.00}";
            }
            else
            {
                priceText = $"USD {_price:0.00}";
            }
        }
        else
        {
            priceText = $"USD {_price:0.00}";
        }

        var lblPrice = new Label
        {
            Text = priceText,
            Font = new Font("Segoe UI", 12f, FontStyle.Bold),
            ForeColor = Color.FromArgb(34, 34, 34),
            Location = new Point(12, 342),
            AutoSize = true
        };

        // Free Shipping badge
        var lblFreeShip = new Label
        {
            Text = "ÜCRETSİZ Kargo",
            Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
            ForeColor = Color.FromArgb(16, 124, 65),
            BackColor = Color.FromArgb(235, 248, 240),
            Location = new Point(12, 372),
            Padding = new Padding(4, 2, 4, 2),
            AutoSize = true
        };

        // Badges
        var lblBadge = new Label
        {
            Text = "Bestseller ✨",
            Font = new Font("Segoe UI", 8f, FontStyle.Bold),
            ForeColor = Color.FromArgb(180, 80, 0),
            BackColor = Color.FromArgb(255, 243, 230),
            Location = new Point(120, 372),
            Padding = new Padding(4, 2, 4, 2),
            AutoSize = true
        };

        card.Controls.AddRange([picThumb, lblShop, lblTitle, lblRating, lblPrice, lblFreeShip, lblBadge]);
        root.Controls.Add(card);

        return root;
    }

    private Panel CreateDetailPagePreview()
    {
        var root = new Panel
        {
            Dock = DockStyle.Fill,
            AutoScroll = true,
            Padding = new Padding(25)
        };

        var lblDetailSection = new Label
        {
            Text = "📑 Ürün Detay Sayfası Önizlemesi (Etsy Product Page)",
            Font = new Font("Segoe UI Semibold", 11.5f, FontStyle.Bold),
            ForeColor = Color.FromArgb(34, 34, 34),
            Dock = DockStyle.Top,
            Height = 35
        };
        root.Controls.Add(lblDetailSection);

        // Content panel
        var content = new Panel
        {
            Location = new Point(25, 50),
            Size = new Size(650, 600),
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
        };

        // Thumbnails Strip
        _pnlThumbnails = new FlowLayoutPanel
        {
            Location = new Point(0, 0),
            Size = new Size(65, 380),
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoScroll = true
        };

        // Large Main PictureBox
        _picMain = new PictureBox
        {
            Location = new Point(75, 0),
            Size = new Size(340, 380),
            SizeMode = PictureBoxSizeMode.Zoom,
            BackColor = Color.FromArgb(245, 247, 250),
            BorderStyle = BorderStyle.FixedSingle
        };

        PopulateDetailImages();

        // Right details pane inside detail page
        var pnlInfo = new Panel
        {
            Location = new Point(430, 0),
            Size = new Size(220, 500),
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
        };

        var lblStock = new Label
        {
            Text = "🟢 Stokta Var (Hazır)",
            Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
            ForeColor = Color.FromArgb(16, 124, 65),
            Location = new Point(0, 0),
            AutoSize = true
        };

        var lblPriceLarge = new Label
        {
            Text = $"${_price:0.00} USD",
            Font = new Font("Segoe UI", 16f, FontStyle.Bold),
            ForeColor = Color.FromArgb(34, 34, 34),
            Location = new Point(0, 22),
            AutoSize = true
        };

        var lblTitleFull = new Label
        {
            Text = _title,
            Font = new Font("Segoe UI", 10.5f, FontStyle.Regular),
            ForeColor = Color.FromArgb(50, 50, 50),
            Location = new Point(0, 60),
            Size = new Size(220, 65)
        };

        int curY = 135;
        var varCombos = new List<ComboBox>();

        void UpdateDetailPrice()
        {
            if (_variationPrices == null || _variationPrices.Count == 0 || varCombos.Count == 0)
            {
                lblPriceLarge.Text = $"${_price:0.00} USD";
                return;
            }

            var parts = varCombos.Select(c => c.SelectedItem?.ToString()?.Trim() ?? "").ToList();
            var comboKey = string.Join(" / ", parts);

            if (_variationPrices.TryGetValue(comboKey, out var p) ||
                (parts.Count > 0 && _variationPrices.TryGetValue(parts[0], out p)))
            {
                lblPriceLarge.Text = $"${p:0.00} USD";
            }
            else
            {
                lblPriceLarge.Text = $"${_price:0.00} USD";
            }
        }

        // Variations dropdowns preview
        if (_variations.Count > 0)
        {
            foreach (var (vName, vVals) in _variations)
            {
                var lblVar = new Label
                {
                    Text = $"{vName} Seçiniz:",
                    Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
                    ForeColor = Color.FromArgb(80, 80, 90),
                    Location = new Point(0, curY),
                    AutoSize = true
                };
                curY += 20;

                var cbo = new ComboBox
                {
                    DropDownStyle = ComboBoxStyle.DropDownList,
                    Location = new Point(0, curY),
                    Width = 210
                };
                foreach (var val in vVals) cbo.Items.Add(val);
                if (cbo.Items.Count > 0) cbo.SelectedIndex = 0;

                varCombos.Add(cbo);
                cbo.SelectedIndexChanged += (_, _) => UpdateDetailPrice();

                pnlInfo.Controls.AddRange([lblVar, cbo]);
                curY += 34;
            }

            UpdateDetailPrice();
        }

        // Etsy Buttons
        var btnAddToCart = new Button
        {
            Text = "Sepete Ekle",
            Size = new Size(210, 38),
            Location = new Point(0, curY),
            BackColor = Color.FromArgb(34, 34, 34),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 10f, FontStyle.Bold),
            Cursor = Cursors.Hand
        };
        curY += 44;

        var btnBuyNow = new Button
        {
            Text = "Hemen Satın Al",
            Size = new Size(210, 36),
            Location = new Point(0, curY),
            BackColor = Color.FromArgb(245, 100, 20),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
            Cursor = Cursors.Hand
        };
        curY += 46;

        pnlInfo.Controls.AddRange([lblStock, lblPriceLarge, lblTitleFull, btnAddToCart, btnBuyNow]);

        content.Controls.AddRange([_pnlThumbnails, _picMain, pnlInfo]);
        root.Controls.Add(content);

        return root;
    }

    private void PopulateDetailImages()
    {
        _pnlThumbnails.Controls.Clear();

        if (_imagePaths.Count == 0)
        {
            return;
        }

        for (int i = 0; i < _imagePaths.Count; i++)
        {
            var path = _imagePaths[i];
            if (!File.Exists(path)) continue;

            var thumb = new PictureBox
            {
                Size = new Size(55, 55),
                SizeMode = PictureBoxSizeMode.Zoom,
                BackColor = Color.FromArgb(240, 242, 245),
                BorderStyle = BorderStyle.FixedSingle,
                Cursor = Cursors.Hand,
                Margin = new Padding(0, 0, 0, 6)
            };

            try
            {
                var img = Image.FromFile(path);
                thumb.Image = img;
                if (i == 0)
                {
                    _picMain.Image = img;
                    _currentMainImage = img;
                }

                thumb.Click += (s, e) =>
                {
                    _picMain.Image = img;
                    _currentMainImage = img;
                };
            }
            catch { }

            _pnlThumbnails.Controls.Add(thumb);
        }
    }
}
