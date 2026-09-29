namespace EtsyMarketPlace.Application.Tests;

using EtsyMarketPlace.Application.Shipping;
using Xunit;

/// <summary>
/// shippingType politikasını doğrular: ABD DDP(1) canlıda çalışıyor; diğer ülkelerde
/// alan boş bırakılır (panel manuel akışı da boş bırakır; GB için DDP sunucuda
/// reddedildi — 29.09.2026 canlı trafik).
/// </summary>
public sealed class ShipEntegraShippingTypeResolverTests
{
    [Theory]
    [InlineData("US", 1)]
    [InlineData("us", 1)]
    [InlineData(" US ", 1)]
    [InlineData("GB", null)]
    [InlineData("UK", null)]
    [InlineData("DE", null)]
    [InlineData("", null)]
    [InlineData(null, null)]
    public void Resolve_UsIcinDdp_DigerlerindeNull(string? country, int? expected)
        => Assert.Equal(expected, ShipEntegraShippingTypeResolver.Resolve(country));
}
