namespace SimilarProductsWinForms.Controls;

using System;
using System.Collections.Concurrent;
using System.Drawing;
using System.IO;
using SkiaSharp;

/// <summary>
/// Yapay zeka butonları için FontAwesome ve resmi markaların orijinal SVG vektörlerini
/// saf monokrom (renk paletsiz, dairesiz, saf FontAwesome glifi olarak) çizen vektör motoru.
/// </summary>
internal static class AiProviderIconHelper
{
    private static readonly ConcurrentDictionary<string, Bitmap> _cache = new(StringComparer.OrdinalIgnoreCase);

    // =========================================================================
    // RESMİ VEKTÖR SVG PATH VERİLERİ (FontAwesome & Simple-Icons Resmi Standartları)
    // =========================================================================

    // OpenAI / ChatGPT: Resmi Simple-Icons / FontAwesome SVG path
    private const string SvgOpenAi =
        "M22.282 9.821a6 6 0 0 0-.516-4.91a6.05 6.05 0 0 0-6.51-2.9A6.065 6.065 0 0 0 4.981 4.18a6 6 0 0 0-3.998 2.9a6.05 6.05 0 0 0 .743 7.097a5.98 5.98 0 0 0 .51 4.911a6.05 6.05 0 0 0 6.515 2.9A6 6 0 0 0 13.26 24a6.06 6.06 0 0 0 5.772-4.206a6 6 0 0 0 3.997-2.9a6.06 6.06 0 0 0-.747-7.073zm-9.022 12.608a4.476 4.476 0 0 1-2.876-1.041l.142-.08l4.778-2.758a.795.795 0 0 0 .393-.681v-6.737l2.02 1.169a.071.071 0 0 1 .038.052v5.583a4.504 4.504 0 0 1-4.495 4.494zm-9.661-4.125a4.471 4.471 0 0 1-.535-3.014l.142.085l4.783 2.758a.771.771 0 0 0 .78 0l5.843-3.368v2.332a.08.08 0 0 1-.033.062L9.74 19.95a4.5 4.5 0 0 1-6.141-1.646zM2.341 7.896a4.485 4.485 0 0 1 2.365-1.973V11.6a.766.766 0 0 0 .388.677l5.814 3.354l-2.02 1.169a.076.076 0 0 1-.071 0l-4.83-2.787A4.504 4.504 0 0 1 2.341 7.872zm16.596 3.856L13.104 8.364l2.015-1.164a.076.076 0 0 1 .071 0l4.83 2.791a4.494 4.494 0 0 1-.676 8.104v-5.677a.79.79 0 0 0-.407-.667zm2.01-3.023l-.142-.085l-4.774-2.782a.776.776 0 0 0-.785 0L9.409 9.23V6.897a.066.066 0 0 1 .028-.061l4.83-2.787a4.499 4.499 0 0 1 6.68 4.66zM8.307 12.863l-2.02-1.164a.08.08 0 0 1-.038-.057V6.074a4.499 4.499 0 0 1 7.376-3.454l-.142.08L8.704 5.459a.795.795 0 0 0-.393.681zm1.097-2.365l2.602-1.5l2.607 1.5v3l-2.598 1.5l-2.607-1.5z";

    // Anthropic Claude: Resmi Claude 14 ışınlı starburst SVG path
    private const string SvgClaude =
        "m4.7144 15.9555 4.7174-2.6471.079-.2307-.079-.1275h-.2307l-.7893-.0486-2.6956-.0729-2.3375-.0971-2.2646-.1214-.5707-.1215-.5343-.7042.0546-.3522.4797-.3218.686.0608 1.5179.1032 2.2767.1578 1.6514.0972 2.4468.255h.3886l.0546-.1579-.1336-.0971-.1032-.0972L6.973 9.8356l-2.55-1.6879-1.3356-.9714-.7225-.4918-.3643-.4614-.1578-1.0078.6557-.7225.8803.0607.2246.0607.8925.686 1.9064 1.4754 2.4893 1.8336.3643.3035.1457-.1032.0182-.0728-.164-.2733-1.3539-2.4467-1.445-2.4893-.6435-1.032-.17-.6194c-.0607-.255-.1032-.4674-.1032-.7285L6.287.1335 6.6997 0l.9957.1336.419.3642.6192 1.4147 1.0018 2.2282 1.5543 3.0296.4553.8985.2429.8318.091.255h.1579v-.1457l.1275-1.706.2368-2.0947.2307-2.6957.0789-.7589.3764-.9107.7468-.4918.5828.2793.4797.686-.0668.4433-.2853 1.8517-.5586 2.9021-.3643 1.9429h.2125l.2429-.2429.9835-1.3053 1.6514-2.0643.7286-.8196.85-.9046.5464-.4311h1.0321l.759 1.1293-.34 1.1657-1.0625 1.3478-.8804 1.1414-1.2628 1.7-.7893 1.36.0729.1093.1882-.0183 2.8535-.607 1.5421-.2794 1.8396-.3157.8318.3886.091.3946-.3278.8075-1.967.4857-2.3072.4614-3.4364.8136-.0425.0304.0486.0607 1.5482.1457.6618.0364h1.621l3.0175.2247.7892.522.4736.6376-.079.4857-1.2142.6193-1.6393-.3886-3.825-.9107-1.3113-.3279h-.1822v.1093l1.0929 1.0686 2.0035 1.8092 2.5075 2.3314.1275.5768-.3218.4554-.34-.0486-2.2039-1.6575-.85-.7468-1.9246-1.621h-.1275v.17l.4432.6496 2.3436 3.5214.1214 1.0807-.17.3521-.6071.2125-.6679-.1214-1.3721-1.9246L14.38 17.959l-1.1414-1.9428-.1397.079-.674 7.2552-.3156.3703-.7286.2793-.6071-.4614-.3218-.7468.3218-1.4753.3886-1.9246.3157-1.53.2853-1.9004.17-.6314-.0121-.0425-.1397.0182-1.4328 1.9672-2.1796 2.9446-1.7243 1.8456-.4128.164-.7164-.3704.0667-.6618.4008-.5889 2.386-3.0357 1.4389-1.882.929-1.0868-.0062-.1579h-.0546l-6.3385 4.1164-1.1293.1457-.4857-.4554.0608-.7467.2307-.2429 1.9064-1.3114Z";

    // Google Gemini: Resmi 4 köşeli astro-elmas / sparkle SVG path
    private const string SvgGemini =
        "M141.201 4.886c2.282-6.17 11.042-6.071 13.184.148l5.985 17.37a184.004 184.004 0 0 0 111.257 113.049l19.304 6.997c6.143 2.227 6.156 10.91.02 13.155l-19.35 7.082a184.001 184.001 0 0 0-109.495 109.385l-7.573 20.629c-2.241 6.105-10.869 6.121-13.133.025l-7.908-21.296a184 184 0 0 0-109.02-108.658l-19.698-7.239c-6.102-2.243-6.118-10.867-.025-13.132l20.083-7.467A183.998 183.998 0 0 0 133.291 26.28l7.91-21.394Z";

    // DeepSeek: Resmi Simple-Icons balina amblemi SVG path
    private const string SvgDeepSeek =
        "M23.748 4.651c-.254-.124-.364.113-.512.233-.051.04-.094.09-.137.137-.372.397-.806.657-1.373.626-.829-.046-1.537.214-2.163.848-.133-.782-.575-1.248-1.247-1.548-.352-.155-.708-.311-.955-.65-.172-.24-.219-.509-.305-.774-.055-.16-.11-.323-.293-.35-.2-.031-.278.136-.356.276-.313.572-.434 1.202-.422 1.84.027 1.436.633 2.58 1.838 3.393.137.094.172.187.129.323-.082.28-.18.553-.266.833-.055.179-.137.218-.328.14a5.5 5.5 0 0 1-1.737-1.179c-.857-.828-1.631-1.743-2.597-2.46a12 12 0 0 0-.689-.47c-.985-.957.13-1.743.387-1.836.27-.098.094-.433-.778-.428-.872.003-1.67.295-2.687.685a3 3 0 0 1-.465.136 9.6 9.6 0 0 0-2.883-.101c-1.885.21-3.39 1.1-4.497 2.622C.082 8.776-.231 10.854.152 13.02c.403 2.284 1.568 4.175 3.36 5.653 1.857 1.533 3.997 2.284 6.438 2.14 1.482-.085 3.132-.284 4.994-1.86.47.234.962.328 1.78.398.629.058 1.235-.031 1.705-.129.735-.155.684-.836.418-.961-2.155-1.004-1.682-.595-2.112-.926 1.095-1.295 2.768-3.598 3.284-6.733.05-.346.115-.834.108-1.114-.004-.171.035-.238.23-.257a4.2 4.2 0 0 0 1.545-.475c1.397-.763 1.96-2.016 2.093-3.517.02-.23-.004-.467-.247-.588M11.58 18.168c-2.088-1.642-3.101-2.183-3.52-2.16-.39.024-.32.472-.234.763.09.288.207.487.371.74.114.167.192.416-.113.603-.673.416-1.842-.14-1.897-.168-1.361-.801-2.5-1.86-3.301-3.306-.775-1.393-1.225-2.888-1.299-4.482-.02-.385.094-.522.477-.592a4.7 4.7 0 0 1 1.53-.038c2.131.311 3.946 1.264 5.467 2.774.868.86 1.525 1.887 2.202 2.89.72 1.066 1.494 2.082 2.48 2.915.348.291.626.513.892.677-.802.09-2.14.109-3.055-.615zm1.001-6.44a.306.306 0 0 1 .415-.287.3.3 0 0 1 .113.074.3.3 0 0 1 .086.214c0 .17-.136.307-.308.307a.303.303 0 0 1-.306-.307m3.11 1.596c-.2.081-.4.151-.591.16a1.25 1.25 0 0 1-.798-.254c-.274-.23-.47-.358-.551-.758a1.7 1.7 0 0 1 .015-.588c.07-.327-.007-.537-.238-.727-.188-.156-.426-.199-.689-.199a.6.6 0 0 1-.254-.078.253.253 0 0 1-.114-.358 1 1 0 0 1 .192-.21c.356-.202.767-.136 1.146.016.352.144.618.408 1.001.782.392.451.462.576.685.915.176.264.336.536.446.848.066.194-.02.353-.25.45";

    // xAI Grok: Resmi Simple-Icons / FontAwesome X geometrisi SVG path
    private const string SvgGrok =
        "M14.234 10.162 22.977 0h-2.072l-7.591 8.824L7.251 0H.258l9.168 13.343L.258 24H2.33l8.016-9.318L16.749 24h6.993zm-2.837 3.299-.929-1.329L3.076 1.56h3.182l5.965 8.532.929 1.329 7.754 11.09h-3.182z";

    // Offline Mod: FontAwesome fa-microchip SVG path
    private const string SvgMicrochip =
        "M160 80v48h192V80c0-26.5-21.5-48-48-48h-96c-26.5 0-48 21.5-48 48zM0 224v64c0 17.7 14.3 32 32 32h32v32c0 35.3 28.7 64 64 64h192c35.3 0 64-28.7 64-64v-32h32c17.7 0 32-14.3 32-32v-64c0-17.7-14.3-32-32-32h-32v-32c0-35.3-28.7-64-64-64H128c-35.3 0-64 28.7-64 64v32H32c-17.7 0-32 14.3-32 32zm128 0h256v128H128V224z";

    /// <summary>
    /// Sağlayıcıya ait orijinal FontAwesome / marka SVG ikonunu, istenen boyutta ve renkte
    /// saf monokrom (şeffaf zemin, yuvarlak rozetsiz) olarak üretir.
    /// </summary>
    public static Bitmap GetProviderIcon(string providerKey, int size = 18, Color? iconColor = null)
    {
        var color = iconColor ?? Color.FromArgb(148, 163, 184);
        string cacheKey = $"{providerKey.Trim().ToLowerInvariant()}_{size}_{color.ToArgb()}";

        return _cache.GetOrAdd(cacheKey, _ =>
        {
            string svgPath = GetSvgPath(providerKey);
            return RenderSvgPath(svgPath, size, color);
        });
    }

    private static string GetSvgPath(string providerKey)
    {
        return providerKey.Trim().ToLowerInvariant() switch
        {
            "gemini" or "google gemini" => SvgGemini,
            "openai" or "openai (gpt)" or "chatgpt" => SvgOpenAi,
            "deepseek" => SvgDeepSeek,
            "claude" or "anthropic" => SvgClaude,
            "grok" or "xai" or "xai grok" => SvgGrok,
            _ => SvgMicrochip
        };
    }

    private static Bitmap RenderSvgPath(string svgPathData, int size, Color color)
    {
        using var skBmp = new SKBitmap(size, size, SKColorType.Bgra8888, SKAlphaType.Premul);
        using (var canvas = new SKCanvas(skBmp))
        {
            canvas.Clear(SKColors.Transparent);

            using var path = SKPath.ParseSvgPathData(svgPathData);
            if (path != null)
            {
                var bounds = path.Bounds;
                if (bounds.Width > 0 && bounds.Height > 0)
                {
                    // Buton içinde dengeli durması için %6 oranında hafif nefes payı
                    float pad = size * 0.06f;
                    float availW = size - (pad * 2f);
                    float availH = size - (pad * 2f);
                    float scale = Math.Min(availW / bounds.Width, availH / bounds.Height);

                    float dx = (size - bounds.Width * scale) / 2f - bounds.Left * scale;
                    float dy = (size - bounds.Height * scale) / 2f - bounds.Top * scale;

                    canvas.Translate(dx, dy);
                    canvas.Scale(scale);

                    using var paint = new SKPaint
                    {
                        Color = new SKColor(color.R, color.G, color.B, color.A),
                        IsAntialias = true,
                        Style = SKPaintStyle.Fill
                    };
                    canvas.DrawPath(path, paint);
                }
            }
        }

        // SKBitmap -> GDI+ System.Drawing.Bitmap dönüşümü
        using var image = SKImage.FromBitmap(skBmp);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        using var ms = new MemoryStream(data.ToArray());
        return new Bitmap(ms);
    }
}
