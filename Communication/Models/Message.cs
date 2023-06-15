using CryptoApp.Communication.Enums;

namespace CryptoApp.Communication.Models;

public record Message(string Payload, MessageType MessageType);
