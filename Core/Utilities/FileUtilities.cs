using System;
using System.IO;
using System.Security.Cryptography;
using System.Threading.Tasks;

namespace CryptoApp.Core.Utilities;

public static class FileUtilities
{
    public static async Task<string> GetFileCheckSum(string filePath)
    {
        using var sha = SHA512.Create();
        await using var fs = File.OpenRead(filePath);

        return Convert.ToBase64String(await sha.ComputeHashAsync(fs));
    }

    public static string GetFileCheckSum(byte[] fileContent)
    {
        using var sha = SHA512.Create();
        return Convert.ToBase64String(sha.ComputeHash(fileContent));
    }
}