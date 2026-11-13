using System;
using System.Threading;
using System.Threading.Tasks;
using EtsyMarketPlace.Application.Features.Accounting;
using EtsyMarketPlace.Domain.Accounting;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;

namespace EtsyMarketPlace.Api.Controllers;

/// <summary>
/// Tekdüzen Hesap Planı (MSUGT 600–692) Resmi Gelir Tablosu ve yardımcı muhasebe kayıtları (Thin Controller).
/// Tüm iş kuralları MediatR Application işleyicileri ve Domain motorları tarafından yürütülür.
/// </summary>
[ApiController]
[Route("api/v{version:apiVersion}/etsy/accounting")]
[Route("api/etsy/accounting")]
public sealed class AccountingStatementsController(
    IMediator mediator,
    IConfiguration config) : BaseApiController
{
    [HttpGet("income-statement")]
    [EndpointSummary("Resmi MSUGT (600–692) Gelir Tablosu, Vergi Köprüsü ve Kapanış Fişlerini Getir")]
    public async Task<IActionResult> GetIncomeStatement(
        [FromQuery] string shopId = "53236321",
        [FromQuery] string period = "this_month",
        [FromQuery] DateTimeOffset? startDate = null,
        [FromQuery] DateTimeOffset? endDate = null,
        [FromQuery] bool? youngEntrepreneurMode = null,
        CancellationToken cancellationToken = default)
    {
        var resolvedShopId = ResolveShopId(shopId, config);
        var response = await mediator.Send(
            new GetIncomeStatementQuery(resolvedShopId, period, startDate, endDate, youngEntrepreneurMode),
            cancellationToken);
        return response.Success ? Ok(response.Data) : StatusCode(response.StatusCode, response);
    }

    [HttpGet("profile")]
    [EndpointSummary("Mağaza Muhasebe Politikası & Vergi Profili Getir")]
    public async Task<IActionResult> GetProfile(
        [FromQuery] string shopId = "53236321",
        CancellationToken cancellationToken = default)
    {
        var resolvedShopId = ResolveShopId(shopId, config);
        var response = await mediator.Send(new GetAccountingProfileQuery(resolvedShopId), cancellationToken);
        return response.Success ? Ok(response.Data) : StatusCode(response.StatusCode, response);
    }

    [HttpPut("profile")]
    [EndpointSummary("Mağaza Muhasebe Politikası & Genç Girişimci Profilini Kaydet")]
    public async Task<IActionResult> SaveProfile(
        [FromBody] SaveAccountingProfileRequest request,
        [FromQuery] string shopId = "53236321",
        CancellationToken cancellationToken = default)
    {
        var resolvedShopId = ResolveShopId(request.ShopId ?? shopId, config);
        var response = await mediator.Send(new UpsertAccountingProfileCommand(
            resolvedShopId,
            request.CommissionTarget,
            request.ShippingTarget,
            request.CanceledCostTarget,
            request.InventoryMethod,
            request.TaxRegime,
            request.YoungEntrepreneurModeEnabled,
            request.YoungEntrepreneurAnnualLimitTry,
            request.YoungEntrepreneurPriorUsedTry,
            request.EExportExemptionRatePct,
            request.CustomOrCorporateTaxRatePct,
            request.CustomGvkBracketsJson,
            request.IncludeFxSpreadAndExchangeDifference), cancellationToken);

        return response.Success ? Ok(response.Data) : StatusCode(response.StatusCode, response);
    }

    [HttpGet("expenses")]
    [EndpointSummary("Manuel İşletme Gideri / KKEG / Bağ-Kur Fişlerini Listele")]
    public async Task<IActionResult> GetExpenses(
        [FromQuery] string shopId = "53236321",
        CancellationToken cancellationToken = default)
    {
        var resolvedShopId = ResolveShopId(shopId, config);
        var response = await mediator.Send(new GetOperatingExpensesQuery(resolvedShopId), cancellationToken);
        return response.Success ? Ok(response.Data) : StatusCode(response.StatusCode, response);
    }

    [HttpPost("expenses")]
    [EndpointSummary("Yeni İşletme Gideri / KKEG Fişi Ekle")]
    public async Task<IActionResult> CreateExpense(
        [FromBody] SaveOperatingExpenseRequest request,
        [FromQuery] string shopId = "53236321",
        CancellationToken cancellationToken = default)
    {
        var resolvedShopId = ResolveShopId(request.ShopId ?? shopId, config);
        var response = await mediator.Send(new UpsertOperatingExpenseCommand(
            null,
            resolvedShopId,
            request.EntryDate,
            request.DocumentNo,
            request.Category,
            request.Description,
            request.Amount,
            request.Currency,
            request.ExchangeRateToTry,
            request.IsTaxDeductible,
            request.Frequency,
            request.RecurringEndDate), cancellationToken);

        return response.Success ? Ok(response.Data) : StatusCode(response.StatusCode, response);
    }

    [HttpPut("expenses/{id:long}")]
    [EndpointSummary("İşletme Gideri Fişini Güncelle")]
    public async Task<IActionResult> UpdateExpense(
        long id,
        [FromBody] SaveOperatingExpenseRequest request,
        [FromQuery] string shopId = "53236321",
        CancellationToken cancellationToken = default)
    {
        var resolvedShopId = ResolveShopId(request.ShopId ?? shopId, config);
        var response = await mediator.Send(new UpsertOperatingExpenseCommand(
            id,
            resolvedShopId,
            request.EntryDate,
            request.DocumentNo,
            request.Category,
            request.Description,
            request.Amount,
            request.Currency,
            request.ExchangeRateToTry,
            request.IsTaxDeductible,
            request.Frequency,
            request.RecurringEndDate), cancellationToken);

        return response.Success ? Ok(response.Data) : StatusCode(response.StatusCode, response);
    }

    [HttpDelete("expenses/{id:long}")]
    [EndpointSummary("İşletme Gideri Fişini Sil")]
    public async Task<IActionResult> DeleteExpense(
        long id,
        [FromQuery] string shopId = "53236321",
        CancellationToken cancellationToken = default)
    {
        var resolvedShopId = ResolveShopId(shopId, config);
        var response = await mediator.Send(new DeleteOperatingExpenseCommand(id, resolvedShopId), cancellationToken);
        return response.Success ? Ok(response.Data) : StatusCode(response.StatusCode, response);
    }

    [HttpGet("fixed-assets")]
    [EndpointSummary("253/255 Duran Varlık (3D Yazıcı / Demirbaş) Kartlarını Listele")]
    public async Task<IActionResult> GetFixedAssets(
        [FromQuery] string shopId = "53236321",
        CancellationToken cancellationToken = default)
    {
        var resolvedShopId = ResolveShopId(shopId, config);
        var response = await mediator.Send(new GetFixedAssetsQuery(resolvedShopId), cancellationToken);
        return response.Success ? Ok(response.Data) : StatusCode(response.StatusCode, response);
    }

    [HttpPost("fixed-assets")]
    [EndpointSummary("Yeni Duran Varlık (Amortisman) Kartı Ekle")]
    public async Task<IActionResult> CreateFixedAsset(
        [FromBody] SaveFixedAssetRequest request,
        [FromQuery] string shopId = "53236321",
        CancellationToken cancellationToken = default)
    {
        var resolvedShopId = ResolveShopId(request.ShopId ?? shopId, config);
        var response = await mediator.Send(new UpsertFixedAssetCommand(
            null,
            resolvedShopId,
            request.AssetName,
            request.AssetAccountCode,
            request.ExpenseTargetCategory,
            request.AcquisitionDate,
            request.AcquisitionCostTry,
            request.SalvageValueTry,
            request.UsefulLifeYears,
            request.Method,
            request.IsActive), cancellationToken);

        return response.Success ? Ok(response.Data) : StatusCode(response.StatusCode, response);
    }

    [HttpPut("fixed-assets/{id:long}")]
    [EndpointSummary("Duran Varlık Kartını Güncelle")]
    public async Task<IActionResult> UpdateFixedAsset(
        long id,
        [FromBody] SaveFixedAssetRequest request,
        [FromQuery] string shopId = "53236321",
        CancellationToken cancellationToken = default)
    {
        var resolvedShopId = ResolveShopId(request.ShopId ?? shopId, config);
        var response = await mediator.Send(new UpsertFixedAssetCommand(
            id,
            resolvedShopId,
            request.AssetName,
            request.AssetAccountCode,
            request.ExpenseTargetCategory,
            request.AcquisitionDate,
            request.AcquisitionCostTry,
            request.SalvageValueTry,
            request.UsefulLifeYears,
            request.Method,
            request.IsActive), cancellationToken);

        return response.Success ? Ok(response.Data) : StatusCode(response.StatusCode, response);
    }

    [HttpDelete("fixed-assets/{id:long}")]
    [EndpointSummary("Duran Varlık Kartını Sil")]
    public async Task<IActionResult> DeleteFixedAsset(
        long id,
        [FromQuery] string shopId = "53236321",
        CancellationToken cancellationToken = default)
    {
        var resolvedShopId = ResolveShopId(shopId, config);
        var response = await mediator.Send(new DeleteFixedAssetCommand(id, resolvedShopId), cancellationToken);
        return response.Success ? Ok(response.Data) : StatusCode(response.StatusCode, response);
    }

    [HttpPut("inventory-adjustment")]
    [EndpointSummary("Aralıklı Envanter Dönem Başı / Dönem Sonu Stok Sayımı Kaydet")]
    public async Task<IActionResult> SaveInventoryAdjustment(
        [FromBody] SaveInventoryAdjustmentRequest request,
        [FromQuery] string shopId = "53236321",
        CancellationToken cancellationToken = default)
    {
        var resolvedShopId = ResolveShopId(request.ShopId ?? shopId, config);
        var response = await mediator.Send(new UpsertInventoryAdjustmentCommand(
            resolvedShopId,
            request.Year,
            request.Month,
            request.OpeningInventoryTry,
            request.GrossPurchasesTry,
            request.PurchaseReturnsAndDiscountsTry,
            request.ClosingInventoryTry,
            request.Notes), cancellationToken);

        return response.Success ? Ok(response.Data) : StatusCode(response.StatusCode, response);
    }
}

public sealed record SaveAccountingProfileRequest(
    string? ShopId,
    CommissionAccountTarget CommissionTarget,
    ShippingPackagingCostTarget ShippingTarget,
    CanceledOrderCostTarget CanceledCostTarget,
    InventoryValuationMethod InventoryMethod,
    TaxRegimeType TaxRegime,
    bool YoungEntrepreneurModeEnabled,
    decimal YoungEntrepreneurAnnualLimitTry,
    decimal YoungEntrepreneurPriorUsedTry,
    decimal EExportExemptionRatePct,
    decimal CustomOrCorporateTaxRatePct,
    string? CustomGvkBracketsJson,
    bool IncludeFxSpreadAndExchangeDifference);

public sealed record SaveOperatingExpenseRequest(
    string? ShopId,
    DateTime EntryDate,
    string? DocumentNo,
    OperatingExpenseCategory Category,
    string Description,
    decimal Amount,
    string Currency,
    decimal ExchangeRateToTry,
    bool IsTaxDeductible,
    ExpenseFrequency Frequency,
    DateTime? RecurringEndDate);

public sealed record SaveFixedAssetRequest(
    string? ShopId,
    string AssetName,
    string AssetAccountCode,
    OperatingExpenseCategory ExpenseTargetCategory,
    DateTime AcquisitionDate,
    decimal AcquisitionCostTry,
    decimal SalvageValueTry,
    int UsefulLifeYears,
    DepreciationMethod Method,
    bool IsActive);

public sealed record SaveInventoryAdjustmentRequest(
    string? ShopId,
    int Year,
    int Month,
    decimal OpeningInventoryTry,
    decimal GrossPurchasesTry,
    decimal PurchaseReturnsAndDiscountsTry,
    decimal ClosingInventoryTry,
    string? Notes);

