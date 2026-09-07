namespace SimilarProductsWinForms.Studio.Services;

using System;
using System.Collections.Generic;
using System.Linq;
using SimilarProductsWinForms.Studio.Core;
using SimilarProductsWinForms.Studio.Engines;

public static class AiImageEngineRegistry
{
    private static readonly Dictionary<string, IAiImageEngine> Engines = new(StringComparer.OrdinalIgnoreCase);

    static AiImageEngineRegistry()
    {
        Register(new PhotoRoomEngine());
        Register(new GoogleGeminiEngine());
        Register(new OpenAiImageEngine());
        Register(new FluxImageEngine());
        Register(new IdeogramImageEngine());
    }

    public static void Register(IAiImageEngine engine)
    {
        Engines[engine.EngineId] = engine;
    }

    public static IAiImageEngine? GetEngine(string engineId)
    {
        return Engines.TryGetValue(engineId, out var engine) ? engine : null;
    }

    public static IReadOnlyList<IAiImageEngine> GetAllEngines()
    {
        return Engines.Values.ToList();
    }

    public static (bool IsConfigured, string KeyName) GetConfigurationState(string engineId)
    {
        var cfg = StudioConfigurationManager.Current;
        return engineId.ToLowerInvariant() switch
        {
            "photoroom" => (!string.IsNullOrWhiteSpace(cfg.PhotoRoomApiKey), "PhotoRoom API Key"),
            "gemini" => (!string.IsNullOrWhiteSpace(cfg.GoogleGeminiApiKey), "Gemini API Key"),
            "openai" => (!string.IsNullOrWhiteSpace(cfg.OpenAiApiKey), "OpenAI API Key"),
            "flux" => (!string.IsNullOrWhiteSpace(cfg.BflApiKey), "BFL (FLUX) API Key"),
            "ideogram" => (!string.IsNullOrWhiteSpace(cfg.IdeogramApiKey), "Ideogram API Key"),
            _ => (true, "API Key")
        };
    }
}
