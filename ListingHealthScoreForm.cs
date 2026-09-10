namespace SimilarProductsWinForms;

using System.Diagnostics;
using System.Globalization;
using System.Text.RegularExpressions;
using EtsyMarketPlace.Application.ListingOptimization;
using SimilarProductsWinForms.Models;
using SimilarProductsWinForms.Services;
using SimilarProductsWinForms.Controls;

internal sealed class ListingHealthScoreForm : Form
{
    private readonly EtsyApiClient _apiClient = new();
    private readonly IAiListingOptimizer? _aiOptimizer;
    private readonly ListingOptimizationHistoryService? _historyService;
    private MarketListingResult? _currentListing;

    private readonly TextBox _linkTextBox = new();
    private readonly Button _analyzeLinkButton = new();
    private readonly Label _statusLabel = new();
    
    // Summary KPI controls
    private readonly Label _scoreGradeLabel = new();
    private readonly Label _scoreNumericLabel = new();
    private readonly Label _summaryLabel = new();
    private readonly FlowLayoutPanel _breakdownPanel = new();
    private ModernScrollPanel? _breakdownScroll;
    
    // Details controls
    private readonly ModernMultilineTextBox _strengthsTextBox = new();
    private readonly ModernMultilineTextBox _warningsTextBox = new();
    private readonly ModernMultilineTextBox _actionsTextBox = new();
    private readonly TabControl _tabControl = new();

    public ListingHealthScoreForm(
        MarketListingResult? initialListing = null,
        IAiListingOptimizer? aiOptimizer = null,
        ListingOptimizationHistoryService? historyService = null)
    {
        _currentListing = initialListing;
        _aiOptimizer = aiOptimizer;
        _historyService = historyService;
        BuildLayout();
    }

    protected override void OnLoad(EventArgs e)
    {
        base.OnLoad(e);
        if (_currentListing != null)
        {
            AnalyzeListing(_currentListing);
        }
    }

    private void BuildLayout()
    {
        Text = "Listing Saglik Skoru Analizi";
        StartPosition = FormStartPosition.CenterParent;
        WindowState = FormWindowState.Maximized;
        UiStyle.ApplyResponsiveTheme(this, new Size(1024, 700));

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            RowCount = 4,
            Padding = new Padding(18)
        };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 65));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 55));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 50));
        Controls.Add(root);

        // Header
        var header = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2 };
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 400));

        header.Controls.Add(new Label
        {
            Dock = DockStyle.Fill,
            Text = "🩺 Listing Sağlık Skoru Paneli",
            Font = UiStyle.TitleFont,
            ForeColor = UiStyle.TextDark,
            TextAlign = ContentAlignment.MiddleLeft,
        }, 0, 0);

        _statusLabel.Dock = DockStyle.Fill;
        _statusLabel.Text = "Listing analizi için link girin veya listeden ürün seçin";
        _statusLabel.TextAlign = ContentAlignment.MiddleRight;
        _statusLabel.ForeColor = UiStyle.TextMuted;
        header.Controls.Add(_statusLabel, 1, 0);
        root.Controls.Add(header, 0, 0);

        // Top Toolbar
        var toolbar = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3 };
        toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 140));
        toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 160));

        toolbar.Controls.Add(new Label
        {
            Dock = DockStyle.Fill,
            Text = "Etsy Listing Linki:",
            ForeColor = UiStyle.TextDark,
            TextAlign = ContentAlignment.MiddleLeft,
            Font = UiStyle.SemiboldBaseFont
        }, 0, 0);

        _linkTextBox.Dock = DockStyle.Fill;
        _linkTextBox.PlaceholderText = "Örn: https://www.etsy.com/listing/123456789/sample-title";
        _linkTextBox.KeyDown += async (_, e) =>
        {
            if (e.KeyCode == Keys.Enter)
            {
                e.SuppressKeyPress = true;
                await AnalyzeFromLinkAsync();
            }
        };
        toolbar.Controls.Add(_linkTextBox, 1, 0);

        _analyzeLinkButton.Dock = DockStyle.Fill;
        _analyzeLinkButton.Text = "Linkten Analiz Et";
        _analyzeLinkButton.BackColor = UiStyle.PrimaryColor;
        _analyzeLinkButton.ForeColor = Color.White;
        _analyzeLinkButton.FlatStyle = FlatStyle.Flat;
        _analyzeLinkButton.FlatAppearance.BorderSize = 0;
        _analyzeLinkButton.Font = UiStyle.SemiboldBaseFont;
        _analyzeLinkButton.Click += async (_, _) => await AnalyzeFromLinkAsync();
        toolbar.Controls.Add(_analyzeLinkButton, 2, 0);

        root.Controls.Add(toolbar, 0, 1);

        // Main Content (Split into Left: Summary & Categories, Right: Tabs)
        var contentPanel = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2 };
        contentPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 42));
        contentPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 58));

        // Left Side
        var leftPanel = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 2 };
        leftPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 150));
        leftPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        // Score Card
        var scoreCard = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            BackColor = UiStyle.CardBackground,
            Padding = new Padding(12),
            Margin = new Padding(0, 0, 8, 0)
        };
        scoreCard.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 110));
        scoreCard.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        var gradeBox = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.FromArgb(30, 41, 59),
            Padding = new Padding(4)
        };
        _scoreGradeLabel.Dock = DockStyle.Fill;
        _scoreGradeLabel.Text = "--";
        _scoreGradeLabel.Font = new Font("Segoe UI", 32F, FontStyle.Bold);
        _scoreGradeLabel.ForeColor = Color.White;
        _scoreGradeLabel.TextAlign = ContentAlignment.MiddleCenter;
        gradeBox.Controls.Add(_scoreGradeLabel);
        scoreCard.Controls.Add(gradeBox, 0, 0);

        var scoreDetails = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 2 };
        scoreDetails.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
        scoreDetails.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        _scoreNumericLabel.Dock = DockStyle.Fill;
        _scoreNumericLabel.Text = "Sağlık Skoru: -- / 100";
        _scoreNumericLabel.Font = UiStyle.KpiValueFont;
        _scoreNumericLabel.ForeColor = UiStyle.TextDark;
        _scoreNumericLabel.TextAlign = ContentAlignment.MiddleLeft;
        scoreDetails.Controls.Add(_scoreNumericLabel, 0, 0);

        _summaryLabel.Dock = DockStyle.Fill;
        _summaryLabel.Text = "Bir listing seçin veya link girerek analizi başlatın.";
        _summaryLabel.Font = UiStyle.SubtitleFont;
        _summaryLabel.ForeColor = UiStyle.TextMuted;
        scoreDetails.Controls.Add(_summaryLabel, 0, 1);

        scoreCard.Controls.Add(scoreDetails, 1, 0);
        leftPanel.Controls.Add(scoreCard, 0, 0);

        // Breakdown List
        _breakdownScroll = new ModernScrollPanel
        {
            Dock = DockStyle.Fill,
            Margin = new Padding(0, 8, 8, 0),
            Padding = new Padding(0, 0, 2, 0)
        };
        _breakdownPanel.Dock = DockStyle.Top;
        _breakdownPanel.AutoSize = true;
        _breakdownPanel.AutoSizeMode = AutoSizeMode.GrowAndShrink;
        _breakdownPanel.AutoScroll = false;
        _breakdownPanel.FlowDirection = FlowDirection.TopDown;
        _breakdownPanel.WrapContents = false;
        _breakdownPanel.BackColor = UiStyle.CardBackground;
        _breakdownPanel.Padding = new Padding(12);
        _breakdownScroll.SetContent(_breakdownPanel);
        leftPanel.Controls.Add(_breakdownScroll, 0, 1);

        contentPanel.Controls.Add(leftPanel, 0, 0);

        // Right Side Tabs
        _tabControl.Dock = DockStyle.Fill;
        _tabControl.Font = UiStyle.SemiboldBaseFont;

        var tabStrengths = new TabPage("💪 Güçlü Yönler");
        ConfigureTabBox(_strengthsTextBox);
        tabStrengths.Controls.Add(_strengthsTextBox);
        _tabControl.TabPages.Add(tabStrengths);

        var tabWarnings = new TabPage("⚠️ Eksikler & Uyarılar");
        ConfigureTabBox(_warningsTextBox);
        tabWarnings.Controls.Add(_warningsTextBox);
        _tabControl.TabPages.Add(tabWarnings);

        var tabActions = new TabPage("💡 İyileştirme Adımları");
        ConfigureTabBox(_actionsTextBox);
        tabActions.Controls.Add(_actionsTextBox);
        _tabControl.TabPages.Add(tabActions);

        contentPanel.Controls.Add(_tabControl, 1, 0);
        root.Controls.Add(contentPanel, 0, 2);

        // Action Buttons at the Bottom
        var bottomBar = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 5 };
        bottomBar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 20));
        bottomBar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 20));
        bottomBar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 20));
        bottomBar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 20));
        bottomBar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 20));

        var btnAiOpt = CreateActionButton("🤖 AI Metin Optimizasyon", OpenAiOptimization);
        btnAiOpt.BackColor = UiStyle.AiColor;
        bottomBar.Controls.Add(btnAiOpt, 0, 0);

        var btnAiImg = CreateActionButton("📸 AI Görsel Üret", OpenAiImageGen);
        btnAiImg.BackColor = UiStyle.PrimaryColor;
        bottomBar.Controls.Add(btnAiImg, 1, 0);

        var btnEtsy = CreateActionButton("🔗 Etsy'de Aç", OpenInEtsy);
        bottomBar.Controls.Add(btnEtsy, 2, 0);

        var btnCopy = CreateActionButton("📋 Raporu Kopyala", CopyReportToClipboard);
        bottomBar.Controls.Add(btnCopy, 3, 0);

        var btnClose = CreateActionButton("Kapat", () => Close());
        btnClose.BackColor = Color.FromArgb(82, 93, 110);
        bottomBar.Controls.Add(btnClose, 4, 0);

        root.Controls.Add(bottomBar, 0, 3);
    }

    private async Task AnalyzeFromLinkAsync()
    {
        var link = _linkTextBox.Text.Trim();
        if (!TryExtractListingId(link, out var listingId))
        {
            MessageBox.Show(
                this,
                "Geçerli bir Etsy listing linki veya kimliği girin.\nÖrn: https://www.etsy.com/listing/123456789/urun-adi",
                "Linkten Analiz",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
            return;
        }

        try
        {
            UseWaitCursor = true;
            _statusLabel.Text = "Etsy'den listing verileri çekiliyor...";
            var settings = EtsyApiSettingsStore.Load();
            var listing = await _apiClient.GetPublicListingAsync(settings, listingId);
            AnalyzeListing(listing);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Listing Alınamadı", MessageBoxButtons.OK, MessageBoxIcon.Error);
            _statusLabel.Text = "Listing verisi alınamadı.";
        }
        finally
        {
            UseWaitCursor = false;
        }
    }

    private void AnalyzeListing(MarketListingResult listing)
    {
        _currentListing = listing;
        var report = ListingHealthCalculator.Calculate(listing);

        // Update Score Card
        _scoreGradeLabel.Text = report.Grade;
        _scoreGradeLabel.Parent!.BackColor = GetGradeColor(report.Grade);
        _scoreNumericLabel.Text = $"Sağlık Skoru: {report.TotalScore} / 100";
        _summaryLabel.Text = report.SummaryText;

        // Render Breakdown List
        _breakdownPanel.Controls.Clear();
        foreach (var item in report.BreakdownList)
        {
            _breakdownPanel.Controls.Add(CreateBreakdownItem(item));
        }
        _breakdownScroll?.RecalculateScroll();

        // Render Details Text
        _strengthsTextBox.Text = report.Strengths.Count > 0
            ? string.Join(Environment.NewLine + Environment.NewLine, report.Strengths.Select(s => "• " + s))
            : "Belirgin bir güçlü yön tespit edilmedi.";

        _warningsTextBox.Text = report.Warnings.Count > 0
            ? string.Join(Environment.NewLine + Environment.NewLine, report.Warnings.Select(w => "⚠️ " + w))
            : "Herhangi bir kritik eksik veya uyarı bulunmuyor.";

        _actionsTextBox.Text = report.ActionItems.Count > 0
            ? string.Join(Environment.NewLine + Environment.NewLine, report.ActionItems.Select((a, idx) => $"{idx + 1}. {a}"))
            : "Tüm alanlar ideal seviyede görünüyor.";

        _statusLabel.Text = $"'{listing.Title}' için sağlık skoru hesaplandı.";
    }

    private static Control CreateBreakdownItem(HealthCategoryBreakdown item)
    {
        var panel = new TableLayoutPanel
        {
            Width = 360,
            Height = 44,
            Margin = new Padding(0, 0, 0, 8),
            BackColor = UiStyle.InputBackground,
            Padding = new Padding(8, 4, 8, 4)
        };
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 60));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 40));

        var lblName = new Label
        {
            Dock = DockStyle.Fill,
            Text = item.CategoryName,
            Font = UiStyle.SemiboldBaseFont,
            ForeColor = UiStyle.TextDark,
            TextAlign = ContentAlignment.MiddleLeft
        };
        panel.Controls.Add(lblName, 0, 0);

        var lblStatus = new Label
        {
            Dock = DockStyle.Fill,
            Text = item.StatusText,
            Font = UiStyle.BaseFont,
            ForeColor = item.StatusColor,
            TextAlign = ContentAlignment.MiddleRight
        };
        panel.Controls.Add(lblStatus, 1, 0);

        return panel;
    }

    private void OpenAiOptimization()
    {
        if (_currentListing is null)
        {
            MessageBox.Show(this, "Önce bir listing analiz edin.", "Uyarı", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        var dbPath = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "EtsyMarketPlace", "market-tracking.db");
        var repo = new EtsyMarketPlace.Infrastructure.ListingOptimization.SqliteListingOptimizationHistoryRepository(dbPath);
        var historyService = _historyService ?? new ListingOptimizationHistoryService(repo);
        var aiOptimizer = _aiOptimizer ?? new OpenAiListingOptimizer(AiOptimizationSettingsStore.Load, new ListingOptimizationService());

        using var form = new ListingOptimizationForm(historyService, aiOptimizer, _currentListing);
        form.ShowDialog(this);
    }

    private void OpenAiImageGen()
    {
        var aiOptimizer = _aiOptimizer ?? new OpenAiListingOptimizer(AiOptimizationSettingsStore.Load, new ListingOptimizationService());
        using var form = new AiListingImageForm(aiOptimizer);
        form.ShowDialog(this);
    }

    private void OpenInEtsy()
    {
        if (_currentListing != null && !string.IsNullOrWhiteSpace(_currentListing.ListingUrl))
        {
            Process.Start(new ProcessStartInfo(_currentListing.ListingUrl) { UseShellExecute = true });
        }
    }

    private void CopyReportToClipboard()
    {
        if (_currentListing is null) return;

        var text = $"=== ETSY LİSTİNG SAĞLIK RAPORU ===\n" +
                   $"Ürün: {_currentListing.Title}\n" +
                   $"Sağlık Skoru: {_scoreNumericLabel.Text} ({_scoreGradeLabel.Text})\n" +
                   $"Özet: {_summaryLabel.Text}\n\n" +
                   $"--- GÜÇLÜ YÖNLER ---\n{_strengthsTextBox.Text}\n\n" +
                   $"--- EKSİKLER & UYARILAR ---\n{_warningsTextBox.Text}\n\n" +
                   $"--- İYİLEŞTİRME ADIMLARI ---\n{_actionsTextBox.Text}\n";

        Clipboard.SetText(text);
        _statusLabel.Text = "Sağlık raporu panoya kopyalandı.";
    }

    private static Color GetGradeColor(string grade) => grade switch
    {
        "A+" or "A" => Color.FromArgb(16, 185, 129),
        "B" => Color.FromArgb(6, 182, 212),
        "C" => Color.FromArgb(245, 158, 11),
        _ => Color.FromArgb(239, 68, 68)
    };

    private static bool TryExtractListingId(string value, out long listingId)
    {
        listingId = 0;
        if (string.IsNullOrWhiteSpace(value)) return false;

        var match = Regex.Match(value, @"(?:listing|copy)/(?<id>\d{6,})", RegexOptions.IgnoreCase);
        if (!match.Success) match = Regex.Match(value, @"(?<id>\d{8,})");

        return match.Success && long.TryParse(match.Groups["id"].Value, NumberStyles.None, CultureInfo.InvariantCulture, out listingId);
    }

    private static void ConfigureTabBox(ModernMultilineTextBox tb)
    {
        tb.Dock = DockStyle.Fill;
        tb.ReadOnly = true;
        tb.Font = new Font("Segoe UI", 10F);
        tb.Margin = new Padding(6);
    }

    private static Button CreateActionButton(string text, Action onClick)
    {
        var btn = new Button
        {
            Dock = DockStyle.Fill,
            Text = text,
            BackColor = UiStyle.SecondaryColor,
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Font = UiStyle.SemiboldBaseFont,
            Margin = new Padding(4)
        };
        btn.FlatAppearance.BorderSize = 0;
        btn.Click += (_, _) => onClick();
        return btn;
    }
}
