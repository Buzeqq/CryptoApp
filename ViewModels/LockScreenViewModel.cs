using System;
using System.Reactive;
using System.Reactive.Linq;
using CryptoApp.Services.Interfaces;
using ReactiveUI;

namespace CryptoApp.ViewModels;

public class LockScreenViewModel : ViewModelBase
{
    private readonly IKeyManagingService _keyManager;
    private readonly INavigationService _navigationService;
    private bool _foundKeys;
    
    private string _prompt;
    public string Prompt
    {
        get => _prompt;
        set
        {
            _prompt = value;
            this.RaisePropertyChanged();
        }
    }
    
    public string Passphrase { get; set; }
    public ReactiveCommand<Unit, Unit> ValidatePassphraseAsyncCommand { get; }

    private IObservable<Unit> ValidatePassphrase()
    {
        return Observable.StartAsync(async () =>
        {
            if (!_foundKeys)
            {
                await _keyManager.GenerateNewKeysAsync(Passphrase);
                _navigationService.NavigateTo<HomeScreenViewModel>();
                return;
            }
            
            try
            {
                await _keyManager.LoadKeysAsync(Passphrase);
            }
            catch (Exception)
            {
                Prompt = "Bad passphrase. Try again";
                return;
            }
            
            _navigationService.NavigateTo<HomeScreenViewModel>();
        });
    }

    public LockScreenViewModel(INavigationService navigationService, IKeyManagingService keyManager)
    {
        _navigationService = navigationService;
        _keyManager = keyManager;
        _foundKeys = keyManager.CanLoadKeys();

        ValidatePassphraseAsyncCommand = ReactiveCommand.CreateFromObservable(ValidatePassphrase);
        Prompt = _foundKeys ? "Enter passphrase" : "Enter passphrase to generate keys";
    }
}