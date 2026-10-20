namespace EtsyMarketPlace.Application.Tests.Features.Logistics;

using System;
using System.Collections.Generic;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using EtsyMarketPlace.Application.Common.Models;
using EtsyMarketPlace.Application.Features.Logistics.Commands.ConnectCarrier;
using EtsyMarketPlace.Application.Features.Logistics.Commands.CreateCarrierShipment;
using EtsyMarketPlace.Application.Features.Logistics.Queries.GetShippingProfiles;
using EtsyMarketPlace.Application.Shipping;
using Xunit;

public sealed class LogisticsHandlerTests
{
    [Fact]
    public void CreateCarrierShipmentValidator_RejectsEmptyCarrierKey()
    {
        var validator = new CreateCarrierShipmentValidator();
        var request = new CreateCarrierShipmentRequest
        {
            Command = new CreateCarrierShipmentCommand
            {
                ReceiptId = 12345,
                CarrierKey = ""
            }
        };

        var result = validator.Validate(request);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == "Command.CarrierKey");
    }

    [Fact]
    public void ConnectCarrierValidator_RejectsMissingCarrierKey()
    {
        var validator = new ConnectCarrierValidator();
        var request = new ConnectCarrierRequest
        {
            Command = new ConnectCarrierCommand
            {
                CarrierKey = ""
            }
        };

        var result = validator.Validate(request);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == "Command.CarrierKey");
    }

    [Fact]
    public async Task GetShippingProfilesHandler_ReturnsProfilesInServiceResponse()
    {
        var fakeLogistics = new FakeLogisticsService
        {
            Profiles =
            [
                new EtsyShippingProfileDto(1, "Standart Profil", 1, 3, "1-3 iş günü")
            ]
        };

        var handler = new GetShippingProfilesHandler(fakeLogistics);
        var request = new GetShippingProfilesRequest { ShopId = "53236321" };

        var response = await handler.Handle(request, CancellationToken.None);

        Assert.True(response.Success);
        Assert.Equal((int)HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(response.Data);
    }

    private sealed class FakeLogisticsService : IEtsyLogisticsService
    {
        public List<EtsyShippingProfileDto> Profiles { get; set; } = [];

        public Task<IReadOnlyList<EtsyShippingProfileDto>> GetShippingProfilesAsync(string shopId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<EtsyShippingProfileDto>>(Profiles);
        }

        public Task<IReadOnlyList<EtsyReadinessStateDto>> GetReadinessStatesAsync(string shopId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<EtsyReadinessStateDto>>([]);
        }

        public Task<IReadOnlyList<EtsyTaxonomyNodeItemDto>> GetSellerTaxonomyNodesAsync(string shopId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<EtsyTaxonomyNodeItemDto>>([]);
        }

        public Task<IReadOnlyList<EtsyTaxonomyNodeItemDto>> SearchSellerTaxonomyNodesAsync(string shopId, string query, int limit = 100, CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<EtsyTaxonomyNodeItemDto>>([]);
        }
    }
}
