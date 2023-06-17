using System.Net;
using System.Security.Cryptography;
using System.Threading.Tasks;

namespace CryptoApp.Services.Interfaces;

public interface IConnectionService
{
    Task<bool> ConnectAsync(IPAddress ipAddress, int port);
    Task SendTextMessageAsync(string message);
    Task SendFileAsync(string path);
    CipherMode Mode { get; set; }
    bool IsSendingFile { get; }
    int PercentDoneSendingFile { get; }
}