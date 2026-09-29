namespace EtsyMarketPlace.Application.Tests;

using System;
using System.Text;
using EtsyMarketPlace.Application.Shipping;
using Xunit;

/// <summary>
/// Etiket yanıtı çözümleyicisini canlı panel trafiğinden alınan gerçek yanıtla doğrular
/// (panel: JSON yanıt + data.label herkese açık PDF URL'si, 29.09.2026).
/// </summary>
public sealed class ShipEntegraLabelResponseParserTests
{
    private const string RealPanelLabelResponse =
        @"{""status"":""success"",""time"":""2026-09-29 17:23:26"",""code"":24010,""data"":{""success"":true,""courier"":""shipentegra"",""orderId"":592013975,""trackingNumber"":592013975,""label"":""https://files.shipentegra.com/labels/shipentegra/fc165ff9044b464a.pdf"",""seOrderId"":11155947,""message"":""errors.global.labelCreated"",""description"":""The ShipEntegra label has been created successfully"",""serviceType"":1,""number"":""M13027C-1790702598159-u494cg6hp7s"",""storeId"":933317,""labelTag"":""SDF"",""specialService"":""ShipEntegra Express""}}";

    [Fact]
    public void TryExtractLabelFileUrl_GercekPanelYaniti_UrlDondurur()
    {
        byte[] bytes = Encoding.UTF8.GetBytes(RealPanelLabelResponse);
        Assert.Equal(
            "https://files.shipentegra.com/labels/shipentegra/fc165ff9044b464a.pdf",
            ShipEntegraLabelResponseParser.TryExtractLabelFileUrl(bytes));
    }

    [Fact]
    public void TryExtractLabelFileUrl_PdfBaytlari_NullDoner()
        => Assert.Null(ShipEntegraLabelResponseParser.TryExtractLabelFileUrl(Encoding.ASCII.GetBytes("%PDF-1.7")));

    [Fact]
    public void TryExtractLabelFileUrl_LabelAlaniOlmayanJson_NullDoner()
        => Assert.Null(ShipEntegraLabelResponseParser.TryExtractLabelFileUrl(
            Encoding.UTF8.GetBytes("{\"status\":\"success\",\"data\":{\"orderId\":1}}")));

    [Fact]
    public void TryExtractLabelFileUrl_UrlOlmayanLabel_NullDoner()
        => Assert.Null(ShipEntegraLabelResponseParser.TryExtractLabelFileUrl(
            Encoding.UTF8.GetBytes("{\"data\":{\"label\":\"etiket-123\"}}")));

    [Fact]
    public void TryExtractLabelFileUrl_BosVeyaNullGirdi_NullDoner()
    {
        Assert.Null(ShipEntegraLabelResponseParser.TryExtractLabelFileUrl(null));
        Assert.Null(ShipEntegraLabelResponseParser.TryExtractLabelFileUrl(Array.Empty<byte>()));
    }

    [Fact]
    public void IsPdf_PdfImzasi_Dogru()
    {
        Assert.True(ShipEntegraLabelResponseParser.IsPdf(Encoding.ASCII.GetBytes("%PDF-1.7 body")));
        Assert.False(ShipEntegraLabelResponseParser.IsPdf(Encoding.UTF8.GetBytes("{\"a\":1}")));
        Assert.False(ShipEntegraLabelResponseParser.IsPdf(null));
        Assert.False(ShipEntegraLabelResponseParser.IsPdf(Array.Empty<byte>()));
    }
}
