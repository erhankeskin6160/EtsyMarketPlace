namespace SimilarProductsWinForms.Controls;

using System.Drawing.Drawing2D;
using EtsyMarketPlace.Domain.Tracking;

internal sealed class TrendChartControl : Control
{
    private IReadOnlyList<TrendPoint> _points = [];
    private string _title = "Trend verisi secilmedi";

    public TrendChartControl()
    {
        DoubleBuffered = true;
        BackColor = UiStyle.CardBackground;
        ForeColor = UiStyle.TextDark;
        ResizeRedraw = true;
        Font = new Font("Segoe UI", 9F);
    }

    public void SetHistory(TrackingHistory? history)
    {
        if (history is null)
        {
            _title = "Trend verisi secilmedi";
            _points = [];
            Invalidate();
            return;
        }

        var metricName = history.Item.EntityType switch
        {
            TrackingEntityType.Listing => "Favori",
            TrackingEntityType.Shop => "Magaza satisi",
            _ => "Firsat puani",
        };
        _points = history.Snapshots
            .OrderBy(item => item.CapturedAt)
            .Select(item => (item.CapturedAt, Value: GetValue(history.Item.EntityType, item)))
            .Where(item => item.Value.HasValue)
            .Select(item => new TrendPoint(item.CapturedAt, item.Value!.Value))
            .ToList();
        _title = $"{history.Item.DisplayName} - {metricName}";
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        e.Graphics.Clear(UiStyle.CardBackground);
        using var titleFont = new Font("Segoe UI Semibold", 11F);
        using var titleBrush = new SolidBrush(UiStyle.TextDark);
        using var textMutedBrush = new SolidBrush(UiStyle.TextMuted);
        e.Graphics.DrawString(_title, titleFont, titleBrush, 18, 14);

        var plot = new RectangleF(65, 52, Math.Max(1, Width - 90), Math.Max(1, Height - 92));
        using var borderPen = new Pen(UiStyle.BorderColor);
        e.Graphics.DrawRectangle(borderPen, plot.X, plot.Y, plot.Width, plot.Height);

        if (_points.Count == 0)
        {
            DrawCentered(e.Graphics, "Henuz snapshot verisi yok.", plot);
            return;
        }

        var min = _points.Min(item => item.Value);
        var max = _points.Max(item => item.Value);
        if (max == min)
        {
            min -= 1;
            max += 1;
        }
        var padding = (max - min) * 0.12m;
        min -= padding;
        max += padding;

        using var gridPen = new Pen(Color.FromArgb(40, UiStyle.BorderColor));
        for (var row = 0; row <= 4; row++)
        {
            var y = plot.Top + plot.Height * row / 4F;
            e.Graphics.DrawLine(gridPen, plot.Left, y, plot.Right, y);
            var label = max - (max - min) * row / 4m;
            e.Graphics.DrawString(label.ToString("0.##"), Font, textMutedBrush, 6, y - 8);
        }

        var rendered = _points.Select((item, index) => new PointF(
            _points.Count == 1 ? plot.Left + plot.Width / 2 : plot.Left + plot.Width * index / (_points.Count - 1),
            plot.Bottom - (float)((item.Value - min) / (max - min)) * plot.Height)).ToArray();

        if (rendered.Length > 1)
        {
            using var linePen = new Pen(UiStyle.PrimaryColor, 3F) { LineJoin = LineJoin.Round };
            e.Graphics.DrawLines(linePen, rendered);
        }
        using var pointBrush = new SolidBrush(UiStyle.EtsyColor);
        foreach (var point in rendered) e.Graphics.FillEllipse(pointBrush, point.X - 4, point.Y - 4, 8, 8);

        e.Graphics.DrawString(_points[0].CapturedAt.LocalDateTime.ToString("dd.MM HH:mm"), Font, textMutedBrush, plot.Left, plot.Bottom + 8);
        var lastLabel = _points[^1].CapturedAt.LocalDateTime.ToString("dd.MM HH:mm");
        var lastSize = e.Graphics.MeasureString(lastLabel, Font);
        e.Graphics.DrawString(lastLabel, Font, textMutedBrush, plot.Right - lastSize.Width, plot.Bottom + 8);
    }

    private static decimal? GetValue(TrackingEntityType type, TrackingSnapshot snapshot) => type switch
    {
        TrackingEntityType.Listing => snapshot.Favorites,
        TrackingEntityType.Shop => snapshot.ShopSales,
        _ => snapshot.OpportunityScore,
    };

    private void DrawCentered(Graphics graphics, string text, RectangleF area)
    {
        var size = graphics.MeasureString(text, Font);
        graphics.DrawString(text, Font, Brushes.Gray, area.Left + (area.Width - size.Width) / 2, area.Top + (area.Height - size.Height) / 2);
    }

    private sealed record TrendPoint(DateTimeOffset CapturedAt, decimal Value);
}
