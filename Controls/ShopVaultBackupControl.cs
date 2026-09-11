namespace SimilarProductsWinForms.Controls;

using System;
using System.Drawing;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using EtsyMarketPlace.Application.ShopVault.Services;
using EtsyMarketPlace.Domain.ShopVault.Entities;
using EtsyMarketPlace.Domain.ShopVault.Interfaces;
using SimilarProductsWinForms.Models;
using SimilarProductsWinForms.Services;

internal sealed class ShopVaultBackupControl : UserControl
{
    private readonly IShopVaultRepository _repository;
    private readonly ShopVaultBackupService _backupService;
    private readonly ShopVaultPackagerService _packagerService;
    private readonly EtsyApiClient _apiClient;

    private readonly ListBox _sessionListBox = new();
    private readonly Label _lblActiveShopBadge = new();
    private readonly ModernCheckBox _chkDownloadImages = new() { Text = "Yüksek Çözünürlüklü Orijinal Fotoğrafları İndir (url_fullxfull)", Checked = true, AutoSize = true };
    private readonly ModernButtonControl _btnStartBackup = new();
    private readonly ProgressBar _progressBar = new();
    private readonly Label _lblProgressStats = new();
    private readonly RichTextBox _txtLog = new();
    private readonly Label _lblSessionDetails = new();

    private CancellationTokenSource? _cts;

    public event Action<string>? BackupCompleted;
    public event Action<string>? SessionSelected;

    public ShopVaultBackupControl(
        IShopVaultRepository repository,
        ShopVaultBackupService backupService,
        ShopVaultPackagerService packagerService,
        EtsyApiClient apiClient)
    {
        _repository = repository;
        _backupService = backupService;
        _packagerService = packagerService;
        _apiClient = apiClient;

        Dock = DockStyle.Fill;
        BuildLayout();
        _ = LoadSessionsAsync();
        _ = UpdateShopBadgeAsync();
    }

    private void BuildLayout()
    {
        var grid = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            Padding = new Padding(8)
        };
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 420));
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        // LEFT PANEL: Backup Archive List
        var leftCard = new ModernCardPanel
        {
            Dock = DockStyle.Fill,
            CardColor = UiStyle.CardBackground,
            BorderColor = UiStyle.BorderColor,
            Padding = new Padding(12),
            Margin = new Padding(0, 0, 6, 0)
        };

        var leftLayout = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 3 };
        leftLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
        leftLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        leftLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));

        var leftTitle = new Label
        {
            Text = "📚 Kayıtlı Mağaza Yedekleri",
            Font = new Font("Segoe UI Semibold", 10.5F, FontStyle.Bold),
            ForeColor = Color.White,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft
        };
        leftLayout.Controls.Add(leftTitle, 0, 0);

        _sessionListBox.Dock = DockStyle.Fill;
        _sessionListBox.BackColor = Color.FromArgb(20, 27, 45);
        _sessionListBox.ForeColor = Color.White;
        _sessionListBox.Font = new Font("Segoe UI", 9.5F);
        _sessionListBox.BorderStyle = BorderStyle.None;
        _sessionListBox.SelectedIndexChanged += (_, _) => OnSessionSelected();
        leftLayout.Controls.Add(_sessionListBox, 0, 1);

        var leftButtons = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            Padding = new Padding(0, 8, 0, 0)
        };

        var btnImport = UiStyle.CreateButton("📂 .etsyvault Aç", isSecondary: true);
        btnImport.Height = 34;
        btnImport.Width = 135;
        btnImport.Click += async (_, _) => await ImportArchiveDialogAsync();
        leftButtons.Controls.Add(btnImport);

        var btnExportZip = UiStyle.CreateButton("💾 Arşiv İndir", isSecondary: true);
        btnExportZip.Height = 34;
        btnExportZip.Width = 135;
        btnExportZip.Click += async (_, _) => await ExportArchiveDialogAsync();
        leftButtons.Controls.Add(btnExportZip);

        var btnDelete = UiStyle.CreateButton("🗑️ Sil", isSecondary: true);
        btnDelete.Height = 34;
        btnDelete.Width = 85;
        btnDelete.Click += async (_, _) => await DeleteSelectedSessionAsync();
        leftButtons.Controls.Add(btnDelete);

        leftLayout.Controls.Add(leftButtons, 0, 2);
        leftCard.Controls.Add(leftLayout);
        grid.Controls.Add(leftCard, 0, 0);

        // RIGHT PANEL: Live Backup Hub & Log Console
        var rightCard = new ModernCardPanel
        {
            Dock = DockStyle.Fill,
            CardColor = UiStyle.CardBackground,
            BorderColor = UiStyle.BorderColor,
            Padding = new Padding(14),
            Margin = new Padding(6, 0, 0, 0)
        };

        var rightLayout = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 7 };
        rightLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 46)); // Shop badge & status
        rightLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 32)); // Options
        rightLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 50)); // Big action button
        rightLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 28)); // Progress Bar
        rightLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 26)); // Progress Stats label
        rightLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); // Log Console
        rightLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 32)); // Session details footer

        // Row 0: Shop Badge
        _lblActiveShopBadge.Text = "🏬 Canlı Etsy Mağazası: Doğrulanıyor...";
        _lblActiveShopBadge.Font = new Font("Segoe UI Semibold", 10.5F, FontStyle.Bold);
        _lblActiveShopBadge.ForeColor = Color.FromArgb(129, 140, 248);
        _lblActiveShopBadge.Dock = DockStyle.Fill;
        _lblActiveShopBadge.TextAlign = ContentAlignment.MiddleLeft;
        rightLayout.Controls.Add(_lblActiveShopBadge, 0, 0);

        // Row 1: Options
        _chkDownloadImages.ForeColor = Color.FromArgb(226, 232, 240);
        _chkDownloadImages.Font = new Font("Segoe UI", 9F);
        rightLayout.Controls.Add(_chkDownloadImages, 0, 1);

        // Row 2: Action Button
        _btnStartBackup.Text = "🚀 TÜM MAĞAZAYI ŞİMDİ YEDEKLE (Fotoğraflar & Varyasyonlar)";
        _btnStartBackup.Dock = DockStyle.Fill;
        _btnStartBackup.NormalColor = Color.FromArgb(79, 70, 229);
        _btnStartBackup.HoverColor = Color.FromArgb(99, 102, 241);
        _btnStartBackup.ForeColor = Color.White;
        _btnStartBackup.Font = new Font("Segoe UI Semibold", 10.5F, FontStyle.Bold);
        _btnStartBackup.Click += async (_, _) => await StartBackupProcessAsync();
        rightLayout.Controls.Add(_btnStartBackup, 0, 2);

        // Row 3: Progress Bar
        _progressBar.Dock = DockStyle.Fill;
        _progressBar.Height = 22;
        _progressBar.Style = ProgressBarStyle.Continuous;
        rightLayout.Controls.Add(_progressBar, 0, 3);

        // Row 4: Progress Stats
        _lblProgressStats.Text = "Hazır. 'Tüm Mağazayı Şimdi Yedekle' butonuna basarak tam afet kopyası alabilirsiniz.";
        _lblProgressStats.Font = new Font("Segoe UI Semibold", 8.8F);
        _lblProgressStats.ForeColor = UiStyle.TextMuted;
        _lblProgressStats.Dock = DockStyle.Fill;
        _lblProgressStats.TextAlign = ContentAlignment.MiddleLeft;
        rightLayout.Controls.Add(_lblProgressStats, 0, 4);

        // Row 5: Log Console
        _txtLog.Dock = DockStyle.Fill;
        _txtLog.BackColor = Color.FromArgb(15, 23, 42);
        _txtLog.ForeColor = Color.FromArgb(148, 163, 184);
        _txtLog.Font = new Font("Consolas", 8.8F);
        _txtLog.ReadOnly = true;
        _txtLog.BorderStyle = BorderStyle.None;
        rightLayout.Controls.Add(_txtLog, 0, 5);

        // Row 6: Footer details
        _lblSessionDetails.Text = "Seçili Yedek: Yok";
        _lblSessionDetails.Font = new Font("Segoe UI", 8.5F);
        _lblSessionDetails.ForeColor = UiStyle.TextMuted;
        _lblSessionDetails.Dock = DockStyle.Fill;
        _lblSessionDetails.TextAlign = ContentAlignment.MiddleLeft;
        rightLayout.Controls.Add(_lblSessionDetails, 0, 6);

        rightCard.Controls.Add(rightLayout);
        grid.Controls.Add(rightCard, 1, 0);

        Controls.Add(grid);
    }

    private async Task UpdateShopBadgeAsync()
    {
        try
        {
            var settings = EtsyApiSettingsStore.Load();
            var profile = await _apiClient.GetOwnShopProfileAsync(settings);
            _lblActiveShopBadge.Text = $"🏬 Aktif Mağaza: {profile.ShopName} (ID: #{profile.ShopId}) • 🟢 Canlı API Bağlı";
        }
        catch
        {
            _lblActiveShopBadge.Text = "🏬 Aktif Mağaza: API Kimliği Bekleniyor (Ayarlar'dan doğrulayabilirsiniz)";
            _lblActiveShopBadge.ForeColor = Color.FromArgb(245, 158, 11);
        }
    }

    public async Task LoadSessionsAsync()
    {
        _sessionListBox.Items.Clear();
        var sessions = await _repository.GetAllSessionsAsync();
        foreach (var s in sessions)
        {
            _sessionListBox.Items.Add(new SessionListItem(s));
        }

        if (_sessionListBox.Items.Count > 0)
        {
            _sessionListBox.SelectedIndex = 0;
        }
    }

    private void OnSessionSelected()
    {
        if (_sessionListBox.SelectedItem is SessionListItem item)
        {
            var s = item.Session;
            _lblSessionDetails.Text = $"📁 Seçili Yedek: {s.ShopName} • {s.TotalListingsCount} Ürün • {s.TotalImagesCount} Görsel • {s.TotalSizeBytes / (1024 * 1024.0):N1} MB • {s.CreatedAtUtc.ToLocalTime():g}";
            SessionSelected?.Invoke(s.SessionId);
        }
    }

    private async Task StartBackupProcessAsync()
    {
        if (_cts != null)
        {
            _cts.Cancel();
            _btnStartBackup.Text = "🚀 TÜM MAĞAZAYI ŞİMDİ YEDEKLE";
            _cts = null;
            AppendLog("Yedekleme kullanıcı tarafından durduruldu.");
            return;
        }

        _cts = new CancellationTokenSource();
        _btnStartBackup.Text = "⏹️ YEDEKLEMEYİ DURDUR";
        _btnStartBackup.NormalColor = Color.FromArgb(225, 29, 72);
        _progressBar.Value = 0;
        _txtLog.Clear();

        AppendLog("=== YEDEKLEME OTURUMU BAŞLATILDI ===");

        var progress = new Progress<ShopVaultBackupProgress>(p =>
        {
            _progressBar.Value = (int)Math.Clamp(p.Percent, 0, 100);
            _lblProgressStats.Text = $"[%{(int)p.Percent}] {p.StatusMessage} (Ürün: {p.ProcessedListings}/{p.TotalListings} | Görsel: {p.ProcessedImages}/{p.TotalImages})";
            AppendLog($"[{DateTime.Now:HH:mm:ss}] {p.StatusMessage}");
        });

        try
        {
            var session = await _backupService.RunBackupAsync(_chkDownloadImages.Checked, progress, _cts.Token);
            AppendLog($"=== YEDEKLEME BAŞARIYLA TAMAMLANDI ===\nOturum ID: {session.SessionId}\nÜrün Sayısı: {session.TotalListingsCount}\nGörsel Sayısı: {session.TotalImagesCount}");
            await LoadSessionsAsync();
            BackupCompleted?.Invoke(session.SessionId);
            MessageBox.Show(this, $"Mağaza yedekleme başarıyla tamamlandı!\n\nToplam Ürün: {session.TotalListingsCount}\nToplam Görsel: {session.TotalImagesCount}\nBoyut: {session.TotalSizeBytes / (1024 * 1024.0):N1} MB", "Yedekleme Başarılı", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (OperationCanceledException)
        {
            AppendLog("Yedekleme iptal edildi.");
        }
        catch (Exception ex)
        {
            AppendLog($"HATA: {ex.Message}");
            MessageBox.Show(this, $"Yedekleme sırasında hata oluştu:\n{ex.Message}", "Hata", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            _btnStartBackup.Text = "🚀 TÜM MAĞAZAYI ŞİMDİ YEDEKLE";
            _btnStartBackup.NormalColor = Color.FromArgb(79, 70, 229);
            _cts = null;
        }
    }

    private async Task ImportArchiveDialogAsync()
    {
        using var ofd = new OpenFileDialog
        {
            Filter = "Etsy Kasa Arşivi (*.etsyvault;*.zip)|*.etsyvault;*.zip|Tüm Dosyalar (*.*)|*.*",
            Title = ".etsyvault veya .zip Arşivi Seçin"
        };

        if (ofd.ShowDialog(this) == DialogResult.OK)
        {
            try
            {
                var progress = new Progress<string>(msg => AppendLog($"[İçe Aktar] {msg}"));
                var session = await _packagerService.ImportArchiveAsync(ofd.FileName, progress);
                await LoadSessionsAsync();
                BackupCompleted?.Invoke(session.SessionId);
                MessageBox.Show(this, $"Arşiv başarıyla içe aktarıldı!\n\nMağaza: {session.ShopName}\nÜrün Sayısı: {session.TotalListingsCount}", "Başarılı", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, $"Arşiv içe aktarılamadı:\n{ex.Message}", "Hata", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }

    private async Task ExportArchiveDialogAsync()
    {
        if (_sessionListBox.SelectedItem is not SessionListItem item)
        {
            MessageBox.Show(this, "Lütfen dışa aktarılacak bir yedek seçin.", "Bilgi", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        using var sfd = new SaveFileDialog
        {
            Filter = "Etsy Kasa Arşivi (*.etsyvault)|*.etsyvault|ZIP Arşivi (*.zip)|*.zip",
            FileName = $"EtsyVault_{item.Session.ShopName}_{item.Session.CreatedAtUtc:yyyyMMdd}.etsyvault",
            Title = "Yedek Paketini Kaydedin"
        };

        if (sfd.ShowDialog(this) == DialogResult.OK)
        {
            try
            {
                var progress = new Progress<string>(msg => AppendLog($"[Dışa Aktar] {msg}"));
                string exportPath = await _packagerService.ExportSessionToArchiveAsync(item.Session.SessionId, sfd.FileName, progress);
                MessageBox.Show(this, $"Yedek arşivi başarıyla kaydedildi:\n{exportPath}", "Arşiv Hazır", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, $"Dışa aktarma hatası:\n{ex.Message}", "Hata", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }

    private async Task DeleteSelectedSessionAsync()
    {
        if (_sessionListBox.SelectedItem is not SessionListItem item) return;

        var confirm = MessageBox.Show(this, $"'{item.Session.ShopName}' mağazasının {item.Session.CreatedAtUtc:g} tarihli yedeğini silmek istediğinizden emin misiniz?", "Silme Onayı", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
        if (confirm == DialogResult.Yes)
        {
            await _repository.DeleteSessionAsync(item.Session.SessionId);
            await LoadSessionsAsync();
        }
    }

    private void AppendLog(string message)
    {
        if (InvokeRequired)
        {
            BeginInvoke(() => AppendLog(message));
            return;
        }

        _txtLog.AppendText(message + "\n");
        _txtLog.SelectionStart = _txtLog.TextLength;
        _txtLog.ScrollToCaret();
    }

    private sealed class SessionListItem
    {
        public VaultBackupSession Session { get; }
        public SessionListItem(VaultBackupSession session) => Session = session;
        public override string ToString() => $"📦 {Session.ShopName} ({Session.CreatedAtUtc.ToLocalTime():yyyy-MM-dd HH:mm}) - {Session.TotalListingsCount} Ürün";
    }
}
