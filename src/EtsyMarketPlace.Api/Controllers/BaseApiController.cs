using Microsoft.AspNetCore.Mvc;

namespace EtsyMarketPlace.Api.Controllers;

[ApiController]
public abstract class BaseApiController : ControllerBase
{
    protected string ResolveShopId(string? queryShopId, IConfiguration config)
    {
        if (!string.IsNullOrWhiteSpace(queryShopId) && queryShopId != "523236321")
            return queryShopId.Trim();

        if (Request.Headers.TryGetValue("X-Etsy-Shop-Id", out var headerVal))
        {
            var hStr = headerVal.ToString().Trim();
            if (!string.IsNullOrWhiteSpace(hStr) && hStr != "523236321")
                return hStr;
        }

        return config["Etsy:DefaultShopId"] ?? config["Etsy:ShopId"] ?? "53236321";
    }
}
