using System;
using System.Security.Cryptography;
using System.Text;

namespace CryptoApp.Cryptography;

public class ShaHasher
{
    public string ComputeSha256Hash(string input)
    {
        using var sha256 = SHA256.Create();
        var inputBytes = Encoding.UTF8.GetBytes(input);
        var hashBytes = sha256.ComputeHash(inputBytes);
        
        return Convert.ToBase64String(hashBytes);
    }

    public string ComputeSha512Hash(string input)
    {
        using var sha512 = SHA512.Create();
        var inputBytes = Encoding.UTF8.GetBytes(input);
        var hashBytes = sha512.ComputeHash(inputBytes);
        
        return Convert.ToBase64String(hashBytes);
    }
}