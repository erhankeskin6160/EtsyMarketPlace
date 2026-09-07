namespace SimilarProductsWinForms.Studio.Core;

using System;

[Flags]
public enum EngineCapabilities
{
    None = 0,
    TextToImage = 1 << 0,
    BackgroundRemoval = 1 << 1,
    VisualGrounding = 1 << 2, // Recontextualize loaded product image
    ShadowGeneration = 1 << 3,
    Inpainting = 1 << 4,
    MultipleAspectRatios = 1 << 5,
    TransparentBackground = 1 << 6
}
