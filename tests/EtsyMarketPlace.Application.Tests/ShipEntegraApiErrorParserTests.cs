namespace EtsyMarketPlace.Application.Tests;

using System;
using EtsyMarketPlace.Domain.Shipping;
using EtsyMarketPlace.Infrastructure.Shipping;
using Xunit;

/// <summary>
/// ShipEntegra iş kuralı ve yetki redlerinin çözümlenmesini canlı örneklerle doğrular.
/// Canlı örnekler:
/// - GB için DDP reddi (29.09.2026): ERR.28050.1008
/// - Süresi dolmuş token ile sipariş oluşturma reddi (27.10.2026): ERR.00000.1125 ("You are not authorized to perform this action.")
/// - Geçersiz/yetkisiz API istemcisi: ERR.12015.1035 ("Yetkisiz API istemcisi")
/// </summary>
public sealed class ShipEntegraApiErrorParserTests
{
    private const string DdpRejectionBody =
        @"{""status"":""fail"",""time"":""2026-09-29 17:42:16"",""code"":28050,""data"":[{""message"":""ERR.28050.1008"",""description"":""Bu gönderi için DDP (Delivery Duty Paid) seçeneği geçerli değildir.""}]}";

    private const string UnauthorizedOrderCreationBody =
        @"{""status"":""fail"",""time"":""2026-10-27 14:52:00"",""code"":0,""data"":[{""message"":""ERR.00000.1125"",""description"":""You are not authorized to perform this action.""}]}";

    private const string UnauthorizedApiClientBody =
        @"{""status"":""fail"",""time"":""2026-10-27 15:39:27"",""code"":12015,""data"":[{""message"":""ERR.12015.1035"",""description"":""Yetkisiz API istemcisi""}]}";

    [Fact]
    public void TryParse_DdpReddi_IsKuraliHatasiDondurur()
    {
        var ex = ShipEntegraApiErrorParser.TryParse(DdpRejectionBody, 403);

        Assert.NotNull(ex);
        Assert.Equal("ERR.28050.1008", ex!.Code);
        Assert.Equal("Bu gönderi için DDP (Delivery Duty Paid) seçeneği geçerli değildir.", ex.Description);
        Assert.Equal(403, ex.StatusCode);
        Assert.Contains("ERR.28050.1008", ex.Message);
        Assert.False(ShipEntegraApiErrorParser.IsAuthOrExpiredError(ex.Code, ex.Description));
    }

    [Fact]
    public void TryParse_UnauthorizedOrderCreation_IsParsedAndIdentifiedAsAuthExpired()
    {
        var ex = ShipEntegraApiErrorParser.TryParse(UnauthorizedOrderCreationBody, 400);

        Assert.NotNull(ex);
        Assert.Equal("ERR.00000.1125", ex!.Code);
        Assert.Equal("You are not authorized to perform this action.", ex.Description);
        Assert.True(ShipEntegraApiErrorParser.IsAuthOrExpiredError(ex.Code, ex.Description));
    }

    [Fact]
    public void TryParse_UnauthorizedApiClient_IsParsedAndIdentifiedAsAuthExpired()
    {
        var ex = ShipEntegraApiErrorParser.TryParse(UnauthorizedApiClientBody, 401);

        Assert.NotNull(ex);
        Assert.Equal("ERR.12015.1035", ex!.Code);
        Assert.Equal("Yetkisiz API istemcisi", ex.Description);
        Assert.True(ShipEntegraApiErrorParser.IsAuthOrExpiredError(ex.Code, ex.Description));
    }

    [Fact]
    public void TryParse_BasariYaniti_NullDoner()
        => Assert.Null(ShipEntegraApiErrorParser.TryParse(
            @"{""status"":""success"",""code"":24010,""data"":{""orderId"":1}}", 200));

    [Fact]
    public void TryParse_ErrOnekliOlmayanMesaj_NullDoner()
        => Assert.Null(ShipEntegraApiErrorParser.TryParse(
            @"{""status"":""fail"",""data"":[{""message"":""AUTH_FAILED""}]}", 403));

    [Fact]
    public void TryParse_BozukJson_NullDoner()
        => Assert.Null(ShipEntegraApiErrorParser.TryParse("yarim json", 500));

    [Fact]
    public void TryParse_BosVeyaNullGirdi_NullDoner()
    {
        Assert.Null(ShipEntegraApiErrorParser.TryParse(null, 500));
        Assert.Null(ShipEntegraApiErrorParser.TryParse("", 500));
    }

    [Fact]
    public void JwtTokenInspector_PasetoV4Token_ParsesExpirationCorrectly()
    {
        // 2 saat gelecekte bir sentetik Paseto v4 token
        string validPaseto = JwtTokenInspector.CreateSyntheticPasetoToken(TimeSpan.FromHours(2), "erhan_keskin");
        Assert.False(JwtTokenInspector.IsExpired(validPaseto));
        var exp = JwtTokenInspector.GetExpirationUtc(validPaseto);
        Assert.NotNull(exp);
        Assert.True(exp > DateTime.UtcNow);

        // Geçmişte (2 saat önce) bir sentetik Paseto v4 token
        string expiredPaseto = JwtTokenInspector.CreateSyntheticPasetoToken(TimeSpan.FromHours(-2), "erhan_keskin");
        Assert.True(JwtTokenInspector.IsExpired(expiredPaseto));
        var expiredExp = JwtTokenInspector.GetExpirationUtc(expiredPaseto);
        Assert.NotNull(expiredExp);
        Assert.True(expiredExp < DateTime.UtcNow);
    }

    [Fact]
    public void JwtTokenInspector_LiveExpiredShipEntegraToken_IsDetectedAsExpired()
    {
        // 2026-09-29 tarihinde üretilen ve 2026-09-30 tarihinde süresi biten gerçek canlı token formatı
        // payload: {"username":"erhan keskin","exp":"2026-09-30T03:08:37.000Z"}
        string liveTokenPayloadJson = "{\"username\":\"erhan keskin\",\"iat\":\"2026-09-29T15:08:37.000Z\",\"exp\":\"2026-09-30T03:08:37.000Z\"}";
        string b64Payload = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(liveTokenPayloadJson)).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        string livePasetoToken = $"v4.public.{b64Payload}.dummy_signature";

        Assert.True(JwtTokenInspector.IsExpired(livePasetoToken));
        var exp = JwtTokenInspector.GetExpirationUtc(livePasetoToken);
        Assert.NotNull(exp);
        Assert.Equal(2026, exp!.Value.Year);
        Assert.Equal(9, exp!.Value.Month);
        Assert.Equal(30, exp!.Value.Day);
        Assert.Equal(3, exp!.Value.Hour);
        Assert.Equal(8, exp!.Value.Minute);
    }
}
