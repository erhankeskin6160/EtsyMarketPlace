namespace SimilarProductsWinForms;

using System.Net.Http.Json;
using System.Text.Json;
using SimilarProductsWinForms.Models;
using SimilarProductsWinForms.Services;

internal sealed class FinancialAnalysisForm : Form
{
    private readonly Label _status = new();
    private readonly TableLayoutPanel _kpis = new();
    private readonly ListBox _insights = new();
    private readonly ListBox _recommendations = new();
    private readonly Button _refresh = new();
    private readonly HttpClient _httpClient = new();

    public FinancialAnalysisForm()
    {
        Text = "Finansal AI Analiz";
        BackColor = UiStyle.BackgroundColor;
        ForeColor = UiStyle.TextDark;
        Padding = new Padding(24);
        BuildLayout();
        Shown += async (_, _) => await LoadAnalysisAsync();
        FormClosed += (_, _) => _httpClient.Dispose();
    }

    private void BuildLayout()
    {
        var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 5 };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 52));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 55));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 45));

        var header = new Panel { Dock = DockStyle.Fill };
        var title = new Label { Text = "Finansal AI Analiz", AutoSize = true, Font = new Font(Font, FontStyle.Bold), Location = new Point(0, 4) };
        _refresh.Text = "Yenile";
        _refresh.AutoSize = true;
        _refresh.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        _refresh.Location = new Point(Width - 120, 0);
        _refresh.Click += async (_, _) => await LoadAnalysisAsync();
        header.Controls.Add(title);
        header.Controls.Add(_refresh);
        root.Controls.Add(header, 0, 0);

        _kpis.Dock = DockStyle.Fill;
        _kpis.ColumnCount = 3;
        for (var i = 0; i < 3; i++) _kpis.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.33f));
        root.Controls.Add(_kpis, 0, 1);

        _status.Text = "Analiz bekleniyor...";
        _status.Dock = DockStyle.Fill;
        root.Controls.Add(_status, 0, 2);

        var insightGroup = new GroupBox { Text = "Bulgular ve Uyarılar", Dock = DockStyle.Fill };
        _insights.Dock = DockStyle.Fill;
        insightGroup.Controls.Add(_insights);
        root.Controls.Add(insightGroup, 0, 3);

        var recommendationGroup = new GroupBox { Text = "Öneriler", Dock = DockStyle.Fill };
        _recommendations.Dock = DockStyle.Fill;
        recommendationGroup.Controls.Add(_recommendations);
        root.Controls.Add(recommendationGroup, 0, 4);
        Controls.Add(root);
    }

    private async Task LoadAnalysisAsync()
    {
        try
        {
            _refresh.Enabled = false;
            var settings = EtsyApiSettingsStore.Load();
            if (string.IsNullOrWhiteSpace(settings.ShopId) || string.IsNullOrWhiteSpace(settings.IntegrationApiBaseUrl))
            {
                _status.Text = "Shop ID ve entegrasyon API adresini Etsy API Ayarları bölümünden girin.";
                return;
            }

            var url = $"{settings.IntegrationApiBaseUrl.TrimEnd('/')}/api/etsy/financial/analysis?shopId={Uri.EscapeDataString(settings.ShopId)}";
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            if (!string.IsNullOrWhiteSpace(settings.IntegrationApiKey)) request.Headers.Add("X-Api-Key", settings.IntegrationApiKey);
            using var response = await _httpClient.SendAsync(request);
            response.EnsureSuccessStatusCode();
            var result = await response.Content.ReadFromJsonAsync<AnalysisResponse>(new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            if (result?.ProfitMarginPercent is null) throw new InvalidOperationException("API geçerli analiz sonucu döndürmedi.");
            ShowResult(result);
        }
        catch (Exception ex)
        {
            _status.Text = $"Analiz alınamadı: {ex.Message}";
        }
        finally { _refresh.Enabled = true; }
    }

    private void ShowResult(AnalysisResponse result)
    {
        _kpis.Controls.Clear();
        AddKpi("Kâr Marjı", $"%{result.ProfitMarginPercent:N1}", 0);
        AddKpi("Gider Oranı", $"%{result.ExpenseRatioPercent:N1}", 1);
        AddKpi("Dönem", $"{result.Snapshot?.StartDate:dd.MM.yyyy} - {result.Snapshot?.EndDate:dd.MM.yyyy}", 2);
        _insights.Items.Clear();
        foreach (var item in result.Insights ?? []) _insights.Items.Add($"[{item.Severity}] {item.Title}: {item.Description}");
        _recommendations.Items.Clear();
        foreach (var item in result.Recommendations ?? []) _recommendations.Items.Add($"[{item.Priority}] {item.Title}: {item.Description}");
        _status.Text = $"Analiz zamanı: {result.Snapshot?.GeneratedAt:dd.MM.yyyy HH:mm} UTC";
    }

    private void AddKpi(string title, string value, int column)
    {
        var label = new Label { Dock = DockStyle.Fill, Text = $"{title}\n{value}", TextAlign = ContentAlignment.MiddleCenter, Font = new Font(Font, FontStyle.Bold), BorderStyle = BorderStyle.FixedSingle };
        _kpis.Controls.Add(label, column, 0);
    }

    private sealed class AnalysisResponse
    {
        public AnalysisSnapshot? Snapshot { get; set; }
        public decimal ProfitMarginPercent { get; set; }
        public decimal ExpenseRatioPercent { get; set; }
        public List<AnalysisInsight>? Insights { get; set; }
        public List<AnalysisRecommendation>? Recommendations { get; set; }
    }

    private sealed class AnalysisSnapshot { public DateTimeOffset StartDate { get; set; } public DateTimeOffset EndDate { get; set; } public DateTimeOffset GeneratedAt { get; set; } }
    private sealed class AnalysisInsight { public string Severity { get; set; } = ""; public string Title { get; set; } = ""; public string Description { get; set; } = ""; }
    private sealed class AnalysisRecommendation { public string Priority { get; set; } = ""; public string Title { get; set; } = ""; public string Description { get; set; } = ""; }
}
