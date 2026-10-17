namespace EtsyMarketPlace.Api.Controllers;

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using EtsyMarketPlace.Application.Orders;
using EtsyMarketPlace.Domain.Orders;

[ApiController]
[Route("api/etsy/orders")]
public class EtsyOrdersController : BaseApiController
{
    private readonly IConfiguration _config;
    private readonly IEtsyOrderFulfillmentService _fulfillmentService;
    private readonly IAiAddressParserService _aiAddressParserService;
    private readonly IOrderAddressRepository? _addressRepository;

    public EtsyOrdersController(
        IConfiguration config,
        IEtsyOrderFulfillmentService fulfillmentService,
        IAiAddressParserService aiAddressParserService,
        IOrderAddressRepository? addressRepository = null)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _fulfillmentService = fulfillmentService ?? throw new ArgumentNullException(nameof(fulfillmentService));
        _aiAddressParserService = aiAddressParserService ?? throw new ArgumentNullException(nameof(aiAddressParserService));
        _addressRepository = addressRepository;
    }

    [HttpGet("fulfillment-queue")]
    [EndpointSummary("Etsy Mağazasının Canlı Sipariş Kuyruğunu ve Teslimat Adreslerini Getir")]
    public async Task<IActionResult> GetFulfillmentQueue(
        [FromQuery] string shopId = "53236321",
        [FromQuery] int lookbackDays = 45,
        [FromQuery] bool? wasShipped = null,
        CancellationToken cancellationToken = default)
    {
        var resolvedShopId = ResolveShopId(shopId, _config);
        var orders = await _fulfillmentService.GetFulfillmentQueueAsync(resolvedShopId, lookbackDays, wasShipped, cancellationToken);
        return Ok(new
        {
            count = orders.Count,
            results = orders
        });
    }

    [HttpPost("save-address")]
    [EndpointSummary("Siparişin Alıcı Teslimat Adresini Kalıcı Olarak Güncelle")]
    public async Task<IActionResult> SaveOrderAddress(
        [FromQuery] long receiptId,
        [FromBody] EtsyOrderFulfillmentItem updatedAddress,
        CancellationToken cancellationToken = default)
    {
        if (receiptId <= 0 || updatedAddress == null)
        {
            return BadRequest(new { message = "Geçersiz sipariş numarası veya adres bilgisi." });
        }

        await _fulfillmentService.SaveOrderAddressAsync(receiptId, updatedAddress, cancellationToken);
        return Ok(new { success = true, message = "Alıcı adresi ve sipariş bilgileri başarıyla kaydedildi." });
    }

    [HttpPost("save-fulfillment-meta")]
    [EndpointSummary("Siparişin Maliyet, Paket Ölçüsü, GTİP ve Seçili Taşıyıcı Bilgilerini Kalıcı Olarak Kaydet")]
    public async Task<IActionResult> SaveFulfillmentMeta(
        [FromQuery] long receiptId,
        [FromBody] EtsyOrderFulfillmentItem updatedMeta,
        CancellationToken cancellationToken = default)
    {
        long resolvedId = receiptId > 0 ? receiptId : (updatedMeta?.ReceiptId ?? 0);
        if (resolvedId <= 0 || updatedMeta == null)
        {
            return BadRequest(new { success = false, message = "Geçersiz sipariş numarası veya meta veri." });
        }

        await _fulfillmentService.SaveOrderAddressAsync(resolvedId, updatedMeta, cancellationToken);
        return Ok(new
        {
            success = true,
            receiptId = resolvedId,
            message = "Sipariş maliyetleri, paket ölçüleri ve GTİP kodu kalıcı olarak SQLite veritabanına kaydedildi."
        });
    }

    [HttpPost("ai-parse-address")]
    [EndpointSummary("Ham Adres Metnini Yapay Zeka ile Ayrıştırıp Form Alanlarına Dönüştür")]
    public async Task<IActionResult> AiParseAddress(
        [FromBody] RawAddressParseRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request?.RawAddress))
        {
            return BadRequest(new { message = "Lütfen ayrıştırılacak adres metnini girin." });
        }

        var result = await _aiAddressParserService.ParseRawAddressAsync(request.RawAddress, cancellationToken);
        return Ok(result);
    }

    [HttpPost("upload-csv")]
    [EndpointSummary("Etsy Sipariş CSV Dosyasını Yükleyerek Adresleri Toplu Olarak Güncelle")]
    public async Task<IActionResult> UploadOrdersCsv(
        Microsoft.AspNetCore.Http.IFormFile? file,
        [FromForm] string? csvText,
        CancellationToken cancellationToken = default)
    {
        string content = string.Empty;

        if (file != null && file.Length > 0)
        {
            using var reader = new System.IO.StreamReader(file.OpenReadStream(), System.Text.Encoding.UTF8);
            content = await reader.ReadToEndAsync(cancellationToken);
        }
        else if (!string.IsNullOrWhiteSpace(csvText))
        {
            content = csvText;
        }

        if (string.IsNullOrWhiteSpace(content))
        {
            return BadRequest(new { message = "Yüklenecek CSV içeriği bulunamadı." });
        }

        var records = EtsyOrderCsvParser.ParseCsv(content, source: "WebCsvUpload");
        if (records.Count == 0)
        {
            return BadRequest(new { message = "CSV dosyası içinde geçerli Etsy sipariş veya adres kaydı bulunamadı." });
        }

        if (_addressRepository != null)
        {
            await _addressRepository.SaveBatchAsync(records, cancellationToken);
        }

        return Ok(new
        {
            success = true,
            count = records.Count,
            message = $"{records.Count} sipariş teslimat adresi CSV'den başarıyla ayrıştırıldı ve kaydedildi."
        });
    }
}
