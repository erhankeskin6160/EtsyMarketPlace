namespace EtsyMarketPlace.Application.Tests;

using System;
using System.IO;
using System.Threading.Tasks;
using EtsyMarketPlace.Domain.Taxonomy;
using EtsyMarketPlace.Infrastructure.Taxonomy;
using Microsoft.Data.Sqlite;
using Xunit;

public sealed class SqliteEtsyTaxonomyRepositoryTests
{
    [Fact]
    public async Task TaxonomyRepository_CanSaveQueryAndSearchNodes()
    {
        var databasePath = Path.Combine(Path.GetTempPath(), $"etsy-taxonomy-{Guid.NewGuid():N}.db");
        try
        {
            var repo = new SqliteEtsyTaxonomyRepository(databasePath);
            await repo.InitializeAsync();

            var initialCount = await repo.GetCountAsync();
            Assert.Equal(0, initialCount);

            var nodes = new[]
            {
                new EtsyTaxonomyNode(130, "Figurines", "Art & Collectibles > Sculpture > Figurines", 129, 3),
                new EtsyTaxonomyNode(69, "Figurines & Knick Knacks", "Art & Collectibles > Collectibles > Figurines & Knick Knacks", 68, 3),
                new EtsyTaxonomyNode(1238, "Solitaire Rings", "Jewelry > Rings > Solitaire Rings", 1237, 3),
                new EtsyTaxonomyNode(1041, "Lamps", "Home & Living > Lighting > Lamps", 1040, 3)
            };

            await repo.SaveAllAsync(nodes);

            var countAfterSave = await repo.GetCountAsync();
            Assert.Equal(4, countAfterSave);

            var node130 = await repo.GetByIdAsync(130);
            Assert.NotNull(node130);
            Assert.Equal("Figurines", node130.Name);
            Assert.Equal("Art & Collectibles > Sculpture > Figurines", node130.Path);

            var searchResults = await repo.SearchAsync("Figurine");
            Assert.Equal(2, searchResults.Count);
            Assert.Contains(searchResults, s => s.Id == 130);
            Assert.Contains(searchResults, s => s.Id == 69);

            var ringResults = await repo.SearchAsync("Rings");
            Assert.Single(ringResults);
            Assert.Equal(1238, ringResults[0].Id);
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (File.Exists(databasePath)) File.Delete(databasePath);
            if (File.Exists(databasePath + "-wal")) File.Delete(databasePath + "-wal");
            if (File.Exists(databasePath + "-shm")) File.Delete(databasePath + "-shm");
        }
    }
}
