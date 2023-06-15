using System.Text.Json;
using CryptoApp.Communication.Interfaces;

namespace CryptoApp.Communication.Models;

public record BeginFileMessage(long SizeInBytes, string FileName) : ISerializable
{
    public string Serialize()
    {
        return JsonSerializer.Serialize(this);
    }
}