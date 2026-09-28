using System.Net;
using System.Net.Sockets;

/// <summary>Picks the loopback port Edge is asked to expose its DevTools endpoint on.</summary>
internal static class DebugPort
{
    /// <summary>
    /// The preferred port when nothing is listening on it, otherwise one the
    /// operating system reports as free.
    /// </summary>
    /// <remarks>
    /// The port used to be fixed, and when another program held it the helper
    /// started Edge anyway and then polled that program forever. A free port
    /// cannot be guessed from outside, and the helper is the one that knows it:
    /// the command-line check and the target filter both take it as a parameter.
    /// There is still a window between this call and Edge binding the port, but a
    /// loser of that race now fails the way any port clash does instead of being
    /// silently redirected.
    /// </remarks>
    public static int Choose(int preferred) => IsFree(preferred) ? preferred : AskOperatingSystem();

    public static bool IsFree(int port)
    {
        try
        {
            var listener = new TcpListener(IPAddress.Loopback, port);
            listener.Start();
            listener.Stop();
            return true;
        }
        catch (SocketException)
        {
            return false;
        }
    }

    private static int AskOperatingSystem()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        try
        {
            return ((IPEndPoint)listener.LocalEndpoint).Port;
        }
        finally
        {
            listener.Stop();
        }
    }
}
