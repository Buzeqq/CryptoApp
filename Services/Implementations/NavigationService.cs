using System;
using CryptoApp.Services.Interfaces;
using CryptoApp.ViewModels;
using ReactiveUI;

namespace CryptoApp.Services.Implementations;

public class NavigationService : ReactiveObject, INavigationService
{
    private ViewModelBase _current;
    private readonly Func<Type, ViewModelBase> _viewModelFactory;

    public ViewModelBase Current
    {
        get => _current;
        private set
        {
            _current = value;
            this.RaisePropertyChanged();
        }
    }

    public NavigationService(Func<Type, ViewModelBase> viewModelFactory)
    {
        _viewModelFactory = viewModelFactory;
    }

    public void NavigateTo<T>() where T : ViewModelBase
    {
        Current = _viewModelFactory.Invoke(typeof(T));
    }
}