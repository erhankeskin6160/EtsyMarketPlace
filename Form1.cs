namespace SimilarProductsWinForms;

using System.Diagnostics;
using System.Globalization;
using System.Text;
using SimilarProductsWinForms.Models;
using SimilarProductsWinForms.Services;

public partial class Form1 : Form
{
    private readonly List<ProductCandidate> _products = ProductDataStore.Load();
    private readonly BindingSource _bindingSource = new();
    private readonly EtsyApiClient _etsyApiClient = new();

    private TextBox _searchTextBox = null!;
    private ComboBox _categoryComboBox = null!;
    private ComboBox _sortComboBox = null!;
    private DataGridView _grid = null!;
    private TextBox _detailTextBox = null!;
    private TextBox _noteTextBox = null!;
    private ComboBox _statusComboBox = null!;
    private Label _countLabel = null!;

    public Form1()
    {
        InitializeComponent();
        BuildLayout();
        LoadFilters();
        ApplyFilters();
    }

    private void BuildLayout()
    {
        Text = "3DArtDesignsStore - Benzer Urun Bulucu";
        MinimumSize = new Size(1120, 720);
        StartPosition = FormStartPosition.CenterScreen;
        Font = new Font("Segoe UI", 10F);
        BackColor = Color.FromArgb(247, 248, 250);

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 5,
            Padding = new Padding(18),
        };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 78));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 58));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 128));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 180));
        Controls.Add(root);

        var titlePanel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
        };
        titlePanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        titlePanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 250));

        var titleLabel = new Label
        {
            AutoSize = false,
            Dock = DockStyle.Fill,
            Text = "3DArtDesignsStore icin benzer Etsy urunleri",
            Font = new Font("Segoe UI Semibold", 19F),
            ForeColor = Color.FromArgb(24, 31, 42),
            TextAlign = ContentAlignment.MiddleLeft,
        };

        _countLabel = new Label
        {
            AutoSize = false,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleRight,
            ForeColor = Color.FromArgb(84, 93, 108),
        };

        titlePanel.Controls.Add(titleLabel, 0, 0);
        titlePanel.Controls.Add(_countLabel, 1, 0);
        root.Controls.Add(titlePanel, 0, 0);

        var filterPanel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 7,
            RowCount = 1,
        };
        filterPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 95));
        filterPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 40));
        filterPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 90));
        filterPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 210));
        filterPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 85));
        filterPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 190));
        filterPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 150));

        filterPanel.Controls.Add(CreateLabel("Arama"), 0, 0);
        _searchTextBox = new TextBox { Dock = DockStyle.Fill, PlaceholderText = "omnitrix, lotr, stand, mask..." };
        _searchTextBox.TextChanged += (_, _) => ApplyFilters();
        filterPanel.Controls.Add(_searchTextBox, 1, 0);

        filterPanel.Controls.Add(CreateLabel("Kategori"), 2, 0);
        _categoryComboBox = new ComboBox { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList };
        _categoryComboBox.SelectedIndexChanged += (_, _) => ApplyFilters();
        filterPanel.Controls.Add(_categoryComboBox, 3, 0);

        filterPanel.Controls.Add(CreateLabel("Sirala"), 4, 0);
        _sortComboBox = new ComboBox { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList };
        _sortComboBox.Items.AddRange(["Oncelik", "Dusuk fiyat", "Yuksek fiyat", "Kategori"]);
        _sortComboBox.SelectedIndexChanged += (_, _) => ApplyFilters();
        filterPanel.Controls.Add(_sortComboBox, 5, 0);

        var clearButton = new Button
        {
            Dock = DockStyle.Fill,
            Text = "Temizle",
            BackColor = Color.White,
            FlatStyle = FlatStyle.Flat,
        };
        clearButton.FlatAppearance.BorderColor = Color.FromArgb(204, 211, 220);
        clearButton.Click += (_, _) =>
        {
            _searchTextBox.Clear();
            _categoryComboBox.SelectedIndex = 0;
            _sortComboBox.SelectedIndex = 0;
        };
        filterPanel.Controls.Add(clearButton, 6, 0);
        root.Controls.Add(filterPanel, 0, 1);

        var commandPanel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 6,
            RowCount = 3,
            Padding = new Padding(0, 4, 0, 6),
        };
        for (var column = 0; column < 6; column++)
        {
            commandPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F / 6F));
        }
        commandPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100F / 3F));
        commandPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100F / 3F));
        commandPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100F / 3F));

        commandPanel.Controls.Add(CreateCommandButton("Etsy Ara", (_, _) => OpenSelectedSearch()), 0, 0);
        commandPanel.Controls.Add(CreateCommandButton("Klon Ac", (_, _) => OpenSelectedCloneShop()), 1, 0);
        commandPanel.Controls.Add(CreateCommandButton("Klon Link", (_, _) => CopySelectedCloneUrl()), 2, 0);
        commandPanel.Controls.Add(CreateCommandButton("API Ayar", (_, _) => OpenApiSettings()), 3, 0);
        commandPanel.Controls.Add(CreateCommandButton("API Ara", async (_, _) => await SearchSelectedWithEtsyApiAsync()), 4, 0);
        commandPanel.Controls.Add(CreateCommandButton("Not Kaydet", (_, _) => SaveSelectedNoteAndStatus()), 5, 0);
        commandPanel.Controls.Add(CreateCommandButton("Kar Hesapla", (_, _) => OpenProfitCalculator()), 0, 1);
        commandPanel.Controls.Add(CreateCommandButton("SEO Kontrol", (_, _) => OpenSeoScore()), 1, 1);
        commandPanel.Controls.Add(CreateCommandButton("Firsat Puani", (_, _) => OpenOpportunityScore()), 2, 1);
        commandPanel.Controls.Add(CreateCommandButton("Listing Taslak", (_, _) => OpenListingDraft()), 3, 1);
        commandPanel.Controls.Add(CreateCommandButton("Rapor", (_, _) => OpenWeeklyReport()), 4, 1);
        commandPanel.Controls.Add(CreateCommandButton("CSV Aktar", (_, _) => ExportCsv()), 5, 1);
        commandPanel.Controls.Add(CreateCommandButton("Gorsel Kontrol", (_, _) => OpenPhotoChecklist()), 0, 2);
        commandPanel.Controls.Add(CreateCommandButton("Manuel Veri", (_, _) => OpenManualCompetitor()), 1, 2);
        root.Controls.Add(commandPanel, 0, 2);

        _grid = new DataGridView
        {
            Dock = DockStyle.Fill,
            AutoGenerateColumns = false,
            AllowUserToAddRows = false,
            AllowUserToDeleteRows = false,
            AllowUserToResizeRows = false,
            ReadOnly = true,
            MultiSelect = false,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            RowHeadersVisible = false,
            BackgroundColor = Color.White,
            BorderStyle = BorderStyle.FixedSingle,
            AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.AllCells,
        };
        _grid.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI Semibold", 9.5F);
        _grid.DefaultCellStyle.WrapMode = DataGridViewTriState.True;
        _grid.DataSource = _bindingSource;
        _grid.SelectionChanged += (_, _) => UpdateDetail();
        _grid.CellDoubleClick += (_, _) => OpenSelectedSearch();
        AddColumns();
        root.Controls.Add(_grid, 0, 3);

        var bottomPanel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            Padding = new Padding(0, 14, 0, 0),
        };
        bottomPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        var detailPanel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 4,
        };
        detailPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 55));
        detailPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
        detailPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 45));
        detailPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 1));

        _detailTextBox = new TextBox
        {
            Dock = DockStyle.Fill,
            Multiline = true,
            ReadOnly = true,
            ScrollBars = ScrollBars.Vertical,
            BackColor = Color.White,
        };
        detailPanel.Controls.Add(_detailTextBox, 0, 0);

        _statusComboBox = new ComboBox
        {
            Dock = DockStyle.Fill,
            DropDownStyle = ComboBoxStyle.DropDownList,
        };
        _statusComboBox.Items.AddRange(["Fikir", "Arastiriliyor", "Modelleme", "Test baski", "Fotograf", "Listelendi", "Beklet"]);
        detailPanel.Controls.Add(_statusComboBox, 0, 1);

        _noteTextBox = new TextBox
        {
            Dock = DockStyle.Fill,
            Multiline = true,
            ScrollBars = ScrollBars.Vertical,
            PlaceholderText = "Secili urun icin not yaz...",
        };
        detailPanel.Controls.Add(_noteTextBox, 0, 2);

        bottomPanel.Controls.Add(detailPanel, 0, 0);
        root.Controls.Add(bottomPanel, 0, 4);
    }

    private static Label CreateLabel(string text) => new()
    {
        AutoSize = false,
        Dock = DockStyle.Fill,
        Text = text,
        TextAlign = ContentAlignment.MiddleLeft,
        ForeColor = Color.FromArgb(67, 76, 92),
    };

    private static Button CreateActionButton(string text)
    {
        var button = new Button
        {
            Dock = DockStyle.Fill,
            Text = text,
            BackColor = Color.FromArgb(32, 97, 165),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Margin = new Padding(10, 0, 0, 8),
        };
        button.FlatAppearance.BorderSize = 0;
        return button;
    }

    private static Button CreateCommandButton(string text, EventHandler clickHandler)
    {
        var button = new Button
        {
            AutoSize = false,
            Dock = DockStyle.Fill,
            Text = text,
            BackColor = Color.FromArgb(32, 97, 165),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Margin = new Padding(0, 0, 8, 6),
        };
        button.FlatAppearance.BorderSize = 0;
        button.Click += clickHandler;
        return button;
    }

    private void AddColumns()
    {
        _grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            HeaderText = "Urun fikri",
            DataPropertyName = nameof(ProductCandidate.Name),
            FillWeight = 235,
            AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill,
        });
        _grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            HeaderText = "Etsy kategori",
            DataPropertyName = nameof(ProductCandidate.EtsyCategoryDisplay),
            FillWeight = 185,
            AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill,
        });
        _grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            HeaderText = "Rakip fiyat",
            DataPropertyName = nameof(ProductCandidate.CompetitorPriceDisplay),
            Width = 120,
        });
        _grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            HeaderText = "Magaza urunu",
            DataPropertyName = nameof(ProductCandidate.RelatedStoreProduct),
            FillWeight = 170,
            AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill,
        });
        _grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            HeaderText = "Klon magaza",
            DataPropertyName = nameof(ProductCandidate.CloneShopNameDisplay),
            Width = 150,
        });
        _grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            HeaderText = "Klon magaza linki",
            DataPropertyName = nameof(ProductCandidate.CloneShopUrlDisplay),
            FillWeight = 190,
            AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill,
        });
        _grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            HeaderText = "Satis adedi",
            DataPropertyName = nameof(ProductCandidate.UnitsSoldDisplay),
            Width = 145,
        });
        _grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            HeaderText = "Durum",
            DataPropertyName = nameof(ProductCandidate.StatusDisplay),
            Width = 120,
        });
        _grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            HeaderText = "Puan",
            DataPropertyName = nameof(ProductCandidate.Priority),
            Width = 78,
        });
    }

    private void LoadFilters()
    {
        _categoryComboBox.Items.Clear();
        _categoryComboBox.Items.Add("Tum kategoriler");
        foreach (var category in _products.Select(product => product.EtsyCategoryDisplay).Distinct().OrderBy(value => value))
        {
            _categoryComboBox.Items.Add(category);
        }

        _categoryComboBox.SelectedIndex = 0;
        _sortComboBox.SelectedIndex = 0;
    }

    private void ApplyFilters()
    {
        var query = _searchTextBox.Text.Trim();
        var category = _categoryComboBox.SelectedItem?.ToString();

        IEnumerable<ProductCandidate> filtered = _products;

        if (!string.IsNullOrWhiteSpace(query))
        {
            filtered = filtered.Where(product => product.SearchBlob.Contains(query, StringComparison.CurrentCultureIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(category) && category != "Tum kategoriler")
        {
            filtered = filtered.Where(product => product.EtsyCategoryDisplay == category);
        }

        filtered = _sortComboBox.SelectedItem?.ToString() switch
        {
            "Dusuk fiyat" => filtered.OrderBy(product => product.MinPrice),
            "Yuksek fiyat" => filtered.OrderByDescending(product => product.MaxPrice),
            "Kategori" => filtered.OrderBy(product => product.EtsyCategoryDisplay).ThenBy(product => product.Name),
            _ => filtered.OrderByDescending(product => product.Priority).ThenBy(product => product.Name),
        };

        var list = filtered.ToList();
        _bindingSource.DataSource = list;
        _countLabel.Text = $"{list.Count} urun listeleniyor";
        _bindingSource.Position = list.Count > 0 ? 0 : -1;

        UpdateDetail();
    }

    private ProductCandidate? SelectedProduct => _bindingSource.Current as ProductCandidate;

    private void UpdateDetail()
    {
        var product = SelectedProduct;
        _detailTextBox.Text = product is null
            ? "Secili urun yok."
            : $"Etsy kategori:{Environment.NewLine}{product.EtsyCategoryDisplay}{Environment.NewLine}{Environment.NewLine}Klon magaza:{Environment.NewLine}{product.CloneShopNameDisplay}{Environment.NewLine}{product.CloneShopUrlDisplay}{Environment.NewLine}{Environment.NewLine}Rakip fiyat / satis adedi:{Environment.NewLine}{product.CompetitorPriceDisplay} / {product.UnitsSoldDisplay}{Environment.NewLine}{Environment.NewLine}Neden benzer/firsat olabilir:{Environment.NewLine}{product.Reason}{Environment.NewLine}{Environment.NewLine}Anahtar kelimeler:{Environment.NewLine}{product.Keywords}{Environment.NewLine}{Environment.NewLine}Etsy arama linki:{Environment.NewLine}{product.SearchUrl}";

        if (product is null)
        {
            _noteTextBox.Clear();
            _statusComboBox.SelectedIndex = -1;
            return;
        }

        _noteTextBox.Text = product.Note;
        _statusComboBox.SelectedItem = product.StatusDisplay;
    }

    private void OpenSelectedSearch()
    {
        var product = SelectedProduct;
        if (product is null)
        {
            return;
        }

        Process.Start(new ProcessStartInfo(product.SearchUrl) { UseShellExecute = true });
    }

    private void CopySelectedUrl()
    {
        var product = SelectedProduct;
        if (product is null)
        {
            return;
        }

        Clipboard.SetText(product.SearchUrl);
        _countLabel.Text = "Link panoya kopyalandi";
    }

    private void OpenSelectedCloneShop()
    {
        var product = SelectedProduct;
        if (product is null)
        {
            return;
        }

        Process.Start(new ProcessStartInfo(product.CloneShopUrlDisplay) { UseShellExecute = true });
    }

    private void CopySelectedCloneUrl()
    {
        var product = SelectedProduct;
        if (product is null)
        {
            return;
        }

        Clipboard.SetText(product.CloneShopUrlDisplay);
        _countLabel.Text = "Klon magaza linki panoya kopyalandi";
    }

    private void OpenApiSettings()
    {
        using var form = new EtsyApiSettingsForm();
        form.ShowDialog(this);
    }

    private void OpenProfitCalculator()
    {
        using var form = new ProfitCalculatorForm(SelectedProduct);
        form.ShowDialog(this);
    }

    private void OpenSeoScore()
    {
        using var form = new SeoScoreForm(SelectedProduct);
        form.ShowDialog(this);
    }

    private void OpenOpportunityScore()
    {
        using var form = new OpportunityScoreForm(SelectedProduct);
        form.ShowDialog(this);
    }

    private void OpenListingDraft()
    {
        using var form = new ListingDraftForm(SelectedProduct);
        form.ShowDialog(this);
    }

    private void OpenWeeklyReport()
    {
        using var form = new WeeklyReportForm(_products);
        form.ShowDialog(this);
    }

    private void OpenPhotoChecklist()
    {
        using var form = new PhotoChecklistForm(SelectedProduct);
        form.ShowDialog(this);
    }

    private void OpenManualCompetitor()
    {
        var product = SelectedProduct;
        if (product is null)
        {
            return;
        }

        using var form = new ManualCompetitorForm(product);
        if (form.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        var sourceIndex = _products.FindIndex(item => ReferenceEquals(item, product) || item.Name == product.Name);
        if (sourceIndex >= 0)
        {
            _products[sourceIndex] = form.Result;
            ProductDataStore.Save(_products);
            ApplyFilters();
            _bindingSource.Position = (_bindingSource.DataSource as List<ProductCandidate>)?.FindIndex(item => item.Name == form.Result.Name) ?? 0;
            _countLabel.Text = "Manuel rakip verisi kaydedildi";
        }
    }

    private async Task SearchSelectedWithEtsyApiAsync()
    {
        var product = SelectedProduct;
        if (product is null)
        {
            return;
        }

        try
        {
            _countLabel.Text = "Etsy API araniyor...";
            var settings = EtsyApiSettingsStore.Load();
            var listings = await _etsyApiClient.FindActiveListingsAsync(settings, product.Keywords, limit: 5);
            var bestMatch = listings.FirstOrDefault();
            if (bestMatch is null)
            {
                _countLabel.Text = "API sonucunda liste bulunamadi";
                return;
            }

            var updated = product with
            {
                CloneShopName = bestMatch.ShopName,
                CloneShopUrl = string.IsNullOrWhiteSpace(bestMatch.ListingUrl) ? bestMatch.ShopUrl : bestMatch.ListingUrl,
                CompetitorPrice = bestMatch.PriceDisplay,
                UnitsSold = bestMatch.SalesDisplay,
            };

            var sourceIndex = _products.FindIndex(item => ReferenceEquals(item, product) || item.Name == product.Name);
            if (sourceIndex >= 0)
            {
                _products[sourceIndex] = updated;
                ProductDataStore.Save(_products);
            }

            ApplyFilters();
            _bindingSource.Position = (_bindingSource.DataSource as List<ProductCandidate>)?.FindIndex(item => item.Name == updated.Name) ?? 0;
            _countLabel.Text = $"API sonucu islendi: {bestMatch.ShopName}";
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Etsy API hatasi", MessageBoxButtons.OK, MessageBoxIcon.Error);
            _countLabel.Text = "Etsy API hatasi";
        }
    }

    private void SaveSelectedNoteAndStatus()
    {
        var product = SelectedProduct;
        if (product is null)
        {
            return;
        }

        var updated = product with
        {
            Status = _statusComboBox.SelectedItem?.ToString() ?? "Fikir",
            Note = _noteTextBox.Text.Trim(),
        };

        var sourceIndex = _products.FindIndex(item => ReferenceEquals(item, product) || item.Name == product.Name);
        if (sourceIndex >= 0)
        {
            _products[sourceIndex] = updated;
            ProductDataStore.Save(_products);
            ApplyFilters();
            _bindingSource.Position = (_bindingSource.DataSource as List<ProductCandidate>)?.FindIndex(item => item.Name == updated.Name) ?? 0;
            _countLabel.Text = "Not ve durum kaydedildi";
        }
    }

    private void ExportCsv()
    {
        using var dialog = new SaveFileDialog
        {
            Filter = "CSV dosyasi (*.csv)|*.csv",
            FileName = "etsy-benzer-urunler.csv",
        };

        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        var rows = (_bindingSource.DataSource as IEnumerable<ProductCandidate>) ?? [];
        var builder = new StringBuilder();
        builder.AppendLine("Urun,EtsyKategori,RakipFiyat,BenzerMagazaUrunu,KlonMagaza,KlonMagazaLinki,SatisAdedi,Durum,Not,Oncelik,AnahtarKelimeler,EtsyAramaLinki");
        foreach (var product in rows)
        {
            builder.AppendLine(string.Join(",", Csv(product.Name), Csv(product.EtsyCategoryDisplay), Csv(product.CompetitorPriceDisplay), Csv(product.RelatedStoreProduct), Csv(product.CloneShopNameDisplay), Csv(product.CloneShopUrlDisplay), Csv(product.UnitsSoldDisplay), Csv(product.StatusDisplay), Csv(product.Note), product.Priority.ToString(CultureInfo.InvariantCulture), Csv(product.Keywords), Csv(product.SearchUrl)));
        }

        File.WriteAllText(dialog.FileName, builder.ToString(), Encoding.UTF8);
        _countLabel.Text = "CSV kaydedildi";
    }

    private static string Csv(string value) => $"\"{value.Replace("\"", "\"\"", StringComparison.Ordinal)}\"";
}

internal sealed record ProductCandidate(
    string Name,
    string Category,
    decimal MinPrice,
    decimal MaxPrice,
    string RelatedStoreProduct,
    int Priority,
    string Keywords,
    string Reason,
    string EtsyCategoryPath = "",
    string CloneShopName = "",
    string CloneShopUrl = "",
    string CompetitorPrice = "",
    string UnitsSold = "",
    string Status = "Fikir",
    string Note = "")
{
    public string PriceDisplay => $"${MinPrice:0.##} - ${MaxPrice:0.##}";

    public string SearchUrl => "https://www.etsy.com/search?q=" + Uri.EscapeDataString(Keywords);

    public string EtsyCategoryDisplay => string.IsNullOrWhiteSpace(EtsyCategoryPath)
        ? GetDefaultEtsyCategory(Category)
        : EtsyCategoryPath;

    public string CloneShopNameDisplay => string.IsNullOrWhiteSpace(CloneShopName)
        ? "Rakip magaza secilmedi"
        : CloneShopName;

    public string CloneShopUrlDisplay => string.IsNullOrWhiteSpace(CloneShopUrl)
        ? SearchUrl
        : CloneShopUrl;

    public string CompetitorPriceDisplay => string.IsNullOrWhiteSpace(CompetitorPrice)
        ? PriceDisplay
        : CompetitorPrice;

    public string UnitsSoldDisplay => string.IsNullOrWhiteSpace(UnitsSold)
        ? "Urun bazli satis gorunmuyor"
        : UnitsSold;

    public string StatusDisplay => string.IsNullOrWhiteSpace(Status)
        ? "Fikir"
        : Status;

    public string SearchBlob => $"{Name} {Category} {EtsyCategoryDisplay} {RelatedStoreProduct} {CloneShopNameDisplay} {CompetitorPriceDisplay} {UnitsSoldDisplay} {StatusDisplay} {Note} {Keywords} {Reason}";

    private static string GetDefaultEtsyCategory(string category) => category switch
    {
        "Anime" => "Art & Collectibles > Sculpture > Figurines",
        "Arcane" => "Home & Living > Home Decor > Lighting",
        "Ben10" => "Toys & Games > Toys > Pretend Play",
        "Desk Decor" => "Home & Living > Office > Desk Accessories",
        "Fantasy Decor" => "Art & Collectibles > Sculpture",
        "Horror" => "Clothing > Costume Accessories > Masks & Prosthetics",
        "League of Legends" => "Home & Living > Home Decor > Lighting",
        "LOTR" => "Art & Collectibles > Sculpture > Figurines",
        "Marvel" => "Home & Living > Home Decor",
        "Minecraft" => "Toys & Games > Toys > Miniatures",
        "Valorant" => "Art & Collectibles > Sculpture",
        "Video Game" => "Art & Collectibles > Sculpture > Figurines",
        _ => "Art & Collectibles > Sculpture",
    };

    public static List<ProductCandidate> Seed() =>
    [
        new("Ben 10 Ultimatrix cosplay watch", "Ben10", 95, 165, "Ben 10 Classic/Omniverse Omnitrix", 98, "Ben 10 Ultimatrix cosplay watch 3D printed prop", "Magazada Omnitrix cesitleri guclu; Ultimatrix ayni alici kitlesine yeni varyasyon sunar."),
        new("Ben 10 Alien Force Omnitrix display prop", "Ben10", 90, 155, "Ben 10 Classic Omnitrix", 96, "Alien Force Omnitrix replica cosplay 3D printed", "Classic Omnitrix alan musteriler seri icindeki farkli donem tasarimlarini da arayabilir."),
        new("Ben 10 Omnitrix wall display stand", "Ben10", 18, 45, "Ben 10 Omnitrix Bracelet", 85, "Omnitrix display stand 3D printed wall mount", "Yuksek fiyatli saat urunlerinin yanina dusuk fiyatli tamamlayici aksesuar olarak eklenebilir."),
        new("Ben 10 alien badge magnet set", "Ben10", 12, 35, "Ben10 Albedo Omnitrix Set", 74, "Ben 10 alien badge magnet set 3D printed", "Kucuk hediyelik urun, Ben10 kategorisinde daha dusuk giris fiyatina imkan verir."),

        new("LOTR Anduril sword wall plaque", "LOTR", 70, 140, "Narsil Sword Replica with Wall Mount", 93, "Anduril sword wall plaque Lord of the Rings 3D printed", "Narsil ve Aragorn temasi magazada var; Anduril varyasyonu dogal genisleme."),
        new("LOTR One Ring display box", "LOTR", 22, 55, "Gandalf Statue / LOTR crown", 88, "Lord of the Rings One Ring display box 3D printed", "LOTR hayranlari icin daha kucuk, kargo ve fiyat avantaji olan koleksiyon urunu."),
        new("Sauron helmet miniature statue", "LOTR", 45, 95, "Barad-dur Tower Statue", 91, "Sauron helmet miniature statue 3D printed hand painted", "Barad-dur ve LOTR koleksiyon cizgisini tamamlayan sergileme parcasi."),
        new("Gondor white tree lamp", "LOTR", 55, 120, "Minas Tirith Lamp", 86, "Gondor white tree lamp Lord of the Rings 3D printed", "Minas Tirith lambaya benzer ev dekoru; ayni hedef kitle fakat farkli ikonografi."),
        new("Witch King crown cosplay replica", "LOTR", 85, 160, "Aragorn Crown / Witch King Figure review", 84, "Witch King crown cosplay replica LOTR 3D printed", "Magaza yorumlarinda Witch King temasi gorunuyor; cosplay taclari fiyat olarak uygun segmentte."),

        new("Iron Man arc reactor desk lamp", "Marvel", 35, 95, "Illuminated Thor Mjolnir / Marvel stands", 92, "Iron Man arc reactor desk lamp 3D printed LED", "Aydinlatmali Mjolnir urununun Marvel icinde LED dekor varyasyonu."),
        new("Spider-Man web headphone stand", "Marvel", 55, 110, "Thanos Headphone Stand / Deadpool Headphone Stand", 90, "Spider Man headphone stand 3D printed gamer desk", "Kulaklik standi formatiniz var; Spider-Man daha genis arama hacimli Marvel varyasyonu."),
        new("Wolverine mask cosplay prop", "Marvel", 65, 130, "Marvel decor and cosplay props", 82, "Wolverine mask cosplay prop 3D printed", "Maske ve cosplay prop deneyimini Marvel lisansli arama talebine yakinlastirir."),
        new("Captain America shield mini wall decor", "Marvel", 28, 70, "Marvel 3D printed desk decor", 78, "Captain America shield mini wall decor 3D printed", "Kucuk boy dekor urunu, Marvel kategorisinde hediye segmenti acabilir."),

        new("Minecraft creeper night light", "Minecraft", 18, 55, "Minecraft TNT Keychain", 88, "Minecraft creeper night light 3D printed LED", "TNT keychain ile ayni oyun kitlesi; daha yuksek sepet degeri olan LED dekor."),
        new("Minecraft sword keychain set", "Minecraft", 8, 24, "Minecraft TNT Keychain", 79, "Minecraft diamond sword keychain set 3D printed", "Mevcut keychain fiyat bandina yakin, varyasyon uretimi kolay kucuk urun."),
        new("Minecraft block desk organizer", "Minecraft", 25, 65, "Minecraft TNT Keychain", 76, "Minecraft block desk organizer 3D printed gamer gift", "Oyuncu masa aksesuari; kucuk dekor ile kullanisli urun arasinda iyi konumlanir."),

        new("Valorant spike lamp replica", "Valorant", 55, 130, "Valorant category", 89, "Valorant spike lamp replica 3D printed LED", "Valorant kategoriniz var; Spike prop hem cosplay hem oda dekoru aramalarina uyar."),
        new("Valorant sheriff pistol display prop", "Valorant", 45, 100, "Valorant category", 80, "Valorant sheriff pistol display prop 3D printed", "Oyun silah prop segmentinde Fate/Excalibur ve God of War prop cizgisine benzer."),
        new("League of Legends Teemo mushroom lamp", "League of Legends", 30, 85, "Arcane Jinx Statue / League category", 87, "League of Legends Teemo mushroom lamp 3D printed", "LoL/Arcane alicilari icin daha eglenceli ve masa dekoru odakli alternatif."),
        new("Arcane Hextech crystal desk light", "Arcane", 35, 90, "Hexcore Rubik's Cube", 86, "Arcane Hextech crystal desk light 3D printed LED", "Hexcore urununun ayni gorsel dilde LED dekor versiyonu."),

        new("God of War Leviathan axe wall mount", "Video Game", 80, 165, "Kratos Controller Holder / Mjolnir God of War", 90, "God of War Leviathan axe wall mount 3D printed", "God of War prop ve standlariniz var; Leviathan Axe yuksek fiyatli tamamlayici."),
        new("Mortal Kombat Sub-Zero mask", "Video Game", 60, 125, "MK9 Toasty Scorpion Mask", 84, "Mortal Kombat Sub Zero mask 3D printed cosplay", "Scorpion maskenin karsilik karakter varyasyonu; ayni kalipta yeni ilan serisi kurulabilir."),
        new("Fallout Nuka Cola bottle cap set", "Video Game", 10, 30, "Fallout Pip-Boy 3000", 77, "Fallout Nuka Cola bottle cap set 3D printed", "Pip-Boy yuksek fiyatli; dusuk fiyatli Fallout aksesuar sepete ek urun olabilir."),
        new("Cyberpunk samurai logo desk sign", "Video Game", 20, 55, "Video Game props", 72, "Cyberpunk samurai logo desk sign 3D printed gamer decor", "Oyun dekorlariyla ayni alici kitlesine yeni ev/ofis urunu."),

        new("Demon Slayer katana wall stand", "Anime", 18, 45, "Demon Slayer Mini Katana", 83, "Demon Slayer katana wall stand 3D printed", "Mini katana icin tamamlayici stand; uretimi daha kolay ve sepet artirici."),
        new("Fate Excalibur display stand", "Anime", 18, 45, "Fate/Stay Night Excalibur Sword", 81, "Fate Excalibur sword display stand 3D printed", "Mevcut Excalibur urunune aksesuar olarak konumlanabilir."),
        new("Anime mini sword mystery set", "Anime", 25, 70, "Demon Slayer Mini Katana", 73, "anime mini sword mystery set 3D printed cosplay", "Kucuk anime prop setleri hediye ve koleksiyon aramalarinda iyi calisir."),

        new("Horror mask wall display hanger", "Horror", 15, 38, "Jason Hockey Mask / Plague Doctor Mask", 82, "horror mask wall display hanger 3D printed", "Maske satan magaza icin tamamlayici sergileme aparati."),
        new("Scream Ghostface mask prop", "Horror", 45, 95, "Jason Hockey Mask", 78, "Ghostface mask prop 3D printed horror cosplay", "Jason maskesine benzer korku cosplay kitlesine hitap eder."),
        new("Plague doctor desk bust", "Horror", 35, 85, "Plague Doctor Mask & Figurine", 75, "plague doctor desk bust 3D printed hand painted", "Mevcut Plague Doctor urununu dekor/bust segmentine genisletir."),

        new("Custom gamer controller holder bust", "Desk Decor", 55, 120, "Kratos Controller Holder", 87, "custom gamer controller holder bust 3D printed", "Controller holder formatiniz calisiyor; farkli karakter ve renk kisisellestirme ile cogaltilabilir."),
        new("3D printed articulated dragon egg stand", "Fantasy Decor", 20, 55, "4FT Crystal Dragon", 76, "articulated dragon egg stand 3D printed fantasy decor", "Crystal Dragon alicisi icin daha kucuk ve tamamlayici fantasy aksesuar."),
        new("Movie character headphone stand", "Desk Decor", 55, 115, "Deadpool Headphone Stand / Thanos Headphone Stand", 83, "movie character headphone stand 3D printed desk decor", "Mevcut stand konseptini film karakterlerine acarak kategori disi talep yakalayabilir.")
    ];
}
