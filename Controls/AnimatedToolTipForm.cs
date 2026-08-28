using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace SimilarProductsWinForms.Controls;

public class AnimatedToolTipForm : Form
{
    private readonly System.Windows.Forms.Timer _animTimer = new() { Interval = 16 }; // ~60fps
    private int _elapsedMs = 0;
    private int _targetDurationMs = 2000;
    private string _tooltipText = "";
    private string? _customHeader = null;
    private bool _isShowingContent = false;
    private float _sweepAngle = 0f;

    // Colors for the gradient arc
    private static readonly Color ArcColor1 = Color.FromArgb(16, 185, 129);   // Emerald
    private static readonly Color ArcColor2 = Color.FromArgb(99, 102, 241);   // Indigo
    private static readonly Color ArcBgColor = Color.FromArgb(80, 120, 120, 120);

    protected override bool ShowWithoutActivation => true;
    protected override CreateParams CreateParams
    {
        get
        {
            var cp = base.CreateParams;
            cp.ExStyle |= 0x00000080; // WS_EX_TOOLWINDOW - hide from taskbar/alt-tab
            return cp;
        }
    }

    public AnimatedToolTipForm()
    {
        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.Manual;
        ShowInTaskbar = false;
        TopMost = true;
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer, true);

        TransparencyKey = Color.Magenta;
        BackColor = Color.Magenta;

        _animTimer.Tick += AnimTimer_Tick;
    }

    public void ShowTooltip(string text, Point screenPosition, int durationMs = 2000, string? customHeader = null)
    {
        _tooltipText = text;
        _customHeader = customHeader;
        _targetDurationMs = durationMs;
        _elapsedMs = 0;
        _sweepAngle = 0f;
        _isShowingContent = false;

        TransparencyKey = Color.Magenta;
        BackColor = Color.Magenta;
        Size = new Size(54, 54);

        // Position at the specified screen location (centered horizontally)
        Location = new Point(screenPosition.X - 27, screenPosition.Y);

        if (!Visible)
            Show();

        Invalidate();
        _animTimer.Start();
    }

    public void HideTooltip()
    {
        _animTimer.Stop();
        _isShowingContent = false;
        Hide();
    }

    private void AnimTimer_Tick(object? sender, EventArgs e)
    {
        if (_isShowingContent) return;

        _elapsedMs += _animTimer.Interval;
        float progress = Math.Min(1f, (float)_elapsedMs / _targetDurationMs);

        // Ease-out cubic for smooth deceleration
        float eased = 1f - (1f - progress) * (1f - progress) * (1f - progress);
        _sweepAngle = eased * 360f;

        if (progress >= 1f)
        {
            _sweepAngle = 360f;
            _animTimer.Stop();
            _isShowingContent = true;
            ShowContent();
        }

        Invalidate();
    }

    private void ShowContent()
    {
        TransparencyKey = Color.Empty;
        BackColor = Color.FromArgb(30, 30, 38);

        using var g = CreateGraphics();
        using var font = new Font("Consolas", 9F);
        var measured = g.MeasureString(_tooltipText, font, 700);

        int w = Math.Min(750, (int)measured.Width + 36);
        int h = Math.Min(500, (int)measured.Height + 70); // extra space for header
        Size = new Size(w, h);

        // Reposition to ensure it's on screen
        var screen = Screen.FromPoint(Location).WorkingArea;
        int x = Location.X;
        int y = Location.Y;
        if (x + w > screen.Right) x = screen.Right - w - 10;
        if (y + h > screen.Bottom) y = screen.Bottom - h - 10;
        if (x < screen.Left) x = screen.Left + 5;
        if (y < screen.Top) y = screen.Top + 5;
        Location = new Point(x, y);

        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.HighQuality;
        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

        if (!_isShowingContent)
        {
            // === Draw animated loading ring ===
            int ringSize = 42;
            int pad = 6;
            var ringRect = new Rectangle(pad, pad, ringSize, ringSize);

            // Background circle (track)
            using var bgPen = new Pen(ArcBgColor, 5f);
            g.DrawEllipse(bgPen, ringRect);

            // Foreground arc with gradient
            if (_sweepAngle > 0.5f)
            {
                using var brush = new LinearGradientBrush(
                    new Point(0, 0), new Point(ringSize + pad * 2, ringSize + pad * 2),
                    ArcColor1, ArcColor2);
                using var pen = new Pen(brush, 5f);
                pen.StartCap = LineCap.Round;
                pen.EndCap = LineCap.Round;
                g.DrawArc(pen, ringRect, -90, _sweepAngle);
            }

            // Percentage text in center
            int pct = (int)(_sweepAngle / 3.6f);
            string pctText = $"{pct}%";
            using var pctFont = new Font("Segoe UI Semibold", 8.5F, FontStyle.Bold);
            using var pctBrush = new SolidBrush(Color.White);
            var pctSize = g.MeasureString(pctText, pctFont);
            float cx = pad + ringSize / 2f - pctSize.Width / 2f;
            float cy = pad + ringSize / 2f - pctSize.Height / 2f;
            g.DrawString(pctText, pctFont, pctBrush, cx, cy);
        }
        else
        {
            // === Draw tooltip content ===
            var rect = new Rectangle(0, 0, Width - 1, Height - 1);

            // Rounded rectangle background
            using var path = CreateRoundedRect(rect, 10);
            using var bgBrush = new SolidBrush(Color.FromArgb(30, 30, 38));
            g.FillPath(bgBrush, path);

            // Border with accent gradient
            using var borderBrush = new LinearGradientBrush(rect, ArcColor1, ArcColor2, 45f);
            using var borderPen = new Pen(borderBrush, 1.5f);
            g.DrawPath(borderPen, path);

            // Header line
            int headerY = 10;
            string headerText;
            if (!string.IsNullOrWhiteSpace(_customHeader))
            {
                headerText = _customHeader;
            }
            else if (_tooltipText.Contains("kargo", StringComparison.OrdinalIgnoreCase) || _tooltipText.Contains("maliyet", StringComparison.OrdinalIgnoreCase))
            {
                headerText = "📦 Sipariş & Kargo Maliyet Analizi";
            }
            else if (_tooltipText.Contains("satış", StringComparison.OrdinalIgnoreCase))
            {
                headerText = "🟢 Satış Detayları";
            }
            else
            {
                headerText = "🔴 İade Detayları";
            }

            using var headerFont = new Font("Segoe UI Semibold", 10.5F, FontStyle.Bold);
            using var headerBrush = new SolidBrush(ArcColor1);
            g.DrawString(headerText, headerFont, headerBrush, 14, headerY);

            // Divider line
            int divY = headerY + 28;
            using var divPen = new Pen(Color.FromArgb(60, 60, 70), 1f);
            g.DrawLine(divPen, 12, divY, Width - 12, divY);

            // Body text
            using var bodyFont = new Font("Consolas", 9F);
            using var bodyBrush = new SolidBrush(Color.FromArgb(220, 220, 230));
            var textRect = new RectangleF(14, divY + 6, Width - 28, Height - divY - 16);
            g.DrawString(_tooltipText, bodyFont, bodyBrush, textRect);
        }
    }

    private static GraphicsPath CreateRoundedRect(Rectangle bounds, int radius)
    {
        var path = new GraphicsPath();
        int d = radius * 2;
        path.AddArc(bounds.X, bounds.Y, d, d, 180, 90);
        path.AddArc(bounds.Right - d, bounds.Y, d, d, 270, 90);
        path.AddArc(bounds.Right - d, bounds.Bottom - d, d, d, 0, 90);
        path.AddArc(bounds.X, bounds.Bottom - d, d, d, 90, 90);
        path.CloseFigure();
        return path;
    }
}
