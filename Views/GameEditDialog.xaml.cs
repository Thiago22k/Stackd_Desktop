using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Stackd.Models;
using Stackd.Services;
using Windows.Storage.Pickers;

namespace Stackd.Views;

public sealed partial class GameEditDialog : ContentDialog
{
    // Carpetas "técnicas" que no sirven como nombre de juego.
    private static readonly HashSet<string> GenericFolders = new(StringComparer.OrdinalIgnoreCase)
    {
        "bin", "bin32", "bin64", "binaries", "win64", "win32", "x64", "x86",
        "game", "release", "retail", "shipping", "client", "app", "windows", "windowsnoeditor"
    };

    private readonly Game? _existing;
    private string _lastGuessedTitle = string.Empty;
    private long? _matchedIgdbId;
    private string? _matchedCoverUrl;

    /// <summary>Juego resultante (solo válido si se pulsó Save).</summary>
    public Game Result { get; private set; } = new();

    public GameEditDialog(Game? existing)
    {
        _existing = existing;
        _matchedIgdbId = existing?.IgdbId;
        _matchedCoverUrl = existing?.CoverUrl;
        InitializeComponent();

        StatusBox.ItemsSource = Enum.GetValues<PlayStatus>();
        StatusBox.SelectedItem = existing?.Status ?? PlayStatus.Backlog;

        if (existing is not null)
        {
            Title = "Edit game";
            SecondaryButtonText = "Delete";

            ExecutableBox.Text = existing.ExecutablePath ?? string.Empty;
            TitleBox.Text = existing.Title;
            var hrs = existing.PlaytimeMinutes / 60;
            var mins = existing.PlaytimeMinutes % 60;
            HoursBox.Value = hrs;
            MinutesBox.Value = mins;
            NotesBox.Text = existing.Notes ?? string.Empty;

            if (existing.LastPlayedAtUtc is { } last)
                LastPlayedPicker.Date = new DateTimeOffset(last.ToLocalTime());

            if (existing.IgdbId is not null)
            {
                IgdbMatchedBadge.Text = "✓ Linked to IGDB";
                IgdbMatchedBadge.Visibility = Visibility.Visible;
            }
        }
    }

    private void OnClearExecutableClick(object sender, RoutedEventArgs e)
    {
        ExecutableBox.Text = string.Empty;
    }

    private async void OnBrowseClick(object sender, RoutedEventArgs e)
    {
        var picker = new FileOpenPicker();
        picker.FileTypeFilter.Add(".exe");

        if (App.MainWindow is { } window)
        {
            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(window);
            WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);
        }

        var file = await picker.PickSingleFileAsync();
        if (file is null) return;

        ExecutableBox.Text = file.Path;

        var current = TitleBox.Text.Trim();
        if (current.Length == 0 || current == _lastGuessedTitle)
        {
            _lastGuessedTitle = GuessTitle(file.Path);
            TitleBox.Text = _lastGuessedTitle;
        }
    }

    private static string GuessTitle(string exePath)
    {
        var dir = new DirectoryInfo(Path.GetDirectoryName(exePath) ?? string.Empty);
        while (dir is { Parent: not null } && GenericFolders.Contains(dir.Name))
            dir = dir.Parent;

        if (dir is { Parent: not null })
            return dir.Name;

        return Path.GetFileNameWithoutExtension(exePath);
    }

    #region Vinculación con IGDB (Inline sin ContentDialog secundario)

    private async void OnMatchIgdbClick(object sender, RoutedEventArgs e)
    {
        IgdbMatchPanel.Visibility = Visibility.Visible;

        var query = TitleBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(query))
        {
            query = _lastGuessedTitle;
        }

        IgdbSearchBox.Text = query;
        if (!string.IsNullOrWhiteSpace(query))
        {
            await ExecuteIgdbSearchAsync(query);
        }
    }

    private void OnCloseMatchPanelClick(object sender, RoutedEventArgs e)
    {
        IgdbMatchPanel.Visibility = Visibility.Collapsed;
    }

    private async void OnIgdbSearchKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == Windows.System.VirtualKey.Enter)
        {
            await ExecuteIgdbSearchAsync(IgdbSearchBox.Text);
        }
    }

    private async void OnIgdbSearchClick(object sender, RoutedEventArgs e)
    {
        await ExecuteIgdbSearchAsync(IgdbSearchBox.Text);
    }

    private async Task ExecuteIgdbSearchAsync(string query)
    {
        var cleanQuery = query.Trim();
        if (string.IsNullOrWhiteSpace(cleanQuery)) return;

        IgdbLoadingRing.IsActive = true;
        IgdbLoadingRing.Visibility = Visibility.Visible;
        IgdbStatusText.Visibility = Visibility.Collapsed;
        IgdbResultsList.ItemsSource = null;

        try
        {
            var secrets = App.GetService<ISecretsService>();
            if (!secrets.HasIgdbAccess)
            {
                await secrets.LoadAsync();
            }

            if (!secrets.HasIgdbAccess)
            {
                IgdbStatusText.Text = "Twitch Client ID / Secret not found in secrets.json. Please configure them in the Explore tab.";
                IgdbStatusText.Visibility = Visibility.Visible;
                return;
            }

            var igdb = App.GetService<IIgdbService>();
            var results = await igdb.SearchGamesAsync(cleanQuery, 12);

            if (results.Count == 0)
            {
                IgdbStatusText.Text = "No matching games found on IGDB.";
                IgdbStatusText.Visibility = Visibility.Visible;
            }
            else
            {
                IgdbResultsList.ItemsSource = results;
            }
        }
        catch (Exception ex)
        {
            IgdbStatusText.Text = $"Error: {ex.Message}";
            IgdbStatusText.Visibility = Visibility.Visible;
        }
        finally
        {
            IgdbLoadingRing.IsActive = false;
            IgdbLoadingRing.Visibility = Visibility.Collapsed;
        }
    }

    private void OnIgdbResultItemClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is not IgdbGame selected) return;

        TitleBox.Text = selected.Name;
        _matchedIgdbId = selected.Id;
        _matchedCoverUrl = selected.CoverUrl;

        if (string.IsNullOrWhiteSpace(NotesBox.Text) && !string.IsNullOrWhiteSpace(selected.Summary))
        {
            NotesBox.Text = selected.Summary;
        }

        IgdbMatchedBadge.Text = $"✓ Linked to IGDB: {selected.Name}";
        IgdbMatchedBadge.Visibility = Visibility.Visible;
        IgdbMatchPanel.Visibility = Visibility.Collapsed;
    }

    #endregion

    private void OnPrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
    {
        var title = TitleBox.Text.Trim();
        if (title.Length == 0)
        {
            ErrorText.Text = "The title is required.";
            ErrorText.Visibility = Visibility.Visible;
            args.Cancel = true;
            return;
        }

        var hours = double.IsNaN(HoursBox.Value) ? 0 : (int)Math.Max(HoursBox.Value, 0);
        var minutes = double.IsNaN(MinutesBox.Value) ? 0 : (int)Math.Clamp(MinutesBox.Value, 0, 59);
        var totalMinutes = (hours * 60) + minutes;
        var exe = ExecutableBox.Text.Trim();
        var notes = NotesBox.Text.Trim();
        var status = (PlayStatus)(StatusBox.SelectedItem ?? PlayStatus.Backlog);

        DateTime? lastPlayedUtc = null;
        if (LastPlayedPicker.Date is { } date)
            lastPlayedUtc = DateTime.SpecifyKind(date.Date, DateTimeKind.Local).ToUniversalTime();
        else if (status == PlayStatus.Playing || totalMinutes > 0)
            lastPlayedUtc = DateTime.UtcNow;

        Result = new Game
        {
            Id = _existing?.Id ?? 0,
            IgdbId = _matchedIgdbId ?? _existing?.IgdbId,
            SteamAppId = _existing?.SteamAppId,
            CoverUrl = _matchedCoverUrl ?? _existing?.CoverUrl,
            AddedAtUtc = _existing?.AddedAtUtc ?? DateTime.UtcNow,
            AchievementsUnlocked = _existing?.AchievementsUnlocked ?? 0,
            AchievementsTotal = _existing?.AchievementsTotal ?? 0,

            Title = title,
            ExecutablePath = exe.Length == 0 ? null : exe,
            Status = status,
            PlaytimeMinutes = totalMinutes,
            LastPlayedAtUtc = lastPlayedUtc,
            Notes = notes.Length == 0 ? null : notes
        };
    }
}
