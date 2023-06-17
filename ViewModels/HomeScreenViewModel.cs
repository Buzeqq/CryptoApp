using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Reactive;
using System.Reactive.Linq;
using System.Security.Cryptography;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using CryptoApp.Models;
using CryptoApp.Repositories.Interfaces;
using CryptoApp.Services.Interfaces;
using ReactiveUI;

namespace CryptoApp.ViewModels;

public class HomeScreenViewModel : ViewModelBase
{
    public IManageableServer Server { get; }
    public IConnectionService ConnectionService { get; }
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

    private string? _message;

    public string? Message
    {
        get => _message;
        set => this.RaiseAndSetIfChanged(ref _message, value);
    }

    public int CipherModeIndex { get; set; }
    public string? IpAddress { get; set; }
    public string? Port { get; set; }
    public string ServerPort { get; }
    public static string HostName => Dns.GetHostName();
    public ReactiveCommand<Unit, Unit> ToggleServerCommand { get; }
    public ReactiveCommand<Unit, Unit> TryToConnectCommand { get; }
    public ReactiveCommand<Unit, Unit> SendCommand { get; }
    public ReactiveCommand<Unit, Unit> AttachFilesCommand { get; }
    public int SelectedServerInterfaceIndex { get; set; }

    public HomeScreenViewModel(IManageableServer server, IConnectionService connectionService, IMessageRepository messageRepository)
    {
        Server = server;
        ServerPort = Server.Port.ToString();
        ConnectionService = connectionService;
        MessageRepository = messageRepository;

        ToggleServerCommand = ReactiveCommand.Create(() =>
        {
            Console.WriteLine(SelectedServerInterfaceIndex);
            Server.Interface = Dns.GetHostEntry(Dns.GetHostName()).AddressList.ElementAt(SelectedServerInterfaceIndex);
            Server.Toggle();
            Listening = Server.IsRunning;
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
            if (!Server.IsRunning) Server.Start();
            
            Connected = await ConnectionService.ConnectAsync(address, port);
        });
    }

    private IObservable<Unit> SendMessage()
    {
        return Observable.StartAsync(async () =>
        {
            ConnectionService.Mode = IndexToMode();
            if (Message is not ("" or null))
            {
                MessageRepository.Add(new Message(HostName, Message));
                await ConnectionService.SendTextMessageAsync(Message);
            }

            if (_attachedFiles is not null && _attachedFiles.Length > 0)
            {
                foreach (var attachedFile in _attachedFiles)
                {
                    await ConnectionService.SendFileAsync(attachedFile);
                }

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
                AllowMultiple = true,
                Filters = new List<FileDialogFilter>
                {
                    new() { Name = "Text files", Extensions = { "txt" } },
                    new() { Name = "All files", Extensions = { "*" }}
                }
            };

            if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                _attachedFiles = await openFileDialog.ShowAsync(desktop.MainWindow);
            }
        });
    }
}