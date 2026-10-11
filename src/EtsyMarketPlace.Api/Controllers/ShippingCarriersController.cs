using Microsoft.AspNetCore.Mvc;
using EtsyMarketPlace.Api.Models;
using EtsyMarketPlace.Application.EtsyIntegration;
using EtsyMarketPlace.Application.Shipping;
using EtsyMarketPlace.Domain.Shipping;

namespace EtsyMarketPlace.Api.Controllers;

public record ArasLoginApiRequest(string Username, string Password, string ShopId = "53236321");
public record ArasConnectTokenApiRequest(string Token, string ShopId = "53236321");

[ApiController]
[Route("api/shipping")]
public class ShippingCarriersController : BaseApiController
{
    private readonly IConfiguration _config;
    private readonly IShopSettingsRepository _settingsRepo;
    private readonly IArasGlobalApiClient _arasClient;

    public ShippingCarriersController(
        IConfiguration config,
        IShopSettingsRepository settingsRepo,
        IArasGlobalApiClient arasClient)
    {
        _config = config;
        _settingsRepo = settingsRepo;
        _arasClient = arasClient;
    }

    [HttpPost("aras/login")]
    [EndpointSummary("Aras Global Kullanıcı Adı ve Şifre ile Doğrudan Otomatik Oturum Aç")]
    public async Task<IActionResult> LoginAras([FromBody] ArasLoginApiRequest request, CancellationToken ct = default)
    {
        var resolvedShopId = ResolveShopId(request.ShopId, _config);

        if (string.IsNullOrWhiteSpace(request.Username) || string.IsNullOrWhiteSpace(request.Password))
        {
            return BadRequest(new { success = false, error = "Kullanıcı adı ve şifre boş bırakılamaz." });
        }

        var (success, accessToken, refreshToken, error) = await _arasClient.LoginAsync(request.Username, request.Password, ct);

        if (!success || string.IsNullOrWhiteSpace(accessToken))
        {
            return BadRequest(new { success = false, error = error ?? "Giriş başarısız oldu. Lütfen bilgilerinizi kontrol edin." });
        }

        // 1. Veritabanına şifreli kaydet
        await _settingsRepo.SaveCarrierSessionAsync(new SaveCarrierSessionRequest(
            resolvedShopId,
            "aras",
            accessToken,
            AccountNo: request.Username,
            ServiceLevel: "Standard",
            IsConnected: true
        ), ct);

        // 2. Masaüstü yerel deposu varsa senkronize et
        try
        {
            var settings = ArasGlobalSettingsStore.Load();
            settings.BearerToken = accessToken;
            settings.SavedEmail = request.Username;
            settings.TokenLastUpdatedUtc = DateTime.UtcNow;
            ArasGlobalSettingsStore.Save(settings);
        }
        catch { }

        return Ok(new
        {
            success = true,
            isConnected = true,
            token = accessToken,
            refreshToken = refreshToken,
            message = "Aras Global oturumu başarıyla bağlandı ve kaydedildi!"
        });
    }

    [HttpPost("aras/connect-token")]
    [EndpointSummary("Aras Global Tokenini Doğrudan veya Tek Tıkla Aktar ve Bağla")]
    public async Task<IActionResult> ConnectArasToken([FromBody] ArasConnectTokenApiRequest request, CancellationToken ct = default)
    {
        var resolvedShopId = ResolveShopId(request.ShopId, _config);

        if (string.IsNullOrWhiteSpace(request.Token))
        {
            return BadRequest(new { success = false, error = "Token değeri boş olamaz." });
        }

        string cleanToken = request.Token.Trim();
        if (cleanToken.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            cleanToken = cleanToken[7..].Trim();
        }

        if (cleanToken.Length < 25 || !cleanToken.Contains("eyJ"))
        {
            return BadRequest(new { success = false, error = "Geçersiz token formatı. Canlı bir JWT tokeni olmalıdır." });
        }

        // Veritabanına kaydet
        await _settingsRepo.SaveCarrierSessionAsync(new SaveCarrierSessionRequest(
            resolvedShopId,
            "aras",
            cleanToken,
            AccountNo: "",
            ServiceLevel: "Standard",
            IsConnected: true
        ), ct);

        // Masaüstü deposuyla senkronize et
        try
        {
            var settings = ArasGlobalSettingsStore.Load();
            settings.BearerToken = cleanToken;
            settings.TokenLastUpdatedUtc = DateTime.UtcNow;
            ArasGlobalSettingsStore.Save(settings);
        }
        catch { }

        return Ok(new
        {
            success = true,
            isConnected = true,
            token = cleanToken,
            message = "Aras Global oturum tokeni başarıyla kaydedildi!"
        });
    }

    [HttpGet("aras/session")]
    [EndpointSummary("Aras Global Canlı Oturum Durumu ve Aktif Tokeni Getir")]
    public async Task<IActionResult> GetArasSession([FromQuery] string shopId = "53236321", CancellationToken ct = default)
    {
        var resolvedShopId = ResolveShopId(shopId, _config);

        // 1. Önce veritabanından ham tokeni çek
        var rawToken = await _settingsRepo.GetRawCarrierTokenAsync(resolvedShopId, "aras", ct);

        // 2. Eğer veritabanında yoksa veya boşsa, yerel Windows dosyasını kontrol et (Masaüstü eşitlemesi)
        if (string.IsNullOrWhiteSpace(rawToken))
        {
            try
            {
                var local = ArasGlobalSettingsStore.Load();
                if (!string.IsNullOrWhiteSpace(local.BearerToken))
                {
                    rawToken = local.BearerToken;
                    // Veritabanına da eşitle
                    await _settingsRepo.SaveCarrierSessionAsync(new SaveCarrierSessionRequest(
                        resolvedShopId,
                        "aras",
                        rawToken,
                        local.SavedEmail ?? "",
                        "Standard",
                        true
                    ), ct);
                }
            }
            catch { }
        }

        if (string.IsNullOrWhiteSpace(rawToken))
        {
            return Ok(new
            {
                carrierId = "aras",
                isConnected = false,
                statusLabel = "⚠️ Giriş Yap",
                token = (string?)null,
                message = "Aktif Aras Global oturumu bulunamadı."
            });
        }

        return Ok(new
        {
            carrierId = "aras",
            isConnected = true,
            statusLabel = "● Bağlı",
            token = rawToken,
            message = "Aras Global aktif oturumu doğrulandı."
        });
    }
}
