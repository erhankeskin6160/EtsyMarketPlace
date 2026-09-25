namespace EtsyMarketPlace.Application.Shipping;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EtsyMarketPlace.Domain.Shipping;

/// <summary>
/// Çoklu kargo firmalarının gönderi oluşturma sağlayıcılarını yöneten merkezi servis.
/// </summary>
public sealed class ShipmentCreationManager
{
    private readonly Dictionary<string, IShipmentCreationProvider> _providers = new(StringComparer.OrdinalIgnoreCase);

    public ShipmentCreationManager(IEnumerable<IShipmentCreationProvider>? providers = null)
    {
        if (providers != null)
        {
            foreach (var p in providers)
            {
                RegisterProvider(p);
            }
        }
    }

    public void RegisterProvider(IShipmentCreationProvider provider)
    {
        if (provider != null)
        {
            _providers[provider.ProviderName] = provider;
        }
    }

    public IReadOnlyCollection<IShipmentCreationProvider> GetAvailableProviders() => _providers.Values.ToList();

    public IShipmentCreationProvider? GetProvider(string providerName)
    {
        if (string.IsNullOrWhiteSpace(providerName)) return null;
        _providers.TryGetValue(providerName.Trim(), out var p);
        return p;
    }

    public async Task<ShipmentCreationResult> CreateShipmentAsync(
        string providerName,
        ShipmentCreationContext context,
        CancellationToken cancellationToken = default)
    {
        var provider = GetProvider(providerName);
        if (provider == null)
        {
            return new ShipmentCreationResult
            {
                IsSuccess = false,
                ErrorMessage = $"'{providerName}' için henüz gönderi oluşturma sağlayıcısı kayıtlı değil."
            };
        }

        if (!provider.IsCreationSupported)
        {
            return new ShipmentCreationResult
            {
                IsSuccess = false,
                ErrorMessage = $"'{providerName}' API gönderi oluşturma modeli henüz geliştirme aşamasındadır. İlk fazda Aras Global aktiftir."
            };
        }

        return await provider.CreateShipmentAsync(context, cancellationToken);
    }
}
