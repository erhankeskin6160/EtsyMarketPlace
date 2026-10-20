namespace EtsyMarketPlace.Api.Controllers;

using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using MediatR;
using EtsyMarketPlace.Application.Orders;
using EtsyMarketPlace.Domain.Orders;
using EtsyMarketPlace.Application.Features.Orders.Commands.AiParseAddress;
using EtsyMarketPlace.Application.Features.Orders.Commands.SaveFulfillmentMeta;
using EtsyMarketPlace.Application.Features.Orders.Commands.SaveOrderAddress;
using EtsyMarketPlace.Application.Features.Orders.Commands.UploadOrdersCsv;
using EtsyMarketPlace.Application.Features.Orders.Queries.GetFulfillmentQueue;

[ApiController]
[Route("api/etsy/orders")]
public class EtsyOrdersController : BaseApiController
{
    private readonly IConfiguration _config;
    private readonly IMediator _mediator;

    public EtsyOrdersController(
        IConfiguration config,
        IMediator mediator)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
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
        var request = new GetFulfillmentQueueRequest
        {
            ShopId = resolvedShopId,
            LookbackDays = lookbackDays,
            WasShipped = wasShipped
        };

        var response = await _mediator.Send(request, cancellationToken);
        return response.Success ? Ok(response.Data) : StatusCode(response.StatusCode, response);
    }

    [HttpPost("save-address")]
    [EndpointSummary("Siparişin Alıcı Teslimat Adresini Kalıcı Olarak Güncelle")]
    public async Task<IActionResult> SaveOrderAddress(
        [FromQuery] long receiptId,
        [FromBody] EtsyOrderFulfillmentItem updatedAddress,
        CancellationToken cancellationToken = default)
    {
        var request = new SaveOrderAddressRequest
        {
            ReceiptId = receiptId,
            UpdatedAddress = updatedAddress
        };

        var response = await _mediator.Send(request, cancellationToken);
        return response.Success
            ? Ok(new { success = true, message = response.Message, data = response.Data })
            : StatusCode(response.StatusCode, response);
    }

    [HttpPost("save-fulfillment-meta")]
    [EndpointSummary("Siparişin Maliyet, Paket Ölçüsü, GTİP ve Seçili Taşıyıcı Bilgilerini Kalıcı Olarak Kaydet")]
    public async Task<IActionResult> SaveFulfillmentMeta(
        [FromQuery] long receiptId,
        [FromBody] EtsyOrderFulfillmentItem updatedMeta,
        CancellationToken cancellationToken = default)
    {
        long resolvedId = receiptId > 0 ? receiptId : (updatedMeta?.ReceiptId ?? 0);
        var request = new SaveFulfillmentMetaRequest
        {
            ReceiptId = resolvedId,
            UpdatedMeta = updatedMeta
        };

        var response = await _mediator.Send(request, cancellationToken);
        return response.Success
            ? Ok(new
            {
                success = true,
                receiptId = resolvedId,
                message = response.Message
            })
            : StatusCode(response.StatusCode, response);
    }

    [HttpPost("ai-parse-address")]
    [EndpointSummary("Ham Adres Metnini Yapay Zeka ile Ayrıştırıp Form Alanlarına Dönüştür")]
    public async Task<IActionResult> AiParseAddress(
        [FromBody] RawAddressParseRequest request,
        CancellationToken cancellationToken = default)
    {
        var parseRequest = new AiParseAddressRequest
        {
            RawAddress = request?.RawAddress ?? string.Empty
        };

        var response = await _mediator.Send(parseRequest, cancellationToken);
        return response.Success ? Ok(response.Data) : StatusCode(response.StatusCode, response);
    }

    [HttpPost("upload-csv")]
    [EndpointSummary("Etsy Sipariş CSV Dosyasını Yükleyerek Adresleri Toplu Olarak Güncelle")]
    public async Task<IActionResult> UploadOrdersCsv(
        IFormFile? file,
        [FromForm] string? csvText,
        CancellationToken cancellationToken = default)
    {
        string content = string.Empty;

        if (file != null && file.Length > 0)
        {
            using var reader = new StreamReader(file.OpenReadStream(), Encoding.UTF8);
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

        var request = new UploadOrdersCsvRequest
        {
            Records = records
        };

        var response = await _mediator.Send(request, cancellationToken);
        return response.Success
            ? Ok(new
            {
                success = true,
                count = records.Count,
                message = response.Message
            })
            : StatusCode(response.StatusCode, response);
    }
}
