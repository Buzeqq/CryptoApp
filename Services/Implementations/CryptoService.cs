using System;
using System.IO;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading.Tasks;
using CryptoApp.Communication.Interfaces;
using CryptoApp.Core.Models;
using CryptoApp.Services.Interfaces;

namespace CryptoApp.Services.Implementations;

public class CryptoService : ICryptoService, IDisposable
{
    private Aes Aes { get; }
    public CipherMode Mode { get; set; }

    public CryptoService(Aes aes)
    {
        Aes = aes;
        Aes.Padding = PaddingMode.PKCS7;
        Aes.Mode = CipherMode.CBC;
    }
    
    public async Task<EncryptedPayload> EncryptAsync(byte[] key, string message)
    {
        Aes.Key = key;
        Aes.Mode = Mode;
        Aes.Padding = PaddingMode.PKCS7;
        Aes.GenerateIV();
        
        using var ms = new MemoryStream();
        await using var cs = new CryptoStream(ms, Aes.CreateEncryptor(), CryptoStreamMode.Write);
        await using (var sw = new StreamWriter(cs))
        {
            await sw.WriteAsync(message);
        }

        return new EncryptedPayload(Aes, ms.ToArray());
    }
    
    public async Task<EncryptedPayload> EncryptAsync<T>(byte[] key, T message)
    where T : class, ISerializable
    {
        Aes.Key = key;
        Aes.Mode = Mode;
        Aes.Padding = PaddingMode.PKCS7;
        Aes.GenerateIV();
        
        using var ms = new MemoryStream();
        await using var cs = new CryptoStream(ms, Aes.CreateEncryptor(), CryptoStreamMode.Write);
        await using (var sw = new StreamWriter(cs))
        {
            await sw.WriteAsync(message.Serialize());
        }

        return new EncryptedPayload(Aes, ms.ToArray());
    }

    public async Task<string> DecryptAsync(Message message, byte[] key)
    {
        var encryptedPayload = JsonSerializer.Deserialize<EncryptedPayload>(message.Payload, EncryptedPayload.GetOptions())
            ?? throw new JsonException();

        Aes.Key = key;
        Aes.Mode = encryptedPayload.Algorithm.Mode;
        Aes.Padding = encryptedPayload.Algorithm.Padding;
        Aes.IV = encryptedPayload.Algorithm.IV;

        using var ms = new MemoryStream(encryptedPayload.Payload);
        await using var cs = new CryptoStream(ms, Aes.CreateDecryptor(), CryptoStreamMode.Read);
        using var sr = new StreamReader(cs);
        
        return await sr.ReadToEndAsync();
    }
    
    public async Task<T> DecryptAsync<T>(Message message, byte[] key)
    where T : class, ISerializable
    {
        var encryptedPayload = JsonSerializer.Deserialize<EncryptedPayload>(message.Payload, EncryptedPayload.GetOptions())
            ?? throw new JsonException();

        Aes.Key = key;
        Aes.Mode = encryptedPayload.Algorithm.Mode;
        Aes.Padding = encryptedPayload.Algorithm.Padding;
        Aes.IV = encryptedPayload.Algorithm.IV;

        using var ms = new MemoryStream(encryptedPayload.Payload);
        await using var cs = new CryptoStream(ms, Aes.CreateDecryptor(), CryptoStreamMode.Read);
        using var sr = new StreamReader(cs);
        
        return JsonSerializer.Deserialize<T>(await sr.ReadToEndAsync()) ?? throw new JsonException();
    }
    

    public void Dispose()
    {
        Aes.Dispose();
    }
}