using System;
using System.Threading;
using System.Threading.Tasks;

namespace EtsyMarketPlace.Domain.Common;

/// <summary>
/// Veritabanı transaction ve değişiklik commit sözleşmesi (Clean Architecture Unit of Work).
/// </summary>
public interface IUnitOfWork : IDisposable
{
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
