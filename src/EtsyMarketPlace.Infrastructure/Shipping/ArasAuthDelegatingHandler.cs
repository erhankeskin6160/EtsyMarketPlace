namespace EtsyMarketPlace.Infrastructure.Shipping;

using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using EtsyMarketPlace.Application.Shipping;
using EtsyMarketPlace.Domain.Shipping;

/// <summary>
/// Aras Global isteklerine geçerli Bearer tokeni ekler; yetki hatası (401/403) alındığında
/// tokeni sessizce yenileyip isteği TAM OLARAK BİR KEZ tekrarlar. İkinci başarısızlıkta
/// döngüye girmez, yanıtı olduğu gibi yukarı verir.
/// </summary>
public sealed class ArasAuthDelegatingHandler : DelegatingHandler
{
    private static readonly HttpRequestOptionsKey<bool> RetriedKey = new("aras.auth.retried");

    private readonly IArasTokenProvider _tokens;

    public ArasAuthDelegatingHandler(IArasTokenProvider tokens)
    {
        _tokens = tokens ?? throw new ArgumentNullException(nameof(tokens));
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        string token = await _tokens.GetValidTokenAsync(cancellationToken).ConfigureAwait(false);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await base.SendAsync(request, cancellationToken).ConfigureAwait(false);

        if (!IsAuthFailure(response))
        {
            return response;
        }

        if (request.Options.TryGetValue(RetriedKey, out bool alreadyRetried) && alreadyRetried)
        {
            return response;
        }

        // Token bayat kabul edilir; sağlayıcıyı zorla yenile.
        _tokens.Invalidate();

        string refreshed;
        try
        {
            refreshed = await _tokens.GetValidTokenAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (ArasGlobalTokenExpiredException)
        {
            // Sessiz yenileme yapılamadı: eldeki yanıtı yukarı ver, üst katman karar verir.
            return response;
        }

        if (string.Equals(refreshed, token, StringComparison.Ordinal))
        {
            // Yenileme yeni bir token üretmedi; aynı isteği boşuna tekrarlamayalım.
            return response;
        }

        var retry = await CloneAsync(request, cancellationToken).ConfigureAwait(false);
        retry.Options.Set(RetriedKey, true);
        retry.Headers.Authorization = new AuthenticationHeaderValue("Bearer", refreshed);

        response.Dispose();
        return await base.SendAsync(retry, cancellationToken).ConfigureAwait(false);
    }

    private static bool IsAuthFailure(HttpResponseMessage response)
        => response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden;

    private static async Task<HttpRequestMessage> CloneAsync(HttpRequestMessage request, CancellationToken ct)
    {
        var clone = new HttpRequestMessage(request.Method, request.RequestUri)
        {
            Version = request.Version,
            VersionPolicy = request.VersionPolicy
        };

        foreach (var header in request.Headers)
        {
            clone.Headers.TryAddWithoutValidation(header.Key, header.Value);
        }

        if (request.Content != null)
        {
            byte[] body = await request.Content.ReadAsByteArrayAsync(ct).ConfigureAwait(false);
            clone.Content = new ByteArrayContent(body);
            foreach (var header in request.Content.Headers)
            {
                clone.Content.Headers.TryAddWithoutValidation(header.Key, header.Value);
            }
        }

        foreach (var option in request.Options)
        {
            if (option.Value is { } value)
            {
                clone.Options.Set(new HttpRequestOptionsKey<object?>(option.Key), value);
            }
        }

        return clone;
    }
}

/// <summary>
/// Aras Global için sessiz yenilemeli <see cref="HttpClient"/> üretir.
/// </summary>
public static class ArasHttpClientFactory
{
    public static HttpClient Create(IArasTokenProvider tokens)
    {
        var handler = new ArasAuthDelegatingHandler(tokens)
        {
            InnerHandler = new HttpClientHandler()
        };

        return new HttpClient(handler);
    }
}
