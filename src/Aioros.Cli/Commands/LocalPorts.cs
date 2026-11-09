using System.Net;
using System.Net.Sockets;

namespace Aioros.Cli.Commands;

/// <summary>Whether a port can be listened on at <c>localhost</c>, on IPv4 and, where the machine has it, IPv6.</summary>
public static class LocalPorts
{
    public static bool Free(int port) => Listenable(IPAddress.Loopback, port) && (!Socket.OSSupportsIPv6 || Listenable(IPAddress.IPv6Loopback, port));

    private static bool Listenable(IPAddress address, int port)
    {
        var listener = new TcpListener(address, port);
        try
        {
            listener.Start();
            return true;
        }
        catch (SocketException)
        {
            return false;
        }
        finally
        {
            listener.Stop();
        }
    }
}
