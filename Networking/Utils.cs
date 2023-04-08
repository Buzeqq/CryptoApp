using System.Collections.Generic;
using System.Linq;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace CryptoApp.Networking;

public static class Utils
{
    public static IEnumerable<string> GetAllLocalIPv4(NetworkInterfaceType type)
    {
        return (from item in NetworkInterface.GetAllNetworkInterfaces()
            where item.NetworkInterfaceType == type && item.OperationalStatus == OperationalStatus.Up
            from ip in item.GetIPProperties().UnicastAddresses
            where ip.Address.AddressFamily == AddressFamily.InterNetwork
            select ip.Address.ToString()).ToArray();
    }
}