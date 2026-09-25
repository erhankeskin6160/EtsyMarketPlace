namespace SimilarProductsWinForms.Controls;

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Windows.Forms;

/// <summary>
/// Kargo sağlayıcıları (Aras Global, ShipEntegra, Navlungo, Shiptomore)
/// ve alt taşıyıcılar (Widect, THY, FedEx, UPS, TNT, DHL) için kurumsal logo sağlayıcı ve önbelleği.
/// </summary>
public static class ShippingLogoHelper
{
    private static readonly Dictionary<string, Image> _carrierLogoCache = new(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<string, Image> _providerLogoCache = new(StringComparer.OrdinalIgnoreCase);

    private static Image? _arasLogo;
    private static Image? _shipEntegraLogo;
    private static Image? _navlungoLogo;
    private static Image? _shiptomoreLogo;
    private static Image? _widectLogo;
    private static Image? _fedexLogo;
    private static Image? _upsLogo;

    static ShippingLogoHelper()
    {
        _arasLogo = LoadLogoSafely("aras_global.png");
        _shipEntegraLogo = LoadLogoSafely("shipentegra.jpg") ?? LoadLogoSafely("shipentegra.png");
        _navlungoLogo = LoadLogoSafely("navlungo.png");
        _shiptomoreLogo = LoadLogoSafely("shiptomore.png");
        _widectLogo = LoadLogoSafely("widect.png");
        _fedexLogo = LoadLogoSafely("fedex.png");
        _upsLogo = LoadLogoSafely("ups.png");
    }

    /// <summary>
    /// Ana sağlayıcı / aracı firma logosunu (Aras, ShipEntegra, Navlungo, Shiptomore) döner.
    /// </summary>
    public static Image GetProviderLogo(string provider)
    {
        string p = provider?.Trim().ToLowerInvariant() ?? string.Empty;
        if (_providerLogoCache.TryGetValue(p, out var cached)) return cached;

        Image? img = null;
        if (p.Contains("aras"))
        {
            img = _arasLogo ?? CreateFallbackProviderLogo("Aras Global");
        }
        else if (p.Contains("shipentegra"))
        {
            img = _shipEntegraLogo ?? CreateFallbackProviderLogo("ShipEntegra");
        }
        else if (p.Contains("navlungo"))
        {
            img = _navlungoLogo ?? CreateFallbackProviderLogo("Navlungo");
        }
        else if (p.Contains("shiptomore"))
        {
            img = _shiptomoreLogo ?? CreateFallbackProviderLogo("Shiptomore");
        }
        else
        {
            img = CreateFallbackProviderLogo(provider ?? "Kargo");
        }

        _providerLogoCache[p] = img;
        return img;
    }

    /// <summary>
    /// Alt taşıyıcı firmanın (Widect, THY, FedEx, UPS, TNT vb.) yüksek kaliteli logosunu döner.
    /// </summary>
    public static Image GetCarrierLogo(string subCarrier, string serviceName = "", string note = "")
    {
        string key = $"{subCarrier}_{serviceName}_{note}".ToLowerInvariant();
        if (_carrierLogoCache.TryGetValue(key, out var cached)) return cached;

        // 1. FedEx
        if (key.Contains("fedex") || key.Contains("smart"))
        {
            _fedexLogo ??= LoadLogoSafely("fedex.png");
            if (_fedexLogo != null)
            {
                _carrierLogoCache[key] = _fedexLogo;
                return _fedexLogo;
            }
        }

        // 2. Widect
        if (key.Contains("widect") || key.Contains("eco express"))
        {
            _widectLogo ??= LoadLogoSafely("widect.png");
            if (_widectLogo != null)
            {
                _carrierLogoCache[key] = _widectLogo;
                return _widectLogo;
            }
        }

        // 3. UPS
        if (key.Contains("ups"))
        {
            _upsLogo ??= LoadLogoSafely("ups.png");
            if (_upsLogo != null)
            {
                _carrierLogoCache[key] = _upsLogo;
                return _upsLogo;
            }
        }

        // 4. THY (Turkish Cargo)
        if (key.Contains("thy") || key.Contains("turkish"))
        {
            var thyBmp = CreateThyLogo();
            _carrierLogoCache[key] = thyBmp;
            return thyBmp;
        }

        // Vektörel Fallback Logo
        var bmp = new Bitmap(68, 36);
        using var g = Graphics.FromImage(bmp);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

        if (key.Contains("fedex") || key.Contains("smart"))
        {
            g.Clear(Color.White);
            using var borderPen = new Pen(Color.FromArgb(203, 213, 225), 1f);
            g.DrawRectangle(borderPen, 0, 0, bmp.Width - 1, bmp.Height - 1);
            using var font = new Font("Segoe UI Black", 10.5F, FontStyle.Bold);
            using var purpleBrush = new SolidBrush(Color.FromArgb(77, 20, 140));
            using var orangeBrush = new SolidBrush(Color.FromArgb(255, 102, 0));
            g.DrawString("Fed", font, purpleBrush, new PointF(6, 7));
            g.DrawString("Ex", font, orangeBrush, new PointF(35, 7));
        }
        else if (key.Contains("ups"))
        {
            g.Clear(Color.FromArgb(53, 26, 12));
            using var shieldBrush = new SolidBrush(Color.FromArgb(255, 181, 0));
            Point[] shield = { new(34, 2), new(62, 7), new(54, 28), new(34, 34), new(14, 28), new(6, 7) };
            g.DrawPolygon(new Pen(Color.FromArgb(255, 181, 0), 1.5f), shield);
            using var font = new Font("Segoe UI Black", 10F, FontStyle.Bold);
            using var textBrush = new SolidBrush(Color.FromArgb(255, 181, 0));
            g.DrawString("ups", font, textBrush, new PointF(18, 8));
        }
        else if (key.Contains("widect"))
        {
            g.Clear(Color.FromArgb(15, 23, 42));
            using var pen = new Pen(Color.FromArgb(14, 165, 233), 1f);
            g.DrawRectangle(pen, 0, 0, bmp.Width - 1, bmp.Height - 1);
            using var cyanBrush = new SolidBrush(Color.FromArgb(56, 189, 248));
            using var orangeBrush = new SolidBrush(Color.FromArgb(251, 146, 60));
            Point[] wing = { new(6, 8), new(20, 18), new(6, 28) };
            g.FillPolygon(cyanBrush, wing);
            using var font = new Font("Segoe UI Black", 9F, FontStyle.Bold);
            g.DrawString("WID", font, cyanBrush, new PointF(22, 10));
            g.DrawString("ECT", font, orangeBrush, new PointF(42, 10));
        }
        else if (key.Contains("tnt"))
        {
            g.Clear(Color.FromArgb(234, 88, 12));
            using var font = new Font("Segoe UI Black", 10.5F, FontStyle.Bold);
            using var brush = new SolidBrush(Color.White);
            g.DrawString("TNT", font, brush, new PointF(15, 7));
        }
        else if (key.Contains("dhl"))
        {
            g.Clear(Color.FromArgb(254, 204, 0)); // DHL Yellow
            using var font = new Font("Segoe UI Black", 11F, FontStyle.Bold | FontStyle.Italic);
            using var brush = new SolidBrush(Color.FromArgb(212, 5, 17)); // DHL Red
            g.DrawString("DHL", font, brush, new PointF(12, 6));
        }
        else
        {
            g.Clear(Color.FromArgb(6, 78, 59));
            using var pen = new Pen(Color.FromArgb(16, 185, 129), 1f);
            g.DrawRectangle(pen, 0, 0, bmp.Width - 1, bmp.Height - 1);
            using var font = new Font("Segoe UI Black", 9F, FontStyle.Bold);
            using var brush = new SolidBrush(Color.FromArgb(52, 211, 153));
            g.DrawString("⚡EKO", font, brush, new PointF(11, 9));
        }

        _carrierLogoCache[key] = bmp;
        return bmp;
    }

    private static Image CreateThyLogo()
    {
        var bmp = new Bitmap(68, 36);
        using var g = Graphics.FromImage(bmp);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.Clear(Color.FromArgb(185, 28, 28)); // Turkish Red
        using var pen = new Pen(Color.FromArgb(239, 68, 68), 1f);
        g.DrawRectangle(pen, 0, 0, bmp.Width - 1, bmp.Height - 1);

        // THY Kuş / Hilal simgesi
        using var whiteBrush = new SolidBrush(Color.White);
        g.FillEllipse(whiteBrush, 6, 6, 24, 24);
        using var redBrush = new SolidBrush(Color.FromArgb(185, 28, 28));
        g.FillEllipse(redBrush, 12, 6, 20, 20);

        using var f = new Font("Segoe UI Black", 9F, FontStyle.Bold);
        g.DrawString("THY", f, whiteBrush, new PointF(33, 9));
        return bmp;
    }

    public static Image? LoadLogoSafely(string fileName)
    {
        try
        {
            string[] probePaths =
            {
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "Shipping", fileName),
                Path.Combine(Application.StartupPath, "Assets", "Shipping", fileName),
                Path.Combine(Directory.GetCurrentDirectory(), "Assets", "Shipping", fileName),
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "Assets", "Shipping", fileName),
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..", "Assets", "Shipping", fileName)
            };

            foreach (var p in probePaths)
            {
                if (File.Exists(p))
                {
                    byte[] bytes = File.ReadAllBytes(p);
                    if (bytes.Length > 0)
                    {
                        var ms = new MemoryStream(bytes);
                        return Image.FromStream(ms);
                    }
                }
            }
        }
        catch { }

        return null;
    }

    public static Image CreateFallbackProviderLogo(string provider)
    {
        var bmp = new Bitmap(120, 60);
        using var g = Graphics.FromImage(bmp);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.Clear(Color.White);

        if (provider.Contains("aras", StringComparison.OrdinalIgnoreCase))
        {
            using var brush = new SolidBrush(Color.FromArgb(220, 38, 38));
            using var f = new Font("Segoe UI Black", 12F, FontStyle.Bold);
            g.DrawString("aras", f, brush, new PointF(10, 8));
            using var fSub = new Font("Segoe UI", 8F, FontStyle.Bold);
            using var gBrush = new SolidBrush(Color.FromArgb(71, 85, 105));
            g.DrawString("global", fSub, gBrush, new PointF(12, 32));
        }
        else if (provider.Contains("shipentegra", StringComparison.OrdinalIgnoreCase))
        {
            using var f = new Font("Segoe UI Black", 10F, FontStyle.Bold);
            using var b = new SolidBrush(Color.FromArgb(30, 41, 59));
            g.DrawString("ShipEntegra", f, b, new PointF(8, 18));
        }
        else if (provider.Contains("navlungo", StringComparison.OrdinalIgnoreCase))
        {
            using var f = new Font("Segoe UI Black", 11F, FontStyle.Bold);
            using var b = new SolidBrush(Color.FromArgb(37, 99, 235));
            g.DrawString("Navlungo", f, b, new PointF(10, 18));
        }
        else
        {
            using var f = new Font("Segoe UI Black", 9.5F, FontStyle.Bold);
            using var b = new SolidBrush(Color.FromArgb(5, 150, 105));
            g.DrawString("Shiptomore", f, b, new PointF(8, 18));
        }

        return bmp;
    }

    public static void DrawImagePreserveAspect(Graphics g, Image? img, Rectangle destRect)
    {
        if (img == null || destRect.Width <= 0 || destRect.Height <= 0) return;

        float imgAspect = (float)img.Width / Math.Max(1, img.Height);
        float destAspect = (float)destRect.Width / Math.Max(1, destRect.Height);

        int drawW, drawH, drawX, drawY;
        if (imgAspect > destAspect)
        {
            drawW = destRect.Width;
            drawH = Math.Max(1, (int)(destRect.Width / imgAspect));
            drawX = destRect.X;
            drawY = destRect.Y + (destRect.Height - drawH) / 2;
        }
        else
        {
            drawH = destRect.Height;
            drawW = Math.Max(1, (int)(destRect.Height * imgAspect));
            drawX = destRect.X + (destRect.Width - drawW) / 2;
            drawY = destRect.Y;
        }

        g.InterpolationMode = InterpolationMode.HighQualityBicubic;
        g.PixelOffsetMode = PixelOffsetMode.HighQuality;
        g.DrawImage(img, new Rectangle(drawX, drawY, drawW, drawH));
    }
}

