namespace SimilarProductsWinForms;

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Windows.Forms;
using SimilarProductsWinForms.Controls;
using SimilarProductsWinForms.Services;

/// <summary>
/// Modal dialog showing the 4-scene batch results with options to load to canvas or export.
/// </summary>
internal sealed class BatchSceneGenerationDialog : Form
{
    public Bitmap? SelectedImage { get; private set; }
    public string? SelectedSceneName { get; private set; }

    private readonly List<BatchSceneResult> _results;

    public BatchSceneGenerationDialog(List<BatchSceneResult> results)
    {
        _results = results;
        BuildUi();
    }

    private void BuildUi()
    {
        Text = "🎯 Çoklu Sahne Toplu Üretim Sonuçları (4'lü Etsy Mockup Seti)";
        Size = new Size(1100, 750);
        StartPosition = FormStartPosition.CenterParent;
        BackColor = Color.FromArgb(15, 23, 42);
        ForeColor = Color.White;
        Font = new Font("Segoe UI", 9.5F);

        var root = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 3, Padding = new Padding(16) };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 55));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 45));
        Controls.Add(root);

        // 1. Header
        var header = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2 };
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 70));
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 30));

        var titleStack = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false };
        titleStack.Controls.Add(new Label
        {
            Text = "🎯 4 Farklı Ticari Etsy Sahnesi Başarıyla Hazırlandı",
            Font = new Font("Segoe UI Semibold", 13F, FontStyle.Bold),
            ForeColor = Color.White,
            AutoSize = true
        });
        titleStack.Controls.Add(new Label
        {
            Text = "İstediğiniz görseli ana tuvale aktarabilir veya tüm seti tek tıkla bilgisayarınıza indirebilirsiniz.",
            ForeColor = UiStyle.TextMuted,
            Font = new Font("Segoe UI", 8.5F),
            AutoSize = true
        });
        header.Controls.Add(titleStack, 0, 0);

        var btnSaveAll = UiStyle.CreateButton("💾 Tüm Seti Klasöre İndir (4 PNG)");
        btnSaveAll.Height = 36;
        btnSaveAll.Anchor = AnchorStyles.Right;
        btnSaveAll.Click += (_, _) => SaveAllImagesToFolder();
        header.Controls.Add(btnSaveAll, 1, 0);
        root.Controls.Add(header, 0, 0);

        // 2. 2x2 Grid of Scene Cards
        var grid = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 2, Padding = new Padding(0, 8, 0, 8) };
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        grid.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
        grid.RowStyles.Add(new RowStyle(SizeType.Percent, 50));

        for (int i = 0; i < 4; i++)
        {
            var result = i < _results.Count ? _results[i] : null;
            int col = i % 2;
            int row = i / 2;
            grid.Controls.Add(BuildSceneCard(result, i + 1), col, row);
        }
        root.Controls.Add(grid, 0, 1);

        // 3. Bottom Bar
        var bottom = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft };
        var btnClose = UiStyle.CreateButton("Kapat", isSecondary: true);
        btnClose.Height = 34;
        btnClose.Click += (_, _) => Close();
        bottom.Controls.Add(btnClose);
        root.Controls.Add(bottom, 0, 2);
    }

    private Control BuildSceneCard(BatchSceneResult? res, int index)
    {
        var card = new ModernCardPanel
        {
            Dock = DockStyle.Fill,
            CornerRadius = 10,
            Padding = new Padding(10),
            Margin = new Padding(6),
            CardColor = UiStyle.CardBackground,
            BorderColor = UiStyle.BorderColor
        };

        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 3 };
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));

        string sceneTitle = res?.SceneName ?? $"Sahne #{index}";
        layout.Controls.Add(new Label
        {
            Text = sceneTitle,
            Font = new Font("Segoe UI Semibold", 9.5F, FontStyle.Bold),
            ForeColor = Color.White,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft
        }, 0, 0);

        var pb = new PictureBox
        {
            Dock = DockStyle.Fill,
            SizeMode = PictureBoxSizeMode.Zoom,
            BackColor = Color.FromArgb(15, 23, 42),
            BorderStyle = BorderStyle.FixedSingle
        };

        if (res?.Success == true && res.Image != null)
        {
            pb.Image = res.Image;
        }
        else
        {
            pb.Paint += (_, pe) =>
            {
                using var brush = new SolidBrush(UiStyle.TextMuted);
                pe.Graphics.DrawString(
                    res?.ErrorMessage ?? "Görsel üretilemedi",
                    Font,
                    brush,
                    new RectangleF(10, pb.Height / 2 - 10, pb.Width - 20, 40));
            };
        }
        layout.Controls.Add(pb, 0, 1);

        var btnBar = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2 };
        btnBar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 60));
        btnBar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 40));

        var btnUse = UiStyle.CreateButton("🎨 Tuvale Yükle");
        btnUse.Height = 32;
        btnUse.Enabled = res?.Success == true && res.Image != null;
        btnUse.Click += (_, _) =>
        {
            if (res?.Image != null)
            {
                SelectedImage = new Bitmap(res.Image);
                SelectedSceneName = res.SceneName;
                DialogResult = DialogResult.OK;
                Close();
            }
        };
        btnBar.Controls.Add(btnUse, 0, 0);

        var btnDownload = UiStyle.CreateButton("💾 İndir", isSecondary: true);
        btnDownload.Height = 32;
        btnDownload.Enabled = res?.Success == true && res.Image != null;
        btnDownload.Click += (_, _) =>
        {
            if (res?.Image != null)
            {
                using var sfd = new SaveFileDialog
                {
                    Filter = "PNG (*.png)|*.png",
                    FileName = $"mockup_{res.SceneName.Replace(" ", "_").ToLowerInvariant()}.png"
                };
                if (sfd.ShowDialog(this) == DialogResult.OK)
                {
                    res.Image.Save(sfd.FileName, ImageFormat.Png);
                    MessageBox.Show(this, "Görsel kaydedildi!", "Başarılı", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
        };
        btnBar.Controls.Add(btnDownload, 1, 0);

        layout.Controls.Add(btnBar, 0, 2);
        card.Controls.Add(layout);
        return card;
    }

    private void SaveAllImagesToFolder()
    {
        using var fbd = new FolderBrowserDialog
        {
            Description = "4'lü Etsy Mockup Setinin Kaydedileceği Klasörü Seçin"
        };
        if (fbd.ShowDialog(this) == DialogResult.OK)
        {
            int saved = 0;
            for (int i = 0; i < _results.Count; i++)
            {
                var r = _results[i];
                if (r.Success && r.Image != null)
                {
                    string safeName = $"etsy_mockup_{i + 1}_{r.SceneName.Replace(" ", "_")}.png";
                    foreach (char c in Path.GetInvalidFileNameChars()) safeName = safeName.Replace(c, '_');
                    string target = Path.Combine(fbd.SelectedPath, safeName);
                    r.Image.Save(target, ImageFormat.Png);
                    saved++;
                }
            }

            MessageBox.Show(this, $"{saved} adet mockup görseli klasöre başarıyla kaydedildi:\n{fbd.SelectedPath}", "Toplu Kaydetme Başarılı", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
    }
}
