using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using CryptoApp.IoC;
using CryptoApp.IoC.Extensions;
using CryptoApp.ViewModels;
using CryptoApp.Views;
using Splat;

namespace CryptoApp;

public partial class App : Application
{
    
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        Bootstraper.Register(Locator.CurrentMutable, Locator.Current);
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new MainWindow
            {
                DataContext = Locator.Current.GetRequiredService<MainWindowViewModel>()
            };
        }

        base.OnFrameworkInitializationCompleted();
    }
}