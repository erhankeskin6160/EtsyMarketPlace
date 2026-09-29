namespace EtsyMarketPlace.Application.Tests;

using EtsyMarketPlace.Application.Shipping;
using Xunit;

public sealed class ShipmentSessionRouterTests
{
    [Fact]
    public void IsShipEntegraContext_ProviderShipEntegra_ReturnsTrue()
    {
        Assert.True(ShipmentSessionRouter.IsShipEntegraContext("ShipEntegra", null));
    }

    [Fact]
    public void IsShipEntegraContext_ErrorMessageMentionsShipEntegra_ReturnsTrue()
    {
        Assert.True(ShipmentSessionRouter.IsShipEntegraContext(
            "Aras Global",
            "ShipEntegra oturumunuzun süresi dolmuş. Lütfen yeniden giriş yapın."));
    }

    [Fact]
    public void IsShipEntegraContext_ArasFailure_ReturnsFalse()
    {
        Assert.False(ShipmentSessionRouter.IsShipEntegraContext(
            "Aras Global",
            "Aras Global oturum tokeni bulunamadı veya süresi dolmuş."));
    }

    [Fact]
    public void IsShipEntegraContext_NullInputs_ReturnsFalse()
    {
        Assert.False(ShipmentSessionRouter.IsShipEntegraContext(null, null));
    }

    [Fact]
    public void IsShipEntegraContext_CaseInsensitive_ReturnsTrue()
    {
        Assert.True(ShipmentSessionRouter.IsShipEntegraContext("shipentegra", null));
    }

    [Fact]
    public void IsNetworkLevelFailure_SocketProviderError_ReturnsTrue()
    {
        Assert.True(ShipmentSessionRouter.IsNetworkLevelFailure(
            "ShipEntegra gönderi oluşturma hatası: İstenen hizmet sağlayıcısı yüklenemedi veya başlatılamadı. (api.shipentegra.com:443)"));
    }

    [Fact]
    public void IsNetworkLevelFailure_AsciiVariant_ReturnsTrue()
    {
        Assert.True(ShipmentSessionRouter.IsNetworkLevelFailure("hizmet saglayicisi yuklenemedi"));
    }

    [Fact]
    public void IsNetworkLevelFailure_ConnectionAborted_ReturnsTrue()
    {
        Assert.True(ShipmentSessionRouter.IsNetworkLevelFailure("Connection aborted. (api.shipentegra.com:443)"));
    }

    [Fact]
    public void IsNetworkLevelFailure_TokenExpiredMessage_ReturnsFalse()
    {
        Assert.False(ShipmentSessionRouter.IsNetworkLevelFailure(
            "ShipEntegra oturumunuzun süresi dolmuş. Lütfen yeniden giriş yapın."));
    }

    [Fact]
    public void IsNetworkLevelFailure_NullOrEmpty_ReturnsFalse()
    {
        Assert.False(ShipmentSessionRouter.IsNetworkLevelFailure(null));
        Assert.False(ShipmentSessionRouter.IsNetworkLevelFailure("   "));
    }

    [Theory]
    [InlineData("ShipEntegra yetkilendirme hatası (401 Unauthorized). Oturum süreniz dolmuş olabilir.", true)]
    [InlineData("ShipEntegra oturumunuzun süresi dolmuş. Lütfen yeniden giriş yapın.", true)]
    [InlineData("ShipEntegra oturum tokeni bulunamadı. Lütfen oturum düğmesinden giriş yapın.", true)]
    [InlineData("ShipEntegra isteği reddedildi: Bu gönderi için DDP (Delivery Duty Paid) seçeneği geçerli değildir. (ERR.28050.1008)", false)]
    [InlineData("Token yenilendi ancak gönderi oluşturulamadı: Bu gönderi için DDP (Delivery Duty Paid) seçeneği geçerli değildir.", false)]
    [InlineData(null, false)]
    public void IsSessionFailure_ClassifiesAuthMessages(string? message, bool expected)
        => Assert.Equal(expected, ShipmentSessionRouter.IsSessionFailure(message));

}