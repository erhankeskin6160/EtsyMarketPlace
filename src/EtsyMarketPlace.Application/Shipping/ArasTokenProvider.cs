namespace EtsyMarketPlace.Application.Shipping;

using System;
using System.Threading;
using System.Threading.Tasks;
using EtsyMarketPlace.Domain.Shipping;

/// <summary>
/// Geçerli Aras Global oturum tokenini sağlar; gerektiğinde sessizce yeniler.
/// </summary>
public interface IArasTokenProvider
{
    /// <summary>Kullanılabilir bir token döndürür; gerekirse yeniler. Yenilenemezse istisna fırlatır.</summary>
    Task<string> GetValidTokenAsync(CancellationToken ct = default);

    /// <summary>Bellekteki token'ı geçersiz kılar (401 sonrası yenilemeyi zorlamak için).</summary>
    void Invalidate();

    /// <summary>Teşhis amaçlı: bellekteki son bilinen token (olabilir boş).</summary>
    string PeekToken { get; }
}

/// <summary>
/// Tek uçuşlu (single-flight) token sağlayıcı.
/// Aynı anda kaç çağrı gelirse gelsin en fazla bir yenileme çalışır; diğerleri aynı sonucu bekler.
/// Token süresi <see cref="ProactiveWindow"/> eşiğinin altına düştüğünde istek bekletilmeden ÖNDEN yenilenir.
/// </summary>
public sealed class ArasTokenProvider : IArasTokenProvider
{
    private readonly Func<CancellationToken, Task<string?>> _refreshAsync;
    private readonly Func<ArasGlobalSettings> _loadSettings;
    private readonly Action<ArasGlobalSettings> _saveSettings;
    private readonly int _leewaySeconds;
    private readonly SemaphoreSlim _gate = new(1, 1);

    private string _token = string.Empty;
    private DateTime? _expiryUtc;
    private int _refreshCount;
    private bool _forceRefresh;

    /// <summary>Denemede gözlemlenebilir yenileme sayısı (test/teşhis amaçlı).</summary>
    public int RefreshCount => _refreshCount;

    /// <summary>Token süresi dolmadan bu kadar önce yenilenir.</summary>
    public TimeSpan ProactiveWindow { get; }

    public ArasTokenProvider(
        Func<CancellationToken, Task<string?>> refreshAsync,
        Func<ArasGlobalSettings>? loadSettings = null,
        Action<ArasGlobalSettings>? saveSettings = null,
        TimeSpan? proactiveWindow = null,
        int leewaySeconds = 120)
    {
        _refreshAsync = refreshAsync ?? throw new ArgumentNullException(nameof(refreshAsync));
        _loadSettings = loadSettings ?? (() => new ArasGlobalSettings());
        _saveSettings = saveSettings ?? (_ => { });
        ProactiveWindow = proactiveWindow ?? TimeSpan.FromMinutes(5);
        _leewaySeconds = Math.Max(0, leewaySeconds);
    }

    public string PeekToken => _token;

    public void Invalidate()
    {
        _token = string.Empty;
        _expiryUtc = null;
        // 401 bize bu token'ın ölü olduğunu söyledi: diskteki kopyası da ölüdür.
        // Bu yüzden bir sonraki çağrı diske düşmeden doğrudan yenilemeye gider.
        _forceRefresh = true;
    }

    public async Task<string> GetValidTokenAsync(CancellationToken ct = default)
    {
        // 1) Bellek — hızlı yol
        if (!_forceRefresh && IsUsable(_token))
        {
            return _token;
        }

        // 2) Tek uçuşlu yenileme
        await _gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            if (!_forceRefresh && IsUsable(_token))
            {
                return _token;
            }

            // 3) Disk (yalnızca zorunlu yenileme yoksa)
            if (!_forceRefresh)
            {
                var settings = _loadSettings();
                string stored = settings?.CleanToken ?? string.Empty;
                if (IsUsable(stored))
                {
                    Adopt(stored);
                    return _token;
                }
            }

            // 4) Yenileme merdiveni (tarayıcı otomasyonu çağıran tarafından yürütülür)
            _refreshCount++;
            string? refreshed;
            try
            {
                refreshed = await _refreshAsync(ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                throw new ArasGlobalTokenExpiredException(
                    $"Aras Global oturumu sessizce yenilenemedi: {ex.Message}", ex);
            }

            string candidate = (refreshed ?? string.Empty).Trim();
            if (candidate.Length == 0)
            {
                throw new ArasGlobalTokenExpiredException(
                    "Aras Global oturumu sessizce yenilenemedi; geçerli bir token alınamadı.");
            }

            Adopt(candidate);
            _forceRefresh = false;
            Persist(candidate);
            return _token;
        }
        finally
        {
            _gate.Release();
        }
    }

    private void Adopt(string token)
    {
        _token = token;
        _expiryUtc = JwtTokenInspector.GetExpirationUtc(token);
    }

    private void Persist(string token)
    {
        try
        {
            var settings = _loadSettings() ?? new ArasGlobalSettings();
            settings.BearerToken = token;
            settings.TokenLastUpdatedUtc = DateTime.UtcNow;
            _saveSettings(settings);
        }
        catch
        {
            // Kalıcılık başarısız olsa da bellekteki token ile çalışmaya devam edilir.
        }
    }

    /// <summary>
    /// Token kullanılabilir mi? Süresi dolmuşsa ya da dolmaya <see cref="ProactiveWindow"/> kadar
    /// kaldıysa "kullanılamaz" sayılır ve önden yenileme tetiklenir.
    /// </summary>
    private bool IsUsable(string? token)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return false;
        }

        if (JwtTokenInspector.IsExpired(token, _leewaySeconds))
        {
            return false;
        }

        DateTime? expiry = ReferenceEquals(token, _token) ? _expiryUtc : JwtTokenInspector.GetExpirationUtc(token);
        if (expiry is { } exp)
        {
            return exp - DateTime.UtcNow > ProactiveWindow;
        }

        // Bitiş bilgisi okunamayan (opaque) token: yalnızca 401 ile anlaşılır, kullanılabilir say.
        return true;
    }
}
