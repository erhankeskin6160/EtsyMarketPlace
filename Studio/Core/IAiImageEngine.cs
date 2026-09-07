namespace SimilarProductsWinForms.Studio.Core;

using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// Common contract for all AI image generation and editing engines (Strategy Pattern).
/// </summary>
public interface IAiImageEngine
{
    string EngineId { get; }
    string DisplayName { get; }
    string Description { get; }
    EngineCapabilities Capabilities { get; }

    /// <summary>
    /// Checks whether the required API Key and settings are valid and configured.
    /// </summary>
    bool IsConfigured();

    /// <summary>
    /// Executes the image transformation or generation request.
    /// </summary>
    Task<ImageEngineResult> ProcessAsync(ImageEngineRequest request, CancellationToken cancellationToken = default);
}
