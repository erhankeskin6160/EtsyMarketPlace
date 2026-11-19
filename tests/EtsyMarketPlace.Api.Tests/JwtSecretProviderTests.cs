namespace EtsyMarketPlace.Api.Tests;

using System;
using System.IO;
using EtsyMarketPlace.Api.Security;
using Microsoft.Extensions.Configuration;
using Xunit;

public sealed class JwtSecretProviderTests
{
    [Fact]
    public void Resolve_PersistsLocalMachineDpapiSecret_AndReturnsSameSecretOnReload()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"jwt_secret_test_{Guid.NewGuid():N}");
        try
        {
            var config = new ConfigurationBuilder().Build();
            var first = JwtSecretProvider.Resolve(config, tempDir);
            var second = JwtSecretProvider.Resolve(config, tempDir);

            Assert.False(string.IsNullOrWhiteSpace(first));
            Assert.Equal(first, second);

            var keyFile = Path.Combine(tempDir, "jwt-signing.key");
            Assert.True(File.Exists(keyFile));
            var content = File.ReadAllText(keyFile);
            if (OperatingSystem.IsWindows())
            {
                Assert.StartsWith("DPAPI_LM:", content, StringComparison.Ordinal);
            }
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                try { Directory.Delete(tempDir, true); } catch { }
            }
        }
    }
}
