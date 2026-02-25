using Microsoft.UI;
using RenoDXCommander.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media;
using RenoDXCommander.Models;
using RenoDXCommander.ViewModels;
using Windows.Storage.Pickers;
using System.Runtime.InteropServices;
using WinRT.Interop;

namespace RenoDXCommander;

public sealed partial class MainWindow : Window
{
    public MainViewModel ViewModel { get; } = new();

    // Sensible default — used on first launch before any saved size exists
    private const int DefaultWidth  = 1280;
    private const int DefaultHeight = 880;

    public MainWindow()
    {
        InitializeComponent();
        AuxInstallService.EnsureInisDir();       // create inis folder on first run
        AuxInstallService.EnsureReShadeStaging(); // copy bundled ReShade DLLs to staging if needed
        Title = "RDXC - RenoDXCommander";
        // Fire-and-forget: check/download Lilium HDR shaders in the background
        _ = ShaderPackService.EnsureLatestAsync(ViewModel.HttpClient);
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

        if (!string.IsNullOrEmpty(prefilledDetected))
        {
            var existing = ViewModel.GetNameMapping(prefilledDetected);
            if (!string.IsNullOrEmpty(existing))
                wikiBox.Text = existing;
        }

        bool isExcluded = !string.IsNullOrEmpty(prefilledDetected) &&
                          ViewModel.IsWikiExcluded(prefilledDetected);

        // Exclusion toggle button — visually distinct, toggleable
        var excludeBtn = new ToggleButton
        {
            Content    = "🚫  Exclude from wiki (show Discord link instead)",
            IsChecked  = isExcluded,
            FontSize   = 12,
            Padding    = new Thickness(10, 6, 10, 6),
            Background = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 30, 14, 30)),
            Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 180, 100, 180)),
            BorderBrush = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 80, 30, 80)),
            HorizontalAlignment = HorizontalAlignment.Stretch,
        };

        var excludeNote = new TextBlock
        {
            Text         = isExcluded
                           ? "✅ Excluded — this game shows a Discord link instead of an install button. Toggle off to resume wiki matching."
                           : "Toggle on to exclude this game from all wiki matching. The card will show a Discord link instead of an install button. Toggle off at any time to restore.",
            TextWrapping = TextWrapping.Wrap,
            FontSize     = 11,
            Foreground   = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 130, 100, 160)),
        };

        excludeBtn.Checked   += (s, e) => excludeNote.Text =
            "✅ Excluded — this game shows a Discord link instead of an install button. Toggle off to resume wiki matching.";
        excludeBtn.Unchecked += (s, e) => excludeNote.Text =
            "Toggle on to exclude this game from all wiki matching. The card will show a Discord link instead of an install button. Toggle off at any time to restore.";

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
        panel.Children.Add(new Border
        {
            Height = 1,
            Background = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 30, 40, 60)),
            Margin = new Thickness(0, 4, 0, 4),
        });
        panel.Children.Add(excludeBtn);
        panel.Children.Add(excludeNote);

        panel.Children.Add(new Border
        {
            Height = 1,
            Background = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 30, 40, 60)),
            Margin = new Thickness(0, 4, 0, 4),
        });

        bool isDcExcluded = !string.IsNullOrEmpty(prefilledDetected) &&
                            ViewModel.IsDcModeExcluded(prefilledDetected);

        var dcExcludeBtn = new ToggleButton
        {
            Content    = "⚙  Exclude from global DC Mode",
            IsChecked  = isDcExcluded,
            FontSize   = 12,
            Padding    = new Thickness(10, 6, 10, 6),
            Background = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 18, 28, 48)),
            Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 100, 160, 200)),
            BorderBrush = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 30, 70, 110)),
            HorizontalAlignment = HorizontalAlignment.Stretch,
        };

        var dcExcludeNote = new TextBlock
        {
            Text         = isDcExcluded
                           ? "✅ Excluded from DC Mode — this game always uses normal file naming (ReShade=dxgi.dll, DC=zzz_display_commander.addon64) regardless of the global DC Mode toggle."
                           : "Toggle on to exclude this game from global DC Mode. It will always use normal naming regardless of the DC Mode toggle.",
            TextWrapping = TextWrapping.Wrap,
            FontSize     = 11,
            Foreground   = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 80, 130, 170)),
        };

        dcExcludeBtn.Checked   += (s, e) => dcExcludeNote.Text =
            "✅ Excluded from DC Mode — this game always uses normal file naming (ReShade=dxgi.dll, DC=zzz_display_commander.addon64) regardless of the global DC Mode toggle.";
        dcExcludeBtn.Unchecked += (s, e) => dcExcludeNote.Text =
            "Toggle on to exclude this game from global DC Mode. It will always use normal naming regardless of the DC Mode toggle.";

        panel.Children.Add(dcExcludeBtn);
        panel.Children.Add(dcExcludeNote);

        panel.Children.Add(new Border
        {
            Height = 1,
            Background = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 30, 40, 60)),
            Margin = new Thickness(0, 4, 0, 4),
        });

        bool isUaExcluded = !string.IsNullOrEmpty(prefilledDetected) &&
                            ViewModel.IsUpdateAllExcluded(prefilledDetected);

        var uaExcludeBtn = new ToggleButton
        {
            Content    = "⬆  Exclude from Update All",
            IsChecked  = isUaExcluded,
            FontSize   = 12,
            Padding    = new Thickness(10, 6, 10, 6),
            Background = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 22, 10, 42)),
            Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 160, 100, 220)),
            BorderBrush = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 80, 30, 140)),
            HorizontalAlignment = HorizontalAlignment.Stretch,
        };

        var uaExcludeNote = new TextBlock
        {
            Text         = isUaExcluded
                           ? "✅ Excluded — this game is skipped by all Update All actions."
                           : "Toggle on to skip this game when using Update All RenoDX, Update All ReShade or Update All DC.",
            TextWrapping = TextWrapping.Wrap,
            FontSize     = 11,
            Foreground   = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 120, 80, 180)),
        };

        uaExcludeBtn.Checked   += (s, e) => uaExcludeNote.Text =
            "✅ Excluded — this game is skipped by all Update All actions.";
        uaExcludeBtn.Unchecked += (s, e) => uaExcludeNote.Text =
            "Toggle on to skip this game when using Update All RenoDX, Update All ReShade or Update All DC.";

        panel.Children.Add(uaExcludeBtn);
        panel.Children.Add(uaExcludeNote);

        panel.Children.Add(new Border
        {
            Height = 1,
            Background = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 30, 40, 60)),
            Margin = new Thickness(0, 4, 0, 4),
        });

        bool isShaderExcluded = !string.IsNullOrEmpty(prefilledDetected) &&
                                ViewModel.IsShaderExcluded(prefilledDetected);

        var shaderExcludeBtn = new ToggleButton
        {
            Content    = "🎨  Exclude from shader management",
            IsChecked  = isShaderExcluded,
            FontSize   = 12,
            Padding    = new Thickness(10, 6, 10, 6),
            Background = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 20, 12, 35)),
            Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 140, 90, 200)),
            BorderBrush = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 70, 25, 120)),
            HorizontalAlignment = HorizontalAlignment.Stretch,
        };

        var shaderExcludeNote = new TextBlock
        {
            Text         = isShaderExcluded
                           ? "✅ Excluded — RDXC will not deploy or manage shaders for this game. Manage your own shaders manually."
                           : "Toggle on to stop RDXC managing shaders for this game. No reshade-shaders folder will be created or removed.",
            TextWrapping = TextWrapping.Wrap,
            FontSize     = 11,
            Foreground   = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 110, 70, 160)),
        };

        shaderExcludeBtn.Checked   += (s, e) => shaderExcludeNote.Text =
            "✅ Excluded — RDXC will not deploy or manage shaders for this game. Manage your own shaders manually.";
        shaderExcludeBtn.Unchecked += (s, e) => shaderExcludeNote.Text =
            "Toggle on to stop RDXC managing shaders for this game. No reshade-shaders folder will be created or removed.";

        panel.Children.Add(shaderExcludeBtn);
        panel.Children.Add(shaderExcludeNote);

        var dlg = new ContentDialog
        {
            Title             = "Overrides",
            Content           = panel,
            PrimaryButtonText = "Save",
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

                // Handle wiki exclusion toggle
                bool nowExcluded = excludeBtn.IsChecked == true;
                if (!string.IsNullOrEmpty(det) && nowExcluded != ViewModel.IsWikiExcluded(det))
                    ViewModel.ToggleWikiExclusion(det);

                // Handle DC Mode exclusion toggle
                bool nowDcExcluded = dcExcludeBtn.IsChecked == true;
                if (!string.IsNullOrEmpty(det) && nowDcExcluded != ViewModel.IsDcModeExcluded(det))
                    ViewModel.ToggleDcModeExclusion(det);

                // Handle Update All exclusion toggle
                bool nowUaExcluded = uaExcludeBtn.IsChecked == true;
                if (!string.IsNullOrEmpty(det) && nowUaExcluded != ViewModel.IsUpdateAllExcluded(det))
                    ViewModel.ToggleUpdateAllExclusion(det);

                // Handle shader exclusion toggle
                bool nowShaderExcluded = shaderExcludeBtn.IsChecked == true;
                if (!string.IsNullOrEmpty(det) && nowShaderExcluded != ViewModel.IsShaderExcluded(det))
                    ViewModel.ToggleShaderExclusion(det);

                // Save name mapping if provided and not excluded
                var key = wikiBox.Text?.Trim();
                if (!nowExcluded && !string.IsNullOrEmpty(det) && !string.IsNullOrEmpty(key))
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

    private void RsIniButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { Tag: GameCardViewModel card }) return;
        if (string.IsNullOrEmpty(card.InstallPath)) return;
        try
        {
            AuxInstallService.CopyRsIni(card.InstallPath);
            card.RsActionMessage = "✅ reshade.ini copied to game folder.";
        }
        catch (Exception ex)
        {
            card.RsActionMessage = $"❌ {ex.Message}";
        }
    }

    private void DcIniButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { Tag: GameCardViewModel card }) return;
        if (string.IsNullOrEmpty(card.InstallPath)) return;
        try
        {
            AuxInstallService.CopyDcIni(card.InstallPath);
            card.DcActionMessage = "✅ DisplayCommander.toml copied to game folder.";
        }
        catch (Exception ex)
        {
            card.DcActionMessage = $"❌ {ex.Message}";
        }
    }

    private void SupportButton_Click(object sender, RoutedEventArgs e)
    {
        _ = Windows.System.Launcher.LaunchUriAsync(
            new Uri("https://discordapp.com/channels/1296187754979528747/1475173660686815374"));
    }

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
            "RenoDXCommander", "logs");
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
        foreach (var b in new[] { FilterDetected, FilterInstalled, FilterNotInstalled, FilterHidden, FilterUnity, FilterUnreal, FilterOther })
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

    private async void InstallRsButton_Click(object sender, RoutedEventArgs e)
    {
        var card = (sender as FrameworkElement)?.Tag as GameCardViewModel;
        if (card == null) return;
        if (string.IsNullOrEmpty(card.InstallPath) || !System.IO.Directory.Exists(card.InstallPath))
        {
            var folder = await PickFolderAsync();
            if (folder == null) return;
            card.InstallPath = folder;
            ViewModel.SaveLibraryPublic();
        }
        await ViewModel.InstallReShadeCommand.ExecuteAsync(card);
    }

    private void UninstallRsButton_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is GameCardViewModel card)
            ViewModel.UninstallReShadeCommand.Execute(card);
    }

    // ── Shaders mode cycle handler ──────────────────────────────────────────

    private void ShadersModeButton_Click(object sender, RoutedEventArgs e)
        => ViewModel.CycleShaderDeployMode();

    // ── Update All handlers ──────────────────────────────────────────────────

    private void UpdateAllButton_Click(object sender, RoutedEventArgs e)
    {
        // The button opens its flyout automatically; nothing extra needed here.
        // (WinUI Button.Flyout opens on click.)
    }

    private async void UpdateAllRenoDx_Click(object sender, RoutedEventArgs e)
        => await ViewModel.UpdateAllRenoDxAsync();

    private async void UpdateAllReShade_Click(object sender, RoutedEventArgs e)
        => await ViewModel.UpdateAllReShadeAsync();

    private async void UpdateAllDc_Click(object sender, RoutedEventArgs e)
        => await ViewModel.UpdateAllDcAsync();

    private async void InstallDcButton_Click(object sender, RoutedEventArgs e)
    {
        var card = (sender as FrameworkElement)?.Tag as GameCardViewModel;
        if (card == null) return;
        if (string.IsNullOrEmpty(card.InstallPath) || !System.IO.Directory.Exists(card.InstallPath))
        {
            var folder = await PickFolderAsync();
            if (folder == null) return;
            card.InstallPath = folder;
            ViewModel.SaveLibraryPublic();
        }
        await ViewModel.InstallDcCommand.ExecuteAsync(card);
    }

    private void UninstallDcButton_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is GameCardViewModel card)
            ViewModel.UninstallDcCommand.Execute(card);
    }

    private void UeExtendedToggle_Click(object sender, RoutedEventArgs e)
    {
        var card = (sender as FrameworkElement)?.Tag as GameCardViewModel;
        if (card == null) return;

        ViewModel.ToggleUeExtended(card);

        // Show toast only when turning UE-Extended ON
        if (card.UseUeExtended)
            ShowUeExtendedToast();
    }

    private DispatcherTimer? _toastTimer;

    private void ShowUeExtendedToast()
    {
        UeExtendedToast.Visibility = Visibility.Visible;
        UeExtendedToast.Opacity    = 1.0;

        _toastTimer?.Stop();
        _toastTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(4) };
        _toastTimer.Tick += (s, e) =>
        {
            _toastTimer.Stop();
            UeExtendedToast.Opacity    = 0.0;
            UeExtendedToast.Visibility = Visibility.Collapsed;
        };
        _toastTimer.Start();
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
        // When IsExternalOnly the ExternalUrl has already been resolved correctly
        // (e.g. forced to Discord by ApplyCardOverrides). Use it directly so a
        // NexusUrl on the underlying mod can't override the intended destination.
        var url = card.IsExternalOnly ? card.ExternalUrl : (card.NexusUrl ?? card.DiscordUrl ?? card.ExternalUrl);
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

        // Build content — use RichTextBlock when there is an embedded clickable link,
        // plain TextBlock otherwise so existing notes are unaffected.
        UIElement content;
        if (!string.IsNullOrEmpty(card.NotesUrl))
        {
            var textColour = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 195, 210, 240));
            var linkColour = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 130, 170, 240));

            var para = new Microsoft.UI.Xaml.Documents.Paragraph();

            // Notes text up to the colon (everything before the link line)
            para.Inlines.Add(new Microsoft.UI.Xaml.Documents.Run
            {
                Text       = card.Notes,
                Foreground = textColour,
                FontSize   = 13,
            });

            // Line break then the clickable hyperlink
            para.Inlines.Add(new Microsoft.UI.Xaml.Documents.LineBreak());
            var link = new Microsoft.UI.Xaml.Documents.Hyperlink
            {
                NavigateUri = new Uri(card.NotesUrl),
                Foreground  = linkColour,
            };
            link.Inlines.Add(new Microsoft.UI.Xaml.Documents.Run
            {
                Text     = card.NotesUrlLabel ?? card.NotesUrl,
                FontSize = 13,
            });
            para.Inlines.Add(link);

            var rtb = new RichTextBlock { IsTextSelectionEnabled = true };
            rtb.Blocks.Add(para);

            content = new ScrollViewer
            {
                Content = rtb,
                MaxHeight = 400, Padding = new Thickness(0, 4, 12, 0),
            };
        }
        else
        {
            content = new ScrollViewer
            {
                Content = new TextBlock
                {
                    Text = card.Notes, TextWrapping = TextWrapping.Wrap,
                    Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 195, 210, 240)),
                    FontSize = 13, LineHeight = 22,
                },
                MaxHeight = 400, Padding = new Thickness(0, 4, 12, 0),
            };
        }

        var dialog = new ContentDialog
        {
            Title           = $"ℹ  {card.GameName}",
            Content         = content,
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
        "RenoDXCommander", "window_main.json");

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
