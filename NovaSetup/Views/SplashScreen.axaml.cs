using Avalonia.Controls;
using NovaSetup.Services;

namespace NovaSetup.Views;

public partial class SplashScreen : Window
{
    public SplashScreen()
    {
        InitializeComponent();
        VersionTextBlock.Text = VersionService.GetFullVersionString();
    }

    public void UpdateStatus(string message)
    {
        StatusTextBlock.Text = string.IsNullOrWhiteSpace(message)
            ? "Preparing your setup..."
            : message;
    }
}
