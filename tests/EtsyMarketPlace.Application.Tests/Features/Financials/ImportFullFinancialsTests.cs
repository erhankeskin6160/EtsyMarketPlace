using System;
using System.Collections.Generic;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using EtsyMarketPlace.Application.Common.Models;
using EtsyMarketPlace.Application.EtsyIntegration;
using EtsyMarketPlace.Application.Features.Commands.Financials.ImportFullFinancials;
using EtsyMarketPlace.Application.Features.Queries.Financials.GetMonthlyOrderSummaries;
using EtsyMarketPlace.Domain.Common;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace EtsyMarketPlace.Application.Tests.Features.Financials;

public sealed class ImportFullFinancialsTests
{
    [Fact]
    public async Task ImportFullFinancialsHandler_SavesAllData_AndReturnsServiceResponse()
    {
        // Arrange
        var stubRepo = new FakeIntegrationRepository();
        var stubUow = new FakeUnitOfWork();
        var logger = NullLogger<ImportFullFinancialsHandler>.Instance;
        var handler = new ImportFullFinancialsHandler(stubRepo, stubUow, logger);

        var request = new ImportFullFinancialsRequest
        {
            ShopId = "53236321",
            Transactions =
            [
                new EtsyFinancialTransaction("53236321", "tx-1", DateTimeOffset.UtcNow, 100m, 10m, 5m, 0m, 20m, 10m, 0m, "USD")
            ],
            Payouts =
            [
                new EtsyBankPayout("53236321", "ref-1", DateTimeOffset.UtcNow, 50m, "USD", 48.5m, "PAID", "Transfer")
            ],
            MonthlyOrders =
            [
                new EtsyMonthlyOrderSummary("53236321", "2026-10", 15, 20, 1500m, 100m, 14, 1, "USD")
            ]
        };

        // Act
        var response = await handler.Handle(request, CancellationToken.None);

        // Assert
        Assert.True(response.Success);
        Assert.Equal((int)HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(response.Data);

        var data = Assert.IsType<ImportFullFinancialsResponse>(response.Data);
        Assert.Equal(1, data.TransactionsCount);
        Assert.Equal(1, data.PayoutsCount);
        Assert.Equal(1, data.MonthlyOrdersCount);
        Assert.True(stubUow.Saved);
    }

    [Fact]
    public void ImportFullFinancialsValidator_RejectsEmptyShopId()
    {
        // Arrange
        var validator = new ImportFullFinancialsValidator();
        var request = new ImportFullFinancialsRequest
        {
            ShopId = ""
        };

        // Act
        var result = validator.Validate(request);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == "ShopId");
    }

    [Fact]
    public async Task GetMonthlyOrderSummariesHandler_ReturnsSummariesFromRepository()
    {
        // Arrange
        var stubRepo = new FakeIntegrationRepository
        {
            Summaries =
            [
                new EtsyMonthlyOrderSummary("53236321", "2026-10", 10, 15, 1000m, 100m, 10, 0, "USD")
            ]
        };
        var handler = new GetMonthlyOrderSummariesHandler(stubRepo);
        var request = new GetMonthlyOrderSummariesRequest
        {
            ShopId = "53236321",
            Months = 12
        };

        // Act
        var response = await handler.Handle(request, CancellationToken.None);

        // Assert
        Assert.True(response.Success);
        var data = Assert.IsAssignableFrom<IReadOnlyList<EtsyMonthlyOrderSummary>>(response.Data);
        Assert.Single(data);
        Assert.Equal("2026-10", data[0].YearMonth);
    }

    private sealed class FakeUnitOfWork : IUnitOfWork
    {
        public bool Saved { get; private set; }
        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            Saved = true;
            return Task.FromResult(1);
        }
        public void Dispose() { }
    }

    private sealed class FakeIntegrationRepository : IEtsyIntegrationRepository
    {
        public List<EtsyMonthlyOrderSummary> Summaries { get; set; } = [];

        public Task<DateTimeOffset?> GetLastCursorAsync(string shopId, string dataType, CancellationToken cancellationToken = default) => Task.FromResult<DateTimeOffset?>(null);
        public Task SavePayoutsAsync(IReadOnlyCollection<EtsyBankPayout> payouts, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<IReadOnlyList<EtsyBankPayout>> GetBankPayoutsAsync(string shopId, DateTimeOffset startDate, DateTimeOffset endDate, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<EtsyBankPayout>>([]);
        public Task SaveTransactionsAsync(IReadOnlyCollection<EtsyFinancialTransaction> transactions, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task SaveOrderAlertsAsync(IReadOnlyCollection<EtsyOrderCostAlert> alerts, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task SaveSyncStateAsync(string shopId, string dataType, DateTimeOffset cursor, string? error = null, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task SaveMonthlyOrderSummariesAsync(IReadOnlyCollection<EtsyMonthlyOrderSummary> summaries, CancellationToken cancellationToken = default)
        {
            Summaries.AddRange(summaries);
            return Task.CompletedTask;
        }
        public Task<IReadOnlyList<EtsyMonthlyOrderSummary>> GetMonthlyOrderSummariesAsync(string shopId, int months = 12, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<EtsyMonthlyOrderSummary>>(Summaries);
        public Task SaveListingTrafficDailyAsync(IReadOnlyCollection<EtsyListingTrafficRecord> records, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<IReadOnlyList<EtsyListingTrafficRecord>> GetListingTrafficAnalyticsAsync(string shopId, string? snapshotDate = null, int limit = 50, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<EtsyListingTrafficRecord>>([]);
        public Task SaveChartSnapshotAsync(EtsyChartSnapshot snapshot, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<EtsyChartSnapshot?> GetChartSnapshotAsync(string shopId, string chartType, CancellationToken cancellationToken = default) => Task.FromResult<EtsyChartSnapshot?>(null);
        public Task<IReadOnlyDictionary<long, EtsyDashboardOrderCost>> GetOrderCostsAsync(string shopId, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyDictionary<long, EtsyDashboardOrderCost>>(new Dictionary<long, EtsyDashboardOrderCost>());
        public Task UpsertOrderCostAsync(string shopId, string orderId, decimal? productCost, decimal? shippingCost, decimal? packagingCost, string? notes, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
