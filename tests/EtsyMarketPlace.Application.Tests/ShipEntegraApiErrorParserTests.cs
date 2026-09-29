namespace EtsyMarketPlace.Application.Tests;

using EtsyMarketPlace.Infrastructure.Shipping;
using Xunit;

/// <summary>
/// ShipEntegra iş kuralı redlerinin çözümlenmesini canlı örnekle doğrular
/// (GB için DDP reddi, 29.09.2026 canlı trafik: ERR.28050.1008).
/// </summary>
public sealed class ShipEntegraApiErrorParserTests
{
    private const string DdpRejectionBody =
        @"{""status"":""fail"",""time"":""2026-09-29 17:42:16"",""code"":28050,""data"":[{""message"":""ERR.28050.1008"",""description"":""Bu gönderi için DDP (Delivery Duty Paid) seçeneği geçerli değildir.""}]}";

    [Fact]
    public void TryParse_DdpReddi_IsKuraliHatasiDondurur()
    {
        var ex = ShipEntegraApiErrorParser.TryParse(DdpRejectionBody, 403);

        Assert.NotNull(ex);
        Assert.Equal("ERR.28050.1008", ex!.Code);
        Assert.Equal("Bu gönderi için DDP (Delivery Duty Paid) seçeneği geçerli değildir.", ex.Description);
        Assert.Equal(403, ex.StatusCode);
        Assert.Contains("ERR.28050.1008", ex.Message);
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
}
