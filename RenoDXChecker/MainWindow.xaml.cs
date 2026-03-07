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
        AuxInstallService.EnsureReShadeStaging(); // create staging dir (DLLs downloaded by ReShadeUpdateService)
        Title = "RDXC - RenoDXCommander";
        // Fire-and-forget: check/download Lilium HDR shaders in the background
        _ = ShaderPackService.EnsureLatestAsync(ViewModel.HttpClient);
        CrashReporter.Log("MainWindow: InitializeComponent complete");
        // Set a sensible default size immediately so the window isn't huge on first launch.
        // TryRestoreWindowBounds (called on Activated) will then override this with the
        // saved size+position from the previous session, if one exists.
        AppWindow.Resize(new Windows.Graphics.SizeInt32(DefaultWidth, DefaultHeight));

        // Set the title bar icon (unpackaged apps need this explicitly)
        AppWindow.SetIcon("icon.ico");

        // Dark title bar — match our theme
        if (AppWindow.TitleBar is { } titleBar)
        {
            titleBar.BackgroundColor = Windows.UI.Color.FromArgb(0xFF, 0x0F, 0x13, 0x18);           // SurfaceHeader
            titleBar.ForegroundColor = Windows.UI.Color.FromArgb(0xFF, 0xE8, 0xEC, 0xF2);           // TextPrimary
            titleBar.InactiveBackgroundColor = Windows.UI.Color.FromArgb(0xFF, 0x0F, 0x13, 0x18);
            titleBar.InactiveForegroundColor = Windows.UI.Color.FromArgb(0xFF, 0x6B, 0x7A, 0x8E);   // TextTertiary
            titleBar.ButtonBackgroundColor = Windows.UI.Color.FromArgb(0xFF, 0x0F, 0x13, 0x18);
            titleBar.ButtonForegroundColor = Windows.UI.Color.FromArgb(0xFF, 0xA0, 0xAA, 0xBB);     // TextSecondary
            titleBar.ButtonHoverBackgroundColor = Windows.UI.Color.FromArgb(0xFF, 0x1E, 0x24, 0x2C); // SurfaceOverlay
            titleBar.ButtonHoverForegroundColor = Windows.UI.Color.FromArgb(0xFF, 0xE8, 0xEC, 0xF2);
            titleBar.ButtonPressedBackgroundColor = Windows.UI.Color.FromArgb(0xFF, 0x28, 0x32, 0x40); // BorderDefault
            titleBar.ButtonPressedForegroundColor = Windows.UI.Color.FromArgb(0xFF, 0xE8, 0xEC, 0xF2);
            titleBar.ButtonInactiveBackgroundColor = Windows.UI.Color.FromArgb(0xFF, 0x0F, 0x13, 0x18);
            titleBar.ButtonInactiveForegroundColor = Windows.UI.Color.FromArgb(0xFF, 0x40, 0x48, 0x58); // TextDisabled
        }
        // Restore window size & position after activation (ensure HWND is ready)
        this.Activated += MainWindow_Activated;
        ViewModel.SetDispatcher(DispatcherQueue);
        ViewModel.ConfirmForeignDxgiOverwrite = ShowForeignDxgiConfirmDialogAsync;
        ViewModel.ConfirmForeignWinmmOverwrite = ShowForeignWinmmConfirmDialogAsync;
        ViewModel.PropertyChanged += OnViewModelChanged;
        GameList.ItemsSource = ViewModel.DisplayedGames;
        // Apply initial visibility
        UpdatePageVisibility();
        // Always show the ✕ clear button on search box
        SearchBox.Loaded += (_, _) => VisualStateManager.GoToState(SearchBox, "ButtonVisible", false);
        _ = ViewModel.InitializeAsync();
        // Silent update check — runs in background, shows dialog only if update found
        _ = CheckForAppUpdateAsync();
        // Show patch notes on first launch after update
        _ = ShowPatchNotesIfNewVersionAsync();
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
        // Check if this game is in Luma mode (disables some controls)
        bool isLumaMode = false;
        if (!string.IsNullOrEmpty(prefilledDetected))
            isLumaMode = ViewModel.IsLumaEnabled(prefilledDetected);

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
            IsEnabled  = !isLumaMode,
            FontSize   = 12,
            Padding    = new Thickness(10, 6, 10, 6),
            Background = Brush("AccentPurpleBgBrush"),
            Foreground = Brush("AccentPurpleBrush"),
            BorderBrush = Brush("AccentPurpleBorderBrush"),
            HorizontalAlignment = HorizontalAlignment.Stretch,
        };
        ToolTipService.SetToolTip(excludeBtn,
            isLumaMode ? "Disabled in Luma mode" :
            "Exclude this game from all wiki matching. The card will show a Discord link instead of an install button.");

        var panel = new StackPanel { Spacing = 8 };

        // ── Game name + Wiki name side by side ───────────────────────────────────
        detectedBox.Header = "Game name (editable)";
        detectedBox.Width  = double.NaN;
        detectedBox.HorizontalAlignment = HorizontalAlignment.Stretch;
        detectedBox.FontSize = 12;
        wikiBox.Header = "Wiki mod name";
        wikiBox.Width  = double.NaN;
        wikiBox.HorizontalAlignment = HorizontalAlignment.Stretch;
        wikiBox.FontSize = 12;

        var resetBtn = new Button
        {
            Content = "↩ Reset",
            FontSize = 12,
            VerticalAlignment = VerticalAlignment.Bottom,
            Padding = new Thickness(10, 6, 10, 6),
            Background = Brush("SurfaceOverlayBrush"),
            Foreground = Brush("TextSecondaryBrush"),
            BorderBrush = Brush("BorderDefaultBrush"),
        };
        ToolTipService.SetToolTip(resetBtn,
            "Reset game name back to auto-detected and clear wiki name mapping.");
        var originalStoreName = !string.IsNullOrEmpty(prefilledDetected)
            ? ViewModel.GetOriginalStoreName(prefilledDetected) : null;
        resetBtn.Click += (s, e) =>
        {
            detectedBox.Text = originalStoreName ?? prefilledDetected ?? "";
            wikiBox.Text = "";
        };

        var nameGrid = new Grid { ColumnSpacing = 8 };
        nameGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        nameGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        nameGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        Grid.SetColumn(detectedBox, 0);
        Grid.SetColumn(wikiBox, 1);
        Grid.SetColumn(resetBtn, 2);
        nameGrid.Children.Add(detectedBox);
        nameGrid.Children.Add(wikiBox);
        nameGrid.Children.Add(resetBtn);
        panel.Children.Add(nameGrid);

        panel.Children.Add(MakeSeparator());

        // ── DLL Naming Override ──────────────────────────────────────────────────
        bool isDllOverride = !string.IsNullOrEmpty(prefilledDetected) &&
                             ViewModel.HasDllOverride(prefilledDetected);
        var existingCfg = !string.IsNullOrEmpty(prefilledDetected)
            ? ViewModel.GetDllOverride(prefilledDetected) : null;

        var dllOverrideBtn = new ToggleButton
        {
            Content    = "📝  DLL naming override",
            IsChecked  = isDllOverride,
            IsEnabled  = !isLumaMode,
            FontSize   = 12,
            Padding    = new Thickness(10, 6, 10, 6),
            Background = Brush("AccentTealBgBrush"),
            Foreground = Brush("AccentTealBrush"),
            BorderBrush = Brush("AccentTealBorderBrush"),
            HorizontalAlignment = HorizontalAlignment.Stretch,
        };
        ToolTipService.SetToolTip(dllOverrideBtn,
            "Override the filenames ReShade and Display Commander are installed as. " +
            "When enabled, existing RS/DC files are renamed to the custom filenames. " +
            "The game is automatically excluded from DC Mode, Update All, and global shaders.");

        // Default names based on 32-bit mode
        bool is32Bit = !string.IsNullOrEmpty(prefilledDetected) &&
                       ViewModel.Is32BitGame(prefilledDetected);
        var defaultRsName = is32Bit ? "ReShade32.dll" : "ReShade64.dll";
        var defaultDcName = is32Bit ? "zzz_display_commander.addon32" : "zzz_display_commander.addon64";

        var rsNameBox = new TextBox
        {
            PlaceholderText = defaultRsName,
            Text = existingCfg?.ReShadeFileName ?? "",
            Header = "ReShade filename",
            FontSize = 12,
            IsEnabled = isDllOverride,
            HorizontalAlignment = HorizontalAlignment.Stretch,
        };

        var dcNameBox = new TextBox
        {
            PlaceholderText = defaultDcName,
            Text = existingCfg?.DcFileName ?? "",
            Header = "DC filename",
            FontSize = 12,
            IsEnabled = isDllOverride,
            HorizontalAlignment = HorizontalAlignment.Stretch,
        };

        dllOverrideBtn.Checked   += (s, ev) => { rsNameBox.IsEnabled = true;  dcNameBox.IsEnabled = true; };
        dllOverrideBtn.Unchecked += (s, ev) => { rsNameBox.IsEnabled = false; dcNameBox.IsEnabled = false; };

        panel.Children.Add(dllOverrideBtn);

        var dllNameGrid = new Grid
        {
            ColumnSpacing = 8,
            Margin = new Thickness(0, 4, 0, 0),
        };
        dllNameGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        dllNameGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        Grid.SetColumn(rsNameBox, 0);
        Grid.SetColumn(dcNameBox, 1);
        dllNameGrid.Children.Add(rsNameBox);
        dllNameGrid.Children.Add(dcNameBox);
        panel.Children.Add(dllNameGrid);

        panel.Children.Add(MakeSeparator());

        // ── Exclude from wiki ────────────────────────────────────────────────────
        panel.Children.Add(excludeBtn);

        panel.Children.Add(MakeSeparator());

        // ── Exclude from Update All ──────────────────────────────────────────────
        bool isUaExcluded = !string.IsNullOrEmpty(prefilledDetected) &&
                            ViewModel.IsUpdateAllExcluded(prefilledDetected);

        var uaExcludeBtn = new ToggleButton
        {
            Content    = "⬆  Exclude from Update All",
            IsChecked  = isUaExcluded,
            FontSize   = 12,
            Padding    = new Thickness(10, 6, 10, 6),
            Background = Brush("AccentPurpleBgBrush"),
            Foreground = Brush("AccentPurpleBrush"),
            BorderBrush = Brush("AccentPurpleBorderBrush"),
            HorizontalAlignment = HorizontalAlignment.Stretch,
        };
        ToolTipService.SetToolTip(uaExcludeBtn,
            "Skip this game when using Update All RenoDX, Update All ReShade, or Update All DC.");

        panel.Children.Add(uaExcludeBtn);

        panel.Children.Add(MakeSeparator());

        // ── 32-bit mode ──────────────────────────────────────────────────────────
        var bit32Btn = new ToggleButton
        {
            Content    = "⚠  32-bit mode",
            IsChecked  = is32Bit,
            IsEnabled  = !isLumaMode,
            FontSize   = 12,
            Padding    = new Thickness(10, 6, 10, 6),
            Background = Brush("AccentAmberBgBrush"),
            Foreground = Brush("AccentAmberBrush"),
            BorderBrush = Brush("BorderDefaultBrush"),
            HorizontalAlignment = HorizontalAlignment.Stretch,
        };
        ToolTipService.SetToolTip(bit32Btn,
            "Installs 32-bit versions of ReShade, Unity addon, and Display Commander. Only enable if you know this game is 32-bit.");

        panel.Children.Add(bit32Btn);

        panel.Children.Add(MakeSeparator());

        // ── Per-game DC Mode override ──────────────────────────────────────────
        int? currentDcMode = !string.IsNullOrEmpty(prefilledDetected)
            ? ViewModel.GetPerGameDcModeOverride(prefilledDetected)
            : null;

        var dcModeOptions = new[] { "Follow Global", "Exclude (Off)", "DC Mode 1", "DC Mode 2" };
        var dcModeCombo = new ComboBox
        {
            ItemsSource  = dcModeOptions,
            SelectedIndex = currentDcMode switch { null => 0, 0 => 1, 1 => 2, 2 => 3, _ => 0 },
            FontSize     = 12,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Header       = "DC Mode for this game",
        };
        ToolTipService.SetToolTip(dcModeCombo,
            "Follow Global = use the header DC Mode toggle. Exclude (Off) = always use normal naming. " +
            "DC Mode 1 = force dxgi.dll proxy. DC Mode 2 = force winmm.dll proxy.");

        panel.Children.Add(dcModeCombo);

        panel.Children.Add(MakeSeparator());

        // ── Shader mode ──────────────────────────────────────────────────────────
        string currentShaderMode = !string.IsNullOrEmpty(prefilledDetected)
            ? ViewModel.GetPerGameShaderMode(prefilledDetected)
            : "Global";

        var shaderModeOptions = new[] { "Global", "Off", "Minimum", "All", "User" };
        var shaderModeCombo = new ComboBox
        {
            ItemsSource  = shaderModeOptions,
            SelectedItem = currentShaderMode,
            FontSize     = 12,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Header       = "Shader mode for this game",
        };
        ToolTipService.SetToolTip(shaderModeCombo,
            "Global = follow the header toggle. Off = no shaders. Minimum = Lilium only. All = all packs. User = custom folder only.\n" +
            "Note: Per-game shader mode only applies when ReShade is used standalone (DC Mode OFF). " +
            "When DC Mode is ON, all DC-mode games share the DC global shader folder.");

        panel.Children.Add(shaderModeCombo);

        var dlg = new ContentDialog
        {
            Title             = "Overrides",
            Content           = new ScrollViewer
            {
                Content = panel,
                MaxHeight = 560,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                Padding = new Thickness(0, 0, 8, 0),
            },
            PrimaryButtonText = "Save",
            SecondaryButtonText = !string.IsNullOrEmpty(prefilledDetected) &&
                                  !string.IsNullOrEmpty(ViewModel.GetNameMapping(prefilledDetected ?? ""))
                                  ? "Remove mapping" : "",
            CloseButtonText   = "Cancel",
            XamlRoot          = Content.XamlRoot,
            Background        = Brush("SurfaceRaisedBrush"),
        };

        _ = dlg.ShowAsync().AsTask().ContinueWith(t =>
        {
            if (t.Result == ContentDialogResult.Primary)
            {
                var det = detectedBox.Text?.Trim();

                // Handle game rename — if the user edited the name, rename everywhere first
                // so all subsequent toggles operate on the new name.
                if (!string.IsNullOrEmpty(prefilledDetected) && !string.IsNullOrEmpty(det)
                    && !det.Equals(prefilledDetected, StringComparison.OrdinalIgnoreCase))
                {
                    ViewModel.RenameGame(prefilledDetected, det);
                }

                // Handle wiki exclusion toggle
                bool nowExcluded = excludeBtn.IsChecked == true;
                if (!string.IsNullOrEmpty(det) && nowExcluded != ViewModel.IsWikiExcluded(det))
                    ViewModel.ToggleWikiExclusion(det);

                // Handle per-game DC Mode override
                int? newDcMode = dcModeCombo.SelectedIndex switch { 1 => 0, 2 => 1, 3 => 2, _ => null };
                if (!string.IsNullOrEmpty(det) && newDcMode != ViewModel.GetPerGameDcModeOverride(det))
                {
                    ViewModel.SetPerGameDcModeOverride(det, newDcMode);
                    ViewModel.ApplyDcModeSwitchForCard(det);
                }

                // Handle Update All exclusion toggle
                bool nowUaExcluded = uaExcludeBtn.IsChecked == true;
                if (!string.IsNullOrEmpty(det) && nowUaExcluded != ViewModel.IsUpdateAllExcluded(det))
                    ViewModel.ToggleUpdateAllExclusion(det);

                // Handle per-game shader mode
                var newShaderMode = shaderModeCombo.SelectedItem as string ?? "Global";
                if (!string.IsNullOrEmpty(det) && newShaderMode != ViewModel.GetPerGameShaderMode(det))
                {
                    ViewModel.SetPerGameShaderMode(det, newShaderMode);
                    ViewModel.DeployShadersForCard(det);
                }

                // Handle 32-bit mode toggle
                bool now32Bit = bit32Btn.IsChecked == true;
                if (!string.IsNullOrEmpty(det) && now32Bit != ViewModel.Is32BitGame(det))
                    ViewModel.Toggle32Bit(det);

                // Handle DLL naming override
                bool nowDllOverride = dllOverrideBtn.IsChecked == true;
                bool wasDllOverride = !string.IsNullOrEmpty(det) && ViewModel.HasDllOverride(det);
                if (!string.IsNullOrEmpty(det))
                {
                    var targetCard = ViewModel.AllCards.FirstOrDefault(c =>
                        c.GameName.Equals(det, StringComparison.OrdinalIgnoreCase));

                    if (nowDllOverride && !wasDllOverride && targetCard != null)
                    {
                        // Toggled ON — enable override (uninstalls existing RS/DC)
                        var rsName = !string.IsNullOrWhiteSpace(rsNameBox.Text) ? rsNameBox.Text.Trim() : rsNameBox.PlaceholderText;
                        var dcName = !string.IsNullOrWhiteSpace(dcNameBox.Text) ? dcNameBox.Text.Trim() : dcNameBox.PlaceholderText;
                        ViewModel.EnableDllOverride(targetCard, rsName, dcName);
                    }
                    else if (nowDllOverride && wasDllOverride && targetCard != null)
                    {
                        // Still ON — rename files on disk if the names changed
                        var rsName = !string.IsNullOrWhiteSpace(rsNameBox.Text) ? rsNameBox.Text.Trim() : rsNameBox.PlaceholderText;
                        var dcName = !string.IsNullOrWhiteSpace(dcNameBox.Text) ? dcNameBox.Text.Trim() : dcNameBox.PlaceholderText;
                        ViewModel.UpdateDllOverrideNames(targetCard, rsName, dcName);
                    }
                    else if (!nowDllOverride && wasDllOverride && targetCard != null)
                    {
                        // Toggled OFF — disable override (removes custom-named files)
                        ViewModel.DisableDllOverride(targetCard);
                    }
                }

                // Save name mapping if provided and not excluded
                // (skip if rename already triggered a rescan to avoid double-rebuild)
                var key = wikiBox.Text?.Trim();
                if (!nowExcluded && !string.IsNullOrEmpty(det) && !string.IsNullOrEmpty(key))
                    ViewModel.AddNameMapping(det, key);
                else if (!nowExcluded && !string.IsNullOrEmpty(det) && string.IsNullOrEmpty(key))
                    ViewModel.RemoveNameMapping(det);
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
                    // Don't disturb the Settings panel if it's currently visible
                    if (ViewModel.CurrentPage != AppPage.Settings)
                    {
                        LoadingPanel.Visibility = loading ? Visibility.Visible  : Visibility.Collapsed;
                        GameViewPanel.Visibility = loading ? Visibility.Collapsed : Visibility.Visible;
                    }
                    LoadingRing.IsActive = loading;
                    RefreshBtn.IsEnabled = !loading;
                    StatusDot.Fill = new SolidColorBrush(loading
                        ? ((SolidColorBrush)Application.Current.Resources["AccentAmberBrush"]).Color
                        : ((SolidColorBrush)Application.Current.Resources["AccentGreenBrush"]).Color);
                    if (!loading) TryRestoreSelection();
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
        _ = RefreshWithScrollRestore();
    }

    private void FullRefreshButton_Click(object sender, RoutedEventArgs e)
    {
        CrashReporter.Log("User clicked Full Refresh");
        _ = FullRefreshWithScrollRestore();
    }

    private async Task RefreshWithScrollRestore()
    {
        var selectedName = (GameList.SelectedItem as GameCardViewModel)?.GameName;

        await ViewModel.RefreshAsync();

        RestoreScrollAndSelection(selectedName);
    }

    private async Task FullRefreshWithScrollRestore()
    {
        var selectedName = (GameList.SelectedItem as GameCardViewModel)?.GameName;

        await ViewModel.FullRefreshAsync();

        RestoreScrollAndSelection(selectedName);
    }

    private void RestoreScrollAndSelection(string? selectedName)
    {
        // Restore game list selection
        if (!string.IsNullOrEmpty(selectedName))
        {
            _pendingReselect = selectedName;
            DispatcherQueue.TryEnqueue(TryRestoreSelection);
        }
    }

    private string? _pendingReselect;

    private void RsIniButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { Tag: GameCardViewModel card }) return;
        if (string.IsNullOrEmpty(card.InstallPath)) return;
        try
        {
            AuxInstallService.MergeRsIni(card.InstallPath);
            card.RsActionMessage = "✅ reshade.ini merged into game folder.";
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
                Foreground   = Brush("AccentAmberBrush"),
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
            Background          = Brush("SurfaceOverlayBrush"),
        };

        var result = await dlg.ShowAsync();
        return result == ContentDialogResult.Primary;
    }

    private async Task<bool> ShowForeignWinmmConfirmDialogAsync(GameCardViewModel card, string winmmPath)
    {
        var fileSize = new System.IO.FileInfo(winmmPath).Length;
        var sizeKB   = fileSize / 1024.0;

        var dlg = new ContentDialog
        {
            Title               = "⚠ Unknown winmm.dll Detected",
            Content             = new TextBlock
            {
                TextWrapping = TextWrapping.Wrap,
                Foreground   = Brush("AccentAmberBrush"),
                FontSize     = 13,
                Text         = $"A winmm.dll file was found in:\n{card.InstallPath}\n\n" +
                               $"File size: {sizeKB:N0} KB\n\n" +
                               "RDXC cannot identify this file as Display Commander. " +
                               "It may belong to another mod or DLL injector.\n\n" +
                               "Overwriting it may break the existing mod. Do you want to proceed?",
            },
            PrimaryButtonText   = "Overwrite",
            CloseButtonText     = "Cancel",
            XamlRoot            = Content.XamlRoot,
            Background          = Brush("SurfaceOverlayBrush"),
        };

        var result = await dlg.ShowAsync();
        return result == ContentDialogResult.Primary;
    }

    // ── Auto-Update ────────────────────────────────────────────────────────────

    private async Task CheckForAppUpdateAsync()
    {
        try
        {
            if (ViewModel.SkipUpdateCheck)
            {
                CrashReporter.Log("MainWindow: update check skipped (disabled in settings)");
                return;
            }

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
                        Foreground   = Brush("TextSecondaryBrush"),
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
            Background          = Brush("SurfaceRaisedBrush"),
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
            Foreground   = Brush("TextSecondaryBrush"),
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
            Background = Brush("SurfaceRaisedBrush"),
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

    private void SettingsButton_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.NavigateToSettingsCommand.Execute(null);
        GameViewPanel.Visibility = Visibility.Collapsed;
        SettingsPanel.Visibility = Visibility.Visible;
        LoadingPanel.Visibility = Visibility.Collapsed;
        // Sync toggle state with ViewModel
        SkipUpdateToggle.IsOn = ViewModel.SkipUpdateCheck;
        VerboseLoggingToggle.IsOn = ViewModel.VerboseLogging;
    }

    private void SkipUpdateToggle_Toggled(object sender, RoutedEventArgs e)
    {
        if (sender is ToggleSwitch toggle)
        {
            ViewModel.SkipUpdateCheck = toggle.IsOn;
            ViewModel.SaveSettingsPublic();
        }
    }

    private void VerboseLoggingToggle_Toggled(object sender, RoutedEventArgs e)
    {
        if (sender is ToggleSwitch toggle)
        {
            ViewModel.VerboseLogging = toggle.IsOn;
            ViewModel.SaveSettingsPublic();
        }
    }

    private static readonly string PatchNotesDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "RenoDXCommander");

    private async Task ShowPatchNotesIfNewVersionAsync()
    {
        try
        {
            // Wait until XamlRoot is ready
            while (Content.XamlRoot == null)
                await Task.Delay(200);

            // Wait for UI to settle and any update dialog to finish
            await Task.Delay(1500);

            var current = Services.UpdateService.CurrentVersion;
            var versionStr = $"{current.Major}.{current.Minor}.{current.Build}";
            var markerFile = Path.Combine(PatchNotesDir, $"PatchNotes-{versionStr}.txt");

            // Clean up markers from older versions
            try
            {
                Directory.CreateDirectory(PatchNotesDir);
                foreach (var old in Directory.EnumerateFiles(PatchNotesDir, "PatchNotes-*.txt"))
                {
                    if (!old.Equals(markerFile, StringComparison.OrdinalIgnoreCase))
                    {
                        try { File.Delete(old); } catch { }
                    }
                }
            }
            catch { }

            // If marker exists, this version's notes have already been shown
            if (File.Exists(markerFile)) return;

            // Write the marker file FIRST — ensures we never show again
            try
            {
                Directory.CreateDirectory(PatchNotesDir);
                File.WriteAllText(markerFile, $"Patch notes shown for v{versionStr}");
            }
            catch (Exception ex)
            {
                CrashReporter.Log($"MainWindow: failed to write patch notes marker — {ex.Message}");
            }

            DispatcherQueue.TryEnqueue(async () =>
            {
                try
                {
                    await ShowPatchNotesDialogAsync();
                }
                catch (Exception ex)
                {
                    CrashReporter.Log($"MainWindow: patch notes dialog failed — {ex.Message}");
                }
            });
        }
        catch (Exception ex)
        {
            CrashReporter.Log($"MainWindow: patch notes check error — {ex.Message}");
        }
    }

    private async Task ShowPatchNotesDialogAsync()
    {
        var notes = ViewModels.MainViewModel.GetRecentPatchNotes(3);

        var markdown = new CommunityToolkit.WinUI.Controls.MarkdownTextBlock
        {
            Text = notes,
            Background = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Transparent),
            Foreground = Brush("TextSecondaryBrush"),
            FontSize = 12,
            UseEmphasisExtras = true,
            UseListExtras = true,
            UseTaskLists = true,
        };

        var scrollViewer = new ScrollViewer
        {
            MaxHeight = 500,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Content = markdown,
        };

        var dlg = new ContentDialog
        {
            Title              = "📋 Patch Notes — What's New",
            Content            = scrollViewer,
            CloseButtonText    = "Close",
            XamlRoot           = Content.XamlRoot,
            Background         = Brush("SurfaceToolbarBrush"),
        };

        await dlg.ShowAsync();
    }

    private async void PatchNotesLink_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            await ShowPatchNotesDialogAsync();
        }
        catch (Exception ex)
        {
            CrashReporter.Log($"MainWindow: patch notes dialog error — {ex.Message}");
        }
    }

    private void OpenLogsFolder_Click(object sender, RoutedEventArgs e)
    {
        var logsDir = System.IO.Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "RenoDXCommander", "logs");
        System.IO.Directory.CreateDirectory(logsDir);
        CrashReporter.Log("User opened logs folder from About panel");
        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(logsDir) { UseShellExecute = true });
    }

    private void OpenDownloadsFolder_Click(object sender, RoutedEventArgs e)
    {
        System.IO.Directory.CreateDirectory(ModInstallService.DownloadCacheDir);
        CrashReporter.Log("User opened downloads cache folder from About panel");
        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(ModInstallService.DownloadCacheDir) { UseShellExecute = true });
    }

    private void SettingsBack_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.NavigateToGameViewCommand.Execute(null);
        SettingsPanel.Visibility = Visibility.Collapsed;
        // Restore whichever panel was showing before Settings was opened
        if (ViewModel.IsLoading)
            LoadingPanel.Visibility = Visibility.Visible;
        else
            GameViewPanel.Visibility = Visibility.Visible;
    }

    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        ViewModel.SearchQuery = SearchBox.Text;
        // Always show the clear (✕) button
        VisualStateManager.GoToState(SearchBox, "ButtonVisible", true);
    }

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
                    new TextBlock { Text = "Enter the game name exactly as it appears on the wiki mod list:", TextWrapping = TextWrapping.Wrap, Foreground = Brush("TextSecondaryBrush") },
                    nameBox
                }
            },
            PrimaryButtonText   = "Pick Folder →",
            CloseButtonText     = "Cancel",
            XamlRoot            = Content.XamlRoot,
            Background          = Brush("SurfaceToolbarBrush"),
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
            e.DragUIOverride.Caption = "Drop to add game or install addon";
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

            var ext = file.FileType?.ToLowerInvariant() ?? "";

            // Handle .addon64 / .addon32 files — install RenoDX addon to a game
            if (ext is ".addon64" or ".addon32")
            {
                try
                {
                    await ProcessDroppedAddon(file.Path);
                }
                catch (Exception ex)
                {
                    CrashReporter.Log($"DragDrop addon: error processing '{file.Path}': {ex.Message}");
                }
                continue;
            }

            // Handle .exe files — add game
            if (!ext.Equals(".exe", StringComparison.OrdinalIgnoreCase)) continue;

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
                Background      = Brush("SurfaceToolbarBrush"),
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
            Text = "Game name:", Foreground = Brush("TextSecondaryBrush"),
        });
        confirmPanel.Children.Add(nameBox);
        confirmPanel.Children.Add(new TextBlock
        {
            Text = $"Engine: {engineLabel}\nInstall path: {installPath}",
            TextWrapping = TextWrapping.Wrap,
            Foreground   = Brush("TextTertiaryBrush"),
            FontSize     = 12, Margin = new Thickness(0, 6, 0, 0),
        });

        var confirmDialog = new ContentDialog
        {
            Title             = "➕ Add Dropped Game",
            Content           = confirmPanel,
            PrimaryButtonText = "Add Game",
            CloseButtonText   = "Cancel",
            XamlRoot          = Content.XamlRoot,
            Background        = Brush("SurfaceToolbarBrush"),
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
    /// Handles a dropped .addon64/.addon32 file — prompts the user to pick a game
    /// and installs the addon to that game's folder after confirmation.
    /// </summary>
    private async Task ProcessDroppedAddon(string addonPath)
    {
        var addonFileName = Path.GetFileName(addonPath);
        CrashReporter.Log($"DragDrop addon: received '{addonFileName}'");

        // Build a list of all detected games to choose from
        var cards = ViewModel.AllCards?.ToList() ?? new();
        if (cards.Count == 0)
        {
            var noGamesDialog = new ContentDialog
            {
                Title = "No Games Available",
                Content = "No games are currently detected. Add a game first.",
                CloseButtonText = "OK",
                XamlRoot = this.Content.XamlRoot,
            };
            await noGamesDialog.ShowAsync();
            return;
        }

        // Build a ComboBox for game selection
        var combo = new ComboBox
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            PlaceholderText = "Select a game...",
        };

        // Sort alphabetically and populate
        var sortedCards = cards.OrderBy(c => c.GameName, StringComparer.OrdinalIgnoreCase).ToList();
        foreach (var card in sortedCards)
            combo.Items.Add(new ComboBoxItem { Content = card.GameName, Tag = card });

        // Try to auto-select a game by matching addon filename to game names
        // e.g. "renodx-re9requiem.addon64" might fuzzy-match a game with "requiem" in the name
        var addonNameLower = Path.GetFileNameWithoutExtension(addonFileName).ToLowerInvariant();
        for (int i = 0; i < sortedCards.Count; i++)
        {
            // Check if the addon name contains a significant part of the game name
            string[] gameWords = sortedCards[i].GameName.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (gameWords.Length >= 2)
            {
                bool matched = false;
                foreach (var w in gameWords)
                {
                    if (w.Length > 3 && addonNameLower.Contains(w.ToLowerInvariant()))
                    {
                        matched = true;
                        break;
                    }
                }
                if (matched)
                {
                    combo.SelectedIndex = i;
                    break;
                }
            }
        }

        var panel = new StackPanel { Spacing = 12 };
        panel.Children.Add(new TextBlock
        {
            Text = $"Install {addonFileName} to a game folder.",
            TextWrapping = TextWrapping.Wrap,
            FontSize = 13,
            Foreground = Brush("TextSecondaryBrush"),
        });
        panel.Children.Add(combo);

        var pickDialog = new ContentDialog
        {
            Title = "📦 Install RenoDX Addon",
            Content = panel,
            PrimaryButtonText = "Next",
            CloseButtonText = "Cancel",
            XamlRoot = this.Content.XamlRoot,
            RequestedTheme = ElementTheme.Dark,
        };

        var pickResult = await pickDialog.ShowAsync();
        if (pickResult != ContentDialogResult.Primary) return;

        if (combo.SelectedItem is not ComboBoxItem selected || selected.Tag is not GameCardViewModel targetCard)
        {
            var noSelection = new ContentDialog
            {
                Title = "No Game Selected",
                Content = "Please select a game to install the addon to.",
                CloseButtonText = "OK",
                XamlRoot = this.Content.XamlRoot,
            };
            await noSelection.ShowAsync();
            return;
        }

        var gameName = targetCard.GameName;
        var installPath = targetCard.InstallPath;

        // Check for existing RenoDX addon files in the game folder
        string? existingAddon = null;
        try
        {
            var existing = Directory.GetFiles(installPath, "*.addon64")
                .Concat(Directory.GetFiles(installPath, "*.addon32"))
                .Where(f => !Path.GetFileName(f).StartsWith("zzz_display_commander", StringComparison.OrdinalIgnoreCase))
                .ToList();
            if (existing.Count > 0)
                existingAddon = string.Join(", ", existing.Select(Path.GetFileName));
        }
        catch { }

        // Confirmation dialog
        var warningText = $"Are you sure you want to install {addonFileName} for {gameName}?";
        if (!string.IsNullOrEmpty(existingAddon))
            warningText += $"\n\nThis will replace the existing addon: {existingAddon}";
        warningText += $"\n\nInstall path: {installPath}";

        var confirmDialog = new ContentDialog
        {
            Title = "⚠ Confirm Addon Install",
            Content = new TextBlock
            {
                Text = warningText,
                TextWrapping = TextWrapping.Wrap,
                FontSize = 13,
            },
            PrimaryButtonText = "Install",
            CloseButtonText = "Cancel",
            XamlRoot = this.Content.XamlRoot,
            RequestedTheme = ElementTheme.Dark,
        };

        var confirmResult = await confirmDialog.ShowAsync();
        if (confirmResult != ContentDialogResult.Primary) return;

        // Remove existing RenoDX addon files (not DC addons)
        try
        {
            var toRemove = Directory.GetFiles(installPath, "*.addon64")
                .Concat(Directory.GetFiles(installPath, "*.addon32"))
                .Where(f => !Path.GetFileName(f).StartsWith("zzz_display_commander", StringComparison.OrdinalIgnoreCase))
                .ToList();
            foreach (var f in toRemove)
            {
                CrashReporter.Log($"DragDrop addon: removing existing '{Path.GetFileName(f)}'");
                File.Delete(f);
            }
        }
        catch (Exception ex)
        {
            CrashReporter.Log($"DragDrop addon: failed to remove existing addons: {ex.Message}");
        }

        // Copy the addon file to the game folder
        var destPath = Path.Combine(installPath, addonFileName);
        try
        {
            File.Copy(addonPath, destPath, overwrite: true);
            CrashReporter.Log($"DragDrop addon: installed '{addonFileName}' to '{installPath}'");

            // Update card status
            targetCard.Status = GameStatus.Installed;
            targetCard.InstalledAddonFileName = addonFileName;
            targetCard.NotifyAll();

            var successDialog = new ContentDialog
            {
                Title = "✅ Addon Installed",
                Content = $"{addonFileName} has been installed for {gameName}.",
                CloseButtonText = "OK",
                XamlRoot = this.Content.XamlRoot,
                RequestedTheme = ElementTheme.Dark,
            };
            await successDialog.ShowAsync();
        }
        catch (Exception ex)
        {
            CrashReporter.Log($"DragDrop addon: install failed: {ex.Message}");
            var errDialog = new ContentDialog
            {
                Title = "❌ Install Failed",
                Content = $"Failed to install addon: {ex.Message}",
                CloseButtonText = "OK",
                XamlRoot = this.Content.XamlRoot,
            };
            await errDialog.ShowAsync();
        }
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
    /// EA, Xbox, Ubisoft, Unity, and Unreal games.
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

            // Ubisoft Connect markers
            if (File.Exists(Path.Combine(dirPath, "uplay_install.state"))
             || File.Exists(Path.Combine(dirPath, "upc.exe"))
             || Directory.GetFiles(dirPath, "uplay_*.dll").Length > 0)
                return true;

            // Battle.net / Blizzard markers
            if (File.Exists(Path.Combine(dirPath, ".build.info"))
             || File.Exists(Path.Combine(dirPath, ".product.db"))
             || File.Exists(Path.Combine(dirPath, "Blizzard Launcher.exe")))
                return true;

            // Rockstar Games Launcher markers
            if (File.Exists(Path.Combine(dirPath, "PlayGTAV.exe"))
             || File.Exists(Path.Combine(dirPath, "RockstarService.exe"))
             || Directory.GetFiles(dirPath, "socialclub*.dll").Length > 0)
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
        var active   = ((SolidColorBrush)Application.Current.Resources["ChipActiveBrush"]).Color;   // ChipActive
        var inactive = ((SolidColorBrush)Application.Current.Resources["ChipDefaultBrush"]).Color;   // ChipDefault
        var activeFg   = ((SolidColorBrush)Application.Current.Resources["TextPrimaryBrush"]).Color; // TextPrimary
        var inactiveFg = ((SolidColorBrush)Application.Current.Resources["ChipTextBrush"]).Color; // ChipText
        var tag = btn.Tag as string ?? "Detected";
        // Update both full-mode and compact-mode filter buttons
        foreach (var b in new[] { FilterFavourites, FilterDetected, FilterUnreal, FilterUnity, FilterOther, FilterRenoDX, FilterLuma, FilterHidden })
        {
            bool isActive = (b.Tag as string) == tag;
            b.Background  = new SolidColorBrush(isActive ? active   : inactive);
            b.Foreground  = new SolidColorBrush(isActive ? activeFg : inactiveFg);
        }
    }

    private void FavouriteButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button btn || btn.Tag is not GameCardViewModel card) return;
        ViewModel.ToggleFavouriteCommand.Execute(card);

        // Refresh the detail panel icon to reflect the new state
        DetailFavIcon.Text = card.IsFavourite ? "⭐" : "☆";
        DetailFavIcon.Foreground = new SolidColorBrush(card.IsFavourite
            ? ((SolidColorBrush)Application.Current.Resources["AccentAmberBrush"]).Color
            : ((SolidColorBrush)Application.Current.Resources["TextDisabledBrush"]).Color);
    }

    // ── Card handlers ─────────────────────────────────────────────────────────────

    private void ExpandComponents_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is GameCardViewModel card)
            card.ComponentExpanded = !card.ComponentExpanded;
    }

    private async void CombinedInstallButton_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is not GameCardViewModel card) return;
        if (string.IsNullOrEmpty(card.InstallPath) || !System.IO.Directory.Exists(card.InstallPath))
        {
            var folder = await PickFolderAsync();
            if (folder == null) return;
            card.InstallPath = folder;
            ViewModel.SaveLibraryPublic();
        }
        // Chain: RenoDX → DC → ReShade (skip components that are N/A)
        if (card.Mod?.SnapshotUrl != null)
            await ViewModel.InstallModCommand.ExecuteAsync(card);
        if (card.DcRowVisibility == Microsoft.UI.Xaml.Visibility.Visible)
            await ViewModel.InstallDcCommand.ExecuteAsync(card);
        if (card.ReShadeRowVisibility == Microsoft.UI.Xaml.Visibility.Visible)
            await ViewModel.InstallReShadeCommand.ExecuteAsync(card);
    }

    private async void InstallButton_Click(object sender, RoutedEventArgs e)
    {

        // If this is an external-only game, open the external URL instead
        var checkCard = GetCardFromSender(sender);
        if (checkCard?.IsExternalOnly == true)
        {
            ExternalLink_Click(sender, e);
            return;
        }
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

    private async void DeployShadersButton_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new ContentDialog
        {
            Title             = "Deploy Shaders",
            Content           = "Deploy the current shader mode to all installed games?",
            PrimaryButtonText = "Continue",
            CloseButtonText   = "Cancel",
            XamlRoot          = Content.XamlRoot,
        };
        if (await dlg.ShowAsync() == ContentDialogResult.Primary)
            ViewModel.DeployAllShaders();
    }

    private void DcModeButton_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.CycleDcMode();
    }

    private async void DeployDcModeButton_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new ContentDialog
        {
            Title             = "Deploy DC Mode",
            Content           = "Apply DC Mode file changes across all installed games?",
            PrimaryButtonText = "Continue",
            CloseButtonText   = "Cancel",
            XamlRoot          = Content.XamlRoot,
        };
        if (await dlg.ShowAsync() == ContentDialogResult.Primary)
            ViewModel.ApplyDcModeSwitch();
    }

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

    private void LumaToggle_Click(object sender, RoutedEventArgs e)
    {
        var card = (sender as FrameworkElement)?.Tag as GameCardViewModel;
        if (card != null) ViewModel.ToggleLumaMode(card);
    }

    private void SwitchToLumaButton_Click(object sender, RoutedEventArgs e)
    {
        var card = (sender as FrameworkElement)?.Tag as GameCardViewModel;
        if (card != null) ViewModel.ToggleLumaMode(card);
    }

    private async void InstallLumaButton_Click(object sender, RoutedEventArgs e)
    {
        var card = (sender as FrameworkElement)?.Tag as GameCardViewModel;
        if (card != null) await ViewModel.InstallLumaAsync(card);
    }

    private void UninstallLumaButton_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is GameCardViewModel card)
            ViewModel.UninstallLumaCommand.Execute(card);
    }

    private void UeExtendedFlyoutItem_Click(object sender, RoutedEventArgs e)
    {
        var card = (sender as FrameworkElement)?.Tag as GameCardViewModel;
        if (card == null) return;

        ViewModel.ToggleUeExtended(card);

        // Directly update the badge text based on the new state
        string newLabel = card.UseUeExtended ? "Extended UE" : "Generic UE";
        DetailGenericText.Text = newLabel;

        // Update the UE button styling
        if (card.UseUeExtended)
        {
            DetailUeExtendedBtn.Background = Brush("AccentGreenBgBrush");
            DetailUeExtendedBtn.Foreground = Brush("AccentGreenBrush");
            DetailUeExtendedBtn.BorderBrush = Brush("AccentGreenBorderBrush");
        }
        else
        {
            DetailUeExtendedBtn.Background = Brush("SurfaceOverlayBrush");
            DetailUeExtendedBtn.Foreground = Brush("TextSecondaryBrush");
            DetailUeExtendedBtn.BorderBrush = Brush("BorderStrongBrush");
        }

        // Update flyout item text
        DetailUeExtendedItem.Text = card.UseUeExtended ? "Disable UE Extended" : "Enable UE Extended";

        // Show inline message
        if (card.UseUeExtended)
        {
            DetailRsMessage.Text = "⚡ UE-Extended enabled — check Discord to confirm this game is compatible.";
            DetailRsMessage.Foreground = Brush("AccentPurpleBrush");
            DetailRsMessage.Visibility = Visibility.Visible;
        }
        else
        {
            DetailRsMessage.Text = "UE-Extended disabled.";
            DetailRsMessage.Foreground = Brush("TextTertiaryBrush");
            DetailRsMessage.Visibility = Visibility.Visible;
        }
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
        if (folder != null)
        {
            card.InstallPath = folder;
            if (card.DetectedGame != null)
                card.DetectedGame.InstallPath = folder;
            // Persist the override so it survives Refresh / app restart
            ViewModel.SetFolderOverride(card.GameName, folder);
        }
    }

    private void OpenFolder_Click(object sender, RoutedEventArgs e)
    {
        var card = GetCardFromSender(sender);
        if (card == null || string.IsNullOrEmpty(card.InstallPath)) return;
        if (System.IO.Directory.Exists(card.InstallPath))
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(card.InstallPath) { UseShellExecute = true });
    }

    private void RemoveManualGame_Click(object sender, RoutedEventArgs e)
    {
        var card = GetCardFromSender(sender);
        if (card == null) return;

        if (card.IsManuallyAdded)
        {
            // Manual game — remove it entirely
            ViewModel.RemoveManualGameCommand.Execute(card);
        }
        else
        {
            // Auto-detected game — reset folder to original detected path
            ViewModel.ResetFolderOverride(card);
        }
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

        var textColour = Brush("TextSecondaryBrush");
        var linkColour = Brush("AccentBlueBrush");
        var dimColour  = Brush("TextTertiaryBrush");

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

        // ── Luma info (when in Luma mode) ───────────────────────────────────────────
        if (card.IsLumaMode && (card.LumaMod != null || !string.IsNullOrWhiteSpace(card.LumaNotes)))
        {
            var lumaLabel = card.LumaMod != null
                ? $"Luma — {card.LumaMod.Status} {card.LumaMod.Author}"
                : "Luma mode";
            var lumaBadge = new Border
            {
                CornerRadius    = new CornerRadius(6),
                Padding         = new Thickness(10, 4, 10, 4),
                HorizontalAlignment = HorizontalAlignment.Left,
                Background      = Brush("AccentGreenBgBrush"),
                BorderBrush     = Brush("AccentGreenBorderBrush"),
                BorderThickness = new Thickness(1),
                Child = new TextBlock
                {
                    Text       = lumaLabel,
                    FontSize   = 12,
                    Foreground = Brush("AccentGreenBrush"),
                }
            };
            outerPanel.Children.Add(lumaBadge);

            var lumaNotesText = "";
            if (card.LumaMod != null)
            {
                if (!string.IsNullOrWhiteSpace(card.LumaMod.SpecialNotes))
                    lumaNotesText += card.LumaMod.SpecialNotes;
                if (!string.IsNullOrWhiteSpace(card.LumaMod.FeatureNotes))
                {
                    if (lumaNotesText.Length > 0) lumaNotesText += "\n\n";
                    lumaNotesText += card.LumaMod.FeatureNotes;
                }
            }

            if (!string.IsNullOrWhiteSpace(lumaNotesText))
            {
                outerPanel.Children.Add(new TextBlock
                {
                    Text         = lumaNotesText,
                    TextWrapping = TextWrapping.Wrap,
                    Foreground   = textColour,
                    FontSize     = 13,
                    LineHeight   = 22,
                });
            }

            // ── Manifest Luma notes (supplement wiki notes) ──────────────────────
            if (!string.IsNullOrWhiteSpace(card.LumaNotes))
            {
                if (!string.IsNullOrEmpty(card.LumaNotesUrl))
                {
                    var para = new Microsoft.UI.Xaml.Documents.Paragraph();
                    para.Inlines.Add(new Microsoft.UI.Xaml.Documents.Run
                    {
                        Text       = card.LumaNotes,
                        Foreground = textColour,
                        FontSize   = 13,
                    });
                    para.Inlines.Add(new Microsoft.UI.Xaml.Documents.LineBreak());
                    var link = new Microsoft.UI.Xaml.Documents.Hyperlink
                    {
                        NavigateUri = new Uri(card.LumaNotesUrl),
                        Foreground  = linkColour,
                    };
                    link.Inlines.Add(new Microsoft.UI.Xaml.Documents.Run
                    {
                        Text     = card.LumaNotesUrlLabel ?? card.LumaNotesUrl,
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
                        Text         = card.LumaNotes,
                        TextWrapping = TextWrapping.Wrap,
                        Foreground   = textColour,
                        FontSize     = 13,
                        LineHeight   = 22,
                    });
                }
            }

            // Fallback if neither wiki nor manifest provided notes
            if (string.IsNullOrWhiteSpace(lumaNotesText) && string.IsNullOrWhiteSpace(card.LumaNotes))
            {
                outerPanel.Children.Add(new TextBlock
                {
                    Text       = "No additional Luma notes for this game.",
                    Foreground = dimColour,
                    FontSize   = 13,
                });
            }
        }
        // ── Standard RenoDX notes ───────────────────────────────────────────────────
        else if (!string.IsNullOrWhiteSpace(card.Notes))
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
            MaxHeight = 440,
            Padding   = new Thickness(0, 4, 12, 0),
        };

        var dialog = new ContentDialog
        {
            Title           = $"ℹ  {card.GameName}",
            Content         = scrollContent,
            CloseButtonText = "Close",
            XamlRoot        = Content.XamlRoot,
            Background      = Brush("SurfaceToolbarBrush"),
        };
        await dialog.ShowAsync();
    }

    /// <summary>Creates a thin horizontal separator line for dialogs.</summary>
    private static Border MakeSeparator() => new()
    {
        Height = 1,
        Background = (SolidColorBrush)Application.Current.Resources["BorderSubtleBrush"],
        Margin = new Thickness(0, 2, 0, 2),
    };

    /// <summary>Looks up a SolidColorBrush from the merged theme resource dictionaries.</summary>
    private SolidColorBrush Brush(string key) =>
        (SolidColorBrush)Application.Current.Resources[key];

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
    // Stores window bounds so the window remembers its last size/position.

    private static readonly string _windowSettingsPath = System.IO.Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "RenoDXCommander", "window_main.json");

    // In-memory cache of window bounds (populated from file on first restore)
    private (int X, int Y, int W, int H)? _windowBounds;

    private void TryRestoreWindowBounds()
    {
        try
        {
            if (!System.IO.File.Exists(_windowSettingsPath)) return;
            var json = System.IO.File.ReadAllText(_windowSettingsPath);
            var doc  = System.Text.Json.JsonDocument.Parse(json).RootElement;

            // Try unified bounds first (new format)
            if (doc.TryGetProperty("X", out var ux) && doc.TryGetProperty("Y", out var uy) &&
                doc.TryGetProperty("W", out var uw) && doc.TryGetProperty("H", out var uh))
                _windowBounds = (ux.GetInt32(), uy.GetInt32(), uw.GetInt32(), uh.GetInt32());

            // Legacy migration: old format stored FullX/FullY or CompactX/CompactY — prefer Compact (closest to new layout)
            if (_windowBounds == null &&
                doc.TryGetProperty("CompactX", out var cx) && doc.TryGetProperty("CompactY", out var cy) &&
                doc.TryGetProperty("CompactW", out var cw) && doc.TryGetProperty("CompactH", out var ch))
                _windowBounds = (cx.GetInt32(), cy.GetInt32(), cw.GetInt32(), ch.GetInt32());

            if (_windowBounds == null &&
                doc.TryGetProperty("FullX", out var fx) && doc.TryGetProperty("FullY", out var fy) &&
                doc.TryGetProperty("FullW", out var fw) && doc.TryGetProperty("FullH", out var fh))
                _windowBounds = (fx.GetInt32(), fy.GetInt32(), fw.GetInt32(), fh.GetInt32());

            // Apply the bounds
            if (_windowBounds is var (x, y, w, h) && w >= 400 && h >= 300 && w <= 7680 && h <= 4320)
            {
                var hwnd = WindowNative.GetWindowHandle(this);
                SetWindowPos(hwnd, IntPtr.Zero, x, y, w, h, 0x0040 /* SWP_NOZORDER */);
            }
        }
        catch { }
    }

    /// <summary>Captures the current window rect into the in-memory cache.</summary>
    private void CaptureCurrentBounds()
    {
        try
        {
            var hwnd = WindowNative.GetWindowHandle(this);
            if (!GetWindowRect(hwnd, out var r)) return;
            var w = r.Right - r.Left;
            var h = r.Bottom - r.Top;
            if (w < 100 || h < 100) return;
            _windowBounds = (r.Left, r.Top, w, h);
        }
        catch { }
    }

    /// <summary>Restores the cached window bounds, if available.</summary>
    private void RestoreWindowBounds()
    {
        try
        {
            if (_windowBounds is var (x, y, w, h) && w >= 400 && h >= 300 && w <= 7680 && h <= 4320)
            {
                var hwnd = WindowNative.GetWindowHandle(this);
                SetWindowPos(hwnd, IntPtr.Zero, x, y, w, h, 0x0040 /* SWP_NOZORDER */);
            }
        }
        catch { }
    }

    private void SaveWindowBounds()
    {
        try
        {
            // Capture final bounds
            CaptureCurrentBounds();

            System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(_windowSettingsPath)!);
            var data = new Dictionary<string, int>();
            if (_windowBounds is var (x, y, w, h))
            {
                data["X"] = x; data["Y"] = y; data["W"] = w; data["H"] = h;
            }
            var json = System.Text.Json.JsonSerializer.Serialize(data);
            System.IO.File.WriteAllText(_windowSettingsPath, json);
        }
        catch { }
    }

    // ── Page visibility management ──────────────────────────────────────────────

    private void UpdatePageVisibility()
    {
        // Show the correct panel based on current page and loading state
        if (ViewModel.CurrentPage == AppPage.Settings)
        {
            GameViewPanel.Visibility = Visibility.Collapsed;
            SettingsPanel.Visibility = Visibility.Visible;
            LoadingPanel.Visibility = Visibility.Collapsed;
        }
        else if (ViewModel.IsLoading)
        {
            GameViewPanel.Visibility = Visibility.Collapsed;
            SettingsPanel.Visibility = Visibility.Collapsed;
            // LoadingPanel is already Visible by default
        }
        else
        {
            GameViewPanel.Visibility = Visibility.Visible;
            SettingsPanel.Visibility = Visibility.Collapsed;
            LoadingPanel.Visibility = Visibility.Collapsed;
        }

        // Auto-select first game if nothing is selected
        if (GameList.SelectedItem == null && ViewModel.DisplayedGames.Count > 0)
        {
            GameList.SelectedItem = ViewModel.DisplayedGames[0];
        }
    }

    private void GameList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (GameList.SelectedItem is GameCardViewModel card)
            {
                ViewModel.SelectedGame = card;
                PopulateDetailPanel(card);
                DetailPanel.Visibility = Visibility.Visible;
                BuildOverridesPanel(card);
                OverridesContainer.Visibility = Visibility.Visible;
            }
            else
            {
                ViewModel.SelectedGame = null;
                DetailPanel.Visibility = Visibility.Collapsed;
                OverridesPanel.Children.Clear();
                OverridesContainer.Visibility = Visibility.Collapsed;
            }
        }


    private GameCardViewModel? _currentDetailCard;

    private void PopulateDetailPanel(GameCardViewModel card)
    {
        // Unsubscribe from previous card
        if (_currentDetailCard != null)
            _currentDetailCard.PropertyChanged -= DetailCard_PropertyChanged;

        _currentDetailCard = card;
        card.PropertyChanged += DetailCard_PropertyChanged;

        // Header
        DetailGameName.Text = card.GameName;

        // Source badge
        if (card.HasSourceIcon)
        {
            DetailSourceIcon.Source = new Microsoft.UI.Xaml.Media.Imaging.BitmapImage(card.SourceIconUri);
            DetailSourceIcon.Visibility = Visibility.Visible;
        }
        else
        {
            DetailSourceIcon.Visibility = Visibility.Collapsed;
        }
        DetailSourceText.Text = card.Source;
        DetailSourceBadge.Visibility = string.IsNullOrEmpty(card.Source) ? Visibility.Collapsed : Visibility.Visible;

        // Engine badge
        if (!string.IsNullOrEmpty(card.EngineHint))
        {
            DetailEngineText.Text = card.EngineHint;
            DetailEngineBadge.Visibility = Visibility.Visible;
        }
        else DetailEngineBadge.Visibility = Visibility.Collapsed;

        // Generic badge
        if (card.IsGenericMod)
        {
            DetailGenericText.Text = card.GenericModLabel;
            DetailGenericBadge.Visibility = Visibility.Visible;
        }
        else DetailGenericBadge.Visibility = Visibility.Collapsed;

        // 32-bit badge
        Detail32BitBadge.Visibility = card.Is32Bit ? Visibility.Visible : Visibility.Collapsed;

        // Wiki status badge
        DetailWikiText.Text = card.WikiStatusLabel;
        DetailWikiText.Foreground = new SolidColorBrush(ParseHexColor(card.WikiStatusBadgeForeground));
        DetailWikiBadge.Background = new SolidColorBrush(ParseHexColor(card.WikiStatusBadgeBackground));
        DetailWikiBadge.BorderBrush = new SolidColorBrush(ParseHexColor(card.WikiStatusBadgeBorderBrush));
        DetailWikiBadge.BorderThickness = new Thickness(1);

        // Install path + installed file
        DetailInstallPath.Text = card.InstallPath;
        if (!string.IsNullOrEmpty(card.InstalledAddonFileName))
        {
            DetailInstalledFile.Text = $"📦 {card.InstalledAddonFileName}";
            DetailInstalledFile.Visibility = Visibility.Visible;
        }
        else DetailInstalledFile.Visibility = Visibility.Collapsed;

        // Utility buttons — set Tag for event handlers
        DetailFavBtn.Tag = card;
        DetailFavIcon.Text = card.IsFavourite ? "⭐" : "☆";
        DetailFavIcon.Foreground = new SolidColorBrush(card.IsFavourite
            ? ((SolidColorBrush)Application.Current.Resources["AccentAmberBrush"]).Color
            : ((SolidColorBrush)Application.Current.Resources["TextDisabledBrush"]).Color);

        DetailDiscussionBtn.Tag = card;
        DetailDiscussionBtn.Visibility = card.NameLinkVisibility;

        DetailNotesBtn.Tag = card;
        DetailNotesBtn.Visibility = card.NotesButtonVisibility;

        DetailHideBtn.Tag = card;
        ToolTipService.SetToolTip(DetailHideBtn, card.HideButtonLabel);
        DetailHideIcon.Text = card.IsHidden ? "🚫" : "🚫";
        DetailHideIcon.Foreground = card.IsHidden
            ? Brush("AccentPurpleDimBrush")
            : Brush("TextDisabledBrush");

        // Folder menu items
        DetailOpenFolder.Tag = card;
        DetailBrowseFolder.Tag = card;
        DetailRemoveGame.Tag = card;

        // Luma badge toggle
        if (card.LumaBadgeVisibility == Visibility.Visible)
        {
            DetailLumaBadgeContainer.Visibility = Visibility.Visible;
            DetailLumaToggle.Tag = card;
            DetailLumaToggle.IsChecked = card.IsLumaMode;
            DetailLumaToggle.Background = new SolidColorBrush(ParseHexColor(card.LumaBadgeBackground));
            DetailLumaToggle.BorderBrush = new SolidColorBrush(ParseHexColor(card.LumaBadgeBorderBrush));
            DetailLumaToggleText.Text = card.LumaBadgeLabel;
            DetailLumaToggleText.Foreground = new SolidColorBrush(ParseHexColor(card.LumaBadgeForeground));
        }
        else DetailLumaBadgeContainer.Visibility = Visibility.Collapsed;

        // Populate component rows
        UpdateDetailComponentRows(card);
    }

    private void UpdateDetailComponentRows(GameCardViewModel card)
    {
        bool isLumaMode = card.LumaFeatureEnabled && card.IsLumaMode;

        // ReShade row
        DetailRsRow.Visibility = isLumaMode ? Visibility.Collapsed : Visibility.Visible;
        if (!isLumaMode)
        {
            DetailRsStatus.Text = card.RsStatusText;
            DetailRsStatus.Foreground = new SolidColorBrush(ParseHexColor(card.RsStatusColor));
            DetailRsInstallBtn.Tag = card;
            DetailRsInstallBtn.Content = card.RsActionLabel;
            DetailRsInstallBtn.IsEnabled = card.IsRsNotInstalling;
            DetailRsInstallBtn.Background = new SolidColorBrush(ParseHexColor(card.RsBtnBackground));
            DetailRsInstallBtn.Foreground = new SolidColorBrush(ParseHexColor(card.RsBtnForeground));
            DetailRsInstallBtn.BorderBrush = new SolidColorBrush(ParseHexColor(card.RsBtnBorderBrush));
            DetailRsInstallBtn.BorderThickness = new Thickness(1);
            DetailRsIniBtn.Tag = card;
            DetailRsIniBtn.IsEnabled = card.RsIniExists;
            DetailRsIniCopy.Tag = card;
            DetailRsIniCopy.Visibility = card.RsIniExists ? Visibility.Visible : Visibility.Collapsed;
            DetailRsDeleteBtn.Tag = card;
            DetailRsDeleteBtn.Visibility = card.RsDeleteVisibility;
        }

        // DC row
        DetailDcRow.Visibility = isLumaMode ? Visibility.Collapsed : Visibility.Visible;
        if (!isLumaMode)
        {
            DetailDcStatus.Text = card.DcStatusText;
            DetailDcStatus.Foreground = new SolidColorBrush(ParseHexColor(card.DcStatusColor));
            DetailDcInstallBtn.Tag = card;
            DetailDcInstallBtn.Content = card.DcActionLabel;
            DetailDcInstallBtn.IsEnabled = card.IsDcNotInstalling;
            DetailDcInstallBtn.Background = new SolidColorBrush(ParseHexColor(card.DcBtnBackground));
            DetailDcInstallBtn.Foreground = new SolidColorBrush(ParseHexColor(card.DcBtnForeground));
            DetailDcInstallBtn.BorderBrush = new SolidColorBrush(ParseHexColor(card.DcBtnBorderBrush));
            DetailDcInstallBtn.BorderThickness = new Thickness(1);
            DetailDcIniBtn.Tag = card;
            DetailDcIniBtn.IsEnabled = card.DcIniExists;
            DetailDcIniCopy.Tag = card;
            DetailDcIniCopy.Visibility = card.DcIniExists ? Visibility.Visible : Visibility.Collapsed;
            DetailDcDeleteBtn.Tag = card;
            DetailDcDeleteBtn.Visibility = card.DcDeleteVisibility;
        }

        // RenoDX row (also used for external-only / Discord link)
        bool showRdx = !isLumaMode;
        DetailRdxRow.Visibility = showRdx ? Visibility.Visible : Visibility.Collapsed;
        if (showRdx)
        {
            DetailRdxInstallBtn.Tag = card;
            if (card.IsExternalOnly)
            {
                DetailRdxStatus.Text = "";
                DetailRdxInstallBtn.Content = card.ExternalLabel;
                DetailRdxInstallBtn.IsEnabled = true;
                DetailRdxInstallBtn.Background = Brush("AccentBlueBgBrush");
                DetailRdxInstallBtn.Foreground = Brush("AccentBlueBrush");
                DetailRdxInstallBtn.BorderBrush = Brush("AccentBlueBorderBrush");
                DetailRdxInstallBtn.BorderThickness = new Thickness(1);
                DetailRdxDeleteBtn.Visibility = Visibility.Collapsed;
            }
            else
            {
                DetailRdxStatus.Text = card.RdxStatusText;
                DetailRdxStatus.Foreground = new SolidColorBrush(ParseHexColor(card.RdxStatusColor));
                DetailRdxInstallBtn.Content = card.InstallActionLabel;
                DetailRdxInstallBtn.IsEnabled = card.CanInstall;
                DetailRdxInstallBtn.Background = new SolidColorBrush(ParseHexColor(card.InstallBtnBackground));
                DetailRdxInstallBtn.Foreground = new SolidColorBrush(ParseHexColor(card.InstallBtnForeground));
                DetailRdxInstallBtn.BorderBrush = new SolidColorBrush(ParseHexColor(card.InstallBtnBorderBrush));
                DetailRdxInstallBtn.BorderThickness = new Thickness(1);
                DetailRdxDeleteBtn.Tag = card;
                DetailRdxDeleteBtn.Visibility = card.ReinstallRowVisibility;
            }
        }

        // Luma row
        if (isLumaMode)
        {
            DetailLumaRow.Visibility = Visibility.Visible;
            DetailLumaStatus.Text = card.LumaActionLabel.Replace("↺  ", "").Replace("⬇  ", "");
            DetailLumaStatus.Foreground = new SolidColorBrush(
                card.LumaStatus == GameStatus.Installed ? ((SolidColorBrush)Application.Current.Resources["AccentGreenBrush"]).Color
                : ((SolidColorBrush)Application.Current.Resources["TextSecondaryBrush"]).Color);
            DetailLumaInstallBtn.Tag = card;
            DetailLumaInstallBtn.Content = card.LumaActionLabel;
            DetailLumaInstallBtn.IsEnabled = card.IsLumaNotInstalling;
            DetailLumaDeleteBtn.Tag = card;
            DetailLumaDeleteBtn.Visibility = card.LumaReinstallVisibility;
        }
        else DetailLumaRow.Visibility = Visibility.Collapsed;

        // UE-Extended flyout (inline in RenoDX row, column 3)
        if (card.UeExtendedToggleVisibility == Visibility.Visible && !isLumaMode)
        {
            DetailUeExtendedBtn.Visibility = Visibility.Visible;
            DetailUeExtendedBtn.Tag = card;
            DetailUeExtendedItem.Tag = card;
            DetailUeExtendedItem.Text = card.UseUeExtended ? "Disable UE Extended" : "Enable UE Extended";
            // Visual indicator: green when enabled, default when off
            if (card.UseUeExtended)
            {
                DetailUeExtendedBtn.Background = Brush("AccentGreenBgBrush");
                DetailUeExtendedBtn.Foreground = Brush("AccentGreenBrush");
                DetailUeExtendedBtn.BorderBrush = Brush("AccentGreenBorderBrush");
            }
            else
            {
                DetailUeExtendedBtn.Background = Brush("SurfaceOverlayBrush");
                DetailUeExtendedBtn.Foreground = Brush("TextSecondaryBrush");
                DetailUeExtendedBtn.BorderBrush = Brush("BorderStrongBrush");
            }
        }
        else DetailUeExtendedBtn.Visibility = Visibility.Collapsed;

        // External-only link is now shown inline in the RenoDX row

        // No mod message
        DetailNoModMsg.Visibility = card.NoModVisibility;

        // Progress bars
        DetailRsProgress.Visibility = card.RsProgressVisibility;
        DetailRsProgress.Value = card.RsProgress;
        DetailRsMessage.Visibility = card.RsMessageVisibility;
        DetailRsMessage.Text = card.RsActionMessage;
        DetailDcProgress.Visibility = card.DcProgressVisibility;
        DetailDcProgress.Value = card.DcProgress;
        DetailDcMessage.Visibility = card.DcMessageVisibility;
        DetailDcMessage.Text = card.DcActionMessage;
        DetailRdxProgress.Visibility = card.ProgressVisibility;
        DetailRdxProgress.Value = card.InstallProgress;
        DetailRdxMessage.Visibility = card.MessageVisibility;
        DetailRdxMessage.Text = card.ActionMessage;
        DetailLumaProgress.Visibility = card.LumaProgressVisibility;
        DetailLumaProgress.Value = card.LumaProgress;
        DetailLumaMessage.Visibility = card.LumaMessageVisibility;
        DetailLumaMessage.Text = card.LumaActionMessage;
    }

    private void DetailCard_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (_currentDetailCard == null) return;
        DispatcherQueue.TryEnqueue(() =>
        {
            if (_currentDetailCard != null)
                UpdateDetailComponentRows(_currentDetailCard);
        });
    }

    private static Windows.UI.Color ParseHexColor(string hex)
    {
        hex = hex.TrimStart('#');
        if (hex.Length == 6)
            return Windows.UI.Color.FromArgb(255,
                byte.Parse(hex[..2], System.Globalization.NumberStyles.HexNumber),
                byte.Parse(hex[2..4], System.Globalization.NumberStyles.HexNumber),
                byte.Parse(hex[4..6], System.Globalization.NumberStyles.HexNumber));
        if (hex.Length == 8)
            return Windows.UI.Color.FromArgb(
                byte.Parse(hex[..2], System.Globalization.NumberStyles.HexNumber),
                byte.Parse(hex[2..4], System.Globalization.NumberStyles.HexNumber),
                byte.Parse(hex[4..6], System.Globalization.NumberStyles.HexNumber),
                byte.Parse(hex[6..8], System.Globalization.NumberStyles.HexNumber));
        return Windows.UI.Color.FromArgb(255, 128, 128, 128);
    }


    private void BuildOverridesPanel(GameCardViewModel card)
    {
        OverridesPanel.Children.Clear();

        var gameName = card.GameName;
        bool isLumaMode = ViewModel.IsLumaEnabled(gameName);

        // ── Title ────────────────────────────────────────────────────────────────
        OverridesPanel.Children.Add(new TextBlock
        {
            Text = "⚙ Overrides",
            FontSize = 13,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            Foreground = Brush("TextSecondaryBrush"),
        });

        // ── Game name + Wiki name ────────────────────────────────────────────────
        var detectedBox = new TextBox
        {
            Header = "Game name (editable)",
            Text = gameName,
            FontSize = 12,
            HorizontalAlignment = HorizontalAlignment.Stretch,
        };
        var wikiBox = new TextBox
        {
            Header = "Wiki mod name",
            PlaceholderText = "Exact wiki name",
            Text = ViewModel.GetNameMapping(gameName) ?? "",
            FontSize = 12,
            HorizontalAlignment = HorizontalAlignment.Stretch,
        };
        var originalStoreName = ViewModel.GetOriginalStoreName(gameName);
        var resetBtn = new Button
        {
            Content = "↩ Reset",
            FontSize = 12,
            VerticalAlignment = VerticalAlignment.Bottom,
            Padding = new Thickness(10, 6, 10, 6),
            Background = Brush("SurfaceOverlayBrush"),
            Foreground = Brush("TextSecondaryBrush"),
            BorderBrush = Brush("BorderDefaultBrush"),
        };
        ToolTipService.SetToolTip(resetBtn,
            "Reset game name back to auto-detected and clear wiki name mapping.");
        resetBtn.Click += (s, ev) =>
        {
            detectedBox.Text = originalStoreName ?? gameName;
            wikiBox.Text = "";
        };

        var nameGrid = new Grid { ColumnSpacing = 8 };
        nameGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        nameGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        nameGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        Grid.SetColumn(detectedBox, 0);
        Grid.SetColumn(wikiBox, 1);
        Grid.SetColumn(resetBtn, 2);
        nameGrid.Children.Add(detectedBox);
        nameGrid.Children.Add(wikiBox);
        nameGrid.Children.Add(resetBtn);
        OverridesPanel.Children.Add(nameGrid);
        OverridesPanel.Children.Add(MakeSeparator());

        // ── DLL naming override ──────────────────────────────────────────────────
        bool isDllOverride = ViewModel.HasDllOverride(gameName);
        var existingCfg = ViewModel.GetDllOverride(gameName);
        bool is32Bit = ViewModel.Is32BitGame(gameName);
        var defaultRsName = is32Bit ? "ReShade32.dll" : "ReShade64.dll";
        var defaultDcName = is32Bit ? "zzz_display_commander.addon32" : "zzz_display_commander.addon64";

        var dllOverrideBtn = new ToggleButton
        {
            Content = "📝  DLL naming override",
            IsChecked = isDllOverride,
            IsEnabled = !isLumaMode,
            FontSize = 12,
            Padding = new Thickness(10, 6, 10, 6),
            Background = Brush("AccentTealBgBrush"),
            Foreground = Brush("AccentTealBrush"),
            BorderBrush = Brush("AccentTealBorderBrush"),
            HorizontalAlignment = HorizontalAlignment.Stretch,
        };
        ToolTipService.SetToolTip(dllOverrideBtn,
            "Override the filenames ReShade and Display Commander are installed as. " +
            "When enabled, existing RS/DC files are renamed to the custom filenames. " +
            "The game is automatically excluded from DC Mode, Update All, and global shaders.");
        var rsNameBox = new TextBox
        {
            PlaceholderText = defaultRsName,
            Text = existingCfg?.ReShadeFileName ?? "",
            Header = "ReShade filename",
            FontSize = 12,
            IsEnabled = isDllOverride,
            HorizontalAlignment = HorizontalAlignment.Stretch,
        };
        var dcNameBox = new TextBox
        {
            PlaceholderText = defaultDcName,
            Text = existingCfg?.DcFileName ?? "",
            Header = "DC filename",
            FontSize = 12,
            IsEnabled = isDllOverride,
            HorizontalAlignment = HorizontalAlignment.Stretch,
        };
        dllOverrideBtn.Checked   += (s, ev) => { rsNameBox.IsEnabled = true;  dcNameBox.IsEnabled = true; };
        dllOverrideBtn.Unchecked += (s, ev) => { rsNameBox.IsEnabled = false; dcNameBox.IsEnabled = false; };

        OverridesPanel.Children.Add(dllOverrideBtn);
        var dllNameGrid = new Grid { ColumnSpacing = 8, Margin = new Thickness(0, 4, 0, 0) };
        dllNameGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        dllNameGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        Grid.SetColumn(rsNameBox, 0);
        Grid.SetColumn(dcNameBox, 1);
        dllNameGrid.Children.Add(rsNameBox);
        dllNameGrid.Children.Add(dcNameBox);
        OverridesPanel.Children.Add(dllNameGrid);
        OverridesPanel.Children.Add(MakeSeparator());

        // ── Exclude from Update All ──────────────────────────────────────────────
        var uaExcludeBtn = new ToggleButton
        {
            Content = "⬆  Exclude from Update All",
            IsChecked = ViewModel.IsUpdateAllExcluded(gameName),
            FontSize = 12,
            Padding = new Thickness(10, 6, 10, 6),
            Background = Brush("AccentPurpleBgBrush"),
            Foreground = Brush("AccentPurpleBrush"),
            BorderBrush = Brush("AccentPurpleBorderBrush"),
            HorizontalAlignment = HorizontalAlignment.Stretch,
        };
        ToolTipService.SetToolTip(uaExcludeBtn,
            "Skip this game when using Update All RenoDX, Update All ReShade, or Update All DC.");
        OverridesPanel.Children.Add(uaExcludeBtn);
        OverridesPanel.Children.Add(MakeSeparator());

        // ── 32-bit mode ──────────────────────────────────────────────────────────
        var bit32Btn = new ToggleButton
        {
            Content = "⚠  32-bit mode",
            IsChecked = is32Bit,
            IsEnabled = !isLumaMode,
            FontSize = 12,
            Padding = new Thickness(10, 6, 10, 6),
            Background = Brush("AccentAmberBgBrush"),
            Foreground = Brush("AccentAmberBrush"),
            BorderBrush = Brush("BorderDefaultBrush"),
            HorizontalAlignment = HorizontalAlignment.Stretch,
        };
        ToolTipService.SetToolTip(bit32Btn,
            "Installs 32-bit versions of ReShade, Unity addon, and Display Commander. Only enable if you know this game is 32-bit.");
        OverridesPanel.Children.Add(bit32Btn);
        OverridesPanel.Children.Add(MakeSeparator());

        // ── Per-game DC Mode + Shader mode (side by side) ─────────────────────────
        int? currentDcMode = ViewModel.GetPerGameDcModeOverride(gameName);
        var dcModeOptions = new[] { "Follow Global", "Exclude (Off)", "DC Mode 1", "DC Mode 2" };
        var dcModeCombo = new ComboBox
        {
            ItemsSource = dcModeOptions,
            SelectedIndex = currentDcMode switch { null => 0, 0 => 1, 1 => 2, 2 => 3, _ => 0 },
            FontSize = 12,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Header = "DC Mode",
        };
        ToolTipService.SetToolTip(dcModeCombo,
            "Follow Global = use the header DC Mode toggle. Exclude (Off) = always use normal naming. " +
            "DC Mode 1 = force dxgi.dll proxy. DC Mode 2 = force winmm.dll proxy.");

        string currentShaderMode = ViewModel.GetPerGameShaderMode(gameName);
        var shaderModeOptions = new[] { "Global", "Off", "Minimum", "All", "User" };
        var shaderModeCombo = new ComboBox
        {
            ItemsSource = shaderModeOptions,
            SelectedItem = currentShaderMode,
            FontSize = 12,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Header = "Shader Mode",
        };
        ToolTipService.SetToolTip(shaderModeCombo,
            "Global = follow the header toggle. Off = no shaders. Minimum = Lilium only. All = all packs. User = custom folder only.\n" +
            "Note: Per-game shader mode only applies when ReShade is used standalone (DC Mode OFF). " +
            "When DC Mode is ON, all DC-mode games share the DC global shader folder.");

        var modeGrid = new Grid { ColumnSpacing = 8 };
        modeGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        modeGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        Grid.SetColumn(dcModeCombo, 0);
        Grid.SetColumn(shaderModeCombo, 1);
        modeGrid.Children.Add(dcModeCombo);
        modeGrid.Children.Add(shaderModeCombo);
        OverridesPanel.Children.Add(modeGrid);
        OverridesPanel.Children.Add(MakeSeparator());

        // ── Save button ──────────────────────────────────────────────────────────
        var saveBtn = new Button
        {
            Content = "💾  Save Overrides",
            FontSize = 12,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            Padding = new Thickness(14, 8, 14, 8),
            Background = Brush("AccentBlueBgBrush"),
            Foreground = Brush("AccentTealBrush"),
            BorderBrush = Brush("AccentBlueBorderBrush"),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            CornerRadius = new CornerRadius(8),
        };
        var capturedName = gameName;
        saveBtn.Click += (s, ev) =>
        {
            var det = detectedBox.Text?.Trim();

            // Handle game rename
            if (!string.IsNullOrEmpty(capturedName) && !string.IsNullOrEmpty(det)
                && !det.Equals(capturedName, StringComparison.OrdinalIgnoreCase))
            {
                ViewModel.RenameGame(capturedName, det);
            }



            // DC Mode override
            int? newDcMode = dcModeCombo.SelectedIndex switch { 1 => 0, 2 => 1, 3 => 2, _ => null };
            if (!string.IsNullOrEmpty(det) && newDcMode != ViewModel.GetPerGameDcModeOverride(det))
            {
                ViewModel.SetPerGameDcModeOverride(det, newDcMode);
                ViewModel.ApplyDcModeSwitchForCard(det);
            }

            // Update All exclusion
            bool nowUaExcluded = uaExcludeBtn.IsChecked == true;
            if (!string.IsNullOrEmpty(det) && nowUaExcluded != ViewModel.IsUpdateAllExcluded(det))
                ViewModel.ToggleUpdateAllExclusion(det);

            // Shader mode
            var newShaderMode = shaderModeCombo.SelectedItem as string ?? "Global";
            if (!string.IsNullOrEmpty(det) && newShaderMode != ViewModel.GetPerGameShaderMode(det))
            {
                ViewModel.SetPerGameShaderMode(det, newShaderMode);
                ViewModel.DeployShadersForCard(det);
            }

            // 32-bit mode
            bool now32Bit = bit32Btn.IsChecked == true;
            if (!string.IsNullOrEmpty(det) && now32Bit != ViewModel.Is32BitGame(det))
                ViewModel.Toggle32Bit(det);

            // DLL naming override
            bool nowDllOverride = dllOverrideBtn.IsChecked == true;
            bool wasDllOverride = !string.IsNullOrEmpty(det) && ViewModel.HasDllOverride(det);
            if (!string.IsNullOrEmpty(det))
            {
                var targetCard = ViewModel.AllCards.FirstOrDefault(c =>
                    c.GameName.Equals(det, StringComparison.OrdinalIgnoreCase));

                if (nowDllOverride && !wasDllOverride && targetCard != null)
                {
                    var rsName = !string.IsNullOrWhiteSpace(rsNameBox.Text) ? rsNameBox.Text.Trim() : rsNameBox.PlaceholderText;
                    var dcName = !string.IsNullOrWhiteSpace(dcNameBox.Text) ? dcNameBox.Text.Trim() : dcNameBox.PlaceholderText;
                    ViewModel.EnableDllOverride(targetCard, rsName, dcName);
                }
                else if (nowDllOverride && wasDllOverride && targetCard != null)
                {
                    var rsName = !string.IsNullOrWhiteSpace(rsNameBox.Text) ? rsNameBox.Text.Trim() : rsNameBox.PlaceholderText;
                    var dcName = !string.IsNullOrWhiteSpace(dcNameBox.Text) ? dcNameBox.Text.Trim() : dcNameBox.PlaceholderText;
                    ViewModel.UpdateDllOverrideNames(targetCard, rsName, dcName);
                }
                else if (!nowDllOverride && wasDllOverride && targetCard != null)
                {
                    ViewModel.DisableDllOverride(targetCard);
                }
            }

            // Name mapping
            var key = wikiBox.Text?.Trim();
            if (!string.IsNullOrEmpty(det) && !string.IsNullOrEmpty(key))
                ViewModel.AddNameMapping(det, key);
            else if (!string.IsNullOrEmpty(det) && string.IsNullOrEmpty(key))
                ViewModel.RemoveNameMapping(det);

            CrashReporter.Log($"Compact overrides saved for: {det}");

            // Re-select the game card after the filter refresh so the user
            // doesn't lose their selection when saving overrides.
            // Store a pending name — the immediate attempt handles the case where
            // no async rebuild is triggered; the IsLoading→false callback in
            // OnViewModelChanged handles the case where InitializeAsync rebuilds
            // DisplayedGames asynchronously.
            _pendingReselect = det;
            DispatcherQueue.TryEnqueue(TryRestoreSelection);
        };
        OverridesPanel.Children.Add(saveBtn);
    }

    private void TryRestoreSelection()
    {
        if (_pendingReselect != null)
        {
            var name = _pendingReselect;
            var match = ViewModel.DisplayedGames.FirstOrDefault(c =>
                c.GameName.Equals(name, StringComparison.OrdinalIgnoreCase));
            if (match != null)
            {
                GameList.SelectedItem = match;
                GameList.ScrollIntoView(match);
                _pendingReselect = null;
                return;
            }
        }

        // Auto-select first game if nothing is selected
        if (GameList.SelectedItem == null && ViewModel.DisplayedGames.Count > 0)
        {
            GameList.SelectedItem = ViewModel.DisplayedGames[0];
        }
    }
}
