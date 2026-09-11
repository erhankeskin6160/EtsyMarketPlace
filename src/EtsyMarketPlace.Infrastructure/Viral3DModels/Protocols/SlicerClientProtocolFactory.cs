namespace EtsyMarketPlace.Infrastructure.Viral3DModels.Protocols;

using System;
using System.Net.Http;
using System.Net.Http.Headers;
using EtsyMarketPlace.Domain.Viral3DModels.Enums;

public static class SlicerClientProtocolFactory
{
    private static readonly string[] BrowserAgents =
    [
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/128.0.0.0 Safari/537.36",
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/127.0.0.0 Safari/537.36 Edg/127.0.0.0",
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64; rv:129.0) Gecko/20100101 Firefox/129.0"
    ];

    public static void ApplySlicerHeaders(HttpClient client, ModelPlatformType platform)
    {
        client.DefaultRequestHeaders.Clear();
        client.Timeout = TimeSpan.FromSeconds(20);

        switch (platform)
        {
            case ModelPlatformType.MakerWorld:
                // Bambu Studio native slicer RPC & API protocol headers
                client.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", "BambuStudio/01.09.05.51 (Windows NT 10.0; Win64; x64)");
                client.DefaultRequestHeaders.TryAddWithoutValidation("Accept", "application/json, text/plain, */*");
                client.DefaultRequestHeaders.TryAddWithoutValidation("Accept-Language", "en-US,en;q=0.9");
                client.DefaultRequestHeaders.TryAddWithoutValidation("X-Bambu-Client", "studio");
                client.DefaultRequestHeaders.TryAddWithoutValidation("X-Bambu-Channel", "stable");
                client.DefaultRequestHeaders.TryAddWithoutValidation("X-Bambu-App-Version", "01.09.05.51");
                client.DefaultRequestHeaders.TryAddWithoutValidation("Sec-Fetch-Dest", "empty");
                client.DefaultRequestHeaders.TryAddWithoutValidation("Sec-Fetch-Mode", "cors");
                client.DefaultRequestHeaders.TryAddWithoutValidation("Sec-Fetch-Site", "same-site");
                break;

            case ModelPlatformType.CrealityCloud:
                // Creality Print desktop slicer headers
                client.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", "CrealityPrint/5.1.2 (Windows 10.0.19045; x64) DesktopClient");
                client.DefaultRequestHeaders.TryAddWithoutValidation("Accept", "application/json, text/plain, */*");
                client.DefaultRequestHeaders.TryAddWithoutValidation("X-Client-Type", "PC-Slicer");
                client.DefaultRequestHeaders.TryAddWithoutValidation("X-App-Version", "5.1.2");
                client.DefaultRequestHeaders.TryAddWithoutValidation("deviceType", "pc");
                break;

            case ModelPlatformType.Printables:
                // PrusaSlicer integrated model downloader
                client.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", "PrusaSlicer/2.8.0 (Windows NT 10.0; Win64; x64) PrusaLink/2.0");
                client.DefaultRequestHeaders.TryAddWithoutValidation("Accept", "application/json, text/plain, */*");
                break;

            case ModelPlatformType.MakerOnline:
                // Anycubic Photon / Kobra Slicer client
                client.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", "AnycubicSlicer/1.4.3 (Windows NT 10.0; Win64; x64)");
                client.DefaultRequestHeaders.TryAddWithoutValidation("Accept", "application/json, text/plain, */*");
                break;

            case ModelPlatformType.Thingiverse:
            default:
                string ua = BrowserAgents[Random.Shared.Next(BrowserAgents.Length)];
                client.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", ua);
                client.DefaultRequestHeaders.TryAddWithoutValidation("Accept", "application/json, text/html, */*");
                client.DefaultRequestHeaders.TryAddWithoutValidation("Accept-Language", "en-US,en;q=0.9,tr;q=0.8");
                break;
        }
    }

    public static string GetRandomBrowserUserAgent()
    {
        return BrowserAgents[Random.Shared.Next(BrowserAgents.Length)];
    }
}
