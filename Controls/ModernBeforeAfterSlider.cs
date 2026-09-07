namespace SimilarProductsWinForms.Controls;

using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

/// <summary>
/// Mode of image comparison: Split slider or side-by-side.
/// </summary>
public enum ImageComparisonMode
{
    SplitSlider,
    SideBySide,
    AfterOnly
}

/// <summary>
/// Modern interactive Before/After comparison slider with antialiased split line,
/// dragging handle, and side-by-side mode support.
/// </summary>
public class ModernBeforeAfterSlider : Control
{
    private Image? _beforeImage;
    private Image? _afterImage;
    private float _splitRatio = 0.5f; // 0.0 to 1.0
    private bool _isDragging;
    private ImageComparisonMode _mode = ImageComparisonMode.SplitSlider;

    public Image? BeforeImage
    {
        get => _beforeImage;
        set
        {
            _beforeImage = value;
            Invalidate();
        }
    }

    public Image? AfterImage
    {
        get => _afterImage;
        set
        {
            _afterImage = value;
            Invalidate();
        }
    }

    public float SplitRatio
    {
        get => _splitRatio;
        set
        {
            _splitRatio = Math.Clamp(value, 0.05f, 0.95f);
            Invalidate();
        }
    }

    public ImageComparisonMode Mode
    {
        get => _mode;
        set
        {
            _mode = value;
            Invalidate();
        }
    }

    public string BeforeLabel { get; set; } = "📷 Orijinal (Öncesi)";
    public string AfterLabel { get; set; } = "✨ AI HD (Sonrası)";

    public ModernBeforeAfterSlider()
    {
        SetStyle(
            ControlStyles.UserPaint |
            ControlStyles.AllPaintingInWmPaint |
            ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.ResizeRedraw |
            ControlStyles.SupportsTransparentBackColor,
            true);
        DoubleBuffered = true;
        BackColor = Color.FromArgb(15, 23, 42); // Slate 950
        Cursor = Cursors.Default;
    }

    private Rectangle GetImageBounds()
    {
        int pad = 8;
        return new Rectangle(pad, pad, Math.Max(1, Width - (pad * 2)), Math.Max(1, Height - (pad * 2)));
    }

    private int GetDividerX(Rectangle bounds)
    {
        return bounds.X + (int)(bounds.Width * _splitRatio);
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (_mode != ImageComparisonMode.SplitSlider || _beforeImage == null || _afterImage == null) return;

        var bounds = GetImageBounds();
        int divX = GetDividerX(bounds);
        if (Math.Abs(e.X - divX) <= 16 || bounds.Contains(e.Location))
        {
            _isDragging = true;
            UpdateSplitFromMouse(e.X, bounds);
            Capture = true;
        }
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        var bounds = GetImageBounds();

        if (_mode == ImageComparisonMode.SplitSlider && _beforeImage != null && _afterImage != null)
        {
            int divX = GetDividerX(bounds);
            if (_isDragging)
            {
                UpdateSplitFromMouse(e.X, bounds);
                Cursor = Cursors.VSplit;
            }
            else
            {
                Cursor = Math.Abs(e.X - divX) <= 14 ? Cursors.VSplit : Cursors.Default;
            }
        }
        else
        {
            Cursor = Cursors.Default;
        }
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        if (_isDragging)
        {
            _isDragging = false;
            Capture = false;
            Cursor = Cursors.Default;
            Invalidate();
        }
    }

    private void UpdateSplitFromMouse(int mouseX, Rectangle bounds)
    {
        if (bounds.Width <= 0) return;
        float newRatio = (float)(mouseX - bounds.X) / bounds.Width;
        SplitRatio = newRatio;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.InterpolationMode = InterpolationMode.HighQualityBicubic;
        g.PixelOffsetMode = PixelOffsetMode.HighQuality;

        // Background
        using (var bgBrush = new SolidBrush(BackColor))
        {
            g.FillRectangle(bgBrush, ClientRectangle);
        }

        var bounds = GetImageBounds();
        if (bounds.Width <= 10 || bounds.Height <= 10) return;

        // Draw outer rounded border
        using (var borderPen = new Pen(Color.FromArgb(40, 50, 70), 1.5f))
        using (var path = ModernCardPanel.CreateRoundedRectanglePath(bounds, 12))
        {
            g.DrawPath(borderPen, path);
        }

        // Case 1: Both images are null -> show nice empty state
        if (_beforeImage == null && _afterImage == null)
        {
            DrawEmptyState(g, bounds);
            return;
        }

        // Case 2: Only one image exists
        if (_beforeImage == null || _afterImage == null || _mode == ImageComparisonMode.AfterOnly)
        {
            var targetImg = _afterImage ?? _beforeImage!;
            DrawSingleImageFit(g, targetImg, bounds);
            DrawBadgePill(g, bounds.X + 16, bounds.Y + 16, _afterImage != null ? AfterLabel : BeforeLabel, Color.FromArgb(200, 15, 23, 42));
            return;
        }

        // Case 3: Side-by-Side mode
        if (_mode == ImageComparisonMode.SideBySide)
        {
            int halfW = (bounds.Width - 8) / 2;
            var leftRect = new Rectangle(bounds.X, bounds.Y, halfW, bounds.Height);
            var rightRect = new Rectangle(bounds.X + halfW + 8, bounds.Y, halfW, bounds.Height);

            DrawSingleImageFit(g, _beforeImage, leftRect);
            DrawBadgePill(g, leftRect.X + 12, leftRect.Y + 12, BeforeLabel, Color.FromArgb(200, 30, 41, 59));

            DrawSingleImageFit(g, _afterImage, rightRect);
            DrawBadgePill(g, rightRect.X + 12, rightRect.Y + 12, AfterLabel, Color.FromArgb(220, 99, 102, 241));
            return;
        }

        // Case 4: Split Slider mode
        DrawSplitSlider(g, bounds);
    }

    private void DrawSplitSlider(Graphics g, Rectangle bounds)
    {
        int divX = GetDividerX(bounds);

        // Clip 1: Draw After Image on the right portion
        var oldClip = g.Clip;
        var rightClipRect = new Rectangle(divX, bounds.Y, bounds.Right - divX, bounds.Height);
        g.SetClip(rightClipRect, CombineMode.Replace);
        DrawSingleImageFit(g, _afterImage!, bounds);
        g.Clip = oldClip;

        // Clip 2: Draw Before Image on the left portion
        var leftClipRect = new Rectangle(bounds.X, bounds.Y, divX - bounds.X, bounds.Height);
        g.SetClip(leftClipRect, CombineMode.Replace);
        DrawSingleImageFit(g, _beforeImage!, bounds);
        g.Clip = oldClip;

        // Draw Divider Line
        using (var linePen = new Pen(Color.White, 2.5f))
        {
            g.DrawLine(linePen, divX, bounds.Y, divX, bounds.Bottom);
        }

        // Draw Center Drag Handle (Circle with arrows)
        int handleSize = 36;
        int handleY = bounds.Y + (bounds.Height / 2) - (handleSize / 2);
        var handleRect = new Rectangle(divX - (handleSize / 2), handleY, handleSize, handleSize);

        using (var shadowBrush = new SolidBrush(Color.FromArgb(80, 0, 0, 0)))
        {
            g.FillEllipse(shadowBrush, handleRect.X + 2, handleRect.Y + 2, handleSize, handleSize);
        }

        using (var handleBrush = new SolidBrush(Color.FromArgb(99, 102, 241)))
        using (var handlePen = new Pen(Color.White, 2f))
        {
            g.FillEllipse(handleBrush, handleRect);
            g.DrawEllipse(handlePen, handleRect);
        }

        // Draw left/right triangles inside handle
        using (var arrowBrush = new SolidBrush(Color.White))
        {
            PointF[] leftArrow = [
                new PointF(handleRect.X + 10, handleRect.Y + (handleSize / 2)),
                new PointF(handleRect.X + 16, handleRect.Y + (handleSize / 2) - 5),
                new PointF(handleRect.X + 16, handleRect.Y + (handleSize / 2) + 5)
            ];
            PointF[] rightArrow = [
                new PointF(handleRect.Right - 10, handleRect.Y + (handleSize / 2)),
                new PointF(handleRect.Right - 16, handleRect.Y + (handleSize / 2) - 5),
                new PointF(handleRect.Right - 16, handleRect.Y + (handleSize / 2) + 5)
            ];
            g.FillPolygon(arrowBrush, leftArrow);
            g.FillPolygon(arrowBrush, rightArrow);
        }

        // Draw floating tags
        DrawBadgePill(g, bounds.X + 14, bounds.Y + 14, BeforeLabel, Color.FromArgb(200, 30, 41, 59));
        DrawBadgePill(g, bounds.Right - 150, bounds.Y + 14, AfterLabel, Color.FromArgb(220, 99, 102, 241));
    }

    private static void DrawSingleImageFit(Graphics g, Image img, Rectangle targetRect)
    {
        if (img.Width <= 0 || img.Height <= 0) return;

        float ratioX = (float)targetRect.Width / img.Width;
        float ratioY = (float)targetRect.Height / img.Height;
        float ratio = Math.Min(ratioX, ratioY);

        int destW = (int)(img.Width * ratio);
        int destH = (int)(img.Height * ratio);
        int destX = targetRect.X + (targetRect.Width - destW) / 2;
        int destY = targetRect.Y + (targetRect.Height - destH) / 2;

        g.DrawImage(img, destX, destY, destW, destH);
    }

    private static void DrawBadgePill(Graphics g, int x, int y, string text, Color bgColor)
    {
        using var font = new Font("Segoe UI Semibold", 8.5F, FontStyle.Bold);
        var size = g.MeasureString(text, font);
        var rect = new Rectangle(x, y, (int)size.Width + 14, (int)size.Height + 8);

        using var path = ModernCardPanel.CreateRoundedRectanglePath(rect, 8);
        using var brush = new SolidBrush(bgColor);
        using var pen = new Pen(Color.FromArgb(80, 255, 255, 255), 1f);
        using var textBrush = new SolidBrush(Color.White);

        g.FillPath(brush, path);
        g.DrawPath(pen, path);
        g.DrawString(text, font, textBrush, rect.X + 7, rect.Y + 4);
    }

    private static void DrawEmptyState(Graphics g, Rectangle bounds)
    {
        using var font = new Font("Segoe UI Semibold", 11F, FontStyle.Bold);
        using var subFont = new Font("Segoe UI", 9F);
        using var brush = new SolidBrush(Color.FromArgb(148, 163, 184));

        string title = "Görsel Yükleyin veya Mağazadan Seçin";
        string sub = "Sol menüden fotoğraf yükleyin veya sahne preseti seçerek yapay zeka ile stüdyo mockup'ı oluşturun.";

        var tSize = g.MeasureString(title, font);
        var sSize = g.MeasureString(sub, subFont);

        float centerY = bounds.Y + (bounds.Height / 2) - 20;
        g.DrawString(title, font, brush, bounds.X + (bounds.Width - tSize.Width) / 2, centerY);
        g.DrawString(sub, subFont, brush, bounds.X + (bounds.Width - sSize.Width) / 2, centerY + 26);
    }
}
