using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Stackd.Services;

namespace Stackd.Views;

public sealed partial class SteamLinkDialog : ContentDialog
{
    private readonly ISteamService _steam;

    public SteamLinkDialog(ISteamService steam, ISecretsService secrets)
    {
        _steam = steam;
        InitializeComponent();

        if (secrets.HasSteamLink)
        {
            Title = "Steam account";
            var who = string.IsNullOrWhiteSpace(secrets.SteamPersonaName) ? secrets.SteamId : secrets.SteamPersonaName;
            IntroText.Text = $"Linked as {who}. Enter a new key and profile to change it, or unlink this account.";
            PrimaryButtonText = "Update";
            SecondaryButtonText = "Unlink";
            CloseButtonText = "Close";
        }
    }

    private async void OnPrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
    {
        // El aplazamiento mantiene el cuadro abierto mientras se valida con Steam.
        var deferral = args.GetDeferral();
        try
        {
            ErrorText.Visibility = Visibility.Collapsed;
            Busy.IsActive = true;
            Busy.Visibility = Visibility.Visible;
            IsPrimaryButtonEnabled = false;

            var result = await _steam.LinkAsync(ApiKeyBox.Password, ProfileBox.Text);
            if (!result.Success)
            {
                ErrorText.Text = result.Error ?? "Couldn't link the account.";
                ErrorText.Visibility = Visibility.Visible;
                args.Cancel = true;
            }
        }
        finally
        {
            Busy.IsActive = false;
            Busy.Visibility = Visibility.Collapsed;
            IsPrimaryButtonEnabled = true;
            deferral.Complete();
        }
    }
}
