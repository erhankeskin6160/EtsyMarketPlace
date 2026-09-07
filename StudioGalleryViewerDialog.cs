namespace SimilarProductsWinForms;

using System;
using System.Drawing;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Forms;
using SimilarProductsWinForms.Controls;
using SimilarProductsWinForms.Services;

/// <summary>
/// Gallery browser form displaying mockups persisted in SQLite.
/// </summary>
internal sealed class StudioGalleryViewerDialog : Form
{
    public Bitmap? SelectedImage { get; private set; }
    public string? SelectedPrompt { get; private set; }

    private readonly FlowLayoutPanel _flowPanel = new() { Dock = DockStyle.Fill, AutoScroll = true, WrapContents = true };
    private readonly TextBox _searchBox = new();

    public StudioGalleryViewerDialog()
    {
        BuildUi();
        Shown += async (_, _) => await LoadGalleryAsync();
    }

    private void BuildUi()
    {
        Text = "📚 Kalıcı Stüdyo Galerisi & Önceki AI Üretimleri";
        Size = new Size(1000, 680);
        StartPosition = FormStartPosition.CenterParent;
        BackColor = Color.FromArgb(15, 23, 42);
        ForeColor = Color.White;
        Font = new Font("Segoe UI", 9F);

        var root = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 3, Padding = new Padding(14) };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 50));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 45));
        Controls.Add(root);

        // 1. Header with Search Box
        var header = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2 };
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 60));
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 40));

        var titleLabel = new Label
        {
            Text = "📚 Kaydedilen AI Mockup Geçmişi (SQLite)",
            Font = new Font("Segoe UI Semibold", 12F, FontStyle.Bold),
            ForeColor = Color.White,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft
        };
        header.Controls.Add(titleLabel, 0, 0);

        _searchBox.Dock = DockStyle.Fill;
        _searchBox.PlaceholderText = "🔍 Ürün adına veya motora göre filtrele...";
        _searchBox.TextChanged += async (_, _) => await LoadGalleryAsync(_searchBox.Text.Trim());
        header.Controls.Add(_searchBox, 1, 0);
        root.Controls.Add(header, 0, 0);

        // 2. Flow Panel
        root.Controls.Add(_flowPanel, 0, 1);

        // 3. Bottom Bar
        var bottom = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft };
        var btnClose = UiStyle.CreateButton("Kapat", isSecondary: true);
        btnClose.Click += (_, _) => Close();
        bottom.Controls.Add(btnClose);
        root.Controls.Add(bottom, 0, 2);
    }

    private async Task LoadGalleryAsync(string filter = "")
    {
        _flowPanel.Controls.Clear();
        var items = await PersistentStudioGalleryService.GetRecentItemsAsync(60);

        if (!string.IsNullOrWhiteSpace(filter))
        {
            items = items.FindAll(i =>
                i.ProductTitle.Contains(filter, StringComparison.OrdinalIgnoreCase) ||
                i.Engine.Contains(filter, StringComparison.OrdinalIgnoreCase) ||
                i.Prompt.Contains(filter, StringComparison.OrdinalIgnoreCase));
        }

        if (items.Count == 0)
        {
            _flowPanel.Controls.Add(new Label
            {
                Text = "Kayıtlı görsel bulunamadı.",
                AutoSize = true,
                ForeColor = UiStyle.TextMuted,
                Padding = new Padding(20)
            });
            return;
        }

        foreach (var item in items)
        {
            if (!File.Exists(item.ImagePath)) continue;

            var card = new ModernCardPanel
            {
                Width = 190,
                Height = 240,
                CornerRadius = 8,
                Padding = new Padding(6),
                Margin = new Padding(8),
                CardColor = UiStyle.CardBackground,
                BorderColor = UiStyle.BorderColor
            };

            var stack = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 3 };
            stack.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            stack.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
            stack.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));

            var pb = new PictureBox
            {
                Dock = DockStyle.Fill,
                SizeMode = PictureBoxSizeMode.Zoom,
                BackColor = Color.FromArgb(15, 23, 42),
                Cursor = Cursors.Hand
            };

            try
            {
                using var stream = File.OpenRead(item.ImagePath);
                using var img = Image.FromStream(stream);
                pb.Image = new Bitmap(img);
            }
            catch { }

            stack.Controls.Add(pb, 0, 0);

            var lblTitle = new Label
            {
                Text = $"{item.ProductTitle} ({item.Engine})",
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 7.8F),
                AutoEllipsis = true,
                Dock = DockStyle.Fill
            };
            stack.Controls.Add(lblTitle, 0, 1);

            var btnLoad = UiStyle.CreateButton("🎨 Tuvale Yükle");
            btnLoad.Height = 26;
            btnLoad.Font = new Font("Segoe UI Semibold", 8F);
            btnLoad.Click += (_, _) =>
            {
                if (pb.Image is Bitmap bmp)
                {
                    SelectedImage = new Bitmap(bmp);
                    SelectedPrompt = item.Prompt;
                    DialogResult = DialogResult.OK;
                    Close();
                }
            };
            stack.Controls.Add(btnLoad, 0, 2);

            card.Controls.Add(stack);
            _flowPanel.Controls.Add(card);
        }
    }
}
