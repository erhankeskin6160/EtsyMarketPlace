namespace SimilarProductsWinForms.Controls;

using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

internal sealed class ModernSpinner : Control
{
    private readonly Timer _timer;
    private float _progress;

    public ModernSpinner()
    {
        DoubleBuffered = true;
        ResizeRedraw = true;
        Size = new Size(80, 80);
        BackColor = Color.White;

        _timer = new Timer { Interval = 25 };
        _timer.Tick += (s, e) =>
        {
            _progress += 0.015f;
            if (_progress > 1f) _progress -= 1f;
            Invalidate();
        };
    }

    protected override void OnVisibleChanged(EventArgs e)
    {
        base.OnVisibleChanged(e);
        if (Visible)
        {
            _timer.Start();
        }
        else
        {
            _timer.Stop();
        }
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);

        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.Clear(BackColor);

        var center = new PointF(Width / 2f, Height / 2f);
        var maxRadius = Math.Min(Width, Height) / 2f - 4f;

        // Render 3 concurrent sonar waves expanding and fading
        for (var i = 0; i < 3; i++)
        {
            var waveProgress = (_progress + i * 0.33f) % 1.0f;
            var radius = maxRadius * waveProgress;

            var alpha = (int)(255 * (1.0f - waveProgress));
            if (alpha < 0) alpha = 0;
            if (alpha > 255) alpha = 255;

            // Translucent fill for wave area
            using var brush = new SolidBrush(Color.FromArgb((int)(alpha * 0.12f), UiStyle.PrimaryColor));
            // Fading outline
            using var pen = new Pen(Color.FromArgb(alpha, UiStyle.PrimaryColor), 2f);

            g.FillEllipse(brush, center.X - radius, center.Y - radius, radius * 2, radius * 2);
            g.DrawEllipse(pen, center.X - radius, center.Y - radius, radius * 2, radius * 2);
        }

        // Draw solid central pulse core
        using var coreBrush = new SolidBrush(UiStyle.PrimaryColor);
        g.FillEllipse(coreBrush, center.X - 10f, center.Y - 10f, 20f, 20f);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _timer.Dispose();
        }
        base.Dispose(disposing);
    }
}
