namespace EtsyMarketPlace.Application.Orders;

using System.Threading;
using System.Threading.Tasks;

public sealed record ParsedOrderAddressDto(
    string BuyerName,
    string BuyerEmail,
    string Phone,
    string StreetAddress,
    string SecondAddress,
    string City,
    string State,
    string PostalCode,
    string Country,
    string CountryCode,
    string IossNumber,
    string Source,
    string ConfidenceNote);

public sealed record RawAddressParseRequest(
    string RawAddress);

public interface IAiAddressParserService
{
    Task<ParsedOrderAddressDto> ParseRawAddressAsync(string rawAddressText, CancellationToken cancellationToken = default);
}
