using System.Text.Json;
using CryptoApp.Communication.Interfaces;

namespace CryptoApp.Core.Models;

public record BeginFileMessage(long SizeInBytes, string FileName) : ISerializable
{
    public string Serialize()
    {
        return JsonSerializer.Serialize(this);
    }
}