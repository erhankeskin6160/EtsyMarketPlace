namespace SimilarProductsWinForms;

using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using EtsyMarketPlace.Application.ShopVault.Interfaces;
using EtsyMarketPlace.Application.ShopVault.Services;
using EtsyMarketPlace.Domain.ShopVault.Entities;
using EtsyMarketPlace.Domain.ShopVault.Interfaces;
using EtsyMarketPlace.Infrastructure.ShopVault.ImageSanitization;
using EtsyMarketPlace.Infrastructure.ShopVault.Repositories;
using SimilarProductsWinForms.Controls;
using SimilarProductsWinForms.Services;

internal sealed class ShopVaultForm : Form
{
    private readonly IShopVaultRepository _repository;
    private readonly IEtsyVaultApiClient _vaultApiClient;
    private readonly EtsyApiClient _rawApiClient;
    private readonly IAntiBanSanitizer _antiBanSanitizer;
    private readonly ShopVaultBackupService _backupService;
    private readonly ShopVaultPackagerService _packagerService;

    // Controls for 3 tabs
    private readonly ShopVaultBackupControl _backupControl;
    private readonly ShopVaultCatalogControl _catalogControl;
    private readonly ShopVaultMigrationControl _migrationControl;

    // Top Navigation & KPI Controls
    private readonly ModernButtonControl _btnTabBackup = new() { Text = "📦 Mağaza Yedekleme & Arşiv", Height = 40, Width = 230, BackColor = UiStyle.PrimaryColor, ForeColor = Color.White, Font = new Font("Segoe UI Semibold", 9.5F, FontStyle.Bold), Cursor = Cursors.Hand };
    private readonly ModernButtonControl _btnTabCatalog = new() { Text = "🗂️ Ürün Kasası & İnceleme", Height = 40, Width = 230, BackColor = UiStyle.CardBackground, ForeColor = UiStyle.TextDark, Font = new Font("Segoe UI Semibold", 9.5F, FontStyle.Bold), Cursor = Cursors.Hand };
    private readonly ModernButtonControl _btnTabMigration = new() { Text = "🚀 Anti-Ban Transfer & Dağıtım", Height = 40, Width = 240, BackColor = UiStyle.CardBackground, ForeColor = UiStyle.TextDark, Font = new Font("Segoe UI Semibold", 9.5F, FontStyle.Bold), Cursor = Cursors.Hand };

    private readonly Label _lblKpiListings = new() { AutoSize = true, Font = UiStyle.KpiValueFont, ForeColor = UiStyle.PrimaryColor, Text = "0" };
    private readonly Label _lblKpiImages = new() { AutoSize = true, Font = UiStyle.KpiValueFont, ForeColor = UiStyle.SuccessColor, Text = "0" };
    private readonly Label _lblKpiDiskSize = new() { AutoSize = true, Font = UiStyle.KpiValueFont, ForeColor = UiStyle.AccentColor, Text = "0 MB" };
    private readonly Label _lblKpiAntiBan = new() { AutoSize = true, Font = UiStyle.KpiValueFont, ForeColor = UiStyle.EtsyColor, Text = "%100 Aktif" };

    private readonly Panel _tabContainer = new() { Dock = DockStyle.Fill, BackColor = UiStyle.BackgroundColor };
    private int _currentTabIndex = 0;

    public ShopVaultForm()
    {
        Text = "Etsy Mağaza Yedekleme & Anti-Ban Transfer Stüdyosu";
        Size = new Size(1280, 840);
        MinimumSize = new Size(1024, 700);
        BackColor = UiStyle.BackgroundColor;
        Font = UiStyle.BaseFont;

        // 1. Service composition
        _repository = new SqliteShopVaultRepository();
        _rawApiClient = new EtsyApiClient();
        _vaultApiClient = new EtsyVaultApiClientAdapter(_rawApiClient);
        _antiBanSanitizer = new AntiBanSanitizerService(new ExifStripperImageProcessor());
        _backupService = new ShopVaultBackupService(_repository, _vaultApiClient);
        _packagerService = new ShopVaultPackagerService(_repository);

        // 2. Tab controls instantiation
        _backupControl = new ShopVaultBackupControl(_repository, _backupService, _packagerService, _rawApiClient);
        _catalogControl = new ShopVaultCatalogControl(_repository);
        _migrationControl = new ShopVaultMigrationControl(_vaultApiClient, _antiBanSanitizer);

        BuildLayout();
        HookEvents();

        // 3. Initial load
        Shown += async (_, _) =>
        {
            await RefreshKpisAsync();
            await _catalogControl.RefreshSessionsAsync();
        };
    }

    private void BuildLayout()
    {
        var rootLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            RowCount = 4,
            Padding = new Padding(12),
            BackColor = UiStyle.BackgroundColor
        };
        rootLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 56)); // Header
        rootLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 84)); // KPI Strip
        rootLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 48)); // Tab Buttons Ribbon
        rootLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); // Tab Content Container

        // HEADER
        var headerPanel = new Panel { Dock = DockStyle.Fill, BackColor = Color.Transparent };
        var lblTitle = new Label
        {
            Text = "🛡️ Etsy Mağaza Yedekleme, Afet Kurtarma & Anti-Ban Transfer Stüdyosu",
            Font = new Font("Segoe UI Semibold", 13.5F, FontStyle.Bold),
            ForeColor = UiStyle.TextDark,
            AutoSize = true,
            Location = new Point(4, 4)
        };
        var lblSubtitle = new Label
        {
            Text = "Mağazanızdaki ürünleri eksiksiz yedekleyin; askıya alınma riskine karşı EXIF temizliği, pHash kırıcı ve AI özgünleştirmeyle yeni mağazanıza aktarın.",
            Font = new Font("Segoe UI", 8.8F),
            ForeColor = UiStyle.TextMuted,
            AutoSize = true,
            Location = new Point(6, 30)
        };
        headerPanel.Controls.Add(lblTitle);
        headerPanel.Controls.Add(lblSubtitle);
        rootLayout.Controls.Add(headerPanel, 0, 0);

        // KPI STRIP
        var kpiStrip = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 4,
            RowCount = 1,
            Padding = new Padding(0, 4, 0, 4)
        };
        for (int i = 0; i < 4; i++)
            kpiStrip.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25f));

        kpiStrip.Controls.Add(CreateKpiCard("📦 TOPLAM YEDEKLİ İLAN", _lblKpiListings, "Yerel SQLite Veritabanında"), 0, 0);
        kpiStrip.Controls.Add(CreateKpiCard("🖼️ GÖRSEL KÜTÜPHANESİ", _lblKpiImages, "Yüksek Çözünürlüklü Arşiv"), 1, 0);
        kpiStrip.Controls.Add(CreateKpiCard("💾 KASA DİSK BOYUTU", _lblKpiDiskSize, "Görseller & SQLite Verisi"), 2, 0);
        kpiStrip.Controls.Add(CreateKpiCard("🛡️ ANTİ-BAN KALKANI", _lblKpiAntiBan, "EXIF Temizleyici + pHash"), 3, 0);
        rootLayout.Controls.Add(kpiStrip, 0, 1);

        // TAB RIBBON
        var tabRibbon = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            Padding = new Padding(0, 4, 0, 4)
        };
        tabRibbon.Controls.Add(_btnTabBackup);
        tabRibbon.Controls.Add(_btnTabCatalog);
        tabRibbon.Controls.Add(_btnTabMigration);
        rootLayout.Controls.Add(tabRibbon, 0, 2);

        // TAB CONTAINER
        rootLayout.Controls.Add(_tabContainer, 0, 3);
        Controls.Add(rootLayout);

        // Default tab
        SwitchTab(0);
    }

    private Control CreateKpiCard(string title, Label valueLabel, string subtext)
    {
        var card = new ModernCardPanel
        {
            Dock = DockStyle.Fill,
            Margin = new Padding(4, 0, 4, 0),
            Padding = new Padding(12, 8, 12, 8),
            CornerRadius = 10,
            CardColor = UiStyle.CardBackground,
            BorderColor = UiStyle.BorderColor
        };

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            RowCount = 3,
            BackColor = Color.Transparent
        };
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 18));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 16));

        var lblTitle = new Label
        {
            Dock = DockStyle.Fill,
            Text = title,
            Font = new Font("Segoe UI Semibold", 8.2F, FontStyle.Bold),
            ForeColor = UiStyle.TextMuted,
            TextAlign = ContentAlignment.MiddleLeft
        };
        valueLabel.Dock = DockStyle.Fill;
        valueLabel.TextAlign = ContentAlignment.MiddleLeft;

        var lblSub = new Label
        {
            Dock = DockStyle.Fill,
            Text = subtext,
            Font = new Font("Segoe UI", 7.8F),
            ForeColor = UiStyle.TextMuted,
            TextAlign = ContentAlignment.MiddleLeft
        };

        layout.Controls.Add(lblTitle, 0, 0);
        layout.Controls.Add(valueLabel, 0, 1);
        layout.Controls.Add(lblSub, 0, 2);
        card.Controls.Add(layout);
        return card;
    }

    private void HookEvents()
    {
        _btnTabBackup.Click += (_, _) => SwitchTab(0);
        _btnTabCatalog.Click += (_, _) => SwitchTab(1);
        _btnTabMigration.Click += (_, _) => SwitchTab(2);

        // Cross-tab workflows:
        // 1. Backup completed -> Refresh KPIs & refresh catalog
        _backupControl.BackupCompleted += async (sessionId) =>
        {
            await RefreshKpisAsync();
            await _catalogControl.RefreshSessionsAsync(sessionId);
        };

        // 2. User clicked a session to inspect in Backup control -> Jump to Catalog tab
        _backupControl.SessionSelected += async (sessionId) =>
        {
            await _catalogControl.RefreshSessionsAsync(sessionId);
            SwitchTab(1);
        };

        // 3. User selected listings in Catalog and clicked "Transfer" -> Send to Migration and switch to Tab 3
        _catalogControl.TransferRequested += (listings) =>
        {
            _migrationControl.SetListingsToMigrate(listings);
            SwitchTab(2);
        };
    }

    public void SwitchTab(int index)
    {
        _currentTabIndex = index;

        _btnTabBackup.BackColor = index == 0 ? UiStyle.PrimaryColor : UiStyle.CardBackground;
        _btnTabBackup.ForeColor = index == 0 ? Color.White : UiStyle.TextDark;

        _btnTabCatalog.BackColor = index == 1 ? UiStyle.PrimaryColor : UiStyle.CardBackground;
        _btnTabCatalog.ForeColor = index == 1 ? Color.White : UiStyle.TextDark;

        _btnTabMigration.BackColor = index == 2 ? UiStyle.PrimaryColor : UiStyle.CardBackground;
        _btnTabMigration.ForeColor = index == 2 ? Color.White : UiStyle.TextDark;

        _tabContainer.SuspendLayout();
        _tabContainer.Controls.Clear();

        UserControl activeControl = index switch
        {
            0 => _backupControl,
            1 => _catalogControl,
            2 => _migrationControl,
            _ => _backupControl
        };

        activeControl.Dock = DockStyle.Fill;
        _tabContainer.Controls.Add(activeControl);
        _tabContainer.ResumeLayout(true);
    }

    public async Task RefreshKpisAsync()
    {
        try
        {
            var sessions = await _repository.GetAllSessionsAsync();
            int totalListings = 0;
            int totalImages = 0;

            foreach (var session in sessions)
            {
                var listings = await _repository.GetListingsBySessionIdAsync(session.SessionId);
                totalListings += listings.Count;
                totalImages += listings.Sum(l => l.Images.Count);
            }

            _lblKpiListings.Text = totalListings.ToString("N0");
            _lblKpiImages.Text = totalImages.ToString("N0");

            // Calculate vault directory disk size
            long totalBytes = 0;
            string vaultRoot = VaultPathHelper.RootPath;
            if (Directory.Exists(vaultRoot))
            {
                var dirInfo = new DirectoryInfo(vaultRoot);
                totalBytes = dirInfo.EnumerateFiles("*", SearchOption.AllDirectories).Sum(f => f.Length);
            }

            double sizeMb = totalBytes / (1024.0 * 1024.0);
            if (sizeMb >= 1024)
            {
                _lblKpiDiskSize.Text = $"{(sizeMb / 1024.0):F2} GB";
            }
            else
            {
                _lblKpiDiskSize.Text = $"{sizeMb:F1} MB";
            }
        }
        catch
        {
            // Non-critical KPI refresh failure
        }
    }
}
