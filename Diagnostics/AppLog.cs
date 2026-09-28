namespace SimilarProductsWinForms.Diagnostics;

using System;

/// <summary>
/// UI katmanı için ince yönlendirici: gerçek günlük Application katmanındadır, böylece
/// tüm katmanlar (UI, Infrastructure, Application) <em>tek</em> günlük dosyasına yazar ve
/// iki ayrı yazıcı birbirinin satırlarını bozmaz.
/// </summary>
public static class AppLog
{
    public static string FilePath => EtsyMarketPlace.Application.Diagnostics.AppLog.FilePath;

    public static void Swallowed(Exception? ex, string? context = null)
        => EtsyMarketPlace.Application.Diagnostics.AppLog.Swallowed(ex, context);

    public static void Info(string message, string? context = null)
        => EtsyMarketPlace.Application.Diagnostics.AppLog.Info(message, context);

    public static void Warn(string message, string? context = null)
        => EtsyMarketPlace.Application.Diagnostics.AppLog.Warn(message, context);
}
