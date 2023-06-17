using System;
using System.IO;
using System.Security.Cryptography;
using System.Threading.Tasks;
using CryptoApp.Services.Interfaces;
using Serilog;

namespace CryptoApp.Services.Implementations;

public class KeyManagingService : IKeyManagingService
{
    private readonly ILogger _logger;
    
    private const int SessionKeySize = 256 / 8;
    public string KeyPrefix { get; private set; }

    public string KeyDirectory { get; }

    public string PublicKey => HostProvider.ToXmlString(false);
    
    public byte[]? SessionKey { get; set; }

    public RSACryptoServiceProvider HostProvider { get; private set; }
    public RSACryptoServiceProvider RecipientProvider { get; }

    public string PublicKeyPath => Path.Combine(KeyDirectory, KeyPrefix + "id_rsa.pub");
    public string PrivateKeyPath => Path.Combine(KeyDirectory, "private", KeyPrefix + "id_rsa");

    public KeyManagingService(ILogger logger)
    {
        _logger = logger;
        KeyPrefix = "";
        KeyDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".crypto", "keys");
        HostProvider = new RSACryptoServiceProvider();
        RecipientProvider = new RSACryptoServiceProvider();
    }

    public async Task GenerateNewKeysAsync(string passphrase, string keyPrefix = "")
    {
        var privateKeyDirectory = Path.Combine(KeyDirectory, "private");
        if (!Directory.Exists(privateKeyDirectory))
        {
            Directory.CreateDirectory(privateKeyDirectory);
        }
        KeyPrefix = keyPrefix;
        
        _logger.Information("Generating new RSA key pair...");
        HostProvider = new RSACryptoServiceProvider();
        File.Create(PublicKeyPath).Close();
        await File.WriteAllTextAsync(PublicKeyPath, PublicKey);
        _logger.Information("Public key saved under {PublicKeyPath}", PublicKeyPath);
        
        var privateKey = HostProvider.ExportEncryptedPkcs8PrivateKey(
            passphrase,
            new PbeParameters(PbeEncryptionAlgorithm.Aes256Cbc, HashAlgorithmName.SHA512, 10_000));
        await File.WriteAllBytesAsync(PrivateKeyPath, privateKey);
        _logger.Information("Private key saved under {PrivateKeyPath}", PrivateKeyPath);
    }

    public async Task LoadKeysAsync(string passphrase, string keyPrefix = "")
    {
        KeyPrefix = keyPrefix;

        if (!Directory.Exists(KeyDirectory))
        {
            throw new DirectoryNotFoundException($"Can't find {KeyDirectory}");
        }
        
        if (!File.Exists(PublicKeyPath))
        {
            throw new FileNotFoundException($"Can't find public key {PublicKeyPath}");
        }

        if (!File.Exists(PrivateKeyPath))
        {
            throw new FileNotFoundException($"Can't find private key: {PrivateKeyPath}");
        }

        var privateKey = await File.ReadAllBytesAsync(PrivateKeyPath);
        
        HostProvider.FromXmlString(await File.ReadAllTextAsync(PublicKeyPath));
        HostProvider.ImportEncryptedPkcs8PrivateKey(passphrase, privateKey, out _);
    }

    public void LoadRecipientPublicKey(string publicKey)
    {
        RecipientProvider.FromXmlString(publicKey);
    }

    public bool CanLoadKeys(string keyPrefix = "")
    {
        return File.Exists(Path.Combine(KeyDirectory, keyPrefix + "id_rsa.pub")) &&
               File.Exists(Path.Combine(KeyDirectory, "private", keyPrefix + "id_rsa"));
    }

    public void GenerateSessionKey()
    {
        SessionKey = new byte[SessionKeySize];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(SessionKey);
    }
}