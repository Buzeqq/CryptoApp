using System;
using System.IO;
using System.Security.Cryptography;
using System.Threading.Tasks;

namespace CryptoApp.Core.Utilities;

public static class FileUtilities
{
    public static async Task<string> GetFileCheckSumAsync(string filePath)
    {
        using var sha = SHA512.Create();
        await using var fs = File.OpenRead(filePath);
        
        return Convert.ToBase64String(await sha.ComputeHashAsync(fs));
    }

    public static async Task<string> GetFileCheckSumAsync(Stream fileStream)
    {
        using var sha = SHA512.Create();
        fileStream.Position = 0;
        return Convert.ToBase64String(await sha.ComputeHashAsync(fileStream));
    }
}