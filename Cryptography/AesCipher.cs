using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace CryptoApp.Cryptography;

public class AesCipher
{
    public byte[] Encrypt(string plaintext, byte[] key, byte[] iv)
    {
        using var aes = Aes.Create();
        aes.Key = key;
        aes.IV = iv;

        var encryptor = aes.CreateEncryptor(aes.Key, aes.IV);

        using var ms = new MemoryStream();
        using var cs = new CryptoStream(ms, encryptor, CryptoStreamMode.Write);
        
        var plaintextBytes = Encoding.UTF8.GetBytes(plaintext);
        cs.Write(plaintextBytes, 0, plaintextBytes.Length);
        var ciphertext = ms.ToArray();

        return ciphertext;
    }

    public string Decrypt(byte[] ciphertext, byte[] key, byte[] iv)
    {
        using var aes = Aes.Create();
        aes.Key = key;
        aes.IV = iv;

        var decryptor = aes.CreateDecryptor(aes.Key, aes.IV);

        using var ms = new MemoryStream(ciphertext);
        using var cs = new CryptoStream(ms, decryptor, CryptoStreamMode.Read);
        using var reader = new StreamReader(cs);
        
        var plaintext = reader.ReadToEnd();
        return plaintext;
    }
}
