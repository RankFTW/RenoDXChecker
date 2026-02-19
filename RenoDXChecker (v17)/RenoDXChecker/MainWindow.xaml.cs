using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using RenoDXChecker.ViewModels;
using Windows.Storage.Pickers;

namespace RenoDXChecker;

public sealed partial class MainWindow : Window
{
    public MainViewModel ViewModel { get; } = new();

    public MainWindow()
    {
        InitializeComponent();
        Title = "RenoDX Mod Manager";
        AppWindow.Resize(new Windows.Graphics.SizeInt32(1200, 860));
        ViewModel.PropertyChanged += OnViewModelChanged;
        GameCardsList.ItemsSource = ViewModel.DisplayedGames;
        _ = ViewModel.InitializeAsync();
    }

    // ── ViewModel → UI sync ───────────────────────────────────────────────────────

    private void OnViewModelChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            switch (e.PropertyName)
            {
                case nameof(ViewModel.IsLoading):
                    var loading = ViewModel.IsLoading;
                    LoadingPanel.Visibility = loading ? Visibility.Visible : Visibility.Collapsed;
                    CardsScroll.Visibility  = loading ? Visibility.Collapsed : Visibility.Visible;
                    LoadingRing.IsActive    = loading;
                    RefreshBtn.IsEnabled    = !loading;
                    RescanBtn.IsEnabled     = !loading;
                    StatusDot.Fill = new SolidColorBrush(loading
                        ? Windows.UI.Color.FromArgb(255, 250, 179, 135)
                        : Windows.UI.Color.FromArgb(255, 166, 227, 161));
                    break;

                case nameof(ViewModel.StatusText):
                case nameof(ViewModel.SubStatusText):
                    LoadingTitle.Text    = ViewModel.StatusText;
                    LoadingSubtitle.Text = ViewModel.SubStatusText;
                    StatusBarText.Text   = ViewModel.StatusText
                        + (string.IsNullOrEmpty(ViewModel.SubStatusText)
                            ? "" : $"  —  {ViewModel.SubStatusText}");
                    break;

                case nameof(ViewModel.InstalledCount):
                    InstalledCountText.Text = $"{ViewModel.InstalledCount} installed";
                    break;

                case nameof(ViewModel.TotalGames):
                    GameCountText.Text = $"{ViewModel.TotalGames} shown";
                    break;
            }
        });
    }

    // ── Header buttons ────────────────────────────────────────────────────────────

    private void RefreshButton_Click(object sender, RoutedEventArgs e) =>
        _ = ViewModel.InitializeAsync(forceRescan: false);

    private void RescanButton_Click(object sender, RoutedEventArgs e) =>
        _ = ViewModel.InitializeAsync(forceRescan: true);

    private void AboutButton_Click(object sender, RoutedEventArgs e)
    {
        var about = new AboutWindow();
        about.Activate();
    }

    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e) =>
        ViewModel.SearchQuery = SearchBox.Text;

    // ── Filter tabs ───────────────────────────────────────────────────────────────

    private void Filter_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button btn) return;
        ViewModel.SetFilterCommand.Execute(btn.Tag as string ?? "Detected");

        var activeColor   = Windows.UI.Color.FromArgb(255, 255, 107, 53);
        var inactiveColor = Windows.UI.Color.FromArgb(255, 30, 30, 58);
        var activeFg      = Colors.White;
        var inactiveFg    = Windows.UI.Color.FromArgb(255, 186, 194, 222);

        foreach (var b in new[] { FilterDetected, FilterInstalled })
        {
            bool active = b == btn;
            b.Background = new SolidColorBrush(active ? activeColor : inactiveColor);
            b.Foreground  = new SolidColorBrush(active ? activeFg  : inactiveFg);
        }
    }

    // ── Card handlers ─────────────────────────────────────────────────────────────

    private async void InstallButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button btn || btn.Tag is not GameCardViewModel card) return;
        if (string.IsNullOrEmpty(card.InstallPath) || !System.IO.Directory.Exists(card.InstallPath))
        {
            var folder = await PickFolderAsync();
            if (folder == null) return;
            card.InstallPath = folder;
        }
        await ViewModel.InstallModCommand.ExecuteAsync(card);
    }

    private async void Install64Button_Click(object sender, RoutedEventArgs e)
    {
        var card = GetCardFromSender(sender);
        if (card == null) return;
        if (string.IsNullOrEmpty(card.InstallPath) || !System.IO.Directory.Exists(card.InstallPath))
        {
            var folder = await PickFolderAsync();
            if (folder == null) return;
            card.InstallPath = folder;
        }
        await ViewModel.InstallModCommand.ExecuteAsync(card);
    }

    private async void Install32Button_Click(object sender, RoutedEventArgs e)
    {
        var card = GetCardFromSender(sender);
        if (card == null) return;
        if (string.IsNullOrEmpty(card.InstallPath) || !System.IO.Directory.Exists(card.InstallPath))
        {
            var folder = await PickFolderAsync();
            if (folder == null) return;
            card.InstallPath = folder;
        }
        await ViewModel.InstallMod32Command.ExecuteAsync(card);
    }

    private void UninstallButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is GameCardViewModel card)
            ViewModel.UninstallModCommand.Execute(card);
    }

    private async void BrowseFolder_Click(object sender, RoutedEventArgs e)
    {
        // Called from MenuFlyoutItem — Tag is the card
        var card = GetCardFromSender(sender);
        if (card == null) return;
        var folder = await PickFolderAsync();
        if (folder != null) card.InstallPath = folder;
    }

    private async void SetFolder_Click(object sender, RoutedEventArgs e)
    {
        var card = GetCardFromSender(sender);
        if (card == null) return;
        var folder = await PickFolderAsync();
        if (folder != null)
        {
            card.InstallPath = folder;
            if (card.Status == GameStatus.NotInstalled)
                card.Status = GameStatus.Available;
        }
    }

    private void OpenFolder_Click(object sender, RoutedEventArgs e)
    {
        var card = GetCardFromSender(sender);
        if (card == null || string.IsNullOrEmpty(card.InstallPath)) return;
        if (System.IO.Directory.Exists(card.InstallPath))
            System.Diagnostics.Process.Start("explorer.exe", card.InstallPath);
    }

    private async void ExternalLink_Click(object sender, RoutedEventArgs e)
    {
        var card = GetCardFromSender(sender);
        if (card != null && !string.IsNullOrEmpty(card.ExternalUrl))
            await Windows.System.Launcher.LaunchUriAsync(new Uri(card.ExternalUrl));
    }

    private async void NotesButton_Click(object sender, RoutedEventArgs e)
    {
        var card = GetCardFromSender(sender);
        if (card == null || string.IsNullOrWhiteSpace(card.Notes)) return;

        var dialog = new ContentDialog
        {
            Title = $"ℹ  {card.GameName}",
            Content = new ScrollViewer
            {
                Content = new TextBlock
                {
                    Text         = card.Notes,
                    TextWrapping = TextWrapping.Wrap,
                    Foreground   = new SolidColorBrush(
                        Windows.UI.Color.FromArgb(255, 200, 210, 240)),
                    FontSize   = 13,
                    LineHeight = 22,
                },
                MaxHeight = 380,
                Padding   = new Thickness(0, 4, 12, 0),
            },
            CloseButtonText = "Close",
            XamlRoot        = Content.XamlRoot,
            Background      = new SolidColorBrush(
                Windows.UI.Color.FromArgb(255, 18, 18, 42)),
        };
        await dialog.ShowAsync();
    }

    // ── Helpers ───────────────────────────────────────────────────────────────────

    /// Gets GameCardViewModel from either Button.Tag or MenuFlyoutItem.Tag
    private static GameCardViewModel? GetCardFromSender(object sender) => sender switch
    {
        Button btn             when btn.Tag is GameCardViewModel c  => c,
        MenuFlyoutItem item    when item.Tag is GameCardViewModel c => c,
        _ => null
    };

    private async Task<string?> PickFolderAsync()
    {
        var picker = new FolderPicker();
        picker.SuggestedStartLocation = PickerLocationId.ComputerFolder;
        picker.FileTypeFilter.Add("*");
        var hwnd = Win32Interop.GetWindowFromWindowId(AppWindow.Id);
        WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);
        var folder = await picker.PickSingleFolderAsync();
        return folder?.Path;
    }
}
