using System;
using System.Threading;
using System.Threading.Tasks;
using EtsyMarketPlace.Domain.Common;
using EtsyMarketPlace.Infrastructure.Database.Postgres;

namespace EtsyMarketPlace.Infrastructure.Persistence;

/// <summary>
/// PostgreSQL EF Core Unit of Work somutlaştırması.
/// </summary>
public class UnitOfWork(EtsyMarketPlaceDbContext context) : IUnitOfWork
{
    private readonly EtsyMarketPlaceDbContext _context = context ?? throw new ArgumentNullException(nameof(context));

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) =>
        _context.SaveChangesAsync(cancellationToken);

    public void Dispose()
    {
        _context.Dispose();
        GC.SuppressFinalize(this);
    }
}
