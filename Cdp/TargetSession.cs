using System.Net.WebSockets;
using System.Text;
using System.Text.Json;

/// <summary>What a <see cref="TargetSession"/> injects, blocks and asks, kept apart so tests can steer it.</summary>
internal sealed class TargetSessionOptions
{
    /// <summary>The controller source, read each time it is injected.</summary>
    public required Func<string> Script { get; init; }

    /// <summary>Whether a page address is one the controller may run on.</summary>
    public Func<string, bool> IsPrimeVideoUrl { get; init; } = PrimeVideoTargetMatcher.IsPrimeVideoUrl;

    /// <summary>What to do with a request that matched one of <see cref="FetchPatterns"/>.</summary>
    public Func<string, AdRequestAction> Classify { get; init; } = AdBlocker.ClassifyRequest;

    /// <summary>The URL globs whose requests are paused and handed to <see cref="Classify"/>.</summary>
    public IReadOnlyList<string> FetchPatterns { get; init; } = AdBlocker.Patterns;

    /// <summary>How often the tab is asked whether the controller is still installed.</summary>
    public TimeSpan SafetyCheckInterval { get; init; } = TimeSpan.FromSeconds(10);
}

/// <summary>
/// One persistent DevTools connection to one browser tab.
/// </summary>
/// <remarks>
/// It owns both jobs the helper does for a tab, on a single socket. Requests that
/// match an ad pattern are paused and answered here, and the controller is
/// injected when the page finishes parsing rather than when a poll next notices
/// it is missing. Navigation events say when that happens, so an idle tab costs
/// nothing; a slow check on the same socket catches anything the events miss.
///
/// The socket is only ever written from the receive loop, which is what keeps the
/// single-writer rule of <see cref="ClientWebSocket"/> without a lock.
/// </remarks>
internal sealed class TargetSession
{
    private const int CheckInstalledId = 1;
    private const int PageEnableId = 101;
    private const int FetchEnableId = 100;

    private readonly ClientWebSocket socket = new();
    private readonly TargetSessionOptions options;
    private readonly byte[] buffer = new byte[32768];
    private string currentUrl;
    private int nextId = 300;

    private TargetSession(string initialUrl, TargetSessionOptions options)
    {
        currentUrl = initialUrl;
        this.options = options;
        socket.Options.Proxy = null;
    }

    /// <summary>
    /// Runs until the tab closes, the socket drops, or <paramref name="cancellationToken"/> fires.
    /// </summary>
    public static async Task RunAsync(
        string webSocketUrl,
        string initialUrl,
        TargetSessionOptions options,
        CancellationToken cancellationToken = default)
    {
        var session = new TargetSession(initialUrl, options);
        try
        {
            await session.RunCoreAsync(webSocketUrl, cancellationToken);
        }
        finally
        {
            session.socket.Dispose();
        }
    }

    private async Task RunCoreAsync(string webSocketUrl, CancellationToken cancellationToken)
    {
        using (var connectTimeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
        {
            connectTimeout.CancelAfter(TimeSpan.FromSeconds(5));
            await socket.ConnectAsync(new Uri(webSocketUrl), connectTimeout.Token);
        }

        // Cancelling a pending receive aborts a ClientWebSocket anyway, so do it
        // deliberately: that is what ends the loop below.
        using var registration = cancellationToken.Register(() => socket.Abort());

        await SendAsync(new
        {
            id = FetchEnableId,
            method = "Fetch.enable",
            @params = new
            {
                patterns = options.FetchPatterns
                    .Select(pattern => new { urlPattern = pattern, requestStage = "Request" })
                    .ToArray()
            }
        });
        await SendAsync(new { id = PageEnableId, method = "Page.enable" });
        await SendAsync(CdpPayloads.CheckInstalled(CheckInstalledId));

        var nextSafetyCheck = DateTime.UtcNow + options.SafetyCheckInterval;
        Task<string?>? receive = null;

        while (socket.State == WebSocketState.Open)
        {
            // No receive timeout: cancelling one aborts the socket. Waiting on the
            // receive alongside a timer lets the loop do periodic work instead.
            receive ??= CdpResponseReader.ReceiveTextMessageAsync(socket, buffer, CancellationToken.None);

            var wait = nextSafetyCheck - DateTime.UtcNow;
            using var timer = new CancellationTokenSource();
            var timeout = Task.Delay(wait > TimeSpan.Zero ? wait : TimeSpan.Zero, timer.Token);
            var finished = await Task.WhenAny(receive, timeout);
            timer.Cancel();

            if (finished != receive)
            {
                await SendAsync(CdpPayloads.CheckInstalled(CheckInstalledId));
                nextSafetyCheck = DateTime.UtcNow + options.SafetyCheckInterval;
                continue;
            }

            var text = await receive;
            receive = null;
            if (text is null) break;
            if (text.Length > 0) await HandleMessageAsync(text);
        }
    }

    private async Task HandleMessageAsync(string text)
    {
        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(text);
        }
        catch (JsonException)
        {
            return;
        }

        using (document)
        {
            var root = document.RootElement;

            if (root.TryGetProperty("method", out var methodElement))
            {
                switch (methodElement.GetString())
                {
                    case "Fetch.requestPaused":
                        await HandleRequestPausedAsync(root);
                        break;
                    case "Page.frameNavigated":
                        RememberMainFrameUrl(root);
                        break;
                    case "Page.domContentEventFired":
                        await InjectIfPrimeVideoAsync();
                        break;
                }
                return;
            }

            if (root.TryGetProperty("id", out var idElement) &&
                idElement.TryGetInt32(out var id) &&
                id == CheckInstalledId)
            {
                await HandleCheckResultAsync(root);
            }
        }
    }

    /// <summary>
    /// The answer to "is the controller installed, and where is this tab?".
    /// </summary>
    /// <remarks>
    /// It reports the address as well because the one the tab was discovered at
    /// can be stale by the time the socket is up.
    /// </remarks>
    private async Task HandleCheckResultAsync(JsonElement root)
    {
        if (!root.TryGetProperty("result", out var outer) ||
            !outer.TryGetProperty("result", out var inner) ||
            !inner.TryGetProperty("value", out var value) ||
            value.ValueKind != JsonValueKind.String)
        {
            return;
        }

        try
        {
            using var report = JsonDocument.Parse(value.GetString()!);
            if (report.RootElement.TryGetProperty("href", out var href) && href.GetString() is { } address)
            {
                currentUrl = address;
            }

            var installed = report.RootElement.TryGetProperty("installed", out var flag) && flag.ValueKind == JsonValueKind.True;
            if (!installed) await InjectIfPrimeVideoAsync();
        }
        catch (JsonException)
        {
            // Not our answer; the next check tries again.
        }
    }

    private void RememberMainFrameUrl(JsonElement root)
    {
        if (root.TryGetProperty("params", out var parameters) &&
            parameters.TryGetProperty("frame", out var frame) &&
            !frame.TryGetProperty("parentId", out _) &&
            frame.TryGetProperty("url", out var url) &&
            url.GetString() is { } address)
        {
            currentUrl = address;
        }
    }

    private async Task InjectIfPrimeVideoAsync()
    {
        if (!options.IsPrimeVideoUrl(currentUrl)) return;

        await SendAsync(new
        {
            id = nextId++,
            method = "Runtime.evaluate",
            @params = new
            {
                expression = options.Script(),
                awaitPromise = false,
                returnByValue = true
            }
        });
    }

    private async Task HandleRequestPausedAsync(JsonElement root)
    {
        string? pausedRequestId = null;
        try
        {
            if (!root.TryGetProperty("params", out var parameters) ||
                !parameters.TryGetProperty("requestId", out var requestIdElement) ||
                string.IsNullOrEmpty(requestIdElement.GetString()))
            {
                return;
            }

            var requestId = requestIdElement.GetString()!;
            pausedRequestId = requestId;

            var requestUrl = parameters.TryGetProperty("request", out var request) &&
                request.TryGetProperty("url", out var urlElement)
                    ? urlElement.GetString() ?? ""
                    : "";

            // A navigation is never an ad call, and failing one leaves the user on
            // a blocked-by-client error page.
            var isDocument = parameters.TryGetProperty("resourceType", out var typeElement) &&
                typeElement.ValueEquals("Document");
            var action = isDocument ? AdRequestAction.Continue : options.Classify(requestUrl);

            switch (action)
            {
                case AdRequestAction.FulfillEmptyVast:
                    var emptyVast = Convert.ToBase64String(
                        Encoding.UTF8.GetBytes("<?xml version=\"1.0\" encoding=\"UTF-8\"?><VAST version=\"3.0\"/>"));
                    await SendAsync(new
                    {
                        id = nextId++,
                        method = "Fetch.fulfillRequest",
                        @params = new
                        {
                            requestId,
                            responseCode = 200,
                            responseHeaders = new[]
                            {
                                new { name = "Content-Type", value = "application/xml" },
                                new { name = "Access-Control-Allow-Origin", value = "*" }
                            },
                            body = emptyVast
                        }
                    });
                    break;

                case AdRequestAction.Block:
                    await SendAsync(new
                    {
                        id = nextId++,
                        method = "Fetch.failRequest",
                        @params = new { requestId, errorReason = "BlockedByClient" }
                    });
                    break;

                default:
                    await SendAsync(new { id = nextId++, method = "Fetch.continueRequest", @params = new { requestId } });
                    break;
            }

            pausedRequestId = null;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Ad shield: failed to handle a network request event ({ex.GetType().Name}: {ex.Message}).");
            if (pausedRequestId is not null && socket.State == WebSocketState.Open)
            {
                // A request left paused hangs in the browser for good.
                try
                {
                    await SendAsync(new { id = nextId++, method = "Fetch.continueRequest", @params = new { requestId = pausedRequestId } });
                }
                catch (Exception continueEx)
                {
                    Console.WriteLine($"Ad shield: could not release a paused request ({continueEx.GetType().Name}).");
                }
            }
        }
    }

    private Task SendAsync<T>(T payload) =>
        SendAsync(JsonSerializer.SerializeToUtf8Bytes(payload));

    private Task SendAsync(byte[] payload) =>
        socket.SendAsync(payload, WebSocketMessageType.Text, true, CancellationToken.None);
}
