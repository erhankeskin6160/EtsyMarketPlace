namespace EtsyMarketPlace.Application.Tests;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using EtsyMarketPlace.Application.Shipping;
using EtsyMarketPlace.Domain.Shipping;

/// <summary>
/// Oturum tokeninin diskte şifrelenmesi ve tek uçuşlu token sağlayıcısının davranışı.
/// Not: <see cref="ShippingSecretProtector.Current"/> genel bir kayıt olduğu için tüm
/// şifreleme testleri bu sınıfta tutulur ve her testte eski değer geri yazılır.
/// </summary>
public sealed class ArasTokenAndSessionTests
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
        Path.Combine(Path.GetTempPath(), $"aras-test-{Guid.NewGuid():N}.json");

    private static void WithProtector(IShippingSecretProtector? protector, Action body)
    {
        var previous = ShippingSecretProtector.Current;
        ShippingSecretProtector.Current = protector;
        try { body(); }
        finally { ShippingSecretProtector.Current = previous; }
    }

    [Fact]
    public void Save_ProtectsTokenAtRest_AndLoadRestoresIt()
    {
        string path = TempFile();
        try
        {
            WithProtector(new FakeProtector(), () =>
            {
                var settings = new ArasGlobalSettings { BearerToken = "abc.def.ghi", TokenLastUpdatedUtc = DateTime.UtcNow };
                ArasGlobalSettingsStore.Save(settings, path);

                string raw = File.ReadAllText(path);
                Assert.DoesNotContain("abc.def.ghi", raw);
                Assert.Contains("\"TokenProtected\": true", raw);

                var loaded = ArasGlobalSettingsStore.Load(path);
                Assert.Equal("abc.def.ghi", loaded.BearerToken);
            });
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void Load_ReadsLegacyPlaintextFile_Unchanged()
    {
        string path = TempFile();
        try
        {
            File.WriteAllText(path, "{ \"BearerToken\": \"legacy.token.value\", \"AutoRefreshEnabled\": true }");

            WithProtector(new FakeProtector(), () =>
            {
                var loaded = ArasGlobalSettingsStore.Load(path);
                Assert.Equal("legacy.token.value", loaded.BearerToken);
            });
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void Load_WithoutProtector_ClearsProtectedToken_InsteadOfUsingCiphertext()
    {
        string path = TempFile();
        try
        {
            WithProtector(new FakeProtector(), () =>
                ArasGlobalSettingsStore.Save(new ArasGlobalSettings { BearerToken = "secret.token" }, path));

            WithProtector(null, () =>
            {
                var loaded = ArasGlobalSettingsStore.Load(path);
                Assert.Equal(string.Empty, loaded.BearerToken);
                Assert.Null(loaded.TokenLastUpdatedUtc);
                Assert.False(loaded.HasValidTokenFormat);
            });
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void Save_WithoutProtector_WritesPlaintextWithFlagFalse()
    {
        string path = TempFile();
        try
        {
            WithProtector(null, () =>
            {
                ArasGlobalSettingsStore.Save(new ArasGlobalSettings { BearerToken = "plain.token" }, path);
                string raw = File.ReadAllText(path);
                Assert.Contains("plain.token", raw);
                Assert.Contains("\"TokenProtected\": false", raw);
            });
        }
        finally { File.Delete(path); }
    }

    private static ArasGlobalSettings SettingsWith(string token) =>
        new() { BearerToken = token, TokenLastUpdatedUtc = DateTime.UtcNow };

    private sealed class RecordingProvider
    {
        public int RefreshCalls;
        public ArasGlobalSettings? Saved;

        public ArasTokenProvider Build(string storedToken, string? refreshedToken,
            TimeSpan? proactiveWindow = null, TimeSpan? refreshedLifetime = null)
        {
            var load = () => SettingsWith(storedToken);
            return new ArasTokenProvider(
                _ =>
                {
                    RefreshCalls++;
                    return Task.FromResult(refreshedToken);
                },
                load,
                s => Saved = s,
                proactiveWindow,
                leewaySeconds: 120);
        }
    }

    [Fact]
    public async Task Provider_ReusesStoredToken_WhenItIsFarFromExpiry()
    {
        string live = JwtTokenInspector.CreateSyntheticToken(TimeSpan.FromMinutes(30));
        var recorder = new RecordingProvider();
        var provider = recorder.Build(live, "should.not.be.used");

        string result = await provider.GetValidTokenAsync();

        Assert.Equal(live, result);
        Assert.Equal(0, recorder.RefreshCalls);
    }

    [Fact]
    public async Task Provider_RefreshesProactively_WhenInsideWindow()
    {
        string almostExpired = JwtTokenInspector.CreateSyntheticToken(TimeSpan.FromMinutes(4));
        string fresh = JwtTokenInspector.CreateSyntheticToken(TimeSpan.FromMinutes(30));
        var recorder = new RecordingProvider();
        var provider = recorder.Build(almostExpired, fresh, TimeSpan.FromMinutes(5));

        string result = await provider.GetValidTokenAsync();

        Assert.Equal(fresh, result);
        Assert.Equal(1, recorder.RefreshCalls);
        Assert.NotNull(recorder.Saved);
        Assert.Equal(fresh, recorder.Saved!.BearerToken);
    }

    [Fact]
    public async Task Provider_RefreshesWhenTokenAlreadyExpired()
    {
        string expired = JwtTokenInspector.CreateSyntheticToken(TimeSpan.FromSeconds(-30));
        string fresh = JwtTokenInspector.CreateSyntheticToken(TimeSpan.FromMinutes(30));
        var recorder = new RecordingProvider();
        var provider = recorder.Build(expired, fresh);

        string result = await provider.GetValidTokenAsync();

        Assert.Equal(fresh, result);
        Assert.Equal(1, recorder.RefreshCalls);
    }

    [Fact]
    public async Task Provider_SingleFlight_RefreshesOnlyOnceUnderConcurrency()
    {
        string expired = JwtTokenInspector.CreateSyntheticToken(TimeSpan.FromSeconds(-30));
        string fresh = JwtTokenInspector.CreateSyntheticToken(TimeSpan.FromMinutes(30));

        int refreshCalls = 0;
        var tcs = new TaskCompletionSource<string?>(TaskCreationOptions.RunContinuationsAsynchronously);

        var provider = new ArasTokenProvider(
            _ =>
            {
                Interlocked.Increment(ref refreshCalls);
                return tcs.Task;
            },
            () => SettingsWith(expired),
            _ => { });

        var calls = Enumerable.Range(0, 10)
            .Select(_ => provider.GetValidTokenAsync())
            .ToArray();

        // Tüm çağrılar tek yenilemeyi bekliyor olmalı.
        tcs.SetResult(fresh);
        string[] results = await Task.WhenAll(calls);

        Assert.Equal(1, refreshCalls);
        Assert.All(results, r => Assert.Equal(fresh, r));
    }

    [Fact]
    public async Task Provider_ThrowsWhenRefreshYieldsNothing()
    {
        string expired = JwtTokenInspector.CreateSyntheticToken(TimeSpan.FromSeconds(-30));
        var recorder = new RecordingProvider();
        var provider = recorder.Build(expired, null);

        await Assert.ThrowsAsync<ArasGlobalTokenExpiredException>(() => provider.GetValidTokenAsync());
        Assert.Equal(1, recorder.RefreshCalls);
    }

    [Fact]
    public async Task Provider_Invalidate_ForcesRefreshOnNextCall()
    {
        string live = JwtTokenInspector.CreateSyntheticToken(TimeSpan.FromMinutes(30));
        string fresh = JwtTokenInspector.CreateSyntheticToken(TimeSpan.FromMinutes(30));
        var recorder = new RecordingProvider();
        var provider = recorder.Build(live, fresh);

        Assert.Equal(live, await provider.GetValidTokenAsync());
        Assert.Equal(0, recorder.RefreshCalls);

        provider.Invalidate();
        Assert.Equal(fresh, await provider.GetValidTokenAsync());
        Assert.Equal(1, recorder.RefreshCalls);
    }
}
