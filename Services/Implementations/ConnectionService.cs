using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading.Tasks;
using CryptoApp.Core.Enums;
using CryptoApp.Core.Models;
using CryptoApp.Services.Interfaces;
using FluentAssertions;

namespace CryptoApp.Services.Implementations;

public class ConnectionService : IConnectionService, IDisposable
{
    private readonly IKeyManagingService _keyManagingService;
    private readonly ICryptoService _cryptoService;
    
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

    public ConnectionService(IKeyManagingService keyManagingService, ICryptoService cryptoService)
    {
        _keyManagingService = keyManagingService;
        _cryptoService = cryptoService;
        _client = new TcpClient();
    }

    public void Dispose()
    {
        _client.Dispose();
    }
}