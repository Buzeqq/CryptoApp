using CryptoApp.Services.Interfaces;
using ReactiveUI;

namespace CryptoApp.ViewModels;

public class MainWindowViewModel : ViewModelBase
{
    
    private INavigationService _navigationService;
    public INavigationService NavigationService
    {
        get => _navigationService;
        set
        {
            _navigationService = value;
            this.RaisePropertyChanged();
        }
    }

    public MainWindowViewModel(INavigationService navigationService)
    {
        NavigationService = navigationService;
        NavigationService.NavigateTo<LockScreenViewModel>();
    }
}