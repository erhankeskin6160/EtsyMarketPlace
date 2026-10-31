namespace EtsyMarketPlace.Application.Features.Orders.Commands.SyncExtensionOrders;

using System.Collections.Generic;
using EtsyMarketPlace.Application.Common.Models;
using MediatR;

public sealed record ExtensionOrderSyncItemDto(
    string ReceiptId,
    string BuyerName,
    string? ShippingAddress,
    string? StreetAddress,
    string? SecondAddress,
    string? City,
    string? State,
    string? PostalCode,
    string? Country,
    string? Phone,
    string? RawAddressText,
    bool? IsFullAddress,
    string? ProductTitle,
    decimal? OrderTotal,
    string? Currency,
    string? TrackingCode,
    string? TrackingNumber = null,
    string? IossNumber = null);

public sealed class SyncExtensionOrdersRequest : IRequest<ServiceResponse>
{
    public string ShopId { get; set; } = "53236321";
    public IReadOnlyList<ExtensionOrderSyncItemDto> Orders { get; set; } = new List<ExtensionOrderSyncItemDto>();
}
