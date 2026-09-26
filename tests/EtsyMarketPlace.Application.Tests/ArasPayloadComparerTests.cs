namespace EtsyMarketPlace.Application.Tests;

using System.Linq;
using Xunit;
using EtsyMarketPlace.Application.Shipping;

/// <summary>
/// Panel gövdesi ile bizim gövdemizi karşılaştıran aracın davranışı.
/// </summary>
public sealed class ArasPayloadComparerTests
{
    private const string Ours = """
    {
      "shipmentId": "00000000-0000-0000-0000-000000000000",
      "weight": 0.4,
      "volumetricWeight": 0.6,
      "desi": 0.6,
      "cargoPrice": 13.13,
      "shipmentDimensions": [ { "length": 20, "width": 15, "height": 10, "volumetricWeight": 0.6 } ]
    }
    """;

    private const string Theirs = """
    {
      "shipmentId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
      "weight": 0.4,
      "volumetricWeight": 0.6,
      "cargoPrice": 12.85,
      "packageType": 1,
      "shipmentDimensions": [ { "length": 20, "width": 15, "height": 10, "volumetricWeight": 0.6, "packageCount": 1 } ]
    }
    """;

    [Fact]
    public void FindsFieldsOnlyWeSend()
    {
        var diffs = ArasPayloadComparer.Compare(Ours, Theirs);

        Assert.Contains(diffs, d => d.Kind == PayloadDiffKind.MissingInTheirs && d.Path == "desi");
    }

    [Fact]
    public void FindsFieldsOnlyTheySend()
    {
        var diffs = ArasPayloadComparer.Compare(Ours, Theirs);

        Assert.Contains(diffs, d => d.Kind == PayloadDiffKind.MissingInOurs && d.Path == "packageType");
        Assert.Contains(diffs, d => d.Kind == PayloadDiffKind.MissingInOurs && d.Path == "shipmentDimensions[0].packageCount");
    }

    [Fact]
    public void FindsValueDifferences()
    {
        var diffs = ArasPayloadComparer.Compare(Ours, Theirs);

        var cargo = Assert.Single(diffs.Where(d => d.Path == "cargoPrice" && d.Kind == PayloadDiffKind.ValueDiffers));
        Assert.Equal("13.13", cargo.Ours);
        Assert.Equal("12.85", cargo.Theirs);
    }

    [Fact]
    public void VolumetricWeightIsReportedAsMatching()
    {
        var diffs = ArasPayloadComparer.Compare(Ours, Theirs);

        Assert.DoesNotContain(diffs, d => d.Path.Contains("volumetricWeight", System.StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void IgnoreListSuppressesVolatileIdentifiers()
    {
        var diffs = ArasPayloadComparer.Compare(Ours, Theirs, new[] { "shipmentId" });

        Assert.DoesNotContain(diffs, d => d.Path == "shipmentId");
        Assert.NotEmpty(diffs);
    }

    [Fact]
    public void MissingOrInvalidInputYieldsNoDiffs()
    {
        Assert.Empty(ArasPayloadComparer.Compare(null, Theirs));
        Assert.Empty(ArasPayloadComparer.Compare(Ours, ""));
        Assert.Empty(ArasPayloadComparer.Compare("{ bozuk json", Theirs));
    }
}
