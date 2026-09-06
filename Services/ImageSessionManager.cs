namespace SimilarProductsWinForms.Services;

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

/// <summary>
/// Manages active bitmaps, compositing, resizing and session history with zero GDI+ memory leaks.
/// </summary>
internal sealed class ImageSessionManager : IDisposable
{
    private Bitmap? _originalBitmap;
    private Bitmap? _generatedBitmap;
    private Bitmap? _finalDisplayBitmap;
    private readonly List<Bitmap> _sessionHistory = [];

    public Bitmap? OriginalBitmap => _originalBitmap;
    public Bitmap? GeneratedBitmap => _generatedBitmap;
    public Bitmap? FinalDisplayBitmap => _finalDisplayBitmap;
    public IReadOnlyList<Bitmap> SessionHistory => _sessionHistory;

    public void SetOriginalImage(Bitmap? bitmap)
    {
        _originalBitmap?.Dispose();
        _originalBitmap = bitmap != null ? new Bitmap(bitmap) : null;
    }

    public void SetGeneratedImage(Bitmap? bitmap)
    {
        _generatedBitmap?.Dispose();
        _generatedBitmap = bitmap != null ? new Bitmap(bitmap) : null;

        if (_generatedBitmap != null)
        {
            // Add thumbnail/clone to session history (capped at 10 items)
            if (_sessionHistory.Count >= 10)
            {
                _sessionHistory[0].Dispose();
                _sessionHistory.RemoveAt(0);
            }
            _sessionHistory.Add(new Bitmap(_generatedBitmap));
        }
    }

    public void SetFinalDisplayImage(Bitmap? bitmap)
    {
        _finalDisplayBitmap?.Dispose();
        _finalDisplayBitmap = bitmap != null ? new Bitmap(bitmap) : null;
    }

    /// <summary>
    /// Resizes or crops the image to the specified aspect ratio and resolution.
    /// </summary>
    public Bitmap PrepareOutputImage(Bitmap source, string formatOption)
    {
        int targetW = source.Width;
        int targetH = source.Height;

        if (formatOption.Contains("1:1", StringComparison.OrdinalIgnoreCase))
        {
            targetW = 2000;
            targetH = 2000;
        }
        else if (formatOption.Contains("4:3", StringComparison.OrdinalIgnoreCase))
        {
            targetW = 2000;
            targetH = 1500;
        }
        else
        {
            return new Bitmap(source);
        }

        var result = new Bitmap(targetW, targetH, PixelFormat.Format32bppArgb);
        using var g = Graphics.FromImage(result);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.InterpolationMode = InterpolationMode.HighQualityBicubic;
        g.PixelOffsetMode = PixelOffsetMode.HighQuality;

        // Clear background with crisp pure white for commercial Etsy listing
        g.Clear(Color.White);

        // Aspect fit centered
        float scale = Math.Min((float)targetW / source.Width, (float)targetH / source.Height);
        int destW = (int)(source.Width * scale);
        int destH = (int)(source.Height * scale);
        int destX = (targetW - destW) / 2;
        int destY = (targetH - destH) / 2;

        g.DrawImage(source, destX, destY, destW, destH);
        return result;
    }

    public void Dispose()
    {
        _originalBitmap?.Dispose();
        _originalBitmap = null;

        _generatedBitmap?.Dispose();
        _generatedBitmap = null;

        _finalDisplayBitmap?.Dispose();
        _finalDisplayBitmap = null;

        foreach (var bmp in _sessionHistory)
        {
            bmp.Dispose();
        }
        _sessionHistory.Clear();
    }
}
