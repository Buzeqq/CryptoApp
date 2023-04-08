using System.Net;
using Avalonia.Media;

namespace CryptoApp.Models;

public sealed class Peer
{
    public IPAddress Address { get; }

    public string Username { get; }

    public SolidColorBrush Color { get; }

    public Peer(IPAddress address, string username, SolidColorBrush color)
    {
        Address = address;
        Username = username;
        Color = color;
    }
}