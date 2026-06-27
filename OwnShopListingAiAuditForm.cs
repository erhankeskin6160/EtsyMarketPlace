namespace SimilarProductsWinForms;

using System.Diagnostics;
using System.Globalization;
using EtsyMarketPlace.Application.ListingOptimization;
using SimilarProductsWinForms.Models;
using SimilarProductsWinForms.Services;

internal sealed class OwnShopListingAiAuditForm(
    IAiListingOptimizer aiOptimizer,
    ListingOptimizationHistoryService historyService) : Form
{
    private readonly EtsyApiClient _apiClient = new();
    private readonly HttpClient _imageHttpClient = new();
    private readonly BindingSource _bindingSource = new();
    private readonly DataGridView _grid = new();
    private readonly PictureBox _pictureBox = new();
    private readonly TextBox _detailTextBox = new();
    private readonly TextBox _suggestionTextBox = new();
    private readonly TextBox _searchTextBox = new();
    private readonly Label _statusLabel = new();
    private readonly NumericUpDown _limitInput = new() { Minimum = 10, Maximum = 100, Increment = 10, Value = 50 };
    private List<AuditRow> _rows = [];
    private ListingOptimizationResult? _lastResult;
    private long _lastResultListingId;

    protected override void OnLoad(EventArgs e)
    {
        base.OnLoad(e);
        BuildLayout();
    }

    private AuditRow? SelectedRow => _bindingSource.Current as AuditRow;

    private void BuildLayout()
    {
        Text = "Kendi Magaza Listing AI Analizi";
        StartPosition = FormStartPosition.CenterParent;
        WindowState = FormWindowState.Maximized;
        MinimumSize = new Size(1280, 780);
        Font = new Font("Segoe UI", 10F);
        BackColor = Color.FromArgb(247, 248, 250);

        var root = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 4, Padding = new Padding(18) };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 72));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 58));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 250));
        Controls.Add(root);

        var header = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2 };
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 420));
        header.Controls.Add(new Label
        {
            Dock = DockStyle.Fill,
            Text = "Kendi Magaza Listing AI Analizi",
            Font = new Font("Segoe UI Semibold", 22F),
            ForeColor = Color.FromArgb(23, 32, 49),
            TextAlign = ContentAlignment.MiddleLeft,
        }, 0, 0);
        _statusLabel.Dock = DockStyle.Fill;
        _statusLabel.TextAlign = ContentAlignment.MiddleRight;
        _statusLabel.ForeColor = Color.FromArgb(82, 93, 110);
        _statusLabel.Text = "Listingleri yuklemek icin yenile";
        header.Controls.Add(_statusLabel, 1, 0);
        root.Controls.Add(header, 0, 0);

        var toolbar = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 11 };
        toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 90));
        toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 90));
        toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 95));
        toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        for (var index = 4; index < 11; index++) toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 132));
        toolbar.Controls.Add(LabelFor("Limit"), 0, 0);
        _limitInput.Dock = DockStyle.Left;
        toolbar.Controls.Add(_limitInput, 1, 0);
        toolbar.Controls.Add(LabelFor("Urun ara"), 2, 0);
        _searchTextBox.Dock = DockStyle.Fill;
        _searchTextBox.PlaceholderText = "Urun adina gore filtrele...";
        _searchTextBox.TextChanged += (_, _) => ApplyFilter();
        toolbar.Controls.Add(_searchTextBox, 3, 0);
        var load = CreateButton("Listingleri Yukle");
        load.Click += async (_, _) => await LoadListingsAsync();
        toolbar.Controls.Add(load, 4, 0);
        var aiAnalyze = CreateButton("AI ile Puanla");
        aiAnalyze.Click += async (_, _) => await AnalyzeSelectedAsync();
        toolbar.Controls.Add(aiAnalyze, 5, 0);
        var settings = CreateButton("AI Ayarlari");
        settings.Click += (_, _) => { using var form = new AiOptimizationSettingsForm(); form.ShowDialog(this); };
        toolbar.Controls.Add(settings, 6, 0);
        var refreshSelected = CreateButton("AI Sonrasi Yenile");
        refreshSelected.Click += async (_, _) => await RefreshSelectedListingAsync();
        toolbar.Controls.Add(refreshSelected, 7, 0);
        var aiImage = CreateButton("AI Gorsel");
        aiImage.Click += async (_, _) => await OpenAiImageWorkflowAsync();
        toolbar.Controls.Add(aiImage, 8, 0);
        var open = CreateButton("Listing Ac");
        open.Click += (_, _) => OpenListing();
        toolbar.Controls.Add(open, 9, 0);
        var history = CreateButton("Gecmis");
        history.Click += (_, _) => { using var form = new ListingOptimizationHistoryForm(historyService); form.ShowDialog(this); };
        toolbar.Controls.Add(history, 10, 0);
        root.Controls.Add(toolbar, 0, 1);

        ConfigureGrid();
        root.Controls.Add(_grid, 0, 2);
        root.Controls.Add(BuildDetailArea(), 0, 3);
    }

    private Control BuildDetailArea()
    {
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 4, Padding = new Padding(0, 12, 0, 0) };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 220));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 45));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 55));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 175));

        _pictureBox.Dock = DockStyle.Fill;
        _pictureBox.SizeMode = PictureBoxSizeMode.Zoom;
        _pictureBox.BackColor = Color.White;
        _pictureBox.BorderStyle = BorderStyle.FixedSingle;
        layout.Controls.Add(_pictureBox, 0, 0);

        ConfigureText(_detailTextBox);
        layout.Controls.Add(_detailTextBox, 1, 0);
        ConfigureText(_suggestionTextBox);
        layout.Controls.Add(_suggestionTextBox, 2, 0);

        var actions = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 5 };
        for (var row = 0; row < 5; row++) actions.RowStyles.Add(new RowStyle(SizeType.Percent, 20));
        var save = CreateButton("Versiyon Kaydet");
        save.Click += async (_, _) => await SaveVersionAsync();
        actions.Controls.Add(save, 0, 0);
        var copy = CreateButton("Oneriyi Kopyala");
        copy.Click += (_, _) => CopySuggestion();
        actions.Controls.Add(copy, 0, 1);
        var draft = CreateButton("Etsy'de Guncelle");
        draft.Click += async (_, _) => await UpdateListingWithConfirmationAsync();
        actions.Controls.Add(draft, 0, 2);
        var shop = CreateButton("Magaza Ac");
        shop.Click += (_, _) => OpenShop();
        actions.Controls.Add(shop, 0, 3);
        var close = CreateButton("Kapat");
        close.BackColor = Color.FromArgb(82, 93, 110);
        close.Click += (_, _) => Close();
        actions.Controls.Add(close, 0, 4);
        layout.Controls.Add(actions, 3, 0);
        return layout;
    }

    private void ConfigureGrid()
    {
        _grid.Dock = DockStyle.Fill;
        _grid.AutoGenerateColumns = false;
        _grid.AllowUserToAddRows = false;
        _grid.AllowUserToDeleteRows = false;
        _grid.ReadOnly = true;
        _grid.MultiSelect = false;
        _grid.RowHeadersVisible = false;
        _grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        _grid.BackgroundColor = Color.White;
        _grid.RowTemplate.MinimumHeight = 78;
        _grid.DataSource = _bindingSource;
        _grid.SelectionChanged += async (_, _) => await UpdateDetailAsync();
        _grid.Columns.Add(new DataGridViewImageColumn
        {
            HeaderText = "Resim",
            DataPropertyName = nameof(AuditRow.ThumbnailImage),
            Width = 90,
            ImageLayout = DataGridViewImageCellLayout.Zoom,
        });
        AddColumn("#", nameof(AuditRow.Rank), 48);
        AddColumn("Listing", nameof(AuditRow.Title), 380, true);
        AddColumn("SEO", nameof(AuditRow.SeoScore), 70);
        AddColumn("AI", nameof(AuditRow.AiScore), 70);
        AddColumn("SEO Eksikler", nameof(AuditRow.SeoNeeds), 180);
        AddColumn("Artilar", nameof(AuditRow.SeoStrengths), 180);
        AddColumn("Fiyat", nameof(AuditRow.Price), 85);
        AddColumn("Favori", nameof(AuditRow.Favorites), 80);
        AddColumn("Stok", nameof(AuditRow.Quantity), 70);
        AddColumn("Tag", nameof(AuditRow.TagCount), 70);
        AddColumn("Durum", nameof(AuditRow.Status), 150);
    }

    private async Task LoadListingsAsync()
    {
        try
        {
            UseWaitCursor = true;
            _statusLabel.Text = "Kendi listinglerin aliniyor...";
            var settings = EtsyApiSettingsStore.Load();
            var listings = await _apiClient.GetOwnShopActiveListingsAsync(settings, (int)_limitInput.Value);
            EtsyApiSettingsStore.Save(settings);
            _rows = listings
                .Select((item, index) => new AuditRow(index + 1, item, ScoreListing(item), "Bekliyor"))
                .OrderBy(row => row.SeoScore)
                .ThenBy(row => row.Title)
                .ToList();
            ApplyFilter();
            _statusLabel.Text = $"{_rows.Count} listing yuklendi | dusuk puanlar ustte";
            await LoadThumbnailsAsync(settings);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Kendi Listing AI Analizi", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            _statusLabel.Text = "Listingler alinamadi";
        }
        finally
        {
            UseWaitCursor = false;
        }
    }

    private async Task AnalyzeSelectedAsync()
    {
        if (SelectedRow is null) return;
        try
        {
            UseWaitCursor = true;
            var row = SelectedRow;
            var input = ToOptimizationInput(row.Listing);
            _lastResult = await aiOptimizer.OptimizeAsync(input);
            _lastResultListingId = row.Listing.ListingId;
            row.AiScore = _lastResult.OptimizedSeoScore;
            row.Status = _lastResult.RiskWarnings.Count > 0 ? "Risk kontrol" : "Oneri hazir";
            _grid.Refresh();
            RenderSuggestion(row, _lastResult);
            _statusLabel.Text = $"{row.Title} analiz edildi";
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "AI Analiz", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
        finally
        {
            UseWaitCursor = false;
        }
    }

    private async Task SaveVersionAsync()
    {
        if (SelectedRow is null || _lastResult is null) return;
        var row = SelectedRow;
        await historyService.SaveAsync(new SaveListingOptimizationHistory(
            row.Listing.ListingId.ToString(CultureInfo.InvariantCulture),
            row.Title,
            PrimaryKeyword(row.Listing),
            _lastResult));
        _statusLabel.Text = "Optimizasyon versiyonu kaydedildi";
    }

    private async Task UpdateDetailAsync()
    {
        if (SelectedRow is null)
        {
            _detailTextBox.Text = "Listing secilmedi.";
            _suggestionTextBox.Clear();
            _pictureBox.Image = null;
            return;
        }

        var row = SelectedRow;
        _detailTextBox.Text =
            $"BASLIK{Environment.NewLine}{row.Title}{Environment.NewLine}{Environment.NewLine}" +
            $"PUANLAR{Environment.NewLine}SEO: {row.SeoScore}/100 | AI: {row.AiScore}/100 | Durum: {row.Status}{Environment.NewLine}{Environment.NewLine}" +
            $"VERI{Environment.NewLine}Fiyat: {row.Price} | Favori: {row.Favorites:N0} | Stok: {row.Quantity} | Tag: {row.TagCount}{Environment.NewLine}{Environment.NewLine}" +
            $"TAGLER{Environment.NewLine}{string.Join(", ", row.Listing.Tags)}";
        _suggestionTextBox.Text = "AI ile Puanla butonuna basinca oneriler burada gorunecek.";
        if (row.ThumbnailImage is not null)
        {
            _pictureBox.Image = row.ThumbnailImage;
        }
        else
        {
            var settings = EtsyApiSettingsStore.Load();
            await LoadThumbnailAsync(row, settings);
        }
    }

    private async Task LoadThumbnailsAsync(EtsyApiSettings settings)
    {
        foreach (var row in _rows.Where(item => item.ThumbnailImage is null).Take(30))
        {
            await LoadThumbnailAsync(row, settings);
        }
        _bindingSource.ResetBindings(false);
        _grid.Invalidate();
    }

    private async Task LoadThumbnailAsync(AuditRow row, EtsyApiSettings settings)
    {
        var imageUrl = row.Listing.ImageUrl;
        if (string.IsNullOrWhiteSpace(imageUrl) && row.Listing.ListingId > 0)
        {
            try
            {
                var urls = await _apiClient.GetListingImagesAsync(settings, row.Listing.ListingId);
                row.Listing.ImageUrls = urls.ToList();
                imageUrl = urls.FirstOrDefault() ?? "";
            }
            catch
            {
                imageUrl = "";
            }
        }

        if (string.IsNullOrWhiteSpace(imageUrl)) return;
        try
        {
            await using var stream = await _imageHttpClient.GetStreamAsync(imageUrl);
            using var memory = new MemoryStream();
            await stream.CopyToAsync(memory);
            memory.Position = 0;
            using var image = Image.FromStream(memory);
            row.ThumbnailImage = new Bitmap(image);
            if (ReferenceEquals(row, SelectedRow)) _pictureBox.Image = row.ThumbnailImage;
            _bindingSource.ResetBindings(false);
            _grid.Invalidate();
        }
        catch
        {
            row.ThumbnailImage = null;
        }
    }

    private void RenderSuggestion(AuditRow row, ListingOptimizationResult result)
    {
        _suggestionTextBox.Text =
            $"ONERILEN BASLIK{Environment.NewLine}{result.TitleSuggestions.FirstOrDefault()}{Environment.NewLine}{Environment.NewLine}" +
            $"ONERILEN TAGLER{Environment.NewLine}{string.Join(", ", result.TagSuggestions)}{Environment.NewLine}{Environment.NewLine}" +
            $"ONERILEN MATERYALLER{Environment.NewLine}{string.Join(", ", result.MaterialSuggestions)}{Environment.NewLine}{Environment.NewLine}" +
            $"ACIKLAMA TASLAGI{Environment.NewLine}{result.DescriptionDraft}{Environment.NewLine}{Environment.NewLine}" +
            $"RISKLER{Environment.NewLine}{string.Join(Environment.NewLine, result.RiskWarnings.DefaultIfEmpty("Risk uyarisi yok."))}";
    }

    private static int ScoreListing(MarketListingResult listing)
    {
        var tagScore = Math.Min(25, listing.Tags.Count * 25 / 13);
        var titleScore = listing.Title.Length is >= 55 and <= 135 ? 25 : listing.Title.Length is >= 35 and <= 140 ? 18 : 8;
        var imageScore = listing.ImageUrls.Count >= 5 ? 20 : listing.ImageUrls.Count * 4;
        var descriptionScore = listing.Description.Length >= 500 ? 20 : listing.Description.Length >= 250 ? 12 : 5;
        var signalScore = listing.Favorites > 0 ? 10 : 0;
        return Math.Clamp(tagScore + titleScore + imageScore + descriptionScore + signalScore, 0, 100);
    }

    private static ListingOptimizationInput ToOptimizationInput(MarketListingResult listing) =>
        new(listing.Title, listing.Description, listing.Tags, PrimaryKeyword(listing));

    private static string PrimaryKeyword(MarketListingResult listing) =>
        listing.Tags.FirstOrDefault(tag => tag.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length >= 2)
        ?? listing.Tags.FirstOrDefault()
        ?? listing.Title.Split(' ', StringSplitOptions.RemoveEmptyEntries).Take(3).Aggregate("", (current, next) => $"{current} {next}").Trim();

    private void CopySuggestion()
    {
        if (!string.IsNullOrWhiteSpace(_suggestionTextBox.Text)) Clipboard.SetText(_suggestionTextBox.Text);
    }

    private async Task UpdateListingWithConfirmationAsync()
    {
        if (SelectedRow is null)
        {
            MessageBox.Show(this, "Once bir listing secin.", "Listing guncelleme", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        if (_lastResult is null || _lastResultListingId != SelectedRow.Listing.ListingId)
        {
            MessageBox.Show(this, "Once secili listing icin AI ile Puanla calistirin.", "Listing guncelleme", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        var update = CreateListingUpdate(_lastResult);
        if (!ValidateListingUpdate(update, out var validationMessage))
        {
            MessageBox.Show(this, validationMessage, "Listing guncelleme", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        using var confirmation = new ListingUpdateConfirmationForm(SelectedRow.Listing, update);
        if (confirmation.ShowDialog(this) != DialogResult.OK || !confirmation.Confirmed)
        {
            return;
        }

        try
        {
            UseWaitCursor = true;
            var settings = EtsyApiSettingsStore.Load();
            await _apiClient.UpdateOwnShopListingTextAsync(settings, SelectedRow.Listing.ListingId, update);
            EtsyApiSettingsStore.Save(settings);
            await SaveVersionAsync();
            SelectedRow.Status = "Etsy guncellendi";
            _grid.Refresh();
            _statusLabel.Text = $"Listing Etsy'de guncellendi: {SelectedRow.Title}";
            MessageBox.Show(this, "Listing Etsy'de guncellendi. Degisikligi Etsy sayfasinda kontrol edin.", "Listing guncelleme", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Listing guncelleme", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
        finally
        {
            UseWaitCursor = false;
        }
    }

    private static ListingTextUpdate CreateListingUpdate(ListingOptimizationResult result) =>
        new(
            result.TitleSuggestions.FirstOrDefault()?.Trim() ?? "",
            result.DescriptionDraft.Trim(),
            result.TagSuggestions.Select(tag => tag.Trim()).Where(tag => tag.Length > 0).Take(13).ToList(),
            EtsyApiClient.NormalizeListingMaterialsForEtsy(result.MaterialSuggestions));

    private static bool ValidateListingUpdate(ListingTextUpdate update, out string message)
    {
        if (string.IsNullOrWhiteSpace(update.Title))
        {
            message = "AI onerisi baslik uretmedi. Guncelleme yapilmadi.";
            return false;
        }

        if (update.Title.Length > 140)
        {
            message = "Baslik 140 karakterden uzun. Etsy kabul etmeyebilir.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(update.Description))
        {
            message = "AI onerisi aciklama uretmedi. Guncelleme yapilmadi.";
            return false;
        }

        if (update.Tags.Count == 0)
        {
            message = "AI onerisi tag uretmedi. Guncelleme yapilmadi.";
            return false;
        }

        var longTag = update.Tags.FirstOrDefault(tag => tag.Length > 20);
        if (longTag is not null)
        {
            message = $"Tag 20 karakterden uzun: {longTag}";
            return false;
        }

        var longMaterial = update.Materials.FirstOrDefault(material => material.Length > 45);
        if (longMaterial is not null)
        {
            message = $"Materyal 45 karakterden uzun: {longMaterial}";
            return false;
        }

        message = "";
        return true;
    }

    private void ApplyFilter()
    {
        var query = _searchTextBox.Text.Trim();
        var filtered = string.IsNullOrWhiteSpace(query)
            ? _rows
            : _rows.Where(row =>
                row.Title.Contains(query, StringComparison.CurrentCultureIgnoreCase) ||
                row.Listing.TagsDisplay.Contains(query, StringComparison.CurrentCultureIgnoreCase))
                .ToList();
        _bindingSource.DataSource = filtered;
        _grid.Refresh();
    }

    private async Task RefreshSelectedListingAsync()
    {
        if (SelectedRow is null)
        {
            MessageBox.Show(this, "Once bir listing secin.", "AI sonrasi yenile", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        try
        {
            UseWaitCursor = true;
            var selectedId = SelectedRow.Listing.ListingId;
            var settings = EtsyApiSettingsStore.Load();
            var refreshed = await _apiClient.GetOwnShopListingAsync(settings, selectedId);
            EtsyApiSettingsStore.Save(settings);
            var index = _rows.FindIndex(row => row.Listing.ListingId == selectedId);
            if (index >= 0)
            {
                _rows[index] = new AuditRow(_rows[index].Rank, refreshed, ScoreListing(refreshed), "Yenilendi");
            }

            ApplyFilter();
            _statusLabel.Text = "Secili listing Etsy'den tekrar yuklendi";
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "AI sonrasi yenile", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
        finally
        {
            UseWaitCursor = false;
        }
    }

    private async Task OpenAiImageWorkflowAsync()
    {
        if (SelectedRow is null)
        {
            MessageBox.Show(this, "Once bir listing secin.", "AI gorsel", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        using var form = new AiListingImageForm(SelectedRow.Listing, _apiClient);
        form.ShowDialog(this);
        await RefreshSelectedListingAsync();
    }

    private void OpenListing() => OpenUrl(SelectedRow?.Listing.ListingUrl);
    private void OpenShop() => OpenUrl(SelectedRow?.Listing.ShopUrl);

    private static void OpenUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url)) return;
        Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
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

    private static void ConfigureText(TextBox textBox)
    {
        textBox.Dock = DockStyle.Fill;
        textBox.Multiline = true;
        textBox.ReadOnly = true;
        textBox.ScrollBars = ScrollBars.Vertical;
        textBox.BackColor = Color.White;
    }

    private static Label LabelFor(string text) => new()
    {
        Dock = DockStyle.Fill,
        Text = text,
        TextAlign = ContentAlignment.MiddleLeft,
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
            Font = new Font("Segoe UI Semibold", 9.2F),
            Margin = new Padding(6, 3, 0, 3),
            UseVisualStyleBackColor = false,
        };
        button.FlatAppearance.BorderSize = 0;
        return button;
    }

    private sealed class AuditRow(int rank, MarketListingResult listing, int seoScore, string status)
    {
        public int Rank { get; } = rank;
        public MarketListingResult Listing { get; } = listing;
        public Image? ThumbnailImage { get; set; }
        public string Title => Listing.Title;
        public string Price => Listing.PriceDisplay;
        public int Favorites => Listing.Favorites;
        public int Quantity => Listing.Quantity;
        public int TagCount => Listing.Tags.Count;
        public int SeoScore { get; } = seoScore;
        public int AiScore { get; set; } = seoScore;
        public string Status { get; set; } = status;
        public string SeoNeeds => BuildSeoNeeds(Listing);
        public string SeoStrengths => BuildSeoStrengths(Listing);

        private static string BuildSeoNeeds(MarketListingResult listing)
        {
            var needs = new List<string>();
            if (listing.Tags.Count < 13) needs.Add($"{13 - listing.Tags.Count} tag eksik");
            if (listing.Title.Length < 55) needs.Add("baslik kisa");
            if (listing.Title.Length > 140) needs.Add("baslik uzun");
            if (listing.Description.Length < 500) needs.Add("aciklama kisa");
            if (listing.ImageUrls.Count < 5) needs.Add("gorsel az");
            return needs.Count == 0 ? "Temel eksik yok" : string.Join(", ", needs);
        }

        private static string BuildSeoStrengths(MarketListingResult listing)
        {
            var strengths = new List<string>();
            if (listing.Tags.Count >= 13) strengths.Add("13 tag");
            if (listing.Title.Length is >= 55 and <= 135) strengths.Add("baslik iyi");
            if (listing.Description.Length >= 500) strengths.Add("aciklama iyi");
            if (listing.ImageUrls.Count >= 5) strengths.Add("gorsel iyi");
            return strengths.Count == 0 ? "-" : string.Join(", ", strengths);
        }
    }
}
