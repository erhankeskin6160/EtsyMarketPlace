namespace SimilarProductsWinForms;

using EtsyMarketPlace.Application.ListingOptimization;
using SimilarProductsWinForms.Models;
using SimilarProductsWinForms.Controls;

internal sealed class ListingOptimizationForm : Form
{
    private readonly ListingOptimizationService _service = new();
    private readonly ListingOptimizationHistoryService _historyService;
    private readonly IAiListingOptimizer _aiOptimizer;
    private readonly ModernMultilineTextBox _titleTextBox = new();
    private readonly TextBox _keywordTextBox = new();
    private readonly ModernMultilineTextBox _tagsTextBox = new();
    private readonly ModernMultilineTextBox _descriptionTextBox = new();
    private readonly ModernMultilineTextBox _titleSuggestionsTextBox = new();
    private readonly ModernMultilineTextBox _tagSuggestionsTextBox = new();
    private readonly ModernMultilineTextBox _descriptionDraftTextBox = new();
    private readonly ListBox _riskListBox = new();
    private readonly ListBox _checklistBox = new();
    private readonly Label _scoreLabel = new();
    private ListingOptimizationResult? _lastResult;
    private string _listingId = "";
    private string _listingTitle = "";

    public ListingOptimizationForm(
        ListingOptimizationHistoryService historyService,
        IAiListingOptimizer aiOptimizer,
        MarketListingResult? listing = null,
        string targetKeyword = "")
    {
        _historyService = historyService;
        _aiOptimizer = aiOptimizer;
        BuildLayout();
        if (listing is not null) LoadListing(listing, targetKeyword);
        Analyze();
    }

    private void BuildLayout()
    {
        Text = "Yapay Zeka Destekli Listing Optimizasyonu";
        StartPosition = FormStartPosition.CenterParent;
        MinimumSize = new Size(1180, 760);
        WindowState = FormWindowState.Maximized;
        Font = new Font("Segoe UI", 10F);
        Padding = new Padding(18);
        UiStyle.ApplyResponsiveTheme(this, new Size(1180, 760));

        var root = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 4 };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 72));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 52));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 52));
        Controls.Add(root);

        var header = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2 };
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 320));
        header.Controls.Add(new Label
        {
            Dock = DockStyle.Fill,
            Text = "Yapay Zeka Destekli Listing Optimizasyonu",
            Font = new Font("Segoe UI Semibold", 21F),
            ForeColor = Color.FromArgb(23, 32, 49),
            TextAlign = ContentAlignment.MiddleLeft,
        }, 0, 0);
        _scoreLabel.Dock = DockStyle.Fill;
        _scoreLabel.TextAlign = ContentAlignment.MiddleRight;
        _scoreLabel.ForeColor = Color.FromArgb(82, 93, 110);
        header.Controls.Add(_scoreLabel, 1, 0);
        root.Controls.Add(header, 0, 0);

        var toolbar = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 6 };
        toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 145));
        toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        for (var index = 2; index < 6; index++) toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 135));
        toolbar.Controls.Add(LabelFor("Hedef anahtar kelime"), 0, 0);
        _keywordTextBox.Dock = DockStyle.Fill;
        toolbar.Controls.Add(_keywordTextBox, 1, 0);
        var analyze = CreateButton("Analiz Et");
        analyze.Click += (_, _) => Analyze();
        toolbar.Controls.Add(analyze, 2, 0);
        var aiGenerate = CreateButton("AI ile Uret");
        aiGenerate.Click += async (_, _) => await GenerateWithAiAsync();
        toolbar.Controls.Add(aiGenerate, 3, 0);
        var aiSettings = CreateButton("AI Ayarlari");
        aiSettings.Click += (_, _) =>
        {
            using var form = new AiOptimizationSettingsForm();
            form.ShowDialog(this);
        };
        toolbar.Controls.Add(aiSettings, 4, 0);
        var copyAll = CreateButton("Tumunu Kopyala");
        copyAll.Click += (_, _) => CopyAll();
        toolbar.Controls.Add(copyAll, 5, 0);
        root.Controls.Add(toolbar, 0, 1);

        var content = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2 };
        content.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 45));
        content.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 55));
        content.Controls.Add(BuildInputPanel(), 0, 0);
        content.Controls.Add(BuildOutputPanel(), 1, 0);
        root.Controls.Add(content, 0, 2);

        var footer = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 6 };
        footer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        for (var index = 1; index < 6; index++) footer.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 145));
        var save = CreateButton("Versiyonu Kaydet");
        save.Click += async (_, _) => await SaveVersionAsync();
        footer.Controls.Add(save, 1, 0);
        var history = CreateButton("Gecmis");
        history.Click += (_, _) =>
        {
            using var form = new ListingOptimizationHistoryForm(_historyService);
            form.ShowDialog(this);
        };
        footer.Controls.Add(history, 2, 0);
        var copyTitle = CreateButton("Baslik Kopyala");
        copyTitle.Click += (_, _) => CopyText(_titleSuggestionsTextBox.Lines.FirstOrDefault() ?? "");
        footer.Controls.Add(copyTitle, 3, 0);
        var copyTags = CreateButton("Tagleri Kopyala");
        copyTags.Click += (_, _) => CopyText(_tagSuggestionsTextBox.Text);
        footer.Controls.Add(copyTags, 4, 0);
        var close = CreateButton("Kapat");
        close.BackColor = Color.FromArgb(82, 93, 110);
        close.Click += (_, _) => Close();
        footer.Controls.Add(close, 5, 0);
        root.Controls.Add(footer, 0, 3);
    }

    private Control BuildInputPanel()
    {
        var panel = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 6, Padding = new Padding(0, 0, 12, 0) };
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 92));
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 112));
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
        panel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        panel.Controls.Add(SectionLabel("Mevcut baslik"), 0, 0);
        ConfigureMultiline(_titleTextBox);
        panel.Controls.Add(_titleTextBox, 0, 1);
        panel.Controls.Add(SectionLabel("Mevcut tagler"), 0, 2);
        ConfigureMultiline(_tagsTextBox);
        panel.Controls.Add(_tagsTextBox, 0, 3);
        panel.Controls.Add(SectionLabel("Mevcut aciklama"), 0, 4);
        ConfigureMultiline(_descriptionTextBox);
        panel.Controls.Add(_descriptionTextBox, 0, 5);
        return panel;
    }

    private Control BuildOutputPanel()
    {
        var panel = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 8 };
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 110));
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 96));
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
        panel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 118));
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 118));
        panel.Controls.Add(SectionLabel("Onerilen basliklar"), 0, 0);
        ConfigureMultiline(_titleSuggestionsTextBox, readOnly: true);
        panel.Controls.Add(_titleSuggestionsTextBox, 0, 1);
        panel.Controls.Add(SectionLabel("Onerilen tagler"), 0, 2);
        ConfigureMultiline(_tagSuggestionsTextBox, readOnly: true);
        panel.Controls.Add(_tagSuggestionsTextBox, 0, 3);
        panel.Controls.Add(SectionLabel("Aciklama taslagi"), 0, 4);
        ConfigureMultiline(_descriptionDraftTextBox, readOnly: true);
        panel.Controls.Add(_descriptionDraftTextBox, 0, 5);

        var lower = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2 };
        lower.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        lower.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        lower.Controls.Add(BuildListSection("Risk uyarilari", _riskListBox), 0, 0);
        lower.Controls.Add(BuildListSection("Aksiyon listesi", _checklistBox), 1, 0);
        panel.Controls.Add(lower, 0, 6);
        panel.SetRowSpan(lower, 2);
        return panel;
    }

    private static Control BuildListSection(string title, ListBox list)
    {
        var panel = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 2, Padding = new Padding(4) };
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
        panel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        panel.Controls.Add(SectionLabel(title), 0, 0);
        list.Dock = DockStyle.Fill;
        list.HorizontalScrollbar = true;
        panel.Controls.Add(list, 0, 1);
        return panel;
    }

    private void LoadListing(MarketListingResult listing, string targetKeyword)
    {
        _listingId = listing.ListingId.ToString(System.Globalization.CultureInfo.InvariantCulture);
        _listingTitle = listing.Title;
        _titleTextBox.Text = listing.Title;
        _tagsTextBox.Text = string.Join(", ", listing.Tags);
        _descriptionTextBox.Text = listing.Description;
        _keywordTextBox.Text = string.IsNullOrWhiteSpace(targetKeyword)
            ? listing.Tags.FirstOrDefault() ?? listing.Title.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? ""
            : targetKeyword;
    }

    private void Analyze()
    {
        var input = new ListingOptimizationInput(
            _titleTextBox.Text.Trim(),
            _descriptionTextBox.Text.Trim(),
            SplitTags(_tagsTextBox.Text),
            _keywordTextBox.Text.Trim());
        var result = _service.Optimize(input);
        _listingTitle = string.IsNullOrWhiteSpace(_listingTitle) ? input.Title : _listingTitle;
        ApplyResult(result);
    }

    private async Task GenerateWithAiAsync()
    {
        try
        {
            UseWaitCursor = true;
            var input = new ListingOptimizationInput(
                _titleTextBox.Text.Trim(),
                _descriptionTextBox.Text.Trim(),
                SplitTags(_tagsTextBox.Text),
                _keywordTextBox.Text.Trim());
            var result = await _aiOptimizer.OptimizeAsync(input);
            _listingTitle = string.IsNullOrWhiteSpace(_listingTitle) ? input.Title : _listingTitle;
            ApplyResult(result);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "AI Optimizasyon", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
        finally
        {
            UseWaitCursor = false;
        }
    }

    private void ApplyResult(ListingOptimizationResult result)
    {
        _lastResult = result;
        _scoreLabel.Text = $"SEO: {result.CurrentSeoScore}/100 -> {result.OptimizedSeoScore}/100";
        _titleSuggestionsTextBox.Text = string.Join(Environment.NewLine, result.TitleSuggestions);
        _tagSuggestionsTextBox.Text = string.Join(", ", result.TagSuggestions);
        _descriptionDraftTextBox.Text = result.DescriptionDraft;
        FillList(_riskListBox, result.RiskWarnings.Count > 0 ? result.RiskWarnings : ["Belirgin marka/telif riski bulunmadi."]);
        FillList(_checklistBox, result.ActionChecklist);
    }

    private async Task SaveVersionAsync()
    {
        if (_lastResult is null)
        {
            Analyze();
        }

        if (_lastResult is null) return;

        var saved = await _historyService.SaveAsync(new SaveListingOptimizationHistory(
            _listingId,
            string.IsNullOrWhiteSpace(_listingTitle) ? _titleTextBox.Text.Trim() : _listingTitle,
            _keywordTextBox.Text.Trim(),
            _lastResult));
        _scoreLabel.Text = $"Kaydedildi #{saved.Id} | SEO: {saved.CurrentSeoScore}/100 -> {saved.OptimizedSeoScore}/100";
    }

    private void CopyAll()
    {
        CopyText(
            $"BASLIK{Environment.NewLine}{_titleSuggestionsTextBox.Text}{Environment.NewLine}{Environment.NewLine}" +
            $"TAGLER{Environment.NewLine}{_tagSuggestionsTextBox.Text}{Environment.NewLine}{Environment.NewLine}" +
            $"ACIKLAMA{Environment.NewLine}{_descriptionDraftTextBox.Text}");
    }

    private static IReadOnlyList<string> SplitTags(string value) =>
        value.Split([",", ";", Environment.NewLine], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(tag => tag.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

    private static void FillList(ListBox list, IEnumerable<string> values)
    {
        list.Items.Clear();
        foreach (var value in values) list.Items.Add(value);
    }

    private static void ConfigureMultiline(ModernMultilineTextBox textBox, bool readOnly = false)
    {
        textBox.Dock = DockStyle.Fill;
        textBox.ReadOnly = readOnly;
    }

    private static Label SectionLabel(string text) => new()
    {
        Dock = DockStyle.Fill,
        Text = text,
        Font = new Font("Segoe UI Semibold", 10F),
        TextAlign = ContentAlignment.MiddleLeft,
    };

    private static Label LabelFor(string text) => new()
    {
        Dock = DockStyle.Fill,
        Text = text,
        TextAlign = ContentAlignment.MiddleLeft,
        ForeColor = Color.FromArgb(52, 61, 75),
    };

    private static Button CreateButton(string text)
    {
        var button = new Button
        {
            Dock = DockStyle.Fill,
            Text = text,
            BackColor = Color.FromArgb(32, 97, 165),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI Semibold", 9.5F),
            Margin = new Padding(6, 3, 0, 3),
            UseVisualStyleBackColor = false,
        };
        button.FlatAppearance.BorderSize = 0;
        return button;
    }

    private static void CopyText(string value)
    {
        if (!string.IsNullOrWhiteSpace(value)) Clipboard.SetText(value);
    }
}
