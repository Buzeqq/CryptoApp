using System.Reactive;
using CryptoApp.Communication.Interfaces;
using ReactiveUI;

namespace CryptoApp.ViewModels;

public class HomeScreenViewModel : ViewModelBase
{
    private readonly IManagableServer _server;
    private bool _connected = false;

    public bool Connected
    {
        get => _connected;
        set
        {
            _connected = value;
            this.RaisePropertyChanged();
        }
    }

    public ReactiveCommand<Unit, Unit> ToggleServerCommand { get; set; }

    public HomeScreenViewModel(IManagableServer server)
    {
        _server = server;
        ToggleServerCommand = ReactiveCommand.Create(() =>
        {
            _server.Toggle();
            Connected = _server.IsRunning;
        });
    }
}