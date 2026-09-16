namespace SimilarProductsWinForms;

using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using SimilarProductsWinForms.Controls;
using SimilarProductsWinForms.Services;

/// <summary>
/// Yeni ve mevcut kullanıcıların programı tek tıkla GitHub 'dev-latest' sürümüne
/// güncelleyebilmesini sağlayan modern, görsel ilerleme çubuklu istemci (client) güncelleyici.
/// </summary>
internal sealed class ClientUpdateDialog : Form
{
    private readonly Label _lblTitle = new();
    private readonly Label _lblStatus = new();
    private readonly Label _lblDetails = new();
    private readonly ProgressBar _progressBar = new();
    private readonly ModernButtonControl _btnAction = new();
    private readonly Button _btnCancel = new();
    private CancellationTokenSource? _downloadCts;
    private bool _isDownloading;

    public ClientUpdateDialog()
    {
        InitializeUi();
        Shown += async (_, _) => await CheckUpdateStatusAsync();
    }

    private void InitializeUi()
    {
        Text = "🚀 EtsyMarketPlace - Sürüm Güncelleyici (Client)";
        Size = new Size(520, 260);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterParent;
        MaximizeBox = false;
        MinimizeBox = false;
        BackColor = UiStyle.CardBackground;
        ForeColor = UiStyle.TextDark;
        Font = new Font("Segoe UI", 9F);

        var pnlRoot = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 5,
            Padding = new Padding(20, 16, 20, 16)
        };
        pnlRoot.RowStyles.Add(new RowStyle(SizeType.Absolute, 36)); // Header
        pnlRoot.RowStyles.Add(new RowStyle(SizeType.Absolute, 30)); // Status
        pnlRoot.RowStyles.Add(new RowStyle(SizeType.Absolute, 28)); // Progress bar
        pnlRoot.RowStyles.Add(new RowStyle(SizeType.Absolute, 30)); // Progress details
        pnlRoot.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); // Buttons

        _lblTitle.Text = "🚀 EtsyMarketPlace Güncelleme Merkezi";
        _lblTitle.Font = new Font("Segoe UI Semibold", 12F, FontStyle.Bold);
        _lblTitle.ForeColor = UiStyle.PrimaryColor;
        _lblTitle.AutoSize = true;
        _lblTitle.Dock = DockStyle.Fill;
        pnlRoot.Controls.Add(_lblTitle, 0, 0);

        _lblStatus.Text = "GitHub üzerindeki son sürüm (dev-latest) kontrol ediliyor...";
        _lblStatus.Font = new Font("Segoe UI", 9.5F);
        _lblStatus.ForeColor = UiStyle.TextDark;
        _lblStatus.AutoSize = true;
        _lblStatus.Dock = DockStyle.Fill;
        pnlRoot.Controls.Add(_lblStatus, 0, 1);

        _progressBar.Dock = DockStyle.Fill;
        _progressBar.Style = ProgressBarStyle.Marquee;
        _progressBar.Height = 22;
        pnlRoot.Controls.Add(_progressBar, 0, 2);

        _lblDetails.Text = "Lütfen bekleyin...";
        _lblDetails.Font = new Font("Segoe UI", 8.5F);
        _lblDetails.ForeColor = UiStyle.TextMuted;
        _lblDetails.AutoSize = true;
        _lblDetails.Dock = DockStyle.Fill;
        pnlRoot.Controls.Add(_lblDetails, 0, 3);

        var pnlButtons = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false,
            Margin = new Padding(0, 8, 0, 0)
        };

        _btnCancel.Text = "Kapat";
        _btnCancel.Font = new Font("Segoe UI", 9F);
        _btnCancel.Size = new Size(90, 34);
        _btnCancel.FlatStyle = FlatStyle.Flat;
        _btnCancel.FlatAppearance.BorderColor = UiStyle.BorderColor;
        _btnCancel.Cursor = Cursors.Hand;
        _btnCancel.Click += (_, _) =>
        {
            if (_isDownloading)
            {
                _downloadCts?.Cancel();
            }
            Close();
        };

        _btnAction.Text = "⚡ Şimdi Güncelle";
        _btnAction.Size = new Size(160, 34);
        _btnAction.NormalColor = UiStyle.PrimaryColor;
        _btnAction.HoverColor = UiStyle.PrimaryHover;
        _btnAction.ForeColor = Color.White;
        _btnAction.Cursor = Cursors.Hand;
        _btnAction.Enabled = false;
        _btnAction.Click += async (_, _) => await StartDownloadAndUpdateAsync();

        pnlButtons.Controls.Add(_btnCancel);
        pnlButtons.Controls.Add(_btnAction);
        pnlRoot.Controls.Add(pnlButtons, 0, 4);

        Controls.Add(pnlRoot);
    }

    private async Task CheckUpdateStatusAsync()
    {
        try
        {
            var update = await VdsUpdateNotifierService.CheckForUpdateAsync();
            _progressBar.Style = ProgressBarStyle.Continuous;
            _progressBar.Value = 0;

            if (update.IsUpdateAvailable)
            {
                var pubTime = update.PublishedAt != DateTimeOffset.MinValue
                    ? update.PublishedAt.LocalDateTime.ToString("dd.MM.yyyy HH:mm")
                    : "En son sürüm";
                _lblStatus.Text = $"🎉 Yeni sürüm tespit edildi! (Tarih: {pubTime})";
                _lblStatus.ForeColor = UiStyle.SuccessColor;
                _lblDetails.Text = "Güncellemek için 'Şimdi Güncelle' butonuna tıklayınız (~92 MB).";
                _btnAction.Enabled = true;
                _btnAction.Text = "⚡ Şimdi Güncelle";
            }
            else
            {
                _lblStatus.Text = "✅ Programınız zaten en son sürümde.";
                _lblStatus.ForeColor = UiStyle.SuccessColor;
                _lblDetails.Text = "Yine de dosyaları yeniden indirip tazelemek için butona tıklayabilirsiniz.";
                _btnAction.Enabled = true;
                _btnAction.Text = "🔄 Yeniden İndir";
            }
        }
        catch (Exception ex)
        {
            _progressBar.Style = ProgressBarStyle.Continuous;
            _progressBar.Value = 0;
            _lblStatus.Text = "⚠️ Güncelleme kontrolü sırasında hata oluştu.";
            _lblStatus.ForeColor = UiStyle.DangerColor;
            _lblDetails.Text = ex.Message;
            _btnAction.Enabled = true;
            _btnAction.Text = "Tekrar Dene";
        }
    }

    private async Task StartDownloadAndUpdateAsync()
    {
        _isDownloading = true;
        _btnAction.Enabled = false;
        _btnCancel.Text = "İptal";
        _lblStatus.Text = "📥 En son sürüm GitHub üzerinden indiriliyor...";
        _lblStatus.ForeColor = UiStyle.PrimaryColor;
        _progressBar.Style = ProgressBarStyle.Continuous;
        _progressBar.Value = 0;

        _downloadCts = new CancellationTokenSource();
        var ct = _downloadCts.Token;

        var currentExe = Environment.ProcessPath ?? Process.GetCurrentProcess().MainModule?.FileName;
        if (string.IsNullOrEmpty(currentExe) || !File.Exists(currentExe))
        {
            MessageBox.Show(this, "Çalışan uygulamanın dosya yolu bulunamadı!", "Hata", MessageBoxButtons.OK, MessageBoxIcon.Error);
            _isDownloading = false;
            _btnAction.Enabled = true;
            return;
        }

        var tempDownloadPath = currentExe + ".download";
        const string downloadUrl = "https://github.com/erhankeskin6160/EtsyMarketPlace/releases/download/dev-latest/SimilarProductsWinForms.exe";

        try
        {
            using var client = new HttpClient();
            client.Timeout = TimeSpan.FromMinutes(5);
            client.DefaultRequestHeaders.Add("User-Agent", "EtsyMarketPlace-ClientUpdater");

            using var response = await client.GetAsync(downloadUrl, HttpCompletionOption.ResponseHeadersRead, ct);
            response.EnsureSuccessStatusCode();

            var totalBytes = response.Content.Headers.ContentLength ?? 95_000_000L;
            var totalMb = Math.Round(totalBytes / (1024.0 * 1024.0), 1);

            await using var contentStream = await response.Content.ReadAsStreamAsync(ct);
            await using var fileStream = new FileStream(tempDownloadPath, FileMode.Create, FileAccess.Write, FileShare.None, 81920, true);

            var buffer = new byte[81920];
            long totalRead = 0;
            int bytesRead;
            var stopwatch = Stopwatch.StartNew();

            while ((bytesRead = await contentStream.ReadAsync(buffer, 0, buffer.Length, ct)) > 0)
            {
                await fileStream.WriteAsync(buffer.AsMemory(0, bytesRead), ct);
                totalRead += bytesRead;

                if (stopwatch.ElapsedMilliseconds > 150)
                {
                    stopwatch.Restart();
                    var currentMb = Math.Round(totalRead / (1024.0 * 1024.0), 1);
                    var percent = totalBytes > 0 ? (int)Math.Clamp((totalRead * 100) / totalBytes, 0, 100) : 0;

                    SafeInvoke(() =>
                    {
                        _progressBar.Value = percent;
                        _lblDetails.Text = $"İndirilen: {currentMb} MB / {totalMb} MB (%{percent})";
                    });
                }
            }

            await fileStream.FlushAsync(ct);
            fileStream.Close();

            _progressBar.Value = 100;
            _lblStatus.Text = "✅ İndirme tamamlandı! Uygulama yeniden başlatılıyor...";
            _lblStatus.ForeColor = UiStyle.SuccessColor;
            _lblDetails.Text = "Eski sürüm kapatılıp yeni sürüm devreye alınıyor, lütfen bekleyin...";

            await Task.Delay(1000, CancellationToken.None);

            // Resilient Restart Helper Script
            LaunchRestartScriptAndExit(currentExe, tempDownloadPath);
        }
        catch (OperationCanceledException)
        {
            _lblStatus.Text = "İndirme kullanıcı tarafından iptal edildi.";
            _lblStatus.ForeColor = UiStyle.TextMuted;
            _lblDetails.Text = "";
            _progressBar.Value = 0;
            _isDownloading = false;
            _btnAction.Enabled = true;
            _btnCancel.Text = "Kapat";
            try { if (File.Exists(tempDownloadPath)) File.Delete(tempDownloadPath); } catch { }
        }
        catch (Exception ex)
        {
            _lblStatus.Text = "❌ İndirme sırasında hata oluştu!";
            _lblStatus.ForeColor = UiStyle.DangerColor;
            _lblDetails.Text = ex.Message;
            _isDownloading = false;
            _btnAction.Enabled = true;
            _btnCancel.Text = "Kapat";
            try { if (File.Exists(tempDownloadPath)) File.Delete(tempDownloadPath); } catch { }
        }
    }

    private static void LaunchRestartScriptAndExit(string targetExe, string tempDownloaded)
    {
        try
        {
            var pid = Process.GetCurrentProcess().Id;
            var scriptPath = Path.Combine(Path.GetTempPath(), $"etsy_update_apply_{pid}.bat");

            var scriptContent = $@"@echo off
chcp 65001 >nul
title EtsyMarketPlace Guncelleme
echo Uygulama kapatiliyor (PID: {pid})...
:waitloop
tasklist /FI ""PID eq {pid}"" 2>NUL | find /I ""{pid}"" >NUL
if not errorlevel 1 (
    timeout /t 1 /nobreak >nul
    goto waitloop
)

echo Yeni surum kuruluyor...
move /y ""{tempDownloaded}"" ""{targetExe}"" >nul
if exist ""{targetExe}"" (
    start """" ""{targetExe}""
) else (
    echo HATA: Uygulama baslatilamadi!
    pause
)
del ""%~f0"" & exit
";

            File.WriteAllText(scriptPath, scriptContent, Encoding.GetEncoding(1254));

            Process.Start(new ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = $"/c \"{scriptPath}\"",
                UseShellExecute = true,
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden
            });

            Application.Exit();
            Environment.Exit(0);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Yeniden başlatma betiği çalıştırılamadı: {ex.Message}", "Hata", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void SafeInvoke(Action action)
    {
        if (IsDisposed || Disposing) return;
        if (InvokeRequired)
        {
            BeginInvoke(action);
        }
        else
        {
            action();
        }
    }
}
