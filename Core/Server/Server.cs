using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Threading;
using CryptoApp.Communication.Interfaces;
using CryptoApp.Core.Enums;
using CryptoApp.Core.Models;
using CryptoApp.Core.Utilities;
using CryptoApp.Repositories.Interfaces;
using CryptoApp.Services.Implementations;
using CryptoApp.Services.Interfaces;
using FluentAssertions;

namespace CryptoApp.Core.Server;

public class Server : IManageableServer
{
    private readonly IMessageRepository _messageRepository;
    private readonly TcpListener _server;
    public int Port { get; }
    private IKeyManagingService KeyManagingService { get; }
    
    private CryptoService CryptoService { get; }

    public Server(int port, IKeyManagingService keyManagingService, IMessageRepository messageRepository)
    {
        Port = port;
        _server = new TcpListener(IPAddress.Loopback, Port);
        KeyManagingService = keyManagingService;
        _messageRepository = messageRepository;
        CryptoService = new CryptoService(Aes.Create());
    }

    public void Stop()
    {
        _server.Stop();
        IsRunning = false;
    }

    public bool IsRunning { get; private set; }
    public string GetPort()
    {
        return Port.ToString();
    }

    public async void Start()
    {
        _server.Start();
        Console.WriteLine($"Listening port: {Port}");
        IsRunning = true;
        while (true)
        {
            try
            {
                var client = await _server.AcceptTcpClientAsync();
                ThreadPool.QueueUserWorkItem(HandleConnection, client);
            }
            catch (Exception)
            {
                IsRunning = false;
                break;
            }
        }
    }

    private async void HandleConnection(object? state)
    {
        using var client = (TcpClient)state!;
        await using var stream = client.GetStream();
        using var sr = new StreamReader(stream);
        await using var sw = new StreamWriter(stream);

        await HandleKeyExchangeAsync(sw, sr);
        await HandleSessionKeyAsync(sw, sr);
        
        while (true)
        {
            var messageString = await sr.ReadLineAsync();
            if (messageString is "" or null)
            {
                continue;
            }
            
            var message = JsonSerializer.Deserialize<Message>(messageString);
            if (message is null)
            {
                Console.WriteLine("Failed to parse message!");
                break;
            }

            if (message.MessageType is MessageType.DisconnectedMessage)
            {
                Console.WriteLine("Connected client terminated session...");
                break;
            }

            var task = message.MessageType switch
            {
                MessageType.SessionKeyMessage => HandleSessionKeyAsync(sw, sr, message),
                MessageType.KeyExchangeMessage => HandleKeyExchangeAsync(sw, sr, message),
                MessageType.TextMessage => HandleTextMessageAsync(sw, sr, message),
                MessageType.SendingFileBegin => HandleSendFileBeginAsync(sw, sr, message),
                _ => throw new ArgumentOutOfRangeException()
            };
            await task;
        }
    }

    private async Task HandleSendFileBeginAsync(StreamWriter sw, StreamReader sr, Message message)
    {
        if (KeyManagingService.SessionKey is null)
        {
            return;
        }

        var decryptedMessage = await CryptoService.DecryptAsync<BeginFileMessage>(message, KeyManagingService.SessionKey);
        Console.WriteLine($"Upcoming file size: {decryptedMessage.SizeInBytes}, name: {decryptedMessage.FileName}");
        
        await using var ms = new MemoryStream();
        var bytesLeft = decryptedMessage.SizeInBytes;
        await foreach (var fileContentPart in ReadFileContent(sr))
        {
            if (fileContentPart.Payload.Length == 0) break;
            await ms.WriteAsync(fileContentPart.Payload, 0, (int)Math.Min(bytesLeft, fileContentPart.Payload.Length));
            bytesLeft -= fileContentPart.Payload.Length;
        }

        var encryptedCheckSumMessage = JsonSerializer.Deserialize<Message>(await sr.ReadLineAsync() ?? string.Empty)
            ?? throw new JsonException();
        var checkSum = await CryptoService.DecryptAsync(encryptedCheckSumMessage, KeyManagingService.SessionKey);
        var fileContent = ms.ToArray();

        var tmp = FileUtilities.GetFileCheckSum(fileContent);

        if (checkSum == tmp)
        {
            await sw.WriteLineAsync(JsonSerializer.Serialize(new Message("", MessageType.SendingFileSuccess)));
            await sw.FlushAsync();
            await File.WriteAllBytesAsync(decryptedMessage.FileName, fileContent);
        }
        else
        {
            await sw.WriteLineAsync(JsonSerializer.Serialize(new Message("", MessageType.SendingFileFailure)));
        }
    }
    
    private async IAsyncEnumerable<SendingFileMessage> ReadFileContent(StreamReader sr)
    {
        if (KeyManagingService.SessionKey is null)
        {
            yield break;
        }
        
        while (JsonSerializer.Deserialize<Message>(await sr.ReadLineAsync() ?? string.Empty) is { } message)
        {
            if (message.MessageType is not MessageType.SendingFile)
            {
                yield return new SendingFileMessage(0, Array.Empty<byte>());
                yield break;
            }
            yield return await CryptoService.DecryptAsync<SendingFileMessage>(message, KeyManagingService.SessionKey);
        }
    }
    
    private async Task HandleTextMessageAsync(StreamWriter sw, StreamReader sr, Message message)
    {
        KeyManagingService.SessionKey.Should().NotBeNull();

        var decryptedMessage = await CryptoService.DecryptAsync(message, KeyManagingService.SessionKey!);
        Dispatcher.UIThread.Post(() => _messageRepository.Add(new CryptoApp.Models.Message(decryptedMessage)));
        Console.WriteLine($"Received message:\n{DateTime.Now}: {decryptedMessage}");
    }

    private async Task HandleKeyExchangeAsync(StreamWriter sw, StreamReader sr, Message? keyMessage = null)
    {
        if (keyMessage is null)
        {
            var keyExchangeString = await sr.ReadLineAsync();
            if (keyExchangeString is null)
            {
                Console.WriteLine("Failed to read stream!");
                return;
            }

            keyMessage = JsonSerializer.Deserialize<Message>(keyExchangeString);
            if (keyMessage is null)
            {
                Console.WriteLine("Failed to parse json!");
                return;
            }
        }
        

        Console.WriteLine($"Public key received: {keyMessage.Payload}");
        KeyManagingService.RecipientProvider.FromXmlString(keyMessage.Payload);

        await sw.WriteLineAsync(JsonSerializer.Serialize(new Message(KeyManagingService.PublicKey, MessageType.KeyExchangeMessageReply)));
        await sw.FlushAsync();
    }

    private async Task HandleSessionKeyAsync(StreamWriter sw, StreamReader sr, Message? sessionKeyMessage = null)
    {
        if (sessionKeyMessage is null)
        {
            var sessionKeyString = await sr.ReadLineAsync();
            if (sessionKeyString is null)
            {
                Console.WriteLine("Failed to read stream!");
                return;
            }
        
            sessionKeyMessage = JsonSerializer.Deserialize<Message>(sessionKeyString);
            if (sessionKeyMessage is null)
            {
                Console.WriteLine("Failed to parse json!");
                return;
            }
        }

        var sessionKeyEncryptedBytes = Convert.FromBase64String(sessionKeyMessage.Payload);
        KeyManagingService.SessionKey = KeyManagingService.HostProvider.Decrypt(sessionKeyEncryptedBytes, true);
        Console.WriteLine($"New session key received: {Convert.ToBase64String(KeyManagingService.SessionKey)}");

        await sw.WriteLineAsync(JsonSerializer.Serialize(new Message("", MessageType.SessionKeyMessageReceived)));
        await sw.FlushAsync();
    }
}