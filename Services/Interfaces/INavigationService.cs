using CryptoApp.ViewModels;

namespace CryptoApp.Services.Interfaces;

public interface INavigationService
{
    ViewModelBase Current { get; }
    void NavigateTo<T>() where T : ViewModelBase;
}