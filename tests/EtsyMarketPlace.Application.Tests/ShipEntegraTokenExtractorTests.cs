namespace EtsyMarketPlace.Application.Tests;

using EtsyMarketPlace.Application.Shipping;
using Xunit;

public sealed class ShipEntegraTokenExtractorTests
{
    private const string FakeV4 = "v4.public.AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA";
    private const string FakeV4Refresh = "v4.public.BBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBB";
    private const string FakeJwt = "eyJhbGciOiJIUzI1NiJ9.eyJzdWIiOiIxMjM0NTY3ODkwIn0.dozjgNryP4J3jVmNHl0w5N_XgL0n3I9PlFUP0THsR8U";

    [Fact]
    public void ExtractFromText_LoginResponse_ParsesAccessAndRefresh()
    {
        string body = "{\"status\":\"success\",\"data\":{\"accessToken\":\"" + FakeV4 + "\",\"refreshToken\":\"" + FakeV4Refresh + "\",\"tokenType\":\"Bearer\"}}";
        var (access, refresh) = ShipEntegraTokenExtractor.ExtractFromText(body);
        Assert.Equal(FakeV4, access);
        Assert.Equal(FakeV4Refresh, refresh);
    }

    [Fact]
    public void ExtractFromText_BareV4Token_FoundAnywhere()
    {
        var (access, _) = ShipEntegraTokenExtractor.ExtractFromText("xhdr v4.public.ZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZ tail");
        Assert.Equal("v4.public.ZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZ", access);
    }

    [Fact]
    public void ExtractFromText_JwtFallback_Works()
    {
        var (access, _) = ShipEntegraTokenExtractor.ExtractFromText("value=" + FakeJwt + "&x=1");
        Assert.Equal(FakeJwt, access);
    }

    [Fact]
    public void ExtractFromText_EmptyOrNull_ReturnsNulls()
    {
        Assert.Equal((null, null), ShipEntegraTokenExtractor.ExtractFromText(null));
        Assert.Equal((null, null), ShipEntegraTokenExtractor.ExtractFromText(""));
        Assert.Equal((null, null), ShipEntegraTokenExtractor.ExtractFromText("hello world"));
    }

    [Fact]
    public void NormalizeToken_StripsBearerPrefix_ForV4()
    {
        Assert.Equal(FakeV4, ShipEntegraTokenExtractor.NormalizeToken("Bearer " + FakeV4));
        Assert.Equal(FakeV4, ShipEntegraTokenExtractor.NormalizeToken(FakeV4));
    }

    [Fact]
    public void NormalizeToken_RejectsShortOrSpaced()
    {
        Assert.Null(ShipEntegraTokenExtractor.NormalizeToken("Bearer kisa"));
        Assert.Null(ShipEntegraTokenExtractor.NormalizeToken("iki kelime var burada"));
        Assert.Null(ShipEntegraTokenExtractor.NormalizeToken(null));
    }

    [Fact]
    public void ExtractTokenFromAny_HeaderWithBearerJwt_ReturnsJwt()
    {
        Assert.Equal(FakeJwt, ShipEntegraTokenExtractor.ExtractTokenFromAny("Bearer " + FakeJwt));
    }

    [Fact]
    public void ExtractTokenFromAny_IrrelevantValue_ReturnsNull()
    {
        Assert.Null(ShipEntegraTokenExtractor.ExtractTokenFromAny("application/json"));
        Assert.Null(ShipEntegraTokenExtractor.ExtractTokenFromAny(null));
    }

    [Fact]
    public void LooksLikeToken_AcceptsV4AndJwt_RejectsGarbage()
    {
        Assert.True(ShipEntegraTokenExtractor.LooksLikeToken(FakeV4));
        Assert.True(ShipEntegraTokenExtractor.LooksLikeToken(FakeJwt));
        Assert.False(ShipEntegraTokenExtractor.LooksLikeToken("x"));
        Assert.False(ShipEntegraTokenExtractor.LooksLikeToken("bir iki uc"));
    }
}
