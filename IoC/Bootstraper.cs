using System;
using System.IO;
using System.Security.Cryptography;
using CryptoApp.Core.Server;
using CryptoApp.Core.Utilities;
using CryptoApp.IoC.Extensions;
using CryptoApp.Repositories.Implementations;
using CryptoApp.Repositories.Interfaces;
using CryptoApp.Services.Implementations;
using CryptoApp.Services.Interfaces;
using CryptoApp.ViewModels;
using Serilog;
using Splat;
using Splat.Serilog;

namespace CryptoApp.IoC;

public static class Bootstraper
{
    public static void Register(IMutableDependencyResolver services, IReadonlyDependencyResolver resolver)
    {
        // app folders setup
        var downloadsDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".crypto", "downloads");
        Directory.CreateDirectory(downloadsDirectory);
        
        // repositories
        services.RegisterLazySingleton<IMessageRepository>(() => new MessageRepository());
        
        // services
        services.RegisterLazySingleton<IKeyManagingService>(() => new KeyManagingService(
            resolver.GetRequiredService<Serilog.ILogger>()
        ));
        services.RegisterLazySingleton<Func<Type, ViewModelBase>>(() => viewModelType => 
            (ViewModelBase) ((object?) resolver.GetService(viewModelType) 
                             ?? throw new InvalidOperationException($"Failed to resolve object of type {viewModelType}")));
        services.RegisterLazySingleton<INavigationService>(() => new NavigationService(
            resolver.GetRequiredService<Func<Type, ViewModelBase>>()
        ));
        services.RegisterLazySingleton<IConnectionService>(() => new ConnectionService(
            resolver.GetRequiredService<IKeyManagingService>(),
            resolver.GetRequiredService<ICryptoService>(),
            resolver.GetRequiredService<IMessageRepository>(),
            resolver.GetRequiredService<Serilog.ILogger>(),
            resolver.GetRequiredService<IBenchmarkService>()
        ));
        services.Register<ICryptoService>(() => new CryptoService(
            Aes.Create()
        ));
        services.Register<Serilog.ILogger>(() => new LoggerConfiguration()
            .WriteTo.Console()
            .CreateLogger()
        );
        Locator.CurrentMutable.UseSerilogFullLogger();
        services.RegisterLazySingleton<IBenchmarkService>(() => new BenchmarkService());

        // view models
        services.Register(() => new HomeScreenViewModel(
            resolver.GetRequiredService<IManageableServer>(),
            resolver.GetRequiredService<IConnectionService>(),
            resolver.GetRequiredService<IMessageRepository>(),
            resolver.GetRequiredService<IBenchmarkService>(),
            resolver.GetRequiredService<Serilog.ILogger>()
        ));
        services.Register(() => new LockScreenViewModel(
            resolver.GetRequiredService<INavigationService>(),
            resolver.GetRequiredService<IKeyManagingService>()
        ));
        services.Register(() => new MainWindowViewModel(
            resolver.GetRequiredService<INavigationService>()
        ));
        
        // server
        services.RegisterLazySingleton<IManageableServer>(() => new Server(
            NetworkUtilities.GetRandomUnusedPort(),
            resolver.GetRequiredService<IKeyManagingService>(),
            resolver.GetRequiredService<IMessageRepository>(),
            resolver.GetRequiredService<Serilog.ILogger>()
        ) { DownloadsDirectory = downloadsDirectory } );
    }
}