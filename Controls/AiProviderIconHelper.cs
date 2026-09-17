namespace SimilarProductsWinForms.Controls;

using System;
using System.Drawing;
using System.Drawing.Drawing2D;

/// <summary>
/// Tüm Windows ve VDS ortamlarında font glif eksikliği (tofu []) yaşamadan
/// %100 jilet gibi net ve renkli AI sağlayıcı rozetleri çizen yardımcı sınıf.
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

    private static void DrawGeminiIcon(Graphics g, float s, float half)
    {
        // Google Gemini: 4 Köşeli Parlayan Zeka Yıldızı (Sparkle)
        var rect = new RectangleF(1, 1, s - 2, s - 2);
        using var bgBrush = new SolidBrush(Color.FromArgb(24, 38, 70));
        g.FillEllipse(bgBrush, rect);

        using var borderPen = new Pen(Color.FromArgb(56, 189, 248), 1.2f);
        g.DrawEllipse(borderPen, rect);

        using var path = new GraphicsPath();
        float pad = s * 0.22f;
        float midX = half;
        float midY = half;

        // 4 Köşeli Yıldız Eğrisi
        PointF top = new(midX, pad);
        PointF right = new(s - pad, midY);
        PointF bottom = new(midX, s - pad);
        PointF left = new(pad, midY);

        PointF cTR = new(midX + (s * 0.12f), midY - (s * 0.12f));
        PointF cBR = new(midX + (s * 0.12f), midY + (s * 0.12f));
        PointF cBL = new(midX - (s * 0.12f), midY + (s * 0.12f));
        PointF cTL = new(midX - (s * 0.12f), midY - (s * 0.12f));

        path.AddBezier(top, cTR, cTR, right);
        path.AddBezier(right, cBR, cBR, bottom);
        path.AddBezier(bottom, cBL, cBL, left);
        path.AddBezier(left, cTL, cTL, top);
        path.CloseFigure();

        using var starBrush = new LinearGradientBrush(
            new PointF(pad, pad),
            new PointF(s - pad, s - pad),
            Color.FromArgb(56, 189, 248), // Cyan
            Color.FromArgb(96, 165, 250)); // Bright Blue
        g.FillPath(starBrush, path);
    }

    private static void DrawOpenAiIcon(Graphics g, float s, float half)
    {
        // OpenAI: Zümrüt Yeşili Rozet & Spiral Çember
        var rect = new RectangleF(1, 1, s - 2, s - 2);
        using var bgBrush = new SolidBrush(Color.FromArgb(16, 50, 40));
        g.FillEllipse(bgBrush, rect);

        using var borderPen = new Pen(Color.FromArgb(52, 211, 153), 1.2f);
        g.DrawEllipse(borderPen, rect);

        // OpenAI Çiçek Rozeti Formu
        using var pen = new Pen(Color.FromArgb(52, 211, 153), 1.5f);
        pen.StartCap = LineCap.Round;
        pen.EndCap = LineCap.Round;

        float r = s * 0.24f;
        for (int i = 0; i < 6; i++)
        {
            double angle = i * Math.PI / 3.0;
            float x1 = half + (float)(Math.Cos(angle) * (r * 0.3f));
            float y1 = half + (float)(Math.Sin(angle) * (r * 0.3f));
            float x2 = half + (float)(Math.Cos(angle + 0.9) * r);
            float y2 = half + (float)(Math.Sin(angle + 0.9) * r);
            g.DrawLine(pen, x1, y1, x2, y2);
        }

        using var centerBrush = new SolidBrush(Color.FromArgb(52, 211, 153));
        g.FillEllipse(centerBrush, half - 2f, half - 2f, 4f, 4f);
    }

    private static void DrawDeepSeekIcon(Graphics g, float s, float half)
    {
        // DeepSeek: Derin Mor / İndigo Zeka Düğümü
        var rect = new RectangleF(1, 1, s - 2, s - 2);
        using var bgBrush = new SolidBrush(Color.FromArgb(35, 25, 60));
        g.FillEllipse(bgBrush, rect);

        using var borderPen = new Pen(Color.FromArgb(167, 139, 250), 1.2f);
        g.DrawEllipse(borderPen, rect);

        // İki iç içe elips / zeka düğümü
        using var nodePen = new Pen(Color.FromArgb(192, 132, 252), 1.5f);
        g.DrawEllipse(nodePen, half - 4.5f, half - 4.5f, 9f, 9f);

        using var dotBrush = new SolidBrush(Color.White);
        g.FillEllipse(dotBrush, half - 2f, half - 2f, 4f, 4f);
    }

    private static void DrawClaudeIcon(Graphics g, float s, float half)
    {
        // Anthropic Claude: Sıcak Kehribar / Terracotta Rozet
        var rect = new RectangleF(1, 1, s - 2, s - 2);
        using var bgBrush = new SolidBrush(Color.FromArgb(50, 30, 18));
        g.FillEllipse(bgBrush, rect);

        using var borderPen = new Pen(Color.FromArgb(251, 146, 60), 1.2f);
        g.DrawEllipse(borderPen, rect);

        // Claude Fener / Güneş Işınları
        using var starBrush = new SolidBrush(Color.FromArgb(251, 146, 60));
        float r = s * 0.25f;
        PointF[] pts = [
            new(half, half - r),
            new(half + (r * 0.35f), half - (r * 0.35f)),
            new(half + r, half),
            new(half + (r * 0.35f), half + (r * 0.35f)),
            new(half, half + r),
            new(half - (r * 0.35f), half + (r * 0.35f)),
            new(half - r, half),
            new(half - (r * 0.35f), half - (r * 0.35f))
        ];
        g.FillPolygon(starBrush, pts);
    }

    private static void DrawGrokIcon(Graphics g, float s, float half)
    {
        // xAI Grok: Minimalist Siber Beyaz 'X' Rozeti
        var rect = new RectangleF(1, 1, s - 2, s - 2);
        using var bgBrush = new SolidBrush(Color.FromArgb(20, 24, 32));
        g.FillEllipse(bgBrush, rect);

        using var borderPen = new Pen(Color.FromArgb(148, 163, 184), 1.2f);
        g.DrawEllipse(borderPen, rect);

        using var pen = new Pen(Color.FromArgb(241, 245, 249), 1.8f);
        pen.StartCap = LineCap.Round;
        pen.EndCap = LineCap.Round;

        float off = s * 0.25f;
        g.DrawLine(pen, half - off, half - off, half + off, half + off);
        g.DrawLine(pen, half + off, half - off, half - off, half + off);
    }

    private static void DrawOfflineIcon(Graphics g, float s, float half)
    {
        // Offline: Metalik Çelik Grisi Dişli / Çevrimdışı Kalkan Rozeti
        var rect = new RectangleF(1, 1, s - 2, s - 2);
        using var bgBrush = new SolidBrush(Color.FromArgb(25, 30, 42));
        g.FillEllipse(bgBrush, rect);

        using var borderPen = new Pen(Color.FromArgb(100, 116, 139), 1.2f);
        g.DrawEllipse(borderPen, rect);

        using var gearPen = new Pen(Color.FromArgb(148, 163, 184), 1.5f);
        g.DrawEllipse(gearPen, half - 3.5f, half - 3.5f, 7f, 7f);

        for (int i = 0; i < 4; i++)
        {
            double angle = i * Math.PI / 2.0;
            float x1 = half + (float)(Math.Cos(angle) * 3.5f);
            float y1 = half + (float)(Math.Sin(angle) * 3.5f);
            float x2 = half + (float)(Math.Cos(angle) * 5.8f);
            float y2 = half + (float)(Math.Sin(angle) * 5.8f);
            g.DrawLine(gearPen, x1, y1, x2, y2);
        }
    }
}
