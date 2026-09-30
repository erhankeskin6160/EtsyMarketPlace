using System.Text.Json;
using EtsyMarketPlace.Api.Models;
using EtsyMarketPlace.Api.Services;
using EtsyMarketPlace.Application.Banking;

var builder = WebApplication.CreateBuilder(args);

// 1. Dependency Injection (Clean Architecture Servisleri)
builder.Services.AddSingleton<IBankDepositService, BankDepositService>();
builder.Services.AddSingleton<McpToolHandler>();

// 2. CORS (Google Gemini Web ve Harici Entegrasyonlar İçin)
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

// 3. Swagger / OpenAPI
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() 
    { 
        Title = "EtsyMarketPlace Gemini Spark & MCP Web API", 
        Version = "v1",
        Description = "Google Gemini Spark (gemini.google.com/spark/apps) ve harici sistemler için Model Context Protocol (MCP) ve Finansal Raporlama API'si."
    });
});

var app = builder.Build();

app.UseCors();
app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "EtsyMarketPlace API v1");
    c.RoutePrefix = "swagger";
});

// ── 4. RESMİ GOOGLE GEMINI SPARK MCP PROTOCOL ENDPOINT'LERİ (/mcp) ────────────

// GET /mcp — Gemini Spark Handshake & Sunucu Bilgisi
app.MapGet("/mcp", (McpToolHandler handler) =>
{
    return Results.Ok(new
    {
        name = "EtsyMarketPlace-MCP-Server",
        version = "1.0.0",
        protocolVersion = "2024-11-05",
        status = "Active",
        supportedMethods = new[] { "initialize", "tools/list", "tools/call" },
        toolsCount = handler.GetRegisteredTools().Count
    });
});

// POST /mcp — Gemini Spark JSON-RPC 2.0 İletişim Hattı
app.MapPost("/mcp", async (HttpContext context, McpToolHandler handler) =>
{
    using var reader = new StreamReader(context.Request.Body);
    var body = await reader.ReadToEndAsync();

    if (string.IsNullOrWhiteSpace(body))
        return Results.BadRequest(McpResponse.Fail(null, -32700, "Geçersiz JSON"));

    McpRequest? request;
    try
    {
        request = JsonSerializer.Deserialize<McpRequest>(body);
    }
    catch (Exception ex)
    {
        return Results.BadRequest(McpResponse.Fail(null, -32700, $"Parse Hatası: {ex.Message}"));
    }

    if (request == null)
        return Results.BadRequest(McpResponse.Fail(null, -32600, "Geçersiz İstek"));

    switch (request.Method)
    {
        // 1. Gemini Bağlantı Kurma (Handshake)
        case "initialize":
            return Results.Ok(McpResponse.Success(request.Id, new
            {
                protocolVersion = "2024-11-05",
                capabilities = new
                {
                    tools = new { listChanged = false }
                },
                serverInfo = new
                {
                    name = "EtsyMarketPlace-SparkX-MCP",
                    version = "1.0.0"
                }
            }));

        // 2. Gemini Başlatma Bildirimi
        case "notifications/initialized":
            return Results.Ok();

        // 3. Gemini Araçları Listeleme (tools/list)
        case "tools/list":
            return Results.Ok(McpResponse.Success(request.Id, new
            {
                tools = handler.GetRegisteredTools()
            }));

        // 4. Gemini Araç Çalıştırma (tools/call)
        case "tools/call":
            if (!request.Params.HasValue)
                return Results.Ok(McpResponse.Fail(request.Id, -32602, "Eksik Parametre"));

            var p = request.Params.Value;
            string? toolName = p.TryGetProperty("name", out var tProp) ? tProp.GetString() : null;
            JsonElement? arguments = p.TryGetProperty("arguments", out var aProp) ? aProp : null;

            if (string.IsNullOrWhiteSpace(toolName))
                return Results.Ok(McpResponse.Fail(request.Id, -32602, "Araç adı ('name') belirtilmeli"));

            try
            {
                var toolResult = await handler.ExecuteToolAsync(toolName, arguments);
                return Results.Ok(McpResponse.Success(request.Id, toolResult));
            }
            catch (Exception ex)
            {
                return Results.Ok(McpResponse.Fail(request.Id, -32000, $"Araç çalıştırma hatası: {ex.Message}"));
            }

        default:
            return Results.Ok(McpResponse.Fail(request.Id, -32601, $"Bilinmeyen metot: {request.Method}"));
    }
});

// ── 5. STANDART REST API ENDPOINT'LERİ ──────────────────────────────────────────

app.MapGet("/health", () => Results.Ok(new { status = "Healthy", timestamp = DateTime.UtcNow }));

app.MapGet("/api/banking/deposits", (IBankDepositService bankService) =>
{
    var now = DateTimeOffset.UtcNow;
    var sampleEntries = new List<RawDepositEntryInput>
    {
        new(101, 18472910, "deposit", 131.25m, -131.25m, "USD", "Etsy Payment Account Payout", now.AddDays(-1), 49.02m),
        new(102, 18410294, "deposit", 145.00m, -145.00m, "USD", "Etsy Payment Account Payout", now.AddDays(-7), 48.95m),
        new(103, 18354890, "deposit", 120.50m, -120.50m, "USD", "Etsy Payment Account Payout", now.AddDays(-14), 48.80m),
        new(104, 18299102, "deposit", 128.00m, -128.00m, "USD", "Etsy Payment Account Payout", now.AddDays(-21), 48.75m)
    };
    var summary = bankService.CalculateMonthlyDeposits(sampleEntries, now.AddMonths(-1), now, _ => 49.02m, 49.02m);
    return Results.Ok(summary);
}).WithTags("Banking");

app.MapGet("/api/financial/summary", () => Results.Ok(new
{
    period = "Eylül 2026",
    grossSales = 45261.97,
    etsyFees = 10173.80,
    netRevenue = 29674.83,
    productCosts = 9835.33,
    realNetProfit = 19839.50,
    bankPayoutsTotal = 25701.31
})).WithTags("Financial");

app.Run();
