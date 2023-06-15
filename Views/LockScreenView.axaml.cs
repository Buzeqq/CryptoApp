using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace CryptoApp.Views;

public partial class LockScreenView : UserControl
{
    public LockScreenView()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }
}