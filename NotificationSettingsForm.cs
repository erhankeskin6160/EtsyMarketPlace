namespace SimilarProductsWinForms;

using SimilarProductsWinForms.Services;
using SimilarProductsWinForms.Controls;

internal sealed class NotificationSettingsForm : Form
{
    private readonly NotificationSettings _settings;

    private readonly ModernCheckBox _telegramEnabledChk = new() { Text = "Telegram Bildirimlerini Etkinleştir", AutoSize = true };
    private readonly TextBox _telegramTokenTxt = new();
    private readonly TextBox _telegramChatIdTxt = new();
    private readonly Button _testTelegramBtn;

    private readonly ModernCheckBox _whatsappEnabledChk = new() { Text = "WhatsApp Bildirimlerini Etkinleştir", AutoSize = true };
    private readonly TextBox _whatsappUrlTxt = new();
    private readonly TextBox _whatsappTokenTxt = new();
    private readonly TextBox _whatsappPhoneTxt = new();
    private readonly Button _testWhatsAppBtn;

    private readonly ModernCheckBox _notifyNewOrderChk = new() { Text = "🎉 Yeni Sipariş Geldiğinde Bildirim Gönder", AutoSize = true };
    private readonly ModernCheckBox _notifyOpportunityChk = new() { Text = "🎯 Yüksek Fırsatlı Kelime Yakalandığında Gönder", AutoSize = true };
    private readonly ModernCheckBox _notifyBatchFinishedChk = new() { Text = "📦 Toplu İşlem Kuyruğu Tamamlandığında Gönder", AutoSize = true };
    private readonly ModernCheckBox _notifyAutomationRunChk = new() { Text = "⚡ Otomasyon Taraması Bittiğinde Gönder", AutoSize = true };
    private readonly ModernCheckBox _notifyAbTestWinnerChk = new() { Text = "📈 A/B Test Kazananı Belli Olduğunda Gönder", AutoSize = true };
    private readonly ModernCheckBox _notifyErrorChk = new() { Text = "⚠️ Kritik Sistem/API Hatası Oluştuğunda Gönder", AutoSize = true };

    // ── 🌙 Günlük Gece Finans Raporu ──────────────────────────────────────────
    private readonly ModernCheckBox _dailyNightReportChk = new() { Text = "🌙 Günlük Gece Finans Raporu (Her Gece Otomatik)", AutoSize = true };
    private readonly TextBox _dailyReportTimeTxt = new() { Text = "23:55", Width = 80 };
    private readonly Button _testNightReportBtn;

    private readonly Label _statusLabel = new() { UseMnemonic = false };

    public NotificationSettingsForm()
    {
        _settings = NotificationSettingsStore.Load();
        _testTelegramBtn = UiStyle.CreateButton("🧪 Telegram Test Bildirimi");
        _testWhatsAppBtn = UiStyle.CreateButton("🧪 WhatsApp Test Bildirimi");
        _testNightReportBtn = UiStyle.CreateButton("🌙 Şimdi Gece Finans Raporunu Gönder (Test)");
        BuildLayout();
        LoadValues();
    }

    private void BuildLayout()
    {
        Text = "Telegram & WhatsApp Bildirim Botu Ayarları";
        StartPosition = FormStartPosition.CenterScreen;
        WindowState = FormWindowState.Maximized;
        MinimumSize = new Size(1100, 750);
        UiStyle.ApplyTheme(this);

        var root = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 3, Padding = new Padding(16, 12, 16, 12) };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 64));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 50));
        Controls.Add(root);

        // Header
        var header = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1 };
        var titlePanel = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false };
        var mainTitle = new Label { AutoSize = true, Text = "🔔 Telegram & WhatsApp Anlık Bildirim Botu", Font = UiStyle.TitleFont, ForeColor = UiStyle.TextDark, UseMnemonic = false };
        var subTitle = new Label { AutoSize = true, Text = "VDS üzerinde 7/24 çalışırken telefonunuza yeni sipariş ve fırsat bildirimleri atın", Font = UiStyle.SubtitleFont, ForeColor = UiStyle.TextMuted, UseMnemonic = false };
        titlePanel.Controls.Add(mainTitle);
        titlePanel.Controls.Add(subTitle);
        header.Controls.Add(titlePanel, 0, 0);
        root.Controls.Add(header, 0, 0);

        // Content Split Grid (50% left, 50% right)
        var content = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, Padding = new Padding(0, 8, 0, 8) };
        content.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        content.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));

        content.Controls.Add(BuildBotConfigPanel(), 0, 0);
        content.Controls.Add(BuildEventsPanel(), 1, 0);
        root.Controls.Add(content, 0, 1);

        // Bottom status & action buttons
        var bottomBar = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3 };
        bottomBar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        bottomBar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 180));
        bottomBar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 140));

        _statusLabel.Dock = DockStyle.Fill;
        _statusLabel.TextAlign = ContentAlignment.MiddleLeft;
        _statusLabel.Font = new Font("Segoe UI Semibold", 9.5F);
        _statusLabel.ForeColor = UiStyle.TextMuted;
        bottomBar.Controls.Add(_statusLabel, 0, 0);

        var saveBtn = UiStyle.CreateButton("Ayarları Kaydet");
        saveBtn.Click += (_, _) => SaveValues();
        bottomBar.Controls.Add(saveBtn, 1, 0);

        var closeBtn = UiStyle.CreateButton("Kapat", isSecondary: true);
        closeBtn.Click += (_, _) => Close();
        bottomBar.Controls.Add(closeBtn, 2, 0);

        root.Controls.Add(bottomBar, 0, 2);
    }

    private Control BuildBotConfigPanel()
    {
        var grid = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 2, Padding = new Padding(0, 0, 8, 0) };
        grid.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
        grid.RowStyles.Add(new RowStyle(SizeType.Percent, 50));

        // Telegram GroupBox
        var tgGroup = new GroupBox
        {
            Dock = DockStyle.Fill,
            Text = "✈️ Telegram Bot Konfigürasyonu (@BotFather)",
            Font = new Font("Segoe UI Semibold", 9.5F),
            ForeColor = UiStyle.TextDark,
            Padding = new Padding(12)
        };
        var tgGrid = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 5, Padding = new Padding(6) };
        tgGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 120));
        tgGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        tgGrid.RowStyles.Add(new RowStyle(SizeType.Absolute, 36)); // Enabled Checkbox
        tgGrid.RowStyles.Add(new RowStyle(SizeType.Absolute, 42)); // Token row
        tgGrid.RowStyles.Add(new RowStyle(SizeType.Absolute, 42)); // Chat ID row
        tgGrid.RowStyles.Add(new RowStyle(SizeType.Absolute, 48)); // Test Button row
        tgGrid.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); // Remaining

        tgGrid.Controls.Add(_telegramEnabledChk, 0, 0);
        tgGrid.SetColumnSpan(_telegramEnabledChk, 2);

        tgGrid.Controls.Add(new Label { Text = "Bot Token:", Anchor = AnchorStyles.Left, AutoSize = true, ForeColor = UiStyle.TextDark, UseMnemonic = false }, 0, 1);
        var tokenRow = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, RowCount = 1 };
        tokenRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        tokenRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 150));
        tokenRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 125));
        _telegramTokenTxt.Dock = DockStyle.Fill;
        _telegramTokenTxt.Font = new Font("Consolas", 9.5F);
        var btnPaste = UiStyle.CreateButton("📋 Panodan Yapıştır");
        btnPaste.Height = 32;
        btnPaste.Click += (_, _) =>
        {
            try
            {
                var clip = Clipboard.GetText();
                if (!string.IsNullOrWhiteSpace(clip))
                {
                    var clean = System.Text.RegularExpressions.Regex.Replace(clip, @"\s+", "");
                    _telegramTokenTxt.Text = clean;
                    _statusLabel.Text = "Token panodan temizlenerek yapıştırıldı.";
                }
            }
            catch { }
        };
        var btnOpenFather = UiStyle.CreateButton("🤖 @BotFather", isSecondary: true);
        btnOpenFather.Height = 32;
        btnOpenFather.Click += (_, _) =>
        {
            try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("https://t.me/BotFather") { UseShellExecute = true }); } catch { }
        };
        tokenRow.Controls.Add(_telegramTokenTxt, 0, 0);
        tokenRow.Controls.Add(btnPaste, 1, 0);
        tokenRow.Controls.Add(btnOpenFather, 2, 0);
        tgGrid.Controls.Add(tokenRow, 1, 1);

        tgGrid.Controls.Add(new Label { Text = "Chat ID:", Anchor = AnchorStyles.Left, AutoSize = true, ForeColor = UiStyle.TextDark, UseMnemonic = false }, 0, 2);
        var chatRow = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1 };
        chatRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        chatRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 150));
        _telegramChatIdTxt.Dock = DockStyle.Fill;
        _telegramChatIdTxt.Font = new Font("Consolas", 9.5F);
        var btnOpenIdBot = UiStyle.CreateButton("🆔 Chat ID'mi Aç", isSecondary: true);
        btnOpenIdBot.Height = 32;
        btnOpenIdBot.Click += (_, _) =>
        {
            try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("https://t.me/userinfobot") { UseShellExecute = true }); } catch { }
        };
        chatRow.Controls.Add(_telegramChatIdTxt, 0, 0);
        chatRow.Controls.Add(btnOpenIdBot, 1, 0);
        tgGrid.Controls.Add(chatRow, 1, 2);

        _testTelegramBtn.Height = 36;
        _testTelegramBtn.Width = 230;
        _testTelegramBtn.Anchor = AnchorStyles.Left;
        _testTelegramBtn.Click += async (_, _) => await TestTelegramAsync();
        tgGrid.Controls.Add(_testTelegramBtn, 1, 3);
        tgGroup.Controls.Add(tgGrid);
        grid.Controls.Add(tgGroup, 0, 0);

        // WhatsApp GroupBox
        var waGroup = new GroupBox
        {
            Dock = DockStyle.Fill,
            Text = "💬 WhatsApp API Konfigürasyonu (UltraMsg / GreenAPI)",
            Font = new Font("Segoe UI Semibold", 9.5F),
            ForeColor = UiStyle.TextDark,
            Padding = new Padding(12),
            Margin = new Padding(0, 8, 0, 0)
        };
        var waGrid = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 6, Padding = new Padding(6) };
        waGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 150));
        waGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        waGrid.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
        waGrid.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
        waGrid.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
        waGrid.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
        waGrid.RowStyles.Add(new RowStyle(SizeType.Absolute, 46));
        waGrid.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        waGrid.Controls.Add(_whatsappEnabledChk, 0, 0);
        waGrid.SetColumnSpan(_whatsappEnabledChk, 2);

        waGrid.Controls.Add(new Label { Text = "API Endpoint URL:", Anchor = AnchorStyles.Left, AutoSize = true, ForeColor = UiStyle.TextDark, UseMnemonic = false }, 0, 1);
        _whatsappUrlTxt.Dock = DockStyle.Fill;
        waGrid.Controls.Add(_whatsappUrlTxt, 1, 1);

        waGrid.Controls.Add(new Label { Text = "API Token:", Anchor = AnchorStyles.Left, AutoSize = true, ForeColor = UiStyle.TextDark, UseMnemonic = false }, 0, 2);
        _whatsappTokenTxt.Dock = DockStyle.Fill;
        waGrid.Controls.Add(_whatsappTokenTxt, 1, 2);

        waGrid.Controls.Add(new Label { Text = "Telefon (Ülke kodlu):", Anchor = AnchorStyles.Left, AutoSize = true, ForeColor = UiStyle.TextDark, UseMnemonic = false }, 0, 3);
        _whatsappPhoneTxt.Dock = DockStyle.Fill;
        waGrid.Controls.Add(_whatsappPhoneTxt, 1, 3);

        _testWhatsAppBtn.Height = 36;
        _testWhatsAppBtn.Width = 230;
        _testWhatsAppBtn.Anchor = AnchorStyles.Left;
        _testWhatsAppBtn.Click += async (_, _) => await TestWhatsAppAsync();
        waGrid.Controls.Add(_testWhatsAppBtn, 1, 4);
        waGroup.Controls.Add(waGrid);
        grid.Controls.Add(waGroup, 0, 1);

        return grid;
    }

    private Control BuildEventsPanel()
    {
        var group = new GroupBox
        {
            Dock = DockStyle.Fill,
            Text = "📋 Anlık Bildirim Tetikleyicileri & Gece Raporu",
            Font = new Font("Segoe UI Semibold", 9.5F),
            ForeColor = UiStyle.TextDark,
            Padding = new Padding(12),
            Margin = new Padding(8, 0, 0, 0)
        };
        var scroll = new ModernScrollPanel { Dock = DockStyle.Fill, Margin = Padding.Empty, Padding = new Padding(0, 0, 4, 0) };
        var stack = new FlowLayoutPanel { Dock = DockStyle.Top, FlowDirection = FlowDirection.TopDown, Padding = new Padding(12), WrapContents = false, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, AutoScroll = false };

        stack.Controls.Add(_notifyNewOrderChk);
        stack.Controls.Add(_notifyOpportunityChk);
        stack.Controls.Add(_notifyBatchFinishedChk);
        stack.Controls.Add(_notifyAutomationRunChk);
        stack.Controls.Add(_notifyAbTestWinnerChk);
        stack.Controls.Add(_notifyErrorChk);

        // ── 🌙 Gece Finans Raporu Paneli ──
        var pnlNight = new Panel { AutoSize = true, Width = 480, Margin = new Padding(0, 12, 0, 0), Padding = new Padding(10), BackColor = UiStyle.CardBackground };
        var nightLayout = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, FlowDirection = FlowDirection.TopDown };
        nightLayout.Controls.Add(_dailyNightReportChk);

        var timeRow = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.LeftToRight, Margin = new Padding(20, 6, 0, 6) };
        timeRow.Controls.Add(new Label { Text = "⏰ Gönderim Saati (SS:DD):", AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(0, 4, 4, 0) });
        timeRow.Controls.Add(_dailyReportTimeTxt);
        nightLayout.Controls.Add(timeRow);

        _testNightReportBtn.Height = 36;
        _testNightReportBtn.Click += async (_, _) => await TestNightReportAsync();
        nightLayout.Controls.Add(_testNightReportBtn);
        pnlNight.Controls.Add(nightLayout);
        stack.Controls.Add(pnlNight);

        var hintLabel = new Label
        {
            Width = 480,
            Height = 110,
            Text = "💡 Hızlı İpucu:\n• Telegram botunuzu 30 saniyede açmak için sol taraftaki '🤖 @BotFather' butonuna basabilirsiniz.\n• Chat ID'nizi öğrenmek için '🆔 Chat ID'mi Aç' butonunu kullanabilirsiniz.\n• Gece Finans Raporu ve sipariş bildirimleri VDS üzerinde 7/24 arka planda otomatik çalışır.",
            ForeColor = UiStyle.TextMuted,
            Font = new Font("Segoe UI", 9F, FontStyle.Italic),
            Margin = new Padding(0, 12, 0, 0),
            UseMnemonic = false
        };
        stack.Controls.Add(hintLabel);

        scroll.SetContent(stack);
        group.Controls.Add(scroll);
        return group;
    }

    private void LoadValues()
    {
        _telegramEnabledChk.Checked = _settings.TelegramEnabled;
        _telegramTokenTxt.Text = _settings.TelegramBotToken;
        _telegramChatIdTxt.Text = _settings.TelegramChatId;

        _whatsappEnabledChk.Checked = _settings.WhatsAppEnabled;
        _whatsappUrlTxt.Text = _settings.WhatsAppApiUrl;
        _whatsappTokenTxt.Text = _settings.WhatsAppToken;
        _whatsappPhoneTxt.Text = _settings.WhatsAppPhone;

        _notifyNewOrderChk.Checked = _settings.NotifyOnNewOrder;
        _notifyOpportunityChk.Checked = _settings.NotifyOnOpportunityFound;
        _notifyBatchFinishedChk.Checked = _settings.NotifyOnBatchQueueFinished;
        _notifyAutomationRunChk.Checked = _settings.NotifyOnAutomationRun;

        _notifyAbTestWinnerChk.Checked = _settings.NotifyOnAbTestWinner;
        _notifyErrorChk.Checked = _settings.NotifyOnError;

        _dailyNightReportChk.Checked = _settings.EnableDailyFinancialNightReport;
        _dailyReportTimeTxt.Text = string.IsNullOrWhiteSpace(_settings.DailyFinancialReportTime) ? "23:55" : _settings.DailyFinancialReportTime;
    }

    private void SaveValues()
    {
        var cleanToken = System.Text.RegularExpressions.Regex.Replace(_telegramTokenTxt.Text ?? "", @"\s+", "");
        var cleanChatId = System.Text.RegularExpressions.Regex.Replace(_telegramChatIdTxt.Text ?? "", @"\s+", "");
        _telegramTokenTxt.Text = cleanToken;
        _telegramChatIdTxt.Text = cleanChatId;

        _settings.TelegramEnabled = _telegramEnabledChk.Checked;
        _settings.TelegramBotToken = cleanToken;
        _settings.TelegramChatId = cleanChatId;

        _settings.WhatsAppEnabled = _whatsappEnabledChk.Checked;
        _settings.WhatsAppApiUrl = _whatsappUrlTxt.Text.Trim();
        _settings.WhatsAppToken = _whatsappTokenTxt.Text.Trim();
        _settings.WhatsAppPhone = _whatsappPhoneTxt.Text.Trim();

        _settings.NotifyOnNewOrder = _notifyNewOrderChk.Checked;
        _settings.NotifyOnOpportunityFound = _notifyOpportunityChk.Checked;
        _settings.NotifyOnBatchQueueFinished = _notifyBatchFinishedChk.Checked;
        _settings.NotifyOnAutomationRun = _notifyAutomationRunChk.Checked;
        _settings.NotifyOnAbTestWinner = _notifyAbTestWinnerChk.Checked;
        _settings.NotifyOnError = _notifyErrorChk.Checked;

        _settings.EnableDailyFinancialNightReport = _dailyNightReportChk.Checked;
        _settings.DailyFinancialReportTime = string.IsNullOrWhiteSpace(_dailyReportTimeTxt.Text) ? "23:55" : _dailyReportTimeTxt.Text.Trim();

        NotificationSettingsStore.Save(_settings);
        _statusLabel.Text = "Ayarlar başarıyla kaydedildi!";
        MessageBox.Show(this, "Bildirim botu ayarları başarıyla kaydedildi.", "Bildirim Botu", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    private async Task TestTelegramAsync()
    {
        var token = System.Text.RegularExpressions.Regex.Replace(_telegramTokenTxt.Text ?? "", @"\s+", "");
        var chatId = System.Text.RegularExpressions.Regex.Replace(_telegramChatIdTxt.Text ?? "", @"\s+", "");
        _telegramTokenTxt.Text = token;
        _telegramChatIdTxt.Text = chatId;

        if (string.IsNullOrWhiteSpace(token) || string.IsNullOrWhiteSpace(chatId))
        {
            MessageBox.Show(this, "Lütfen Telegram Bot Token ve Chat ID alanlarını doldurun.", "Telegram Test", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        _statusLabel.Text = "Telegram mesajı gönderiliyor...";
        var (success, msg) = await NotificationService.SendTelegramMessageAsync(token, chatId, "🎉 <b>Etsy Marketplace Bot Testi</b>\n\nTelegram bildirim entegrasyonunuz başarıyla çalışıyor!");
        _statusLabel.Text = msg;
        MessageBox.Show(this, msg, "Telegram Test", MessageBoxButtons.OK, success ? MessageBoxIcon.Information : MessageBoxIcon.Error);
    }

    private async Task TestNightReportAsync()
    {
        _statusLabel.Text = "Finans raporu derleniyor ve Telegram'a gönderiliyor...";
        var (success, msg) = await DailyFinancialReportNotificationService.SendDailyReportAsync(isManualTrigger: true);
        _statusLabel.Text = msg;
        MessageBox.Show(this, msg, "🌙 Gece Finans Raporu", MessageBoxButtons.OK, success ? MessageBoxIcon.Information : MessageBoxIcon.Error);
    }

    private async Task TestWhatsAppAsync()
    {
        var url = _whatsappUrlTxt.Text.Trim();
        var token = _whatsappTokenTxt.Text.Trim();
        var phone = _whatsappPhoneTxt.Text.Trim();

        if (string.IsNullOrWhiteSpace(url) || string.IsNullOrWhiteSpace(phone))
        {
            MessageBox.Show(this, "Lütfen WhatsApp API Endpoint URL ve Telefon Numarası alanlarını doldurun.", "WhatsApp Test", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        _statusLabel.Text = "WhatsApp mesajı gönderiliyor...";
        var (success, msg) = await NotificationService.SendWhatsAppMessageAsync(url, token, phone, "💬 Etsy Marketplace Bot: WhatsApp bildirim entegrasyonunuz başarıyla çalışıyor!");
        _statusLabel.Text = msg;
        MessageBox.Show(this, msg, "WhatsApp Test", MessageBoxButtons.OK, success ? MessageBoxIcon.Information : MessageBoxIcon.Error);
    }
}
