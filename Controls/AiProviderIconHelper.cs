namespace SimilarProductsWinForms.Controls;

using System;
using System.Drawing;
using System.Drawing.Drawing2D;

/// <summary>
/// Tüm Windows ve VDS ortamlarında harici font kurulumu gerektirmeden,
/// FontAwesome & resmi AI marka vektör geometrilerini %100 jilet gibi netlikte
/// ve anti-alias pürüzsüzlüğünde çizen yüksek çözünürlüklü ikon motoru.
/// </summary>
internal static class AiProviderIconHelper
{
    public static Bitmap GetProviderIcon(string providerKey, int size = 20)
    {
        var bmp = new Bitmap(size, size, System.Drawing.Imaging.PixelFormat.Format32bppPArgb);
        using var g = Graphics.FromImage(bmp);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.PixelOffsetMode = PixelOffsetMode.HighQuality;
        g.InterpolationMode = InterpolationMode.HighQualityBicubic;
        g.Clear(Color.Transparent);

        float s = size;
        float half = s / 2f;

        switch (providerKey.Trim().ToLowerInvariant())
        {
            case "gemini":
            case "google gemini":
                DrawGeminiIcon(g, s, half);
                break;

            case "openai":
            case "openai (gpt)":
            case "chatgpt":
                DrawOpenAiIcon(g, s, half);
                break;

            case "deepseek":
                DrawDeepSeekIcon(g, s, half);
                break;

            case "claude":
            case "anthropic":
                DrawClaudeIcon(g, s, half);
                break;

            case "grok":
            case "xai":
            case "xai grok":
                DrawGrokIcon(g, s, half);
                break;

            default:
                DrawOfflineIcon(g, s, half);
                break;
        }

        return bmp;
    }

    private static void DrawBadgeBackground(Graphics g, float s, Color bgColor, Color borderColor)
    {
        var rect = new RectangleF(0.85f, 0.85f, s - 1.7f, s - 1.7f);
        using var bgBrush = new SolidBrush(bgColor);
        g.FillEllipse(bgBrush, rect);

        using var borderPen = new Pen(borderColor, 1.15f);
        g.DrawEllipse(borderPen, rect);
    }

    /// <summary>
    /// OpenAI / ChatGPT: Resmi FontAwesome (fa-brands fa-chatgpt) 6'lı hekzagonal spiral girdap.
    /// </summary>
    private static void DrawOpenAiIcon(Graphics g, float s, float half)
    {
        // OpenAI koyu zümrüt rozet tabanı
        DrawBadgeBackground(g, s, Color.FromArgb(8, 44, 34), Color.FromArgb(16, 163, 127));

        var state = g.Save();
        g.TranslateTransform(half, half);

        float r = s * 0.33f;
        float penWidth = Math.Max(1.4f, s * 0.082f);

        using var pen = new Pen(Color.FromArgb(16, 185, 129), penWidth)
        {
            StartCap = LineCap.Round,
            EndCap = LineCap.Round,
            LineJoin = LineJoin.Round
        };

        // 60 derece aralıklarla 6 adet birbiri içine kilitlenen spiral kol
        for (int i = 0; i < 6; i++)
        {
            using var armPath = new GraphicsPath();
            // Merkezden dışa doğru kıvrılan FontAwesome ChatGPT spiral yayı
            PointF p1 = new(-r * 0.20f, -r * 0.38f);
            PointF p2 = new(r * 0.18f, -r * 0.88f);
            PointF p3 = new(r * 0.68f, -r * 0.85f);
            PointF p4 = new(r * 0.80f, -r * 0.42f);
            PointF p5 = new(r * 0.40f, -r * 0.16f);

            armPath.AddLine(p1, p2);
            armPath.AddBezier(p2, new PointF(r * 0.42f, -r * 0.94f), new PointF(r * 0.62f, -r * 0.92f), p3);
            armPath.AddBezier(p3, new PointF(r * 0.78f, -r * 0.72f), new PointF(r * 0.84f, -r * 0.56f), p4);
            armPath.AddLine(p4, p5);

            g.DrawPath(pen, armPath);
            g.RotateTransform(60f);
        }

        g.Restore(state);
    }

    /// <summary>
    /// Anthropic Claude: Resmi FontAwesome (fa-brands fa-claude) 14 ışınlı yuvarlak starburst.
    /// </summary>
    private static void DrawClaudeIcon(Graphics g, float s, float half)
    {
        // Claude sıcak pişmiş toprak (terracotta / amber) rozet tabanı
        DrawBadgeBackground(g, s, Color.FromArgb(46, 26, 16), Color.FromArgb(245, 158, 11));

        var state = g.Save();
        g.TranslateTransform(half, half);

        float penWidth = Math.Max(1.3f, s * 0.078f);
        using var rayPen = new Pen(Color.FromArgb(251, 191, 36), penWidth)
        {
            StartCap = LineCap.Round,
            EndCap = LineCap.Round
        };

        // 14 Işınlı Claude Güneşi (Her biri 360 / 14 = ~25.71 derece)
        // Ana eksenlerdeki ışınlar ritmik olarak hafifçe daha uzun
        for (int i = 0; i < 14; i++)
        {
            float rInner = s * 0.12f;
            float rOuter = (i % 2 == 0) ? (s * 0.35f) : (s * 0.28f);

            g.DrawLine(rayPen, 0, -rInner, 0, -rOuter);
            g.RotateTransform(360f / 14f);
        }

        // Merkezdeki minik sıcak çekirdek
        using var coreBrush = new SolidBrush(Color.FromArgb(253, 230, 138));
        float coreR = s * 0.08f;
        g.FillEllipse(coreBrush, -coreR, -coreR, coreR * 2, coreR * 2);

        g.Restore(state);
    }

    /// <summary>
    /// Google Gemini: 4 Köşeli parlayan astro-elmas (Sparkle) ve mavi-mor gradyan.
    /// </summary>
    private static void DrawGeminiIcon(Graphics g, float s, float half)
    {
        // Google derin gece mavisi rozet tabanı
        DrawBadgeBackground(g, s, Color.FromArgb(15, 23, 42), Color.FromArgb(56, 189, 248));

        using var path = new GraphicsPath();
        float pad = s * 0.20f;
        float pull = s * 0.09f;

        // 4 Köşeli Keskin Uçlar
        PointF top = new(half, pad);
        PointF right = new(s - pad, half);
        PointF bottom = new(half, s - pad);
        PointF left = new(pad, half);

        // İç bükey hipersikloid kontrol noktaları
        PointF cTR1 = new(half + pull, half - pull);
        PointF cBR1 = new(half + pull, half + pull);
        PointF cBL1 = new(half - pull, half + pull);
        PointF cTL1 = new(half - pull, half - pull);

        path.AddBezier(top, cTR1, cTR1, right);
        path.AddBezier(right, cBR1, cBR1, bottom);
        path.AddBezier(bottom, cBL1, cBL1, left);
        path.AddBezier(left, cTL1, cTL1, top);
        path.CloseFigure();

        // Google Gemini Açık Cyan -> Derin Mor-Mavi Gradyan
        using var starBrush = new LinearGradientBrush(
            new PointF(pad, pad),
            new PointF(s - pad, s - pad),
            Color.FromArgb(56, 189, 248),   // Cyan
            Color.FromArgb(129, 140, 248));  // Indigo/Purple
        g.FillPath(starBrush, path);

        // Merkezdeki parlak zeka ışığı (Core Flare)
        using var flareBrush = new SolidBrush(Color.FromArgb(240, 255, 255, 255));
        float flareR = s * 0.07f;
        g.FillEllipse(flareBrush, half - flareR, half - flareR, flareR * 2, flareR * 2);
    }

    /// <summary>
    /// DeepSeek: Resmi DeepSeek zeka balinası ve yüzgeç vektör amblemi.
    /// </summary>
    private static void DrawDeepSeekIcon(Graphics g, float s, float half)
    {
        // DeepSeek okyanus laciverti rozet tabanı
        DrawBadgeBackground(g, s, Color.FromArgb(15, 23, 42), Color.FromArgb(99, 102, 241));

        using var whalePath = new GraphicsPath();

        // Balina gövdesi ve yüzgeç silüeti (oranlı koordinatlar)
        PointF tail1 = new(s * 0.22f, s * 0.70f);
        PointF tailFluke = new(s * 0.16f, s * 0.60f);
        PointF dorsalBase = new(s * 0.38f, s * 0.44f);
        PointF dorsalTip = new(s * 0.46f, s * 0.24f);
        PointF dorsalBack = new(s * 0.54f, s * 0.38f);
        PointF headTop = new(s * 0.76f, s * 0.40f);
        PointF snout = new(s * 0.82f, s * 0.52f);
        PointF jaw = new(s * 0.74f, s * 0.62f);
        PointF belly = new(s * 0.48f, s * 0.68f);

        whalePath.AddLine(tail1, tailFluke);
        whalePath.AddBezier(tailFluke, new PointF(s * 0.26f, s * 0.54f), new PointF(s * 0.32f, s * 0.48f), dorsalBase);
        whalePath.AddLine(dorsalBase, dorsalTip);
        whalePath.AddLine(dorsalTip, dorsalBack);
        whalePath.AddBezier(dorsalBack, new PointF(s * 0.64f, s * 0.36f), new PointF(s * 0.72f, s * 0.36f), headTop);
        whalePath.AddBezier(headTop, new PointF(s * 0.80f, s * 0.44f), new PointF(s * 0.84f, s * 0.48f), snout);
        whalePath.AddBezier(snout, new PointF(s * 0.80f, s * 0.58f), new PointF(s * 0.76f, s * 0.62f), jaw);
        whalePath.AddBezier(jaw, new PointF(s * 0.62f, s * 0.68f), new PointF(s * 0.54f, s * 0.70f), belly);
        whalePath.AddLine(belly, tail1);
        whalePath.CloseFigure();

        // Elektrik mavisi gradyan dolgu
        using var whaleBrush = new LinearGradientBrush(
            new PointF(s * 0.2f, s * 0.2f),
            new PointF(s * 0.8f, s * 0.8f),
            Color.FromArgb(56, 189, 248),   // Electric Cyan
            Color.FromArgb(99, 102, 241));  // Indigo
        g.FillPath(whaleBrush, whalePath);

        // Balina gözü (beyaz nokta)
        using var eyeBrush = new SolidBrush(Color.White);
        float eyeSize = Math.Max(1.5f, s * 0.08f);
        g.FillEllipse(eyeBrush, s * 0.68f, s * 0.47f, eyeSize, eyeSize);
    }

    /// <summary>
    /// xAI Grok: Resmi geometrik eğik çizgi ve asimetrik 'X' siber amblemi.
    /// </summary>
    private static void DrawGrokIcon(Graphics g, float s, float half)
    {
        // Grok siber uzay siyahı rozet tabanı
        DrawBadgeBackground(g, s, Color.FromArgb(10, 14, 23), Color.FromArgb(148, 163, 184));

        float strokeWidth = Math.Max(1.6f, s * 0.095f);
        using var pen = new Pen(Color.FromArgb(248, 250, 252), strokeWidth)
        {
            StartCap = LineCap.Round,
            EndCap = LineCap.Round
        };

        float pad = s * 0.25f;

        // xAI karakteristik kalın ana köşegen
        g.DrawLine(pen, pad, pad, s - pad, s - pad);

        // İkincil karşı köşegen (asimetrik xAI Grok stili)
        float midOffset = s * 0.06f;
        g.DrawLine(pen, s - pad, pad, half + midOffset, half - midOffset);
        g.DrawLine(pen, half - midOffset, half + midOffset, pad, s - pad);
    }

    /// <summary>
    /// Çevrimdışı / Dahili Motor: Kalkan ve mikroçip rozeti.
    /// </summary>
    private static void DrawOfflineIcon(Graphics g, float s, float half)
    {
        // Koyu arduvaz çelik rozet tabanı
        DrawBadgeBackground(g, s, Color.FromArgb(30, 41, 59), Color.FromArgb(100, 116, 139));

        float strokeWidth = Math.Max(1.4f, s * 0.08f);
        using var pen = new Pen(Color.FromArgb(203, 213, 225), strokeWidth)
        {
            StartCap = LineCap.Round,
            EndCap = LineCap.Round
        };

        // Donanım mikroçip karesi
        float chipPad = s * 0.30f;
        var chipRect = new RectangleF(chipPad, chipPad, s - (chipPad * 2), s - (chipPad * 2));
        using var chipBrush = new SolidBrush(Color.FromArgb(51, 65, 85));
        g.FillRectangle(chipBrush, chipRect);
        g.DrawRectangle(pen, chipRect.X, chipRect.Y, chipRect.Width, chipRect.Height);

        // 4 Bağlantı pini
        g.DrawLine(pen, half, chipPad - (s * 0.08f), half, chipPad);
        g.DrawLine(pen, half, s - chipPad, half, s - chipPad + (s * 0.08f));
        g.DrawLine(pen, chipPad - (s * 0.08f), half, chipPad, half);
        g.DrawLine(pen, s - chipPad, half, s - chipPad + (s * 0.08f), half);

        // Merkez çekirdek noktası
        using var dotBrush = new SolidBrush(Color.FromArgb(56, 189, 248));
        float dotR = s * 0.06f;
        g.FillEllipse(dotBrush, half - dotR, half - dotR, dotR * 2, dotR * 2);
    }
}

