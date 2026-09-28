namespace EtsyMarketPlace.Application.Tests;

using System;
using System.IO;
using Xunit;
using EtsyMarketPlace.Application.Shipping;

/// <summary>
/// Ship to More API anahtarlarının diskte şifreli saklanması ve geri okunması.
/// Genel <see cref="ShippingSecretProtector.Current"/> kaydını değiştirdiği için
/// tüm koruma testleri bu sınıfta toplanır ve her testte eski değer geri yazılır.
/// </summary>
public sealed class ShiptomoreCredentialStorageTests
{
    private sealed class FakeProtector : IShippingSecretProtector
    {
        public string Protect(string? plainText)
            => string.IsNullOrEmpty(plainText)
                ? string.Empty
                : Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(plainText));

        public string Unprotect(string? cipherText)
            => string.IsNullOrEmpty(cipherText)
                ? string.Empty
                : System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(cipherText));
    }

    private static string TempFile() =>
        Path.Combine(Path.GetTempPath(), $"stm-settings-{Guid.NewGuid():N}.json");

    private static void WithProtector(IShippingSecretProtector? protector, Action body)
    {
        var previous = ShippingSecretProtector.Current;
        ShippingSecretProtector.Current = protector;
        try { body(); }
        finally { ShippingSecretProtector.Current = previous; }
    }

    [Fact]
    public void SaveCredentials_NeverWritesTheSecretInPlaintext()
    {
        string path = TempFile();
        try
        {
            WithProtector(new FakeProtector(), () =>
            {
                ShiptomoreSettingsStore.SaveCredentials("client-abc", "super-secret", path);

                string raw = File.ReadAllText(path);
                Assert.DoesNotContain("super-secret", raw);
                Assert.Contains("\"ClientSecretProtected\": true", raw);
                Assert.Contains("client-abc", raw);
            });
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void LoadCredentials_ReturnsTheOriginalValues()
    {
        string path = TempFile();
        try
        {
            WithProtector(new FakeProtector(), () =>
            {
                ShiptomoreSettingsStore.SaveCredentials("client-abc", "super-secret", path);

                var (id, secret) = ShiptomoreSettingsStore.LoadCredentials(path);
                Assert.Equal("client-abc", id);
                Assert.Equal("super-secret", secret);
                Assert.True(ShiptomoreSettingsStore.HasCredentials(path));
            });
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void ProtectedFile_WithoutProtector_ClearsTheSecret()
    {
        string path = TempFile();
        try
        {
            WithProtector(new FakeProtector(), () =>
                ShiptomoreSettingsStore.SaveCredentials("client-abc", "super-secret", path));

            WithProtector(null, () =>
            {
                var (id, secret) = ShiptomoreSettingsStore.LoadCredentials(path);
                Assert.Equal("client-abc", id);
                Assert.Equal(string.Empty, secret);
                Assert.False(ShiptomoreSettingsStore.HasCredentials(path));
            });
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void LegacyPlaintextFile_StillLoads()
    {
        string path = TempFile();
        try
        {
            File.WriteAllText(path, "{ \"ClientId\": \"old-client\", \"EncryptedClientSecret\": \"old-secret\" }");

            WithProtector(new FakeProtector(), () =>
            {
                var (id, secret) = ShiptomoreSettingsStore.LoadCredentials(path);
                Assert.Equal("old-client", id);
                Assert.Equal("old-secret", secret);
            });
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void Save_PreservesUnrelatedSettings()
    {
        string path = TempFile();
        try
        {
            WithProtector(new FakeProtector(), () =>
            {
                ShiptomoreSettingsStore.SaveCredentials("client-abc", "super-secret", path);

                var settings = ShiptomoreSettingsStore.Load(path);
                settings.SessionCookie = "cookie-value";
                ShiptomoreSettingsStore.Save(settings, path);

                var reloaded = ShiptomoreSettingsStore.Load(path);
                Assert.Equal("cookie-value", reloaded.SessionCookie);
                Assert.Equal("client-abc", reloaded.ClientId);
                Assert.Equal("super-secret", reloaded.ClientSecret);
            });
        }
        finally { File.Delete(path); }
    }
}
