namespace EtsyMarketPlace.Application.Shipping;

using System.Threading;
using System.Threading.Tasks;
using EtsyMarketPlace.Domain.Shipping;

/// <summary>
/// ShipEntegra gönderi oluşturma sağlayıcısı (API modeli keşfedildiğinde aktifleştirilecek).
/// </summary>
public sealed class ShipEntegraShipmentCreationProvider : IShipmentCreationProvider
{
    public string ProviderName => "ShipEntegra";
    public bool IsCreationSupported => false;

    public Task<ShipmentCreationResult> CreateShipmentAsync(ShipmentCreationContext context, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new ShipmentCreationResult
        {
            IsSuccess = false,
            ErrorMessage = "ShipEntegra kargo oluşturma API modeli keşfedildiğinde bu modül üzerinden otomatik gönderi oluşturulabilecektir. Şu an Aras Global tam aktiftir."
        });
    }
}

/// <summary>
/// Navlungo gönderi oluşturma sağlayıcısı (API modeli keşfedildiğinde aktifleştirilecek).
/// </summary>
public sealed class NavlungoShipmentCreationProvider : IShipmentCreationProvider
{
    public string ProviderName => "Navlungo";
    public bool IsCreationSupported => false;

    public Task<ShipmentCreationResult> CreateShipmentAsync(ShipmentCreationContext context, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new ShipmentCreationResult
        {
            IsSuccess = false,
            ErrorMessage = "Navlungo kargo oluşturma API modeli keşfedildiğinde bu modül üzerinden otomatik gönderi oluşturulabilecektir. Şu an Aras Global tam aktiftir."
        });
    }
}

/// <summary>
/// Shiptomore gönderi oluşturma sağlayıcısı (API modeli keşfedildiğinde aktifleştirilecek).
/// </summary>
public sealed class ShiptomoreShipmentCreationProvider : IShipmentCreationProvider
{
    public string ProviderName => "Shiptomore";
    public bool IsCreationSupported => false;

    public Task<ShipmentCreationResult> CreateShipmentAsync(ShipmentCreationContext context, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new ShipmentCreationResult
        {
            IsSuccess = false,
            ErrorMessage = "Shiptomore kargo oluşturma API modeli keşfedildiğinde bu modül üzerinden otomatik gönderi oluşturulabilecektir. Şu an Aras Global tam aktiftir."
        });
    }
}
