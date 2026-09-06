namespace SimilarProductsWinForms.Controls;

using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using SimilarProductsWinForms.Models;

internal sealed class FunnelStageCardControl : Panel
{
    public FunnelStageCardControl(
        string stepNumber,
        string stageTitle,
        string primaryValue,
        string conversionRateText,
        string benchmarkComparison,
        bool isBottleneck,
        string? bottleneckWarning = null)
    {
        Dock = DockStyle.Top;
        Height = isBottleneck ? 115 : 90;
        Margin = new Padding(0, 0, 0, 8);
        Padding = new Padding(14, 10, 14, 10);
        BackColor = isBottleneck ? Color.FromArgb(45, 26, 30) : Color.FromArgb(30, 41, 59);

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 3,
            RowCount = isBottleneck ? 3 : 2,
            BackColor = Color.Transparent
        };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 42));  // Step Badge (1, 2, 3...)
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); // Stage Info & Benchmark
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 130)); // Primary Value / Rate

        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 28)); // Header + Value
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 36)); // Benchmark & Details
        if (isBottleneck)
        {
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); // Bottleneck Alert
        }

        // Step Badge
        var lblStep = new Label
        {
            Text = stepNumber,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleCenter,
            Font = new Font("Segoe UI Semibold", 12F, FontStyle.Bold),
            ForeColor = isBottleneck ? Color.FromArgb(239, 68, 68) : UiStyle.AccentColor,
            BackColor = Color.FromArgb(15, 23, 42)
        };
        root.Controls.Add(lblStep, 0, 0);
        root.SetRowSpan(lblStep, isBottleneck ? 3 : 2);

        // Title
        var lblTitle = new Label
        {
            Text = stageTitle,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            Font = new Font("Segoe UI Semibold", 10F, FontStyle.Bold),
            ForeColor = Color.White
        };
        root.Controls.Add(lblTitle, 1, 0);

        // Primary Value
        var lblValue = new Label
        {
            Text = primaryValue,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleRight,
            Font = new Font("Segoe UI Semibold", 13F, FontStyle.Bold),
            ForeColor = isBottleneck ? Color.FromArgb(248, 113, 113) : UiStyle.SuccessColor
        };
        root.Controls.Add(lblValue, 2, 0);

        // Benchmark & Conversion detail
        var lblBenchmark = new Label
        {
            Text = $"{conversionRateText} • {benchmarkComparison}",
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.TopLeft,
            Font = new Font("Segoe UI", 8.4F),
            ForeColor = Color.FromArgb(148, 163, 184)
        };
        root.Controls.Add(lblBenchmark, 1, 1);
        root.SetColumnSpan(lblBenchmark, 2);

        // Bottleneck Warning
        if (isBottleneck && !string.IsNullOrWhiteSpace(bottleneckWarning))
        {
            var lblWarn = new Label
            {
                Text = $"⚠️ TIKANIKLIK NOKTASI: {bottleneckWarning}",
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                Font = new Font("Segoe UI Semibold", 8.2F, FontStyle.Bold),
                ForeColor = Color.FromArgb(254, 202, 202),
                BackColor = Color.FromArgb(127, 29, 29),
                Padding = new Padding(6, 2, 6, 2)
            };
            root.Controls.Add(lblWarn, 1, 2);
            root.SetColumnSpan(lblWarn, 2);
        }

        Controls.Add(root);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;

        using var pen = new Pen(Color.FromArgb(51, 65, 85), 1);
        e.Graphics.DrawRectangle(pen, 0, 0, Width - 1, Height - 1);
    }
}
