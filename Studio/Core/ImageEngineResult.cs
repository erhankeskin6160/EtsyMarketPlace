namespace SimilarProductsWinForms.Studio.Core;

using System.Drawing;

/// <summary>
/// Strongly typed result returned by any IAiImageEngine.
/// </summary>
public sealed class ImageEngineResult
{
    public bool Success { get; set; }
    public Bitmap? ResultImage { get; set; }
    public string EngineName { get; set; } = string.Empty;
    public string ModelUsed { get; set; } = string.Empty;
    public long ElapsedMilliseconds { get; set; }
    public string ErrorMessage { get; set; } = string.Empty;

    public static ImageEngineResult Fail(string error, string engineName = "") =>
        new() { Success = false, ErrorMessage = error, EngineName = engineName };

    public static ImageEngineResult Ok(Bitmap image, string engineName, string modelUsed, long elapsedMs) =>
        new()
        {
            Success = true,
            ResultImage = image,
            EngineName = engineName,
            ModelUsed = modelUsed,
            ElapsedMilliseconds = elapsedMs
        };
}
