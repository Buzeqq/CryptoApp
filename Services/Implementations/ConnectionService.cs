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
using ReactiveUI;
using Serilog;

namespace CryptoApp.Services.Implementations;

public class ConnectionService : ReactiveObject, IConnectionService, IDisposable
{
    private readonly IKeyManagingService _keyManagingService;
    private readonly ICryptoService _cryptoService;
    private readonly IMessageRepository _messageRepository;
    private readonly ILogger _logger;
    private readonly IBenchmarkService _benchmarkService;
    
    private readonly TcpClient _client;
    private NetworkStream? _networkStream;
    private StreamReader? _sr;
    private StreamWriter? _sw;

    public CipherMode Mode
    {
        get => _cryptoService.Mode;
        set => _cryptoService.Mode = value;
    }

    public string HostName => Dns.GetHostName();

    private bool _isSendingFile;

    public bool IsSendingFile
    {
        get => _isSendingFile;
        set
        {
            _isSendingFile = value;
            this.RaisePropertyChanged();
        }
    }

    private int _percentDoneSendingFile;
    public int PercentDoneSendingFile
    {
        get => _percentDoneSendingFile;
        set
        {
            _percentDoneSendingFile = value;
            this.RaisePropertyChanged();
        }
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
        var encryptedMessage = new Message(HostName, encryptedPayload.Serialize(), MessageType.TextMessage);
        await _sw!.WriteLineAsync(JsonSerializer.Serialize(encryptedMessage));
        await _sw.FlushAsync();
    }

    private async Task ExchangeKeysAsync()
    {
        _sw.Should().NotBeNull();
        _sr.Should().NotBeNull();
        
        await _sw!.WriteLineAsync(
            JsonSerializer.Serialize(new Message(HostName, _keyManagingService.PublicKey, MessageType.KeyExchangeMessage)));
        await _sw.FlushAsync();
        
        var jsonReply = await _sr!.ReadLineAsync();
        jsonReply.Should().NotBeNull();

        var message = JsonSerializer.Deserialize<Message>(jsonReply!);
        message.Should().NotBeNull();
        
        _logger.Information("Public key received: {Payload}", message!.Payload);
        
        _keyManagingService.LoadRecipientPublicKey(message.Payload);
    }

    private async Task ExchangeSessionKeyAsync()
    {
        _sw.Should().NotBeNull();
        _sr.Should().NotBeNull();

        if (_keyManagingService.SessionKey is null) _keyManagingService.GenerateSessionKey();
        
        _logger.Information("New session key: {Base64String}", Convert.ToBase64String(_keyManagingService.SessionKey!));
        var encryptedSessionKey = _keyManagingService.RecipientProvider.Encrypt(_keyManagingService.SessionKey!, true);
        var sessionKeyString = Convert.ToBase64String(encryptedSessionKey.AsSpan());
        
        await _sw!.WriteLineAsync(JsonSerializer.Serialize(new Message(HostName, sessionKeyString, MessageType.SessionKeyMessage)));
        await _sw.FlushAsync();
        
        var receiveConfirmationString = await _sr!.ReadLineAsync();
        receiveConfirmationString.Should().NotBeNull();
        
        var receiveConfirmationMessage = JsonSerializer.Deserialize<Message>(receiveConfirmationString!);
        receiveConfirmationMessage.Should().NotBeNull();
        receiveConfirmationMessage!.MessageType.Should().Be(MessageType.SessionKeyMessageReceived);
        _logger.Information("Session key exchanged successfully");
    }
    
    public async Task SendFileAsync(string path)
    {
        _keyManagingService.SessionKey.Should().NotBeNull();
        File.Exists(path).Should().Be(true);
        IsSendingFile = true;
        const long bufferSize = 1024;
        
        var fileInfo = new FileInfo(path);
        var numberOfChunks = (long) Math.Ceiling((double)fileInfo.Length / bufferSize);
        _logger.Information("Sending file: {FileInfoName}, {FileInfoLength}, in {NumberOfChunks} chunks", 
            fileInfo.Name, fileInfo.Length, numberOfChunks);

        var encryptedFileSendBeginMessage = await _cryptoService.EncryptAsync(
            _keyManagingService.SessionKey!, new BeginFileMessage(fileInfo.Length, fileInfo.Name, numberOfChunks));
        var encryptedFileInfoMessage = new Message(HostName, encryptedFileSendBeginMessage.Serialize(), 
            MessageType.SendingFileBegin);
        await _sw!.WriteLineAsync(JsonSerializer.Serialize(encryptedFileInfoMessage));
        await _sw.FlushAsync();
        
        var buffer = new byte[bufferSize];
        await using var fs = File.OpenRead(path);

        var checkSumTask = FileUtilities.GetFileCheckSumAsync(path);
        var id = 0;

        int bytesRead;
        while ((bytesRead = await fs.ReadAsync(buffer)) > 0)
        {
            var encryptedPayload = await _cryptoService.EncryptAsync(_keyManagingService.SessionKey!, new SendingFileMessage(id++, buffer));
            var encryptedFileContentMessage = new Message(HostName, encryptedPayload.Serialize(), MessageType.SendingFile);
            await _sw.WriteLineAsync(JsonSerializer.Serialize(encryptedFileContentMessage));
            PercentDoneSendingFile = Math.Clamp((int)((double)bytesRead * (id + 1) / fileInfo.Length * 100), 0, 100);
        }
        await _sw.FlushAsync();
        
        var contentEnd = new Message(HostName, "", MessageType.SendingFileContentEnd);
        await _sw.WriteLineAsync(JsonSerializer.Serialize(contentEnd));
        await _sw.FlushAsync();

        var checkSum = await checkSumTask;
        var encryptedCheckSum = await _cryptoService.EncryptAsync(_keyManagingService.SessionKey!, checkSum);
        var checkSumMessage = new Message(HostName, encryptedCheckSum.Serialize(), MessageType.SendingFileEnd);
        await _sw.WriteLineAsync(JsonSerializer.Serialize(checkSumMessage));
        await _sw.FlushAsync();
        
        var response = await _sr!.ReadLineAsync() ?? throw new ChannelClosedException();
        var responseMessage = JsonSerializer.Deserialize<Message>(response) ?? throw new JsonException();
        
        switch (responseMessage.MessageType)
        {
            case MessageType.SendingFileSuccess:
                _benchmarkService.StopTimeBenchmark();
                _logger.Information("Managed to send file in {Result}", _benchmarkService.GetResult());
                _logger.Information("File sent successfully");
                _messageRepository.Add(new Models.Message(HostName, $"Sending file {fileInfo.Name}: success!"));
                break;
            case MessageType.SendingFileFailure:
                _logger.Error("Failed to send file");
                _messageRepository.Add(new Models.Message(HostName, $"Sending file {fileInfo.Name}: failure!"));
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

        IsSendingFile = false;
    }

    public ConnectionService(IKeyManagingService keyManagingService, ICryptoService cryptoService, IMessageRepository messageRepository, ILogger logger, IBenchmarkService benchmarkService)
    {
        _keyManagingService = keyManagingService;
        _cryptoService = cryptoService;
        _messageRepository = messageRepository;
        _logger = logger;
        _benchmarkService = benchmarkService;
        _client = new TcpClient();
    }

    public void Dispose()
    {
        _client.Dispose();
    }
}