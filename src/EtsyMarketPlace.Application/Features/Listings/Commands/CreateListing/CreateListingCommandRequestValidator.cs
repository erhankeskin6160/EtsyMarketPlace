namespace EtsyMarketPlace.Application.Features.Listings.Commands.CreateListing;

using System;
using System.Threading;
using System.Threading.Tasks;
using EtsyMarketPlace.Application.Common.Localization;
using EtsyMarketPlace.Application.ListingOptimization;
using EtsyMarketPlace.Application.ListingOptimization.Validation;
using FluentValidation;

/// <summary>
/// MediatR pipeline üzerinden <see cref="CreateListingCommandRequest"/> doğrulamasını çalıştırır.
/// Temel alan kurallarına ek olarak fiziksel ürünler için Etsy kargo profili varlığını doğrular.
/// </summary>
public sealed class CreateListingCommandRequestValidator : AbstractValidator<CreateListingCommandRequest>
{
    public static string MissingShippingProfileMessage => ValidationMessageLocalizer.Pick(
        "Fiziksel ürünler için Etsy'de tanımlı bir Kargo Profili gereklidir. Lütfen Etsy Mağaza Yöneticisi alanından bir kargo profili oluşturun veya ilanı 'Dijital' olarak seçin.",
        "A shipping profile configured on Etsy is required for physical products. Please create a shipping profile in Etsy Shop Manager or mark the listing as 'Digital'.");

    public CreateListingCommandRequestValidator(
        IValidator<FastListingPublishCommand> commandValidator,
        IEtsyListingApiClient apiClient)
    {
        ArgumentNullException.ThrowIfNull(commandValidator);
        ArgumentNullException.ThrowIfNull(apiClient);

        RuleFor(x => x.Command)
            .NotNull()
            .WithMessage(_ => ValidationMessageLocalizer.Pick(
                "İlan oluşturma verileri boş olamaz.",
                "Listing creation data cannot be empty."));

        When(x => x.Command != null, () =>
        {
            RuleFor(x => x.Command).SetValidator(commandValidator);

            RuleFor(x => x.Command.ShippingProfileId)
                .MustAsync(async (req, shippingProfileId, cancellationToken) =>
                    await HasValidShippingProfileAsync(req.Command, shippingProfileId, apiClient, cancellationToken))
                .OverridePropertyName("ShippingProfileId")
                .WithMessage(_ => MissingShippingProfileMessage);

            When(x => !x.Command.IsDigital && x.Command.ReturnPolicyId.HasValue, () =>
            {
                RuleFor(x => x.Command.ReturnPolicyId!.Value)
                    .GreaterThan(0)
                    .OverridePropertyName("ReturnPolicyId")
                    .WithMessage(_ => ValidationMessageLocalizer.Pick(
                        "Geçersiz iade politikası ID'si (ReturnPolicyId 0'dan büyük olmalıdır).",
                        "Invalid return policy ID (ReturnPolicyId must be greater than 0)."));
            });
        });
    }

    private static async Task<bool> HasValidShippingProfileAsync(
        FastListingPublishCommand command,
        long? shippingProfileId,
        IEtsyListingApiClient apiClient,
        CancellationToken cancellationToken)
    {
        if (command.IsDigital)
            return true;

        if (shippingProfileId.HasValue && shippingProfileId.Value > 0)
            return true;

        if (string.IsNullOrWhiteSpace(command.ShopId))
            return true; // ShopId hatası FastListingPublishCommandValidator tarafından raporlanır.

        try
        {
            var defaultProfileId = await apiClient.GetDefaultShippingProfileIdAsync(command.ShopId.Trim(), cancellationToken);
            return defaultProfileId.HasValue && defaultProfileId.Value > 0;
        }
        catch
        {
            return false;
        }
    }
}
