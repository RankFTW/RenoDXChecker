using Microsoft.UI;
using RenoDXChecker.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using RenoDXChecker.Models;
using RenoDXChecker.ViewModels;
using Windows.Storage.Pickers;
using System.Runtime.InteropServices;
using WinRT.Interop;

namespace RenoDXChecker;

public sealed partial class MainWindow : Window
{
    public MainViewModel ViewModel { get; } = new();

    // Sensible default — used on first launch before any saved size exists
    private const int DefaultWidth  = 1280;
    private const int DefaultHeight = 880;

    public MainWindow()
    {
        InitializeComponent();
        Title = "RDXC - RenoDXChecker";
        CrashReporter.Log("MainWindow: InitializeComponent complete");
        // Set a sensible default size immediately so the window isn't huge on first launch.
        // TryRestoreWindowBounds (called on Activated) will then override this with the
        // saved size+position from the previous session, if one exists.
        AppWindow.Resize(new Windows.Graphics.SizeInt32(DefaultWidth, DefaultHeight));
        // Restore window size & position after activation (ensure HWND is ready)
        this.Activated += MainWindow_Activated;
        ViewModel.SetDispatcher(DispatcherQueue);
        ViewModel.PropertyChanged += OnViewModelChanged;
        GameCardsList.ItemsSource  = ViewModel.DisplayedGames;
        _ = ViewModel.InitializeAsync();
        this.Closed += MainWindow_Closed;
    }

    private void TuneButton_Click(object sender, RoutedEventArgs e)
    {
        // Get the card this button was invoked from (if it came from a card context menu).
        // The global Tune button passes no context, so we show a generic dialog.
        ShowTuneDialog(prefilledDetected: null);
    }

    private void TuneButton_Card_Click(object sender, RoutedEventArgs e)
    {
        // Per-card Tune — pre-fill the detected game name
        var card = (sender as FrameworkElement)?.Tag as GameCardViewModel;
        ShowTuneDialog(prefilledDetected: card?.GameName);
    }

    private void ShowTuneDialog(string? prefilledDetected)
    {
        var detectedBox = new TextBox
        {
            PlaceholderText = "Detected game name (as shown on the card)",
            Text            = prefilledDetected ?? "",
            Width           = 340,
        };
        var wikiBox = new TextBox
        {
            PlaceholderText = "Exact wiki name (e.g. God of War Ragnarok)",
            Width           = 340,
        };

        // If there's already a mapping for this game, pre-fill the wiki box too
        if (!string.IsNullOrEmpty(prefilledDetected))
        {
            var existing = ViewModel.GetNameMapping(prefilledDetected);
            if (!string.IsNullOrEmpty(existing))
                wikiBox.Text = existing;
        }

        var panel = new StackPanel { Spacing = 10 };
        panel.Children.Add(new TextBlock
        {
            TextWrapping = TextWrapping.Wrap,
            Foreground   = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 120, 140, 180)),
            FontSize     = 12,
            Text         = "If a game isn't matching its wiki mod, enter the detected name " +
                           "(exactly as shown on the card) and the wiki name " +
                           "(exactly as listed on the RenoDX wiki). The app will re-match immediately.",
        });
        panel.Children.Add(new TextBlock { Text = "Detected game name:", FontSize = 12 });
        panel.Children.Add(detectedBox);
        panel.Children.Add(new TextBlock { Text = "Wiki mod name:", FontSize = 12 });
        panel.Children.Add(wikiBox);

        var dlg = new ContentDialog
        {
            Title             = "Name Matching Override",
            Content           = panel,
            PrimaryButtonText = "Save mapping",
            SecondaryButtonText = !string.IsNullOrEmpty(prefilledDetected) &&
                                  !string.IsNullOrEmpty(ViewModel.GetNameMapping(prefilledDetected ?? ""))
                                  ? "Remove mapping" : "",
            CloseButtonText   = "Cancel",
            XamlRoot          = Content.XamlRoot,
            Background        = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 16, 20, 36)),
        };

        _ = dlg.ShowAsync().AsTask().ContinueWith(t =>
        {
            if (t.Result == ContentDialogResult.Primary)
            {
                var det = detectedBox.Text?.Trim();
                var key = wikiBox.Text?.Trim();
                if (!string.IsNullOrEmpty(det) && !string.IsNullOrEmpty(key))
                    ViewModel.AddNameMapping(det, key);
            }
            else if (t.Result == ContentDialogResult.Secondary)
            {
                var det = detectedBox.Text?.Trim() ?? prefilledDetected;
                if (!string.IsNullOrEmpty(det))
                    ViewModel.RemoveNameMapping(det);
            }
        }, TaskScheduler.FromCurrentSynchronizationContext());
    }

    private void MainWindow_Activated(object? sender, WindowActivatedEventArgs e)
    {
        try
        {
            // Only restore once
            this.Activated -= MainWindow_Activated;
            TryRestoreWindowBounds();
        }
        catch { }
    }

    private void MainWindow_Closed(object? sender, WindowEventArgs e)
    {
        SaveWindowBounds();
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
                    // Don't disturb the About panel if it's currently visible
                    if (!_aboutVisible)
                    {
                        LoadingPanel.Visibility = loading ? Visibility.Visible  : Visibility.Collapsed;
                        CardsScroll.Visibility  = loading ? Visibility.Collapsed : Visibility.Visible;
                    }
                    LoadingRing.IsActive = loading;
                    RefreshBtn.IsEnabled = !loading;
                    StatusDot.Fill = new SolidColorBrush(loading
                        ? Windows.UI.Color.FromArgb(255, 180, 160, 100)
                        : Windows.UI.Color.FromArgb(255, 130, 200, 140));
                    break;
                case nameof(ViewModel.StatusText):
                case nameof(ViewModel.SubStatusText):
                    LoadingTitle.Text    = ViewModel.StatusText;
                    LoadingSubtitle.Text = ViewModel.SubStatusText;
                    StatusBarText.Text   = ViewModel.StatusText
                        + (string.IsNullOrEmpty(ViewModel.SubStatusText) ? "" : $"  —  {ViewModel.SubStatusText}");
                    break;
                case nameof(ViewModel.InstalledCount):
                    InstalledCountText.Text = $"{ViewModel.InstalledCount} installed";
                    break;
                case nameof(ViewModel.TotalGames):
                    GameCountText.Text = $"{ViewModel.TotalGames} shown";
                    break;
                case nameof(ViewModel.HiddenCount):
                    HiddenCountText.Text = ViewModel.HiddenCount > 0
                        ? $"· {ViewModel.HiddenCount} hidden" : "";
                    break;
            }
        });
    }

    // ── Header buttons ────────────────────────────────────────────────────────────

    private void RefreshButton_Click(object sender, RoutedEventArgs e)
    {
        CrashReporter.Log("User clicked Refresh");
        _ = ViewModel.InitializeAsync(forceRescan: true);
    }

    private bool _aboutVisible = false;

    private void AboutButton_Click(object sender, RoutedEventArgs e)
    {
        _aboutVisible = true;
        AboutPanel.Visibility  = Visibility.Visible;
        CardsScroll.Visibility = Visibility.Collapsed;
        LoadingPanel.Visibility = Visibility.Collapsed;
    }

    private void OpenLogsFolder_Click(object sender, RoutedEventArgs e)
    {
        var logsDir = System.IO.Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "RenoDXChecker", "logs");
        System.IO.Directory.CreateDirectory(logsDir);
        CrashReporter.Log("User opened logs folder from About panel");
        System.Diagnostics.Process.Start("explorer.exe", logsDir);
    }

    private void OpenDownloadsFolder_Click(object sender, RoutedEventArgs e)
    {
        System.IO.Directory.CreateDirectory(ModInstallService.DownloadCacheDir);
        CrashReporter.Log("User opened downloads cache folder from About panel");
        System.Diagnostics.Process.Start("explorer.exe", ModInstallService.DownloadCacheDir);
    }

    private void AboutBack_Click(object sender, RoutedEventArgs e)
    {
        _aboutVisible = false;
        AboutPanel.Visibility = Visibility.Collapsed;
        // Restore whichever panel was showing before About was opened
        if (ViewModel.IsLoading)
            LoadingPanel.Visibility = Visibility.Visible;
        else
            CardsScroll.Visibility = Visibility.Visible;
    }

    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e) =>
        ViewModel.SearchQuery = SearchBox.Text;

    // ShowHidden toggle removed; Hidden tab shows hidden games by default.

    // ── Manual add game ───────────────────────────────────────────────────────────

    private async void AddGameButton_Click(object sender, RoutedEventArgs e)
    {
        // Ask for game name
        var nameBox = new TextBox { PlaceholderText = "Game name (e.g. Cyberpunk 2077)", Width = 350 };
        var nameDialog = new ContentDialog
        {
            Title           = "➕ Add Game Manually",
            Content         = new StackPanel
            {
                Spacing = 10,
                Children =
                {
                    new TextBlock { Text = "Enter the game name exactly as it appears on the wiki mod list:", TextWrapping = TextWrapping.Wrap, Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 180, 190, 220)) },
                    nameBox
                }
            },
            PrimaryButtonText   = "Pick Folder →",
            CloseButtonText     = "Cancel",
            XamlRoot            = Content.XamlRoot,
            Background          = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 16, 20, 40)),
        };
        var result = await nameDialog.ShowAsync();
        if (result != ContentDialogResult.Primary) return;

        var gameName = nameBox.Text.Trim();
        if (string.IsNullOrEmpty(gameName)) return;
        CrashReporter.Log($"AddGame: {gameName}");

        // Pick the game folder
        var folder = await PickFolderAsync();
        if (folder == null) return;

        var game = new DetectedGame
        {
            Name = gameName, InstallPath = folder, Source = "Manual", IsManuallyAdded = true
        };
        ViewModel.AddManualGameCommand.Execute(game);
    }

    // ── Filter tabs ───────────────────────────────────────────────────────────────

    private void Filter_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button btn) return;
        ViewModel.SetFilterCommand.Execute(btn.Tag as string ?? "Detected");
        var active   = Windows.UI.Color.FromArgb(255, 100, 130, 200);
        var inactive = Windows.UI.Color.FromArgb(255, 22, 27, 44);
        var activeFg   = Colors.White;
        var inactiveFg = Windows.UI.Color.FromArgb(255, 160, 170, 200);
        foreach (var b in new[] { FilterDetected, FilterInstalled, FilterHidden, FilterUnity, FilterUnreal, FilterOther })
        {
            bool isActive = b == btn;
            b.Background  = new SolidColorBrush(isActive ? active   : inactive);
            b.Foreground  = new SolidColorBrush(isActive ? activeFg : inactiveFg);
        }
    }

    // ── Card handlers ─────────────────────────────────────────────────────────────

    private async void InstallButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button btn || btn.Tag is not GameCardViewModel card) return;
        await EnsurePathAndInstall(card, () => ViewModel.InstallModCommand.ExecuteAsync(card));
    }

    private async void Install64Button_Click(object sender, RoutedEventArgs e)
    {
        var card = GetCardFromSender(sender);
        if (card == null) return;
        await EnsurePathAndInstall(card, () => ViewModel.InstallModCommand.ExecuteAsync(card));
    }

    private async void Install32Button_Click(object sender, RoutedEventArgs e)
    {
        var card = GetCardFromSender(sender);
        if (card == null) return;
        await EnsurePathAndInstall(card, () => ViewModel.InstallMod32Command.ExecuteAsync(card));
    }

    private async Task EnsurePathAndInstall(GameCardViewModel card, Func<Task> installAction)
    {
        if (string.IsNullOrEmpty(card.InstallPath) || !System.IO.Directory.Exists(card.InstallPath))
        {
            var folder = await PickFolderAsync();
            if (folder == null) return;
            card.InstallPath = folder;
        }
        await installAction();
    }

    private void UninstallButton_Click(object sender, RoutedEventArgs e)
    {
        if (GetCardFromSender(sender) is { } card)
            ViewModel.UninstallModCommand.Execute(card);
    }

    private void HideButton_Click(object sender, RoutedEventArgs e)
    {
        if (GetCardFromSender(sender) is { } card)
            ViewModel.ToggleHideGameCommand.Execute(card);
    }

    private async void BrowseFolder_Click(object sender, RoutedEventArgs e)
    {
        var card = GetCardFromSender(sender);
        if (card == null) return;
        var folder = await PickFolderAsync();
        if (folder != null) card.InstallPath = folder;
    }

    private void OpenFolder_Click(object sender, RoutedEventArgs e)
    {
        var card = GetCardFromSender(sender);
        if (card == null || string.IsNullOrEmpty(card.InstallPath)) return;
        if (System.IO.Directory.Exists(card.InstallPath))
            System.Diagnostics.Process.Start("explorer.exe", card.InstallPath);
    }

    private void RemoveManualGame_Click(object sender, RoutedEventArgs e)
    {
        var card = GetCardFromSender(sender);
        if (card == null) return;
        ViewModel.RemoveManualGameCommand.Execute(card);
    }

    private async void ExternalLink_Click(object sender, RoutedEventArgs e)
    {
        var card = GetCardFromSender(sender);
        if (card == null) return;
        var url = card.NexusUrl ?? card.DiscordUrl ?? card.ExternalUrl;
        if (!string.IsNullOrEmpty(url))
            await Windows.System.Launcher.LaunchUriAsync(new Uri(url));
    }


    private async void NameLink_Click(object sender, RoutedEventArgs e)
    {
        var card = GetCardFromSender(sender);
        if (card?.NameUrl != null)
            await Windows.System.Launcher.LaunchUriAsync(new Uri(card.NameUrl));
    }


    private async void NotesButton_Click(object sender, RoutedEventArgs e)
    {
        var card = GetCardFromSender(sender);
        if (card == null || string.IsNullOrWhiteSpace(card.Notes)) return;

        var dialog = new ContentDialog
        {
            Title           = $"ℹ  {card.GameName}",
            Content         = new ScrollViewer
            {
                Content = new TextBlock
                {
                    Text = card.Notes, TextWrapping = TextWrapping.Wrap,
                    Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 195, 210, 240)),
                    FontSize = 13, LineHeight = 22,
                },
                MaxHeight = 400, Padding = new Thickness(0, 4, 12, 0),
            },
            CloseButtonText = "Close",
            XamlRoot        = Content.XamlRoot,
            Background      = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 16, 20, 40)),
        };
        await dialog.ShowAsync();
    }

    // ── Helpers ───────────────────────────────────────────────────────────────────

    private static GameCardViewModel? GetCardFromSender(object sender) => sender switch
    {
        Button btn          when btn.Tag  is GameCardViewModel c => c,
        MenuFlyoutItem item when item.Tag is GameCardViewModel c => c,
        _ => null
    };

    private async Task<string?> PickFolderAsync()
    {
        var picker = new FolderPicker { SuggestedStartLocation = PickerLocationId.ComputerFolder };
        picker.FileTypeFilter.Add("*");
        var hwnd = WindowNative.GetWindowHandle(this);
        InitializeWithWindow.Initialize(picker, hwnd);
        var folder = await picker.PickSingleFolderAsync();
        return folder?.Path;
    }

    // --- Window persistence helpers ---
    [DllImport("user32.dll")]
    private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT { public int Left; public int Top; public int Right; public int Bottom; }

    // ── Window persistence (JSON-based, works for unpackaged WinUI 3 apps) ────────
    // ApplicationData.Current.LocalSettings requires package identity and throws in
    // unpackaged apps — so we use a plain JSON file in %LocalAppData% instead.

    private static readonly string _windowSettingsPath = System.IO.Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "RenoDXChecker", "window_main.json");

    private void TryRestoreWindowBounds()
    {
        try
        {
            if (!System.IO.File.Exists(_windowSettingsPath)) return;
            var json = System.IO.File.ReadAllText(_windowSettingsPath);
            var doc  = System.Text.Json.JsonDocument.Parse(json).RootElement;
            if (doc.TryGetProperty("X", out var jx) && doc.TryGetProperty("Y", out var jy) &&
                doc.TryGetProperty("W", out var jw) && doc.TryGetProperty("H", out var jh))
            {
                var x = jx.GetInt32(); var y = jy.GetInt32();
                var w = jw.GetInt32(); var h = jh.GetInt32();
                // Sanity-check: reject obviously bad values
                if (w >= 400 && h >= 300 && w <= 7680 && h <= 4320)
                {
                    var hwnd = WindowNative.GetWindowHandle(this);
                    SetWindowPos(hwnd, IntPtr.Zero, x, y, w, h, 0x0040 /* SWP_NOZORDER */);
                }
            }
        }
        catch { }
    }

    private void SaveWindowBounds()
    {
        try
        {
            var hwnd = WindowNative.GetWindowHandle(this);
            if (!GetWindowRect(hwnd, out var r)) return;
            var w = r.Right - r.Left;
            var h = r.Bottom - r.Top;
            // Don't save minimised/invalid state
            if (w < 100 || h < 100) return;
            System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(_windowSettingsPath)!);
            var json = System.Text.Json.JsonSerializer.Serialize(
                new { X = r.Left, Y = r.Top, W = w, H = h });
            System.IO.File.WriteAllText(_windowSettingsPath, json);
        }
        catch { }
    }
}
