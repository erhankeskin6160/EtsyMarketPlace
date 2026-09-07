namespace SimilarProductsWinForms.Controls;

using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Windows.Forms;

/// <summary>
/// Modern drag-and-drop zone with clipboard paste support and thumbnail preview.
/// </summary>
public class ModernImageDropZone : Control
{
    public event EventHandler<(Bitmap Bitmap, string? FilePath)>? ImageSelected;

    private Image? _previewThumbnail;
    private bool _isDragOver;
    private string _filenameText = "";

    public string TitleText { get; set; } = "📁 Görseli Buraya Sürükleyin";
    public string SubtitleText { get; set; } = "PNG, JPG, WEBP veya Panodan Yapıştır (Ctrl+V)";

    public ModernImageDropZone()
    {
        SetStyle(
            ControlStyles.UserPaint |
            ControlStyles.AllPaintingInWmPaint |
            ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.ResizeRedraw |
            ControlStyles.SupportsTransparentBackColor,
            true);
        DoubleBuffered = true;
        AllowDrop = true;
        Height = 110;
        Cursor = Cursors.Hand;
        BackColor = Color.FromArgb(20, 28, 48);
    }

    public void SetThumbnail(Image? img, string? name = null)
    {
        _previewThumbnail?.Dispose();
        _previewThumbnail = img != null ? new Bitmap(img) : null;
        _filenameText = name ?? "";
        Invalidate();
    }

    public void ClearThumbnail()
    {
        _previewThumbnail?.Dispose();
        _previewThumbnail = null;
        _filenameText = "";
        Invalidate();
    }

    protected override void OnDragEnter(DragEventArgs drgevent)
    {
        base.OnDragEnter(drgevent);
        if (drgevent.Data?.GetDataPresent(DataFormats.FileDrop) == true)
        {
            drgevent.Effect = DragDropEffects.Copy;
            _isDragOver = true;
            Invalidate();
        }
    }

    protected override void OnDragLeave(EventArgs e)
    {
        base.OnDragLeave(e);
        _isDragOver = false;
        Invalidate();
    }

    protected override void OnDragDrop(DragEventArgs drgevent)
    {
        base.OnDragDrop(drgevent);
        _isDragOver = false;

        if (drgevent.Data?.GetData(DataFormats.FileDrop) is string[] files && files.Length > 0)
        {
            var file = files[0];
            LoadFromFile(file);
        }
        Invalidate();
    }

    protected override void OnClick(EventArgs e)
    {
        base.OnClick(e);
        using var ofd = new OpenFileDialog
        {
            Filter = "Görsel Dosyaları (*.jpg;*.jpeg;*.png;*.webp)|*.jpg;*.jpeg;*.png;*.webp|Tüm Dosyalar (*.*)|*.*",
            Title = "Ürün Fotoğrafı Seç"
        };
        if (ofd.ShowDialog(FindForm()) == DialogResult.OK)
        {
            LoadFromFile(ofd.FileName);
        }
    }

    public bool TryPasteFromClipboard()
    {
        if (Clipboard.ContainsImage())
        {
            var img = Clipboard.GetImage();
            if (img is Bitmap bmp)
            {
                SetThumbnail(bmp, "Panodan Yapıştırılan Görsel");
                ImageSelected?.Invoke(this, (new Bitmap(bmp), null));
                return true;
            }
        }
        return false;
    }

    public void LoadFromFile(string filePath)
    {
        if (!File.Exists(filePath)) return;

        try
        {
            using var stream = File.OpenRead(filePath);
            using var loaded = Image.FromStream(stream);
            var bmp = new Bitmap(loaded);
            SetThumbnail(bmp, Path.GetFileName(filePath));
            ImageSelected?.Invoke(this, (bmp, filePath));
        }
        catch (Exception ex)
        {
            MessageBox.Show(FindForm(), $"Görsel yüklenemedi: {ex.Message}", "Hata", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

        var rect = new Rectangle(2, 2, Width - 5, Height - 5);
        using var path = ModernCardPanel.CreateRoundedRectanglePath(rect, 10);

        // Background
        Color bg = _isDragOver ? Color.FromArgb(30, 45, 80) : BackColor;
        using (var brush = new SolidBrush(bg))
        {
            g.FillPath(brush, path);
        }

        // Dashed Border
        Color border = _isDragOver ? Color.FromArgb(99, 102, 241) : Color.FromArgb(60, 75, 100);
        using (var pen = new Pen(border, 1.5f) { DashStyle = DashStyle.Dash })
        {
            g.DrawPath(pen, path);
        }

        if (_previewThumbnail != null)
        {
            // Draw thumbnail on the left
            int thumbSize = Height - 20;
            var thumbRect = new Rectangle(12, 10, thumbSize, thumbSize);
            g.DrawImage(_previewThumbnail, thumbRect);

            // Draw info text on right
            using var titleFont = new Font("Segoe UI Semibold", 9.5F, FontStyle.Bold);
            using var subFont = new Font("Segoe UI", 8.5F);
            using var textBrush = new SolidBrush(Color.White);
            using var mutedBrush = new SolidBrush(Color.FromArgb(148, 163, 184));

            g.DrawString("✅ Seçilen Ürün Görseli", titleFont, textBrush, thumbSize + 22, 18);
            using var accentBrush = new SolidBrush(Color.FromArgb(129, 140, 248));
            g.DrawString("Değiştirmek için tıkla veya sürükle", subFont, accentBrush, thumbSize + 22, 58);
        }
        else
        {
            // Draw empty state
            using var titleFont = new Font("Segoe UI Semibold", 9.5F, FontStyle.Bold);
            using var subFont = new Font("Segoe UI", 8.2F);
            using var textBrush = new SolidBrush(_isDragOver ? Color.FromArgb(129, 140, 248) : Color.White);
            using var mutedBrush = new SolidBrush(Color.FromArgb(148, 163, 184));

            var tSize = g.MeasureString(TitleText, titleFont);
            var sSize = g.MeasureString(SubtitleText, subFont);

            float startY = (Height - (tSize.Height + sSize.Height + 4)) / 2;
            g.DrawString(TitleText, titleFont, textBrush, (Width - tSize.Width) / 2, startY);
            g.DrawString(SubtitleText, subFont, mutedBrush, (Width - sSize.Width) / 2, startY + tSize.Height + 4);
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _previewThumbnail?.Dispose();
        }
        base.Dispose(disposing);
    }
}
