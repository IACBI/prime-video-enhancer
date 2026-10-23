using System.Collections.Concurrent;
using System.Buffers;
using System.Text;
using System.Text.Json;
using System.Threading.Channels;

/// <summary>One DevTools conversation: a browser-wide connection or a session attached to one tab.</summary>
internal interface ICdpChannel
{
    /// <summary>Sends one protocol message, a serialized JSON object.</summary>
    Task SendAsync(byte[] message);

    /// <summary>The next message, or null once the channel has closed.</summary>
    Task<string?> ReceiveAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// The browser-wide DevTools connection, over Chromium's pipe transport
/// (<c>--remote-debugging-pipe</c>): JSON messages, each terminated by a NUL byte.
/// </summary>
/// <remarks>
/// Tabs are reached through flattened sessions (<c>Target.attachToTarget</c> with
/// <c>flatten</c>), which share this one stream and are told apart by the
/// <c>sessionId</c> field. Messages without one belong to the browser itself.
/// </remarks>
internal sealed class CdpConnection : IAsyncDisposable
{
    private readonly Stream input;
    private readonly Stream output;
    private readonly SemaphoreSlim writeLock = new(1, 1);
    private readonly ConcurrentDictionary<int, TaskCompletionSource<JsonElement>> pending = new();
    private readonly ConcurrentDictionary<string, SessionChannel> sessions = new(StringComparer.Ordinal);
    private int nextId;

    public CdpConnection(Stream input, Stream output)
    {
        this.input = input;
        this.output = output;
        Completion = Task.Run(ReadLoopAsync);
    }

    /// <summary>Completes when the browser closes its end of the pipe, normally because it exited.</summary>
    public Task Completion { get; }

    /// <summary>Sends a browser-level command and returns its <c>result</c>.</summary>
    /// <exception cref="InvalidOperationException">The browser answered with an error.</exception>
    /// <exception cref="IOException">The connection closed before the answer arrived.</exception>
    public async Task<JsonElement> SendAsync(string method, object? parameters = null, CancellationToken cancellationToken = default)
    {
        var id = Interlocked.Increment(ref nextId);
        var answer = new TaskCompletionSource<JsonElement>(TaskCreationOptions.RunContinuationsAsynchronously);
        pending[id] = answer;
        try
        {
            if (Completion.IsCompleted) throw new IOException("The DevTools connection is closed.");
            await WriteAsync(JsonSerializer.SerializeToUtf8Bytes(new { id, method, @params = parameters ?? new { } }));
            var reply = await answer.Task.WaitAsync(cancellationToken);
            if (reply.TryGetProperty("error", out var error))
            {
                var message = error.TryGetProperty("message", out var text) ? text.GetString() : error.GetRawText();
                throw new InvalidOperationException($"{method} failed: {message}");
            }

            return reply.TryGetProperty("result", out var result) ? result : default;
        }
        finally
        {
            pending.TryRemove(id, out _);
        }
    }

    /// <summary>Opens a session on one target. Dispose it to detach.</summary>
    public async Task<SessionChannel> AttachAsync(string targetId, CancellationToken cancellationToken = default)
    {
        var result = await SendAsync("Target.attachToTarget", new { targetId, flatten = true }, cancellationToken);
        var sessionId = result.GetProperty("sessionId").GetString()
            ?? throw new InvalidOperationException("Target.attachToTarget returned no session.");

        // Registered only once the id is known. Nothing addressed to the session
        // can precede this: it sends nothing until its owner enables a domain.
        var session = new SessionChannel(this, sessionId);
        sessions[sessionId] = session;
        if (Completion.IsCompleted) session.Close();
        return session;
    }

    internal async Task WriteAsync(byte[] message)
    {
        await writeLock.WaitAsync();
        try
        {
            // One write per message: a reader on the other end never sees half of
            // one followed by another session's message.
            var framed = ArrayPool<byte>.Shared.Rent(message.Length + 1);
            try
            {
                message.CopyTo(framed, 0);
                framed[message.Length] = 0;
                await output.WriteAsync(framed.AsMemory(0, message.Length + 1));
                await output.FlushAsync();
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(framed);
            }
        }
        finally
        {
            writeLock.Release();
        }
    }

    internal void Forget(string sessionId) => sessions.TryRemove(sessionId, out _);

    private async Task ReadLoopAsync()
    {
        var buffer = new byte[65536];
        var message = new ArrayBufferWriter<byte>(65536);
        try
        {
            while (true)
            {
                var read = await input.ReadAsync(buffer);
                if (read == 0) break;
                Split(buffer.AsSpan(0, read), message);
            }
        }
        catch (Exception ex) when (ex is IOException or ObjectDisposedException)
        {
            // The browser is gone; the same as end of stream.
        }
        finally
        {
            foreach (var session in sessions.Values) session.Close();
            sessions.Clear();
            foreach (var answer in pending.Values) answer.TrySetException(new IOException("The DevTools connection closed."));
        }
    }

    /// <summary>Dispatches every message <paramref name="chunk"/> completes and keeps the unfinished tail.</summary>
    private void Split(ReadOnlySpan<byte> chunk, ArrayBufferWriter<byte> message)
    {
        int terminator;
        while ((terminator = chunk.IndexOf((byte)0)) >= 0)
        {
            message.Write(chunk[..terminator]);
            Dispatch(message.WrittenSpan);
            message.Clear();
            chunk = chunk[(terminator + 1)..];
        }

        message.Write(chunk);
    }

    private void Dispatch(ReadOnlySpan<byte> bytes)
    {
        if (bytes.IsEmpty) return;

        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(bytes.ToArray());
        }
        catch (JsonException)
        {
            return;
        }

        using (document)
        {
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object) return;

            if (root.TryGetProperty("sessionId", out var sessionElement))
            {
                if (sessionElement.GetString() is { } sessionId && sessions.TryGetValue(sessionId, out var session))
                {
                    session.Deliver(Encoding.UTF8.GetString(bytes));
                }

                return;
            }

            if (root.TryGetProperty("id", out var idElement))
            {
                if (idElement.TryGetInt32(out var id) && pending.TryGetValue(id, out var answer))
                {
                    answer.TrySetResult(root.Clone());
                }

                return;
            }

            // The tab closed, crashed or was detached from: its session is over.
            if (root.TryGetProperty("method", out var method) &&
                method.ValueEquals("Target.detachedFromTarget") &&
                root.TryGetProperty("params", out var parameters) &&
                parameters.TryGetProperty("sessionId", out var detached) &&
                detached.GetString() is { } detachedId &&
                sessions.TryRemove(detachedId, out var ended))
            {
                ended.Close();
            }
        }
    }

    public async ValueTask DisposeAsync()
    {
        await output.DisposeAsync();
        await input.DisposeAsync();
        try
        {
            await Completion;
        }
        catch
        {
        }

        writeLock.Dispose();
    }
}

/// <summary>A flattened DevTools session on one tab, multiplexed over a <see cref="CdpConnection"/>.</summary>
internal sealed class SessionChannel : ICdpChannel, IAsyncDisposable
{
    private readonly CdpConnection connection;
    private readonly byte[] prefix;
    private readonly Channel<string> inbox = Channel.CreateUnbounded<string>(new UnboundedChannelOptions { SingleReader = true });

    internal SessionChannel(CdpConnection connection, string sessionId)
    {
        this.connection = connection;
        SessionId = sessionId;
        prefix = Encoding.UTF8.GetBytes("{\"sessionId\":" + JsonSerializer.Serialize(sessionId) + ",");
    }

    public string SessionId { get; }

    public Task SendAsync(byte[] message)
    {
        // Every message the helper sends is a non-empty JSON object, so the
        // session id can be spliced in after its opening brace instead of
        // parsing and re-serializing the whole payload.
        if (message.Length < 2 || message[0] != (byte)'{')
        {
            throw new ArgumentException("A DevTools message must be a JSON object.", nameof(message));
        }

        var routed = new byte[prefix.Length + message.Length - 1];
        prefix.CopyTo(routed, 0);
        message.AsSpan(1).CopyTo(routed.AsSpan(prefix.Length));
        return connection.WriteAsync(routed);
    }

    public async Task<string?> ReceiveAsync(CancellationToken cancellationToken = default)
    {
        while (await inbox.Reader.WaitToReadAsync(cancellationToken))
        {
            if (inbox.Reader.TryRead(out var message)) return message;
        }

        return null;
    }

    internal void Deliver(string message) => inbox.Writer.TryWrite(message);

    internal void Close() => inbox.Writer.TryComplete();

    /// <summary>Detaches from the tab, if the browser is still there to hear it.</summary>
    public async ValueTask DisposeAsync()
    {
        connection.Forget(SessionId);
        Close();
        try
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(2));
            await connection.SendAsync("Target.detachFromTarget", new { sessionId = SessionId }, timeout.Token);
        }
        catch (Exception ex) when (ex is IOException or InvalidOperationException or OperationCanceledException or ObjectDisposedException)
        {
            // Already detached, or the browser is gone.
        }
    }
}
