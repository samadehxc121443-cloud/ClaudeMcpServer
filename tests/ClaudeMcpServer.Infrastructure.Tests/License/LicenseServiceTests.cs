using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using ClaudeMcpServer.Domain.Interfaces;
using ClaudeMcpServer.Infrastructure.Configuration;
using ClaudeMcpServer.Infrastructure.License;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace ClaudeMcpServer.Infrastructure.Tests.License;

/// <summary>Tests for <see cref="LicenseService"/> token exchange and caching.</summary>
public class LicenseServiceTests
{
    // ── Helpers ────────────────────────────────────────────────────────────

    /// <summary>
    /// Minimal <see cref="HttpMessageHandler"/> that returns pre-configured responses
    /// and tracks how many HTTP calls were made.
    /// </summary>
    private sealed class FakeHttpHandler : HttpMessageHandler
    {
        private readonly Queue<HttpResponseMessage> _responses;
        public int CallCount { get; private set; }

        public FakeHttpHandler(params HttpResponseMessage[] responses)
            => _responses = new Queue<HttpResponseMessage>(responses);

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage _, CancellationToken ct)
        {
            CallCount++;
            if (_responses.Count == 0)
                throw new InvalidOperationException("FakeHttpHandler has no more responses.");
            return Task.FromResult(_responses.Dequeue());
        }
    }

    /// <summary>Handler that always throws <see cref="HttpRequestException"/> to simulate a network failure.</summary>
    private sealed class NetworkFailHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage _, CancellationToken ct)
            => Task.FromException<HttpResponseMessage>(new HttpRequestException("Network unreachable."));
    }

    /// <summary>Handler that always throws <see cref="TaskCanceledException"/> to simulate a timeout.</summary>
    private sealed class TimeoutHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage _, CancellationToken ct)
            => Task.FromException<HttpResponseMessage>(new TaskCanceledException("Request timed out."));
    }

    private static IOptions<LicenseSettings> Opts(
        string serverUrl = "https://test.example.com",
        string apiKey    = "testkey123",
        bool   devMode   = false)
        => Options.Create(new LicenseSettings { ServerUrl = serverUrl, ApiKey = apiKey, DevMode = devMode });

    private static LicenseService Build(HttpMessageHandler handler, IOptions<LicenseSettings>? opts = null)
        => new(opts ?? Opts(), new HttpClient(handler), NullLogger<LicenseService>.Instance);

    private static HttpResponseMessage OkToken(string clientName = "Test Client") =>
        new(HttpStatusCode.OK)
        {
            Content = new StringContent(
                JsonSerializer.Serialize(new
                {
                    token      = Guid.NewGuid().ToString("N"),
                    clientName,
                    expiresAt  = DateTime.UtcNow.AddHours(1)
                }),
                Encoding.UTF8, "application/json")
        };

    private static HttpResponseMessage ErrorResponse(HttpStatusCode status, string error) =>
        new(status)
        {
            Content = new StringContent(
                JsonSerializer.Serialize(new { error }),
                Encoding.UTF8, "application/json")
        };

    // ── DevMode ────────────────────────────────────────────────────────────

    /// <summary>When DevMode is true no HTTP call is made and the result is valid.</summary>
    [Fact]
    public async Task ValidateAsync_Returns_DevMode_And_Makes_No_HTTP_Call()
    {
        var handler = new FakeHttpHandler(); // no responses configured — would throw if called
        var svc = Build(handler, Opts(devMode: true));

        var result = await svc.ValidateAsync(CancellationToken.None);

        Assert.True(result.IsValid);
        Assert.Equal("dev", result.ClientName);
        Assert.Equal(0, handler.CallCount);
    }

    // ── Config validation ──────────────────────────────────────────────────

    /// <summary>Missing ServerUrl produces an invalid result without touching the network.</summary>
    [Fact]
    public async Task ValidateAsync_Returns_Invalid_When_ServerUrl_Missing()
    {
        var handler = new FakeHttpHandler();
        var svc = Build(handler, Opts(serverUrl: ""));

        var result = await svc.ValidateAsync(CancellationToken.None);

        Assert.False(result.IsValid);
        Assert.Contains("ServerUrl", result.Message);
        Assert.Equal(0, handler.CallCount);
    }

    /// <summary>Missing ApiKey produces an invalid result without touching the network.</summary>
    [Fact]
    public async Task ValidateAsync_Returns_Invalid_When_ApiKey_Missing()
    {
        var handler = new FakeHttpHandler();
        var svc = Build(handler, Opts(apiKey: ""));

        var result = await svc.ValidateAsync(CancellationToken.None);

        Assert.False(result.IsValid);
        Assert.Contains("ApiKey", result.Message);
        Assert.Equal(0, handler.CallCount);
    }

    // ── Successful token exchange ──────────────────────────────────────────

    /// <summary>A 200 response with a valid token yields a Valid result with the correct client name.</summary>
    [Fact]
    public async Task ValidateAsync_Returns_Valid_With_ClientName_On_Successful_Token_Exchange()
    {
        var handler = new FakeHttpHandler(OkToken("Jorge Lopez"));
        var svc = Build(handler);

        var result = await svc.ValidateAsync(CancellationToken.None);

        Assert.True(result.IsValid);
        Assert.Equal("Jorge Lopez", result.ClientName);
        Assert.Equal(1, handler.CallCount);
    }

    // ── Token caching ──────────────────────────────────────────────────────

    /// <summary>
    /// After a successful exchange the token is cached: a second call within the
    /// valid window must NOT make another HTTP request.
    /// </summary>
    [Fact]
    public async Task ValidateAsync_Returns_Cached_Result_On_Second_Call_Without_HTTP()
    {
        // Only one response is queued; a second HTTP call would throw.
        var handler = new FakeHttpHandler(OkToken("Cached Client"));
        var svc = Build(handler);

        var first  = await svc.ValidateAsync(CancellationToken.None);
        var second = await svc.ValidateAsync(CancellationToken.None);

        Assert.True(first.IsValid);
        Assert.True(second.IsValid);
        Assert.Equal(first.ClientName, second.ClientName);
        Assert.Equal(1, handler.CallCount); // only one HTTP call for both invocations
    }

    // ── Server error responses ─────────────────────────────────────────────

    /// <summary>A 401 from the token endpoint produces an invalid result with the server's error message.</summary>
    [Fact]
    public async Task ValidateAsync_Returns_Invalid_With_Server_Error_Message_On_401()
    {
        var handler = new FakeHttpHandler(ErrorResponse(HttpStatusCode.Unauthorized, "License key has been revoked."));
        var svc = Build(handler);

        var result = await svc.ValidateAsync(CancellationToken.None);

        Assert.False(result.IsValid);
        Assert.Contains("revoked", result.Message);
    }

    /// <summary>A non-success status without a parseable body still produces an invalid result.</summary>
    [Fact]
    public async Task ValidateAsync_Returns_Invalid_On_Non_Success_Status_Without_Body()
    {
        var handler = new FakeHttpHandler(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)
        {
            Content = new StringContent("upstream error", Encoding.UTF8, "text/plain")
        });
        var svc = Build(handler);

        var result = await svc.ValidateAsync(CancellationToken.None);

        Assert.False(result.IsValid);
        Assert.Contains("503", result.Message);
    }

    // ── Network / timeout failures ─────────────────────────────────────────

    /// <summary>An <see cref="HttpRequestException"/> produces an invalid result describing the network failure.</summary>
    [Fact]
    public async Task ValidateAsync_Returns_Invalid_On_Network_Failure()
    {
        var svc = Build(new NetworkFailHandler());

        var result = await svc.ValidateAsync(CancellationToken.None);

        Assert.False(result.IsValid);
        Assert.Contains("reach", result.Message); // "Could not reach license server"
    }

    /// <summary>A timeout produces an invalid result mentioning the timeout.</summary>
    [Fact]
    public async Task ValidateAsync_Returns_Invalid_On_Timeout()
    {
        var svc = Build(new TimeoutHandler());

        var result = await svc.ValidateAsync(CancellationToken.None);

        Assert.False(result.IsValid);
        Assert.Contains("timed out", result.Message);
    }
}
