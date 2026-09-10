namespace SimilarProductsWinForms.Studio.Core;

using System.Drawing;

/// <summary>
/// Strongly typed request passed to any IAiImageEngine.
/// </summary>
public sealed class ImageEngineRequest
{
    public Bitmap? InputImage { get; set; }
    public byte[]? MaskBytes { get; set; }
    public string EditMode { get; set; } = "generate"; // "generate", "edit", "bg_replace"
    public string QualityTier { get; set; } = "high"; // "low", "medium", "high", "xhigh", "max"
    public string Prompt { get; set; } = string.Empty;
    public string NegativePrompt { get; set; } = string.Empty;
    public string ModelName { get; set; } = string.Empty;
    public string AspectRatio { get; set; } = "1:1"; // "1:1", "4:3", "16:9"
    public string LightingPreset { get; set; } = string.Empty;
    public string CameraAnglePreset { get; set; } = string.Empty;
    public string ProcessMode { get; set; } = "ai_background"; // "remove_bg", "white_bg", "ai_background"
    public string ShadowMode { get; set; } = "ai_soft"; // "ai_soft", "ai_hard", "none"
    public double Padding { get; set; } = 0.10;
    public bool PreserveProduct { get; set; } = true;
    public bool TransparentBackground { get; set; } = false;
}
