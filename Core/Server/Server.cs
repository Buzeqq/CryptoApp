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
using CryptoApp.Core.Enums;
using CryptoApp.Core.Models;
using CryptoApp.Core.Utilities;
using CryptoApp.Repositories.Interfaces;
using CryptoApp.Services.Implementations;
using CryptoApp.Services.Interfaces;
using FluentAssertions;
using ReactiveUI;

namespace CryptoApp.Core.Server;

public class Server : ReactiveObject, IManageableServer
{
    private readonly IMessageRepository _messageRepository;
    private TcpListener? _server;
    public IPAddress Interface { get; set; }
    public int Port { get; }
    public string DownloadsDirectory { get; init; }
    private static string HostName => Dns.GetHostName();
    private IKeyManagingService KeyManagingService { get; }
    
    private ICryptoService CryptoService { get; }

    private bool _isDownloading;

    public bool IsDownloading
    {
        get => _isDownloading;
        set
        {
            _isDownloading = value;
            this.RaisePropertyChanged();
        }
    }

    private int _downloadPercentProgress;

    public int DownloadPercentProgress
    {
        get => _downloadPercentProgress;
        set
        {
            _downloadPercentProgress = value;
            this.RaisePropertyChanged();
        }
    }

    public Server(int port, IKeyManagingService keyManagingService, IMessageRepository messageRepository)
    {
        Port = port;
        KeyManagingService = keyManagingService;
        _messageRepository = messageRepository;
        CryptoService = new CryptoService(Aes.Create());
    }

    public void Stop()
    {
        _server.Should().NotBeNull();
        _server!.Stop();
        IsRunning = false;
        _server = null;
    }

    public bool IsRunning { get; private set; }

    public async void Start()
    {
        _server = new TcpListener(Interface, Port);
        _server.Start();
        Console.WriteLine($"Listening port: {Interface}:{Port}");
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
        KeyManagingService.SessionKey.Should().NotBeNull();

        var decryptedMessage = await CryptoService.DecryptAsync<BeginFileMessage>(message, KeyManagingService.SessionKey!);
        IsDownloading = true;
        Console.WriteLine($"Upcoming file size: {decryptedMessage.SizeInBytes}, name: {decryptedMessage.FileName}");

        var tmpFilePath = Path.Combine(Path.GetTempPath(), $"CryptoApp-{Guid.NewGuid()}");
        await using var fs = new FileStream(tmpFilePath, FileMode.Create);
        var bytesLeft = decryptedMessage.SizeInBytes;
        var chunkIndex = 0;
        var numberOfChunks = decryptedMessage.NumberOfChunks;
        await foreach (var fileContentPart in ReadFileContent(sr))
        {
            if (fileContentPart.Payload.Length == 0) break;
            await fs.WriteAsync(fileContentPart.Payload, 0, (int)Math.Min(bytesLeft, fileContentPart.Payload.Length));
            DownloadPercentProgress = Math.Clamp((int)((chunkIndex + 1) / (double) numberOfChunks * 100), 0, 100); 
            bytesLeft -= fileContentPart.Payload.Length;
            chunkIndex++;
        }

        var encryptedCheckSumMessage = JsonSerializer.Deserialize<Message>(await sr.ReadLineAsync() ?? string.Empty)
            ?? throw new JsonException();
        var checkSum = await CryptoService.DecryptAsync(encryptedCheckSumMessage, KeyManagingService.SessionKey!);

        var tmp = await FileUtilities.GetFileCheckSumAsync(fs);

        if (checkSum == tmp)
        {
            await sw.WriteLineAsync(JsonSerializer.Serialize(new Message(HostName,"", MessageType.SendingFileSuccess)));
            await sw.FlushAsync();
            var newFilePath = Path.Combine(DownloadsDirectory, decryptedMessage.FileName);
            try
            {
                File.Copy(tmpFilePath, newFilePath);
            }
            catch (IOException)
            {
                File.Delete(newFilePath);
                File.Copy(tmpFilePath, newFilePath);
            }

            await Dispatcher.UIThread.InvokeAsync(() =>
                _messageRepository.Add(new CryptoApp.Models.Message(message.Sender, $"Received new file: {decryptedMessage.FileName}")));
        }
        else
        {
            await sw.WriteLineAsync(JsonSerializer.Serialize(new Message(HostName, "", MessageType.SendingFileFailure)));
        }
        
        File.Delete(tmpFilePath);
        IsDownloading = false;
    }
    
    private async IAsyncEnumerable<SendingFileMessage> ReadFileContent(StreamReader sr)
    {
        KeyManagingService.SessionKey.Should().NotBeNull();
        
        while (JsonSerializer.Deserialize<Message>(await sr.ReadLineAsync() ?? string.Empty) is { } message)
        {
            if (message.MessageType is not MessageType.SendingFile)
            {
                yield return new SendingFileMessage(0, Array.Empty<byte>());
                yield break;
            }
            yield return await CryptoService.DecryptAsync<SendingFileMessage>(message, KeyManagingService.SessionKey!);
        }
    }
    
    private async Task HandleTextMessageAsync(StreamWriter sw, StreamReader sr, Message message)
    {
        KeyManagingService.SessionKey.Should().NotBeNull();
        
        var decryptedMessage = await CryptoService.DecryptAsync(message, KeyManagingService.SessionKey!);
        Dispatcher.UIThread.Post(() => _messageRepository.Add(new CryptoApp.Models.Message(message.Sender, decryptedMessage)));
        Console.WriteLine($"Received message:\n{DateTime.Now}: {decryptedMessage}");
    }

    private async Task HandleKeyExchangeAsync(StreamWriter sw, StreamReader sr, Message? keyMessage = null)
    {
        if (keyMessage is null)
        {
            var keyExchangeString = await sr.ReadLineAsync();
            keyExchangeString.Should().NotBeNull();

            keyMessage = JsonSerializer.Deserialize<Message>(keyExchangeString!);
            keyMessage.Should().NotBeNull();
        }
        

        Console.WriteLine($"Public key received: {keyMessage!.Payload}");
        KeyManagingService.RecipientProvider.FromXmlString(keyMessage.Payload);

        await sw.WriteLineAsync(JsonSerializer.Serialize(new Message(HostName, KeyManagingService.PublicKey, MessageType.KeyExchangeMessageReply)));
        await sw.FlushAsync();
    }

    private async Task HandleSessionKeyAsync(StreamWriter sw, StreamReader sr, Message? sessionKeyMessage = null)
    {
        if (sessionKeyMessage is null)
        {
            var sessionKeyString = await sr.ReadLineAsync();
            sessionKeyString.Should().NotBeNull();
        
            sessionKeyMessage = JsonSerializer.Deserialize<Message>(sessionKeyString!);
            sessionKeyMessage.Should().NotBeNull();
        }

        var sessionKeyEncryptedBytes = Convert.FromBase64String(sessionKeyMessage!.Payload);
        KeyManagingService.SessionKey = KeyManagingService.HostProvider.Decrypt(sessionKeyEncryptedBytes, true);
        Console.WriteLine($"New session key received: {Convert.ToBase64String(KeyManagingService.SessionKey)}");

        await sw.WriteLineAsync(JsonSerializer.Serialize(new Message(HostName,"", MessageType.SessionKeyMessageReceived)));
        await sw.FlushAsync();
    }
}