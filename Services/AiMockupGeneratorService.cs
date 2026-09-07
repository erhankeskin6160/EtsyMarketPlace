namespace SimilarProductsWinForms.Services;

using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

public sealed record MockupTemplate(string Id, string Name, string Icon, string PromptTemplate);

public sealed class AiMockupGeneratorService
{
    public static readonly IReadOnlyList<MockupTemplate> Templates =
    [
        new MockupTemplate("rustic_wood", "Ahşap Rustic Masa", "🪵", "Professional product photography placed on a warm rustic wooden tabletop, soft natural sunlight pouring from a side window, subtle bokeh, 8k resolution, photorealistic studio lighting"),
        new MockupTemplate("nordic_home", "Modern İskandinav Ev", "🏡", "Professional product photography in a modern minimalist Nordic living room, clean Scandinavian shelf, soft interior daylight, aesthetic home decor atmosphere"),
        new MockupTemplate("marble_podium", "Lüks Mermer Podyum", "🏛️", "Luxury commercial product photography placed on a smooth white marble podium, studio spotlighting, soft realistic shadows, minimal clean aesthetic"),
        new MockupTemplate("boho_botanical", "Boho & Botanik Yaprak", "🌿", "Boho botanical product photography, natural stone texture, green palm leaf shadows, warm sunlight, organic earthy aesthetic"),
        new MockupTemplate("gift_holiday", "Hediye & Sezonluk Tema", "🎁", "Festive holiday gift product photography, cozy warm ambient background, soft fairy lights, luxury gift presentation")
    ];

    public static Bitmap RemoveBackgroundSimple(Bitmap source)
    {
        var result = new Bitmap(source.Width, source.Height, PixelFormat.Format32bppArgb);
        using var g = Graphics.FromImage(result);
        g.DrawImage(source, 0, 0, source.Width, source.Height);

        // Simple thresholding mask for demo/fallback
        Color cornerColor = source.GetPixel(0, 0);
        result.MakeTransparent(cornerColor);
        return result;
    }

    public static Bitmap CompositeProductOnBackground(Bitmap productNoBg, Bitmap background)
    {
        var canvas = new Bitmap(background.Width, background.Height, PixelFormat.Format32bppArgb);
        using var g = Graphics.FromImage(canvas);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.InterpolationMode = InterpolationMode.HighQualityBicubic;

        // Draw background
        g.DrawImage(background, 0, 0, background.Width, background.Height);

        // Scale product to fit center ~60%
        int targetWidth = (int)(background.Width * 0.6);
        int targetHeight = (int)(background.Height * 0.6);
        float ratio = Math.Min((float)targetWidth / productNoBg.Width, (float)targetHeight / productNoBg.Height);
        int finalW = (int)(productNoBg.Width * ratio);
        int finalH = (int)(productNoBg.Height * ratio);

        int posX = (background.Width - finalW) / 2;
        int posY = (background.Height - finalH) / 2 + (int)(background.Height * 0.05);

        // Soft drop shadow
        using (var shadowBrush = new SolidBrush(Color.FromArgb(40, 0, 0, 0)))
        {
            g.FillEllipse(shadowBrush, posX + 10, posY + finalH - 15, finalW - 20, 25);
        }

        g.DrawImage(productNoBg, posX, posY, finalW, finalH);
        return canvas;
    }

    public static Bitmap CreateProceduralTemplateBackground(string templateId, int width = 1024, int height = 1024)
    {
        var bmp = new Bitmap(width, height, PixelFormat.Format32bppArgb);
        using var g = Graphics.FromImage(bmp);
        g.SmoothingMode = SmoothingMode.AntiAlias;

        switch (templateId)
        {
            case "rustic_wood":
                using (var brush = new LinearGradientBrush(new Rectangle(0, 0, width, height), Color.FromArgb(45, 30, 20), Color.FromArgb(100, 70, 45), 45f))
                    g.FillRectangle(brush, 0, 0, width, height);
                using (var linePen = new Pen(Color.FromArgb(30, 255, 255, 255), 2))
                {
                    for (int y = 100; y < height; y += 120) g.DrawLine(linePen, 0, y, width, y + 40);
                }
                break;

            case "nordic_home":
                using (var brush = new LinearGradientBrush(new Rectangle(0, 0, width, height), Color.FromArgb(240, 242, 245), Color.FromArgb(210, 215, 222), 90f))
                    g.FillRectangle(brush, 0, 0, width, height);
                using (var shelfBrush = new SolidBrush(Color.FromArgb(180, 160, 140)))
                    g.FillRectangle(shelfBrush, 0, (int)(height * 0.7), width, 24);
                break;

            case "marble_podium":
                using (var brush = new LinearGradientBrush(new Rectangle(0, 0, width, height), Color.FromArgb(245, 245, 250), Color.FromArgb(220, 225, 235), 135f))
                    g.FillRectangle(brush, 0, 0, width, height);
                using (var podiumBrush = new SolidBrush(Color.FromArgb(255, 255, 255)))
                    g.FillEllipse(podiumBrush, (int)(width * 0.15), (int)(height * 0.6), (int)(width * 0.7), (int)(height * 0.35));
                break;

            case "boho_botanical":
                using (var brush = new LinearGradientBrush(new Rectangle(0, 0, width, height), Color.FromArgb(235, 230, 220), Color.FromArgb(205, 195, 180), 30f))
                    g.FillRectangle(brush, 0, 0, width, height);
                using (var leafBrush = new SolidBrush(Color.FromArgb(25, 34, 139, 34)))
                    g.FillEllipse(leafBrush, (int)(width * 0.6), -50, 450, 550);
                break;

            default: // gift
                using (var brush = new LinearGradientBrush(new Rectangle(0, 0, width, height), Color.FromArgb(30, 40, 60), Color.FromArgb(15, 20, 35), 90f))
                    g.FillRectangle(brush, 0, 0, width, height);
                break;
        }

        return bmp;
    }
}
