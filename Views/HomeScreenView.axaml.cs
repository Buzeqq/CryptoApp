using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace CryptoApp.Views;

public partial class HomeScreenView : UserControl
{
    public HomeScreenView()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }
}