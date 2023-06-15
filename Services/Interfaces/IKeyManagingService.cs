using System.Security.Cryptography;
using System.Threading.Tasks;

namespace CryptoApp.Services.Interfaces;

public interface IKeyManagingService
{
    RSACryptoServiceProvider HostProvider { get; }
    RSACryptoServiceProvider RecipientProvider { get; }
    string PublicKeyPath { get; }
    string PrivateKeyPath { get; }
    byte[]? SessionKey { get; set; }
    string KeyDirectory { get; }
    string KeyPrefix { get; }
    string PublicKey { get; }
    Task GenerateNewKeysAsync(string passphrase, string keyPrefix = "");
    Task LoadKeysAsync(string passphrase, string keyPrefix = "");
    void LoadRecipientPublicKey(string publicKey);
    bool CanLoadKeys(string keyPrefix = "");
    void GenerateSessionKey();
}