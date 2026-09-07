namespace SimilarProductsWinForms.Services;

using System;
using System.Diagnostics;
using System.IO;

internal static class InvoiceStorageService
{
    private static readonly string InvoicesFolder = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "SimilarProductsWinForms",
        "Invoices");

    static InvoiceStorageService()
    {
        try
        {
            if (!Directory.Exists(InvoicesFolder))
            {
                Directory.CreateDirectory(InvoicesFolder);
            }
        }
        catch { /* ignored */ }
    }

    /// <summary>
    /// Seçilen fatura dosyasını (PDF/Resim) Invoices klasörüne güvenli ve benzersiz bir adla kopyalar.
    /// </summary>
    public static string? SaveInvoiceFile(string sourceFilePath, string identifier)
    {
        if (string.IsNullOrWhiteSpace(sourceFilePath) || !File.Exists(sourceFilePath))
            return null;

        try
        {
            Directory.CreateDirectory(InvoicesFolder);

            string ext = Path.GetExtension(sourceFilePath);
            string safeId = string.Join("_", identifier.Split(Path.GetInvalidFileNameChars()));
            string destFileName = $"fatura_{safeId}_{DateTime.Now:yyyyMMdd_HHmmss}{ext}";
            string destFilePath = Path.Combine(InvoicesFolder, destFileName);

            File.Copy(sourceFilePath, destFilePath, overwrite: true);
            return destFilePath;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[InvoiceStorageService] Dosya kopyalama hatası: {ex.Message}");
            return sourceFilePath; // Fallback: doğrudan seçilen yolu döndür
        }
    }

    /// <summary>
    /// Kayıtlı faturayı sistemin varsayılan PDF okuyucusu veya resim görüntüleyicisi ile açar.
    /// </summary>
    public static bool OpenInvoice(string? filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
            return false;

        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = filePath,
                UseShellExecute = true
            };
            Process.Start(psi);
            return true;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[InvoiceStorageService] Dosya açma hatası: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Fatura dosyasının adını kullanıcı dostu gösterim için döndürür.
    /// </summary>
    public static string GetDisplayFileName(string? filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
            return "Fatura yüklenmedi";

        if (!File.Exists(filePath))
            return Path.GetFileName(filePath) + " (Dosya bulunamadı)";

        var fi = new FileInfo(filePath);
        double sizeKb = fi.Length / 1024.0;
        return sizeKb >= 1024
            ? $"{fi.Name} ({(sizeKb / 1024.0):N1} MB)"
            : $"{fi.Name} ({sizeKb:N0} KB)";
    }
}
