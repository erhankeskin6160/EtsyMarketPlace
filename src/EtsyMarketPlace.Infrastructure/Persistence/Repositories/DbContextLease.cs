using System;
using System.Threading;
using System.Threading.Tasks;
using EtsyMarketPlace.Infrastructure.Database.Postgres;
using Microsoft.EntityFrameworkCore;

namespace EtsyMarketPlace.Infrastructure.Persistence.Repositories;

/// <summary>
/// Eşzamanlı Task.WhenAll ve IHttpClientFactory çağrılarında EF Core DbContext çakışmalarını önlemek için
/// IDbContextFactory üzerinden izole bağlam açar; fabrika yoksa geri uyumluluk kilidiyle tekil bağlamı serileştirir.
/// </summary>
internal sealed class DbContextLeaseProvider(
    EtsyMarketPlaceDbContext? fallbackContext,
    IDbContextFactory<EtsyMarketPlaceDbContext>? contextFactory)
{
    private readonly SemaphoreSlim _fallbackLock = new(1, 1);

    public async ValueTask<Lease> AcquireAsync(CancellationToken cancellationToken = default)
    {
        if (contextFactory is not null)
        {
            var context = await contextFactory.CreateDbContextAsync(cancellationToken);
            return new Lease(context, ownsContext: true, fallbackLock: null);
        }

        if (fallbackContext is null)
            throw new InvalidOperationException("EtsyMarketPlaceDbContext veya IDbContextFactory bulunamadı.");

        await _fallbackLock.WaitAsync(cancellationToken);
        return new Lease(fallbackContext, ownsContext: false, fallbackLock: _fallbackLock);
    }

    internal readonly struct Lease(
        EtsyMarketPlaceDbContext context,
        bool ownsContext,
        SemaphoreSlim? fallbackLock) : IAsyncDisposable
    {
        public EtsyMarketPlaceDbContext Context { get; } = context;

        public async ValueTask DisposeAsync()
        {
            if (ownsContext)
                await Context.DisposeAsync();
            else
                fallbackLock?.Release();
        }
    }
}

