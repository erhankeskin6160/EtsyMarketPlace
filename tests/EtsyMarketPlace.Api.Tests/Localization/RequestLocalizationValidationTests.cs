namespace EtsyMarketPlace.Api.Tests.Localization;

using System.Net;
using System.Text;
using System.Text.Json;
using Xunit;

public class RequestLocalizationValidationTests : IClassFixture<ApiTestFactory>
{
    private readonly HttpClient _anonymousClient;

    public RequestLocalizationValidationTests(ApiTestFactory factory)
    {
        _anonymousClient = factory.CreateClient();
    }

    [Fact]
    public async Task PostLogin_WithXAppLanguageEn_OverridesTurkishAcceptLanguageAndReturnsEnglishValidation()
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/auth/login")
        {
            Content = new StringContent(
                JsonSerializer.Serialize(new { usernameOrEmail = "", password = "" }),
                Encoding.UTF8,
                "application/json")
        };
        request.Headers.Add("Accept-Language", "tr-TR,tr;q=0.9");
        request.Headers.Add("X-App-Language", "en");

        var response = await _anonymousClient.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("Username/email and password are required.", body);
        Assert.DoesNotContain("gereklidir", body);
    }

    [Fact]
    public async Task PostLogin_WithXAppLanguageTr_ReturnsTurkishValidation()
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/auth/login")
        {
            Content = new StringContent(
                JsonSerializer.Serialize(new { usernameOrEmail = "", password = "" }),
                Encoding.UTF8,
                "application/json")
        };
        request.Headers.Add("Accept-Language", "en-US,en;q=0.9");
        request.Headers.Add("X-App-Language", "tr");

        var response = await _anonymousClient.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("Kullanıcı adı/e-posta ve şifre gereklidir.", body);
        Assert.DoesNotContain("Username/email and password are required.", body);
    }
}
