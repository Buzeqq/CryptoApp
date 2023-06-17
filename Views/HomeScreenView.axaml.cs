using System.Linq;
using System.Net;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace CryptoApp.Views;

public partial class HomeScreenView : UserControl
{
    public HomeScreenView()
    {
        InitializeComponent();
        var availableServerInterfacesComboBox = this.Find<ComboBox>("availableInterfacesComboBox");
        availableServerInterfacesComboBox.Items = Dns.GetHostEntry(Dns.GetHostName()).AddressList
            .Select(address => address.ToString()).ToList();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }
}