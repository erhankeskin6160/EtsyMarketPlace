namespace EtsyMarketPlace.Infrastructure.Persistence.Repositories;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using EtsyMarketPlace.Application.AbTesting;
using EtsyMarketPlace.Domain.Entities;
using EtsyMarketPlace.Infrastructure.Database.Postgres;

public sealed class EfPostgreSqlAbTestRepository : IAbTestRepository
{
    private readonly EtsyMarketPlaceDbContext _context;
    private readonly ILogger<EfPostgreSqlAbTestRepository> _logger;

    public EfPostgreSqlAbTestRepository(
        EtsyMarketPlaceDbContext context,
        ILogger<EfPostgreSqlAbTestRepository> logger)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public Task InitializeAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

    public async Task<ListingAbTestExperiment> SaveAsync(
        SaveAbTestExperiment experiment,
        CancellationToken cancellationToken = default)
    {
        var entity = new ListingAbTest
        {
            ListingId = experiment.ListingId ?? string.Empty,
            ListingTitle = experiment.ListingTitle ?? string.Empty,
            ExperimentName = experiment.ExperimentName ?? string.Empty,
            VariantATitle = experiment.VariantA_Title ?? string.Empty,
            VariantBTitle = experiment.VariantB_Title ?? string.Empty,
            VariantATags = JsonSerializer.Serialize(experiment.VariantA_Tags ?? []),
            VariantBTags = JsonSerializer.Serialize(experiment.VariantB_Tags ?? []),
            VariantADescription = experiment.VariantA_Description ?? string.Empty,
            VariantBDescription = experiment.VariantB_Description ?? string.Empty,
            StartDate = DateTime.UtcNow,
            EndDate = experiment.EndDate?.UtcDateTime,
            Status = AbTestStatus.Active.ToString(),
            BeforeViews = experiment.InitialViews,
            BeforeFavorites = experiment.InitialFavorites,
            BeforeSales = experiment.InitialSales,
            AfterViews = experiment.InitialViews,
            AfterFavorites = experiment.InitialFavorites,
            AfterSales = experiment.InitialSales
        };

        _context.ListingAbTests.Add(entity);
        await _context.SaveChangesAsync(cancellationToken);

        return ToModel(entity);
    }

    public async Task<ListingAbTestExperiment?> GetByIdAsync(
        long id,
        CancellationToken cancellationToken = default)
    {
        var entity = await _context.ListingAbTests
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        return entity is null ? null : ToModel(entity);
    }

    public async Task<IReadOnlyList<ListingAbTestExperiment>> GetRecentAsync(
        int limit = 50,
        CancellationToken cancellationToken = default)
    {
        var entities = await _context.ListingAbTests
            .AsNoTracking()
            .OrderByDescending(x => x.CreatedAt)
            .Take(limit)
            .ToListAsync(cancellationToken);

        return entities.Select(ToModel).ToList();
    }

    public async Task<IReadOnlyList<ListingAbTestExperiment>> GetByListingIdAsync(
        string listingId,
        CancellationToken cancellationToken = default)
    {
        var entities = await _context.ListingAbTests
            .AsNoTracking()
            .Where(x => x.ListingId == listingId)
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync(cancellationToken);

        return entities.Select(ToModel).ToList();
    }

    public async Task<ListingAbTestExperiment?> UpdateMetricsAsync(
        UpdateAbTestMetrics update,
        CancellationToken cancellationToken = default)
    {
        var entity = await _context.ListingAbTests
            .FirstOrDefaultAsync(x => x.Id == update.ExperimentId, cancellationToken);

        if (entity is null) return null;

        entity.AfterViews = update.AfterViews;
        entity.AfterFavorites = update.AfterFavorites;
        entity.AfterSales = update.AfterSales;

        if (update.CompleteExperiment)
        {
            entity.Status = AbTestStatus.Completed.ToString();
            entity.EndDate ??= DateTime.UtcNow;
        }

        await _context.SaveChangesAsync(cancellationToken);
        return ToModel(entity);
    }

    public async Task<ListingAbTestExperiment?> UpdateStatusAsync(
        long id,
        AbTestStatus status,
        CancellationToken cancellationToken = default)
    {
        var entity = await _context.ListingAbTests
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (entity is null) return null;

        entity.Status = status.ToString();
        if (status == AbTestStatus.Completed || status == AbTestStatus.Cancelled)
        {
            entity.EndDate ??= DateTime.UtcNow;
        }

        await _context.SaveChangesAsync(cancellationToken);
        return ToModel(entity);
    }

    public async Task<bool> DeleteAsync(
        long id,
        CancellationToken cancellationToken = default)
    {
        var entity = await _context.ListingAbTests
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (entity is null) return false;

        _context.ListingAbTests.Remove(entity);
        await _context.SaveChangesAsync(cancellationToken);
        return true;
    }

    private static ListingAbTestExperiment ToModel(ListingAbTest e)
    {
        var aTags = DeserializeList(e.VariantATags);
        var bTags = DeserializeList(e.VariantBTags);
        var status = Enum.TryParse<AbTestStatus>(e.Status, true, out var s) ? s : AbTestStatus.Active;

        return new ListingAbTestExperiment(
            e.Id,
            new DateTimeOffset(e.CreatedAt, TimeSpan.Zero),
            e.ListingId,
            e.ListingTitle,
            e.ExperimentName,
            e.VariantATitle,
            e.VariantBTitle,
            aTags,
            bTags,
            e.VariantADescription,
            e.VariantBDescription,
            new DateTimeOffset(e.StartDate, TimeSpan.Zero),
            e.EndDate.HasValue ? new DateTimeOffset(e.EndDate.Value, TimeSpan.Zero) : null,
            status,
            e.BeforeViews,
            e.BeforeFavorites,
            e.BeforeSales,
            e.AfterViews,
            e.AfterFavorites,
            e.AfterSales);
    }

    private static IReadOnlyList<string> DeserializeList(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return [];
        try
        {
            return JsonSerializer.Deserialize<List<string>>(json) ?? [];
        }
        catch
        {
            return [];
        }
    }
}
