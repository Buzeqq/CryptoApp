using System;
using System.Net;
using System.Net.Sockets;

namespace CryptoApp.Core.Utilities;

public static class PortUtilities
{
    public static int GetRandomUnusedPort()
    {
        const int minPortNumber = 1025;
        const int maxPortNumber = IPEndPoint.MaxPort;

        var random = new Random();
        while (true)
        {
            var port = random.Next(minPortNumber, maxPortNumber + 1);
            if (IsPortAvailable(port))
            {
                return port;
            }
        }
    }

    private static bool IsPortAvailable(int port)
    {
        try
        {
            using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            socket.Bind(new IPEndPoint(IPAddress.Loopback, port));
            return true;
        }
        catch (SocketException)
        {
            return false;
        }
    }
}