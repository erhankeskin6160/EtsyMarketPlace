namespace EtsyMarketPlace.Application.Diagnostics;

using System;
using System.IO;
using System.Text;

/// <summary>
/// Uygulamanın merkezi, güvenli günlüğü.
///
/// Katman kararı: gerçek uygulama Application katmanındadır, çünkü Infrastructure ve UI
/// bu katmana referans verir; Domain hiçbir şeye referans vermediği için Domain'de günlük
/// kullanılmaz (Domain olabildiğince saf kalır).
///
/// Amaç: <c>catch { }</c> ile sessizce yutulan hataları görünür kılmak.
/// Kurallar:
/// - <strong>Asla istisna fırlatmaz</strong> (günlük yazarken hata olursa sessizce durur).
/// - Kendi içinde <c>AppLog</c> çağırmaz (sonsuz döngü olmaz).
/// - 2 MB'ı geçince dosya döndürülür (en fazla 2 dosya tutulur).
/// </summary>
public static class AppLog
{
    private const long MaxBytes = 2 * 1024 * 1024;
    private static readonly object Sync = new();
    private static string? _path;
    private static bool _failed;

    /// <summary>Günlük dosyasının tam yolu (kullanıcıya gösterilebilir).</summary>
    public static string FilePath => _path ??= BuildPath();

    /// <summary>
    /// Yutulan bir istisnayı kaydeder. <paramref name="context"/> çağıran üye adıdır;
    /// böylece "hangi işlem sessizce başarısız oldu" sorusu cevaplanır.
    /// </summary>
    public static void Swallowed(Exception? ex, string? context = null)
    {
        if (ex == null)
        {
            return;
        }

        Write("SWALLOWED", context, ex.GetType().Name + ": " + ex.Message);
    }

    /// <summary>Bilgilendirici kayıt (kritik akışlar için).</summary>
    public static void Info(string message, string? context = null) => Write("INFO", context, message);

    /// <summary>Uyarı kaydı (yutulmayan ama dikkat gerektiren durumlar).</summary>
    public static void Warn(string message, string? context = null) => Write("WARN", context, message);

    private static void Write(string level, string? context, string message)
    {
        if (_failed)
        {
            return;
        }

        try
        {
            lock (Sync)
            {
                string path = FilePath;
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                RotateIfNeeded(path);

                string line = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] [{level}] [{context ?? "-"}] {message}{Environment.NewLine}";
                File.AppendAllText(path, line, Encoding.UTF8);
            }
        }
        catch
        {
            // Günlük yazılamıyorsa uygulamayı etkilemeyiz; tekrar denemeyi bırakırız.
            _failed = true;
        }
    }

    private static void RotateIfNeeded(string path)
    {
        var info = new FileInfo(path);
        if (!info.Exists || info.Length < MaxBytes)
        {
            return;
        }

        string old = path + ".1";
        try
        {
            if (File.Exists(old))
            {
                File.Delete(old);
            }

            File.Move(path, old);
        }
        catch
        {
            // Döndürme başarısızsa mevcut dosyaya eklemeye devam edilir.
        }
    }

    private static string BuildPath()
    {
        string folder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "SimilarProductsWinForms");
        return Path.Combine(folder, "application.log");
    }
}
