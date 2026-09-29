namespace EtsyMarketPlace.Application.Tests;

using EtsyMarketPlace.Application.Shipping;
using Xunit;

/// <summary>
/// Etiket servis kodu eslemesinin panel karsiliklarini dogru cozdugunu dogrular
/// (panel bundle servis haritasi, 29.09.2026).
/// </summary>
public sealed class ShipEntegraLabelServiceCodeTests
{
    [Theory]
    [InlineData("shipentegra-amerika-eko-plus", "", "shipentegra-amerika-eko-plus")]
    [InlineData("", "ShipEntegra Amerika Eko Plus", "shipentegra-amerika-eko-plus")]
    [InlineData("", "Eko Plus", "shipentegra-eko-plus")]
    [InlineData("", "Widect", "shipentegra-widect")]
    [InlineData("", "Express", "shipentegra-express")]
    [InlineData("shipentegra-smart-express", "", "shipentegra-express")]
    [InlineData("", "shipentegra-almanya-eko-plus", "shipentegra-almanya-eko-plus")]
    public void Resolve_MapsKnownServices(string code, string name, string expected)
        => Assert.Equal(expected, ShipEntegraLabelServiceCodes.Resolve(code, name));

    [Theory]
    [InlineData(null, null)]
    [InlineData("shipentegra-expedited", "")]
    [InlineData("", "Bilinmeyen Servis")]
    public void Resolve_UnknownService_ReturnsNull(string? code, string? name)
        => Assert.Null(ShipEntegraLabelServiceCodes.Resolve(code, name));

    [Theory]
    [InlineData("shipentegra-amerika-eko-plus", 2)]
    [InlineData("shipentegra-eko-plus", 2)]
    [InlineData("shipentegra-eco", 2)]
    [InlineData("se-dhlecommerce-eko-plus", 2)]
    [InlineData("shipentegra-express", 1)]
    [InlineData("shipentegra-smart-express", 1)]
    [InlineData("shipentegra-ups-ekspress", 1)]
    [InlineData(null, 1)]
    [InlineData("", 1)]
    public void ResolveServiceType_EkoIcinIki_DigerIcinBirDondurur(string? code, int expected)
        => Assert.Equal(expected, ShipEntegraLabelServiceCodes.ResolveServiceType(code));
}
