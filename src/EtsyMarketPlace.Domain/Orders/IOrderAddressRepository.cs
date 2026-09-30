namespace EtsyMarketPlace.Domain.Orders;

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// Sipariş adreslerinin yerel veri tabanında kalıcı olarak saklanmasını ve
/// açılışta hızlı yüklenmesini sağlayan repository arayüzü (Clean Architecture - DIP).
/// </summary>
public interface IOrderAddressRepository
{
    Task<OrderAddressRecord?> GetByReceiptIdAsync(long receiptId, CancellationToken cancellationToken = default);
    Task<IReadOnlyDictionary<long, OrderAddressRecord>> GetAllAsync(CancellationToken cancellationToken = default);
    Task SaveAsync(OrderAddressRecord record, CancellationToken cancellationToken = default);
    Task SaveBatchAsync(IEnumerable<OrderAddressRecord> records, CancellationToken cancellationToken = default);
}
