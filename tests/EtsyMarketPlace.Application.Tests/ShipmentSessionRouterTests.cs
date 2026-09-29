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
}
