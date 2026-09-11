namespace SimilarProductsWinForms;

using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Forms;
using SimilarProductsWinForms.Controls;
using SimilarProductsWinForms.Services;

internal sealed class AiStudioImagePickerDialog : Form
{
    private readonly FlowLayoutPanel _flowPanel = new() { Dock = DockStyle.Top, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, AutoScroll = false, WrapContents = true };
    private ModernScrollPanel? _pickerScroll;
    private readonly List<ModernCheckBox> _checkBoxes = [];
    private readonly List<string> _selectedPaths = [];
    private readonly Label _statusLabel = new();

    public IReadOnlyList<string> SelectedImagePaths => _selectedPaths;

    public AiStudioImagePickerDialog()
    {
        Text = "🖼️ AI Görsel Stüdyo Galerisinden Aktar";
        Size = new Size(720, 560);
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        UiStyle.ApplyTheme(this);

        BuildLayout();
        Shown += async (_, _) => await LoadGalleryItemsAsync();
    }

    private void BuildLayout()
    {
        var root = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 3, Padding = new Padding(12) };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));  // Header
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));  // Gallery flow
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 50));  // Action buttons

        // Header
        var headerPanel = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2 };
        headerPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 70));
        headerPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 30));

        var lblInfo = new Label
        {
            Dock = DockStyle.Fill,
            Text = "Daha önce AI Stüdyosu'nda üretilen görsellerden ürününüze eklemek istediklerinizi seçin:",
            Font = new Font("Segoe UI", 9F),
            ForeColor = UiStyle.TextDark,
            TextAlign = ContentAlignment.MiddleLeft,
        };
        headerPanel.Controls.Add(lblInfo, 0, 0);

        var btnSelectAll = UiStyle.CreateButton("Tümünü Seç", isSecondary: true);
        btnSelectAll.Height = 28;
        btnSelectAll.Click += (_, _) =>
        {
            bool anyUnchecked = _checkBoxes.Exists(c => !c.Checked);
            foreach (var cb in _checkBoxes) cb.Checked = anyUnchecked;
        };
        headerPanel.Controls.Add(btnSelectAll, 1, 0);
        root.Controls.Add(headerPanel, 0, 0);

        // Flow Panel
        _pickerScroll = new ModernScrollPanel { Dock = DockStyle.Fill, Margin = Padding.Empty, Padding = new Padding(0, 0, 2, 0) };
        _flowPanel.BackColor = UiStyle.CardBackground;
        _flowPanel.Padding = new Padding(8);
        _pickerScroll.SetContent(_flowPanel);
        root.Controls.Add(_pickerScroll, 0, 1);

        // Bottom Bar
        var bottomPanel = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3 };
        bottomPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        bottomPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 160));
        bottomPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 100));

        _statusLabel.Dock = DockStyle.Fill;
        _statusLabel.TextAlign = ContentAlignment.MiddleLeft;
        _statusLabel.ForeColor = UiStyle.TextMuted;
        _statusLabel.Font = new Font("Segoe UI", 8.5F);
        bottomPanel.Controls.Add(_statusLabel, 0, 0);

        var confirmBtn = new ModernButtonControl
        {
            Dock = DockStyle.Fill,
            Text = "✅ Seçilenleri Aktar",
            NormalColor = UiStyle.SuccessColor,
            HoverColor = Color.FromArgb(5, 150, 105),
            ForeColor = Color.White,
            Margin = new Padding(4),
        };
        confirmBtn.Click += (_, _) =>
        {
            _selectedPaths.Clear();
            foreach (var cb in _checkBoxes)
            {
                if (cb.Checked && cb.Tag is string path && File.Exists(path))
                {
                    _selectedPaths.Add(path);
                }
            }

            if (_selectedPaths.Count == 0)
            {
                MessageBox.Show(this, "Lütfen listeye aktarmak için en az bir görsel işaretleyin.", "Görsel Seçimi", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            DialogResult = DialogResult.OK;
        };
        bottomPanel.Controls.Add(confirmBtn, 1, 0);

        var cancelBtn = UiStyle.CreateButton("İptal", isSecondary: true);
        cancelBtn.Margin = new Padding(4);
        cancelBtn.Click += (_, _) => DialogResult = DialogResult.Cancel;
        bottomPanel.Controls.Add(cancelBtn, 2, 0);

        root.Controls.Add(bottomPanel, 0, 2);
        Controls.Add(root);
    }

    private async Task LoadGalleryItemsAsync()
    {
        try
        {
            _statusLabel.Text = "Stüdyo görselleri yükleniyor...";
            var items = await PersistentStudioGalleryService.GetRecentItemsAsync(60);

            _flowPanel.Controls.Clear();
            _checkBoxes.Clear();

            if (items.Count == 0)
            {
                _flowPanel.Controls.Add(new Label
                {
                    Text = "Kayıtlı stüdyo görseli bulunamadı.\n'AI Görsel Studio' modülünden yeni görseller üretebilir veya doğrudan bilgisayarınızdan resim yükleyebilirsiniz.",
                    AutoSize = true,
                    ForeColor = UiStyle.TextMuted,
                    Font = new Font("Segoe UI", 10F),
                    Padding = new Padding(20),
                });
                _statusLabel.Text = "0 görsel bulundu.";
                return;
            }

            foreach (var item in items)
            {
                if (!File.Exists(item.ImagePath)) continue;

                var card = new ModernCardPanel
                {
                    Width = 145,
                    Height = 175,
                    Margin = new Padding(6),
                    Padding = new Padding(4),
                    CardColor = UiStyle.CardBackground,
                    BorderColor = UiStyle.BorderColor,
                    CornerRadius = 8,
                };

                var cardTable = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 3 };
                cardTable.RowStyles.Add(new RowStyle(SizeType.Absolute, 110)); // Thumbnail
                cardTable.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));  // Checkbox & Date
                cardTable.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); // Prompt snippet

                var pic = new PictureBox
                {
                    Dock = DockStyle.Fill,
                    SizeMode = PictureBoxSizeMode.Zoom,
                    BackColor = Color.Black,
                    Cursor = Cursors.Hand,
                };

                try
                {
                    using var fs = new FileStream(item.ImagePath, FileMode.Open, FileAccess.Read);
                    using var original = Image.FromStream(fs);
                    pic.Image = new Bitmap(original);
                }
                catch
                {
                    // Fallback if image cannot be decoded
                }

                cardTable.Controls.Add(pic, 0, 0);

                var cb = new ModernCheckBox
                {
                    Text = item.CreatedAt.ToString("dd.MM HH:mm"),
                    Font = new Font("Segoe UI Semibold", 8F),
                    ForeColor = UiStyle.TextDark,
                    Dock = DockStyle.Fill,
                    Tag = item.ImagePath,
                    Cursor = Cursors.Hand,
                };
                pic.Click += (_, _) => cb.Checked = !cb.Checked;
                _checkBoxes.Add(cb);
                cardTable.Controls.Add(cb, 0, 1);

                var lblPrompt = new Label
                {
                    Text = string.IsNullOrWhiteSpace(item.Prompt) ? item.Engine : item.Prompt,
                    Font = new Font("Segoe UI", 7.5F),
                    ForeColor = UiStyle.TextMuted,
                    Dock = DockStyle.Fill,
                    AutoEllipsis = true,
                };
                cardTable.Controls.Add(lblPrompt, 0, 2);

                card.Controls.Add(cardTable);
                _flowPanel.Controls.Add(card);
            }
            _pickerScroll?.RecalculateScroll();

            _statusLabel.Text = $"{_checkBoxes.Count} stüdyo görseli listelendi.";
        }
        catch (Exception ex)
        {
            _statusLabel.Text = $"Görseller yüklenemedi: {ex.Message}";
        }
    }
}
