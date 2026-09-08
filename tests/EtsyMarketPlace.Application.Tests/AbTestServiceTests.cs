namespace EtsyMarketPlace.Application.Tests;

using EtsyMarketPlace.Application.AbTesting;
using Xunit;

public sealed class AbTestServiceTests
{
    private readonly InMemoryAbTestRepository _repository = new();
    private readonly AbTestService _service;

    public AbTestServiceTests()
    {
        _service = new AbTestService(_repository);
    }

    [Fact]
    public async Task StartExperimentAsync_SavesExperimentWithActiveStatus()
    {
        var save = new SaveAbTestExperiment(
            "listing-123",
            "Dragon Bust",
            "SEO Title Test",
            "Original Title",
            "Optimized AI Title",
            ["tag1", "tag2"],
            ["tag1", "tag2", "tag3"],
            "Original Desc",
            "Optimized Desc",
            InitialViews: 50,
            InitialFavorites: 5,
            InitialSales: 1);

        var experiment = await _service.StartExperimentAsync(save);

        Assert.NotNull(experiment);
        Assert.True(experiment.Id > 0);
        Assert.Equal("SEO Title Test", experiment.ExperimentName);
        Assert.Equal(AbTestStatus.Active, experiment.Status);
        Assert.Equal(50, experiment.BeforeViews);
        Assert.Equal(5, experiment.BeforeFavorites);
        Assert.Equal(1, experiment.BeforeSales);
    }

    [Fact]
    public async Task StartExperimentAsync_ThrowsWhenTitleIsEmpty()
    {
        var save = new SaveAbTestExperiment(
            "listing-123",
            "",
            "SEO Test",
            "Title A",
            "Title B",
            [],
            [],
            "",
            "");

        await Assert.ThrowsAsync<ArgumentException>(() => _service.StartExperimentAsync(save));
    }

    [Fact]
    public void CalculateImpact_SalesIncreased_DeclaresVariantBAsWinner()
    {
        var experiment = new ListingAbTestExperiment(
            1,
            DateTimeOffset.Now,
            "123",
            "Dragon Bust",
            "Title Test",
            "Original Title",
            "AI Title",
            ["tag1"],
            ["tag1", "tag2"],
            "Desc A",
            "Desc B",
            DateTimeOffset.Now,
            null,
            AbTestStatus.Active,
            BeforeViews: 100,
            BeforeFavorites: 10,
            BeforeSales: 2,
            AfterViews: 180,
            AfterFavorites: 25,
            AfterSales: 8);

        var impact = _service.CalculateImpact(experiment);

        Assert.Contains("Varyant B", impact.WinnerVariant);
        Assert.True(impact.SalesChangePercent > 0);
        Assert.True(impact.ViewsChangePercent > 0);
        Assert.True(impact.FavoritesChangePercent > 0);
        Assert.NotEmpty(impact.KeyTakeaways);
    }

    [Fact]
    public void CalculateImpact_SalesDecreased_DeclaresVariantAAsWinner()
    {
        var experiment = new ListingAbTestExperiment(
            1,
            DateTimeOffset.Now,
            "123",
            "Dragon Bust",
            "Title Test",
            "Original Title",
            "AI Title",
            ["tag1"],
            ["tag1", "tag2"],
            "Desc A",
            "Desc B",
            DateTimeOffset.Now,
            null,
            AbTestStatus.Active,
            BeforeViews: 200,
            BeforeFavorites: 20,
            BeforeSales: 10,
            AfterViews: 100,
            AfterFavorites: 5,
            AfterSales: 2);

        var impact = _service.CalculateImpact(experiment);

        Assert.Contains("Varyant A", impact.WinnerVariant);
        Assert.True(impact.SalesChangePercent < 0);
    }

    [Fact]
    public void CalculateImpact_NoChange_DeclaresInconclusive()
    {
        var experiment = new ListingAbTestExperiment(
            1,
            DateTimeOffset.Now,
            "123",
            "Dragon Bust",
            "Title Test",
            "Original Title",
            "AI Title",
            ["tag1"],
            ["tag1", "tag2"],
            "Desc A",
            "Desc B",
            DateTimeOffset.Now,
            null,
            AbTestStatus.Active,
            BeforeViews: 100,
            BeforeFavorites: 10,
            BeforeSales: 2,
            AfterViews: 100,
            AfterFavorites: 10,
            AfterSales: 2);

        var impact = _service.CalculateImpact(experiment);

        Assert.Contains("Nötr", impact.WinnerVariant, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(0, impact.ViewsChangePercent);
        Assert.Equal(0, impact.FavoritesChangePercent);
        Assert.Equal(0, impact.SalesChangePercent);
    }

    [Fact]
    public async Task UpdateMetricsAsync_UpdatesExperimentAfterMetrics()
    {
        var save = new SaveAbTestExperiment(
            "listing-123",
            "Dragon Bust",
            "SEO Test",
            "Title A",
            "Title B",
            [],
            [],
            "",
            "",
            InitialViews: 10,
            InitialFavorites: 1,
            InitialSales: 0);

        var experiment = await _service.StartExperimentAsync(save);

        var updated = await _service.UpdateMetricsAsync(new UpdateAbTestMetrics(
            experiment.Id,
            AfterViews: 150,
            AfterFavorites: 15,
            AfterSales: 4,
            CompleteExperiment: true));

        Assert.NotNull(updated);
        Assert.Equal(150, updated.AfterViews);
        Assert.Equal(15, updated.AfterFavorites);
        Assert.Equal(4, updated.AfterSales);
        Assert.Equal(AbTestStatus.Completed, updated.Status);
    }

    [Fact]
    public async Task DeleteAsync_RemovesExperimentFromRepository()
    {
        var save = new SaveAbTestExperiment(
            "listing-999",
            "Listing To Delete",
            "Delete Me Test",
            "Title A",
            "Title B",
            ["tag1"],
            ["tag2"],
            "Desc A",
            "Desc B");

        var exp = await _service.StartExperimentAsync(save);
        Assert.NotNull(exp);

        var deleteResult = await _service.DeleteAsync(exp.Id);
        Assert.True(deleteResult);

        var retrieved = await _service.GetByIdAsync(exp.Id);
        Assert.Null(retrieved);
    }

    private sealed class InMemoryAbTestRepository : IAbTestRepository
    {
        private readonly List<ListingAbTestExperiment> _items = [];
        private long _nextId = 1;

        public Task InitializeAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task<ListingAbTestExperiment> SaveAsync(SaveAbTestExperiment experiment, CancellationToken cancellationToken = default)
        {
            var item = new ListingAbTestExperiment(
                _nextId++,
                DateTimeOffset.Now,
                experiment.ListingId,
                experiment.ListingTitle,
                experiment.ExperimentName,
                experiment.VariantA_Title,
                experiment.VariantB_Title,
                experiment.VariantA_Tags,
                experiment.VariantB_Tags,
                experiment.VariantA_Description,
                experiment.VariantB_Description,
                DateTimeOffset.Now,
                experiment.EndDate,
                AbTestStatus.Active,
                experiment.InitialViews,
                experiment.InitialFavorites,
                experiment.InitialSales,
                experiment.InitialViews,
                experiment.InitialFavorites,
                experiment.InitialSales);
            _items.Add(item);
            return Task.FromResult(item);
        }

        public Task<ListingAbTestExperiment?> GetByIdAsync(long id, CancellationToken cancellationToken = default) =>
            Task.FromResult(_items.FirstOrDefault(i => i.Id == id));

        public Task<IReadOnlyList<ListingAbTestExperiment>> GetRecentAsync(int limit = 50, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<ListingAbTestExperiment>>(_items.OrderByDescending(i => i.Id).Take(limit).ToList());

        public Task<IReadOnlyList<ListingAbTestExperiment>> GetByListingIdAsync(string listingId, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<ListingAbTestExperiment>>(_items.Where(i => i.ListingId == listingId).ToList());

        public Task<ListingAbTestExperiment?> UpdateMetricsAsync(UpdateAbTestMetrics update, CancellationToken cancellationToken = default)
        {
            var index = _items.FindIndex(i => i.Id == update.ExperimentId);
            if (index < 0) return Task.FromResult<ListingAbTestExperiment?>(null);

            var existing = _items[index];
            var updated = existing with
            {
                AfterViews = update.AfterViews,
                AfterFavorites = update.AfterFavorites,
                AfterSales = update.AfterSales,
                Status = update.CompleteExperiment ? AbTestStatus.Completed : existing.Status,
                EndDate = update.CompleteExperiment ? DateTimeOffset.Now : existing.EndDate,
            };
            _items[index] = updated;
            return Task.FromResult<ListingAbTestExperiment?>(updated);
        }

        public Task<ListingAbTestExperiment?> UpdateStatusAsync(long id, AbTestStatus status, CancellationToken cancellationToken = default)
        {
            var index = _items.FindIndex(i => i.Id == id);
            if (index < 0) return Task.FromResult<ListingAbTestExperiment?>(null);

            var existing = _items[index];
            var updated = existing with
            {
                Status = status,
                EndDate = status == AbTestStatus.Completed ? DateTimeOffset.Now : existing.EndDate,
            };
            _items[index] = updated;
            return Task.FromResult<ListingAbTestExperiment?>(updated);
        }

        public Task<bool> DeleteAsync(long id, CancellationToken cancellationToken = default)
        {
            var removed = _items.RemoveAll(i => i.Id == id);
            return Task.FromResult(removed > 0);
        }
    }
}
