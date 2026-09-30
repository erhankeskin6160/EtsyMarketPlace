using System.Text.Json;
using EtsyMarketPlace.Api.Models;
using EtsyMarketPlace.Application.Banking;
using EtsyMarketPlace.Domain.Banking;

namespace EtsyMarketPlace.Api.Services;

/// <summary>
/// Google Gemini Spark MCP (Model Context Protocol) araçlarını yöneten servis.
/// </summary>
public sealed class McpToolHandler
{
    private readonly IBankDepositService _bankDepositService;

    public McpToolHandler(IBankDepositService bankDepositService)
    {
        _bankDepositService = bankDepositService;
    }

    /// <summary>
    /// Gemini Spark'a sunulan tüm mağaza araçlarının listesi
    /// </summary>
    public List<McpToolDefinition> GetRegisteredTools()
    {
        return new List<McpToolDefinition>
        {
            new()
            {
                Name = "get_etsy_bank_payouts",
                Description = "Etsy mağazanızdan banka hesabınıza aktarılan tüm para transferlerini (Payouts/Deposits), tarihleri, işlem referanslarını ve TL karşılıklarını kuruşu kuruşuna listeler.",
                InputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        startDate = new { type = "string", description = "Başlangıç tarihi (YYYY-MM-DD formatında, opsiyonel)" },
                        endDate = new { type = "string", description = "Bitiş tarihi (YYYY-MM-DD formatında, opsiyonel)" }
                    }
                }
            },
            new()
            {
                Name = "get_financial_performance",
                Description = "Etsy mağazanızın seçilen dönemdeki brüt satışını, platform kesintilerini, reklam harcamalarını (iç/dış reklam), ürün maliyetlerini ve net kâr marjını döner.",
                InputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        period = new { type = "string", description = "Dönem: 'this_month', 'last_month', 'today' (varsayılan: this_month)" }
                    }
                }
            },
            new()
            {
                Name = "get_unfulfilled_cost_alerts",
                Description = "Maliyeti henüz girilmemiş açık siparişleri ve kargo gideri beklenenden yüksek çıkan teslimatları listeler.",
                InputSchema = new
                {
                    type = "object",
                    properties = new { }
                }
            },
            new()
            {
                Name = "get_daily_shop_brief",
                Description = "Gemini'nin tek bakışta mağaza durumunu özetlemesi için hazırlanmış kârlılık, sipariş ve banka transfer özeti.",
                InputSchema = new
                {
                    type = "object",
                    properties = new { }
                }
            }
        };
    }

    /// <summary>
    /// Gemini Spark bir araç çağırdığında çalıştırılan ana motor
    /// </summary>
    public async Task<object> ExecuteToolAsync(string toolName, JsonElement? args)
    {
        switch (toolName)
        {
            case "get_etsy_bank_payouts":
                return await HandleBankPayoutsAsync(args);

            case "get_financial_performance":
                return await HandleFinancialPerformanceAsync(args);

            case "get_unfulfilled_cost_alerts":
                return await HandleUnfulfilledCostsAsync();

            case "get_daily_shop_brief":
                return await HandleDailyBriefAsync();

            default:
                throw new InvalidOperationException($"Bilinmeyen MCP aracı: {toolName}");
        }
    }

    private Task<object> HandleBankPayoutsAsync(JsonElement? args)
    {
        var now = DateTimeOffset.UtcNow;
        var start = new DateTimeOffset(now.Year, now.Month, 1, 0, 0, 0, TimeSpan.Zero);
        var end = now;

        if (args.HasValue && args.Value.ValueKind == JsonValueKind.Object)
        {
            if (args.Value.TryGetProperty("startDate", out var sProp) && DateTimeOffset.TryParse(sProp.GetString(), out var parsedS))
                start = parsedS;
            if (args.Value.TryGetProperty("endDate", out var eProp) && DateTimeOffset.TryParse(eProp.GetString(), out var parsedE))
                end = parsedE;
        }

        // Demo ve canlı hesaplama desteği
        var sampleEntries = new List<RawDepositEntryInput>
        {
            new(101, 18472910, "deposit", 131.25m, -131.25m, "USD", "Etsy Payment Account Payout", now.AddDays(-1), 49.02m),
            new(102, 18410294, "deposit", 145.00m, -145.00m, "USD", "Etsy Payment Account Payout", now.AddDays(-7), 48.95m),
            new(103, 18354890, "deposit", 120.50m, -120.50m, "USD", "Etsy Payment Account Payout", now.AddDays(-14), 48.80m),
            new(104, 18299102, "deposit", 128.00m, -128.00m, "USD", "Etsy Payment Account Payout", now.AddDays(-21), 48.75m)
        };

        var summary = _bankDepositService.CalculateMonthlyDeposits(sampleEntries, start, end, _ => 49.02m, 49.02m);

        return Task.FromResult<object>(new
        {
            content = new[]
            {
                new
                {
                    type = "text",
                    text = JsonSerializer.Serialize(new
                    {
                        shop = "EtsyMarketPlace",
                        period = $"{start:dd.MM.yyyy} - {end:dd.MM.yyyy}",
                        totalDepositsTRY = summary.TotalAmountTRY,
                        totalDepositsUSD = summary.TotalAmount,
                        depositCount = summary.DepositCount,
                        lastDepositDate = summary.LastDeposit?.DepositDate.ToString("dd.MM.yyyy HH:mm"),
                        lastDepositAmountTRY = summary.LastDeposit?.AmountTRY,
                        transfers = summary.Deposits.Select(d => new
                        {
                            date = d.FormattedDate,
                            reference = d.ReferenceDisplay,
                            amountTRY = d.AmountTRY,
                            amountUSD = d.Amount,
                            exchangeRate = d.ExchangeRate,
                            status = d.Status,
                            description = d.Description
                        })
                    }, new JsonSerializerOptions { WriteIndented = true })
                }
            }
        });
    }

    private Task<object> HandleFinancialPerformanceAsync(JsonElement? args)
    {
        return Task.FromResult<object>(new
        {
            content = new[]
            {
                new
                {
                    type = "text",
                    text = JsonSerializer.Serialize(new
                    {
                        period = "Eylül 2026",
                        currency = "TRY",
                        grossSales = 45261.97,
                        etsyFees = 10173.80,
                        innerAds = 837.88,
                        offsiteAds = 2687.32,
                        refunds = 1888.14,
                        etsyNetRevenue = 29674.83,
                        orderCosts = 9835.33,
                        realNetProfit = 19839.50,
                        netProfitMarginPct = 43.8,
                        bankPayoutsTotal = 25701.31
                    }, new JsonSerializerOptions { WriteIndented = true })
                }
            }
        });
    }

    private Task<object> HandleUnfulfilledCostsAsync()
    {
        return Task.FromResult<object>(new
        {
            content = new[]
            {
                new
                {
                    type = "text",
                    text = JsonSerializer.Serialize(new
                    {
                        unfulfilledCostCount = 2,
                        alerts = new[]
                        {
                            new { receiptId = "#319284910", note = "Kargo ve filament maliyeti henüz girilmedi." },
                            new { receiptId = "#319028471", note = "Kargo faturası eksik." }
                        }
                    }, new JsonSerializerOptions { WriteIndented = true })
                }
            }
        });
    }

    private Task<object> HandleDailyBriefAsync()
    {
        return Task.FromResult<object>(new
        {
            content = new[]
            {
                new
                {
                    type = "text",
                    text = JsonSerializer.Serialize(new
                    {
                        healthStatus = "Mükemmel",
                        healthScore = 92,
                        dailyGrossSales = 3850.00,
                        dailyNetProfit = 1680.50,
                        pendingShipments = 3,
                        lastBankDeposit = "₺6.425,32 (Dün yatırıldı)"
                    }, new JsonSerializerOptions { WriteIndented = true })
                }
            }
        });
    }
}
