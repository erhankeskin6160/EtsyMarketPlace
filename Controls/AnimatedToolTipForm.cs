using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace SimilarProductsWinForms.Controls;

public sealed record ToolTipKpiCard(string Title, string PrimaryValue, string? SubValue, Color AccentColor);

public sealed record ToolTipTableRow(string Date, string Identifier, string Quantity, string Amount, bool HasBadge, string BadgeText, string Description);

public sealed record ToolTipDataPayload(
    string HeaderTitle,
    string? Subtitle,
    List<ToolTipKpiCard> KpiCards,
    string[] ColumnHeaders,
    float[] ColumnWidthWeights,
    List<ToolTipTableRow> Rows,
    string? FooterNote = null
);

public class AnimatedToolTipForm : Form
{
    private readonly System.Windows.Forms.Timer _animTimer = new() { Interval = 16 }; // ~60fps
    private int _elapsedMs = 0;
    private int _targetDurationMs = 2000;
    private float _sweepAngle = 0f;
    private bool _isShowingContent = false;

    private ToolTipDataPayload? _payload = null;
    private string _fallbackText = "";
    private string? _customHeader = null;

    // Accent colors
    private static readonly Color ArcColor1 = Color.FromArgb(16, 185, 129);   // Emerald
    private static readonly Color ArcColor2 = Color.FromArgb(99, 102, 241);   // Indigo
    private static readonly Color ArcBgColor = Color.FromArgb(60, 80, 100);

    // Dark theme palette
    private static readonly Color BgColor = Color.FromArgb(20, 24, 33);
    private static readonly Color CardBgColor = Color.FromArgb(28, 33, 46);
    private static readonly Color RowAltColor = Color.FromArgb(24, 29, 41);
    private static readonly Color HeaderBgColor = Color.FromArgb(33, 39, 54);
    private static readonly Color BorderColor = Color.FromArgb(51, 65, 85);
    private static readonly Color TextPrimary = Color.FromArgb(241, 245, 249);
    private static readonly Color TextMuted = Color.FromArgb(148, 163, 184);

    protected override bool ShowWithoutActivation => true;
    protected override CreateParams CreateParams
    {
        get
        {
            var cp = base.CreateParams;
            cp.ExStyle |= 0x00000080; // WS_EX_TOOLWINDOW
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

    public void ShowStructuredTooltip(ToolTipDataPayload payload, Point screenPosition, int durationMs = 2000)
    {
        _payload = payload;
        _fallbackText = "";
        _customHeader = payload.HeaderTitle;
        StartCountdown(screenPosition, durationMs);
    }

    public void ShowTooltip(string text, Point screenPosition, int durationMs = 2000, string? customHeader = null)
    {
        _payload = null;
        _fallbackText = text;
        _customHeader = customHeader;
        StartCountdown(screenPosition, durationMs);
    }

    private void StartCountdown(Point screenPosition, int durationMs)
    {
        _targetDurationMs = durationMs;
        _elapsedMs = 0;
        _sweepAngle = 0f;
        _isShowingContent = false;

        TransparencyKey = Color.Magenta;
        BackColor = Color.Magenta;
        Size = new Size(54, 54);

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

        // Ease-out cubic
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
        BackColor = BgColor;

        int width = 780;
        int height;

        if (_payload != null)
        {
            int baseHeight = 110; // Header + padding
            if (_payload.KpiCards.Count > 0) baseHeight += 65; // KPI cards
            if (_payload.Rows.Count > 0)
            {
                baseHeight += 32; // Table header
                baseHeight += _payload.Rows.Count * 28; // Rows
            }
            else
            {
                baseHeight += 40; // "No records" note
            }
            if (!string.IsNullOrWhiteSpace(_payload.FooterNote)) baseHeight += 25;

            height = Math.Min(560, Math.Max(180, baseHeight + 15));
        }
        else
        {
            using var g = CreateGraphics();
            using var font = new Font("Segoe UI", 9F);
            var measured = g.MeasureString(_fallbackText, font, 720);
            width = Math.Min(780, (int)measured.Width + 40);
            height = Math.Min(520, (int)measured.Height + 80);
        }

        Size = new Size(width, height);

        // Keep inside screen area
        var screen = Screen.FromPoint(Location).WorkingArea;
        int x = Location.X;
        int y = Location.Y;
        if (x + width > screen.Right) x = screen.Right - width - 12;
        if (y + height > screen.Bottom) y = screen.Bottom - height - 12;
        if (x < screen.Left) x = screen.Left + 8;
        if (y < screen.Top) y = screen.Top + 8;
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
            // === Loading Ring ===
            int ringSize = 42;
            int pad = 6;
            var ringRect = new Rectangle(pad, pad, ringSize, ringSize);

            using var bgPen = new Pen(ArcBgColor, 5f);
            g.DrawEllipse(bgPen, ringRect);

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
            // === Background Card & Border ===
            var rect = new Rectangle(0, 0, Width - 1, Height - 1);
            using var path = CreateRoundedRect(rect, 12);
            using var bgBrush = new SolidBrush(BgColor);
            g.FillPath(bgBrush, path);

            using var borderBrush = new LinearGradientBrush(rect, ArcColor1, ArcColor2, 45f);
            using var borderPen = new Pen(borderBrush, 1.5f);
            g.DrawPath(borderPen, path);

            if (_payload != null)
            {
                DrawStructuredPayload(g, rect);
            }
            else
            {
                DrawFallbackText(g, rect);
            }
        }
    }

    private void DrawStructuredPayload(Graphics g, Rectangle bounds)
    {
        int padX = 16;
        int curY = 12;

        // 1. Header
        using var titleFont = new Font("Segoe UI", 11.5F, FontStyle.Bold);
        using var titleBrush = new SolidBrush(Color.FromArgb(52, 211, 153)); // Emerald
        g.DrawString(_payload!.HeaderTitle, titleFont, titleBrush, padX, curY);
        curY += 22;

        if (!string.IsNullOrWhiteSpace(_payload.Subtitle))
        {
            using var subFont = new Font("Segoe UI", 8.5F);
            using var subBrush = new SolidBrush(TextMuted);
            g.DrawString(_payload.Subtitle, subFont, subBrush, padX, curY);
            curY += 18;
        }

        // Divider
        using var divPen = new Pen(BorderColor, 1f);
        g.DrawLine(divPen, padX, curY, bounds.Width - padX, curY);
        curY += 10;

        // 2. Mini KPI Cards
        if (_payload.KpiCards.Count > 0)
        {
            int cardCount = _payload.KpiCards.Count;
            int gap = 10;
            int availableWidth = bounds.Width - (padX * 2) - ((cardCount - 1) * gap);
            int cardW = availableWidth / cardCount;
            int cardH = 50;

            for (int i = 0; i < cardCount; i++)
            {
                var kpi = _payload.KpiCards[i];
                var cardRect = new Rectangle(padX + i * (cardW + gap), curY, cardW, cardH);

                using var cardPath = CreateRoundedRect(cardRect, 8);
                using var cardBg = new SolidBrush(CardBgColor);
                using var cardBorder = new Pen(BorderColor, 1f);
                g.FillPath(cardBg, cardPath);
                g.DrawPath(cardBorder, cardPath);

                // Accent vertical left bar
                var leftBar = new Rectangle(cardRect.X, cardRect.Y + 6, 3, cardRect.Height - 12);
                using var barBrush = new SolidBrush(kpi.AccentColor);
                g.FillRectangle(barBrush, leftBar);

                // Title
                using var kpiTitleFont = new Font("Segoe UI Semibold", 8F, FontStyle.Bold);
                using var kpiTitleBrush = new SolidBrush(TextMuted);
                g.DrawString(kpi.Title, kpiTitleFont, kpiTitleBrush, cardRect.X + 10, cardRect.Y + 6);

                // Primary Value
                using var kpiValFont = new Font("Segoe UI", 10.5F, FontStyle.Bold);
                using var kpiValBrush = new SolidBrush(kpi.AccentColor);
                g.DrawString(kpi.PrimaryValue, kpiValFont, kpiValBrush, cardRect.X + 10, cardRect.Y + 22);

                // Sub Value (right-aligned if present)
                if (!string.IsNullOrWhiteSpace(kpi.SubValue))
                {
                    using var kpiSubFont = new Font("Segoe UI", 7.8F);
                    using var kpiSubBrush = new SolidBrush(TextMuted);
                    var subSize = g.MeasureString(kpi.SubValue, kpiSubFont);
                    g.DrawString(kpi.SubValue, kpiSubFont, kpiSubBrush, cardRect.Right - subSize.Width - 8, cardRect.Y + 8);
                }
            }

            curY += cardH + 12;
        }

        // 3. Table Header
        if (_payload.ColumnHeaders.Length > 0 && _payload.ColumnWidthWeights.Length == _payload.ColumnHeaders.Length)
        {
            int tableW = bounds.Width - (padX * 2);
            var headerRect = new Rectangle(padX, curY, tableW, 26);

            using var headerPath = CreateRoundedRect(headerRect, 6);
            using var headerBg = new SolidBrush(HeaderBgColor);
            g.FillPath(headerBg, headerPath);

            using var thFont = new Font("Segoe UI Semibold", 8F, FontStyle.Bold);
            using var thBrush = new SolidBrush(TextMuted);
            using var sfRight = new StringFormat { Alignment = StringAlignment.Far, LineAlignment = StringAlignment.Center };
            using var sfLeft = new StringFormat { Alignment = StringAlignment.Near, LineAlignment = StringAlignment.Center };

            float currentX = padX + 8;
            for (int c = 0; c < _payload.ColumnHeaders.Length; c++)
            {
                float colW = tableW * _payload.ColumnWidthWeights[c];
                var colRect = new RectangleF(currentX, curY, colW - 6, 26);

                bool isNumeric = (c == 2 || c == 3); // Quantity or Amount
                g.DrawString(_payload.ColumnHeaders[c], thFont, thBrush, colRect, isNumeric ? sfRight : sfLeft);
                currentX += colW;
            }

            curY += 28;

            // 4. Table Rows
            using var rowFont = new Font("Segoe UI", 8.5F);
            using var idFont = new Font("Consolas", 8.5F, FontStyle.Bold);
            using var amtFont = new Font("Segoe UI Semibold", 8.5F, FontStyle.Bold);
            using var sfTrimming = new StringFormat
            {
                Alignment = StringAlignment.Near,
                LineAlignment = StringAlignment.Center,
                Trimming = StringTrimming.EllipsisCharacter,
                FormatFlags = StringFormatFlags.NoWrap
            };

            for (int r = 0; r < _payload.Rows.Count; r++)
            {
                if (curY + 24 > bounds.Height - 30) break; // Don't overflow

                var row = _payload.Rows[r];
                var rowRect = new Rectangle(padX, curY, tableW, 24);

                if (r % 2 == 1)
                {
                    using var rowBg = new SolidBrush(RowAltColor);
                    using var rPath = CreateRoundedRect(rowRect, 4);
                    g.FillPath(rowBg, rPath);
                }

                currentX = padX + 8;

                // Col 0: Date
                float w0 = tableW * _payload.ColumnWidthWeights[0];
                using (var brush = new SolidBrush(TextMuted))
                    g.DrawString(row.Date, rowFont, brush, new RectangleF(currentX, curY, w0 - 6, 24), sfLeft);
                currentX += w0;

                // Col 1: Identifier (#ReceiptId)
                float w1 = tableW * _payload.ColumnWidthWeights[1];
                using (var brush = new SolidBrush(Color.FromArgb(96, 165, 250))) // Sky Blue
                    g.DrawString(row.Identifier, idFont, brush, new RectangleF(currentX, curY, w1 - 6, 24), sfLeft);
                currentX += w1;

                // Col 2: Quantity
                float w2 = tableW * _payload.ColumnWidthWeights[2];
                using (var brush = new SolidBrush(TextPrimary))
                    g.DrawString(row.Quantity, rowFont, brush, new RectangleF(currentX, curY, w2 - 6, 24), sfRight);
                currentX += w2;

                // Col 3: Amount
                float w3 = tableW * _payload.ColumnWidthWeights[3];
                using (var brush = new SolidBrush(Color.FromArgb(52, 211, 153))) // Emerald
                    g.DrawString(row.Amount, amtFont, brush, new RectangleF(currentX, curY, w3 - 6, 24), sfRight);
                currentX += w3;

                // Col 4: Badge (Fatura Durumu)
                float w4 = tableW * _payload.ColumnWidthWeights[4];
                var badgeBounds = new Rectangle((int)currentX + 4, curY + 3, (int)w4 - 14, 18);
                if (row.HasBadge)
                {
                    using var badgePath = CreateRoundedRect(badgeBounds, 9);
                    using var badgeBg = new SolidBrush(Color.FromArgb(6, 78, 59)); // Dark Emerald
                    using var badgeBorder = new Pen(Color.FromArgb(16, 185, 129), 1f);
                    using var badgeTextBrush = new SolidBrush(Color.FromArgb(110, 231, 183));
                    using var badgeFont = new Font("Segoe UI Semibold", 7.5F, FontStyle.Bold);
                    using var sfCenter = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };

                    g.FillPath(badgeBg, badgePath);
                    g.DrawPath(badgeBorder, badgePath);
                    g.DrawString(row.BadgeText, badgeFont, badgeTextBrush, badgeBounds, sfCenter);
                }
                else
                {
                    using var mutedBrush = new SolidBrush(Color.FromArgb(100, 116, 139));
                    using var sfCenter = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                    g.DrawString("—", rowFont, mutedBrush, badgeBounds, sfCenter);
                }
                currentX += w4;

                // Col 5: Description (Product Title)
                float w5 = tableW * _payload.ColumnWidthWeights[5];
                using (var descBrush = new SolidBrush(TextPrimary))
                    g.DrawString(row.Description, rowFont, descBrush, new RectangleF(currentX, curY, w5 - 6, 24), sfTrimming);

                curY += 24;
            }
        }

        // 5. Footer Note
        if (!string.IsNullOrWhiteSpace(_payload.FooterNote) && curY < bounds.Height - 16)
        {
            using var footFont = new Font("Segoe UI Italic", 8F);
            using var footBrush = new SolidBrush(TextMuted);
            g.DrawString(_payload.FooterNote, footFont, footBrush, padX, bounds.Height - 20);
        }
    }

    private void DrawFallbackText(Graphics g, Rectangle bounds)
    {
        int headerY = 12;
        string headerText = _customHeader ?? "ℹ️ Detay Bilgisi";

        using var headerFont = new Font("Segoe UI Semibold", 10.5F, FontStyle.Bold);
        using var headerBrush = new SolidBrush(ArcColor1);
        g.DrawString(headerText, headerFont, headerBrush, 16, headerY);

        int divY = headerY + 26;
        using var divPen = new Pen(BorderColor, 1f);
        g.DrawLine(divPen, 16, divY, bounds.Width - 16, divY);

        using var bodyFont = new Font("Segoe UI", 9F);
        using var bodyBrush = new SolidBrush(TextPrimary);
        var textRect = new RectangleF(16, divY + 8, bounds.Width - 32, bounds.Height - divY - 16);
        g.DrawString(_fallbackText, bodyFont, bodyBrush, textRect);
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
