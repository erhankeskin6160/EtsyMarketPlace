namespace EtsyMarketPlace.Application.Features.Orders.Commands.SaveFulfillmentMeta;

using EtsyMarketPlace.Application.Common.Models;
using EtsyMarketPlace.Domain.Orders;
using MediatR;

public class SaveFulfillmentMetaRequest : IRequest<ServiceResponse>
{
    public long ReceiptId { get; set; }
    public EtsyOrderFulfillmentItem? UpdatedMeta { get; set; }
}
