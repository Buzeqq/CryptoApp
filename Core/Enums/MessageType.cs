namespace CryptoApp.Core.Enums;

public enum MessageType
{
    KeyExchangeMessage = 0,
    KeyExchangeMessageReply,
    SessionKeyMessage,
    SessionKeyMessageReceived,
    TextMessage,
    IsTypingMessage,
    DisconnectedMessage,
    SendingFileBegin,
    SendingFile,
    SendingFileContentEnd,
    SendingFileEnd,
    SendingFileSuccess,
    SendingFileFailure
}