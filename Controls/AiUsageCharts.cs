namespace SimilarProductsWinForms.Controls;

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Windows.Forms;

/// <summary>
/// KPI kartlarının içine gömülen gerçek veriye duyarlı mikro trend dalgası
/// </summary>
public sealed class AiCardSparkline : Control
{
    private List<float> _points = [];
    private Color _lineColor = Color.FromArgb(56, 189, 248); // Electric Cyan
    private Color _glowColor = Color.FromArgb(40, 56, 189, 248);

    public AiCardSparkline()
    {
        SetStyle(
            ControlStyles.SupportsTransparentBackColor |
            ControlStyles.AllPaintingInWmPaint |
            ControlStyles.UserPaint |
            ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.ResizeRedraw,
            true);
        DoubleBuffered = true;
        Height = 32;
    }

    protected override void OnParentChanged(EventArgs e)
    {
        base.OnParentChanged(e);
        if (Parent != null && Parent.BackColor != Color.Empty)
        {
            BackColor = Parent.BackColor;
        }
    }

    public void SetData(IEnumerable<float> data, Color lineColor)
    {
        _points = data?.ToList() ?? [];
        _lineColor = lineColor;
        _glowColor = Color.FromArgb(40, lineColor.R, lineColor.G, lineColor.B);
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;

        Color bg = BackColor != Color.Transparent && BackColor != Color.Empty
            ? BackColor
            : (Parent?.BackColor ?? Color.FromArgb(20, 35, 60));
        using (var bgBrush = new SolidBrush(bg))
        {
            g.FillRectangle(bgBrush, ClientRectangle);
        }

        // Eğer veri yoksa veya tüm değerler 0 ise uydurma dalga çizme; sakin sıfır çizgisi çiz
        if (_points.Count < 2 || _points.All(p => p <= 0.0001f))
        {
            float baseY = Height - 5;
            using var baseGlowPen = new Pen(Color.FromArgb(20, _lineColor), 3f);
            g.DrawLine(baseGlowPen, 2, baseY, Width - 2, baseY);
            using var basePen = new Pen(Color.FromArgb(70, _lineColor), 1.2f) { DashStyle = DashStyle.Dash };
            g.DrawLine(basePen, 2, baseY, Width - 2, baseY);
            return;
        }

        float max = Math.Max(1f, _points.Max());
        float min = Math.Min(0f, _points.Min());
        float range = Math.Max(1f, max - min);

        var pts = new PointF[_points.Count];
        float stepX = (float)Width / Math.Max(1, _points.Count - 1);

        for (int i = 0; i < _points.Count; i++)
        {
            float norm = (_points[i] - min) / range;
            float y = Height - 4 - (norm * (Height - 8));
            pts[i] = new PointF(i * stepX, y);
        }

        // Dolgu gradienti
        using (var path = new GraphicsPath())
        {
            path.AddCurve(pts, 0.4f);
            path.AddLine(pts[^1].X, Height, pts[0].X, Height);
            path.CloseFigure();

            using var brush = new LinearGradientBrush(
                new Point(0, 0),
                new Point(0, Height),
                _glowColor,
                Color.FromArgb(0, _lineColor));
            g.FillPath(brush, path);
        }

        // Parlama çizgisi
        using (var glowPen = new Pen(Color.FromArgb(50, _lineColor), 4f))
        {
            glowPen.StartCap = LineCap.Round;
            glowPen.EndCap = LineCap.Round;
            g.DrawCurve(glowPen, pts, 0.4f);
        }

        using (var linePen = new Pen(_lineColor, 2f))
        {
            linePen.StartCap = LineCap.Round;
            linePen.EndCap = LineCap.Round;
            g.DrawCurve(linePen, pts, 0.4f);
        }
    }
}

/// <summary>
/// Günlük Token Tüketimini gösteren çift renkli (Prompt / Completion) yumuşak gradient alan grafiği
/// </summary>
public sealed class AiTokenAreaTrendChart : Control
{
    public sealed class DayTokenPoint
    {
        public DateTime Date { get; set; }
        public long PromptTokens { get; set; }
        public long CompletionTokens { get; set; }
        public long TotalTokens => PromptTokens + CompletionTokens;
    }

    private List<DayTokenPoint> _data = [];
    private PointF? _hoverPoint;
    private DayTokenPoint? _hoverItem;

    private readonly Color PromptColor = Color.FromArgb(168, 85, 247);      // Neon Violet (#A855F7)
    private readonly Color CompletionColor = Color.FromArgb(6, 182, 212);   // Electric Cyan (#06B6D4)

    public AiTokenAreaTrendChart()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer, true);
        BackColor = Color.FromArgb(15, 23, 42); // Deep Slate Navy
        Font = new Font("Segoe UI", 8.5F);

        MouseMove += (_, e) =>
        {
            FindHoverPoint(e.Location);
            Invalidate();
        };

        MouseLeave += (_, _) =>
        {
            _hoverPoint = null;
            _hoverItem = null;
            Invalidate();
        };
    }

    public void SetData(IEnumerable<DayTokenPoint> data)
    {
        _data = data?.OrderBy(x => x.Date).ToList() ?? [];
        Invalidate();
    }

    private void FindHoverPoint(Point mouse)
    {
        if (_data.Count < 2) return;

        float padLeft = 65f;
        float padRight = 20f;
        float chartW = Width - padLeft - padRight;
        float stepX = chartW / (_data.Count - 1);

        int closestIdx = (int)Math.Round((mouse.X - padLeft) / stepX);
        if (closestIdx >= 0 && closestIdx < _data.Count)
        {
            _hoverItem = _data[closestIdx];
            _hoverPoint = new PointF(padLeft + (closestIdx * stepX), mouse.Y);
        }
        else
        {
            _hoverPoint = null;
            _hoverItem = null;
        }
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;

        float padLeft = 65f;
        float padRight = 25f;
        float padTop = 45f;
        float padBottom = 35f;

        float chartW = Width - padLeft - padRight;
        float chartH = Height - padTop - padBottom;

        if (chartW <= 20 || chartH <= 20) return;

        // Kart kapsayıcısı
        using (var cardPath = RoundedRect(new Rectangle(1, 1, Width - 2, Height - 2), 10))
        {
            using var cardBrush = new SolidBrush(Color.FromArgb(17, 24, 39));
            g.FillPath(cardBrush, cardPath);
            using var borderPen = new Pen(Color.FromArgb(31, 41, 55), 1.2f);
            g.DrawPath(borderPen, cardPath);
        }

        // Başlık
        using (var fontTitle = new Font("Segoe UI", 10.5F, FontStyle.Bold))
        using (var brushTitle = new SolidBrush(Color.White))
        {
            g.DrawString("Günlük Token Tüketimi (Giriş vs Çıkış)", fontTitle, brushTitle, 16, 14);
        }

        // Lejant
        float legendX = Width - 240;
        using (var pBrush = new SolidBrush(PromptColor))
        using (var cBrush = new SolidBrush(CompletionColor))
        using (var lFont = new Font("Segoe UI", 8.5F))
        using (var textBrush = new SolidBrush(Color.FromArgb(203, 213, 225)))
        {
            g.FillEllipse(pBrush, legendX, 16, 10, 10);
            g.DrawString("Giriş (Prompt)", lFont, textBrush, legendX + 14, 13);

            g.FillEllipse(cBrush, legendX + 115, 16, 10, 10);
            g.DrawString("Çıkış (Completion)", lFont, textBrush, legendX + 129, 13);
        }

        bool isAllZero = _data.Count == 0 || _data.All(d => d.PromptTokens == 0 && d.CompletionTokens == 0);

        long maxTokens = isAllZero ? 1000 : Math.Max(500, _data.Max(d => Math.Max(d.PromptTokens, d.CompletionTokens)));
        maxTokens = (long)(Math.Ceiling(maxTokens / 250.0) * 250);

        // Yatay ızgara çizgileri
        int gridSteps = 4;
        using (var gridPen = new Pen(Color.FromArgb(30, 41, 59), 1f) { DashStyle = DashStyle.Dash })
        using (var axisFont = new Font("Segoe UI", 7.5F))
        using (var axisBrush = new SolidBrush(Color.FromArgb(148, 163, 184)))
        {
            for (int i = 0; i <= gridSteps; i++)
            {
                float y = padTop + (chartH * i / gridSteps);
                g.DrawLine(gridPen, padLeft, y, padLeft + chartW, y);

                long labelVal = maxTokens - (maxTokens * i / gridSteps);
                string labelStr = FormatTokens(labelVal);
                var sz = g.MeasureString(labelStr, axisFont);
                g.DrawString(labelStr, axisFont, axisBrush, padLeft - sz.Width - 8, y - (sz.Height / 2));
            }
        }

        if (isAllZero)
        {
            // Gerçek sıfır taban çizgisi
            using (var basePen = new Pen(Color.FromArgb(60, 148, 163, 184), 1.2f) { DashStyle = DashStyle.Dash })
            {
                g.DrawLine(basePen, padLeft, padTop + chartH, padLeft + chartW, padTop + chartH);
            }

            // X Ekseninde gerçek tarihleri göster
            if (_data.Count >= 2)
            {
                float stepX = chartW / (_data.Count - 1);
                using var dateFont = new Font("Segoe UI", 7.5F);
                using var dateBrush = new SolidBrush(Color.FromArgb(148, 163, 184));
                int labelStep = Math.Max(1, _data.Count / 6);
                for (int i = 0; i < _data.Count; i += labelStep)
                {
                    string dtStr = _data[i].Date.ToString("dd MMM");
                    var sz = g.MeasureString(dtStr, dateFont);
                    g.DrawString(dtStr, dateFont, dateBrush, padLeft + (i * stepX) - (sz.Width / 2), padTop + chartH + 8);
                }
            }

            // Ortada şık, dürüst bilgilendirme kutusu
            string msgTitle = "📊 Seçilen Dönemde Henüz Token Harcaması Yok";
            string msgSub = "Program içinde yapay zeka araçları (Pazar Araştırması, SEO, Taslak) kullanıldıkça gerçek veriler anlık grafiğe dökülecektir.";
            using var fontMsg1 = new Font("Segoe UI", 9F, FontStyle.Bold);
            using var fontMsg2 = new Font("Segoe UI", 8F);
            using var brushMsg1 = new SolidBrush(Color.FromArgb(226, 232, 240));
            using var brushMsg2 = new SolidBrush(Color.FromArgb(148, 163, 184));

            var sz1 = g.MeasureString(msgTitle, fontMsg1);
            var sz2 = g.MeasureString(msgSub, fontMsg2);

            float boxW = Math.Max(sz1.Width, sz2.Width) + 36;
            float boxH = 54;
            float boxX = padLeft + (chartW - boxW) / 2;
            float boxY = padTop + (chartH - boxH) / 2;

            using (var boxPath = RoundedRect(new Rectangle((int)boxX, (int)boxY, (int)boxW, (int)boxH), 8))
            {
                using var boxBg = new SolidBrush(Color.FromArgb(220, 20, 27, 45));
                g.FillPath(boxBg, boxPath);
                using var boxBorder = new Pen(Color.FromArgb(50, 75, 115), 1.2f);
                g.DrawPath(boxBorder, boxPath);
            }

            g.DrawString(msgTitle, fontMsg1, brushMsg1, boxX + 18, boxY + 9);
            g.DrawString(msgSub, fontMsg2, brushMsg2, boxX + 18, boxY + 30);
            return;
        }

        // Gerçek Veri Çizimi
        float pointStepX = chartW / (_data.Count - 1);
        var promptPts = new PointF[_data.Count];
        var compPts = new PointF[_data.Count];

        for (int i = 0; i < _data.Count; i++)
        {
            float x = padLeft + (i * pointStepX);
            float yP = padTop + chartH - ((float)_data[i].PromptTokens / maxTokens * chartH);
            float yC = padTop + chartH - ((float)_data[i].CompletionTokens / maxTokens * chartH);

            promptPts[i] = new PointF(x, Math.Max(padTop, Math.Min(padTop + chartH, yP)));
            compPts[i] = new PointF(x, Math.Max(padTop, Math.Min(padTop + chartH, yC)));
        }

        // 1. Prompt Dalgası
        DrawGradientArea(g, promptPts, padTop + chartH, PromptColor, 70);
        // 2. Completion Dalgası
        DrawGradientArea(g, compPts, padTop + chartH, CompletionColor, 75);

        // Tarih etiketleri
        using (var dateFont = new Font("Segoe UI", 7.5F))
        using (var dateBrush = new SolidBrush(Color.FromArgb(148, 163, 184)))
        {
            int labelStep = Math.Max(1, _data.Count / 6);
            for (int i = 0; i < _data.Count; i += labelStep)
            {
                string dtStr = _data[i].Date.ToString("dd MMM");
                var sz = g.MeasureString(dtStr, dateFont);
                g.DrawString(dtStr, dateFont, dateBrush, promptPts[i].X - (sz.Width / 2), padTop + chartH + 8);
            }
        }

        // Tooltip on Hover
        if (_hoverPoint.HasValue && _hoverItem != null)
        {
            float hx = _hoverPoint.Value.X;
            using var hoverPen = new Pen(Color.FromArgb(120, 255, 255, 255), 1.2f) { DashStyle = DashStyle.Dot };
            g.DrawLine(hoverPen, hx, padTop, hx, padTop + chartH);

            string tipText = $"{_hoverItem.Date:dd MMMM yyyy}\n• Giriş: {_hoverItem.PromptTokens:N0} token\n• Çıkış: {_hoverItem.CompletionTokens:N0} token\n• Toplam: {_hoverItem.TotalTokens:N0}";
            using var tipFont = new Font("Segoe UI", 8.5F);
            var tipSz = g.MeasureString(tipText, tipFont);

            float tipX = hx + 10;
            if (tipX + tipSz.Width + 16 > Width) tipX = hx - tipSz.Width - 16;
            float tipY = Math.Max(padTop, Math.Min(padTop + chartH - tipSz.Height - 12, _hoverPoint.Value.Y));

            var tipRect = new RectangleF(tipX, tipY, tipSz.Width + 16, tipSz.Height + 12);
            using (var tipPath = RoundedRect(Rectangle.Round(tipRect), 6))
            {
                using var tipBg = new SolidBrush(Color.FromArgb(235, 15, 23, 42));
                g.FillPath(tipBg, tipPath);
                using var tipBorder = new Pen(Color.FromArgb(96, 165, 250), 1.2f);
                g.DrawPath(tipBorder, tipPath);
            }

            using var tipTextBrush = new SolidBrush(Color.White);
            g.DrawString(tipText, tipFont, tipTextBrush, tipX + 8, tipY + 6);
        }
    }

    private static void DrawGradientArea(Graphics g, PointF[] pts, float bottomY, Color baseColor, int alpha)
    {
        if (pts.Length < 2) return;

        using (var path = new GraphicsPath())
        {
            path.AddCurve(pts, 0.45f);
            path.AddLine(pts[^1].X, bottomY, pts[0].X, bottomY);
            path.CloseFigure();

            using var brush = new LinearGradientBrush(
                new Point(0, 0),
                new Point(0, (int)bottomY),
                Color.FromArgb(alpha, baseColor),
                Color.FromArgb(5, baseColor));
            g.FillPath(brush, path);
        }

        using (var glowPen = new Pen(Color.FromArgb(40, baseColor), 5f))
        {
            g.DrawCurve(glowPen, pts, 0.45f);
        }

        using (var linePen = new Pen(baseColor, 2.2f))
        {
            g.DrawCurve(linePen, pts, 0.45f);
        }
    }

    private static string FormatTokens(long val)
    {
        if (val >= 1_000_000) return $"{(val / 1_000_000.0):0.#}M";
        if (val >= 1_000) return $"{(val / 1_000.0):0.#}K";
        return val.ToString();
    }

    private static GraphicsPath RoundedRect(Rectangle bounds, int radius)
    {
        int diameter = radius * 2;
        var size = new Size(diameter, diameter);
        var arc = new Rectangle(bounds.Location, size);
        var path = new GraphicsPath();

        if (radius == 0)
        {
            path.AddRectangle(bounds);
            return path;
        }

        path.AddArc(arc, 180, 90);
        arc.X = bounds.Right - diameter;
        path.AddArc(arc, 270, 90);
        arc.Y = bounds.Bottom - diameter;
        path.AddArc(arc, 0, 90);
        arc.X = bounds.Left;
        path.AddArc(arc, 90, 90);
        path.CloseFigure();
        return path;
    }
}

/// <summary>
/// AI Model Maliyet Dağılımını gösteren gerçek veriye dayalı Donut Pasta Grafiği
/// </summary>
public sealed class AiModelCostDonutChart : Control
{
    public sealed class ModelSlice
    {
        public string ModelName { get; set; } = string.Empty;
        public decimal CostUsd { get; set; }
        public Color SliceColor { get; set; }
    }

    private readonly List<ModelSlice> _slices = [];
    private readonly List<string> _configuredApiModels = [];
    private decimal _totalCost;

    private static readonly Color[] Palette =
    [
        Color.FromArgb(6, 182, 212),   // Cyan (GPT-4o)
        Color.FromArgb(168, 85, 247),  // Purple (DeepSeek)
        Color.FromArgb(245, 158, 11),  // Amber (Claude)
        Color.FromArgb(16, 185, 129),  // Emerald (Gemini)
        Color.FromArgb(239, 68, 68),   // Rose
        Color.FromArgb(59, 130, 246)   // Blue
    ];

    public AiModelCostDonutChart()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer, true);
        BackColor = Color.FromArgb(15, 23, 42);
    }

    public void SetData(IEnumerable<KeyValuePair<string, decimal>> data, IEnumerable<string>? configuredApiModels = null)
    {
        _slices.Clear();
        _configuredApiModels.Clear();

        if (configuredApiModels != null)
        {
            _configuredApiModels.AddRange(configuredApiModels);
        }

        int colorIdx = 0;
        var items = data?.Where(x => x.Value > 0).OrderByDescending(x => x.Value).ToList() ?? [];
        _totalCost = items.Sum(x => x.Value);

        foreach (var item in items)
        {
            _slices.Add(new ModelSlice
            {
                ModelName = item.Key,
                CostUsd = item.Value,
                SliceColor = Palette[colorIdx % Palette.Length]
            });
            colorIdx++;
        }

        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;

        // Container
        using (var cardPath = RoundedRect(new Rectangle(1, 1, Width - 2, Height - 2), 10))
        {
            using var cardBrush = new SolidBrush(Color.FromArgb(17, 24, 39));
            g.FillPath(cardBrush, cardPath);
            using var borderPen = new Pen(Color.FromArgb(31, 41, 55), 1.2f);
            g.DrawPath(borderPen, cardPath);
        }

        // Title
        using (var fontTitle = new Font("Segoe UI", 10.5F, FontStyle.Bold))
        using (var brushTitle = new SolidBrush(Color.White))
        {
            g.DrawString("Model Maliyet Dağılımı", fontTitle, brushTitle, 16, 14);
        }

        // Layout: Donut on the left, Legend on the right
        float donutSize = Math.Min(Height - 60, (Width * 0.44f));
        donutSize = Math.Max(80, Math.Min(160, donutSize));

        float donutX = 20;
        float donutY = 44 + ((Height - 50 - donutSize) / 2);

        var donutRect = new RectangleF(donutX, donutY, donutSize, donutSize);
        float holeSize = donutSize * 0.58f;
        var holeRect = new RectangleF(
            donutX + ((donutSize - holeSize) / 2),
            donutY + ((donutSize - holeSize) / 2),
            holeSize,
            holeSize);

        if (_totalCost > 0 && _slices.Count > 0)
        {
            // Gerçek Harcama Dilimlerini Çiz
            float startAngle = -90f;
            foreach (var slice in _slices)
            {
                float sweepAngle = (float)(slice.CostUsd / _totalCost) * 360f;
                if (sweepAngle <= 0) continue;

                using (var path = new GraphicsPath())
                {
                    path.AddArc(donutRect, startAngle, sweepAngle);
                    path.AddArc(holeRect, startAngle + sweepAngle, -sweepAngle);
                    path.CloseFigure();

                    using var brush = new SolidBrush(slice.SliceColor);
                    g.FillPath(brush, path);

                    using var pen = new Pen(Color.FromArgb(17, 24, 39), 2f);
                    g.DrawPath(pen, path);
                }

                startAngle += sweepAngle;
            }

            // Ortadaki toplam tutar
            string totalStr = $"${_totalCost:F2}";
            using var fCenter = new Font("Segoe UI", 11F, FontStyle.Bold);
            using var fSub = new Font("Segoe UI", 7.5F);
            using var bCenter = new SolidBrush(Color.White);
            using var bSub = new SolidBrush(Color.FromArgb(148, 163, 184));
            var szTotal = g.MeasureString(totalStr, fCenter);
            var szSub = g.MeasureString("Toplam", fSub);

            float cx = donutX + (donutSize / 2);
            float cy = donutY + (donutSize / 2);
            g.DrawString(totalStr, fCenter, bCenter, cx - (szTotal.Width / 2), cy - 11);
            g.DrawString("Toplam", fSub, bSub, cx - (szSub.Width / 2), cy + 7);
        }
        else
        {
            // Sıfır harcama durumunda boş şık halka
            using (var emptyRingPath = new GraphicsPath())
            {
                emptyRingPath.AddEllipse(donutRect);
                emptyRingPath.AddEllipse(holeRect);
                using var ringBrush = new SolidBrush(Color.FromArgb(28, 36, 50));
                g.FillPath(ringBrush, emptyRingPath);
                using var ringPen = new Pen(Color.FromArgb(45, 55, 75), 1.2f);
                g.DrawPath(ringPen, emptyRingPath);
            }

            // Ortadaki $0.00
            using var fCenter = new Font("Segoe UI", 11.5F, FontStyle.Bold);
            using var fSub = new Font("Segoe UI", 7.5F);
            using var bCenter = new SolidBrush(Color.FromArgb(148, 163, 184));
            using var bSub = new SolidBrush(Color.FromArgb(100, 115, 140));

            var szTotal = g.MeasureString("$0.00", fCenter);
            var szSub = g.MeasureString("0 İşlem", fSub);
            float cx = donutX + (donutSize / 2);
            float cy = donutY + (donutSize / 2);
            g.DrawString("$0.00", fCenter, bCenter, cx - (szTotal.Width / 2), cy - 11);
            g.DrawString("0 İşlem", fSub, bSub, cx - (szSub.Width / 2), cy + 7);
        }

        // Sağdaki Lejant (Kullanıcının Programdaki Gerçek API Tanımları)
        float legendX = donutX + donutSize + 20;
        float legendY = 48;
        using var legFont = new Font("Segoe UI", 8F);
        using var legFontBold = new Font("Segoe UI", 8F, FontStyle.Bold);

        if (_slices.Count > 0)
        {
            foreach (var slice in _slices.Take(5))
            {
                float pct = _totalCost > 0 ? (float)(slice.CostUsd / _totalCost * 100m) : 0f;
                using (var dotBrush = new SolidBrush(slice.SliceColor))
                {
                    g.FillEllipse(dotBrush, legendX, legendY + 3, 10, 10);
                }

                using (var nameBrush = new SolidBrush(Color.FromArgb(226, 232, 240)))
                {
                    string displayName = slice.ModelName.Length > 18 ? slice.ModelName[..16] + ".." : slice.ModelName;
                    g.DrawString(displayName, legFont, nameBrush, legendX + 16, legendY);
                }

                using (var valBrush = new SolidBrush(Color.FromArgb(148, 163, 184)))
                {
                    string valStr = $"{pct:F0}% (${slice.CostUsd:F2})";
                    g.DrawString(valStr, legFontBold, valBrush, legendX + 16, legendY + 15);
                }

                legendY += 36;
                if (legendY + 25 > Height) break;
            }
        }
        else if (_configuredApiModels.Count > 0)
        {
            // Kullanıcının ayarlarda girdiği gerçek API'lerin listesi
            int cIdx = 0;
            foreach (var apiModel in _configuredApiModels.Take(5))
            {
                var color = Palette[cIdx % Palette.Length];
                using (var dotBrush = new SolidBrush(color))
                {
                    g.FillEllipse(dotBrush, legendX, legendY + 3, 10, 10);
                }

                using (var nameBrush = new SolidBrush(Color.FromArgb(226, 232, 240)))
                {
                    string displayName = apiModel.Length > 20 ? apiModel[..18] + ".." : apiModel;
                    g.DrawString(displayName, legFont, nameBrush, legendX + 16, legendY);
                }

                using (var valBrush = new SolidBrush(Color.FromArgb(52, 211, 153))) // Soft Green
                {
                    g.DrawString("$0.00 (Hazır / 0 Çağrı)", legFontBold, valBrush, legendX + 16, legendY + 15);
                }

                legendY += 36;
                cIdx++;
                if (legendY + 25 > Height) break;
            }
        }
        else
        {
            // Hiç API anahtarı tanımlanmamış durumu
            using var textBrush = new SolidBrush(Color.FromArgb(148, 163, 184));
            g.DrawString("🔑 API Anahtarı Tanımlı Değil", legFontBold, textBrush, legendX, legendY);
            using var subBrush = new SolidBrush(Color.FromArgb(100, 115, 140));
            g.DrawString("Yukarıdaki 'AI Sağlayıcı & API Merkezi'nden\nanahtarlarınızı ekleyebilirsiniz.", legFont, subBrush, legendX, legendY + 18);
        }
    }

    private static GraphicsPath RoundedRect(Rectangle bounds, int radius)
    {
        int diameter = radius * 2;
        var size = new Size(diameter, diameter);
        var arc = new Rectangle(bounds.Location, size);
        var path = new GraphicsPath();

        if (radius == 0)
        {
            path.AddRectangle(bounds);
            return path;
        }

        path.AddArc(arc, 180, 90);
        arc.X = bounds.Right - diameter;
        path.AddArc(arc, 270, 90);
        arc.Y = bounds.Bottom - diameter;
        path.AddArc(arc, 0, 90);
        arc.X = bounds.Left;
        path.AddArc(arc, 90, 90);
        path.CloseFigure();
        return path;
    }
}

/// <summary>
/// Programın 3 ana modülünün gerçek harcama ve çağrı sayılarını gösteren Bar Grafiği
/// </summary>
public sealed class AiModuleUsageBarChart : Control
{
    public sealed class ModuleUsageItem
    {
        public string ModuleName { get; set; } = string.Empty;
        public decimal CostUsd { get; set; }
        public int RequestCount { get; set; }
        public Color BarColor { get; set; }
    }

    private readonly List<ModuleUsageItem> _items = [];

    private static readonly Color[] BarColors =
    [
        Color.FromArgb(168, 85, 247), // Purple
        Color.FromArgb(6, 182, 212),  // Cyan
        Color.FromArgb(245, 158, 11), // Amber
        Color.FromArgb(16, 185, 129)  // Emerald
    ];

    public AiModuleUsageBarChart()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer, true);
        BackColor = Color.FromArgb(15, 23, 42);
        Height = 90;
    }

    public void SetData(IEnumerable<Tuple<string, decimal, int>> modules)
    {
        _items.Clear();
        int idx = 0;
        foreach (var m in modules.OrderByDescending(x => x.Item2))
        {
            _items.Add(new ModuleUsageItem
            {
                ModuleName = m.Item1,
                CostUsd = m.Item2,
                RequestCount = m.Item3,
                BarColor = BarColors[idx % BarColors.Length]
            });
            idx++;
        }

        // Eğer modüllerde henüz harcama yoksa programın 3 ana modülünü sıfır değerlerle göster (Uydurma veri yok)
        if (_items.Count == 0)
        {
            _items.Add(new ModuleUsageItem { ModuleName = "Pazar Araştırması", CostUsd = 0.00m, RequestCount = 0, BarColor = BarColors[0] });
            _items.Add(new ModuleUsageItem { ModuleName = "SEO Başlık & Etiket", CostUsd = 0.00m, RequestCount = 0, BarColor = BarColors[1] });
            _items.Add(new ModuleUsageItem { ModuleName = "Ürün Açıklama Stüdyosu", CostUsd = 0.00m, RequestCount = 0, BarColor = BarColors[2] });
        }

        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;

        using (var cardPath = RoundedRect(new Rectangle(1, 1, Width - 2, Height - 2), 10))
        {
            using var cardBrush = new SolidBrush(Color.FromArgb(17, 24, 39));
            g.FillPath(cardBrush, cardPath);
            using var borderPen = new Pen(Color.FromArgb(31, 41, 55), 1.2f);
            g.DrawPath(borderPen, cardPath);
        }

        // Başlık
        using (var fontTitle = new Font("Segoe UI", 9.5F, FontStyle.Bold))
        using (var brushTitle = new SolidBrush(Color.White))
        {
            g.DrawString("Modül Bazlı Kullanım & Harcama Oranları", fontTitle, brushTitle, 16, 10);
        }

        decimal maxCost = _items.Any(x => x.CostUsd > 0) ? _items.Max(x => x.CostUsd) : 0m;
        int colCount = Math.Min(3, _items.Count);
        if (colCount == 0) return;

        float colW = (Width - 32) / colCount;
        using var nameFont = new Font("Segoe UI", 8.5F);
        using var valFont = new Font("Segoe UI", 8F, FontStyle.Bold);
        using var textBrush = new SolidBrush(Color.FromArgb(203, 213, 225));
        using var subBrush = new SolidBrush(Color.FromArgb(148, 163, 184));

        for (int i = 0; i < colCount; i++)
        {
            var item = _items[i];
            float x = 16 + (i * colW);
            float y = 34;

            // Modül adı ve maliyeti
            g.DrawString(item.ModuleName, nameFont, textBrush, x, y);
            string costStr = $"${item.CostUsd:F2} ({item.RequestCount} çağrı)";
            var szCost = g.MeasureString(costStr, valFont);
            g.DrawString(costStr, valFont, subBrush, x + colW - szCost.Width - 16, y);

            // Çubuk arkaplanı
            float barW = colW - 20;
            float barH = 10;
            float barY = y + 20;

            using (var bgPath = RoundedRect(new Rectangle((int)x, (int)barY, (int)barW, (int)barH), 4))
            {
                using var bgBrush = new SolidBrush(Color.FromArgb(31, 41, 55));
                g.FillPath(bgBrush, bgPath);
            }

            // Gerçek harcama varsa çubuğu doldur; yoksa boş kalsın (0% doluluk)
            if (maxCost > 0 && item.CostUsd > 0)
            {
                float fillRatio = (float)(item.CostUsd / maxCost);
                float fillW = Math.Max(8, barW * fillRatio);

                using var fillPath = RoundedRect(new Rectangle((int)x, (int)barY, (int)fillW, (int)barH), 4);
                using var fillBrush = new LinearGradientBrush(
                    new Point((int)x, (int)barY),
                    new Point((int)(x + fillW), (int)barY),
                    Color.FromArgb(180, item.BarColor),
                    item.BarColor);
                g.FillPath(fillBrush, fillPath);
            }
        }
    }

    private static GraphicsPath RoundedRect(Rectangle bounds, int radius)
    {
        int diameter = radius * 2;
        var size = new Size(diameter, diameter);
        var arc = new Rectangle(bounds.Location, size);
        var path = new GraphicsPath();

        if (radius == 0)
        {
            path.AddRectangle(bounds);
            return path;
        }

        path.AddArc(arc, 180, 90);
        arc.X = bounds.Right - diameter;
        path.AddArc(arc, 270, 90);
        arc.Y = bounds.Bottom - diameter;
        path.AddArc(arc, 0, 90);
        arc.X = bounds.Left;
        path.AddArc(arc, 90, 90);
        path.CloseFigure();
        return path;
    }
}
