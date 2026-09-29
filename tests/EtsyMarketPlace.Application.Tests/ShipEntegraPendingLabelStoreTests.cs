namespace EtsyMarketPlace.Application.Tests;

using System;
using System.IO;
using EtsyMarketPlace.Application.Shipping;
using Xunit;

/// <summary>
/// Bekleyen etiket kaydinin yaz/oku/sil dongusunu dogrular.
/// </summary>
public sealed class ShipEntegraPendingLabelStoreTests
{
    [Fact]
    public void SaveLoadDelete_RoundTrips()
    {
        string dir = Path.Combine(Path.GetTempPath(), "se-pending-" + Guid.NewGuid().ToString("N"));
        try
        {
            ShipEntegraPendingLabelStore.Save(4178829104, 592013697, "{\"a\":1}", "shipentegra-amerika-eko-plus", dir);

            Assert.True(ShipEntegraPendingLabelStore.TryLoad(4178829104, out long orderId, out string json, dir));
            Assert.Equal(592013697, orderId);
            Assert.Equal("{\"a\":1}", json);

            ShipEntegraPendingLabelStore.Delete(4178829104, dir);
            Assert.False(ShipEntegraPendingLabelStore.TryLoad(4178829104, out _, out _, dir));
        }
        finally
        {
            if (Directory.Exists(dir))
            {
                Directory.Delete(dir, true);
            }
        }
    }

    [Fact]
    public void TryLoad_MissingRecord_ReturnsFalse()
    {
        string dir = Path.Combine(Path.GetTempPath(), "se-pending-" + Guid.NewGuid().ToString("N"));
        Assert.False(ShipEntegraPendingLabelStore.TryLoad(1234567, out _, out _, dir));
    }
}
