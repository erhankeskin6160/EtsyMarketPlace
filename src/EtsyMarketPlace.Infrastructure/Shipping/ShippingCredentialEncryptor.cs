namespace EtsyMarketPlace.Infrastructure.Shipping;

using System;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;

/// <summary>
/// Windows DPAPI (Data Protection API) tabanlı yerel kimlik şifreleme yardımcısı.
/// </summary>
public static class ShippingCredentialEncryptor
{
    public static string Encrypt(string? plainText)
    {
        if (string.IsNullOrEmpty(plainText)) return string.Empty;

        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            // Windows dışı ortamda fallback
            return Convert.ToBase64String(Encoding.UTF8.GetBytes(plainText));
        }

        try
        {
            var plainBytes = Encoding.UTF8.GetBytes(plainText);
            var encrypted = ProtectedData.Protect(plainBytes, null, DataProtectionScope.CurrentUser);
            return Convert.ToBase64String(encrypted);
        }
        catch
        {
            return string.Empty;
        }
    }

    public static string Decrypt(string? cipherText)
    {
        if (string.IsNullOrEmpty(cipherText)) return string.Empty;

        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            try
            {
                return Encoding.UTF8.GetString(Convert.FromBase64String(cipherText));
            }
            catch
            {
                return string.Empty;
            }
        }

        try
        {
            var encryptedBytes = Convert.FromBase64String(cipherText);
            var plain = ProtectedData.Unprotect(encryptedBytes, null, DataProtectionScope.CurrentUser);
            return Encoding.UTF8.GetString(plain);
        }
        catch
        {
            return string.Empty;
        }
    }
}
