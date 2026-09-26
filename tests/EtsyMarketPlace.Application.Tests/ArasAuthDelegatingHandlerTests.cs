namespace EtsyMarketPlace.Application.Tests;

using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using EtsyMarketPlace.Application.Shipping;
using EtsyMarketPlace.Infrastructure.Shipping;

/// <summary>
/// Aras yetkilendirme mesaj işleyicisinin 401 davranışı: sessizce yenile ve
/// isteği tam olarak bir kez tekrarla; asla döngüye girme.
/// </summary>
public sealed class ArasAuthDelegatingHandlerTests
{
    /// <summary>
    /// Sağlayıcı taklidi. <see cref="Invalidate"/> çağrısı "yeni token üretildi"
    /// anlamına gelecek şekilde sıradaki token'a geçer.
    /// </summary>
    private sealed class StubTokenProvider : IArasTokenProvider
    {
        private readonly Queue<string> _pending;
        private string _current;

        public int InvalidateCalls;

        public StubTokenProvider(params string[] tokens)
        {
            _pending = new Queue<string>(tokens);
            _current = _pending.Count > 0 ? _pending.Dequeue() : string.Empty;
        }

        public string PeekToken => _current;

        public Task<string> GetValidTokenAsync(CancellationToken ct = default)
            => Task.FromResult(_current);

        public void Invalidate()
        {
            InvalidateCalls++;
            if (_pending.Count > 0)
            {
                _current = _pending.Dequeue();
            }
        }
    }

    private sealed class ScriptedHandler : HttpMessageHandler
    {
        private readonly Queue<HttpStatusCode> _codes;

        public List<HttpRequestMessage> Requests { get; } = new();

        public ScriptedHandler(params HttpStatusCode[] codes)
            => _codes = new Queue<HttpStatusCode>(codes);

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(request);
            var code = _codes.Count > 0 ? _codes.Dequeue() : HttpStatusCode.OK;
            return Task.FromResult(new HttpResponseMessage(code));
        }
    }

    private static (HttpClient Client, ScriptedHandler Inner, StubTokenProvider Tokens) Build(params HttpStatusCode[] codes)
        => Build(new StubTokenProvider("token-a"), codes);

    private static (HttpClient Client, ScriptedHandler Inner, StubTokenProvider Tokens) Build(StubTokenProvider tokens, params HttpStatusCode[] codes)
    {
        var inner = new ScriptedHandler(codes);
        var handler = new ArasAuthDelegatingHandler(tokens) { InnerHandler = inner };
        return (new HttpClient(handler), inner, tokens);
    }

    [Fact]
    public async Task AddsBearerTokenToEveryRequest()
    {
        var (client, inner, _) = Build(HttpStatusCode.OK);
        using (client)
        {
            await client.GetAsync("https://api.example.com/x");
        }

        var request = Assert.Single(inner.Requests);
        Assert.Equal("Bearer", request.Headers.Authorization?.Scheme);
        Assert.Equal("token-a", request.Headers.Authorization?.Parameter);
    }

    [Fact]
    public async Task RetriesOnceAfterRefreshingOn401()
    {
        var tokens = new StubTokenProvider("stale-token", "fresh-token");
        var (client, inner, _) = Build(tokens, HttpStatusCode.Unauthorized, HttpStatusCode.OK);

        HttpResponseMessage response;
        using (client)
        {
            response = await client.GetAsync("https://api.example.com/y");
        }

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(2, inner.Requests.Count);
        Assert.Equal("stale-token", inner.Requests[0].Headers.Authorization?.Parameter);
        Assert.Equal("fresh-token", inner.Requests[1].Headers.Authorization?.Parameter);
        Assert.Equal(1, tokens.InvalidateCalls);
    }

    [Fact]
    public async Task DoesNotRetryTwice()
    {
        var tokens = new StubTokenProvider("stale-token", "fresh-token", "another-token");
        var (client, inner, _) = Build(tokens, HttpStatusCode.Unauthorized, HttpStatusCode.Unauthorized);

        HttpResponseMessage response;
        using (client)
        {
            response = await client.GetAsync("https://api.example.com/z");
        }

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal(2, inner.Requests.Count);
        Assert.Equal(1, tokens.InvalidateCalls);
    }

    [Fact]
    public async Task DoesNotRetryWhenRefreshYieldsSameToken()
    {
        var (client, inner, tokens) = Build(HttpStatusCode.Unauthorized);
        using (client)
        {
            await client.GetAsync("https://api.example.com/w");
        }

        Assert.Single(inner.Requests);
        Assert.Equal(1, tokens.InvalidateCalls);
    }

    [Fact]
    public async Task PreservesBodyAndContentTypeOnRetry()
    {
        var tokens = new StubTokenProvider("stale", "fresh");
        var (client, inner, _) = Build(tokens, HttpStatusCode.Forbidden, HttpStatusCode.OK);

        using (client)
        {
            var content = new StringContent("{\"a\":1}", System.Text.Encoding.UTF8, "application/json");
            await client.PostAsync("https://api.example.com/p", content);
        }

        Assert.Equal(2, inner.Requests.Count);
        string body = await inner.Requests[1].Content!.ReadAsStringAsync();
        Assert.Equal("{\"a\":1}", body);
        Assert.Equal("application/json", inner.Requests[1].Content!.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task DoesNotRetryOnSuccess()
    {
        var (client, inner, tokens) = Build(HttpStatusCode.OK);
        using (client)
        {
            await client.GetAsync("https://api.example.com/ok");
        }

        Assert.Single(inner.Requests);
        Assert.Equal(0, tokens.InvalidateCalls);
    }
}
