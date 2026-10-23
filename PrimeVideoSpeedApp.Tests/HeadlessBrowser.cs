using System.Collections.Concurrent;
using System.ComponentModel;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;

// PipeBrowser is Windows-only. Nothing here runs elsewhere: FindExecutable returns
// null off Windows, and that null is what makes the integration tests skip.
#pragma warning disable CA1416

/// <summary>
/// A real headless Edge (or another Chromium, via PVSC_BROWSER) on a throwaway
/// profile, driven over the same pipe transport the helper uses, for tests that
/// need the actual DevTools protocol rather than a stand-in.
/// </summary>
internal sealed class HeadlessBrowser : IAsyncDisposable
{
    private static readonly TimeSpan StartupTimeout = TimeSpan.FromSeconds(20);
    private static readonly TimeSpan ExitTimeout = TimeSpan.FromSeconds(10);

    private readonly string profileDirectory;
    private readonly PipeBrowser browser;

    private HeadlessBrowser(string profileDirectory, PipeBrowser browser)
    {
        this.profileDirectory = profileDirectory;
        this.browser = browser;
    }

    public CdpConnection Connection => browser.Connection;

    public Process Process => browser.Process;

    public static string? FindExecutable()
    {
        // PipeBrowser hands the pipe to the browser through CreateProcess, so off Windows there is nothing to start.
        if (!OperatingSystem.IsWindows()) return null;

        string?[] candidates =
        [
            Environment.GetEnvironmentVariable("PVSC_BROWSER"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Microsoft", "Edge", "Application", "msedge.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Microsoft", "Edge", "Application", "msedge.exe")
        ];
        return candidates.FirstOrDefault(candidate => !string.IsNullOrEmpty(candidate) && File.Exists(candidate));
    }

    public static async Task<HeadlessBrowser> StartAsync(string executable)
    {
        var profile = Directory.CreateTempSubdirectory("pvsc-tests-").FullName;
        var pipe = PipeBrowser.Launch(executable,
            $"--headless=new --user-data-dir=\"{profile}\" --no-first-run --no-default-browser-check --disable-extensions --disable-sync --disable-background-networking --disable-gpu --lang=en-US about:blank");
        var browser = new HeadlessBrowser(profile, pipe);
        try
        {
            using var deadline = new CancellationTokenSource(StartupTimeout);
            await pipe.Connection.SendAsync("Browser.getVersion", cancellationToken: deadline.Token);
            return browser;
        }
        catch (Exception ex) when (ex is OperationCanceledException or IOException or InvalidOperationException)
        {
            await browser.DisposeAsync();
            throw new TimeoutException("The browser did not answer over its DevTools pipe.", ex);
        }
    }

    /// <summary>Opens a new tab and attaches a test client to it.</summary>
    public async Task<TestTab> NewTabAsync(string url = "about:blank")
    {
        var created = await Connection.SendAsync("Target.createTarget", new { url });
        var targetId = created.GetProperty("targetId").GetString()!;
        var channel = await Connection.AttachAsync(targetId);
        return new TestTab(this, targetId, new CdpTestClient(channel));
    }

    /// <summary>Attaches a separate session to a tab, for handing to a session under test.</summary>
    public Task<SessionChannel> AttachAsync(string targetId) => Connection.AttachAsync(targetId);

    public async Task CloseTabAsync(string targetId)
    {
        try
        {
            await Connection.SendAsync("Target.closeTarget", new { targetId });
        }
        catch (Exception ex) when (ex is IOException or InvalidOperationException)
        {
            // Already closed, or the browser is gone.
        }
    }

    public async ValueTask DisposeAsync()
    {
        try
        {
            using var closing = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            await Connection.SendAsync("Browser.close", cancellationToken: closing.Token);
        }
        catch (Exception ex) when (ex is IOException or InvalidOperationException or OperationCanceledException)
        {
            // The browser is already closing, or gone.
        }

        try
        {
            using var exit = new CancellationTokenSource(ExitTimeout);
            await Process.WaitForExitAsync(exit.Token);
        }
        catch (OperationCanceledException)
        {
            try
            {
                Process.Kill(entireProcessTree: true);
            }
            catch (Exception ex) when (ex is InvalidOperationException or Win32Exception)
            {
                // Exited between the wait and the kill.
            }
        }

        await browser.DisposeAsync();

        // Windows will not delete a directory a dying browser still has open.
        for (var attempt = 0; attempt < 40; attempt++)
        {
            try
            {
                Directory.Delete(profileDirectory, recursive: true);
                return;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                await Task.Delay(250);
            }
        }
    }
}

internal sealed class TestTab : IAsyncDisposable
{
    private readonly HeadlessBrowser browser;

    public TestTab(HeadlessBrowser browser, string targetId, CdpTestClient client)
    {
        this.browser = browser;
        TargetId = targetId;
        Client = client;
    }

    public string TargetId { get; }

    public CdpTestClient Client { get; }

    public async Task NavigateAsync(string url)
    {
        await Client.SendAsync("Page.navigate", new { url });
        await TestWait.UntilAsync(async () => await Client.EvaluateAsync("location.href") == url && await Client.EvaluateAsync("document.readyState") == "complete", $"{url} to load");
    }

    public async ValueTask DisposeAsync()
    {
        await Client.DisposeAsync();
        await browser.CloseTabAsync(TargetId);
    }
}

/// <summary>A minimal DevTools client for steering a tab from a test.</summary>
internal sealed class CdpTestClient : IAsyncDisposable
{
    private readonly ICdpChannel channel;
    private readonly ConcurrentDictionary<int, TaskCompletionSource<JsonElement>> pending = new();
    private readonly Task reader;
    private int nextId;

    public CdpTestClient(ICdpChannel channel)
    {
        this.channel = channel;
        reader = Task.Run(ReadLoopAsync);
    }

    private async Task ReadLoopAsync()
    {
        try
        {
            while (await channel.ReceiveAsync() is { } text)
            {
                using var document = JsonDocument.Parse(text);
                if (document.RootElement.TryGetProperty("id", out var idElement) &&
                    idElement.TryGetInt32(out var id) &&
                    pending.TryRemove(id, out var completion))
                {
                    completion.TrySetResult(document.RootElement.Clone());
                }
            }
        }
        finally
        {
            foreach (var entry in pending) entry.Value.TrySetException(new IOException("The tab closed."));
        }
    }

    public async Task<JsonElement> SendAsync(string method, object? parameters = null)
    {
        var id = Interlocked.Increment(ref nextId);
        var completion = new TaskCompletionSource<JsonElement>(TaskCreationOptions.RunContinuationsAsynchronously);
        pending[id] = completion;
        try
        {
            // The reader may have ended before this request was registered, in
            // which case nothing would ever answer it.
            if (reader.IsCompleted) throw new IOException("The tab closed.");
            await channel.SendAsync(JsonSerializer.SerializeToUtf8Bytes(new { id, method, @params = parameters ?? new { } }));

            var reply = await completion.Task.WaitAsync(TimeSpan.FromSeconds(20));
            if (reply.TryGetProperty("error", out var error))
            {
                throw new InvalidOperationException($"{method} failed: {error.GetProperty("message").GetString()}");
            }

            return reply.GetProperty("result");
        }
        finally
        {
            pending.TryRemove(id, out _);
        }
    }

    /// <summary>Evaluates an expression in the page and returns its value as text.</summary>
    public async Task<string?> EvaluateAsync(string expression)
    {
        var result = await SendAsync("Runtime.evaluate", new { expression, returnByValue = true, awaitPromise = true });
        if (result.TryGetProperty("exceptionDetails", out var details))
        {
            throw new InvalidOperationException($"The page threw: {details.GetProperty("text").GetString()}");
        }

        var value = result.GetProperty("result");
        if (!value.TryGetProperty("value", out var inner)) return null;
        return inner.ValueKind switch
        {
            JsonValueKind.String => inner.GetString(),
            JsonValueKind.Null => null,
            _ => inner.GetRawText()
        };
    }

    /// <summary>Detaches from the tab, then waits briefly for the reader to finish.</summary>
    public async ValueTask DisposeAsync()
    {
        if (channel is IAsyncDisposable disposable) await disposable.DisposeAsync();
        await Task.WhenAny(reader, Task.Delay(2000));
    }
}

internal static class TestWait
{
    public static async Task UntilAsync(Func<Task<bool>> condition, string description, int timeoutMs = 10000)
    {
        var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
        while (true)
        {
            try
            {
                if (await condition()) return;
            }
            catch (InvalidOperationException)
            {
                // The page may be mid-navigation; ask again.
            }

            if (DateTime.UtcNow > deadline) throw new TimeoutException($"Timed out waiting for {description}.");
            await Task.Delay(100);
        }
    }
}

/// <summary>A few pages and endpoints over plain HTTP on loopback, with a count of what was fetched.</summary>
internal sealed class FixtureServer : IDisposable
{
    private readonly TcpListener listener = new(IPAddress.Loopback, 0);
    private readonly ConcurrentDictionary<string, int> hits = new(StringComparer.Ordinal);
    private readonly CancellationTokenSource stop = new();

    public FixtureServer()
    {
        listener.Start();
        BaseUrl = $"http://127.0.0.1:{((IPEndPoint)listener.LocalEndpoint).Port}";
        _ = Task.Run(AcceptLoopAsync);
    }

    public string BaseUrl { get; }

    public int HitsFor(string path) => hits.GetValueOrDefault(path);

    private async Task AcceptLoopAsync()
    {
        while (!stop.IsCancellationRequested)
        {
            TcpClient client;
            try
            {
                client = await listener.AcceptTcpClientAsync(stop.Token);
            }
            catch (Exception ex) when (ex is OperationCanceledException or ObjectDisposedException or SocketException)
            {
                return;
            }

            _ = Task.Run(() => ServeAsync(client));
        }
    }

    private async Task ServeAsync(TcpClient client)
    {
        using (client)
        {
            try
            {
                var stream = client.GetStream();
                var buffer = new byte[8192];
                var read = await stream.ReadAsync(buffer);
                var requestLine = Encoding.ASCII.GetString(buffer, 0, read).Split("\r\n")[0].Split(' ');
                var path = requestLine.Length > 1 ? requestLine[1] : "/";
                hits.AddOrUpdate(path, 1, (_, count) => count + 1);

                var (contentType, body) = path switch
                {
                    "/ok/data.txt" => ("text/plain", "fine"),
                    "/vast/ad.xml" => ("application/xml", "<VAST version=\"2.0\"><Ad/></VAST>"),
                    "/blocked/track" => ("text/plain", "tracked"),
                    _ => ("text/html", "<!doctype html><html><body><div class=\"webPlayerSDKContainer\" style=\"width:640px;height:360px\"><video style=\"width:640px;height:360px\"></video></div></body></html>")
                };

                var bytes = Encoding.UTF8.GetBytes(body);
                var header = $"HTTP/1.1 200 OK\r\nContent-Type: {contentType}\r\nContent-Length: {bytes.Length}\r\nConnection: close\r\n\r\n";
                await stream.WriteAsync(Encoding.ASCII.GetBytes(header));
                await stream.WriteAsync(bytes);
            }
            catch (Exception ex) when (ex is IOException or SocketException or ObjectDisposedException)
            {
            }
        }
    }

    public void Dispose()
    {
        stop.Cancel();
        listener.Stop();
    }
}
