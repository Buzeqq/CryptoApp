using System.Security.Cryptography;
using System.Threading.Tasks;
using CryptoApp.Core.Models;

namespace CryptoApp.Services.Interfaces;

public interface ICryptoService
{
    CipherMode Mode { get; set; }
    Task<EncryptedPayload> EncryptAsync(byte[] key, string message);
}