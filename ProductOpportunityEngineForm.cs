namespace SimilarProductsWinForms;

using System.Diagnostics;
using System.Globalization;
using System.Text;
using EtsyMarketPlace.Application.ListingOptimization;
using EtsyMarketPlace.Application.ProductOpportunity;
using SimilarProductsWinForms.Models;
using SimilarProductsWinForms.Services;

internal sealed class ProductOpportunityEngineForm(IAiListingOptimizer aiListingOptimizer) : Form
{
    private readonly EtsyApiClient _apiClient = new();
    private readonly ProductOpportunityScorer _scorer = new();
    private readonly HttpClient _imageHttpClient = new();
    private readonly BindingSource _bindingSource = new();
    private readonly DataGridView _grid = new();
    private readonly TextBox _shopTypeTextBox = new();
    private readonly TextBox _keywordTextBox = new();
    private readonly TextBox _includeTextBox = new();
    private readonly TextBox _excludeTextBox = new();
    private readonly NumericUpDown _limitInput = new() { Minimum = 10, Maximum = 100, Increment = 10, Value = 50 };
    private readonly ComboBox _sortComboBox = new() { DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly PictureBox _pictureBox = new() { Dock = DockStyle.Fill, SizeMode = PictureBoxSizeMode.Zoom, BackColor = Color.White };
    private readonly TextBox _detailTextBox = new();
    private readonly TextBox _aiTextBox = new();
    private readonly Label _statusLabel = new();
    private readonly Button _searchButton = new();
    private List<OpportunityRow> _rows = [];

    private OpportunityRow? SelectedRow => _bindingSource.Current as OpportunityRow;

    public ProductOpportunityEngineForm() : this(new OpenAiListingOptimizer(AiOptimizationSettingsStore.Load, new ListingOptimizationService()))
    {
    }

    protected override void OnLoad(EventArgs e)
    {
        base.OnLoad(e);
        BuildLayout();
    }

    private void BuildLayout()
    {
        Text = "Urun Firsat Motoru";
        StartPosition = FormStartPosition.CenterParent;
        WindowState = FormWindowState.Maximized;
        MinimumSize = new Size(1320, 820);
        Font = new Font("Segoe UI", 10F);
        BackColor = Color.FromArgb(247, 248, 250);

        var root = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 4, Padding = new Padding(18) };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 70));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 106));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 250));
        Controls.Add(root);

        var header = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2 };
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 540));
        header.Controls.Add(new Label
        {
            Dock = DockStyle.Fill,
            Text = "Urun Firsat Motoru",
            Font = new Font("Segoe UI Semibold", 22F),
            ForeColor = Color.FromArgb(23, 32, 49),
            TextAlign = ContentAlignment.MiddleLeft,
        }, 0, 0);
        _statusLabel.Dock = DockStyle.Fill;
        _statusLabel.Text = "Magaza turu ve anahtar kelime girerek firsat arayin";
        _statusLabel.TextAlign = ContentAlignment.MiddleRight;
        _statusLabel.ForeColor = Color.FromArgb(82, 93, 110);
        header.Controls.Add(_statusLabel, 1, 0);
        root.Controls.Add(header, 0, 0);

        root.Controls.Add(BuildToolbar(), 0, 1);
        ConfigureGrid();
        root.Controls.Add(_grid, 0, 2);
        root.Controls.Add(BuildDetailArea(), 0, 3);
    }

    private Control BuildToolbar()
    {
        var toolbar = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 12, RowCount = 2, Padding = new Padding(0, 8, 0, 8) };
        toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 105));
        toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 28));
        toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 105));
        toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 28));
        toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 72));
        toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 82));
        toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 124));
        toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 124));
        toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 124));
        toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 124));
        toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 150));
        toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 110));

        toolbar.Controls.Add(LabelFor("Magaza turu"), 0, 0);
        _shopTypeTextBox.Dock = DockStyle.Fill;
        _shopTypeTextBox.PlaceholderText = "Orn: 3D cosplay prop";
        toolbar.Controls.Add(_shopTypeTextBox, 1, 0);

        toolbar.Controls.Add(LabelFor("Ana keyword"), 2, 0);
        _keywordTextBox.Dock = DockStyle.Fill;
        _keywordTextBox.PlaceholderText = "Orn: fantasy bust, sword stand";
        _keywordTextBox.KeyDown += async (_, e) =>
        {
            if (e.KeyCode == Keys.Enter)
            {
                e.SuppressKeyPress = true;
                await SearchOpportunitiesAsync();
            }
        };
        toolbar.Controls.Add(_keywordTextBox, 3, 0);

        toolbar.Controls.Add(LabelFor("Limit"), 4, 0);
        _limitInput.Dock = DockStyle.Fill;
        toolbar.Controls.Add(_limitInput, 5, 0);

        ConfigureButton(_searchButton, "Firsat Ara");
        _searchButton.Click += async (_, _) => await SearchOpportunitiesAsync();
        toolbar.Controls.Add(_searchButton, 6, 0);

        var ai = CreateButton("AI Strateji");
        ai.Click += async (_, _) => await ExplainSelectedAsync();
        toolbar.Controls.Add(ai, 7, 0);

        var safe = CreateButton("Guvenli Ad");
        safe.Click += async (_, _) => await GenerateSafePositioningAsync();
        toolbar.Controls.Add(safe, 8, 0);

        var variation = CreateButton("Varyasyon");
        variation.Click += async (_, _) => await GenerateVariationIdeasAsync();
        toolbar.Controls.Add(variation, 9, 0);

        var send = CreateButton("Listinge Gonder");
        send.Click += (_, _) => SendToListingCreator();
        toolbar.Controls.Add(send, 10, 0);

        var close = CreateButton("Kapat");
        close.BackColor = Color.FromArgb(82, 93, 110);
        close.Click += (_, _) => Close();
        toolbar.Controls.Add(close, 11, 0);

        toolbar.Controls.Add(LabelFor("Dahil"), 0, 1);
        _includeTextBox.Dock = DockStyle.Fill;
        _includeTextBox.PlaceholderText = "Orn: bust, figure, prop";
        toolbar.Controls.Add(_includeTextBox, 1, 1);

        toolbar.Controls.Add(LabelFor("Haric"), 2, 1);
        _excludeTextBox.Dock = DockStyle.Fill;
        _excludeTextBox.PlaceholderText = "Orn: stl, file, digital";
        toolbar.Controls.Add(_excludeTextBox, 3, 1);

        toolbar.Controls.Add(LabelFor("Sirala"), 4, 1);
        _sortComboBox.Dock = DockStyle.Fill;
        _sortComboBox.Items.AddRange(["Firsat", "Talep", "Dusuk rekabet", "SEO boslugu", "Dusuk risk", "Fiyat"]);
        _sortComboBox.SelectedIndex = 0;
        _sortComboBox.SelectedIndexChanged += (_, _) => ApplySort();
        toolbar.Controls.Add(_sortComboBox, 5, 1);

        var open = CreateButton("Rakibi Ac");
        open.Click += (_, _) => OpenSelectedListing();
        toolbar.Controls.Add(open, 6, 1);

        var shop = CreateButton("Magazayi Ac");
        shop.Click += (_, _) => OpenSelectedShop();
        toolbar.Controls.Add(shop, 7, 1);

        var csv = CreateButton("CSV Aktar");
        csv.Click += (_, _) => ExportCsv();
        toolbar.Controls.Add(csv, 8, 1);

        var api = CreateButton("API Ayarlari");
        api.Click += (_, _) => { using var form = new EtsyApiSettingsForm(); form.ShowDialog(this); };
        toolbar.Controls.Add(api, 9, 1);
        toolbar.Controls.Add(new Panel { Dock = DockStyle.Fill }, 10, 1);
        toolbar.Controls.Add(new Panel { Dock = DockStyle.Fill }, 11, 1);
        return toolbar;
    }

    private Control BuildDetailArea()
    {
        var detail = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, Padding = new Padding(0, 10, 0, 0) };
        detail.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 235));
        detail.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 52));
        detail.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 48));

        _pictureBox.BorderStyle = BorderStyle.FixedSingle;
        detail.Controls.Add(_pictureBox, 0, 0);

        _detailTextBox.Dock = DockStyle.Fill;
        _detailTextBox.Multiline = true;
        _detailTextBox.ReadOnly = true;
        _detailTextBox.ScrollBars = ScrollBars.Vertical;
        _detailTextBox.BackColor = Color.White;
        detail.Controls.Add(_detailTextBox, 1, 0);

        _aiTextBox.Dock = DockStyle.Fill;
        _aiTextBox.Multiline = true;
        _aiTextBox.ReadOnly = true;
        _aiTextBox.ScrollBars = ScrollBars.Vertical;
        _aiTextBox.BackColor = Color.White;
        _aiTextBox.Text = "AI Strateji, Guvenli Ad veya Varyasyon butonuna basinca secili firsat icin analiz burada gorunecek.";
        detail.Controls.Add(_aiTextBox, 2, 0);
        return detail;
    }

    private void ConfigureGrid()
    {
        _grid.Dock = DockStyle.Fill;
        _grid.AutoGenerateColumns = false;
        _grid.AllowUserToAddRows = false;
        _grid.AllowUserToDeleteRows = false;
        _grid.AllowUserToResizeRows = false;
        _grid.ReadOnly = true;
        _grid.MultiSelect = false;
        _grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        _grid.RowHeadersVisible = false;
        _grid.BackgroundColor = Color.White;
        _grid.AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.AllCells;
        _grid.RowTemplate.MinimumHeight = 70;
        _grid.DefaultCellStyle.WrapMode = DataGridViewTriState.True;
        _grid.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI Semibold", 9.5F);
        _grid.DataSource = _bindingSource;
        _grid.SelectionChanged += (_, _) => UpdateDetail();
        _grid.CellDoubleClick += (_, _) => OpenSelectedListing();

        _grid.Columns.Add(new DataGridViewImageColumn
        {
            HeaderText = "Resim",
            DataPropertyName = nameof(OpportunityRow.Thumbnail),
            ImageLayout = DataGridViewImageCellLayout.Zoom,
            Width = 78,
        });
        AddColumn("Firsat", nameof(OpportunityRow.Opportunity), 76);
        AddColumn("Talep", nameof(OpportunityRow.Demand), 70);
        AddColumn("Rekabet", nameof(OpportunityRow.Competition), 78);
        AddColumn("Risk", nameof(OpportunityRow.Risk), 65);
        AddColumn("Karar", nameof(OpportunityRow.Decision), 135);
        AddColumn("Urun fikri", nameof(OpportunityRow.Title), 340, true);
        AddColumn("Fiyat", nameof(OpportunityRow.Price), 90);
        AddColumn("Magaza", nameof(OpportunityRow.Shop), 145);
        AddColumn("Magaza satisi", nameof(OpportunityRow.ShopSales), 105);
        AddColumn("Favori", nameof(OpportunityRow.Favorites), 75);
        AddColumn("Goruntulenme", nameof(OpportunityRow.Views), 105);
        AddColumn("SEO boslugu", nameof(OpportunityRow.SeoGap), 90);
        AddColumn("Kategori", nameof(OpportunityRow.Category), 190);
        AddColumn("Tagler", nameof(OpportunityRow.Tags), 300);
    }

    private async Task SearchOpportunitiesAsync()
    {
        var keyword = SearchKeyword();
        if (string.IsNullOrWhiteSpace(keyword))
        {
            MessageBox.Show(this, "Ana keyword veya magaza turu girin.", "Firsat Ara", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        try
        {
            _searchButton.Enabled = false;
            _searchButton.Text = "Araniyor...";
            _statusLabel.Text = "Etsy'de firsat adaylari araniyor...";
            var settings = EtsyApiSettingsStore.Load();
            if (!settings.HasApiCredentials)
            {
                using var form = new EtsyApiSettingsForm();
                form.ShowDialog(this);
                settings = EtsyApiSettingsStore.Load();
            }

            var listings = await _apiClient.FindMarketListingsAsync(settings, keyword, (int)_limitInput.Value);
            await EnrichCategoriesAsync(settings, listings);
            _rows = listings
                .Where(MatchesFilters)
                .Select(listing => new OpportunityRow(listing, _scorer.Score(ToOpportunityInput(listing))))
                .OrderByDescending(row => row.Score.Opportunity)
                .ToList();
            ApplySort();
            _ = LoadThumbnailsAsync(_rows);
            _statusLabel.Text = $"{_rows.Count} firsat adayi listelendi";
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Firsat Ara", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            _statusLabel.Text = "Firsat aramasi basarisiz";
        }
        finally
        {
            _searchButton.Enabled = true;
            _searchButton.Text = "Firsat Ara";
        }
    }

    private async Task ExplainSelectedAsync()
    {
        await RunAiOpportunityAnalysisAsync(
            "AI STRATEJI",
            "AI firsat stratejisi yaziyor...",
            "AI firsat yorumu hazir",
            "AI stratejisi alinamadi",
            BuildAiOpportunityPrompt);
    }

    private async Task GenerateSafePositioningAsync()
    {
        await RunAiOpportunityAnalysisAsync(
            "GUVENLI ADLANDIRMA",
            "AI guvenli urun konumu yaziyor...",
            "Guvenli adlandirma hazir",
            "Guvenli adlandirma alinamadi",
            BuildSafePositioningPrompt);
    }

    private async Task GenerateVariationIdeasAsync()
    {
        await RunAiOpportunityAnalysisAsync(
            "VARYASYON FIKIRLERI",
            "AI varyasyon fikirleri yaziyor...",
            "Varyasyon fikirleri hazir",
            "Varyasyon fikirleri alinamadi",
            BuildVariationPrompt);
    }

    private async Task RunAiOpportunityAnalysisAsync(
        string heading,
        string loadingStatus,
        string successStatus,
        string failedStatus,
        Func<OpportunityRow, string> promptFactory)
    {
        var row = SelectedRow;
        if (row is null)
        {
            MessageBox.Show(this, "AI analizi icin bir firsat secin.", heading, MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        try
        {
            UseWaitCursor = true;
            _statusLabel.Text = loadingStatus;
            var input = new ListingOptimizationInput(
                $"Opportunity product: {row.Listing.Title}",
                promptFactory(row),
                row.Listing.Tags,
                SearchKeyword());
            var result = await aiListingOptimizer.OptimizeAsync(input);
            _aiTextBox.Text = FormatAiOpportunityResult(heading, row, result);
            _statusLabel.Text = successStatus;
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, heading, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            _statusLabel.Text = failedStatus;
        }
        finally
        {
            UseWaitCursor = false;
        }
    }

    private void SendToListingCreator()
    {
        var keyword = SelectedRow?.Listing.Title ?? SearchKeyword();
        using var form = new ProductDiscoveryListingCreatorForm(aiListingOptimizer, keyword);
        form.ShowDialog(this);
    }

    private void ApplySort()
    {
        IEnumerable<OpportunityRow> sorted = _sortComboBox.SelectedItem?.ToString() switch
        {
            "Talep" => _rows.OrderByDescending(row => row.Score.Demand),
            "Dusuk rekabet" => _rows.OrderBy(row => row.Score.Competition).ThenByDescending(row => row.Score.Opportunity),
            "SEO boslugu" => _rows.OrderByDescending(row => row.Score.SeoGap),
            "Dusuk risk" => _rows.OrderBy(row => row.Score.Risk).ThenByDescending(row => row.Score.Opportunity),
            "Fiyat" => _rows.OrderByDescending(row => row.Score.PricePotential),
            _ => _rows.OrderByDescending(row => row.Score.Opportunity),
        };

        var list = sorted.ToList();
        _bindingSource.DataSource = list;
        _bindingSource.Position = list.Count > 0 ? 0 : -1;
        UpdateDetail();
    }

    private void UpdateDetail()
    {
        var row = SelectedRow;
        if (row is null)
        {
            _pictureBox.Image = null;
            _detailTextBox.Text = "Firsat secilmedi.";
            return;
        }

        var listing = row.Listing;
        _pictureBox.Image = listing.ThumbnailImage;
        _detailTextBox.Text =
            $"URUN{Environment.NewLine}{listing.Title}{Environment.NewLine}{Environment.NewLine}" +
            $"FIRSAT SKORLARI{Environment.NewLine}" +
            $"Firsat: {row.Score.Opportunity}/100 | Talep: {row.Score.Demand}/100 | Rekabet: {row.Score.Competition}/100 | Risk: {row.Score.Risk}/100{Environment.NewLine}" +
            $"SEO boslugu: {row.Score.SeoGap}/100 | Fiyat potansiyeli: {row.Score.PricePotential}/100 | Karar: {row.Score.Decision}{Environment.NewLine}{Environment.NewLine}" +
            $"NEDEN{Environment.NewLine}{string.Join(Environment.NewLine, row.Score.Reasons.Select(reason => "- " + reason))}{Environment.NewLine}{Environment.NewLine}" +
            $"MAGAZA / FIYAT{Environment.NewLine}{listing.ShopName} | {listing.PriceDisplay} | Magaza satisi: {listing.ShopSalesDisplay}{Environment.NewLine}" +
            $"Favori: {listing.Favorites:N0} | Goruntulenme: {listing.ViewsDisplay} | Stok: {listing.Quantity}{Environment.NewLine}{Environment.NewLine}" +
            $"KATEGORI{Environment.NewLine}{listing.TaxonomyDisplay}{Environment.NewLine}{Environment.NewLine}" +
            $"TAGLER{Environment.NewLine}{listing.TagsDisplay}";
    }

    private ProductOpportunityInput ToOpportunityInput(MarketListingResult listing) =>
        new(
            listing.Title,
            listing.Description,
            listing.Tags,
            listing.Price,
            listing.Favorites,
            listing.Views,
            listing.ShopSales,
            listing.SeoScore,
            listing.Quantity,
            SearchKeyword(),
            listing.TaxonomyDisplay);

    private string BuildAiOpportunityPrompt(OpportunityRow row) =>
        BuildBaseAiPrompt(row) +
        "Task: Analyze this Etsy product as a product opportunity for a seller. " +
        "Use risk_warnings and action_checklist for practical Turkish notes. Keep proposed Etsy title, tags and description_draft in English. " +
        "Focus on why it can sell, competition, SEO gap, price angle, visual angle, safer generic positioning, and first listing test plan.";

    private string BuildSafePositioningPrompt(OpportunityRow row) =>
        BuildBaseAiPrompt(row) +
        "Task: Create a safer generic positioning plan for this opportunity. " +
        "Do not use trademark, brand, copyrighted character, movie, game, anime, or celebrity names in title or tags. " +
        "Keep product type, material, buyer intent, style, room/use case, size, color and gift angle. " +
        "Return English title suggestions, English tags, English materials, and a short English description draft. " +
        "Use Turkish risk_warnings to explain which risky words were removed and why.";

    private string BuildVariationPrompt(OpportunityRow row) =>
        BuildBaseAiPrompt(row) +
        "Task: Suggest practical listing variations for testing this opportunity. " +
        "Use title_suggestions as different English variation titles. Use tag_suggestions as reusable English SEO tags. " +
        "Use description_draft to explain the best first variation in English. " +
        "Use Turkish risk_warnings and action_checklist to list price tests, visual tests, bundle ideas, personalization options, and publish warnings.";

    private string BuildBaseAiPrompt(OpportunityRow row)
    {
        var input = ToOpportunityInput(row.Listing);
        var riskTerms = _scorer.DetectRiskTerms(input);
        return
            $"Shop type: {_shopTypeTextBox.Text.Trim()}\n" +
            $"Keyword: {SearchKeyword()}\n" +
            $"Scores: opportunity {row.Score.Opportunity}, demand {row.Score.Demand}, competition {row.Score.Competition}, risk {row.Score.Risk}, seo gap {row.Score.SeoGap}\n" +
            $"Detected risky terms: {string.Join(", ", riskTerms.DefaultIfEmpty("none"))}\n" +
            $"Decision: {row.Score.Decision}\n" +
            $"Reasons: {string.Join(" | ", row.Score.Reasons)}\n" +
            $"Category: {row.Listing.TaxonomyDisplay}\n" +
            $"Price: {row.Listing.PriceDisplay}\n" +
            $"Shop sales: {row.Listing.ShopSalesDisplay}\n" +
            $"Favorites: {row.Listing.Favorites:N0}\n" +
            $"Views: {row.Listing.ViewsDisplay}\n" +
            $"Tags: {row.Listing.TagsDisplay}\n" +
            $"Description: {row.Listing.Description}\n\n";
    }

    private string FormatAiOpportunityResult(string heading, OpportunityRow row, ListingOptimizationResult result)
    {
        var riskTerms = _scorer.DetectRiskTerms(ToOpportunityInput(row.Listing));
        return
            $"{heading}{Environment.NewLine}" +
            $"Secili urun: {row.Listing.Title}{Environment.NewLine}" +
            $"Skor: {row.Score.Opportunity}/100 | Talep: {row.Score.Demand}/100 | Rekabet: {row.Score.Competition}/100 | Risk: {row.Score.Risk}/100{Environment.NewLine}" +
            $"Riskli terimler: {string.Join(", ", riskTerms.DefaultIfEmpty("-"))}{Environment.NewLine}{Environment.NewLine}" +
            $"INGILIZCE LISTING TASLAGI / STRATEJI{Environment.NewLine}{result.DescriptionDraft}{Environment.NewLine}{Environment.NewLine}" +
            $"INGILIZCE BASLIK ONERILERI{Environment.NewLine}{string.Join(Environment.NewLine, result.TitleSuggestions.DefaultIfEmpty("-"))}{Environment.NewLine}{Environment.NewLine}" +
            $"INGILIZCE TAG ONERILERI{Environment.NewLine}{string.Join(", ", result.TagSuggestions.DefaultIfEmpty("-"))}{Environment.NewLine}{Environment.NewLine}" +
            $"MATERYAL ONERILERI{Environment.NewLine}{string.Join(", ", result.MaterialSuggestions.DefaultIfEmpty("-"))}{Environment.NewLine}{Environment.NewLine}" +
            $"TURKCE RISK / NOTLAR{Environment.NewLine}{string.Join(Environment.NewLine, result.RiskWarnings.DefaultIfEmpty("-"))}{Environment.NewLine}{Environment.NewLine}" +
            $"AKSIYON LISTESI{Environment.NewLine}{string.Join(Environment.NewLine, result.ActionChecklist.DefaultIfEmpty("-"))}";
    }

    private string SearchKeyword()
    {
        var keyword = _keywordTextBox.Text.Trim();
        if (keyword.Length > 0) return keyword;
        return _shopTypeTextBox.Text.Trim();
    }

    private bool MatchesFilters(MarketListingResult listing)
    {
        var searchable = $"{listing.Title} {listing.Description} {listing.TagsDisplay}".ToLowerInvariant();
        var includeTerms = SplitTerms(_includeTextBox.Text);
        var excludeTerms = SplitTerms(_excludeTextBox.Text);
        if (excludeTerms.Any(term => searchable.Contains(term, StringComparison.OrdinalIgnoreCase)))
        {
            return false;
        }

        return includeTerms.Count == 0 || includeTerms.Any(term => searchable.Contains(term, StringComparison.OrdinalIgnoreCase));
    }

    private static IReadOnlyList<string> SplitTerms(string value) =>
        value.Split([',', ';', '|'], StringSplitOptions.RemoveEmptyEntries)
            .Select(term => term.Trim().ToLowerInvariant())
            .Where(term => term.Length > 0)
            .ToList();

    private async Task EnrichCategoriesAsync(EtsyApiSettings settings, IReadOnlyList<MarketListingResult> listings)
    {
        var ids = listings.Select(listing => listing.TaxonomyId).Where(id => id > 0).Distinct().ToList();
        if (ids.Count == 0) return;
        var names = await _apiClient.GetSellerTaxonomyNamesAsync(settings, ids);
        foreach (var listing in listings)
        {
            if (names.TryGetValue(listing.TaxonomyId, out var name))
            {
                listing.TaxonomyName = name;
            }
        }
    }

    private async Task LoadThumbnailsAsync(IReadOnlyList<OpportunityRow> rows)
    {
        foreach (var row in rows)
        {
            var imageUrl = row.Listing.ImageUrls.FirstOrDefault();
            if (string.IsNullOrWhiteSpace(imageUrl)) imageUrl = row.Listing.ImageUrl;
            if (string.IsNullOrWhiteSpace(imageUrl)) continue;

            try
            {
                using var stream = await _imageHttpClient.GetStreamAsync(imageUrl);
                using var image = Image.FromStream(stream);
                row.Listing.ThumbnailImage = CreateThumbnail(image, 74, 58);
            }
            catch
            {
                row.Listing.ThumbnailImage = null;
            }
        }

        _grid.Refresh();
        UpdateDetail();
    }

    private static Image CreateThumbnail(Image source, int width, int height)
    {
        var bitmap = new Bitmap(width, height);
        using var graphics = Graphics.FromImage(bitmap);
        graphics.Clear(Color.White);
        graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
        var ratio = Math.Min((float)width / source.Width, (float)height / source.Height);
        var targetWidth = (int)(source.Width * ratio);
        var targetHeight = (int)(source.Height * ratio);
        var x = (width - targetWidth) / 2;
        var y = (height - targetHeight) / 2;
        graphics.DrawImage(source, x, y, targetWidth, targetHeight);
        return bitmap;
    }

    private void OpenSelectedListing() => OpenUrl(SelectedRow?.Listing.ListingUrl);
    private void OpenSelectedShop() => OpenUrl(SelectedRow?.Listing.ShopUrl);

    private static void OpenUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url)) return;
        Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
    }

    private void ExportCsv()
    {
        if (_rows.Count == 0)
        {
            MessageBox.Show(this, "Aktarilacak firsat yok.", "CSV Aktar", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        using var dialog = new SaveFileDialog
        {
            Filter = "CSV files|*.csv",
            FileName = $"urun-firsatlari-{DateTime.Now:yyyyMMdd-HHmm}.csv",
        };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;

        var builder = new StringBuilder();
        builder.AppendLine("Opportunity,Demand,Competition,Risk,Decision,Title,Price,Shop,ShopSales,Favorites,Views,Category,Url");
        foreach (var row in _rows)
        {
            builder.AppendLine(string.Join(',', new[]
            {
                row.Score.Opportunity.ToString(CultureInfo.InvariantCulture),
                row.Score.Demand.ToString(CultureInfo.InvariantCulture),
                row.Score.Competition.ToString(CultureInfo.InvariantCulture),
                row.Score.Risk.ToString(CultureInfo.InvariantCulture),
                Csv(row.Score.Decision),
                Csv(row.Listing.Title),
                Csv(row.Listing.PriceDisplay),
                Csv(row.Listing.ShopName),
                row.Listing.ShopSales.ToString(CultureInfo.InvariantCulture),
                row.Listing.Favorites.ToString(CultureInfo.InvariantCulture),
                row.Listing.Views.ToString(CultureInfo.InvariantCulture),
                Csv(row.Listing.TaxonomyDisplay),
                Csv(row.Listing.ListingUrl),
            }));
        }

        File.WriteAllText(dialog.FileName, builder.ToString(), Encoding.UTF8);
        _statusLabel.Text = "Firsatlar CSV olarak aktarildi";
    }

    private static string Csv(string value) => $"\"{value.Replace("\"", "\"\"")}\"";

    private static Label LabelFor(string text) => new()
    {
        Dock = DockStyle.Fill,
        Text = text,
        TextAlign = ContentAlignment.MiddleLeft,
        ForeColor = Color.FromArgb(23, 32, 49),
    };

    private Button CreateButton(string text)
    {
        var button = new Button();
        ConfigureButton(button, text);
        return button;
    }

    private static void ConfigureButton(Button button, string text)
    {
        button.Dock = DockStyle.Fill;
        button.Text = text;
        button.BackColor = Color.FromArgb(38, 101, 166);
        button.ForeColor = Color.White;
        button.FlatStyle = FlatStyle.Flat;
        button.FlatAppearance.BorderSize = 0;
        button.Margin = new Padding(4);
    }

    private void AddColumn(string header, string property, int width, bool fill = false)
    {
        _grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            HeaderText = header,
            DataPropertyName = property,
            Width = width,
            AutoSizeMode = fill ? DataGridViewAutoSizeColumnMode.Fill : DataGridViewAutoSizeColumnMode.None,
        });
    }

    private sealed class OpportunityRow(MarketListingResult listing, ProductOpportunityScore score)
    {
        public MarketListingResult Listing { get; } = listing;
        public ProductOpportunityScore Score { get; } = score;
        public Image? Thumbnail => Listing.ThumbnailImage;
        public string Opportunity => $"{Score.Opportunity}/100";
        public string Demand => $"{Score.Demand}/100";
        public string Competition => $"{Score.Competition}/100";
        public string SeoGap => $"{Score.SeoGap}/100";
        public string Risk => $"{Score.Risk}/100";
        public string Decision => Score.Decision;
        public string Title => Listing.Title;
        public string Price => Listing.PriceDisplay;
        public string Shop => Listing.ShopName;
        public string ShopSales => Listing.ShopSalesDisplay;
        public string Favorites => Listing.Favorites.ToString("N0");
        public string Views => Listing.ViewsDisplay;
        public string Category => Listing.TaxonomyDisplay;
        public string Tags => Listing.TagsDisplay;
    }
}
