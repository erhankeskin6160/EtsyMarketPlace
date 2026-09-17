namespace SimilarProductsWinForms.Controls;

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Windows.Forms;

/// <summary>
/// KPI kartlarının içine gömülen neon mikro trend dalgası
/// </summary>
public sealed class AiCardSparkline : Control
{
    private List<float> _points = [];
    private Color _lineColor = Color.FromArgb(56, 189, 248); // Electric Cyan
    private Color _glowColor = Color.FromArgb(40, 56, 189, 248);

    public AiCardSparkline()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer, true);
        BackColor = Color.Transparent;
        Height = 32;
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

        if (_points.Count < 2)
        {
            // Default graceful aesthetic sine wave if empty
            _points = [10, 14, 12, 19, 15, 24, 20, 28, 26, 35, 30, 42];
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

        // Fill subtle gradient area under curve
        using (var path = new GraphicsPath())
        {
            path.AddCurve(pts, 0.4f);
            path.AddLine(pts[^1].X, Height, pts[0].X, Height);
            path.CloseFigure();

            using var brush = new LinearGradientBrush(
                new Point(0, 0),
                new Point(0, Height),
                _glowColor,
                Color.Transparent);
            g.FillPath(brush, path);
        }

        // Draw glowing line
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
/// Günlük Token Tüketimini gösteren çift renkli (Prompt / Completion) yumuşak gradient alan dalga grafiği
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

        // Card container background and rounded border
        using (var cardPath = RoundedRect(new Rectangle(1, 1, Width - 2, Height - 2), 10))
        {
            using var cardBrush = new SolidBrush(Color.FromArgb(17, 24, 39));
            g.FillPath(cardBrush, cardPath);
            using var borderPen = new Pen(Color.FromArgb(31, 41, 55), 1.2f);
            g.DrawPath(borderPen, cardPath);
        }

        // Header and Legend
        using (var fontTitle = new Font("Segoe UI", 10.5F, FontStyle.Bold))
        using (var brushTitle = new SolidBrush(Color.White))
        {
            g.DrawString("Günlük Token Tüketimi (Giriş vs Çıkış)", fontTitle, brushTitle, 16, 14);
        }

        // Legend dots
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

        // If no data, display mock aesthetic wave or informative placeholder
        var displayData = _data.Count >= 2 ? _data : GenerateMockData();

        long maxTokens = Math.Max(1000, displayData.Max(d => Math.Max(d.PromptTokens, d.CompletionTokens)));
        // Round maxTokens to nice number
        maxTokens = (long)(Math.Ceiling(maxTokens / 500.0) * 500);

        // Draw horizontal grid lines
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

        // Calculate points
        float stepX = chartW / (displayData.Count - 1);
        var promptPts = new PointF[displayData.Count];
        var compPts = new PointF[displayData.Count];

        for (int i = 0; i < displayData.Count; i++)
        {
            float x = padLeft + (i * stepX);
            float yP = padTop + chartH - ((float)displayData[i].PromptTokens / maxTokens * chartH);
            float yC = padTop + chartH - ((float)displayData[i].CompletionTokens / maxTokens * chartH);

            promptPts[i] = new PointF(x, Math.Max(padTop, Math.Min(padTop + chartH, yP)));
            compPts[i] = new PointF(x, Math.Max(padTop, Math.Min(padTop + chartH, yC)));
        }

        // 1. Draw Prompt Wave (Neon Purple Area + Glowing Line)
        DrawGradientArea(g, promptPts, padTop + chartH, PromptColor, 70);
        // 2. Draw Completion Wave (Electric Cyan Area + Glowing Line)
        DrawGradientArea(g, compPts, padTop + chartH, CompletionColor, 75);

        // Draw Date Labels on X Axis
        using (var dateFont = new Font("Segoe UI", 7.5F))
        using (var dateBrush = new SolidBrush(Color.FromArgb(148, 163, 184)))
        {
            int labelStep = Math.Max(1, displayData.Count / 6);
            for (int i = 0; i < displayData.Count; i += labelStep)
            {
                string dtStr = displayData[i].Date.ToString("dd MMM");
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

        // Glow pen
        using (var glowPen = new Pen(Color.FromArgb(40, baseColor), 5f))
        {
            g.DrawCurve(glowPen, pts, 0.45f);
        }

        // Sharp curve line
        using (var linePen = new Pen(baseColor, 2.2f))
        {
            g.DrawCurve(linePen, pts, 0.45f);
        }
    }

    private static List<DayTokenPoint> GenerateMockData()
    {
        var list = new List<DayTokenPoint>();
        var now = DateTime.UtcNow.Date;
        for (int i = 14; i >= 0; i--)
        {
            var dt = now.AddDays(-i);
            list.Add(new DayTokenPoint
            {
                Date = dt,
                PromptTokens = 1200 + (long)(Math.Sin(i * 0.8) * 800 + 800),
                CompletionTokens = 800 + (long)(Math.Cos(i * 0.9) * 600 + 600)
            });
        }
        return list;
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
/// AI Model & Sağlayıcı Maliyet Dağılımını gösteren modern Donut (Simit) Pasta Grafiği
/// </summary>
public sealed class AiModelCostDonutChart : Control
{
    public sealed class ModelSlice
    {
        public string ModelName { get; set; } = string.Empty;
        public decimal CostUsd { get; set; }
        public Color SliceColor { get; set; }
    }

    private List<ModelSlice> _slices = [];
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

    public void SetData(IEnumerable<KeyValuePair<string, decimal>> data)
    {
        _slices.Clear();
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

        if (_slices.Count == 0)
        {
            // Default elegant representation
            _slices =
            [
                new ModelSlice { ModelName = "gpt-4o", CostUsd = 1.65m, SliceColor = Palette[0] },
                new ModelSlice { ModelName = "deepseek-chat", CostUsd = 0.85m, SliceColor = Palette[1] },
                new ModelSlice { ModelName = "claude-3.5-sonnet", CostUsd = 0.45m, SliceColor = Palette[2] }
            ];
            _totalCost = _slices.Sum(x => x.CostUsd);
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

        // Draw slices
        float startAngle = -90f;
        foreach (var slice in _slices)
        {
            float sweepAngle = _totalCost > 0 ? (float)(slice.CostUsd / _totalCost) * 360f : 0f;
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

        // Center Total text inside Donut
        string totalStr = $"${_totalCost:F2}";
        using (var fCenter = new Font("Segoe UI", 11F, FontStyle.Bold))
        using (var fSub = new Font("Segoe UI", 7.5F))
        using (var bCenter = new SolidBrush(Color.White))
        using (var bSub = new SolidBrush(Color.FromArgb(148, 163, 184)))
        {
            var szTotal = g.MeasureString(totalStr, fCenter);
            var szSub = g.MeasureString("Toplam", fSub);

            float cx = donutX + (donutSize / 2);
            float cy = donutY + (donutSize / 2);

            g.DrawString(totalStr, fCenter, bCenter, cx - (szTotal.Width / 2), cy - 11);
            g.DrawString("Toplam", fSub, bSub, cx - (szSub.Width / 2), cy + 7);
        }

        // Legend on right side
        float legendX = donutX + donutSize + 20;
        float legendY = 48;
        using var legFont = new Font("Segoe UI", 8F);
        using var legFontBold = new Font("Segoe UI", 8F, FontStyle.Bold);

        foreach (var slice in _slices.Take(5))
        {
            float pct = _totalCost > 0 ? (float)(slice.CostUsd / _totalCost * 100m) : 0f;

            using (var dotBrush = new SolidBrush(slice.SliceColor))
            {
                g.FillEllipse(dotBrush, legendX, legendY + 3, 10, 10);
            }

            using (var nameBrush = new SolidBrush(Color.FromArgb(226, 232, 240)))
            {
                string displayName = slice.ModelName.Length > 16 ? slice.ModelName[..14] + ".." : slice.ModelName;
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
/// Modül Bazlı Kullanım ve Maliyet Çubuk Grafiği (Horizontal Progress Bars)
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

    private List<ModuleUsageItem> _items = [];

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

        if (_items.Count == 0)
        {
            _items =
            [
                new ModuleUsageItem { ModuleName = "Pazar Araştırması", CostUsd = 1.45m, RequestCount = 24, BarColor = BarColors[0] },
                new ModuleUsageItem { ModuleName = "SEO Başlık & Etiket", CostUsd = 0.85m, RequestCount = 18, BarColor = BarColors[1] },
                new ModuleUsageItem { ModuleName = "Ürün Açıklama Stüdyosu", CostUsd = 0.65m, RequestCount = 12, BarColor = BarColors[2] }
            ];
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

        // Header
        using (var fontTitle = new Font("Segoe UI", 9.5F, FontStyle.Bold))
        using (var brushTitle = new SolidBrush(Color.White))
        {
            g.DrawString("Modül Bazlı Kullanım & Harcama Oranları", fontTitle, brushTitle, 16, 10);
        }

        decimal maxCost = Math.Max(0.01m, _items.Count > 0 ? _items.Max(x => x.CostUsd) : 1m);

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

            // Module name and cost
            g.DrawString(item.ModuleName, nameFont, textBrush, x, y);
            string costStr = $"${item.CostUsd:F2} ({item.RequestCount} çağrı)";
            var szCost = g.MeasureString(costStr, valFont);
            g.DrawString(costStr, valFont, subBrush, x + colW - szCost.Width - 16, y);

            // Bar background
            float barW = colW - 20;
            float barH = 10;
            float barY = y + 20;

            using (var bgPath = RoundedRect(new Rectangle((int)x, (int)barY, (int)barW, (int)barH), 4))
            {
                using var bgBrush = new SolidBrush(Color.FromArgb(31, 41, 55));
                g.FillPath(bgBrush, bgPath);
            }

            // Fill Bar with gradient
            float fillRatio = (float)(item.CostUsd / maxCost);
            float fillW = Math.Max(8, barW * fillRatio);

            using (var fillPath = RoundedRect(new Rectangle((int)x, (int)barY, (int)fillW, (int)barH), 4))
            {
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
