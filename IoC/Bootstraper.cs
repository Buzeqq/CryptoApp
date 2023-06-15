using System;
using CryptoApp.Communication.Interfaces;
using CryptoApp.Communication.Server;
using CryptoApp.IoC.Extensions;
using CryptoApp.Services.Implementations;
using CryptoApp.Services.Interfaces;
using CryptoApp.ViewModels;
using Splat;

namespace CryptoApp.IoC;

public static class Bootstraper
{
    public static void Register(IMutableDependencyResolver services, IReadonlyDependencyResolver resolver)
    {
        // services
        services.RegisterLazySingleton<IKeyManagingService>(() => new KeyManagingService());
        services.RegisterLazySingleton<Func<Type, ViewModelBase>>(() => viewModelType => 
            (ViewModelBase) ((object?) resolver.GetService(viewModelType) 
                             ?? throw new InvalidOperationException($"Failed to resolve object of type {viewModelType}")));
        services.RegisterLazySingleton<INavigationService>(() => new NavigationService(
            resolver.GetRequiredService<Func<Type, ViewModelBase>>()
        ));
        
        // view models
        services.Register(() => new HomeScreenViewModel(
            resolver.GetRequiredService<IManagableServer>()
        ));
        services.Register(() => new LockScreenViewModel(
            resolver.GetRequiredService<INavigationService>(),
            resolver.GetRequiredService<IKeyManagingService>()
        ));
        services.Register(() => new MainWindowViewModel(
            resolver.GetRequiredService<INavigationService>()
        ));
        
        // server
        services.RegisterLazySingleton<IManagableServer>(() => new Server(
            int.Parse(Environment.GetEnvironmentVariable("CRY_PORT") ?? "20000"),
            resolver.GetRequiredService<IKeyManagingService>()
        ));
    }
}