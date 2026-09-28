namespace EtsyMarketPlace.Application.Tests;

using System.Collections.Generic;
using System.Linq;
using Xunit;
using EtsyMarketPlace.Application.Orders;
using EtsyMarketPlace.Application.Shipping;
using EtsyMarketPlace.Domain.Orders;

/// <summary>
/// Arayüzün içinde gömülü olan iş mantığının testleri.
/// Bu mantık Application katmanına çıkarıldı; arayüz yalnızca sonucu gösterir.
/// Amaç: kırılgan pencere otomasyonu olmadan kritik akışın kurallarını doğrulamak.
/// </summary>
public sealed class UiFlowLogicTests
{
    private static EtsyOrderFulfillmentItem Order(long id, string status)
        => new() { ReceiptId = id, Status = status, BuyerName = $"Alıcı {id}" };

    private static List<EtsyOrderFulfillmentItem> ThreeOrders() => new()
    {
        Order(1, "Shipped"),
        Order(2, "Unfulfilled"),
        Order(3, "Fulfilled")
    };

    // ---------------------------------------------------------------- sipariş kuyruğu

    [Theory]
    [InlineData("Shipped", true)]
    [InlineData("shipped", true)]
    [InlineData("Fulfilled", true)]
    [InlineData("Unfulfilled", false)]
    [InlineData("Paid", false)]
    [InlineData("", false)]
    public void IsShipped_TreatsShippedAndFulfilledEqually(string status, bool expected)
    {
        Assert.Equal(expected, OrderQueueFilter.IsShipped(Order(1, status)));
    }

    [Fact]
    public void IsShipped_NullOrderIsNotShipped()
    {
        Assert.False(OrderQueueFilter.IsShipped(null));
    }

    [Fact]
    public void ApplyStatus_AllReturnsEverything()
    {
        var result = OrderQueueFilter.ApplyStatus(ThreeOrders(), OrderQueueFilter.All);
        Assert.Equal(3, result.Count);
    }

    [Fact]
    public void ApplyStatus_PendingExcludesShippedAndFulfilled()
    {
        var result = OrderQueueFilter.ApplyStatus(ThreeOrders(), OrderQueueFilter.Pending);
        var single = Assert.Single(result);
        Assert.Equal(2, single.ReceiptId);
    }

    [Fact]
    public void ApplyStatus_ShippedReturnsBothShippedAndFulfilled()
    {
        var result = OrderQueueFilter.ApplyStatus(ThreeOrders(), OrderQueueFilter.Shipped);
        Assert.Equal(2, result.Count);
        Assert.All(result, o => Assert.True(OrderQueueFilter.IsShipped(o)));
    }

    [Fact]
    public void ApplyStatus_UnknownFilterBehavesLikeAll()
    {
        var result = OrderQueueFilter.ApplyStatus(ThreeOrders(), "anlamsız filtre");
        Assert.Equal(3, result.Count);
    }

    [Fact]
    public void ApplyStatus_NullInputIsEmptyNotThrow()
    {
        Assert.Empty(OrderQueueFilter.ApplyStatus(null, OrderQueueFilter.All));
    }

    [Fact]
    public void Counts_FeedTheFilterChipBadges()
    {
        var (all, pending, shipped) = OrderQueueFilter.Counts(ThreeOrders());

        Assert.Equal(3, all);
        Assert.Equal(1, pending);
        Assert.Equal(2, shipped);
    }

    // ---------------------------------------------------------------- eylem çubuğu

    [Fact]
    public void Action_WithoutQuote_IsDisabledWithGuidance()
    {
        var state = ShipmentActionEvaluator.Evaluate(false, false, null, QuoteSource.Estimated);

        Assert.False(state.CanCreate);
        Assert.Contains("Önce sipariş seçip", state.StatusMessage);
        Assert.Equal(ShipmentActionEvaluator.ToneNeutral, state.Tone);
    }

    [Fact]
    public void Action_UnsupportedCarrier_IsDisabledAndExplainsWhy()
    {
        var state = ShipmentActionEvaluator.Evaluate(true, false, "ShipEntegra", QuoteSource.Live);

        Assert.False(state.CanCreate);
        Assert.Equal("Gönderi kapalı", state.ButtonText);
        Assert.Contains("ShipEntegra", state.StatusMessage);
        Assert.Contains("karşılaştırma", state.StatusMessage);
        Assert.Equal(ShipmentActionEvaluator.ToneWarning, state.Tone);
    }

    [Fact]
    public void Action_LiveQuote_EnablesCreationAndSaysSo()
    {
        var state = ShipmentActionEvaluator.Evaluate(true, true, "Aras Global", QuoteSource.Live);

        Assert.True(state.CanCreate);
        Assert.Equal("Aras Global ile Gönderi Oluştur", state.ButtonText);
        Assert.Contains("Canlı teklif", state.StatusMessage);
        Assert.Equal(ShipmentActionEvaluator.ToneSuccess, state.Tone);
    }

    [Fact]
    public void Action_EstimatedQuote_WarnsAboutPriceConfirmation()
    {
        var state = ShipmentActionEvaluator.Evaluate(true, true, "Shipmore", QuoteSource.Estimated);

        Assert.True(state.CanCreate);
        Assert.Contains("Tahmini tarife", state.StatusMessage);
        Assert.Equal(ShipmentActionEvaluator.ToneWarning, state.Tone);
    }

    [Fact]
    public void Action_MissingProviderName_FallsBackGracefully()
    {
        var state = ShipmentActionEvaluator.Evaluate(true, true, "   ", QuoteSource.Live);

        Assert.True(state.CanCreate);
        Assert.Contains("Taşıyıcı", state.ButtonText);
    }
}
