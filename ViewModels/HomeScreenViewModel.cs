using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Reactive;
using System.Reactive.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using CryptoApp.Communication.Interfaces;
using CryptoApp.Models;
using CryptoApp.Repositories.Interfaces;
using CryptoApp.Services.Interfaces;
using DynamicData;
using ReactiveUI;

namespace CryptoApp.ViewModels;

public class HomeScreenViewModel : ViewModelBase
{
    private readonly IManageableServer _server;
    private readonly IConnectionService _connectionService;
    private string[]? _attachedFiles;
    public  IMessageRepository MessageRepository { get; }
    
    private bool _listening;
    public bool Listening
    {
        get => _listening;
        set
        {
            _listening = value;
            this.RaisePropertyChanged();
        }
    }
    
    private bool _connected;
    public bool Connected
    {
        get => _connected;
        set
        {
            _connected = value;
            this.RaisePropertyChanged();
        }
    }

    private string _message;

    public string Message
    {
        get => _message;
        set => this.RaiseAndSetIfChanged(ref _message, value);
    }

    public int CipherModeIndex { get; set; }
    public string? IpAddress { get; set; }
    public string? Port { get; set; }
    public string ServerPort { get; }
    public ReactiveCommand<Unit, Unit> ToggleServerCommand { get; }
    public ReactiveCommand<Unit, Unit> TryToConnectCommand { get; }
    public ReactiveCommand<Unit, Unit> SendCommand { get; }
    public ReactiveCommand<Unit, Unit> AttachFilesCommand { get; }

    public HomeScreenViewModel(IManageableServer server, IConnectionService connectionService, IMessageRepository messageRepository)
    {
        _server = server;
        ServerPort = _server.Port.ToString();
        _connectionService = connectionService;
        MessageRepository = messageRepository;

        ToggleServerCommand = ReactiveCommand.Create(() =>
        {
            _server.Toggle();
            Listening = _server.IsRunning;
        });
        
        TryToConnectCommand = ReactiveCommand.CreateFromObservable(TryToConnect);
        SendCommand = ReactiveCommand.CreateFromObservable(SendMessage);
        AttachFilesCommand = ReactiveCommand.CreateFromObservable(AttachFiles);
    }

    private IObservable<Unit> TryToConnect()
    {
        return Observable.StartAsync(async () =>
        {
            IpAddress = IpAddress?.Trim();
            Port = Port?.Trim();
            
            if (IpAddress is null or "" || Port is null or "") return;
            var address = IPAddress.Parse(IpAddress);
            var port = int.Parse(Port);
            if (!_server.IsRunning) _server.Start();
            
            Connected = await _connectionService.ConnectAsync(address, port);
        });
    }

    private IObservable<Unit> SendMessage()
    {
        return Observable.StartAsync(async () =>
        {
            MessageRepository.Add(new Message(Message));
            _connectionService.Mode = IndexToMode();
            await _connectionService.SendTextMessageAsync(Message);

            if (_attachedFiles is not null && _attachedFiles.Length > 0)
            {
                var sendTasks = _attachedFiles.Select(path => _connectionService.SendFileAsync(path));
                await Task.WhenAll(sendTasks.ToArray());
                _attachedFiles = null;
            }
            
            Message = string.Empty;
        });
    }

    private CipherMode IndexToMode()
    {
        return CipherModeIndex switch
        {
            0 => CipherMode.CBC,
            1 => CipherMode.ECB,
            _ => throw new ArgumentOutOfRangeException(nameof(CipherModeIndex))
        };
    }

    private IObservable<Unit> AttachFiles()
    {
        return Observable.StartAsync(async () =>
        {
            var openFileDialog = new OpenFileDialog
            {
                Title = "Select files to attach",
                Filters = new List<FileDialogFilter>
                {
                    new() { Name = "Text files", Extensions = { "txt" } }
                }
            };

            if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                _attachedFiles = await openFileDialog.ShowAsync(desktop.MainWindow);
            }
        });
    }
}