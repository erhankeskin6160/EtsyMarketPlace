namespace EtsyMarketPlace.Application.Features.Orders.Commands.UploadOrdersCsv;

using System.Collections.Generic;
using EtsyMarketPlace.Application.Common.Models;
using EtsyMarketPlace.Domain.Orders;
using MediatR;

public class UploadOrdersCsvRequest : IRequest<ServiceResponse>
{
    public List<OrderAddressRecord> Records { get; set; } = [];
}
