using System.Security.Cryptography;

namespace CryptoApp.Cryptography;

public class RsaKeyGenerator
{
    public RsaKeyPair GenerateKeyPair(int keySize = 2048)
    {
        using var rsa = new RSACryptoServiceProvider(keySize);
        
        var publicKey = rsa.ExportParameters(false); // Get the public key
        var privateKey = rsa.ExportParameters(true); // Get the private key

        return new RsaKeyPair { PublicKey = publicKey, PrivateKey = privateKey };
    }

    public class RsaKeyPair
    {
        public RSAParameters PublicKey { get; set; }
        public RSAParameters PrivateKey { get; set; }
    }
}
