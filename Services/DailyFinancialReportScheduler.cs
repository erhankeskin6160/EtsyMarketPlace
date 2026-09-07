using System;
using System.Threading;
using System.Threading.Tasks;

namespace SimilarProductsWinForms.Services;

/// <summary>
/// 7/24 Arka planda VDS üzerinde çalışan Günlük Gece Finans Raporu Zamanlayıcısı
/// </summary>
internal sealed class DailyFinancialReportScheduler : IDisposable
{
    private static DailyFinancialReportScheduler? _instance;
    public static DailyFinancialReportScheduler Instance => _instance ??= new DailyFinancialReportScheduler();

    private readonly CancellationTokenSource _cts = new();
    private Task? _loopTask;
    private bool _isDisposed;

    public string LastStatus { get; private set; } = "Zamanlayıcı başlatılmadı.";
    public event Action<string>? StatusChanged;

    public void Start()
    {
        if (_loopTask is not null || _isDisposed) return;
        SetStatus("🌙 Günlük Gece Finans Raporu Zamanlayıcısı aktif.");
        _loopTask = Task.Run(() => RunLoopAsync(_cts.Token));
    }

    private async Task RunLoopAsync(CancellationToken ct)
    {
        try
        {
            using var timer = new PeriodicTimer(TimeSpan.FromMinutes(1));
            await CheckAndSendDueReportAsync(ct);

            while (await timer.WaitForNextTickAsync(ct))
            {
                await CheckAndSendDueReportAsync(ct);
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            SetStatus($"Zamanlayıcı hatası: {ex.Message}");
        }
    }

    public async Task CheckAndSendDueReportAsync(CancellationToken ct = default)
    {
        try
        {
            var settings = NotificationSettingsStore.Load();
            if (!settings.EnableDailyFinancialNightReport) return;
            if (!settings.TelegramEnabled || string.IsNullOrWhiteSpace(settings.TelegramBotToken) || string.IsNullOrWhiteSpace(settings.TelegramChatId)) return;

            string targetTimeStr = string.IsNullOrWhiteSpace(settings.DailyFinancialReportTime) ? "23:55" : settings.DailyFinancialReportTime;
            if (!TimeSpan.TryParse(targetTimeStr, out var targetTime))
            {
                targetTime = new TimeSpan(23, 55, 0);
            }

            var now = DateTime.Now;
            var todayStr = now.ToString("yyyy-MM-dd");

            // Eğer bugün zaten gönderildiyse tekrar gönderme
            if (settings.LastDailyFinancialReportSentDate == todayStr) return;

            // Zamanı geldi mi kontrol et (örn: 23:55 veya sonrası)
            if (now.TimeOfDay >= targetTime)
            {
                SetStatus($"🌙 Günlük Gece Finans Raporu hazırlanıyor ({now:HH:mm})...");
                var (success, msg) = await DailyFinancialReportNotificationService.SendDailyReportAsync(settings, ct: ct);
                if (success)
                {
                    SetStatus($"✅ Gece Raporu gönderildi: {now:dd.MM.yyyy HH:mm}");
                }
                else
                {
                    SetStatus($"⚠️ Gece Raporu gönderilemedi: {msg}");
                }
            }
        }
        catch (Exception ex)
        {
            SetStatus($"Kontrol hatası: {ex.Message}");
        }
    }

    private void SetStatus(string value)
    {
        LastStatus = value;
        StatusChanged?.Invoke(value);
    }

    public void Dispose()
    {
        if (_isDisposed) return;
        _isDisposed = true;
        _cts.Cancel();
        _cts.Dispose();
    }
}
