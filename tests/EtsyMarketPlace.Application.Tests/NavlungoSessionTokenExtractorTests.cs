namespace EtsyMarketPlace.Application.Tests;

using EtsyMarketPlace.Application.Shipping;
using Xunit;

public sealed class NavlungoSessionTokenExtractorTests
{
    [Fact]
    public void TryExtractIdToken_FindsIdTokenInJsonBody()
    {
        const string json = "{\"status\":\"success\",\"id_token\":\"eyJhbGciOiJIUzI1NiJ9.eyJzdWIiOiIxMjM0NTY3ODkwIn0.c2lnbmF0dXJl\"}";

        Assert.Equal(
            "eyJhbGciOiJIUzI1NiJ9.eyJzdWIiOiIxMjM0NTY3ODkwIn0.c2lnbmF0dXJl",
            NavlungoSessionTokenExtractor.TryExtractIdToken(json));
    }

    [Fact]
    public void TryExtractIdToken_HandlesEscapedStorageDump()
    {
        const string dump = "{\\\"id_token\\\":\\\"eyJhbGciOiJIUzI1NiJ9.eyJzdWIiOiJhYmMifQ.c2lnbmF0dXJlMg\\\"}";

        Assert.Equal(
            "eyJhbGciOiJIUzI1NiJ9.eyJzdWIiOiJhYmMifQ.c2lnbmF0dXJlMg",
            NavlungoSessionTokenExtractor.TryExtractIdToken(dump));
    }

    [Fact]
    public void TryExtractIdToken_UsesAccessTokenWhenIdTokenMissing()
    {
        const string json = "{\"accessToken\":\"tok_abcdefghijklmnopqrstuvwxyz123\"}";

        Assert.Equal(
            "tok_abcdefghijklmnopqrstuvwxyz123",
            NavlungoSessionTokenExtractor.TryExtractIdToken(json));
    }

    [Fact]
    public void TryExtractIdToken_FallsBackToJwtScan()
    {
        const string blob = "session-data: eyJAAAAAAAAAA.BBBBBBBBBB.CCCCCC end";

        Assert.Equal(
            "eyJAAAAAAAAAA.BBBBBBBBBB.CCCCCC",
            NavlungoSessionTokenExtractor.TryExtractIdToken(blob));
    }

    [Fact]
    public void TryExtractIdToken_ReturnsNullWhenNoToken()
    {
        Assert.Null(NavlungoSessionTokenExtractor.TryExtractIdToken("hello world, no tokens here"));
        Assert.Null(NavlungoSessionTokenExtractor.TryExtractIdToken(null));
        Assert.Null(NavlungoSessionTokenExtractor.TryExtractIdToken("   "));
    }

    [Fact]
    public void TryExtractFromCookieHeader_FindsIdTokenValue()
    {
        const string header = "locale=tr; id_token=cookie.jwt.value12345; _SessionUser_=abc";

        Assert.Equal(
            "cookie.jwt.value12345",
            NavlungoSessionTokenExtractor.TryExtractFromCookieHeader(header));
    }

    [Fact]
    public void TryExtractFromCookieHeader_ReturnsNullWhenMissing()
    {
        Assert.Null(NavlungoSessionTokenExtractor.TryExtractFromCookieHeader("locale=tr; theme=dark"));
        Assert.Null(NavlungoSessionTokenExtractor.TryExtractFromCookieHeader(null));
    }
}
