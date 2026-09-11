namespace EtsyMarketPlace.Application.ShopVault.Services;

public static class VaultPathHelper
{
    private static string? _customRootPath;

    public static string RootPath
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(_customRootPath))
            {
                return _customRootPath;
            }

            var folder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "SimilarProductsWinForms",
                "Vault");
            Directory.CreateDirectory(folder);
            return folder;
        }
    }

    public static void SetCustomRootPath(string path)
    {
        _customRootPath = path;
        Directory.CreateDirectory(path);
    }

    public static string DatabasePath => Path.Combine(RootPath, "vault.db");

    public static string ExportsFolder
    {
        get
        {
            var folder = Path.Combine(RootPath, "Exports");
            Directory.CreateDirectory(folder);
            return folder;
        }
    }

    public static string GetSessionFolder(string sessionId)
    {
        var folder = Path.Combine(RootPath, "Sessions", sessionId);
        Directory.CreateDirectory(folder);
        return folder;
    }

    public static string GetListingImagesFolder(string sessionId, long listingId)
    {
        var folder = Path.Combine(GetSessionFolder(sessionId), "Images", listingId.ToString());
        Directory.CreateDirectory(folder);
        return folder;
    }

    public static string GetRelativeImagePath(string sessionId, long listingId, string fileName)
    {
        return Path.Combine("Sessions", sessionId, "Images", listingId.ToString(), fileName);
    }

    public static string ResolveFullPath(string relativePath)
    {
        if (Path.IsPathRooted(relativePath))
        {
            return relativePath;
        }
        return Path.Combine(RootPath, relativePath);
    }
}
