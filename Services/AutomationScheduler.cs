namespace SimilarProductsWinForms.Services;

using EtsyMarketPlace.Application.Automation;

internal sealed class AutomationScheduler(
    AutomationRunService runService,
    IAutomationSettingsStore settingsStore) : IDisposable
{
    private readonly CancellationTokenSource _cancellation = new();
    private readonly SemaphoreSlim _runLock = new(1, 1);
    private Task? _loopTask;

    public string LastStatus { get; private set; } = "Zamanlayici baslatilmadi.";
    public event Action<string>? StatusChanged;

    public void Start()
    {
        if (_loopTask is not null) return;
        SetStatus("Zamanlayici aktif; ayarlar kontrol ediliyor.");
        _loopTask = Task.Run(() => RunLoopAsync(_cancellation.Token));
    }

    public Task<AutomationRunResult> RunNowAsync(CancellationToken cancellationToken = default) =>
        RunCoreAsync(settingsStore.Load(), cancellationToken);

    private async Task RunLoopAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var timer = new PeriodicTimer(TimeSpan.FromMinutes(1));
            await CheckScheduleAsync(cancellationToken);
            while (await timer.WaitForNextTickAsync(cancellationToken))
                await CheckScheduleAsync(cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
    }

    private async Task CheckScheduleAsync(CancellationToken cancellationToken)
    {
        try
        {
            var settings = settingsStore.Load();
            if (AutomationRunService.IsDue(settings, DateTimeOffset.Now))
                await RunCoreAsync(settings, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            SetStatus($"Otomatik calisma basarisiz: {ex.Message}");
        }
    }

    private async Task<AutomationRunResult> RunCoreAsync(
        AutomationSettings settings,
        CancellationToken cancellationToken)
    {
        if (!await _runLock.WaitAsync(0, cancellationToken))
            throw new InvalidOperationException("Bir otomasyon calismasi zaten devam ediyor.");
        try
        {
            SetStatus("Magaza verileri yenileniyor ve rapor uretiliyor...");
            var result = await runService.RunAsync(settings, cancellationToken: cancellationToken);
            SetStatus($"Tamamlandi: {result.CompletedAt:dd.MM.yyyy HH:mm} | {result.Alerts.Count} uyari");
            return result;
        }
        catch (Exception ex)
        {
            SetStatus($"Calisma basarisiz: {ex.Message}");
            throw;
        }
        finally
        {
            _runLock.Release();
        }
    }

    private void SetStatus(string value)
    {
        LastStatus = value;
        StatusChanged?.Invoke(value);
    }

    public void Dispose()
    {
        _cancellation.Cancel();
        _cancellation.Dispose();
    }
}
