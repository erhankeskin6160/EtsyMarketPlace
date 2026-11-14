namespace EtsyMarketPlace.Application.Features.Listings.Commands.CreateListing;

using System;
using System.Threading;
using System.Threading.Tasks;
using EtsyMarketPlace.Application.ListingOptimization;
using EtsyMarketPlace.Application.ListingOptimization.Validation;
using FluentValidation;

/// <summary>
/// MediatR pipeline üzerinden <see cref="CreateListingCommandRequest"/> doğrulamasını çalıştırır.
/// Temel alan kurallarına ek olarak fiziksel ürünler için Etsy kargo profili varlığını doğrular.
/// </summary>
public sealed class CreateListingCommandRequestValidator : AbstractValidator<CreateListingCommandRequest>
{
    public const string MissingShippingProfileMessage =
        "Fiziksel ürünler için Etsy'de tanımlı bir Kargo Profili gereklidir. Lütfen Etsy Mağaza Yöneticisi alanından bir kargo profili oluşturun veya ilanı 'Dijital' olarak seçin.";

    public CreateListingCommandRequestValidator(
        IValidator<FastListingPublishCommand> commandValidator,
        IEtsyListingApiClient apiClient)
    {
        ArgumentNullException.ThrowIfNull(commandValidator);
        ArgumentNullException.ThrowIfNull(apiClient);

        RuleFor(x => x.Command)
            .NotNull()
            .WithMessage("İlan oluşturma verileri boş olamaz.");

        When(x => x.Command != null, () =>
        {
            RuleFor(x => x.Command).SetValidator(commandValidator);

            RuleFor(x => x.Command.ShippingProfileId)
                .MustAsync(async (req, shippingProfileId, cancellationToken) =>
                    await HasValidShippingProfileAsync(req.Command, shippingProfileId, apiClient, cancellationToken))
                .OverridePropertyName("ShippingProfileId")
                .WithMessage(MissingShippingProfileMessage);

            When(x => !x.Command.IsDigital && x.Command.ReturnPolicyId.HasValue, () =>
            {
                RuleFor(x => x.Command.ReturnPolicyId!.Value)
                    .GreaterThan(0)
                    .OverridePropertyName("ReturnPolicyId")
                    .WithMessage("Geçersiz iade politikası ID'si (ReturnPolicyId 0'dan büyük olmalıdır).");
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
