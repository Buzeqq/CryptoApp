using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading.Channels;
using System.Threading.Tasks;
using CryptoApp.Core.Enums;
using CryptoApp.Core.Models;
using CryptoApp.Core.Utilities;
using CryptoApp.Repositories.Interfaces;
using CryptoApp.Services.Interfaces;
using FluentAssertions;

namespace CryptoApp.Services.Implementations;

public class ConnectionService : IConnectionService, IDisposable
{
    private readonly IKeyManagingService _keyManagingService;
    private readonly ICryptoService _cryptoService;
    private readonly IMessageRepository _messageRepository;
    
    private readonly TcpClient _client;
    private NetworkStream? _networkStream;
    private StreamReader? _sr;
    private StreamWriter? _sw;

    public CipherMode Mode
    {
        get => _cryptoService.Mode;
        set => _cryptoService.Mode = value;
    }

    public async Task<bool> ConnectAsync(IPAddress address, int port)
    {
        try
        {
            await _client.ConnectAsync(address, port);
            _networkStream = _client.GetStream();
            _sr = new StreamReader(_networkStream);
            _sw = new StreamWriter(_networkStream);

            await ExchangeKeysAsync();
            await ExchangeSessionKeyAsync();
        }
        catch (Exception)
        {
            return false;
        }

        return true;
    }

    public async Task SendTextMessageAsync(string message)
    {
        _sw.Should().NotBeNull();
        _sr.Should().NotBeNull();

        _keyManagingService.SessionKey.Should().NotBeNull();
        
        var encryptedPayload = await _cryptoService.EncryptAsync(_keyManagingService.SessionKey!, message);
        var encryptedMessage = new Message(encryptedPayload.Serialize(), MessageType.TextMessage);
        await _sw!.WriteLineAsync(JsonSerializer.Serialize(encryptedMessage));
        await _sw.FlushAsync();
    }

    private async Task ExchangeKeysAsync()
    {
        _sw.Should().NotBeNull();
        _sr.Should().NotBeNull();
        
        await _sw!.WriteLineAsync(
            JsonSerializer.Serialize(new Message(_keyManagingService.PublicKey, MessageType.KeyExchangeMessage)));
        await _sw.FlushAsync();
        
        var jsonReply = await _sr!.ReadLineAsync();
        jsonReply.Should().NotBeNull();

        var message = JsonSerializer.Deserialize<Message>(jsonReply!);
        message.Should().NotBeNull();
        
        Console.WriteLine($"Public key received: {message!.Payload}");
        _keyManagingService.LoadRecipientPublicKey(message.Payload);
    }

    private async Task ExchangeSessionKeyAsync()
    {
        _sw.Should().NotBeNull();
        _sr.Should().NotBeNull();

        if (_keyManagingService.SessionKey is null) _keyManagingService.GenerateSessionKey();
        
        Console.WriteLine($"New session key: {Convert.ToBase64String(_keyManagingService.SessionKey!)}");
        var encryptedSessionKey = _keyManagingService.RecipientProvider.Encrypt(_keyManagingService.SessionKey!, true);
        var sessionKeyString = Convert.ToBase64String(encryptedSessionKey.AsSpan());
        
        await _sw!.WriteLineAsync(JsonSerializer.Serialize(new Message(sessionKeyString, MessageType.SessionKeyMessage)));
        await _sw.FlushAsync();
        
        var receiveConfirmationString = await _sr!.ReadLineAsync();
        receiveConfirmationString.Should().NotBeNull();
        
        var receiveConfirmationMessage = JsonSerializer.Deserialize<Message>(receiveConfirmationString!);
        receiveConfirmationMessage.Should().NotBeNull();
        receiveConfirmationMessage!.MessageType.Should().Be(MessageType.SessionKeyMessageReceived);
        
        Console.WriteLine("Successful session key exchange!");
    }
    
    public async Task SendFileAsync(string path)
    {
        _keyManagingService.SessionKey.Should().NotBeNull();
        File.Exists(path).Should().Be(true);
        
        var fileInfo = new FileInfo(path);
        Console.WriteLine($"File size: {fileInfo.Length}");
        
        var encryptedFileSendBeginMessage = await _cryptoService.EncryptAsync(
            _keyManagingService.SessionKey!, new BeginFileMessage(fileInfo.Length, fileInfo.Name));
        var encryptedFileInfoMessage = new Message(encryptedFileSendBeginMessage.Serialize(), 
            MessageType.SendingFileBegin);
        await _sw!.WriteLineAsync(JsonSerializer.Serialize(encryptedFileInfoMessage));
        await _sw.FlushAsync();
        
        const long bufferSize = 4096;
        var buffer = new byte[bufferSize];
        await using var fs = File.OpenRead(path);

        var checkSumTask = FileUtilities.GetFileCheckSumAsync(path);
        var id = 0;

        int bytesRead;
        while ((bytesRead = await fs.ReadAsync(buffer)) > 0)
        {
            Console.WriteLine(bytesRead);
            var encryptedPayload = await _cryptoService.EncryptAsync(_keyManagingService.SessionKey!, new SendingFileMessage(id++, buffer));
            var encryptedFileContentMessage = new Message(encryptedPayload.Serialize(), MessageType.SendingFile);
            await _sw.WriteLineAsync(JsonSerializer.Serialize(encryptedFileContentMessage));
        }
        await _sw.FlushAsync();
        
        var contentEnd = new Message("", MessageType.SendingFileContentEnd);
        await _sw.WriteLineAsync(JsonSerializer.Serialize(contentEnd));
        await _sw.FlushAsync();

        var checkSum = await checkSumTask;
        var encryptedCheckSum = await _cryptoService.EncryptAsync(_keyManagingService.SessionKey!, checkSum);
        var checkSumMessage = new Message(encryptedCheckSum.Serialize(), MessageType.SendingFileEnd);
        await _sw.WriteLineAsync(JsonSerializer.Serialize(checkSumMessage));
        await _sw.FlushAsync();
        
        var response = await _sr!.ReadLineAsync() ?? throw new ChannelClosedException();
        var responseMessage = JsonSerializer.Deserialize<Message>(response) ?? throw new JsonException();
        
        switch (responseMessage.MessageType)
        {
            case MessageType.SendingFileSuccess:
                Console.WriteLine("Sending file: success!");
                _messageRepository.Add(new Models.Message($"Sending file {fileInfo.Name}: success!"));
                break;
            case MessageType.SendingFileFailure:
                Console.WriteLine("Sending file: failure!");
                _messageRepository.Add(new Models.Message($"Sending file {fileInfo.Name}: failure!"));
                break;
            
            case MessageType.KeyExchangeMessage:
            case MessageType.KeyExchangeMessageReply:
            case MessageType.SessionKeyMessage:
            case MessageType.SessionKeyMessageReceived:
            case MessageType.TextMessage:
            case MessageType.IsTypingMessage:
            case MessageType.DisconnectedMessage:
            case MessageType.SendingFileBegin:
            case MessageType.SendingFile:
            case MessageType.SendingFileEnd:
            case MessageType.SendingFileContentEnd:
            default:
                throw new ArgumentOutOfRangeException();
        }
    }

    public ConnectionService(IKeyManagingService keyManagingService, ICryptoService cryptoService, IMessageRepository messageRepository)
    {
        _keyManagingService = keyManagingService;
        _cryptoService = cryptoService;
        _messageRepository = messageRepository;
        _client = new TcpClient();
    }

    public void Dispose()
    {
        _client.Dispose();
    }
}