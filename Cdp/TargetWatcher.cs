using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text.Json;

/// <summary>
/// Finds the Prime Video tabs of one browser and gives each a <see cref="TargetSession"/>.
/// </summary>
/// <remarks>
/// Polling is only for discovery now: a list request over HTTP, and a session
/// started for a tab that has none. A tab that already has one is left alone, so
/// a steady poll no longer opens a socket per tab per round.
/// </remarks>
internal sealed class TargetWatcher
{
    private readonly HttpClient httpClient;
    private readonly int port;
    private readonly TargetSessionOptions options;
    private readonly ConcurrentDictionary<string, Task> sessions = new(StringComparer.Ordinal);

    public TargetWatcher(HttpClient httpClient, int port, TargetSessionOptions options)
    {
        this.httpClient = httpClient;
        this.port = port;
        this.options = options;
    }

    /// <summary>Sessions that are still running.</summary>
    public int RunningSessions => sessions.Count(session => !session.Value.IsCompleted);

    /// <summary>
    /// Lists the tabs once and starts a session for each Prime Video tab that
    /// lacks one. Returns whether there is at least one such tab.
    /// </summary>
    public async Task<bool> PollAsync(CancellationToken cancellationToken = default)
    {
        var found = false;
        foreach (var target in await GetTargetsAsync(cancellationToken))
        {
            if (!string.Equals(target.Type, "page", StringComparison.OrdinalIgnoreCase) ||
                !options.IsPrimeVideoUrl(target.Url) ||
                PrimeVideoTargetMatcher.TryGetLocalDebuggerUrl(target, port) is not { } debuggerUrl)
            {
                continue;
            }

            found = true;
            if (InterceptorRegistry.TryRegister(debuggerUrl))
            {
                sessions[debuggerUrl] = RunSessionAsync(debuggerUrl, target.Url);
            }
        }

        return found;
    }

    private async Task RunSessionAsync(string debuggerUrl, string initialUrl)
    {
        // Off the caller's stack: a session lives as long as its tab.
        await Task.Yield();
        try
        {
            await TargetSession.RunAsync(debuggerUrl, initialUrl, options);
        }
        catch (Exception ex) when (ex is OperationCanceledException or WebSocketException or IOException or HttpRequestException)
        {
            // The tab navigated away, crashed, or Edge is closing.
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Ad shield: the connection to a Prime Video tab stopped ({ex.GetType().Name}: {ex.Message}). It will restart on the next poll.");
        }
        finally
        {
            InterceptorRegistry.Remove(debuggerUrl);
        }
    }

    private async Task<List<DebugTarget>> GetTargetsAsync(CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(3));
        using var response = await httpClient.GetAsync($"http://127.0.0.1:{port}/json", timeout.Token);
        response.EnsureSuccessStatusCode();
        using var stream = await response.Content.ReadAsStreamAsync(timeout.Token);
        return await JsonSerializer.DeserializeAsync<List<DebugTarget>>(stream, AppJson.Options, timeout.Token) ?? [];
    }
}
