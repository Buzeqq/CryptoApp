using System.Net;

namespace CryptoApp.Services.Interfaces;

public interface IManageableServer
{
    IPAddress Interface { get; set; }
    int Port { get; }
    void Start();
    void Stop();

    void Toggle()
    {
        if (IsRunning)
        {
            Stop();
        }
        else
        {
            Start();
        }
    }
    bool IsRunning { get; }
    
    bool IsDownloading { get; }
    int DownloadPercentProgress { get; }
}