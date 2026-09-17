namespace SimilarProductsWinForms;

using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using EtsyMarketPlace.Application.AiUsage;
using SimilarProductsWinForms.Models;
using SimilarProductsWinForms.Services;

public sealed class GeminiRateLimitDialog : Form
{
    private readonly ComboBox _cboProject = new();
    private readonly ComboBox _cboTimeRange = new();
    private readonly Button _btnRefresh = new();
    private readonly Button _btnOpenWeb = new();
    private readonly Button _btnClose = new();

    // Top 4 KPI Cards
    private readonly Panel _pnlCard1 = new();
    private readonly Label _lblCard1Title = new();
    private readonly Label _lblCard1Value = new();

    private readonly Panel _pnlCard2 = new();
    private readonly Label _lblCard2Title = new();
    private readonly Label _lblCard2Value = new();

    private readonly Panel _pnlCard3 = new();
    private readonly Label _lblCard3Title = new();
    private readonly Label _lblCard3Value = new();

    private readonly Panel _pnlCard4 = new();
    private readonly Label _lblCard4Title = new();
    private readonly Label _lblCard4Value = new();

    // Center Table
    private readonly DataGridView _grid = new();

    // Bottom Trends Chart
    private readonly TrendChartPanel _chartPanel = new();

    private GeminiRateLimitReport? _report;

    public GeminiRateLimitDialog()
    {
        Text = "Google Gemini Rate Limits & Peak Usage Monitor";
        StartPosition = FormStartPosition.CenterParent;
        Size = new Size(1140, 740);
        MinimumSize = new Size(1000, 640);
        BackColor = Color.FromArgb(15, 23, 42); // #0F172A
        ForeColor = Color.White;
        Font = new Font("Segoe UI", 9.5F);

        BuildLayout();
        Load += async (_, _) => await LoadDataAsync();
    }

    private void BuildLayout()
    {
        var mainLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 5,
            Padding = new Padding(24, 18, 24, 18),
            BackColor = Color.FromArgb(15, 23, 42)
        };
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 55));  // Header
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 95));  // 4 KPI Cards
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 55));   // Table: Rate limits by model
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 45));   // Bottom: Peak Usage Trends Chart
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 45));  // Bottom Action Bar
        Controls.Add(mainLayout);

        // 1. Header
        var pnlHeader = new Panel { Dock = DockStyle.Fill };
        
        var lblSparkle = new Label
        {
            Text = "✦",
            ForeColor = Color.FromArgb(96, 165, 250),
            Font = new Font("Segoe UI", 16F, FontStyle.Bold),
            Location = new Point(0, 8),
            AutoSize = true,
            UseMnemonic = false
        };

        var lblLogo = new Label
        {
            Text = "Gemini",
            ForeColor = Color.FromArgb(147, 197, 253),
            Font = new Font("Segoe UI", 16F, FontStyle.Bold),
            Location = new Point(24, 8),
            AutoSize = true,
            UseMnemonic = false
        };

        var lblTitle = new Label
        {
            Text = "Gemini API Hız Sınırları & Kota İzleme",
            ForeColor = Color.White,
            Font = new Font("Segoe UI", 13.5F, FontStyle.Bold),
            Location = new Point(125, 11),
            AutoSize = true,
            UseMnemonic = false
        };

        var lblTierBadge = new Label
        {
            Text = "Free Tier",
            ForeColor = Color.FromArgb(191, 219, 254),
            BackColor = Color.FromArgb(30, 41, 59),
            Font = new Font("Segoe UI", 8.5F, FontStyle.Bold),
            Location = new Point(485, 15),
            Size = new Size(75, 24),
            TextAlign = ContentAlignment.MiddleCenter,
            UseMnemonic = false
        };

        // Right side controls (Project & Date Range)
        var lblProject = new Label
        {
            Text = "Project",
            ForeColor = Color.FromArgb(148, 163, 184),
            Font = new Font("Segoe UI", 8.5F),
            Location = new Point(660, 4),
            AutoSize = true
        };

        _cboProject.Location = new Point(660, 24);
        _cboProject.Width = 120;
        _cboProject.DropDownStyle = ComboBoxStyle.DropDownList;
        _cboProject.BackColor = Color.FromArgb(30, 41, 59);
        _cboProject.ForeColor = Color.White;
        _cboProject.FlatStyle = FlatStyle.Flat;
        _cboProject.Items.AddRange(["ffff", "default-project"]);
        _cboProject.SelectedIndex = 0;

        var lblDateRange = new Label
        {
            Text = "Date range",
            ForeColor = Color.FromArgb(148, 163, 184),
            Font = new Font("Segoe UI", 8.5F),
            Location = new Point(795, 4),
            AutoSize = true
        };

        _cboTimeRange.Location = new Point(795, 24);
        _cboTimeRange.Width = 140;
        _cboTimeRange.DropDownStyle = ComboBoxStyle.DropDownList;
        _cboTimeRange.BackColor = Color.FromArgb(30, 41, 59);
        _cboTimeRange.ForeColor = Color.White;
        _cboTimeRange.FlatStyle = FlatStyle.Flat;
        _cboTimeRange.Items.AddRange(["Son 28 Gün", "Son 7 Gün", "Bugün (Canlı)"]);
        _cboTimeRange.SelectedIndex = 0;
        _cboTimeRange.SelectedIndexChanged += async (_, _) => await LoadDataAsync();

        pnlHeader.Controls.Add(lblSparkle);
        pnlHeader.Controls.Add(lblLogo);
        pnlHeader.Controls.Add(lblTitle);
        pnlHeader.Controls.Add(lblTierBadge);
        pnlHeader.Controls.Add(lblProject);
        pnlHeader.Controls.Add(_cboProject);
        pnlHeader.Controls.Add(lblDateRange);
        pnlHeader.Controls.Add(_cboTimeRange);
        mainLayout.Controls.Add(pnlHeader, 0, 0);

        // 2. Top 4 KPI Cards
        var pnlCards = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 4,
            RowCount = 1
        };
        for (int i = 0; i < 4; i++) pnlCards.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25));

        SetupKpiCard(_pnlCard1, _lblCard1Title, _lblCard1Value, "Günlük İstek (RPD) Aşımı:", "3 Model Kritik", isCritical: true);
        SetupKpiCard(_pnlCard2, _lblCard2Title, _lblCard2Value, "Aktif RPM Yükü:", "6 / 5 Tepe", isCritical: false);
        SetupKpiCard(_pnlCard3, _lblCard3Title, _lblCard3Value, "Token / Dakika:", "10.1K / 250K", isCritical: false);
        SetupKpiCard(_pnlCard4, _lblCard4Title, _lblCard4Value, "Model Sağlığı:", "50 Model Aktif", isCritical: false);

        pnlCards.Controls.Add(_pnlCard1, 0, 0);
        pnlCards.Controls.Add(_pnlCard2, 1, 0);
        pnlCards.Controls.Add(_pnlCard3, 2, 0);
        pnlCards.Controls.Add(_pnlCard4, 3, 0);
        mainLayout.Controls.Add(pnlCards, 0, 1);

        // 3. Center Table: Rate limits by model
        var pnlTableContainer = new Panel { Dock = DockStyle.Fill, Margin = new Padding(0, 10, 0, 10) };
        
        var lblTableTitle = new Label
        {
            Text = "Rate limits by model",
            Font = new Font("Segoe UI", 11.5F, FontStyle.Bold),
            ForeColor = Color.White,
            Dock = DockStyle.Top,
            Height = 26,
            UseMnemonic = false
        };

        SetupGrid();
        _grid.Dock = DockStyle.Fill;

        pnlTableContainer.Controls.Add(_grid);
        pnlTableContainer.Controls.Add(lblTableTitle);
        mainLayout.Controls.Add(pnlTableContainer, 0, 2);

        // 4. Bottom Chart: Peak Usage Trends
        var pnlChartContainer = new Panel { Dock = DockStyle.Fill, Margin = new Padding(0, 0, 0, 10) };
        var lblChartTitle = new Label
        {
            Text = "Peak Usage Trends",
            Font = new Font("Segoe UI", 11F, FontStyle.Bold),
            ForeColor = Color.White,
            Dock = DockStyle.Top,
            Height = 24,
            UseMnemonic = false
        };

        _chartPanel.Dock = DockStyle.Fill;
        pnlChartContainer.Controls.Add(_chartPanel);
        pnlChartContainer.Controls.Add(lblChartTitle);
        mainLayout.Controls.Add(pnlChartContainer, 0, 3);

        // 5. Bottom Action Bar
        var pnlBottom = new Panel { Dock = DockStyle.Fill };

        _btnOpenWeb.Text = "↗ Google AI Studio Rate Limit Aç (Web)";
        _btnOpenWeb.BackColor = Color.FromArgb(30, 41, 59);
        _btnOpenWeb.ForeColor = Color.FromArgb(147, 197, 253);
        _btnOpenWeb.FlatStyle = FlatStyle.Flat;
        _btnOpenWeb.FlatAppearance.BorderColor = Color.FromArgb(70, 85, 110);
        _btnOpenWeb.Size = new Size(290, 34);
        _btnOpenWeb.Location = new Point(0, 4);
        _btnOpenWeb.Cursor = Cursors.Hand;
        _btnOpenWeb.Click += (_, _) => OpenWebRateLimits();

        _btnRefresh.Text = "🔄 Yenile";
        _btnRefresh.BackColor = Color.FromArgb(30, 41, 59);
        _btnRefresh.ForeColor = Color.White;
        _btnRefresh.FlatStyle = FlatStyle.Flat;
        _btnRefresh.FlatAppearance.BorderColor = Color.FromArgb(70, 85, 110);
        _btnRefresh.Size = new Size(100, 34);
        _btnRefresh.Location = new Point(300, 4);
        _btnRefresh.Cursor = Cursors.Hand;
        _btnRefresh.Click += async (_, _) => await LoadDataAsync();

        _btnClose.Text = "Kapat";
        _btnClose.BackColor = Color.FromArgb(30, 41, 59);
        _btnClose.ForeColor = Color.White;
        _btnClose.FlatStyle = FlatStyle.Flat;
        _btnClose.FlatAppearance.BorderColor = Color.FromArgb(70, 85, 110);
        _btnClose.Size = new Size(100, 34);
        _btnClose.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        _btnClose.Location = new Point(mainLayout.Width - 140, 4);
        _btnClose.Cursor = Cursors.Hand;
        _btnClose.Click += (_, _) => Close();

        pnlBottom.Controls.Add(_btnOpenWeb);
        pnlBottom.Controls.Add(_btnRefresh);
        pnlBottom.Controls.Add(_btnClose);
        mainLayout.Controls.Add(pnlBottom, 0, 4);
    }

    private static void SetupKpiCard(Panel pnl, Label lblTitle, Label lblVal, string title, string val, bool isCritical)
    {
        pnl.Dock = DockStyle.Fill;
        pnl.Margin = new Padding(4);
        pnl.BackColor = isCritical ? Color.FromArgb(35, 18, 24) : Color.FromArgb(30, 41, 59);
        pnl.Padding = new Padding(14, 10, 14, 10);

        lblTitle.Text = title;
        lblTitle.Font = new Font("Segoe UI", 9F);
        lblTitle.ForeColor = isCritical ? Color.FromArgb(248, 113, 113) : Color.FromArgb(148, 163, 184);
        lblTitle.Dock = DockStyle.Top;
        lblTitle.Height = 22;
        lblTitle.UseMnemonic = false;

        lblVal.Text = val;
        lblVal.Font = new Font("Segoe UI", 15F, FontStyle.Bold);
        lblVal.ForeColor = isCritical ? Color.FromArgb(239, 68, 68) : Color.White;
        lblVal.Dock = DockStyle.Fill;
        lblVal.UseMnemonic = false;

        pnl.Controls.Add(lblVal);
        pnl.Controls.Add(lblTitle);

        pnl.Paint += (_, e) =>
        {
            using var pen = new Pen(isCritical ? Color.FromArgb(220, 38, 38) : Color.FromArgb(51, 65, 85), 1.5f);
            e.Graphics.DrawRectangle(pen, 0, 0, pnl.Width - 1, pnl.Height - 1);
        };
    }

    private void SetupGrid()
    {
        _grid.BackgroundColor = Color.FromArgb(15, 23, 42);
        _grid.BorderStyle = BorderStyle.None;
        _grid.ReadOnly = true;
        _grid.AllowUserToAddRows = false;
        _grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        _grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        _grid.RowHeadersVisible = false;
        _grid.EnableHeadersVisualStyles = false;
        _grid.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.None;
        _grid.CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;
        _grid.GridColor = Color.FromArgb(30, 41, 59);
        _grid.RowTemplate.Height = 36;

        _grid.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(15, 23, 42);
        _grid.ColumnHeadersDefaultCellStyle.ForeColor = Color.FromArgb(148, 163, 184);
        _grid.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", 9F, FontStyle.Regular);
        _grid.ColumnHeadersDefaultCellStyle.SelectionBackColor = Color.FromArgb(15, 23, 42);
        _grid.ColumnHeadersHeight = 30;

        _grid.DefaultCellStyle.BackColor = Color.FromArgb(15, 23, 42);
        _grid.DefaultCellStyle.ForeColor = Color.White;
        _grid.DefaultCellStyle.Font = new Font("Segoe UI", 9.5F);
        _grid.DefaultCellStyle.SelectionBackColor = Color.FromArgb(30, 41, 59);
        _grid.DefaultCellStyle.SelectionForeColor = Color.White;

        _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Model Name", FillWeight = 30 });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Category", FillWeight = 22 });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "RPM", FillWeight = 26 });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "TPM", FillWeight = 26 });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "RPD", FillWeight = 26 });

        _grid.CellPainting += Grid_CellPainting;
    }

    private void Grid_CellPainting(object? sender, DataGridViewCellPaintingEventArgs e)
    {
        if (e.RowIndex < 0) return;

        // Custom render for RPM (col 2), TPM (col 3), RPD (col 4)
        if (e.ColumnIndex == 2 || e.ColumnIndex == 3 || e.ColumnIndex == 4)
        {
            e.PaintBackground(e.ClipBounds, true);

            var cellVal = e.Value?.ToString() ?? "";
            var tag = _grid.Rows[e.RowIndex].Tag as GeminiModelRateLimitItem;
            if (tag == null)
            {
                e.Handled = true;
                return;
            }

            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            int barWidth = Math.Max(60, e.CellBounds.Width - 100);
            int barHeight = 6;
            int barX = e.CellBounds.X + 8;
            int barY = e.CellBounds.Y + (e.CellBounds.Height - barHeight) / 2;

            // Background track
            using var trackBrush = new SolidBrush(Color.FromArgb(40, 50, 70));
            g.FillRoundedRectangle(trackBrush, barX, barY, barWidth, barHeight, 3);

            float ratio = 0f;
            Color barColor = Color.FromArgb(6, 182, 212); // Cyan
            bool isOver = false;

            if (e.ColumnIndex == 2) // RPM
            {
                ratio = tag.LimitRpm > 0 ? (float)tag.PeakRpm / tag.LimitRpm : 0f;
                isOver = tag.IsExceededRpm;
                barColor = isOver ? Color.FromArgb(239, 68, 68) : Color.FromArgb(6, 182, 212);
            }
            else if (e.ColumnIndex == 3) // TPM
            {
                ratio = tag.LimitTpm > 0 ? (float)tag.PeakTpm / tag.LimitTpm : 0f;
                barColor = Color.FromArgb(6, 182, 212);
            }
            else if (e.ColumnIndex == 4) // RPD
            {
                ratio = tag.LimitRpd > 0 ? (float)tag.PeakRpd / tag.LimitRpd : 0f;
                isOver = tag.IsExceededRpd;
                barColor = isOver ? Color.FromArgb(239, 68, 68) : Color.FromArgb(100, 116, 139);
            }

            int fillWidth = (int)Math.Min(barWidth, barWidth * Math.Min(1.0f, ratio));
            if (fillWidth > 0)
            {
                using var fillBrush = new SolidBrush(barColor);
                g.FillRoundedRectangle(fillBrush, barX, barY, fillWidth, barHeight, 3);
            }

            // Draw Value Text
            int textX = barX + barWidth + 12;
            using var font = new Font("Segoe UI", 9F, isOver ? FontStyle.Bold : FontStyle.Regular);
            using var textBrush = new SolidBrush(isOver ? Color.FromArgb(248, 113, 113) : Color.FromArgb(226, 232, 240));
            
            var textRect = new Rectangle(textX, e.CellBounds.Y, e.CellBounds.Right - textX - 4, e.CellBounds.Height);
            using var sf = new StringFormat { LineAlignment = StringAlignment.Center, Alignment = StringAlignment.Near };
            g.DrawString(cellVal, font, textBrush, textRect, sf);

            e.Handled = true;
        }
    }

    private async Task LoadDataAsync()
    {
        _btnRefresh.Enabled = false;
        _btnRefresh.Text = "⏳ Yükleniyor...";

        try
        {
            var settings = AiOptimizationSettingsStore.Load();
            int days = _cboTimeRange.SelectedIndex switch
            {
                1 => 7,
                2 => 1,
                _ => 28
            };

            _report = await GeminiRateLimitService.FetchRateLimitReportAsync(
                settings.GeminiApiKey,
                days: days,
                projectName: _cboProject.SelectedItem?.ToString() ?? "ffff");

            UpdateUi();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Kota ve hız sınırları verisi alınamadı: {ex.Message}", "Hata", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
        finally
        {
            _btnRefresh.Enabled = true;
            _btnRefresh.Text = "🔄 Yenile";
        }
    }

    private void UpdateUi()
    {
        if (_report == null) return;

        // KPI Cards
        _lblCard1Value.Text = _report.TotalCriticalOverQuotaModels > 0
            ? $"{_report.TotalCriticalOverQuotaModels} Model Kritik"
            : "Kotalar Sağlıklı";

        _lblCard2Value.Text = $"{_report.PeakActiveRpm} / {_report.PeakRpmLimit} Tepe";
        _lblCard3Value.Text = $"{FormatNumber(_report.PeakTpm)} / {FormatNumber(_report.PeakTpmLimit)}";
        _lblCard4Value.Text = $"{_report.TotalAvailableModels} Model Aktif";

        // Grid
        _grid.Rows.Clear();
        foreach (var item in _report.Models)
        {
            string rpmText = $"{item.PeakRpm} / {item.LimitRpm}";
            string tpmText = $"{FormatNumber(item.PeakTpm)} / {FormatNumber(item.LimitTpm)}";
            string rpdText = $"{item.PeakRpd} / {item.LimitRpd}";

            int rowIdx = _grid.Rows.Add(item.DisplayName, item.Category, rpmText, tpmText, rpdText);
            _grid.Rows[rowIdx].Tag = item;
        }

        // Chart
        _chartPanel.SetTrends(_report.DailyTrends);
    }

    private static string FormatNumber(long val)
    {
        if (val >= 1_000_000) return $"{(val / 1_000_000.0):0.#}M";
        if (val >= 1_000) return $"{(val / 1_000.0):0.#}K";
        return val.ToString();
    }

    private void OpenWebRateLimits()
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "https://aistudio.google.com/app/rate-limit?timeRange=last-28-days",
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Web sayfası açılamadı: {ex.Message}", "Hata", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }
}

/// <summary>
/// 28 günlük modern gradient çubuk grafiği (Peak Usage Trends)
/// </summary>
public sealed class TrendChartPanel : Panel
{
    private System.Collections.Generic.List<GeminiDailyUsageTrend> _trends = [];

    public TrendChartPanel()
    {
        DoubleBuffered = true;
        BackColor = Color.FromArgb(15, 23, 42);
    }

    public void SetTrends(System.Collections.Generic.List<GeminiDailyUsageTrend> trends)
    {
        _trends = trends;
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;

        if (_trends.Count == 0) return;

        int count = _trends.Count;
        int bottomAxisY = Height - 28;
        int topY = 12;
        int availableHeight = bottomAxisY - topY;

        int maxReq = Math.Max(35, _trends.Max(t => t.RequestCount));
        float colWidth = (float)(Width - 30) / count;
        float barWidth = Math.Max(6, colWidth * 0.58f);

        using var fontAxis = new Font("Segoe UI", 7.5F);
        using var brushAxis = new SolidBrush(Color.FromArgb(100, 116, 139));
        using var sfCenter = new StringFormat { Alignment = StringAlignment.Center };

        for (int i = 0; i < count; i++)
        {
            var t = _trends[i];
            float centerX = 15 + (i * colWidth) + (colWidth / 2f);
            float barX = centerX - (barWidth / 2f);

            float ratio = (float)t.RequestCount / maxReq;
            float barHeight = Math.Max(4, availableHeight * ratio);
            float barY = bottomAxisY - barHeight;

            var barRect = new RectangleF(barX, barY, barWidth, barHeight);

            // Sleek gradient for bars
            bool isPeak = t.RequestCount >= 20 || t.PeakRpm >= 5;
            Color topColor = isPeak ? Color.FromArgb(244, 114, 182) : Color.FromArgb(56, 189, 248);
            Color bottomColor = isPeak ? Color.FromArgb(239, 68, 68) : Color.FromArgb(30, 58, 138);

            using (var grad = new LinearGradientBrush(new PointF(barX, barY), new PointF(barX, bottomAxisY), topColor, bottomColor))
            {
                g.FillRoundedRectangle(grad, barRect.X, barRect.Y, barRect.Width, barRect.Height, 3);
            }

            // Glow line on top
            using (var glowPen = new Pen(isPeak ? Color.FromArgb(251, 191, 36) : Color.FromArgb(125, 211, 252), 1.5f))
            {
                g.DrawLine(glowPen, barX + 1, barY, barX + barWidth - 1, barY);
            }

            // Day label
            g.DrawString(t.DayIndex.ToString(), fontAxis, brushAxis, centerX, bottomAxisY + 6, sfCenter);
        }
    }
}

internal static class GraphicsExtensions
{
    public static void FillRoundedRectangle(this Graphics g, Brush brush, float x, float y, float width, float height, float radius)
    {
        using var path = new GraphicsPath();
        float diameter = radius * 2;
        path.AddArc(x, y, diameter, diameter, 180, 90);
        path.AddArc(x + width - diameter, y, diameter, diameter, 270, 90);
        path.AddArc(x + width - diameter, y + height - diameter, diameter, diameter, 0, 90);
        path.AddArc(x, y + height - diameter, diameter, diameter, 90, 90);
        path.CloseFigure();
        g.FillPath(brush, path);
    }
}
