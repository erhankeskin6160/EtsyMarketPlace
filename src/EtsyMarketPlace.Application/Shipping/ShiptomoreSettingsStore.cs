namespace EtsyMarketPlace.Application.Shipping;

using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using EtsyMarketPlace.Domain.Shipping;

/// <summary>
/// Shiptomore ayarlarını, oturum bilgilerini ve <strong>resmî API anahtarlarını</strong>
/// yerel diskte saklayan depo.
///
/// Güvenlik: Client Secret diskte <strong>şifreli</strong> tutulur (kayıtlı bir
/// <see cref="IShippingSecretProtector"/> varsa — Aras tokeninde kullandığımız DPAPI mekanizması)
/// ve dosyada <c>ClientSecretProtected: true</c> işaretiyle işaretlenir. Yalnız bu Windows
/// kullanıcısı çözebilir. Eski düz metin dosyalar okunmaya devam eder; ilk kayıtta şifreli
/// yazıma geçilir. Şifre çözücü yokken korumalı bir dosya okunursa sır <em>boşaltılır</em> —
/// şifreli metin asla secret sanılmaz.
/// </summary>
public static class ShiptomoreSettingsStore
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public static string SettingsPath
    {
        get
        {
            var folder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "SimilarProductsWinForms");
            Directory.CreateDirectory(folder);
            return Path.Combine(folder, "shiptomore-settings.json");
        }
    }

    public static ShiptomoreSettings Load(string? customPath = null)
    {
        var path = customPath ?? SettingsPath;
        if (!File.Exists(path))
        {
            return new ShiptomoreSettings();
        }

        try
        {
            string json = File.ReadAllText(path);
            bool protectedAtRest = IsSecretProtectedInFile(json);

            var settings = JsonSerializer.Deserialize<ShiptomoreSettings>(json, JsonOptions) ?? new ShiptomoreSettings();

            if (protectedAtRest && !string.IsNullOrWhiteSpace(settings.EncryptedClientSecret))
            {
                var protector = ShippingSecretProtector.Current;
                if (protector == null)
                {
                    // Şifre çözücü kayıtlı değil: şifreli metni secret sanmak yerine temizle.
                    settings.EncryptedClientSecret = string.Empty;
                    settings.ClientSecret = string.Empty;
                }
                else
                {
                    settings.ClientSecret = protector.Unprotect(settings.EncryptedClientSecret);
                }
            }
            else
            {
                // Eski (düz metin) dosya: değeri olduğu gibi kabul et.
                settings.ClientSecret = settings.EncryptedClientSecret;
            }

            return settings;
        }
        catch
        {
            return new ShiptomoreSettings();
        }
    }

    public static void Save(ShiptomoreSettings settings, string? customPath = null)
    {
        if (settings == null)
        {
            return;
        }

        TryWrite(settings, customPath);
    }

    /// <summary>API anahtarlarını tek çağrıda, şifreli biçimde kaydeder.</summary>
    public static void SaveCredentials(string clientId, string clientSecret, string? customPath = null)
    {
        var settings = Load(customPath);
        settings.ClientId = clientId?.Trim();
        settings.ClientSecret = clientSecret ?? string.Empty;
        settings.TokenLastUpdatedUtc = DateTime.UtcNow;
        Save(settings, customPath);
    }

    /// <summary>Kayıtlı API anahtarlarını düz metin olarak döndürür (bellekte).</summary>
    public static (string ClientId, string ClientSecret) LoadCredentials(string? customPath = null)
    {
        var settings = Load(customPath);
        return (settings.ClientId ?? string.Empty, settings.ClientSecret ?? string.Empty);
    }

    /// <summary>Kimlik bilgileri girilmiş mi (Client ID + Secret)?</summary>
    public static bool HasCredentials(string? customPath = null)
    {
        var (id, secret) = LoadCredentials(customPath);
        return !string.IsNullOrWhiteSpace(id) && !string.IsNullOrWhiteSpace(secret);
    }

    private static void TryWrite(ShiptomoreSettings settings, string? customPath)
    {
        try
        {
            var path = customPath ?? SettingsPath;
            var node = JsonSerializer.SerializeToNode(settings, JsonOptions) as JsonObject ?? new JsonObject();

            var protector = ShippingSecretProtector.Current;
            string plainSecret = settings.ClientSecret ?? string.Empty;

            if (protector != null && !string.IsNullOrWhiteSpace(plainSecret))
            {
                node["EncryptedClientSecret"] = protector.Protect(plainSecret);
                node["ClientSecretProtected"] = true;
            }
            else
            {
                // Koruyucu yoksa düz metin yazılır ve bu durum işaretlenir (şeffaflık).
                node["EncryptedClientSecret"] = plainSecret;
                node["ClientSecretProtected"] = false;
            }

            File.WriteAllText(path, node.ToJsonString(JsonOptions));
        }
        catch
        {
        }
    }

    private static bool IsSecretProtectedInFile(string json)
    {
        try
        {
            var node = JsonNode.Parse(json) as JsonObject;
            return node?["ClientSecretProtected"]?.GetValue<bool>() == true;
        }
        catch
        {
            return false;
        }
    }
}
