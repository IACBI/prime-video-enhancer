using System.Collections.Concurrent;
using System.Text.Json;

/// <summary>
/// Finds the Prime Video tabs of one browser and gives each a <see cref="TargetSession"/>.
/// </summary>
/// <remarks>
/// Polling is only for discovery: a <c>Target.getTargets</c> command on the
/// browser's pipe, and a session started for a tab that has none. A tab that
/// already has one is left alone, so a steady poll does not attach to every tab
/// on every round.
/// </remarks>
internal sealed class TargetWatcher
{
    private readonly CdpConnection connection;
    private readonly TargetSessionOptions options;
    private readonly ConcurrentDictionary<string, Task> sessions = new(StringComparer.Ordinal);

    public TargetWatcher(CdpConnection connection, TargetSessionOptions options)
    {
        this.connection = connection;
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
                !options.IsPrimeVideoUrl(target.Url))
            {
                continue;
            }

            found = true;
            if (InterceptorRegistry.TryRegister(target.TargetId))
            {
                sessions[target.TargetId] = RunSessionAsync(target.TargetId, target.Url);
            }
        }

        return found;
    }

    private async Task RunSessionAsync(string targetId, string initialUrl)
    {
        // Off the caller's stack: a session lives as long as its tab.
        await Task.Yield();
        try
        {
            await using var channel = await connection.AttachAsync(targetId);
            await TargetSession.RunAsync(channel, initialUrl, options);
        }
        catch (Exception ex) when (ex is OperationCanceledException or IOException or InvalidOperationException)
        {
            // The tab closed or navigated away, or the browser is closing.
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Ad shield: the connection to a Prime Video tab stopped ({ex.GetType().Name}: {ex.Message}). It will restart on the next poll.");
        }
        finally
        {
            InterceptorRegistry.Remove(targetId);
        }
    }

    private async Task<List<DebugTarget>> GetTargetsAsync(CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(3));
        var reply = await connection.SendAsync("Target.getTargets", cancellationToken: timeout.Token);
        return reply.TryGetProperty("targetInfos", out var targetInfos)
            ? targetInfos.Deserialize<List<DebugTarget>>(AppJson.Options) ?? []
            : [];
    }
}
