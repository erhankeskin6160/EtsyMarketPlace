namespace SimilarProductsWinForms.Controls;

using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

/// <summary>
/// Modern, SaaS dashboard estetiğine uygun interaktif yayın durumu kartı.
/// Pürüzsüz animasyonlu iOS/Linear stili toggle switch, dinamik durum rozeti (badge)
/// ve açıklama metni barındırır.
/// </summary>
public class ModernPublishToggleCard : Control
{
    private bool _checked = false;
    private bool _isHovered = false;
    private float _animProgress = 0f; // 0.0f (Taslak/Off) -> 1.0f (Canlı/On)
    private readonly System.Windows.Forms.Timer _animTimer;

    public event EventHandler? CheckedChanged;

    public string Title { get; set; } = "Hemen Canlı Yayına Al";
    public string ActiveBadgeText { get; set; } = "CANLI";
    public string InactiveBadgeText { get; set; } = "TASLAK";
    public string ActiveDescription { get; set; } = "Açık: Ürün doğrudan mağazada satışa açılır.";
    public string InactiveDescription { get; set; } = "Kapalı: Güvenli mod. Taslak olarak saklanır.";
    public int CornerRadius { get; set; } = 8;

    public bool Checked
    {
        get => _checked;
        set
        {
            if (_checked != value)
            {
                _checked = value;
                StartAnimation();
                Invalidate();
                CheckedChanged?.Invoke(this, EventArgs.Empty);
            }
        }
    }

    public ModernPublishToggleCard()
    {
        SetStyle(
            ControlStyles.UserPaint |
            ControlStyles.AllPaintingInWmPaint |
            ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.ResizeRedraw |
            ControlStyles.Selectable |
            ControlStyles.SupportsTransparentBackColor,
            true);

        DoubleBuffered = true;
        Height = 58;
        Cursor = Cursors.Hand;
        TabStop = true;

        _animTimer = new System.Windows.Forms.Timer { Interval = 15 };
        _animTimer.Tick += OnAnimTimerTick;
    }

    public override Size GetPreferredSize(Size proposedSize)
    {
        int w = proposedSize.Width > 0 ? proposedSize.Width : 260;
        return new Size(w, 58);
    }

    private void StartAnimation()
    {
        _animTimer.Start();
    }

    private void OnAnimTimerTick(object? sender, EventArgs e)
    {
        const float step = 0.18f;
        if (_checked)
        {
            _animProgress += step;
            if (_animProgress >= 1f)
            {
                _animProgress = 1f;
                _animTimer.Stop();
            }
        }
        else
        {
            _animProgress -= step;
            if (_animProgress <= 0f)
            {
                _animProgress = 0f;
                _animTimer.Stop();
            }
        }
        Invalidate();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _animTimer.Stop();
            _animTimer.Dispose();
        }
        base.Dispose(disposing);
    }

    protected override void OnMouseEnter(EventArgs e)
    {
        base.OnMouseEnter(e);
        _isHovered = true;
        Invalidate();
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        _isHovered = false;
        Invalidate();
    }

    protected override void OnClick(EventArgs e)
    {
        base.OnClick(e);
        Focus();
        Checked = !Checked;
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.KeyCode is Keys.Space or Keys.Enter)
        {
            Checked = !Checked;
            e.Handled = true;
        }
    }

    private Color GetEffectiveParentBackColor()
    {
        Control? p = Parent;
        while (p != null)
        {
            if (p.BackColor != Color.Transparent && p.BackColor.A == 255)
            {
                return p.BackColor;
            }
            p = p.Parent;
        }
        return UiStyle.CardBackground;
    }

    protected override void OnPaint(PaintEventArgs pevent)
    {
        var g = pevent.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

        // 1. Arka planı temizle (köşelerde kırpılma olmaması için)
        Color parentBg = GetEffectiveParentBackColor();
        using (var clearBrush = new SolidBrush(parentBg))
        {
            g.FillRectangle(clearBrush, ClientRectangle);
        }

        int w = Width;
        int h = Height;
        if (w <= 10 || h <= 10) return;

        var cardRect = new Rectangle(1, 1, w - 2, h - 2);
        using var cardPath = ModernCardPanel.CreateRoundedRectanglePath(cardRect, CornerRadius);

        // 2. Kart Zemin Rengi (Taslak ve Canlı durumu arasında pürüzsüz geçiş)
        Color baseOffColor = _isHovered ? Color.FromArgb(30, 42, 64) : Color.FromArgb(24, 34, 53);
        Color baseOnColor = _isHovered ? Color.FromArgb(16, 46, 44) : Color.FromArgb(13, 38, 38);
        Color currentCardBg = InterpolateColor(baseOffColor, baseOnColor, _animProgress);

        using (var cardBrush = new SolidBrush(currentCardBg))
        {
            g.FillPath(cardBrush, cardPath);
        }

        // 3. Kart Kenarlık Rengi
        Color borderOff = _isHovered ? UiStyle.PrimaryColor : Color.FromArgb(51, 65, 85);
        Color borderOn = _isHovered ? Color.FromArgb(52, 211, 153) : Color.FromArgb(16, 185, 129);
        Color currentBorder = InterpolateColor(borderOff, borderOn, _animProgress);

        using (var borderPen = new Pen(currentBorder, 1.5f))
        {
            g.DrawPath(borderPen, cardPath);
        }

        // 4. Sağ Taraftaki Toggle Switch Çizimi
        int switchW = 40;
        int switchH = 22;
        int switchX = w - switchW - 10;
        int switchY = (h - switchH) / 2;
        var switchRect = new Rectangle(switchX, switchY, switchW, switchH);

        using (var trackPath = ModernCardPanel.CreateRoundedRectanglePath(switchRect, switchH / 2))
        {
            Color trackOff = _isHovered ? Color.FromArgb(71, 85, 105) : Color.FromArgb(51, 65, 85);
            Color trackOn = _isHovered ? Color.FromArgb(5, 150, 105) : Color.FromArgb(16, 185, 129);
            Color currentTrackColor = InterpolateColor(trackOff, trackOn, _animProgress);

            using var trackBrush = new SolidBrush(currentTrackColor);
            g.FillPath(trackBrush, trackPath);

            using var trackPen = new Pen(InterpolateColor(Color.FromArgb(71, 85, 105), Color.FromArgb(16, 185, 129), _animProgress), 1f);
            g.DrawPath(trackPen, trackPath);
        }

        // Toggle Knob (Yuvarlak Düğme)
        int knobSize = 16;
        int knobMinX = switchX + 3;
        int knobMaxX = switchX + switchW - 3 - knobSize;
        float currentKnobX = knobMinX + (knobMaxX - knobMinX) * _animProgress;
        float knobY = switchY + (switchH - knobSize) / 2f;
        var knobRect = new RectangleF(currentKnobX, knobY, knobSize, knobSize);

        // Knob hafif alt gölgesi
        var knobShadowRect = new RectangleF(currentKnobX, knobY + 1f, knobSize, knobSize);
        using (var shadowBrush = new SolidBrush(Color.FromArgb(40, 0, 0, 0)))
        {
            g.FillEllipse(shadowBrush, knobShadowRect);
        }

        // Knob gövdesi
        using (var knobBrush = new SolidBrush(Color.White))
        {
            g.FillEllipse(knobBrush, knobRect);
        }

        // 5. Sol / Orta İçerik Çizimi (Başlık, Rozet ve Açıklama)
        int contentLeft = 10;
        int contentRight = switchX - 8;
        int maxContentWidth = Math.Max(40, contentRight - contentLeft);

        // 5.1 Başlık Metni
        using var titleFont = new Font("Segoe UI Semibold", 8.5F, FontStyle.Bold);
        var titleSize = g.MeasureString(Title, titleFont);
        g.DrawString(Title, titleFont, Brushes.White, new PointF(contentLeft, 9));

        // 5.2 Durum Rozeti (Pill Badge)
        string badgeText = _animProgress >= 0.5f ? ActiveBadgeText : InactiveBadgeText;
        using var badgeFont = new Font("Segoe UI Semibold", 7F, FontStyle.Bold);
        var badgeTextSize = g.MeasureString(badgeText, badgeFont);

        float badgeW = badgeTextSize.Width + 18; // 6px dot + gaps
        float badgeH = 17;
        bool fitsOnTitleRow = (contentLeft + titleSize.Width + 6 + badgeW) <= contentRight;

        float badgeX;
        float badgeY;
        RectangleF descRect;

        if (fitsOnTitleRow)
        {
            badgeX = contentLeft + titleSize.Width + 6;
            badgeY = 9;
            descRect = new RectangleF(contentLeft, 31, maxContentWidth, 18);
        }
        else
        {
            badgeX = contentLeft;
            badgeY = 30;
            float descLeft = badgeX + badgeW + 6;
            float descWidth = Math.Max(20, contentRight - descLeft);
            descRect = new RectangleF(descLeft, 31, descWidth, 18);
        }

        // Rozet Çizimi
        var badgeRect = new RectangleF(badgeX, badgeY, badgeW, badgeH);
        using var badgePath = ModernCardPanel.CreateRoundedRectanglePath(Rectangle.Round(badgeRect), 4);

        Color badgeBgOff = Color.FromArgb(39, 52, 73);
        Color badgeBgOn = Color.FromArgb(6, 78, 59);
        Color currentBadgeBg = InterpolateColor(badgeBgOff, badgeBgOn, _animProgress);

        Color badgeFgOff = Color.FromArgb(203, 213, 225);
        Color badgeFgOn = Color.FromArgb(167, 243, 208);
        Color currentBadgeFg = InterpolateColor(badgeFgOff, badgeFgOn, _animProgress);

        Color dotColorOff = Color.FromArgb(148, 163, 184);
        Color dotColorOn = Color.FromArgb(52, 211, 153);
        Color currentDotColor = InterpolateColor(dotColorOff, dotColorOn, _animProgress);

        using (var badgeBgBrush = new SolidBrush(currentBadgeBg))
        {
            g.FillPath(badgeBgBrush, badgePath);
        }

        using (var badgeBorderPen = new Pen(InterpolateColor(Color.FromArgb(71, 85, 105), Color.FromArgb(16, 185, 129), _animProgress), 1f))
        {
            g.DrawPath(badgeBorderPen, badgePath);
        }

        // Rozet içindeki renkli durum noktası
        using (var dotBrush = new SolidBrush(currentDotColor))
        {
            g.FillEllipse(dotBrush, badgeX + 5, badgeY + 5.5f, 5.5f, 5.5f);
        }

        // Rozet metni
        using (var badgeFgBrush = new SolidBrush(currentBadgeFg))
        {
            g.DrawString(badgeText, badgeFont, badgeFgBrush, badgeX + 13, badgeY + 2f);
        }

        // 5.3 Alt Açıklama Metni
        string descText = _animProgress >= 0.5f ? ActiveDescription : InactiveDescription;
        using var descFont = new Font("Segoe UI", 7.5F, FontStyle.Regular);
        Color descOff = Color.FromArgb(148, 163, 184);
        Color descOn = Color.FromArgb(110, 231, 183);
        Color currentDescColor = InterpolateColor(descOff, descOn, _animProgress);

        using (var descBrush = new SolidBrush(currentDescColor))
        {
            var sf = new StringFormat
            {
                Trimming = StringTrimming.EllipsisCharacter,
                FormatFlags = StringFormatFlags.NoWrap
            };
            g.DrawString(descText, descFont, descBrush, descRect, sf);
        }

        // 6. Odak Çerçevesi (Klavye Gezinimi)
        if (Focused)
        {
            using var focusPen = new Pen(Color.FromArgb(99, 102, 241), 1f) { DashStyle = DashStyle.Dot };
            var focusRect = new Rectangle(2, 2, w - 5, h - 5);
            using var focusPath = ModernCardPanel.CreateRoundedRectanglePath(focusRect, CornerRadius - 2);
            g.DrawPath(focusPen, focusPath);
        }
    }

    private static Color InterpolateColor(Color from, Color to, float factor)
    {
        factor = Math.Clamp(factor, 0f, 1f);
        int r = (int)(from.R + (to.R - from.R) * factor);
        int g = (int)(from.G + (to.G - from.G) * factor);
        int b = (int)(from.B + (to.B - from.B) * factor);
        return Color.FromArgb(r, g, b);
    }
}
