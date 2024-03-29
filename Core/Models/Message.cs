using CryptoApp.Core.Enums;

namespace CryptoApp.Core.Models;

public record Message(string Sender, string Payload, MessageType MessageType);
