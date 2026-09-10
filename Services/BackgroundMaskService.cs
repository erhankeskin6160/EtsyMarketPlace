namespace SimilarProductsWinForms.Services;

using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// Ürün fotoğrafları için mask oluşturma, PhotoRoom segmentasyonu ve alfa kanalı izolasyon servisi.
/// </summary>
internal static class BackgroundMaskService
{
    /// <summary>
    /// PhotoRoom Segment API'sini kullanarak arka planı kaldırılmış şeffaf PNG çıktısını alır.
    /// Dönen görsel: Ürün opak (alpha=255), arka plan tamamen şeffaf (alpha=0).
    /// </summary>
    public static async Task<(bool Success, Bitmap? TransparentCutout, byte[]? MaskPngBytes, string ErrorMessage)>
        CreateCutoutAndMaskWithPhotoRoomAsync(
            byte[] imageBytes,
            string photoRoomApiKey,
            CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(photoRoomApiKey))
        {
            return (false, null, null, "PhotoRoom API anahtarı girilmedi.");
        }

        var (success, resultImage, error) = await PhotoRoomApiService.EditProductPhotoAsync(
            imageBytes,
            photoRoomApiKey,
            mode: "remove_bg",
            cancellationToken: ct);

        if (!success || resultImage == null)
        {
            return (false, null, null, error);
        }

        // PhotoRoom'un döndürdüğü görselden OpenAI /v1/images/edits için mask üret
        byte[] maskBytes = GenerateOpenAiEditMask(resultImage);

        return (true, resultImage, maskBytes, "Başarılı");
    }

    /// <summary>
    /// OpenAI /v1/images/edits endpoint'i için geçerli PNG mask üretir.
    /// OpenAI şartı: Düzenlenecek kısımlar (arka plan) şeffaf (alpha=0),
    /// korunacak kısımlar (ürün) opak (alpha=255, siyah/beyaz fark etmez).
    /// </summary>
    public static byte[] GenerateOpenAiEditMask(Bitmap cutoutWithAlpha)
    {
        int width = cutoutWithAlpha.Width;
        int height = cutoutWithAlpha.Height;

        using var maskBmp = new Bitmap(width, height, PixelFormat.Format32bppArgb);

        var rect = new Rectangle(0, 0, width, height);
        var srcData = cutoutWithAlpha.LockBits(rect, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
        var dstData = maskBmp.LockBits(rect, ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);

        int totalBytes = Math.Abs(srcData.Stride) * height;
        byte[] srcPixels = new byte[totalBytes];
        byte[] dstPixels = new byte[totalBytes];

        System.Runtime.InteropServices.Marshal.Copy(srcData.Scan0, srcPixels, 0, totalBytes);

        int stride = srcData.Stride;
        for (int y = 0; y < height; y++)
        {
            int rowOffset = y * stride;
            for (int x = 0; x < width; x++)
            {
                int idx = rowOffset + (x * 4);
                byte alpha = srcPixels[idx + 3];

                if (alpha > 30) // Ürün pikseli -> Korunacak (Opak)
                {
                    dstPixels[idx + 0] = 0;   // B
                    dstPixels[idx + 1] = 0;   // G
                    dstPixels[idx + 2] = 0;   // R
                    dstPixels[idx + 3] = 255; // A (opak = korunacak)
                }
                else // Arka plan pikseli -> Düzenlenecek (Şeffaf)
                {
                    dstPixels[idx + 0] = 0;
                    dstPixels[idx + 1] = 0;
                    dstPixels[idx + 2] = 0;
                    dstPixels[idx + 3] = 0;   // A (şeffaf = AI arka plan çizecek)
                }
            }
        }

        System.Runtime.InteropServices.Marshal.Copy(dstPixels, 0, dstData.Scan0, totalBytes);

        cutoutWithAlpha.UnlockBits(srcData);
        maskBmp.UnlockBits(dstData);

        using var ms = new MemoryStream();
        maskBmp.Save(ms, ImageFormat.Png);
        return ms.ToArray();
    }

    /// <summary>
    /// API kullanmadan, beyaz veya düz renkli arka planı olan görseller için
    /// yerel renk toleransı ile şeffaf kesim ve mask oluşturur.
    /// </summary>
    public static (Bitmap TransparentCutout, byte[] MaskBytes) CreateLocalMaskFromThreshold(
        Bitmap original,
        Color targetBgColor,
        int tolerance = 35)
    {
        int width = original.Width;
        int height = original.Height;

        var cutout = new Bitmap(width, height, PixelFormat.Format32bppArgb);
        using var maskBmp = new Bitmap(width, height, PixelFormat.Format32bppArgb);

        int tR = targetBgColor.R;
        int tG = targetBgColor.G;
        int tB = targetBgColor.B;

        var rect = new Rectangle(0, 0, width, height);
        var srcData = original.LockBits(rect, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
        var cutData = cutout.LockBits(rect, ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
        var mskData = maskBmp.LockBits(rect, ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);

        int totalBytes = Math.Abs(srcData.Stride) * height;
        byte[] srcPixels = new byte[totalBytes];
        byte[] cutPixels = new byte[totalBytes];
        byte[] mskPixels = new byte[totalBytes];

        System.Runtime.InteropServices.Marshal.Copy(srcData.Scan0, srcPixels, 0, totalBytes);

        int stride = srcData.Stride;
        for (int y = 0; y < height; y++)
        {
            int rowOffset = y * stride;
            for (int x = 0; x < width; x++)
            {
                int idx = rowOffset + (x * 4);
                byte b = srcPixels[idx + 0];
                byte g = srcPixels[idx + 1];
                byte r = srcPixels[idx + 2];

                int dist = Math.Abs(r - tR) + Math.Abs(g - tG) + Math.Abs(b - tB);

                if (dist <= tolerance * 3)
                {
                    // Arka plan rengine çok yakın -> Şeffaf yap
                    cutPixels[idx + 0] = 0;
                    cutPixels[idx + 1] = 0;
                    cutPixels[idx + 2] = 0;
                    cutPixels[idx + 3] = 0;

                    mskPixels[idx + 0] = 0;
                    mskPixels[idx + 1] = 0;
                    mskPixels[idx + 2] = 0;
                    mskPixels[idx + 3] = 0; // Şeffaf (AI değiştirecek)
                }
                else
                {
                    // Ürün pikseli -> Birebir koru
                    cutPixels[idx + 0] = b;
                    cutPixels[idx + 1] = g;
                    cutPixels[idx + 2] = r;
                    cutPixels[idx + 3] = 255;

                    mskPixels[idx + 0] = 0;
                    mskPixels[idx + 1] = 0;
                    mskPixels[idx + 2] = 0;
                    mskPixels[idx + 3] = 255; // Opak (Korunacak)
                }
            }
        }

        System.Runtime.InteropServices.Marshal.Copy(cutPixels, 0, cutData.Scan0, totalBytes);
        System.Runtime.InteropServices.Marshal.Copy(mskPixels, 0, mskData.Scan0, totalBytes);

        original.UnlockBits(srcData);
        cutout.UnlockBits(cutData);
        maskBmp.UnlockBits(mskData);

        using var ms = new MemoryStream();
        maskBmp.Save(ms, ImageFormat.Png);
        return (cutout, ms.ToArray());
    }

    /// <summary>
    /// Şeffaf arka planlı bir ürünü yeni bir arka plan görseli üzerine ortalayarak yerleştirir.
    /// </summary>
    public static Bitmap CompositeProductOnBackground(
        Bitmap productCutout,
        Bitmap background,
        double productScale = 0.85,
        int offsetY = 20)
    {
        var result = new Bitmap(background.Width, background.Height, PixelFormat.Format32bppArgb);

        using var g = Graphics.FromImage(result);
        g.InterpolationMode = InterpolationMode.HighQualityBicubic;
        g.SmoothingMode = SmoothingMode.HighQuality;
        g.PixelOffsetMode = PixelOffsetMode.HighQuality;

        // Arka planı çiz
        g.DrawImage(background, 0, 0, background.Width, background.Height);

        // Ürün boyutunu hesapla
        int prodW = (int)(background.Width * productScale);
        int prodH = (int)(productCutout.Height * ((double)prodW / productCutout.Width));

        if (prodH > background.Height * 0.9)
        {
            prodH = (int)(background.Height * 0.9);
            prodW = (int)(productCutout.Width * ((double)prodH / productCutout.Height));
        }

        int posX = (background.Width - prodW) / 2;
        int posY = (background.Height - prodH) / 2 + offsetY;

        // Yumuşak temas gölgesi ekle
        using (var shadowBrush = new SolidBrush(Color.FromArgb(50, 0, 0, 0)))
        {
            int shadowW = (int)(prodW * 0.8);
            int shadowH = Math.Max(10, prodH / 12);
            int shadowX = posX + (prodW - shadowW) / 2;
            int shadowY = posY + prodH - (shadowH / 2);
            g.FillEllipse(shadowBrush, shadowX, shadowY, shadowW, shadowH);
        }

        // Ürünü çiz
        g.DrawImage(productCutout, posX, posY, prodW, prodH);

        return result;
    }
}
