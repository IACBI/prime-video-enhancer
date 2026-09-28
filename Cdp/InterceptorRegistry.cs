using System.Collections.Concurrent;

internal static class InterceptorRegistry
{
    private static readonly ConcurrentDictionary<string, byte> Active = new(StringComparer.Ordinal);

    public static bool TryRegister(string targetUrl) => Active.TryAdd(targetUrl, 0);

    public static bool Remove(string targetUrl) => Active.TryRemove(targetUrl, out _);
}
