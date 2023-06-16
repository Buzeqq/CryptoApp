using CryptoApp.Core.Enums;

namespace CryptoApp.Core.Models;

public record Message(string Payload, MessageType MessageType);
