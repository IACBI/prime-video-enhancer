using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;

/// <summary>
/// A real headless Edge (or another Chromium, via PVSC_BROWSER) on a throwaway
/// profile, for tests that need the actual DevTools protocol rather than a stand-in.
/// </summary>
internal sealed class HeadlessBrowser : IAsyncDisposable
{
    private readonly string profileDirectory;
    private readonly HttpClient http = new(new SocketsHttpHandler { UseProxy = false });

    private HeadlessBrowser(string profileDirectory, int port)
    {
        this.profileDirectory = profileDirectory;
        Port = port;
    }

    public int Port { get; }

    public HttpClient Http => http;

    public static string? FindExecutable()
    {
        string?[] candidates =
        [
            Environment.GetEnvironmentVariable("PVSC_BROWSER"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Microsoft", "Edge", "Application", "msedge.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Microsoft", "Edge", "Application", "msedge.exe"),
            "/usr/bin/microsoft-edge",
            "/usr/bin/google-chrome",
            "/usr/bin/chromium",
            "/usr/bin/chromium-browser"
        ];
        return candidates.FirstOrDefault(candidate => !string.IsNullOrEmpty(candidate) && File.Exists(candidate));
    }

    public static async Task<HeadlessBrowser> StartAsync(string executable)
    {
        var profile = Directory.CreateTempSubdirectory("pvsc-tests-").FullName;
        Process.Start(new ProcessStartInfo
        {
            FileName = executable,
            UseShellExecute = false,
            CreateNoWindow = true,
            ArgumentList =
            {
                "--headless=new", "--remote-debugging-port=0", "--remote-debugging-address=127.0.0.1",
                $"--user-data-dir={profile}", "--no-first-run", "--no-default-browser-check",
                "--disable-extensions", "--disable-sync", "--disable-background-networking",
                "--disable-gpu", "--lang=en-US", "about:blank"
            }
        })?.Dispose();

        // The process started above is a launcher that hands over to a detached
        // browser, so the port is read from the file the browser writes instead.
        var portFile = Path.Combine(profile, "DevToolsActivePort");
        var deadline = DateTime.UtcNow.AddSeconds(30);
        while (DateTime.UtcNow < deadline)
        {
            try
            {
                if (File.Exists(portFile) && int.TryParse(File.ReadLines(portFile).FirstOrDefault(), out var port) && port > 0)
                {
                    var browser = new HeadlessBrowser(profile, port);
                    await browser.WaitForEndpointAsync();
                    return browser;
                }
            }
            catch (IOException)
            {
                // Still being written.
            }

            await Task.Delay(100);
        }

        throw new TimeoutException("The browser did not publish its debugging port.");
    }

    private async Task WaitForEndpointAsync()
    {
        var deadline = DateTime.UtcNow.AddSeconds(30);
        while (DateTime.UtcNow < deadline)
        {
            try
            {
                using var response = await http.GetAsync($"http://127.0.0.1:{Port}/json/version");
                if (response.IsSuccessStatusCode) return;
            }
            catch (HttpRequestException)
            {
            }

            await Task.Delay(100);
        }

        throw new TimeoutException("The browser's debugging endpoint never answered.");
    }

    /// <summary>Opens a new tab and connects to it.</summary>
    public async Task<TestTab> NewTabAsync(string url = "about:blank")
    {
        using var request = new HttpRequestMessage(HttpMethod.Put, $"http://127.0.0.1:{Port}/json/new?{Uri.EscapeDataString(url)}");
        using var response = await http.SendAsync(request);
        response.EnsureSuccessStatusCode();
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var id = document.RootElement.GetProperty("id").GetString()!;
        var webSocketUrl = document.RootElement.GetProperty("webSocketDebuggerUrl").GetString()!;
        return new TestTab(this, id, webSocketUrl, await CdpTestClient.ConnectAsync(webSocketUrl));
    }

    public async Task CloseTabAsync(string id)
    {
        try
        {
            using var response = await http.GetAsync($"http://127.0.0.1:{Port}/json/close/{id}");
        }
        catch (HttpRequestException)
        {
        }
    }

    public async ValueTask DisposeAsync()
    {
        // Ask the browser to close itself; killing the launcher would orphan it.
        try
        {
            var version = await http.GetStringAsync($"http://127.0.0.1:{Port}/json/version");
            using var document = JsonDocument.Parse(version);
            using var control = new ClientWebSocket();
            control.Options.Proxy = null;
            await control.ConnectAsync(new Uri(document.RootElement.GetProperty("webSocketDebuggerUrl").GetString()!), CancellationToken.None);
            await control.SendAsync(Encoding.UTF8.GetBytes("{\"id\":1,\"method\":\"Browser.close\"}"), WebSocketMessageType.Text, true, CancellationToken.None);
        }
        catch (Exception ex) when (ex is HttpRequestException or WebSocketException or IOException)
        {
        }

        for (var attempt = 0; attempt < 50 && await IsUpAsync(); attempt++) await Task.Delay(100);

        if (await IsUpAsync() && OperatingSystem.IsWindows())
        {
            using var kill = Process.Start(new ProcessStartInfo
            {
                FileName = "powershell",
                UseShellExecute = false,
                CreateNoWindow = true,
                ArgumentList =
                {
                    "-NoProfile", "-Command",
                    $"Get-CimInstance Win32_Process | Where-Object {{ $_.CommandLine -like '*{profileDirectory}*' }} | ForEach-Object {{ Stop-Process -Id $_.ProcessId -Force }}"
                }
            });
            kill?.WaitForExit(10000);
        }

        http.Dispose();

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

    private async Task<bool> IsUpAsync()
    {
        try
        {
            using var response = await http.GetAsync($"http://127.0.0.1:{Port}/json/version");
            return true;
        }
        catch (HttpRequestException)
        {
            return false;
        }
    }
}

internal sealed class TestTab : IAsyncDisposable
{
    private readonly HeadlessBrowser browser;

    public TestTab(HeadlessBrowser browser, string id, string webSocketUrl, CdpTestClient client)
    {
        this.browser = browser;
        Id = id;
        WebSocketUrl = webSocketUrl;
        Client = client;
    }

    public string Id { get; }

    public string WebSocketUrl { get; }

    public CdpTestClient Client { get; }

    public async Task NavigateAsync(string url)
    {
        await Client.SendAsync("Page.navigate", new { url });
        await TestWait.UntilAsync(async () => await Client.EvaluateAsync("location.href") == url && await Client.EvaluateAsync("document.readyState") == "complete", $"{url} to load");
    }

    public async ValueTask DisposeAsync()
    {
        await Client.DisposeAsync();
        await browser.CloseTabAsync(Id);
    }
}

/// <summary>A minimal DevTools client for steering a tab from a test.</summary>
internal sealed class CdpTestClient : IAsyncDisposable
{
    private readonly ClientWebSocket socket = new();
    private readonly ConcurrentDictionary<int, TaskCompletionSource<JsonElement>> pending = new();
    private int nextId;
    private Task? reader;

    public static async Task<CdpTestClient> ConnectAsync(string webSocketUrl)
    {
        var client = new CdpTestClient();
        client.socket.Options.Proxy = null;
        await client.socket.ConnectAsync(new Uri(webSocketUrl), CancellationToken.None);
        client.reader = Task.Run(client.ReadLoopAsync);
        return client;
    }

    private async Task ReadLoopAsync()
    {
        var buffer = new byte[65536];
        try
        {
            while (socket.State == WebSocketState.Open)
            {
                var text = await CdpResponseReader.ReceiveTextMessageAsync(socket, buffer, CancellationToken.None);
                if (text is null) break;
                if (text.Length == 0) continue;

                using var document = JsonDocument.Parse(text);
                if (document.RootElement.TryGetProperty("id", out var idElement) &&
                    pending.TryRemove(idElement.GetInt32(), out var completion))
                {
                    completion.TrySetResult(document.RootElement.Clone());
                }
            }
        }
        catch (Exception ex) when (ex is WebSocketException or OperationCanceledException or ObjectDisposedException)
        {
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
        var payload = JsonSerializer.SerializeToUtf8Bytes(new { id, method, @params = parameters ?? new { } });
        await socket.SendAsync(payload, WebSocketMessageType.Text, true, CancellationToken.None);

        var reply = await completion.Task.WaitAsync(TimeSpan.FromSeconds(20));
        if (reply.TryGetProperty("error", out var error))
        {
            throw new InvalidOperationException($"{method} failed: {error.GetProperty("message").GetString()}");
        }

        return reply.GetProperty("result");
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

    public async ValueTask DisposeAsync()
    {
        try
        {
            if (socket.State == WebSocketState.Open)
            {
                await socket.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, null, CancellationToken.None);
            }
        }
        catch (WebSocketException)
        {
        }

        if (reader is not null) await Task.WhenAny(reader, Task.Delay(2000));
        socket.Dispose();
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
