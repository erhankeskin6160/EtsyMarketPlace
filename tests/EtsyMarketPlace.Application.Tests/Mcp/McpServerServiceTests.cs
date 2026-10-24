namespace EtsyMarketPlace.Application.Tests.Mcp;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using EtsyMarketPlace.Application.Common.Models;
using EtsyMarketPlace.Application.EtsyIntegration;
using EtsyMarketPlace.Application.Features.Queries.Financials.GetFinancialPerformance;
using EtsyMarketPlace.Application.Mcp;
using Xunit;

public sealed class McpServerServiceTests
{
    [Fact]
    public void GetToolDefinitions_ReturnsAllTenLiveStoreAndOfficialMcpTools()
    {
        var sut = CreateSut();
        var tools = sut.GetToolDefinitions();

        Assert.Equal(10, tools.Count);
        Assert.Contains(tools, t => t.Name == "get_store_financial_summary");
        Assert.Contains(tools, t => t.Name == "get_store_orders");
        Assert.Contains(tools, t => t.Name == "get_bank_payouts");
        Assert.Contains(tools, t => t.Name == "save_order_cost");
        Assert.Contains(tools, t => t.Name == "get_shop_active_listings");
        Assert.Contains(tools, t => t.Name == "create_etsy_draft_listing");
        Assert.Contains(tools, t => t.Name == "update_etsy_listing");
        Assert.Contains(tools, t => t.Name == "search_market_intelligence");
        Assert.Contains(tools, t => t.Name == "calculate_shipping_rates");
        Assert.Contains(tools, t => t.Name == "query_etsy_official_dev_mcp");
    }

    [Fact]
    public async Task HandleJsonRpcAsync_InitializeAndToolsList_ReturnValidJsonRpc20Responses()
    {
        var sut = CreateSut();

        var initResp = await sut.HandleJsonRpcAsync(new McpJsonRpcRequest
        {
            Id = 1,
            Method = "initialize"
        }, "53236321");

        Assert.Null(initResp.Error);
        Assert.NotNull(initResp.Result);

        var listResp = await sut.HandleJsonRpcAsync(new McpJsonRpcRequest
        {
            Id = 2,
            Method = "tools/list"
        }, "53236321");

        Assert.Null(listResp.Error);
        Assert.NotNull(listResp.Result);
    }

    [Fact]
    public async Task ExecuteToolAsync_GetStoreFinancialSummary_DispatchesToMediatR()
    {
        var sut = CreateSut();
        using var doc = JsonDocument.Parse("{\"period\":\"this_month\"}");

        var result = await sut.ExecuteToolAsync(
            new McpToolCallRequest("get_store_financial_summary", doc.RootElement),
            "53236321");

        Assert.False(result.IsError);
        Assert.Equal("get_store_financial_summary", result.ToolName);
        Assert.Contains("742.61", result.Content.First().Text);
    }

    [Fact]
    public async Task ExecuteToolAsync_QueryOfficialEtsyMcp_CallsOfficialMcpClient()
    {
        var sut = CreateSut();
        using var doc = JsonDocument.Parse("{\"officialToolName\":\"search_etsy_api\",\"query\":\"ShopListing\"}");

        var result = await sut.ExecuteToolAsync(
            new McpToolCallRequest("query_etsy_official_dev_mcp", doc.RootElement),
            "53236321");

        Assert.False(result.IsError);
        Assert.False(result.IsError);
        Assert.Contains("ShopListing", result.Content.First().Text);
    }

    [Fact]
    public void BuildDetailedOrderRows_AppendsCrossPeriodRefundRow_WhenLedgerHasRefundForPriorMonthReceipt()
    {
        var octOrderDate = new DateTimeOffset(2026, 10, 16, 12, 0, 0, TimeSpan.Zero);
        var refundDate = new DateTimeOffset(2026, 10, 14, 15, 30, 0, TimeSpan.Zero);

        var receipts = new List<EtsyDashboardReceipt>
        {
            new(
                ReceiptId: 4073688891L,
                CreatedAt: octOrderDate,
                IsPaid: true,
                IsCanceled: false,
                IsFromOffsiteAds: false,
                CurrencyCode: "USD",
                GrandTotal: 78.66m,
                Subtotal: 78.66m,
                ShippingCost: 0m,
                DiscountAmt: 0m,
                TaxCost: 0m,
                RefundedAmount: 0m,
                Items: new[] { new EtsyDashboardReceiptItem("Ben 10 Classic Omnitrix", 1001L, 1) },
                BuyerName: "Ashley")
        };

        var ledgerFees = new List<EtsyDashboardLedgerFee>
        {
            new(
                Type: "refund",
                ReferenceId: 4011223344L,
                Description: "Refund for Order #4011223344",
                Amount: -102.97m,
                CreatedAt: refundDate)
        };

        var rows = EtsyLiveDashboardCalculator.BuildDetailedOrderRows(
            receipts,
            ledgerFees,
            new Dictionary<long, EtsyDashboardOrderCost>(),
            49.24m);

        Assert.Equal(2, rows.Count);
        var crossPeriodRow = rows.Single(r => r.ReceiptId == 4011223344L);
        Assert.Equal("refunded", crossPeriodRow.OrderStatus);
        Assert.Equal("cross_period_refund", crossPeriodRow.CostNotes);
        Assert.Equal(102.97m, crossPeriodRow.GrandTotalUsd);
        Assert.Equal(-102.97m, crossPeriodRow.NetProfitUsd);
    }

    private static McpServerService CreateSut()
    {
        return new McpServerService(
            new FakeMediator(),
            new FakeDashboardService(),
            null!,
            new FakeOfficialMcpClient());
    }

    private sealed class FakeMediator : IMediator
    {
        public Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default)
        {
            if (request is GetFinancialPerformanceRequest)
            {
                var resp = ServiceResponse.CreateSuccess("OK", new { grossSales = 742.61m, netProfit = 233.46m, receiptCount = 6 });
                return Task.FromResult((TResponse)(object)resp);
            }

            throw new NotSupportedException();
        }

        public Task Send<TRequest>(TRequest request, CancellationToken cancellationToken = default) where TRequest : IRequest => Task.CompletedTask;
        public Task<object?> Send(object request, CancellationToken cancellationToken = default) => Task.FromResult<object?>(null);
        public IAsyncEnumerable<TResponse> CreateStream<TResponse>(IStreamRequest<TResponse> request, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public IAsyncEnumerable<object?> CreateStream(object request, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task Publish(object notification, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task Publish<TNotification>(TNotification notification, CancellationToken cancellationToken = default) where TNotification : INotification => Task.CompletedTask;
    }

    private sealed class FakeDashboardService : IEtsyDashboardService
    {
        public Task<LiveFinancialPerformanceResult> GetFinancialPerformanceAsync(string shopId, string period, DateTimeOffset? startDate, DateTimeOffset? endDate, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<IReadOnlyList<EtsyBankPayout>> GetBankPayoutsAsync(string shopId, DateTimeOffset startDate, DateTimeOffset endDate, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<EtsyBankPayout>>(Array.Empty<EtsyBankPayout>());

        public Task<DailyShopBrief> GetDailyShopBriefAsync(string shopId, DateTimeOffset? date, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<RecentOrdersDashboardResult> GetRecentOrdersAsync(string shopId, int days, int limit, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<OrderFinancialsDashboardResult> GetOrderFinancialsAsync(string shopId, string period, DateTimeOffset? startDate, DateTimeOffset? endDate, CancellationToken cancellationToken = default)
            => Task.FromResult(new OrderFinancialsDashboardResult(Array.Empty<EtsyOrderFinancialDetail>(), 0, 0m, 0m, 49.30m, true, null));

        public Task<DailySeriesDashboardResult> GetDailySeriesAsync(string shopId, string? month, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<FinancialSummaryDashboardResult> GetRealFinancialSummaryAsync(string shopId, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<EtsyMarketPlace.Domain.Banking.MonthlyDepositSummary> GetRealBankDepositsAsync(string shopId, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();
    }

    private sealed class FakeOfficialMcpClient : IEtsyOfficialMcpClient
    {
        public Task<string> CallOfficialToolAsync(string toolName, JsonElement? arguments, CancellationToken cancellationToken = default)
        {
            return Task.FromResult($"{{\"tool\":\"{toolName}\",\"schema\":\"ShopListing\"}}");
        }
    }
}
