using System.Security.Cryptography;
using System.Text;

namespace EtsyMarketPlace.Api.Security;

/// <summary>
/// JWT imza anahtarını çözer: yapılandırma (Jwt:Secret) → ortam değişkeni (JWT_SECRET) →
/// diske kalıcı rastgele anahtar (DPAPI korumalı). Sabit/gömülü varsayılan anahtar YOKTUR.
/// </summary>
public static class JwtSecretProvider
{
    private const string SecretFileName = "jwt-signing.key";
    private const int MinimumLength = 32;

    public static string Resolve(IConfiguration configuration, string dataDirectory)
    {
        var configured = configuration["Jwt:Secret"];
        if (IsUsable(configured))
        {
            return configured!.Trim();
        }

        var fromEnvironment = Environment.GetEnvironmentVariable("JWT_SECRET");
        if (IsUsable(fromEnvironment))
        {
            return fromEnvironment!.Trim();
        }

        return LoadOrCreatePersisted(dataDirectory);
    }

    private static bool IsUsable(string? value) =>
        !string.IsNullOrWhiteSpace(value) && value.Trim().Length >= MinimumLength;

    private static string LoadOrCreatePersisted(string dataDirectory)
    {
        try
        {
            Directory.CreateDirectory(dataDirectory);
            var path = Path.Combine(dataDirectory, SecretFileName);

            if (File.Exists(path))
            {
                var unprotect = TryUnprotect(File.ReadAllText(path).Trim());
                if (IsUsable(unprotect))
                {
                    return unprotect!;
                }

                Console.Error.WriteLine("[JWT] Kalıcı imza anahtarı okunamadı; yeni anahtar üretilecek (mevcut oturumlar geçersiz olacak).");
            }

            var secret = Convert.ToBase64String(RandomNumberGenerator.GetBytes(48));
            File.WriteAllText(path, TryProtect(secret));
            Console.Error.WriteLine("[JWT] Jwt:Secret yapılandırılmadı; diske kalıcı, DPAPI korumalı rastgele imza anahtarı üretildi: " + path);
            return secret;
        }
        catch (Exception ex)
        {
            // Son çare: süreç ömrü boyunca geçerli rastgele anahtar (yeniden başlatmada oturumlar geçersiz olur).
            Console.Error.WriteLine("[JWT] Kalıcı anahtar oluşturulamadı (" + ex.Message + "); süreç-içi geçici anahtar kullanılacak.");
            return Convert.ToBase64String(RandomNumberGenerator.GetBytes(48));
        }
    }

    private static string TryProtect(string secret)
    {
        if (OperatingSystem.IsWindows())
        {
            try
            {
                var protectedBytes = ProtectedData.Protect(
                    Encoding.UTF8.GetBytes(secret), null, DataProtectionScope.CurrentUser);
                return "DPAPI:" + Convert.ToBase64String(protectedBytes);
            }
            catch
            {
                // DPAPI kullanılamıyorsa ham base64 ile devam edilir (dosya izinleriyle korunur).
            }
        }

        return "RAW:" + Convert.ToBase64String(Encoding.UTF8.GetBytes(secret));
    }

    private static string? TryUnprotect(string stored)
    {
        try
        {
            if (stored.StartsWith("DPAPI:", StringComparison.Ordinal))
            {
                if (!OperatingSystem.IsWindows())
                {
                    return null;
                }

                var bytes = ProtectedData.Unprotect(
                    Convert.FromBase64String(stored["DPAPI:".Length..]), null, DataProtectionScope.CurrentUser);
                return Encoding.UTF8.GetString(bytes);
            }

            if (stored.StartsWith("RAW:", StringComparison.Ordinal))
            {
                return Encoding.UTF8.GetString(Convert.FromBase64String(stored["RAW:".Length..]));
            }

            return null;
        }
        catch
        {
            return null;
        }
    }
}
