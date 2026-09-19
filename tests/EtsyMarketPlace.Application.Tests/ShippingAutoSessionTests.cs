namespace EtsyMarketPlace.Application.Tests;

using System;
using System.IO;
using EtsyMarketPlace.Application.Shipping;
using EtsyMarketPlace.Domain.Shipping;
using EtsyMarketPlace.Infrastructure.Shipping;
using Xunit;

public sealed class ShippingAutoSessionTests
{
    [Fact]
    public void ShippingCredentialEncryptor_EncryptAndDecrypt_RoundtripsSuccessfully()
    {
        string original = "P@ssw0rd_Etsy_2026!#$";
        string encrypted = ShippingCredentialEncryptor.Encrypt(original);

        Assert.False(string.IsNullOrWhiteSpace(encrypted));
        Assert.NotEqual(original, encrypted);

        string decrypted = ShippingCredentialEncryptor.Decrypt(encrypted);
        Assert.Equal(original, decrypted);
    }

    [Fact]
    public void ShippingCredentialEncryptor_HandlesEmptyAndNullGracefully()
    {
        Assert.Equal(string.Empty, ShippingCredentialEncryptor.Encrypt(null));
        Assert.Equal(string.Empty, ShippingCredentialEncryptor.Encrypt(""));
        Assert.Equal(string.Empty, ShippingCredentialEncryptor.Decrypt(null));
        Assert.Equal(string.Empty, ShippingCredentialEncryptor.Decrypt(""));
    }

    [Fact]
    public void ArasGlobalSettings_WithCredentials_PersistsCorrectly()
    {
        var testDir = Path.Combine(Path.GetTempPath(), "ArasCredTest_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(testDir);
        var testFile = Path.Combine(testDir, "aras.json");

        try
        {
            var settings = new ArasGlobalSettings
            {
                SavedEmail = "test@arasglobalcargo.com",
                EncryptedPassword = ShippingCredentialEncryptor.Encrypt("secret123"),
                AutoRefreshEnabled = true
            };

            ArasGlobalSettingsStore.Save(settings, testFile);
            var reloaded = ArasGlobalSettingsStore.Load(testFile);

            Assert.Equal("test@arasglobalcargo.com", reloaded.SavedEmail);
            Assert.True(reloaded.AutoRefreshEnabled);
            Assert.Equal("secret123", ShippingCredentialEncryptor.Decrypt(reloaded.EncryptedPassword));
        }
        finally
        {
            if (Directory.Exists(testDir)) Directory.Delete(testDir, true);
        }
    }

    [Fact]
    public void ShipEntegraSettings_WithCredentials_PersistsCorrectly()
    {
        var testDir = Path.Combine(Path.GetTempPath(), "ShipCredTest_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(testDir);
        var testFile = Path.Combine(testDir, "ship.json");

        try
        {
            var settings = new ShipEntegraSettings
            {
                SavedEmail = "user@shipentegra.com",
                EncryptedPassword = ShippingCredentialEncryptor.Encrypt("my_secure_pass"),
                AutoRefreshEnabled = true
            };

            ShipEntegraSettingsStore.Save(settings, testFile);
            var reloaded = ShipEntegraSettingsStore.Load(testFile);

            Assert.Equal("user@shipentegra.com", reloaded.SavedEmail);
            Assert.True(reloaded.AutoRefreshEnabled);
            Assert.Equal("my_secure_pass", ShippingCredentialEncryptor.Decrypt(reloaded.EncryptedPassword));
        }
        finally
        {
            if (Directory.Exists(testDir)) Directory.Delete(testDir, true);
        }
    }

    [Fact]
    public void PuppeteerShippingSessionManager_ProfileDirectories_CreatedSuccessfully()
    {
        string arasDir = PuppeteerShippingSessionManager.GetProfileDirectory("ArasGlobal");
        string shipDir = PuppeteerShippingSessionManager.GetProfileDirectory("ShipEntegra");

        Assert.True(Directory.Exists(arasDir));
        Assert.True(Directory.Exists(shipDir));
        Assert.Contains("ArasGlobal", arasDir);
        Assert.Contains("ShipEntegra", shipDir);
    }
}
