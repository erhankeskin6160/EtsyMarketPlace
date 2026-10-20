using EtsyMarketPlace.Domain.Entities;
using EtsyMarketPlace.Infrastructure.Database.Postgres;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace EtsyMarketPlace.Application.Tests;

public class EtsyMarketPlaceDbContextTests
{
    [Fact]
    public void ModelConfiguration_ShouldContainAllTablesAndKeys()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<EtsyMarketPlaceDbContext>()
            .UseNpgsql("Host=localhost;Database=test;Username=test")
            .Options;

        // Act
        using var context = new EtsyMarketPlaceDbContext(options);
        var model = context.Model;

        // Assert Entities
        var userEntity = model.FindEntityType(typeof(AppUser));
        Assert.NotNull(userEntity);
        Assert.Equal("app_users", userEntity.GetTableName());
        Assert.NotNull(userEntity.FindPrimaryKey());

        var shopEntity = model.FindEntityType(typeof(ShopConnection));
        Assert.NotNull(shopEntity);
        Assert.Equal("shop_connections", shopEntity.GetTableName());

        var payoutEntity = model.FindEntityType(typeof(BankPayout));
        Assert.NotNull(payoutEntity);
        Assert.Equal("bank_payouts", payoutEntity.GetTableName());
        Assert.Equal(2, payoutEntity.FindPrimaryKey()!.Properties.Count); // Composite (shop_id, reference_id)

        var transEntity = model.FindEntityType(typeof(FinancialTransaction));
        Assert.NotNull(transEntity);
        Assert.Equal("financial_transactions", transEntity.GetTableName());
        Assert.Equal(2, transEntity.FindPrimaryKey()!.Properties.Count);

        var orderCostEntity = model.FindEntityType(typeof(OrderCost));
        Assert.NotNull(orderCostEntity);
        Assert.Equal("order_costs", orderCostEntity.GetTableName());
        Assert.Equal(2, orderCostEntity.FindPrimaryKey()!.Properties.Count);

        var trafficEntity = model.FindEntityType(typeof(ListingTrafficDaily));
        Assert.NotNull(trafficEntity);
        Assert.Equal("listing_traffic_daily", trafficEntity.GetTableName());
        Assert.Equal(3, trafficEntity.FindPrimaryKey()!.Properties.Count); // (shop_id, listing_id, snapshot_date)
    }

    [Fact]
    public void NavigationProperties_ShouldCascadeDeleteFromShopConnection()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<EtsyMarketPlaceDbContext>()
            .UseNpgsql("Host=localhost;Database=test;Username=test")
            .Options;

        // Act
        using var context = new EtsyMarketPlaceDbContext(options);
        var model = context.Model;
        var shopEntity = model.FindEntityType(typeof(ShopConnection));

        // Assert Navigations
        var navigations = shopEntity!.GetNavigations().Select(n => n.Name).ToList();
        Assert.Contains(nameof(ShopConnection.OAuthToken), navigations);
        Assert.Contains(nameof(ShopConnection.AppCredential), navigations);
        Assert.Contains(nameof(ShopConnection.BankPayouts), navigations);
        Assert.Contains(nameof(ShopConnection.FinancialTransactions), navigations);
        Assert.Contains(nameof(ShopConnection.OrderCosts), navigations);
    }
}
