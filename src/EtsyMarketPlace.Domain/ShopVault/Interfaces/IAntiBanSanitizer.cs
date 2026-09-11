namespace EtsyMarketPlace.Domain.ShopVault.Interfaces;

using EtsyMarketPlace.Domain.ShopVault.Entities;
using EtsyMarketPlace.Domain.ShopVault.ValueObjects;

public interface IAntiBanSanitizer
{
    Task<byte[]> SanitizeImageAsync(
        byte[] inputBytes,
        bool stripExif = true,
        bool permutateHash = true,
        CancellationToken cancellationToken = default);

    Task<string> RewriteTextWithAiAsync(
        string text,
        bool isTitle,
        string? apiKey,
        string provider = "OpenAI",
        CancellationToken cancellationToken = default);

    VaultListing SanitizeListingMetadata(
        VaultListing listing,
        AntiBanSettings settings);
}
