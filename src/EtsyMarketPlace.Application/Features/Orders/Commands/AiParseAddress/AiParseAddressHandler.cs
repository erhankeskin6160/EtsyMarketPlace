namespace EtsyMarketPlace.Application.Features.Orders.Commands.AiParseAddress;

using System.Net;
using System.Threading;
using System.Threading.Tasks;
using EtsyMarketPlace.Application.Common.Models;
using EtsyMarketPlace.Application.Orders;
using MediatR;

public class AiParseAddressHandler(
    IAiAddressParserService parserService
) : IRequestHandler<AiParseAddressRequest, ServiceResponse>
{
    public async Task<ServiceResponse> Handle(AiParseAddressRequest request, CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested)
            return ServiceResponse.CreateError(ResponseMessage.OperationCancelled, HttpStatusCode.RequestTimeout);

        var result = await parserService.ParseRawAddressAsync(request.RawAddress, cancellationToken);

        return ServiceResponse.CreateSuccess(ResponseMessage.Succeed, result);
    }
}
