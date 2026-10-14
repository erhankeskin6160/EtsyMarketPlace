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

    public EtsyOrdersController(
        IConfiguration config,
        IEtsyOrderFulfillmentService fulfillmentService)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _fulfillmentService = fulfillmentService ?? throw new ArgumentNullException(nameof(fulfillmentService));
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
        return Ok(new { success = true, message = "Alıcı adresi başarıyla kaydedildi." });
    }
}
