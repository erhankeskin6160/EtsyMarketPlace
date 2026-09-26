namespace EtsyMarketPlace.Application.Shipping;

using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using EtsyMarketPlace.Domain.Shipping;

/// <summary>
/// Aras Global ayarlarını ve oturum tokenini yerel diskte saklayan depo.
///
/// Güvenlik: oturum tokeni diskte ŞİFRELİ tutulur (kayıtlı bir <see cref="IShippingSecretProtector"/>
/// varsa) ve dosyada <c>TokenProtected: true</c> işaretiyle işaretlenir. Geriye uyumluluk için eski
/// düz metin dosyalar okunmaya devam eder; bir sonraki kayıtta şifreli yazıma geçilir.
/// </summary>
public static class ArasGlobalSettingsStore
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
            return Path.Combine(folder, "aras-global-settings.json");
        }
    }

    public static ArasGlobalSettings Load(string? customPath = null)
    {
        var path = customPath ?? SettingsPath;
        if (!File.Exists(path))
        {
            return new ArasGlobalSettings();
        }

        try
        {
            string json = File.ReadAllText(path);
            bool protectedAtRest = IsTokenProtectedInFile(json);

            var settings = JsonSerializer.Deserialize<ArasGlobalSettings>(json, JsonOptions) ?? new ArasGlobalSettings();

            if (protectedAtRest && !string.IsNullOrWhiteSpace(settings.BearerToken))
            {
                var protector = ShippingSecretProtector.Current;
                if (protector == null)
                {
                    // Şifre çözücü yok. Şifreli metni token sanıp API'ye göndermektense temizle.
                    settings.BearerToken = string.Empty;
                    settings.TokenLastUpdatedUtc = null;
                }
                else
                {
                    settings.BearerToken = protector.Unprotect(settings.BearerToken);
                }
            }

            return settings;
        }
        catch
        {
            return new ArasGlobalSettings();
        }
    }

    public static void Save(ArasGlobalSettings settings, string? customPath = null)
    {
        if (settings == null)
        {
            return;
        }

        try
        {
            var path = customPath ?? SettingsPath;
            var node = JsonSerializer.SerializeToNode(settings, JsonOptions) as JsonObject ?? new JsonObject();

            var protector = ShippingSecretProtector.Current;
            if (protector != null && !string.IsNullOrWhiteSpace(settings.BearerToken))
            {
                node["BearerToken"] = protector.Protect(settings.BearerToken);
                node["TokenProtected"] = true;
            }
            else
            {
                node["TokenProtected"] = false;
            }

            File.WriteAllText(path, node.ToJsonString(JsonOptions));
        }
        catch { }
    }

    private static bool IsTokenProtectedInFile(string json)
    {
        try
        {
            var node = JsonNode.Parse(json) as JsonObject;
            return node?["TokenProtected"]?.GetValue<bool>() == true;
        }
        catch
        {
            return false;
        }
    }

    public static void LogTrace(string step, string content)
    {
        try
        {
            var folder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "SimilarProductsWinForms");
            Directory.CreateDirectory(folder);
            string logPath = Path.Combine(folder, "aras_shipment_api_trace.log");
            string line = $"[{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss.fff}] [{step}]\n{content}\n----------------------------------------\n";
            File.AppendAllText(logPath, line);
        }
        catch { }
    }
}
