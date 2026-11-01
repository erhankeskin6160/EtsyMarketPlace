namespace EtsyMarketPlace.Application.Tests;

using System;
using System.Text;
using EtsyMarketPlace.Domain.Shipping;
using Xunit;

/// <summary>
/// JwtTokenInspector süre sonu davranışı.
/// Regresyon: ShipEntegra'nın gerçek token'ları `v4.public.*` biçiminde ve payload'ı JSON
/// değil; expiry okunamadığında token "süresi dolmuş" sayılırsa geçerli oturumla bile
/// "ShipEntegra oturum tokeni bulunamadı" hatası üretilir.
/// </summary>
public sealed class JwtTokenInspectorExpiryTests
{
    private static string Base64Url(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    [Fact]
    public void IsExpired_ReturnsFalse_ForPasetoV4TokenWithNonJsonPayload()
    {
        // Gerçek ShipEntegra token'ının payload bölümü binary; JSON olarak parse edilemez.
        byte[] binaryPayload = { 0x00, 0xFF, 0x1A, 0x9C, 0x7E, 0x42, 0x00, 0xB3, 0x5D, 0x10, 0xFA };
        string token = "v4.public." + Base64Url(binaryPayload) + ".synthetic_signature";

        Assert.Null(JwtTokenInspector.GetExpirationUtc(token));
        Assert.False(JwtTokenInspector.IsExpired(token));
    }

    [Fact]
    public void IsExpired_ReturnsTrue_ForStandardJwtWithPastExp()
    {
        long pastExp = DateTimeOffset.UtcNow.AddHours(-2).ToUnixTimeSeconds();
        string token = BuildJwt(pastExp);

        Assert.Equal(pastExp, new DateTimeOffset(JwtTokenInspector.GetExpirationUtc(token)!.Value).ToUnixTimeSeconds());
        Assert.True(JwtTokenInspector.IsExpired(token));
    }

    [Fact]
    public void IsExpired_ReturnsFalse_ForStandardJwtWithFutureExp()
    {
        long futureExp = DateTimeOffset.UtcNow.AddHours(2).ToUnixTimeSeconds();
        string token = BuildJwt(futureExp);

        Assert.False(JwtTokenInspector.IsExpired(token));
    }

    [Fact]
    public void IsExpired_ReturnsTrue_ForStructurallyBrokenToken()
    {
        // Eksik/bozuk değerlerde eski davranış korunur.
        Assert.True(JwtTokenInspector.IsExpired("eyJ_broken.payload"));
        Assert.True(JwtTokenInspector.IsExpired("kisa.token"));
    }

    [Fact]
    public void IsExpired_ReturnsTrue_ForMissingToken()
    {
        Assert.True(JwtTokenInspector.IsExpired(null));
        Assert.True(JwtTokenInspector.IsExpired(string.Empty));
        Assert.True(JwtTokenInspector.IsExpired("   "));
    }

    private static string BuildJwt(long exp)
    {
        string header = "{\"alg\":\"HS256\",\"typ\":\"JWT\"}";
        string payload = $"{{\"sub\":\"buyer\",\"exp\":{exp}}}";
        return Base64Url(Encoding.UTF8.GetBytes(header)) + "." +
               Base64Url(Encoding.UTF8.GetBytes(payload)) + ".signature";
    }
}