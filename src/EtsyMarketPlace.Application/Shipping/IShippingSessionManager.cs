namespace EtsyMarketPlace.Application.Shipping;

using System;
using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// Kargo sağlayıcıları (Aras Global & ShipEntegra) için arka planda otomatik
/// oturum açma ve canlı Bearer token yakalama yöneticisi arayüzü.
/// </summary>
public interface IShippingSessionManager
{
    Task<string?> RefreshArasGlobalTokenAsync(
        string? email = null,
        string? password = null,
        bool showBrowser = false,
        Action<string>? statusCallback = null,
        CancellationToken ct = default);

    Task<string?> RefreshShipEntegraTokenAsync(
        string? email = null,
        string? password = null,
        bool showBrowser = false,
        Action<string>? statusCallback = null,
        CancellationToken ct = default);
}
