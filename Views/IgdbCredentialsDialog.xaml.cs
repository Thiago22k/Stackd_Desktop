using Microsoft.UI.Xaml.Controls;
using Stackd.Services;

namespace Stackd.Views;

public sealed partial class IgdbCredentialsDialog : ContentDialog
{
    private readonly ISecretsService _secrets;

    public string ClientId => ClientIdBox.Text.Trim();
    public string ClientSecret => ClientSecretBox.Password.Trim();

    public IgdbCredentialsDialog(ISecretsService secrets)
    {
        _secrets = secrets;
        InitializeComponent();

        ClientIdBox.Text = _secrets.TwitchClientId ?? string.Empty;
        ClientSecretBox.Password = _secrets.TwitchClientSecret ?? string.Empty;
    }

    private void OnPrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
    {
        if (string.IsNullOrWhiteSpace(ClientId) || string.IsNullOrWhiteSpace(ClientSecret))
        {
            ErrorText.Text = "Both Client ID and Client Secret are required.";
            ErrorText.Visibility = Microsoft.UI.Xaml.Visibility.Visible;
            args.Cancel = true;
            return;
        }
    }
}

