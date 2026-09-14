namespace SimilarProductsWinForms.Services;

using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using SimilarProductsWinForms.Models;

internal static class EtsyApiSettingsStore
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    private const string DpapiPrefix = "ENC_DPAPI:";

    public static string SettingsPath
    {
        get
        {
            var folder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "SimilarProductsWinForms");
            Directory.CreateDirectory(folder);
            return Path.Combine(folder, "etsy-api-settings.json");
        }
    }

    public static EtsyApiSettings Load()
    {
        if (!File.Exists(SettingsPath))
        {
            return new EtsyApiSettings();
        }

        try
        {
            var fileContent = File.ReadAllText(SettingsPath).Trim();
            if (string.IsNullOrEmpty(fileContent))
            {
                return new EtsyApiSettings();
            }

            string json;
            if (fileContent.StartsWith(DpapiPrefix, StringComparison.Ordinal))
            {
                var base64 = fileContent.Substring(DpapiPrefix.Length);
                var encryptedBytes = Convert.FromBase64String(base64);
                var plainBytes = ProtectedData.Unprotect(encryptedBytes, null, DataProtectionScope.CurrentUser);
                json = Encoding.UTF8.GetString(plainBytes);
            }
            else
            {
                // Legacy düz JSON: Oku ve otomatik olarak DPAPI ile şifreli hale dönüştür
                json = fileContent;
                if (json.StartsWith("{", StringComparison.Ordinal))
                {
                    var migrated = JsonSerializer.Deserialize<EtsyApiSettings>(json);
                    if (migrated != null && migrated.HasApiCredentials)
                    {
                        try { Save(migrated); } catch { /* migration save hatası yutulabilir */ }
                    }
                }
            }

            return JsonSerializer.Deserialize<EtsyApiSettings>(json) ?? new EtsyApiSettings();
        }
        catch (CryptographicException)
        {
            // Bu dosya başka bir bilgisayardan veya başka bir Windows kullanıcısından kopyalanmış!
            // DPAPI şifresi başka ortamda çözülemez; güvenlik amacıyla boş ayar dönülür.
            return new EtsyApiSettings();
        }
        catch
        {
            return new EtsyApiSettings();
        }
    }

    public static void Save(EtsyApiSettings settings)
    {
        try
        {
            var json = JsonSerializer.Serialize(settings, JsonOptions);
            var plainBytes = Encoding.UTF8.GetBytes(json);
            var encryptedBytes = ProtectedData.Protect(plainBytes, null, DataProtectionScope.CurrentUser);
            var fileContent = DpapiPrefix + Convert.ToBase64String(encryptedBytes);
            File.WriteAllText(SettingsPath, fileContent);
        }
        catch
        {
            // DPAPI desteklenmeyen ortamda düz JSON yedek
            var fallbackJson = JsonSerializer.Serialize(settings, JsonOptions);
            File.WriteAllText(SettingsPath, fallbackJson);
        }
    }

    public static void Clear()
    {
        try
        {
            if (File.Exists(SettingsPath))
            {
                File.Delete(SettingsPath);
            }
        }
        catch { }
    }
}
