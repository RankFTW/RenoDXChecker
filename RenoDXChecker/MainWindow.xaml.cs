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
using System.Text.RegularExpressions;
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
        ViewModel.ConfirmForeignDxgiOverwrite = ShowForeignDxgiConfirmDialogAsync;
        ViewModel.PropertyChanged += OnViewModelChanged;
        GameCardsList.ItemsSource  = ViewModel.DisplayedGames;
        _ = ViewModel.InitializeAsync();
        // Silent update check — runs in background, shows dialog only if update found
        _ = CheckForAppUpdateAsync();
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

        var excludeBtn = new ToggleButton
        {
            Content    = "🚫  Exclude from wiki",
            IsChecked  = isExcluded,
            FontSize   = 12,
            Padding    = new Thickness(10, 6, 10, 6),
            Background = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 30, 14, 30)),
            Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 180, 100, 180)),
            BorderBrush = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 80, 30, 80)),
            HorizontalAlignment = HorizontalAlignment.Stretch,
        };
        ToolTipService.SetToolTip(excludeBtn,
            "Exclude this game from all wiki matching. The card will show a Discord link instead of an install button.");

        var panel = new StackPanel { Spacing = 8 };
        panel.Children.Add(new TextBlock
        {
            TextWrapping = TextWrapping.Wrap,
            Foreground   = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 120, 140, 180)),
            FontSize     = 12,
            Text         = "Map a detected game name to a wiki mod name for correct matching.",
        });
        panel.Children.Add(new TextBlock { Text = "Detected game name:", FontSize = 12 });
        panel.Children.Add(detectedBox);
        panel.Children.Add(new TextBlock { Text = "Wiki mod name:", FontSize = 12 });
        panel.Children.Add(wikiBox);
        panel.Children.Add(MakeSeparator());
        panel.Children.Add(excludeBtn);

        panel.Children.Add(MakeSeparator());

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
        ToolTipService.SetToolTip(dcExcludeBtn,
            "This game will always use normal file naming (ReShade=dxgi.dll, DC=addon) regardless of the global DC Mode toggle.");

        panel.Children.Add(dcExcludeBtn);

        panel.Children.Add(MakeSeparator());

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
        ToolTipService.SetToolTip(uaExcludeBtn,
            "Skip this game when using Update All RenoDX, Update All ReShade, or Update All DC.");

        panel.Children.Add(uaExcludeBtn);

        panel.Children.Add(MakeSeparator());

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
        ToolTipService.SetToolTip(shaderExcludeBtn,
            "RDXC will not deploy or remove shaders for this game. Manage your own shaders manually.");

        panel.Children.Add(shaderExcludeBtn);

        panel.Children.Add(MakeSeparator());

        bool is32Bit = !string.IsNullOrEmpty(prefilledDetected) &&
                       ViewModel.Is32BitGame(prefilledDetected);

        var bit32Btn = new ToggleButton
        {
            Content    = "⚠  32-bit mode",
            IsChecked  = is32Bit,
            FontSize   = 12,
            Padding    = new Thickness(10, 6, 10, 6),
            Background = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 28, 14, 8)),
            Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 200, 120, 60)),
            BorderBrush = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 120, 50, 20)),
            HorizontalAlignment = HorizontalAlignment.Stretch,
        };
        ToolTipService.SetToolTip(bit32Btn,
            "Installs 32-bit versions of ReShade, Unity addon, and Display Commander. Only enable if you know this game is 32-bit.");

        panel.Children.Add(bit32Btn);

        var dlg = new ContentDialog
        {
            Title             = "Overrides",
            Content           = new ScrollViewer
            {
                Content = panel,
                MaxHeight = 480,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                Padding = new Thickness(0, 0, 8, 0),
            },
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

                // Handle 32-bit mode toggle
                bool now32Bit = bit32Btn.IsChecked == true;
                if (!string.IsNullOrEmpty(det) && now32Bit != ViewModel.Is32BitGame(det))
                    ViewModel.Toggle32Bit(det);

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

    private async Task<bool> ShowForeignDxgiConfirmDialogAsync(GameCardViewModel card, string dxgiPath)
    {
        var fileSize = new System.IO.FileInfo(dxgiPath).Length;
        var sizeKB   = fileSize / 1024.0;

        var dlg = new ContentDialog
        {
            Title               = "⚠ Unknown dxgi.dll Detected",
            Content             = new TextBlock
            {
                TextWrapping = TextWrapping.Wrap,
                Foreground   = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 220, 180, 120)),
                FontSize     = 13,
                Text         = $"A dxgi.dll file was found in:\n{card.InstallPath}\n\n" +
                               $"File size: {sizeKB:N0} KB\n\n" +
                               "RDXC cannot identify this file as ReShade or Display Commander. " +
                               "It may belong to another mod (e.g. DXVK, Special K, ENB).\n\n" +
                               "Overwriting it may break the existing mod. Do you want to proceed?",
            },
            PrimaryButtonText   = "Overwrite",
            CloseButtonText     = "Cancel",
            XamlRoot            = Content.XamlRoot,
            Background          = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 30, 20, 10)),
        };

        var result = await dlg.ShowAsync();
        return result == ContentDialogResult.Primary;
    }

    // ── Auto-Update ────────────────────────────────────────────────────────────

    private async Task CheckForAppUpdateAsync()
    {
        try
        {
            // Wait until the XamlRoot is available (window needs to be fully loaded for dialogs)
            while (Content.XamlRoot == null)
                await Task.Delay(200);

            var updateInfo = await UpdateService.CheckForUpdateAsync(ViewModel.HttpClient);
            if (updateInfo == null) return; // up to date or check failed

            // Show update dialog on UI thread
            DispatcherQueue.TryEnqueue(async () =>
            {
                await ShowUpdateDialogAsync(updateInfo);
            });
        }
        catch (Exception ex)
        {
            CrashReporter.Log($"MainWindow: update check error — {ex.Message}");
        }
    }

    private async Task ShowUpdateDialogAsync(UpdateInfo updateInfo)
    {
        var dlg = new ContentDialog
        {
            Title   = "🔄 Update Available",
            Content = new StackPanel
            {
                Spacing = 8,
                Children =
                {
                    new TextBlock
                    {
                        TextWrapping = TextWrapping.Wrap,
                        Foreground   = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 180, 220, 255)),
                        FontSize     = 14,
                        Text         = $"A new version of RDXC is available!\n\n" +
                                       $"Installed:  v{updateInfo.CurrentVersion}\n" +
                                       $"Available:  v{updateInfo.RemoteVersion}\n\n" +
                                       "Would you like to update now?",
                    },
                },
            },
            PrimaryButtonText   = "Update Now",
            CloseButtonText     = "Later",
            XamlRoot            = Content.XamlRoot,
            Background          = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 20, 20, 35)),
        };

        var result = await dlg.ShowAsync();
        if (result != ContentDialogResult.Primary) return; // user chose "Later"

        // User chose "Update Now" — show downloading dialog
        await DownloadAndInstallUpdateAsync(updateInfo);
    }

    private async Task DownloadAndInstallUpdateAsync(UpdateInfo updateInfo)
    {
        // Create a non-dismissable progress dialog
        var progressText = new TextBlock
        {
            Text         = "Starting download...",
            TextWrapping = TextWrapping.Wrap,
            Foreground   = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 180, 220, 255)),
            FontSize     = 13,
        };
        var progressBar = new ProgressBar
        {
            Minimum = 0,
            Maximum = 100,
            Value   = 0,
            Height  = 6,
            IsIndeterminate = false,
        };
        var downloadDlg = new ContentDialog
        {
            Title   = "⬇ Downloading Update",
            Content = new StackPanel
            {
                Spacing = 12,
                Children = { progressText, progressBar },
            },
            XamlRoot   = Content.XamlRoot,
            Background = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 20, 20, 35)),
            // No buttons — dialog will be closed programmatically when download completes
        };

        // Show dialog non-blocking
        var dialogTask = downloadDlg.ShowAsync();

        var progress = new Progress<(string msg, double pct)>(p =>
        {
            DispatcherQueue.TryEnqueue(() =>
            {
                progressText.Text = p.msg;
                progressBar.Value = p.pct;
            });
        });

        var installerPath = await UpdateService.DownloadInstallerAsync(
            ViewModel.HttpClient, updateInfo.DownloadUrl, progress);

        if (string.IsNullOrEmpty(installerPath))
        {
            // Download failed — update dialog to show error with a Close button
            DispatcherQueue.TryEnqueue(() =>
            {
                progressText.Text = "❌ Download failed. Please try again later or download manually from GitHub.";
                progressBar.Value = 0;
                downloadDlg.CloseButtonText = "Close";
            });
            return;
        }

        // Close the progress dialog
        downloadDlg.Hide();

        // Launch installer and close RDXC
        UpdateService.LaunchInstallerAndExit(installerPath, () =>
        {
            DispatcherQueue.TryEnqueue(() =>
            {
                this.Close();
            });
        });
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

    // ── Drag-and-drop game add ────────────────────────────────────────────────

    private void Grid_DragOver(object sender, DragEventArgs e)
    {
        if (e.DataView.Contains(Windows.ApplicationModel.DataTransfer.StandardDataFormats.StorageItems))
        {
            e.AcceptedOperation = Windows.ApplicationModel.DataTransfer.DataPackageOperation.Copy;
            e.DragUIOverride.Caption = "Drop to add game";
            e.DragUIOverride.IsCaptionVisible = true;
            e.DragUIOverride.IsGlyphVisible = true;
        }
    }

    private async void Grid_Drop(object sender, DragEventArgs e)
    {
        if (!e.DataView.Contains(Windows.ApplicationModel.DataTransfer.StandardDataFormats.StorageItems))
            return;

        var items = await e.DataView.GetStorageItemsAsync();
        foreach (var item in items)
        {
            if (item is not Windows.Storage.StorageFile file) continue;
            if (!file.FileType.Equals(".exe", StringComparison.OrdinalIgnoreCase)) continue;

            var exePath = file.Path;
            CrashReporter.Log($"DragDrop: received exe '{exePath}'");

            try
            {
                await ProcessDroppedExe(exePath);
            }
            catch (Exception ex)
            {
                CrashReporter.Log($"DragDrop: error processing '{exePath}': {ex.Message}");
            }
        }
    }

    private async Task ProcessDroppedExe(string exePath)
    {
        var exeDir  = Path.GetDirectoryName(exePath)!;
        var exeName = Path.GetFileNameWithoutExtension(exePath);

        // ── Determine the game root folder ────────────────────────────────────
        // Walk up from the exe to find the likely game root.
        // For Unreal: the exe is usually in GameRoot\Binaries\Win64 or \WinGDK
        // For Unity: the exe is usually in the game root next to UnityPlayer.dll
        // For others: the exe folder or its parent is the game root
        var gameRoot = InferGameRoot(exeDir);
        CrashReporter.Log($"DragDrop: inferred game root '{gameRoot}' from exe dir '{exeDir}'");

        // ── Detect engine and correct install path ────────────────────────────
        var (installPath, engine) = GameDetectionService.DetectEngineAndPath(gameRoot);

        // ── Infer game name ───────────────────────────────────────────────────
        var gameName = InferGameName(exePath, gameRoot, engine);
        CrashReporter.Log($"DragDrop: inferred name '{gameName}', engine={engine}");

        // ── Check for duplicates (by install path or normalized name) ─────────
        var normName = GameDetectionService.NormalizeName(gameName);
        var normInstall = installPath.TrimEnd(Path.DirectorySeparatorChar).ToLowerInvariant();

        var existingCard = ViewModel.AllCards.FirstOrDefault(c =>
            GameDetectionService.NormalizeName(c.GameName) == normName
            || (!string.IsNullOrEmpty(c.InstallPath)
                && c.InstallPath.TrimEnd(Path.DirectorySeparatorChar)
                    .Equals(normInstall, StringComparison.OrdinalIgnoreCase)));

        if (existingCard != null)
        {
            var dupDialog = new ContentDialog
            {
                Title           = "Game Already Exists",
                Content         = $"\"{existingCard.GameName}\" is already in your library at:\n{existingCard.InstallPath}",
                CloseButtonText = "OK",
                XamlRoot        = Content.XamlRoot,
                Background      = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 16, 20, 40)),
            };
            await dupDialog.ShowAsync();
            return;
        }

        // ── Confirm with user (allow name edit) ──────────────────────────────
        var nameBox = new TextBox { Text = gameName, Width = 380 };
        var engineLabel = engine switch
        {
            EngineType.Unreal       => "Unreal Engine",
            EngineType.UnrealLegacy => "Unreal Engine (Legacy)",
            EngineType.Unity        => "Unity",
            _                       => "Unknown"
        };

        var confirmPanel = new StackPanel { Spacing = 8 };
        confirmPanel.Children.Add(new TextBlock
        {
            Text = "Game name:", Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 180, 190, 220)),
        });
        confirmPanel.Children.Add(nameBox);
        confirmPanel.Children.Add(new TextBlock
        {
            Text = $"Engine: {engineLabel}\nInstall path: {installPath}",
            TextWrapping = TextWrapping.Wrap,
            Foreground   = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 130, 145, 175)),
            FontSize     = 12, Margin = new Thickness(0, 6, 0, 0),
        });

        var confirmDialog = new ContentDialog
        {
            Title             = "➕ Add Dropped Game",
            Content           = confirmPanel,
            PrimaryButtonText = "Add Game",
            CloseButtonText   = "Cancel",
            XamlRoot          = Content.XamlRoot,
            Background        = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 16, 20, 40)),
        };
        var result = await confirmDialog.ShowAsync();
        if (result != ContentDialogResult.Primary) return;

        var finalName = nameBox.Text.Trim();
        if (string.IsNullOrEmpty(finalName)) return;

        CrashReporter.Log($"DragDrop: adding game '{finalName}' at '{installPath}'");
        var game = new DetectedGame
        {
            Name = finalName, InstallPath = gameRoot, Source = "Manual", IsManuallyAdded = true
        };
        ViewModel.AddManualGameCommand.Execute(game);
    }

    /// <summary>
    /// Walk up from the exe directory to find the game root.
    /// Stops when we find a directory that looks like a game root.
    /// For Unreal: recognises Binaries\Win64 structure (2 levels up).
    /// For other games: checks for store markers (Steam, GOG, Epic, EA, Xbox)
    /// and defaults to the exe's own directory if no markers are found.
    /// </summary>
    private static string InferGameRoot(string exeDir)
    {
        var dir = exeDir;

        // If the exe is inside Binaries\Win64, Binaries\WinGDK, or Binaries\Win32,
        // the game root is two levels up.
        var dirName   = Path.GetFileName(dir) ?? "";
        var parentDir = Path.GetDirectoryName(dir);
        var parentName = parentDir != null ? Path.GetFileName(parentDir) ?? "" : "";

        if (parentName.Equals("Binaries", StringComparison.OrdinalIgnoreCase)
            && (dirName.Equals("Win64", StringComparison.OrdinalIgnoreCase)
             || dirName.Equals("WinGDK", StringComparison.OrdinalIgnoreCase)
             || dirName.Equals("Win32", StringComparison.OrdinalIgnoreCase)))
        {
            var grandparent = Path.GetDirectoryName(parentDir);
            if (grandparent != null) return grandparent;
        }

        // Walk up looking for game root markers (max 3 levels).
        // Check the exe's own directory first — most non-Unreal games have
        // the exe right in the game root alongside store markers.
        var current = dir;
        for (int i = 0; i < 3 && current != null; i++)
        {
            if (LooksLikeGameRoot(current))
                return current;
            current = Path.GetDirectoryName(current);
        }

        // No markers found at all — the exe directory itself is the safest bet.
        // Don't walk up further, as that risks hitting a library root or drive root.
        return dir;
    }

    /// <summary>
    /// Returns true if a directory looks like a game root based on store markers
    /// or engine files. This is intentionally broad to catch Steam, GOG, Epic,
    /// EA, Xbox, Unity, and Unreal games.
    /// </summary>
    private static bool LooksLikeGameRoot(string dirPath)
    {
        try
        {
            // Steam markers
            if (File.Exists(Path.Combine(dirPath, "steam_appid.txt"))
             || File.Exists(Path.Combine(dirPath, "steam_api64.dll"))
             || File.Exists(Path.Combine(dirPath, "steam_api.dll")))
                return true;

            // GOG markers — GOG games have goggame-*.dll, goglog.ini, gog.ico, etc.
            if (File.Exists(Path.Combine(dirPath, "goglog.ini"))
             || File.Exists(Path.Combine(dirPath, "gog.ico"))
             || File.Exists(Path.Combine(dirPath, "goggame.sdb")))
                return true;
            // Also check for goggame-*.dll pattern
            if (Directory.GetFiles(dirPath, "goggame-*.dll").Length > 0)
                return true;

            // Epic markers
            if (Directory.Exists(Path.Combine(dirPath, ".egstore")))
                return true;

            // EA markers
            if (File.Exists(Path.Combine(dirPath, "installerdata.xml"))
             || File.Exists(Path.Combine(dirPath, "__Installer")))
                return true;

            // Xbox / Game Pass markers
            if (File.Exists(Path.Combine(dirPath, "MicrosoftGame.config"))
             || File.Exists(Path.Combine(dirPath, "appxmanifest.xml")))
                return true;

            // Unity marker
            if (File.Exists(Path.Combine(dirPath, "UnityPlayer.dll")))
                return true;

            // Unreal markers
            if (Directory.Exists(Path.Combine(dirPath, "Binaries"))
             || Directory.Exists(Path.Combine(dirPath, "Engine")))
                return true;
        }
        catch { /* permission issues — skip silently */ }

        return false;
    }

    /// <summary>
    /// Infer the game name from the exe and folder structure.
    /// Priority:
    ///   1. For Unreal: use the top-level folder name under game root (the "project" name)
    ///   2. For Unity: use the exe name (typically matches the game name)
    ///   3. Use the game root folder name
    ///   4. Fallback to exe filename
    /// Cleans up common suffixes like "-Win64-Shipping", "Shipping", etc.
    /// </summary>
    private static string InferGameName(string exePath, string gameRoot, EngineType engine)
    {
        var exeName     = Path.GetFileNameWithoutExtension(exePath);
        var rootDirName = Path.GetFileName(gameRoot) ?? exeName;

        if (engine == EngineType.Unreal || engine == EngineType.UnrealLegacy)
        {
            // Unreal games: the exe is often "GameName-Win64-Shipping.exe"
            // Strip the suffix to get the clean name
            var cleanExe = CleanUnrealExeName(exeName);

            // Sometimes the root folder is better (e.g. "Avowed" vs "Michigan-Win64-Shipping")
            // Prefer root folder name if it looks like a proper name (has spaces or is short)
            if (rootDirName.Contains(' ') || rootDirName.Contains('-'))
                return CleanFolderName(rootDirName);

            // Check for a content/game subfolder one level into root (common Xbox pattern)
            try
            {
                var subdirs = Directory.GetDirectories(gameRoot)
                    .Select(Path.GetFileName)
                    .Where(d => d != null
                        && !d.Equals("Binaries", StringComparison.OrdinalIgnoreCase)
                        && !d.Equals("Engine", StringComparison.OrdinalIgnoreCase)
                        && !d.Equals("Content", StringComparison.OrdinalIgnoreCase)
                        && !d.StartsWith(".", StringComparison.Ordinal))
                    .ToList();

                // If there's one or two content folders, the first might be the game name
                if (subdirs.Count > 0 && subdirs.Count <= 3)
                {
                    var candidate = subdirs.FirstOrDefault(d =>
                        !string.IsNullOrEmpty(d)
                        && !d.Equals("Saved", StringComparison.OrdinalIgnoreCase)
                        && !d.Equals("Plugins", StringComparison.OrdinalIgnoreCase)
                        && !d.Equals("Intermediate", StringComparison.OrdinalIgnoreCase));

                    if (candidate != null && candidate.Length > 2)
                        return CleanFolderName(candidate);
                }
            }
            catch { }

            return !string.IsNullOrEmpty(cleanExe) ? cleanExe : CleanFolderName(rootDirName);
        }

        if (engine == EngineType.Unity)
        {
            // Unity: exe name typically IS the game name
            return CleanFolderName(exeName);
        }

        // Unknown engine: prefer root folder name, fall back to exe name
        return CleanFolderName(rootDirName);
    }

    /// <summary>Strips common Unreal exe suffixes to get a clean game name.</summary>
    private static string CleanUnrealExeName(string exeName)
    {
        // Common patterns: "GameName-Win64-Shipping", "GameName-WinGDK-Shipping",
        // "GameNameShipping", "GameName-Win64-Test"
        var cleaned = Regex.Replace(exeName, @"[_-]?(Win64|WinGDK|Win32)[_-]?Shipping$", "", RegexOptions.IgnoreCase);
        cleaned = Regex.Replace(cleaned, @"[_-]?Shipping$", "", RegexOptions.IgnoreCase);
        cleaned = Regex.Replace(cleaned, @"[_-]?(Win64|WinGDK|Win32)[_-]?Test$", "", RegexOptions.IgnoreCase);
        cleaned = Regex.Replace(cleaned, @"[_-]?(Win64|WinGDK|Win32)$", "", RegexOptions.IgnoreCase);
        return cleaned.Trim('-', '_', ' ');
    }

    /// <summary>
    /// Cleans a folder or exe name into a presentable game name.
    /// Replaces underscores and camelCase boundaries with spaces.
    /// </summary>
    private static string CleanFolderName(string name)
    {
        // Replace underscores and hyphens with spaces
        var cleaned = name.Replace('_', ' ').Replace('-', ' ');
        // Insert spaces before uppercase letters in camelCase (e.g. "HighOnLife" → "High On Life")
        // but not for consecutive caps (e.g. "AFOP" stays "AFOP")
        cleaned = Regex.Replace(cleaned, @"(?<=[a-z])(?=[A-Z])", " ");
        // Collapse multiple spaces
        cleaned = Regex.Replace(cleaned, @"\s+", " ").Trim();
        return cleaned;
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
        if (card == null) return;

        var textColour = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 195, 210, 240));
        var linkColour = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 130, 170, 240));
        var dimColour  = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 120, 140, 170));

        var outerPanel = new StackPanel { Spacing = 10 };

        // ── Wiki status badge at top-left ─────────────────────────────────────────
        var statusBg     = card.WikiStatusBadgeBackground;
        var statusBorder = card.WikiStatusBadgeBorderBrush;
        var statusFg     = card.WikiStatusBadgeForeground;
        var statusBadge = new Border
        {
            CornerRadius    = new CornerRadius(6),
            Padding         = new Thickness(10, 4, 10, 4),
            HorizontalAlignment = HorizontalAlignment.Left,
            Background      = new SolidColorBrush(ParseColor(statusBg)),
            BorderBrush     = new SolidColorBrush(ParseColor(statusBorder)),
            BorderThickness = new Thickness(1),
            Child = new TextBlock
            {
                Text       = card.WikiStatusLabel,
                FontSize   = 12,
                Foreground = new SolidColorBrush(ParseColor(statusFg)),
            }
        };
        outerPanel.Children.Add(statusBadge);

        // ── Notes content (if any) ────────────────────────────────────────────────
        if (!string.IsNullOrWhiteSpace(card.Notes))
        {
            if (!string.IsNullOrEmpty(card.NotesUrl))
            {
                var para = new Microsoft.UI.Xaml.Documents.Paragraph();
                para.Inlines.Add(new Microsoft.UI.Xaml.Documents.Run
                {
                    Text       = card.Notes,
                    Foreground = textColour,
                    FontSize   = 13,
                });
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
                outerPanel.Children.Add(rtb);
            }
            else
            {
                outerPanel.Children.Add(new TextBlock
                {
                    Text         = card.Notes,
                    TextWrapping = TextWrapping.Wrap,
                    Foreground   = textColour,
                    FontSize     = 13,
                    LineHeight   = 22,
                });
            }
        }
        else
        {
            outerPanel.Children.Add(new TextBlock
            {
                Text       = "No additional notes for this game.",
                Foreground = dimColour,
                FontSize   = 13,
            });
        }

        var scrollContent = new ScrollViewer
        {
            Content   = outerPanel,
            MaxHeight = 400,
            Padding   = new Thickness(0, 4, 12, 0),
        };

        var dialog = new ContentDialog
        {
            Title           = $"ℹ  {card.GameName}",
            Content         = scrollContent,
            CloseButtonText = "Close",
            XamlRoot        = Content.XamlRoot,
            Background      = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 16, 20, 40)),
        };
        await dialog.ShowAsync();
    }

    /// <summary>Creates a thin horizontal separator line for dialogs.</summary>
    private static Border MakeSeparator() => new()
    {
        Height = 1,
        Background = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 30, 40, 60)),
        Margin = new Thickness(0, 2, 0, 2),
    };

    /// <summary>Parses a hex colour string like "#1C2848" into a Windows.UI.Color.</summary>
    private static Windows.UI.Color ParseColor(string hex)
    {
        hex = hex.TrimStart('#');
        byte a = 255;
        int offset = 0;
        if (hex.Length == 8) { a = Convert.ToByte(hex[..2], 16); offset = 2; }
        byte r = Convert.ToByte(hex.Substring(offset, 2), 16);
        byte g = Convert.ToByte(hex.Substring(offset + 2, 2), 16);
        byte b = Convert.ToByte(hex.Substring(offset + 4, 2), 16);
        return Windows.UI.Color.FromArgb(a, r, g, b);
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
