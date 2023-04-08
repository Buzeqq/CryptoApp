using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Media;
using CryptoApp.Models;
using CryptoApp.Networking;
using CryptoApp.Views;

namespace CryptoApp.ViewModels;

public class MainWindowViewModel : ViewModelBase
{
    public ObservableCollection<Peer> Peers { get; init; } = new();

    public Peer User { get; } = new(IPAddress.Parse(Utils.GetAllLocalIPv4(NetworkInterfaceType.Ethernet).FirstOrDefault() ?? string.Empty),
        "Buzeqq", new SolidColorBrush(Colors.Black));
    
    public async Task OpenSettingsView()
    {
        var starTaskWindow = new SettingsWindow();
        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            await starTaskWindow.ShowDialog(desktop.MainWindow);
        }
    }

    public MainWindowViewModel()
    {
        var rand = new Random();
        for (var i = 1; i <= 4; i++)
        {
            var color = (uint)rand.Next(1 << 30) << 2 | (uint)rand.Next(1 << 2);
            var peer = new Peer(
                IPAddress.Parse($"192.168.0.{i}"), 
                $"Peer {i}",
                new SolidColorBrush(Color.FromUInt32(color)));
            Peers.Add(peer);
        }
    }
}
