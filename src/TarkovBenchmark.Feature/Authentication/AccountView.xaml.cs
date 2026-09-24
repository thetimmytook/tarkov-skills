using System.Windows;
using System.Windows.Controls;

namespace TarkovBenchmark.Feature.Authentication;

// The owning dialog controls session lifetime and cancels it before closing.
public partial class AccountView : UserControl
{
    private readonly AccountController controller;

    internal AccountView(AccountController controller)
    {
        InitializeComponent();
        this.controller = controller;
        DataContext = controller;
    }

    private async void SignIn_Click(object sender, RoutedEventArgs e) => await controller.SignInAsync();
    private async void SignOut_Click(object sender, RoutedEventArgs e) => await controller.SignOutAsync();
    private async void Retry_Click(object sender, RoutedEventArgs e) => await controller.RestoreAsync();
    private void Cancel_Click(object sender, RoutedEventArgs e) => controller.CancelSignIn();
}
