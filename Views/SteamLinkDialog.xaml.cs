using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Stackd.Services;

namespace Stackd.Views;

public sealed partial class SteamLinkDialog : ContentDialog
{
    private readonly ISteamService _steam;
    private CancellationTokenSource? _signInCts;

    public SteamLinkDialog(ISteamService steam, ISecretsService secrets)
    {
        _steam = steam;
        InitializeComponent();

        // Con servidor de Stackd no hace falta ninguna clave: solo el inicio de sesión.
        if (secrets.HasServer)
        {
            AdvancedExpander.Visibility = Visibility.Collapsed;
        }
        else if (!string.IsNullOrWhiteSpace(secrets.SteamApiKey))
        {
            // Ya hay una key guardada: se reutiliza si se deja el campo vacío.
            ApiKeyBox.PlaceholderText = "Saved key (leave empty to keep it)";
            AdvancedExpander.IsExpanded = false;
        }

        if (secrets.HasSteamLink)
        {
            Title = "Steam account";
            var who = string.IsNullOrWhiteSpace(secrets.SteamPersonaName) ? secrets.SteamId : secrets.SteamPersonaName;
            IntroText.Text = $"Linked as {who}. Sign in again to switch accounts, or unlink this one. Your imported games are kept.";
            PrimaryButtonText = "Sign in again";
            SecondaryButtonText = "Unlink";
            CloseButtonText = "Close";
        }
    }

    private async void OnPrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
    {
        // El aplazamiento mantiene el cuadro abierto mientras el usuario inicia sesión en el navegador.
        var deferral = args.GetDeferral();
        _signInCts = new CancellationTokenSource();

        try
        {
            ErrorText.Visibility = Visibility.Collapsed;
            BusyPanel.Visibility = Visibility.Visible;
            IsPrimaryButtonEnabled = false;
            IsSecondaryButtonEnabled = false;
            AdvancedExpander.IsEnabled = false;

            var result = await _steam.SignInWithSteamAsync(ApiKeyBox.Password, _signInCts.Token);

            if (result.Success)
            {
                // Trae Stackd al frente después de que el navegador tuviera el foco.
                App.MainWindow?.Activate();
            }
            else
            {
                ErrorText.Text = result.Error ?? "Couldn't link the account.";
                ErrorText.Visibility = Visibility.Visible;
                args.Cancel = true;
            }
        }
        finally
        {
            BusyPanel.Visibility = Visibility.Collapsed;
            IsPrimaryButtonEnabled = true;
            IsSecondaryButtonEnabled = true;
            AdvancedExpander.IsEnabled = true;
            _signInCts.Dispose();
            _signInCts = null;
            deferral.Complete();
        }
    }

    private void OnCancelSignInClick(object sender, RoutedEventArgs e) => _signInCts?.Cancel();
}
