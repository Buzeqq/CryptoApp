using System;
using System.IO;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;

namespace CryptoApp.Core.Utilities;

public static class FileUtilities
{
    public static async Task<string> GetFileCheckSumAsync(string filePath)
    {
        using var sha = SHA512.Create();
        await using var fs = File.OpenRead(filePath);

        var source = new CancellationTokenSource();
        source.CancelAfter(5000);
        return Convert.ToBase64String(await sha.ComputeHashAsync(fs, source.Token));
    }

    public static async Task<string> GetFileCheckSumAsync(byte[] fileContent)
    {
        using var sha = SHA512.Create();
        using var ms = new MemoryStream(fileContent);
        return Convert.ToBase64String(await sha.ComputeHashAsync(ms));
    }
}