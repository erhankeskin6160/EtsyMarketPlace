namespace EtsyMarketPlace.Application.Features.Orders.Queries.GetReceiptDetail;

using EtsyMarketPlace.Application.Common.Models;
using MediatR;

public class GetReceiptDetailRequest : IRequest<ServiceResponse>
{
    public string ShopId { get; set; } = "53236321";
    public long ReceiptId { get; set; }
}
