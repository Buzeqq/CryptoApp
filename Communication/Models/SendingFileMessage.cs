using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using CryptoApp.Communication.Interfaces;

namespace CryptoApp.Communication.Models;

public record SendingFileMessage(long Id, byte[] Payload) : ISerializable
{
    public string Serialize()
    {
        return JsonSerializer.Serialize(this);
    }

    public static JsonSerializerOptions GetOptions()
    {
        var options = new JsonSerializerOptions();
        options.Converters.Add(new SendingFileMessageConverter());
        return options;
    }
}

public class SendingFileMessageConverter : JsonConverter<SendingFileMessage>
{
    public override SendingFileMessage Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        long id = 0;
        byte[] payload = { };

        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.PropertyName)
            {
                var propertyName = reader.GetString();

                switch (propertyName)
                {
                    case "Id":
                    {
                        reader.Read();
                        id = reader.GetInt64();
                        break;
                    }

                    case "Payload":
                    {
                        reader.Read();
                        var base64Payload = reader.GetString();
                        payload = Convert.FromBase64String(base64Payload ?? throw new JsonException());
                        break;
                    }
                }
            }
            else if (reader.TokenType == JsonTokenType.EndObject)
            {
                break;
            }
        }

        return new SendingFileMessage(id, payload ?? throw new JsonException());
    }

    public override void Write(Utf8JsonWriter writer, SendingFileMessage value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();

        writer.WriteNumber("Id", value.Id);
        writer.WriteString("Payload", Convert.ToBase64String(value.Payload));

        writer.WriteEndObject();
    }
}