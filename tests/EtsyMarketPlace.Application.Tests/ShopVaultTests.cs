namespace EtsyMarketPlace.Application.Tests;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using EtsyMarketPlace.Application.ShopVault.Services;
using EtsyMarketPlace.Domain.ShopVault.Entities;
using EtsyMarketPlace.Domain.ShopVault.ValueObjects;
using EtsyMarketPlace.Infrastructure.ShopVault.ImageSanitization;
using EtsyMarketPlace.Infrastructure.ShopVault.Repositories;
using SkiaSharp;
using Xunit;

public sealed class ShopVaultTests : IDisposable
{
    private readonly string _tempDirectory;
    private readonly string _testDbPath;

    public ShopVaultTests()
    {
        _tempDirectory = Path.Combine(Path.GetTempPath(), "EtsyVault_Test_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDirectory);
        _testDbPath = Path.Combine(_tempDirectory, "test_vault.db");
        VaultPathHelper.SetCustomRootPath(_tempDirectory);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_tempDirectory))
            {
                Directory.Delete(_tempDirectory, true);
            }
        }
        catch { }
    }

    [Fact]
    public async Task SqliteRepository_SessionAndListings_CrudLifecycle_Succeeds()
    {
        // Arrange
        var repo = new SqliteShopVaultRepository(_testDbPath);
        var session = new VaultBackupSession
        {
            SessionId = "sess_test_123",
            ShopId = 987654,
            ShopName = "TestWoodCrafts",
            ShopUrl = "https://etsy.com/shop/TestWoodCrafts",
            TotalListingsCount = 1,
            TotalImagesCount = 2,
            TotalSizeBytes = 3072,
            Status = "Completed"
        };

        var listing = new VaultListing
        {
            ListingId = 1001,
            SessionId = session.SessionId,
            OriginalShopId = session.ShopId,
            OriginalShopName = session.ShopName,
            Title = "Handmade Wooden Chess Set",
            Description = "Premium walnut and maple chess board with weighted pieces.",
            Price = 149.99m,
            Currency = "USD",
            Quantity = 5,
            Tags = ["chess", "wooden", "handmade", "walnut"],
            Materials = ["walnut", "maple"],
            Images =
            [
                new VaultListingImage { ListingId = 1001, OriginalImageId = 101, Rank = 1, OriginalUrl = "https://example.com/chess1.jpg", LocalRelativePath = "images/1001_1.jpg", FileSizeBytes = 1024 },
                new VaultListingImage { ListingId = 1001, OriginalImageId = 102, Rank = 2, OriginalUrl = "https://example.com/chess2.jpg", LocalRelativePath = "images/1001_2.jpg", FileSizeBytes = 2048 }
            ],
            Variations =
            [
                new VaultListingVariation { ListingId = 1001, PropertyId = 501, PropertyName = "Board Size", ValueId = 1, ValueName = "Large 16 inch", PriceDifference = 0, StockQuantity = 3, IsAvailable = true }
            ]
        };

        // Act - Save session & listings
        await repo.SaveSessionAsync(session);
        await repo.SaveListingsAsync([listing]);

        // Assert - Read back
        var sessions = await repo.GetAllSessionsAsync();
        Assert.Single(sessions);
        Assert.Equal("TestWoodCrafts", sessions[0].ShopName);

        var retrievedListings = await repo.GetListingsBySessionIdAsync(session.SessionId);
        Assert.Single(retrievedListings);
        var savedListing = retrievedListings[0];
        Assert.Equal(1001, savedListing.ListingId);
        Assert.Equal("Handmade Wooden Chess Set", savedListing.Title);
        Assert.Equal(149.99m, savedListing.Price);
        Assert.Equal(4, savedListing.Tags.Count);
        Assert.Equal(2, savedListing.Images.Count);
        Assert.Single(savedListing.Variations);
        Assert.Equal("Large 16 inch", savedListing.Variations[0].ValueName);

        // Delete session
        await repo.DeleteSessionAsync(session.SessionId);
        var afterDeleteSessions = await repo.GetAllSessionsAsync();
        Assert.Empty(afterDeleteSessions);

        var afterDeleteListings = await repo.GetListingsBySessionIdAsync(session.SessionId);
        Assert.Empty(afterDeleteListings);
    }

    [Fact]
    public void ExifStripper_ValidImage_StripsAndMicroCropsSuccessfully()
    {
        // Arrange - Create a test 100x100 PNG image
        using var bitmap = new SKBitmap(100, 100);
        using (var canvas = new SKCanvas(bitmap))
        {
            canvas.Clear(SKColors.Blue);
            using var paint = new SKPaint { Color = SKColors.Red, StrokeWidth = 3 };
            canvas.DrawLine(0, 0, 100, 100, paint);
        }
        using var img = SKImage.FromBitmap(bitmap);
        using var originData = img.Encode(SKEncodedImageFormat.Png, 100);
        byte[] inputBytes = originData.ToArray();

        var stripper = new ExifStripperImageProcessor();

        // Act
        byte[] outputBytes = stripper.ProcessImage(inputBytes, stripExif: true, permutateHash: true);

        // Assert
        Assert.NotNull(outputBytes);
        Assert.NotEmpty(outputBytes);
        Assert.True(outputBytes.Length > 0);

        // Decode the sanitized output to verify dimension micro-crop (100x100 cropped to 98x98)
        using var resultBitmap = SKBitmap.Decode(outputBytes);
        Assert.NotNull(resultBitmap);
        Assert.Equal(98, resultBitmap.Width);
        Assert.Equal(98, resultBitmap.Height);
    }

    [Fact]
    public void AntiBanSanitizer_ListingMetadata_AppliesPrefixPriceAndTags()
    {
        // Arrange
        var stripper = new ExifStripperImageProcessor();
        var sanitizer = new AntiBanSanitizerService(stripper);

        var original = new VaultListing
        {
            ListingId = 500,
            Title = "Vintage Leather Journal",
            Description = "Genuine antique leather notebook diary.",
            Price = 50.00m,
            Tags = ["tag1", "tag2", "tag3", "tag4", "tag5"],
            Variations =
            [
                new VaultListingVariation { ListingId = 500, PropertyId = 10, PropertyName = "Color", ValueId = 1, ValueName = "Brown", Sku = "JRNL-01" }
            ]
        };

        var settings = new AntiBanSettings
        {
            SkuPrefix = "MIG_",
            PriceAdjustmentPercent = 10.0m, // +10% -> 55.00
            CreateAsDraftFirst = true
        };

        // Act
        var sanitized = sanitizer.SanitizeListingMetadata(original, settings);

        // Assert
        Assert.Equal("draft", sanitized.State);
        Assert.Equal(55.00m, sanitized.Price);
        Assert.Equal(5, sanitized.Tags.Count);
        Assert.Single(sanitized.Variations);
        Assert.Equal("MIG_JRNL-01", sanitized.Variations[0].Sku);
    }

    [Fact]
    public async Task ShopVaultPackager_ExportAndImport_ArchiveRoundtrip_Succeeds()
    {
        // Arrange
        var repo = new SqliteShopVaultRepository(_testDbPath);
        var packager = new ShopVaultPackagerService(repo);

        string sessionId = "sess_export_pack";
        var session = new VaultBackupSession
        {
            SessionId = sessionId,
            ShopId = 777,
            ShopName = "ArchiveCrafts",
            TotalListingsCount = 1,
            TotalImagesCount = 1
        };

        // Create a dummy image file inside session folder
        string localImgRel = VaultPathHelper.GetRelativeImagePath(sessionId, 999, "test_pack.jpg");
        string localImgFull = VaultPathHelper.ResolveFullPath(localImgRel);
        Directory.CreateDirectory(Path.GetDirectoryName(localImgFull)!);
        await File.WriteAllBytesAsync(localImgFull, [1, 2, 3, 4, 5]);

        var listing = new VaultListing
        {
            ListingId = 999,
            SessionId = sessionId,
            OriginalShopId = 777,
            Title = "Archive Package Test Item",
            Price = 25.00m,
            Images =
            [
                new VaultListingImage { OriginalImageId = 1, ListingId = 999, Rank = 1, LocalRelativePath = localImgRel, FileSizeBytes = 5 }
            ]
        };

        await repo.SaveSessionAsync(session);
        await repo.SaveListingsAsync([listing]);

        string zipExportPath = Path.Combine(_tempDirectory, "export_test.etsyvault");

        // Act 1: Export
        var exportedPath = await packager.ExportSessionToArchiveAsync(sessionId, zipExportPath);
        Assert.True(File.Exists(exportedPath));
        Assert.True(new FileInfo(exportedPath).Length > 0);

        // Act 2: Import into a new session
        var importedSession = await packager.ImportArchiveAsync(exportedPath);

        // Assert
        Assert.NotNull(importedSession);
        Assert.Equal("ArchiveCrafts", importedSession.ShopName);

        var importedListings = await repo.GetListingsBySessionIdAsync(importedSession.SessionId);
        Assert.Single(importedListings);
        Assert.Equal("Archive Package Test Item", importedListings[0].Title);
        Assert.Single(importedListings[0].Images);

        string importedImgPath = VaultPathHelper.ResolveFullPath(importedListings[0].Images[0].LocalRelativePath);
        Assert.True(File.Exists(importedImgPath));
    }
}
