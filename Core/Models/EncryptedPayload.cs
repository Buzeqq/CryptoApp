using System;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using CryptoApp.Communication.Interfaces;

namespace CryptoApp.Core.Models;

public record EncryptedPayload(SymmetricAlgorithm Algorithm, byte[] Payload) : ISerializable
{
    public string Serialize()
    {
        return JsonSerializer.Serialize(this, GetOptions());
    }

    public static JsonSerializerOptions GetOptions()
    {
        var options = new JsonSerializerOptions();
        options.Converters.Add(new EncryptedPayloadJsonConverter());
        options.Converters.Add(new SymmetricAlgorithmJsonConverter());
        return options;
    }
}

public class EncryptedPayloadJsonConverter : JsonConverter<EncryptedPayload>
{
    public override EncryptedPayload Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        reader.Read();
        reader.Read();
        var algorithm = JsonSerializer.Deserialize<SymmetricAlgorithm>(ref reader, options) ?? throw new JsonException("Expected object for Algorithm");
        
        reader.Read();
        reader.Read();
        if (reader.TokenType != JsonTokenType.String)
        {
            throw new JsonException("Expected string for Payload");
        }

        var encryptedPayload = new EncryptedPayload(algorithm, Convert.FromBase64String(reader.GetString() ?? throw new JsonException()));

        reader.Read();
        return encryptedPayload;
    }

    public override void Write(Utf8JsonWriter writer, EncryptedPayload value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        
        writer.WritePropertyName("Algorithm");
        JsonSerializer.Serialize(writer, value.Algorithm, options);
        writer.WriteString("Payload", Convert.ToBase64String(value.Payload));

        writer.WriteEndObject();
    }
}

public class SymmetricAlgorithmJsonConverter : JsonConverter<SymmetricAlgorithm>
{
    public override SymmetricAlgorithm Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.StartObject)
        {
            throw new JsonException();
        }

        byte[]? iv = null;
        var padding = 0;
        var mode = CipherMode.CBC;
        var keySize = 0;

        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.EndObject)
            {
                var aes = Aes.Create();
                aes.IV = iv ?? throw new JsonException();
                aes.Padding = (PaddingMode)padding;
                aes.Mode = mode;
                aes.KeySize = keySize;

                return aes;
            }

            if (reader.TokenType != JsonTokenType.PropertyName)
            {
                throw new JsonException();
            }

            var propertyName = reader.GetString() ?? throw new JsonException();
            reader.Read();

            switch (propertyName)
            {
                case "IV":
                    iv = Convert.FromBase64String(reader.GetString() ?? throw new JsonException());
                    break;
                case "Padding":
                    padding = reader.GetInt32();
                    break;
                case "Mode":
                    mode = Enum.Parse<CipherMode>(reader.GetString() ?? throw new JsonException());
                    break;
                case "KeySize":
                    keySize = reader.GetInt32();
                    break;
                default:
                    reader.Skip();
                    break;
            }
        }

        throw new JsonException();
    }

    public override void Write(Utf8JsonWriter writer, SymmetricAlgorithm value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        writer.WriteString("IV", Convert.ToBase64String(value.IV));
        writer.WriteNumber("Padding", (int)value.Padding);
        writer.WriteString("Mode", value.Mode.ToString());
        writer.WriteNumber("KeySize", value.KeySize);
        writer.WriteEndObject();
    }
}

