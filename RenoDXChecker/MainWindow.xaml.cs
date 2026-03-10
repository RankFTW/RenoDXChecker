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
    private const int DefaultHeight = 1000;

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

        // Enforce minimum window size via Win32 subclass
        _hwnd = WindowNative.GetWindowHandle(this);
        _origWndProc = GetWindowLongPtr(_hwnd, GWLP_WNDPROC);
        _wndProcDelegate = new WndProcDelegate(WndProc);
        SetWindowLongPtr(_hwnd, GWLP_WNDPROC, Marshal.GetFunctionPointerForDelegate(_wndProcDelegate));

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
                    if (ViewModel.IsGridLayout) RebuildCardGrid();
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

    private void LayoutToggle_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.IsGridLayout = !ViewModel.IsGridLayout;
        if (ViewModel.IsGridLayout)
        {
            RebuildCardGrid();
        }
        else
        {
            // Switching to detail mode — repopulate detail panel for selected game if any
            if (ViewModel.SelectedGame is { } card)
            {
                PopulateDetailPanel(card);
                DetailPanel.Visibility = Visibility.Visible;
                BuildOverridesPanel(card);
                OverridesContainer.Visibility = Visibility.Visible;
            }
        }
    }

    // ── Card Grid rendering (Tasks 6.2–6.4) ──────────────────────────────────────

    private void RebuildCardGrid()
    {
        CardGridPanel.Children.Clear();
        foreach (var card in ViewModel.DisplayedGames)
        {
            try
            {
                CardGridPanel.Children.Add(BuildGameCard(card));
            }
            catch (Exception ex)
            {
                CrashReporter.Log($"RebuildCardGrid: skipped card '{card.GameName}': {ex.Message}");
            }
        }
        // If the selected game is in the displayed list, scroll to it
        if (ViewModel.SelectedGame is { } sel && ViewModel.DisplayedGames.Contains(sel))
            ScrollToCard(sel);
    }

    private Border BuildGameCard(GameCardViewModel card)
    {
        var gameName = string.IsNullOrEmpty(card.GameName) ? "Unknown Game" : card.GameName;

        // ── Outer border ──────────────────────────────────────────────────────
        var border = new Border
        {
            Width = 280,
            CornerRadius = new CornerRadius(10),
            BorderThickness = new Thickness(1),
            Background = new SolidColorBrush(ParseColor(card.CardBackground)),
            BorderBrush = new SolidColorBrush(ParseColor(card.CardBorderBrush)),
            Padding = new Thickness(14, 12, 14, 12),
            Tag = card,
        };

        var root = new StackPanel { Spacing = 8 };

        // ── Header row: source icon, game name, favourite star, overrides gear ──
        var header = new Grid();
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });  // icon
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); // name
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });  // fav
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });  // gear

        // Source icon
        if (card.HasSourceIcon)
        {
            var srcImg = new Image
            {
                Source = new Microsoft.UI.Xaml.Media.Imaging.BitmapImage(card.SourceIconUri),
                Width = 16, Height = 16,
                Margin = new Thickness(0, 0, 6, 0),
                VerticalAlignment = VerticalAlignment.Center,
            };
            Grid.SetColumn(srcImg, 0);
            header.Children.Add(srcImg);
        }
        else
        {
            var srcText = new TextBlock
            {
                Text = card.SourceIcon,
                FontSize = 14,
                Margin = new Thickness(0, 0, 6, 0),
                VerticalAlignment = VerticalAlignment.Center,
            };
            Grid.SetColumn(srcText, 0);
            header.Children.Add(srcText);
        }

        // Game name (trimmed)
        var nameBlock = new TextBlock
        {
            Text = gameName.Length > 28 ? gameName[..25] + "…" : gameName,
            FontSize = 13,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            Foreground = Brush("TextPrimaryBrush"),
            VerticalAlignment = VerticalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis,
            MaxLines = 1,
        };
        ToolTipService.SetToolTip(nameBlock, gameName);
        Grid.SetColumn(nameBlock, 1);
        header.Children.Add(nameBlock);

        // Favourite star button
        var favBtn = new Button
        {
            Content = card.IsFavourite ? "⭐" : "☆",
            Tag = card,
            FontSize = 14,
            Padding = new Thickness(4, 2, 4, 2),
            MinWidth = 0, MinHeight = 0,
            Background = new SolidColorBrush(Windows.UI.Color.FromArgb(0, 0, 0, 0)),
            BorderThickness = new Thickness(0),
            VerticalAlignment = VerticalAlignment.Center,
        };
        favBtn.Click += CardFavouriteButton_Click;
        Grid.SetColumn(favBtn, 2);
        header.Children.Add(favBtn);

        // More options (...) button
        var moreBtn = new Button
        {
            Content = "⋯",
            Tag = card,
            FontSize = 14,
            Padding = new Thickness(4, 0, 4, 0),
            MinWidth = 0, MinHeight = 0,
            Background = new SolidColorBrush(Windows.UI.Color.FromArgb(0, 0, 0, 0)),
            Foreground = new SolidColorBrush(ParseColor("#6B7A8E")),
            BorderThickness = new Thickness(0),
            VerticalAlignment = VerticalAlignment.Center,
        };
        moreBtn.Click += CardMoreMenu_Click;
        Grid.SetColumn(moreBtn, 3);
        header.Children.Add(moreBtn);

        root.Children.Add(header);

        // ── Status row: RS/DC/RDX status dots with labels, conditionally Luma, wiki status icon ──
        var statusRow = new Grid();
        statusRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); // dots
        statusRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); // wiki icon

        var dotsPanel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10 };

        var rdxDotPanel = MakeStatusDot("RDX", card.CardRdxStatusDot);
        var rsDotPanel = MakeStatusDot("RS", card.CardRsStatusDot);
        var dcDotPanel = MakeStatusDot("DC", card.CardDcStatusDot);
        dotsPanel.Children.Add(rdxDotPanel);
        dotsPanel.Children.Add(rsDotPanel);
        dotsPanel.Children.Add(dcDotPanel);

        StackPanel? lumaDotPanel = null;
        if (card.CardLumaVisible)
        {
            lumaDotPanel = MakeStatusDot("Luma", card.CardLumaStatusDot);
            dotsPanel.Children.Add(lumaDotPanel);
        }

        Grid.SetColumn(dotsPanel, 0);
        statusRow.Children.Add(dotsPanel);

        // Wiki status icon (right-aligned, icon only, hidden in Luma mode)
        if (card.WikiStatusIconVisible)
        {
            var wikiIcon = new TextBlock
            {
                Text = card.WikiStatusIcon,
                FontSize = 14,
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Right,
            };
            ToolTipService.SetToolTip(wikiIcon, card.WikiStatusLabel);
            Grid.SetColumn(wikiIcon, 1);
            statusRow.Children.Add(wikiIcon);
        }

        root.Children.Add(statusRow);

        // ── Action row: full-width install button with flyout ──
        var installFlyout = new Flyout
        {
            Placement = Microsoft.UI.Xaml.Controls.Primitives.FlyoutPlacementMode.Bottom,
        };
        installFlyout.Opening += CardInstallFlyout_Opening;

        // Apply dark background to the flyout presenter via its style
        var flyoutPresenterStyle = new Style(typeof(FlyoutPresenter));
        flyoutPresenterStyle.Setters.Add(new Setter(Control.BackgroundProperty, new SolidColorBrush(ParseColor("#0C1018"))));
        flyoutPresenterStyle.Setters.Add(new Setter(Control.BorderBrushProperty, new SolidColorBrush(ParseColor("#2A4468"))));
        flyoutPresenterStyle.Setters.Add(new Setter(Control.PaddingProperty, new Thickness(12)));
        installFlyout.FlyoutPresenterStyle = flyoutPresenterStyle;

        var installBtn = new Button
        {
            Content = card.CardPrimaryActionLabel,
            Tag = card,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            FontSize = 12,
            Padding = new Thickness(8, 5, 8, 5),
            IsEnabled = card.CanCardInstall,
            Background = new SolidColorBrush(ParseColor("#182840")),
            Foreground = new SolidColorBrush(ParseColor("#7AACDD")),
            BorderBrush = new SolidColorBrush(ParseColor("#2A4468")),
            CornerRadius = new CornerRadius(6),
            Flyout = installFlyout,
        };

        root.Children.Add(installBtn);

        // ── Bottom row: info buttons (left) + overrides button (right) ──
        var bottomRow = new Grid();
        bottomRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        bottomRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        // Left side: info/notes buttons
        if (card.HasInfoIndicator)
        {
            var infoRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
            if (card.HasNameUrl)
            {
                var infoBtn = new Button
                {
                    Content = "Wiki",
                    Tag = card,
                    FontSize = 11,
                    Padding = new Thickness(6, 2, 6, 2),
                    MinWidth = 0, MinHeight = 0,
                    Background = new SolidColorBrush(ParseColor("#1E242C")),
                    Foreground = new SolidColorBrush(ParseColor("#6B7A8E")),
                    BorderBrush = new SolidColorBrush(ParseColor("#283240")),
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(4),
                };
                ToolTipService.SetToolTip(infoBtn, "Open discussion / instructions");
                infoBtn.Click += CardInfoLink_Click;
                infoRow.Children.Add(infoBtn);
            }
            if (card.HasNotes)
            {
                var notesBtn = new Button
                {
                    Content = "Info",
                    Tag = card,
                    FontSize = 11,
                    Padding = new Thickness(6, 2, 6, 2),
                    MinWidth = 0, MinHeight = 0,
                    Background = new SolidColorBrush(ParseColor("#1E242C")),
                    Foreground = new SolidColorBrush(ParseColor("#6B7A8E")),
                    BorderBrush = new SolidColorBrush(ParseColor("#283240")),
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(4),
                };
                ToolTipService.SetToolTip(notesBtn, "View notes");
                notesBtn.Click += CardNotesButton_Click;
                infoRow.Children.Add(notesBtn);
            }
            Grid.SetColumn(infoRow, 0);
            bottomRow.Children.Add(infoRow);
        }

        // Right side: overrides button
        var overridesBtn = new Button
        {
            Content = "⚙",
            Tag = card,
            FontSize = 12,
            Padding = new Thickness(4, 2, 4, 2),
            MinWidth = 0, MinHeight = 0,
            Background = new SolidColorBrush(Windows.UI.Color.FromArgb(0, 0, 0, 0)),
            Foreground = new SolidColorBrush(ParseColor("#6B7A8E")),
            BorderThickness = new Thickness(0),
            HorizontalAlignment = HorizontalAlignment.Right,
        };
        ToolTipService.SetToolTip(overridesBtn, "Overrides");
        overridesBtn.Click += CardOverridesButton_Click;
        Grid.SetColumn(overridesBtn, 1);
        bottomRow.Children.Add(overridesBtn);

        root.Children.Add(bottomRow);

        // ── Click-to-highlight on the card border ──
        border.PointerPressed += Card_PointerPressed;

        border.Child = root;

        // ── Subscribe to PropertyChanged for live updates ──
        card.PropertyChanged += (s, e) =>
        {
            if (s is not GameCardViewModel c) return;
            DispatcherQueue.TryEnqueue(() =>
            {
                try
                {
                    switch (e.PropertyName)
                    {
                        case nameof(c.CardBackground):
                            border.Background = new SolidColorBrush(ParseColor(c.CardBackground));
                            break;
                        case nameof(c.CardBorderBrush):
                            border.BorderBrush = new SolidColorBrush(ParseColor(c.CardBorderBrush));
                            break;
                        case nameof(c.CardPrimaryActionLabel):
                            installBtn.Content = c.CardPrimaryActionLabel;
                            break;
                        case nameof(c.CanCardInstall):
                            installBtn.IsEnabled = c.CanCardInstall;
                            break;
                        case nameof(c.IsFavourite):
                            favBtn.Content = c.IsFavourite ? "⭐" : "☆";
                            break;
                        case nameof(c.CardRdxStatusDot):
                            if (rdxDotPanel.Children[0] is Microsoft.UI.Xaml.Shapes.Ellipse rdxEllipse)
                                rdxEllipse.Fill = new SolidColorBrush(ParseColor(c.CardRdxStatusDot));
                            break;
                        case nameof(c.CardRsStatusDot):
                            if (rsDotPanel.Children[0] is Microsoft.UI.Xaml.Shapes.Ellipse rsEllipse)
                                rsEllipse.Fill = new SolidColorBrush(ParseColor(c.CardRsStatusDot));
                            break;
                        case nameof(c.CardDcStatusDot):
                            if (dcDotPanel.Children[0] is Microsoft.UI.Xaml.Shapes.Ellipse dcEllipse)
                                dcEllipse.Fill = new SolidColorBrush(ParseColor(c.CardDcStatusDot));
                            break;
                        case nameof(c.CardLumaStatusDot):
                            if (lumaDotPanel?.Children[0] is Microsoft.UI.Xaml.Shapes.Ellipse lumaEllipse)
                                lumaEllipse.Fill = new SolidColorBrush(ParseColor(c.CardLumaStatusDot));
                            break;
                        case nameof(c.CardLumaVisible):
                            bool effectiveLuma = c.LumaFeatureEnabled && c.IsLumaMode;
                            // Hide/show RDX/RS/DC dots based on Luma mode
                            rdxDotPanel.Visibility = effectiveLuma ? Visibility.Collapsed : Visibility.Visible;
                            rsDotPanel.Visibility = effectiveLuma ? Visibility.Collapsed : Visibility.Visible;
                            dcDotPanel.Visibility = effectiveLuma ? Visibility.Collapsed : Visibility.Visible;
                            // Add/remove Luma dot
                            if (c.CardLumaVisible && lumaDotPanel == null)
                            {
                                lumaDotPanel = MakeStatusDot("Luma", c.CardLumaStatusDot);
                                statusRow.Children.Add(lumaDotPanel);
                            }
                            else if (!c.CardLumaVisible && lumaDotPanel != null)
                            {
                                statusRow.Children.Remove(lumaDotPanel);
                                lumaDotPanel = null;
                            }
                            break;
                    }
                }
                catch { /* card may have been removed from visual tree */ }
            });
        };

        return border;
    }

    /// <summary>Creates a small status dot + label pair for the card grid.</summary>
    private static StackPanel MakeStatusDot(string label, string colorHex)
    {
        var panel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4 };
        panel.Children.Add(new Microsoft.UI.Xaml.Shapes.Ellipse
        {
            Width = 8, Height = 8,
            Fill = new SolidColorBrush(ParseColor(colorHex)),
            VerticalAlignment = VerticalAlignment.Center,
        });
        panel.Children.Add(new TextBlock
        {
            Text = label,
            FontSize = 11,
            Foreground = new SolidColorBrush(ParseColor("#A0AABB")),
            VerticalAlignment = VerticalAlignment.Center,
        });
        return panel;
    }

    // ── Install flyout builder (Task 3.1) ────────────────────────────────────────

    /// <summary>
    /// Builds the install flyout content panel with per-component rows.
    /// Each row has: component name, status text (colored), install button, copy config 📋 (RS/DC only), uninstall ✕.
    /// </summary>
    private StackPanel BuildInstallFlyoutContent(GameCardViewModel card)
    {
        var panel = new StackPanel { Spacing = 6, Width = 380 };

        // ── Header row: "Components" label + "Install All" button ──
        var headerRow = new Grid();
        headerRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        headerRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var headerLabel = new TextBlock
        {
            Text = "Components",
            FontSize = 13,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            Foreground = new SolidColorBrush(ParseColor("#A0AABB")),
            VerticalAlignment = VerticalAlignment.Center,
        };
        Grid.SetColumn(headerLabel, 0);
        headerRow.Children.Add(headerLabel);

        var installAllBtn = new Button
        {
            Content = "Install All",
            Tag = card,
            FontSize = 11,
            Padding = new Thickness(10, 4, 10, 4),
            IsEnabled = card.CanCardInstall,
            Background = new SolidColorBrush(ParseColor("#182840")),
            Foreground = new SolidColorBrush(ParseColor("#7AACDD")),
            BorderBrush = new SolidColorBrush(ParseColor("#2A4468")),
            CornerRadius = new CornerRadius(6),
        };
        installAllBtn.Click += CardInstallButton_Click;
        Grid.SetColumn(installAllBtn, 1);
        headerRow.Children.Add(installAllBtn);

        panel.Children.Add(headerRow);
        panel.Children.Add(MakeSeparator());

        // ── Component rows ──

        // ReShade row
        var rsRow = BuildComponentRow(card, "ReShade", "RS",
            card.RsStatusText, card.RsStatusColor, card.RsShortAction,
            card.CardRsInstallEnabled, card.IsRsInstalled,
            showCopyConfig: true, copyConfigVisible: card.RsIniExists,
            copyConfigTooltip: "Copy reshade.ini",
            btnBackground: card.RsBtnBackground, btnForeground: card.RsBtnForeground, btnBorderBrush: card.RsBtnBorderBrush);
        rsRow.Visibility = card.ReShadeRowVisibility;
        panel.Children.Add(rsRow);

        // Display Commander row
        var dcRow = BuildComponentRow(card, "DC", "DC",
            card.DcStatusText, card.DcStatusColor, card.DcShortAction,
            card.CardDcInstallEnabled, card.IsDcInstalled,
            showCopyConfig: true, copyConfigVisible: card.DcIniExists,
            copyConfigTooltip: "Copy DisplayCommander.toml",
            btnBackground: card.DcBtnBackground, btnForeground: card.DcBtnForeground, btnBorderBrush: card.DcBtnBorderBrush);
        dcRow.Visibility = card.DcRowVisibility;
        panel.Children.Add(dcRow);

        // RenoDX row
        var rdxRow = BuildComponentRow(card, "RenoDX", "RDX",
            card.RdxStatusText, card.RdxStatusColor, card.RdxShortAction,
            card.CardRdxInstallEnabled, card.IsRdxInstalled,
            showCopyConfig: false, copyConfigVisible: false,
            copyConfigTooltip: null,
            btnBackground: card.InstallBtnBackground, btnForeground: card.InstallBtnForeground, btnBorderBrush: card.InstallBtnBorderBrush);
        rdxRow.Visibility = card.RenoDxRowVisibility;
        panel.Children.Add(rdxRow);

        // External/Discord row — shown when game is external-only (no wiki mod)
        Grid? externalRow = null;
        if (card.IsExternalOnly && !(card.LumaFeatureEnabled && card.IsLumaMode))
        {
            externalRow = new Grid { Margin = new Thickness(0, 2, 0, 2) };
            externalRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(60) });
            externalRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var extName = new TextBlock
            {
                Text = "RenoDX",
                FontSize = 12,
                Foreground = new SolidColorBrush(ParseColor("#A0AABB")),
                VerticalAlignment = VerticalAlignment.Center,
            };
            Grid.SetColumn(extName, 0);
            externalRow.Children.Add(extName);

            var extBtn = new Button
            {
                Content = card.ExternalLabel,
                Tag = card,
                FontSize = 11,
                Padding = new Thickness(8, 3, 8, 3),
                MinWidth = 0,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                Background = new SolidColorBrush(ParseColor("#1A2040")),
                Foreground = new SolidColorBrush(ParseColor("#7AACDD")),
                BorderBrush = new SolidColorBrush(ParseColor("#2A4468")),
                CornerRadius = new CornerRadius(6),
            };
            extBtn.Click += async (s, ev) =>
            {
                var url = card.ExternalUrl;
                if (!string.IsNullOrEmpty(url))
                    await Windows.System.Launcher.LaunchUriAsync(new Uri(url));
            };
            Grid.SetColumn(extBtn, 1);
            externalRow.Children.Add(extBtn);

            panel.Children.Add(externalRow);
        }

        // Luma row (conditional)
        Grid? lumaRow = null;
        if (card.CardLumaVisible)
        {
            lumaRow = BuildComponentRow(card, "Luma", "Luma",
                card.LumaStatusText, card.LumaStatusColor, card.LumaShortAction,
                card.CardLumaInstallEnabled, card.IsLumaInstalled,
                showCopyConfig: false, copyConfigVisible: false,
                copyConfigTooltip: null,
                btnBackground: card.LumaBtnBackground, btnForeground: card.LumaBtnForeground,
                btnBorderBrush: card.LumaBtnBorderBrush);
            panel.Children.Add(lumaRow);
        }

        // ── Subscribe to PropertyChanged for live updates while flyout is open ──
        System.ComponentModel.PropertyChangedEventHandler? handler = null;
        handler = (s, e) =>
        {
            if (s is not GameCardViewModel c) return;
            DispatcherQueue.TryEnqueue(() =>
            {
                try
                {
                    // Update Install All button
                    installAllBtn.IsEnabled = c.CanCardInstall;

                    // Update row visibility
                    rsRow.Visibility = c.ReShadeRowVisibility;
                    dcRow.Visibility = c.DcRowVisibility;
                    rdxRow.Visibility = c.RenoDxRowVisibility;

                    // Update each component row's status/buttons
                    UpdateComponentRow(rsRow, c.RsStatusText, c.RsStatusColor, c.RsShortAction,
                        c.CardRsInstallEnabled, c.IsRsInstalled, c.RsIniExists,
                        c.RsBtnBackground, c.RsBtnForeground, c.RsBtnBorderBrush);
                    UpdateComponentRow(dcRow, c.DcStatusText, c.DcStatusColor, c.DcShortAction,
                        c.CardDcInstallEnabled, c.IsDcInstalled, c.DcIniExists,
                        c.DcBtnBackground, c.DcBtnForeground, c.DcBtnBorderBrush);
                    UpdateComponentRow(rdxRow, c.RdxStatusText, c.RdxStatusColor, c.RdxShortAction,
                        c.CardRdxInstallEnabled, c.IsRdxInstalled, false,
                        c.InstallBtnBackground, c.InstallBtnForeground, c.InstallBtnBorderBrush);

                    if (lumaRow != null)
                    {
                        UpdateComponentRow(lumaRow, c.LumaStatusText, c.LumaStatusColor, c.LumaShortAction,
                            c.CardLumaInstallEnabled, c.IsLumaInstalled, false,
                            c.LumaBtnBackground, c.LumaBtnForeground, c.LumaBtnBorderBrush);
                    }
                }
                catch { /* flyout may have been closed / card removed */ }
            });
        };
        card.PropertyChanged += handler;

        // Store handler reference on the panel so we can unsubscribe on flyout close
        panel.Tag = (card, handler);

        return panel;
    }

    /// <summary>
    /// Builds a single component row Grid with: name, status text, install button, copy config 📋, uninstall ✕.
    /// </summary>
    private Grid BuildComponentRow(
        GameCardViewModel card, string componentName, string componentTag,
        string statusText, string statusColor, string actionLabel,
        bool installEnabled, bool isInstalled,
        bool showCopyConfig, bool copyConfigVisible, string? copyConfigTooltip,
        string? btnBackground = null, string? btnForeground = null, string? btnBorderBrush = null)
    {
        var row = new Grid { Margin = new Thickness(0, 2, 0, 2) };
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(60) });  // name
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(75) });  // status
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); // install btn
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });     // copy config
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });     // uninstall

        // Component name
        var nameText = new TextBlock
        {
            Text = componentName,
            FontSize = 12,
            Foreground = new SolidColorBrush(ParseColor("#A0AABB")),
            VerticalAlignment = VerticalAlignment.Center,
        };
        Grid.SetColumn(nameText, 0);
        row.Children.Add(nameText);

        // Status text (colored)
        var statusBlock = new TextBlock
        {
            Text = statusText,
            FontSize = 11,
            Foreground = new SolidColorBrush(ParseColor(statusColor)),
            VerticalAlignment = VerticalAlignment.Center,
            Tag = "StatusText",
        };
        Grid.SetColumn(statusBlock, 1);
        row.Children.Add(statusBlock);

        // Install/update button
        var installBtn = new Button
        {
            Content = actionLabel,
            Tag = card,
            FontSize = 11,
            Padding = new Thickness(8, 3, 8, 3),
            MinWidth = 0,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            IsEnabled = installEnabled,
            Background = new SolidColorBrush(ParseColor(btnBackground ?? "#182840")),
            Foreground = new SolidColorBrush(ParseColor(btnForeground ?? "#7AACDD")),
            BorderBrush = new SolidColorBrush(ParseColor(btnBorderBrush ?? "#2A4468")),
            CornerRadius = new CornerRadius(6),
        };
        // Store component tag for the click handler to identify which component
        installBtn.DataContext = componentTag;
        installBtn.Click += CardComponentInstall_Click;
        Grid.SetColumn(installBtn, 2);
        row.Children.Add(installBtn);

        // Copy config 📋 button — always added to reserve column space; hidden when not applicable
        bool copyVisible = showCopyConfig && copyConfigVisible;
        var copyBtn = new Button
        {
            Content = "📋",
            Tag = card,
            FontSize = 11,
            Padding = new Thickness(4, 3, 4, 3),
            MinWidth = 0, MinHeight = 0,
            Margin = new Thickness(4, 0, 0, 0),
            Opacity = copyVisible ? 1 : 0,
            IsHitTestVisible = copyVisible,
            Background = new SolidColorBrush(ParseColor("#1A2030")),
            Foreground = new SolidColorBrush(ParseColor("#6B7A8E")),
            BorderBrush = new SolidColorBrush(ParseColor("#283240")),
            CornerRadius = new CornerRadius(4),
        };
        copyBtn.DataContext = componentTag;
        if (componentTag == "RS")
            copyBtn.Click += CardCopyRsIni_Click;
        else if (componentTag == "DC")
            copyBtn.Click += CardCopyDcToml_Click;
        if (copyConfigTooltip != null)
            ToolTipService.SetToolTip(copyBtn, copyConfigTooltip);
        Grid.SetColumn(copyBtn, 3);
        row.Children.Add(copyBtn);

        // Uninstall ✕ button
        var uninstallBtn = new Button
        {
            Content = "✕",
            Tag = card,
            FontSize = 11,
            Padding = new Thickness(4, 3, 4, 3),
            MinWidth = 0, MinHeight = 0,
            Margin = new Thickness(4, 0, 0, 0),
            Opacity = isInstalled ? 1 : 0,
            IsHitTestVisible = isInstalled,
            Background = new SolidColorBrush(ParseColor("#301820")),
            Foreground = new SolidColorBrush(ParseColor("#E06060")),
            BorderBrush = new SolidColorBrush(ParseColor("#502838")),
            CornerRadius = new CornerRadius(4),
        };
        uninstallBtn.DataContext = componentTag;
        uninstallBtn.Click += CardComponentUninstall_Click;
        Grid.SetColumn(uninstallBtn, 4);
        row.Children.Add(uninstallBtn);

        return row;
    }

    /// <summary>
    /// Updates a component row's status text, color, install button label/enabled, copy config visibility, and uninstall visibility.
    /// </summary>
    private static void UpdateComponentRow(Grid row, string statusText, string statusColor,
        string actionLabel, bool installEnabled, bool isInstalled, bool copyConfigVisible,
        string? btnBackground = null, string? btnForeground = null, string? btnBorderBrush = null)
    {
        foreach (var child in row.Children)
        {
            if (child is TextBlock tb && tb.Tag as string == "StatusText")
            {
                tb.Text = statusText;
                tb.Foreground = new SolidColorBrush(ParseColor(statusColor));
            }
            else if (child is Button btn)
            {
                var col = Grid.GetColumn(btn);
                if (col == 2) // install button
                {
                    btn.Content = actionLabel;
                    btn.IsEnabled = installEnabled;
                    if (btnBackground != null)
                        btn.Background = new SolidColorBrush(ParseColor(btnBackground));
                    if (btnForeground != null)
                        btn.Foreground = new SolidColorBrush(ParseColor(btnForeground));
                    if (btnBorderBrush != null)
                        btn.BorderBrush = new SolidColorBrush(ParseColor(btnBorderBrush));
                }
                else if (col == 3) // copy config button
                {
                    btn.Opacity = copyConfigVisible ? 1 : 0;
                    btn.IsHitTestVisible = copyConfigVisible;
                }
                else if (col == 4) // uninstall button
                {
                    btn.Opacity = isInstalled ? 1 : 0;
                    btn.IsHitTestVisible = isInstalled;
                }
            }
        }
    }

    /// <summary>
    /// Handler for the install flyout opening — builds the flyout content and attaches it.
    /// Called when the install button's flyout is about to open.
    /// </summary>
    private void CardInstallFlyout_Opening(object? sender, object e)
    {
        if (sender is not Flyout flyout) return;
        if (flyout.Target is not FrameworkElement { Tag: GameCardViewModel card }) return;

        var content = BuildInstallFlyoutContent(card);

        var scrollViewer = new ScrollViewer
        {
            Content = content,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            MaxHeight = 400,
        };

        flyout.Content = scrollViewer;

        // Unsubscribe from PropertyChanged when flyout closes
        flyout.Closed += FlyoutClosed;

        void FlyoutClosed(object? s, object ev)
        {
            flyout.Closed -= FlyoutClosed;
            if (content.Tag is (GameCardViewModel c, System.ComponentModel.PropertyChangedEventHandler h))
            {
                c.PropertyChanged -= h;
            }
        }
    }

    // ── Per-component install flyout click handlers ──

    private async void CardComponentInstall_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button btn || btn.Tag is not GameCardViewModel card) return;
        var component = btn.DataContext as string;

        // Ensure install path exists (same pattern as CardInstallButton_Click)
        if (string.IsNullOrEmpty(card.InstallPath) || !System.IO.Directory.Exists(card.InstallPath))
        {
            var folder = await PickFolderAsync();
            if (folder == null) return;
            card.InstallPath = folder;
            ViewModel.SaveLibraryPublic();
        }

        switch (component)
        {
            case "RDX":
                await ViewModel.InstallModCommand.ExecuteAsync(card);
                break;
            case "RS":
                await ViewModel.InstallReShadeCommand.ExecuteAsync(card);
                break;
            case "DC":
                await ViewModel.InstallDcCommand.ExecuteAsync(card);
                break;
            case "Luma":
                await ViewModel.InstallLumaAsync(card);
                break;
        }
    }

    private void CardComponentUninstall_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button btn || btn.Tag is not GameCardViewModel card) return;
        var component = btn.DataContext as string;

        switch (component)
        {
            case "RDX":
                ViewModel.UninstallModCommand.Execute(card);
                break;
            case "RS":
                ViewModel.UninstallReShadeCommand.Execute(card);
                break;
            case "DC":
                ViewModel.UninstallDcCommand.Execute(card);
                break;
            case "Luma":
                ViewModel.UninstallLumaCommand.Execute(card);
                break;
        }
    }

    private void CardCopyRsIni_Click(object sender, RoutedEventArgs e)
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

    private void CardCopyDcToml_Click(object sender, RoutedEventArgs e)
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

    // ── Card action button handlers (Task 6.4) ───────────────────────────────────

    private async void CardInstallButton_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is not GameCardViewModel card) return;

        // Route to Luma install if in Luma mode, otherwise RenoDX combined install
        if (card.LumaFeatureEnabled && card.IsLumaMode && card.LumaMod != null)
        {
            await ViewModel.InstallLumaAsync(card);
        }
        else
        {
            // Ensure install path exists
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
            if (card.DcRowVisibility == Visibility.Visible)
                await ViewModel.InstallDcCommand.ExecuteAsync(card);
            if (card.ReShadeRowVisibility == Visibility.Visible)
                await ViewModel.InstallReShadeCommand.ExecuteAsync(card);
        }
    }

    private void CardFavouriteButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button btn || btn.Tag is not GameCardViewModel card) return;
        ViewModel.ToggleFavouriteCommand.Execute(card);
        btn.Content = card.IsFavourite ? "⭐" : "☆";

        // Also refresh the detail panel icon if this is the selected game
        if (card == ViewModel.SelectedGame)
        {
            DetailFavIcon.Text = card.IsFavourite ? "⭐" : "☆";
            DetailFavIcon.Foreground = new SolidColorBrush(card.IsFavourite
                ? ((SolidColorBrush)Application.Current.Resources["AccentAmberBrush"]).Color
                : ((SolidColorBrush)Application.Current.Resources["TextDisabledBrush"]).Color);
        }
    }

    private void CardOpenFolder_Click(object sender, RoutedEventArgs e)
    {
        var card = GetCardFromSender(sender);
        if (card == null || string.IsNullOrEmpty(card.InstallPath)) return;
        if (System.IO.Directory.Exists(card.InstallPath))
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(card.InstallPath) { UseShellExecute = true });
    }

    private void CardOverridesButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement anchor && anchor.Tag is GameCardViewModel card)
        {
            ViewModel.SelectedGame = card;
            OpenOverridesFlyout(card, anchor);
        }
    }

    private void CardMoreMenu_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement anchor || anchor.Tag is not GameCardViewModel card)
            return;

        ViewModel.SelectedGame = card;

        var menu = new MenuFlyout
        {
            Placement = Microsoft.UI.Xaml.Controls.Primitives.FlyoutPlacementMode.BottomEdgeAlignedRight,
        };

        // ── Open Folder ──
        var openFolderItem = new MenuFlyoutItem
        {
            Text = "📂 Open Folder",
            Tag = card,
        };
        openFolderItem.Click += CardOpenFolder_Click;
        menu.Items.Add(openFolderItem);

        // ── Hide / Show ──
        var hideItem = new MenuFlyoutItem
        {
            Text = card.HideButtonLabel,
            Tag = card,
        };
        hideItem.Click += (s, ev) => ViewModel.ToggleHideGameCommand.Execute(card);
        menu.Items.Add(hideItem);

        // ── Luma toggle (conditional — only when Luma is available for this game) ──
        if (card.LumaFeatureEnabled && card.IsLumaAvailable)
        {
            var lumaLabel = card.IsLumaMode ? "🟢 Luma Enabled" : "⚫ Enable Luma";
            var lumaItem = new MenuFlyoutItem
            {
                Text = lumaLabel,
                Tag = card,
            };
            lumaItem.Click += (s, ev) => ViewModel.ToggleLumaMode(card);
            menu.Items.Add(lumaItem);
        }

        menu.Items.Add(new MenuFlyoutSeparator());

        // ── Discussion / Instructions (conditional) ──
        if (card.HasNameUrl)
        {
            var discussionItem = new MenuFlyoutItem
            {
                Text = "ℹ Discussion / Instructions",
                Tag = card,
            };
            discussionItem.Click += async (s, ev) =>
            {
                if (card.NameUrl != null)
                    await Windows.System.Launcher.LaunchUriAsync(new Uri(card.NameUrl));
            };
            menu.Items.Add(discussionItem);
        }

        // ── View Notes (conditional) ──
        if (card.HasNotes)
        {
            var notesItem = new MenuFlyoutItem
            {
                Text = "💬 View Notes",
                Tag = card,
            };
            notesItem.Click += NotesButton_Click;
            menu.Items.Add(notesItem);
        }

        menu.ShowAt(anchor);
    }

    private void OpenOverridesFlyout(GameCardViewModel card, FrameworkElement anchor)
    {
        var gameName = card.GameName;
        bool isLumaMode = ViewModel.IsLumaEnabled(gameName);

        var panel = new StackPanel { Spacing = 8, Width = 420 };

        // ── Title ──
        panel.Children.Add(new TextBlock
        {
            Text = "Overrides",
            FontSize = 13,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            Foreground = Brush("TextPrimaryBrush"),
        });

        // ── Game name + Wiki name ──
        var gameNameBox = new TextBox
        {
            Header = "Game name (editable)",
            Text = gameName,
            FontSize = 12,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Background = new SolidColorBrush(ColorHelper.FromArgb(0xFF, 0x1A, 0x20, 0x30)),
            Foreground = new SolidColorBrush(ColorHelper.FromArgb(0xFF, 0xE2, 0xE8, 0xFF)),
            BorderBrush = new SolidColorBrush(ColorHelper.FromArgb(0xFF, 0x28, 0x32, 0x40)),
            Padding = new Thickness(8, 4, 8, 4),
        };
        var wikiNameBox = new TextBox
        {
            Header = "Wiki mod name",
            PlaceholderText = "Exact wiki name",
            Text = ViewModel.GetNameMapping(gameName) ?? "",
            FontSize = 12,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Background = new SolidColorBrush(ColorHelper.FromArgb(0xFF, 0x1A, 0x20, 0x30)),
            Foreground = new SolidColorBrush(ColorHelper.FromArgb(0xFF, 0xE2, 0xE8, 0xFF)),
            BorderBrush = new SolidColorBrush(ColorHelper.FromArgb(0xFF, 0x28, 0x32, 0x40)),
            Padding = new Thickness(8, 4, 8, 4),
        };
        var originalStoreName = ViewModel.GetOriginalStoreName(gameName);
        var nameResetBtn = new Button
        {
            Content = "↩ Reset",
            FontSize = 11,
            VerticalAlignment = VerticalAlignment.Bottom,
            Padding = new Thickness(8, 4, 8, 4),
            Background = new SolidColorBrush(ColorHelper.FromArgb(0xFF, 0x1A, 0x20, 0x30)),
            Foreground = new SolidColorBrush(ColorHelper.FromArgb(0xFF, 0x6B, 0x7A, 0x8E)),
            BorderBrush = new SolidColorBrush(ColorHelper.FromArgb(0xFF, 0x28, 0x32, 0x40)),
        };
        ToolTipService.SetToolTip(nameResetBtn,
            "Reset game name back to auto-detected and clear wiki name mapping.");
        nameResetBtn.Click += (s, ev) =>
        {
            gameNameBox.Text = originalStoreName ?? gameName;
            wikiNameBox.Text = "";
        };

        var nameGrid = new Grid { ColumnSpacing = 8 };
        nameGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        nameGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        nameGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        Grid.SetColumn(gameNameBox, 0);
        Grid.SetColumn(wikiNameBox, 1);
        Grid.SetColumn(nameResetBtn, 2);
        nameGrid.Children.Add(gameNameBox);
        nameGrid.Children.Add(wikiNameBox);
        nameGrid.Children.Add(nameResetBtn);
        panel.Children.Add(nameGrid);
        panel.Children.Add(MakeSeparator());

        // ── DC Mode + Shader Mode (side by side) ──
        int? currentDcMode = ViewModel.GetPerGameDcModeOverride(gameName);
        var globalDcLabel = ViewModel.DcModeLevel switch { 1 => "DC Mode 1", 2 => "DC Mode 2", _ => "Off" };
        var dcModeOptions = new[] { $"Global ({globalDcLabel})", "Exclude (Off)", "DC Mode 1", "DC Mode 2" };
        var dcModeCombo = new ComboBox
        {
            ItemsSource = dcModeOptions,
            SelectedIndex = currentDcMode switch { null => 0, 0 => 1, 1 => 2, 2 => 3, _ => 0 },
            FontSize = 12,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Header = "DC Mode",
        };
        ToolTipService.SetToolTip(dcModeCombo,
            "Global = use the Settings DC Mode. Exclude (Off) = always use normal naming. " +
            "DC Mode 1 = force dxgi.dll proxy. DC Mode 2 = force winmm.dll proxy.");

        string currentShaderMode = ViewModel.GetPerGameShaderMode(gameName);
        var globalShaderLabel = ViewModel.ShaderDeployMode.ToString();
        var shaderModeValues = new[] { "Global", "Off", "Minimum", "All", "User" };
        var shaderModeDisplay = new[] { $"Global ({globalShaderLabel})", "Off", "Minimum", "All", "User" };
        int shaderSelectedIdx = Array.IndexOf(shaderModeValues, currentShaderMode);
        if (shaderSelectedIdx < 0) shaderSelectedIdx = 0;
        var shaderModeCombo = new ComboBox
        {
            ItemsSource = shaderModeDisplay,
            SelectedIndex = shaderSelectedIdx,
            FontSize = 12,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Header = "Shader Mode",
        };
        ToolTipService.SetToolTip(shaderModeCombo,
            "Global = follow the Settings toggle. Off = no shaders. Minimum = Lilium only. All = all packs. User = custom folder only.\n" +
            "Note: Per-game shader mode only applies when ReShade is used standalone (DC Mode OFF). " +
            "When DC Mode is ON, all DC-mode games share the DC global shader folder.");

        var modeGrid = new Grid { ColumnSpacing = 8 };
        modeGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        modeGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        Grid.SetColumn(dcModeCombo, 0);
        Grid.SetColumn(shaderModeCombo, 1);
        modeGrid.Children.Add(dcModeCombo);
        modeGrid.Children.Add(shaderModeCombo);
        panel.Children.Add(modeGrid);
        panel.Children.Add(MakeSeparator());

        // ── DLL naming override ──
        bool isDllOverride = ViewModel.HasDllOverride(gameName);
        var existingCfg = ViewModel.GetDllOverride(gameName);
        bool is32Bit = card.Is32Bit;
        var defaultRsName = is32Bit ? "ReShade32.dll" : "ReShade64.dll";
        var defaultDcName = is32Bit ? "zzz_display_commander.addon32" : "zzz_display_commander.addon64";

        var dllOverrideToggle = new ToggleSwitch
        {
            Header = "DLL naming override",
            IsOn = isDllOverride,
            IsEnabled = !isLumaMode,
            OnContent = "Custom filenames enabled",
            OffContent = "Using default filenames",
            Foreground = Brush("TextSecondaryBrush"),
            FontSize = 12,
        };
        ToolTipService.SetToolTip(dllOverrideToggle,
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
        dllOverrideToggle.Toggled += (s, ev) =>
        {
            rsNameBox.IsEnabled = dllOverrideToggle.IsOn;
            dcNameBox.IsEnabled = dllOverrideToggle.IsOn;
        };

        var dllNameGrid = new Grid { ColumnSpacing = 8, Margin = new Thickness(0, 4, 0, 0) };
        dllNameGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        dllNameGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        Grid.SetColumn(rsNameBox, 0);
        Grid.SetColumn(dcNameBox, 1);
        dllNameGrid.Children.Add(rsNameBox);
        dllNameGrid.Children.Add(dcNameBox);

        var dllGroupPanel = new StackPanel { Spacing = 4 };
        dllGroupPanel.Children.Add(dllOverrideToggle);
        dllGroupPanel.Children.Add(dllNameGrid);
        var dllGroupBorder = new Border
        {
            Child = dllGroupPanel,
            Background = Brush("SurfaceOverlayBrush"),
            BorderBrush = Brush("BorderSubtleBrush"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(12, 10, 12, 12),
        };
        panel.Children.Add(dllGroupBorder);
        panel.Children.Add(MakeSeparator());

        // ── Update All exclusion ──
        var updateAllToggle = new ToggleSwitch
        {
            Header = "Update All",
            IsOn = !ViewModel.IsUpdateAllExcluded(gameName),
            OnContent = "Included in bulk updates",
            OffContent = "Excluded from bulk updates",
            Foreground = Brush("TextSecondaryBrush"),
            FontSize = 12,
        };
        ToolTipService.SetToolTip(updateAllToggle,
            "When enabled, this game is included in Update All RenoDX, Update All ReShade, and Update All DC.");
        panel.Children.Add(updateAllToggle);
        panel.Children.Add(MakeSeparator());

        // ── Wiki exclusion ──
        var wikiExcludeToggle = new ToggleSwitch
        {
            Header = "Wiki exclusion",
            IsOn = ViewModel.IsWikiExcluded(gameName),
            OnContent = "Excluded from wiki lookups",
            OffContent = "Included in wiki lookups",
            Foreground = Brush("TextSecondaryBrush"),
            FontSize = 12,
        };
        ToolTipService.SetToolTip(wikiExcludeToggle,
            "When enabled, this game will not be looked up on the RenoDX wiki. " +
            "Useful for games that share a name with an unrelated wiki entry.");
        panel.Children.Add(wikiExcludeToggle);

        // Wrap in a ScrollViewer for long content
        var scrollViewer = new ScrollViewer
        {
            Content = panel,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            MaxHeight = 500,
        };

        var flyout = new Flyout
        {
            Content = scrollViewer,
            Placement = FlyoutPlacementMode.BottomEdgeAlignedRight,
        };

        // On flyout closed, save all overrides and refresh the card
        var capturedName = gameName;
        flyout.Closed += (s, ev) =>
        {
            bool anyChanged = false;

            // ── Handle game rename ──
            var det = gameNameBox.Text?.Trim();
            var effectiveName = capturedName;

            if (!string.IsNullOrEmpty(capturedName) && !string.IsNullOrEmpty(det)
                && !det.Equals(capturedName, StringComparison.OrdinalIgnoreCase))
            {
                ViewModel.RenameGame(capturedName, det);
                effectiveName = det;
                anyChanged = true;
            }

            // ── Name mapping ──
            var wikiKey = wikiNameBox.Text?.Trim();
            var existingMapping = ViewModel.GetNameMapping(effectiveName);
            if (!string.IsNullOrEmpty(effectiveName) && !string.IsNullOrEmpty(wikiKey))
            {
                if (!string.Equals(wikiKey, existingMapping, StringComparison.OrdinalIgnoreCase))
                {
                    ViewModel.AddNameMapping(effectiveName, wikiKey);
                    anyChanged = true;
                }
            }
            else if (!string.IsNullOrEmpty(effectiveName) && string.IsNullOrEmpty(wikiKey) && !string.IsNullOrEmpty(existingMapping))
            {
                ViewModel.RemoveNameMapping(effectiveName);
                anyChanged = true;
            }

            // DC Mode override
            int? newDcMode = dcModeCombo.SelectedIndex switch { 1 => 0, 2 => 1, 3 => 2, _ => null };
            if (newDcMode != ViewModel.GetPerGameDcModeOverride(effectiveName))
            {
                ViewModel.SetPerGameDcModeOverride(effectiveName, newDcMode);
                ViewModel.ApplyDcModeSwitchForCard(effectiveName);
                anyChanged = true;
            }

            // Shader mode
            var shaderModeIdx = shaderModeCombo.SelectedIndex;
            var newShaderMode = shaderModeIdx >= 0 && shaderModeIdx < shaderModeValues.Length
                ? shaderModeValues[shaderModeIdx] : "Global";
            if (newShaderMode != ViewModel.GetPerGameShaderMode(effectiveName))
            {
                ViewModel.SetPerGameShaderMode(effectiveName, newShaderMode);
                ViewModel.DeployShadersForCard(effectiveName);
                anyChanged = true;
            }

            // DLL naming override
            bool nowDllOverride = dllOverrideToggle.IsOn;
            bool wasDllOverride = ViewModel.HasDllOverride(effectiveName);
            var targetCard = ViewModel.AllCards.FirstOrDefault(c =>
                c.GameName.Equals(effectiveName, StringComparison.OrdinalIgnoreCase));

            if (nowDllOverride && !wasDllOverride && targetCard != null)
            {
                var rsName = !string.IsNullOrWhiteSpace(rsNameBox.Text) ? rsNameBox.Text.Trim() : rsNameBox.PlaceholderText;
                var dcName = !string.IsNullOrWhiteSpace(dcNameBox.Text) ? dcNameBox.Text.Trim() : dcNameBox.PlaceholderText;
                ViewModel.EnableDllOverride(targetCard, rsName, dcName);
                anyChanged = true;
            }
            else if (nowDllOverride && wasDllOverride && targetCard != null)
            {
                var rsName = !string.IsNullOrWhiteSpace(rsNameBox.Text) ? rsNameBox.Text.Trim() : rsNameBox.PlaceholderText;
                var dcName = !string.IsNullOrWhiteSpace(dcNameBox.Text) ? dcNameBox.Text.Trim() : dcNameBox.PlaceholderText;
                ViewModel.UpdateDllOverrideNames(targetCard, rsName, dcName);
                anyChanged = true;
            }
            else if (!nowDllOverride && wasDllOverride && targetCard != null)
            {
                ViewModel.DisableDllOverride(targetCard);
                anyChanged = true;
            }

            // Update All (toggle is inverted: IsOn = included, so excluded = !IsOn)
            bool nowUaExcluded = !updateAllToggle.IsOn;
            if (nowUaExcluded != ViewModel.IsUpdateAllExcluded(effectiveName))
            {
                ViewModel.ToggleUpdateAllExclusion(effectiveName);
                anyChanged = true;
            }

            // Wiki exclusion
            if (wikiExcludeToggle.IsOn != ViewModel.IsWikiExcluded(effectiveName))
            {
                ViewModel.ToggleWikiExclusion(effectiveName);
                anyChanged = true;
            }

            if (!anyChanged) return;

            CrashReporter.Log($"Flyout overrides saved for: {effectiveName}");

            // Trigger pending reselect and restore selection (mirrors BuildOverridesPanel save logic)
            _pendingReselect = effectiveName;
            DispatcherQueue.TryEnqueue(TryRestoreSelection);

            // Refresh the card's status indicators
            card.NotifyAll();

            // Also rebuild the card in the grid if we're in grid mode
            if (ViewModel.IsGridLayout)
                RebuildCardGrid();
        };

        flyout.ShowAt(anchor);
    }

    /// <summary>Scrolls the card grid to bring the given card into view and highlights it.</summary>
    private void ScrollToCard(GameCardViewModel target)
    {
        foreach (var child in CardGridPanel.Children)
        {
            if (child is Border b && b.Tag is GameCardViewModel c)
            {
                bool isTarget = c == target;
                c.CardHighlighted = isTarget;
                if (isTarget)
                    b.StartBringIntoView();
            }
        }
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

    private void DetailScrollViewer_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        const double maxWidth = 850;
        const double padding = 48; // 24 left + 24 right
        var available = e.NewSize.Width - padding;
        DetailPanel.Width = available > maxWidth ? maxWidth : (available > 0 ? available : double.NaN);
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
            e.DragUIOverride.Caption = "Drop to add game, install addon, or extract archive";
            e.DragUIOverride.IsCaptionVisible = true;
            e.DragUIOverride.IsGlyphVisible = true;
        }
    }

    private static readonly HashSet<string> ArchiveExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".zip", ".7z", ".rar", ".tar", ".gz", ".bz2", ".xz", ".tar.gz", ".tgz", ".tar.bz2", ".tar.xz",
    };

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

            // Handle archive files — extract and look for .addon64/.addon32 inside
            if (ArchiveExtensions.Contains(ext))
            {
                try
                {
                    await ProcessDroppedArchive(file.Path);
                }
                catch (Exception ex)
                {
                    CrashReporter.Log($"DragDrop archive: error processing '{file.Path}': {ex.Message}");
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
    /// <summary>
    /// Handles a dropped archive file (.zip, .7z, .rar, etc.) — extracts it using 7-Zip,
    /// looks for .addon64/.addon32 files inside, and passes them to ProcessDroppedAddon.
    /// </summary>
    private async Task ProcessDroppedArchive(string archivePath)
    {
        var archiveName = Path.GetFileName(archivePath);
        CrashReporter.Log($"DragDrop archive: received '{archiveName}'");

        var sevenZipExe = Services.ReShadeExtractor.Find7ZipExe();
        if (sevenZipExe == null)
        {
            var errDialog = new ContentDialog
            {
                Title = "7-Zip Not Found",
                Content = "Cannot extract archive — 7-Zip was not found. Please reinstall RDXC.",
                CloseButtonText = "OK",
                XamlRoot = this.Content.XamlRoot,
            };
            await errDialog.ShowAsync();
            return;
        }

        // Extract entire archive to a temp directory
        var tempDir = Path.Combine(Path.GetTempPath(), $"rdxc_archive_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        try
        {
            var psi = new System.Diagnostics.ProcessStartInfo
            {
                FileName = sevenZipExe,
                Arguments = $"x \"{archivePath}\" -o\"{tempDir}\" -y",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };

            CrashReporter.Log($"DragDrop archive: extracting with {psi.FileName} {psi.Arguments}");

            using var proc = System.Diagnostics.Process.Start(psi);
            if (proc == null)
            {
                CrashReporter.Log("DragDrop archive: failed to start 7z process");
                return;
            }

            // Read output asynchronously to prevent deadlock
            var stdoutTask = proc.StandardOutput.ReadToEndAsync();
            var stderrTask = proc.StandardError.ReadToEndAsync();
            proc.WaitForExit(60_000); // 60 second timeout for large archives

            var stderr = await stderrTask;
            if (!string.IsNullOrWhiteSpace(stderr))
                CrashReporter.Log($"DragDrop archive: 7z stderr: {stderr}");

            if (proc.ExitCode != 0)
            {
                CrashReporter.Log($"DragDrop archive: 7z exit code {proc.ExitCode}");
                var failDialog = new ContentDialog
                {
                    Title = "Archive Extraction Failed",
                    Content = $"Failed to extract '{archiveName}'. The file may be corrupt or in an unsupported format.",
                    CloseButtonText = "OK",
                    XamlRoot = this.Content.XamlRoot,
                };
                await failDialog.ShowAsync();
                return;
            }

            // Search for .addon64 and .addon32 files in the extracted contents
            var addonFiles = Directory.GetFiles(tempDir, "*.addon64", SearchOption.AllDirectories)
                .Concat(Directory.GetFiles(tempDir, "*.addon32", SearchOption.AllDirectories))
                .ToList();

            if (addonFiles.Count == 0)
            {
                CrashReporter.Log($"DragDrop archive: no addon files found in '{archiveName}'");
                var noAddonDialog = new ContentDialog
                {
                    Title = "No Addon Found",
                    Content = $"No .addon64 or .addon32 files were found inside '{archiveName}'.",
                    CloseButtonText = "OK",
                    XamlRoot = this.Content.XamlRoot,
                };
                await noAddonDialog.ShowAsync();
                return;
            }

            CrashReporter.Log($"DragDrop archive: found {addonFiles.Count} addon file(s): [{string.Join(", ", addonFiles.Select(Path.GetFileName))}]");

            // If multiple addons found, let the user pick; otherwise use the single one
            string addonToInstall;
            if (addonFiles.Count == 1)
            {
                addonToInstall = addonFiles[0];
            }
            else
            {
                // Show a picker dialog for multiple addons
                var combo = new ComboBox
                {
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    PlaceholderText = "Select addon to install...",
                };
                foreach (var af in addonFiles)
                    combo.Items.Add(new ComboBoxItem { Content = Path.GetFileName(af), Tag = af });
                combo.SelectedIndex = 0;

                var pickDialog = new ContentDialog
                {
                    Title = $"Multiple Addons in '{archiveName}'",
                    Content = combo,
                    PrimaryButtonText = "Install",
                    CloseButtonText = "Cancel",
                    XamlRoot = this.Content.XamlRoot,
                };
                if (await pickDialog.ShowAsync() != ContentDialogResult.Primary) return;
                addonToInstall = (combo.SelectedItem as ComboBoxItem)?.Tag as string ?? addonFiles[0];
            }

            // Pass the extracted addon to the existing install flow
            await ProcessDroppedAddon(addonToInstall);
        }
        finally
        {
            // Clean up temp directory
            try { Directory.Delete(tempDir, recursive: true); } catch { }
        }
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

        // Style buttons based on active filter set
        var active   = ((SolidColorBrush)Application.Current.Resources["ChipActiveBrush"]).Color;
        var inactive = ((SolidColorBrush)Application.Current.Resources["ChipDefaultBrush"]).Color;
        var activeFg   = ((SolidColorBrush)Application.Current.Resources["TextPrimaryBrush"]).Color;
        var inactiveFg = ((SolidColorBrush)Application.Current.Resources["ChipTextBrush"]).Color;

        foreach (var b in new[] { FilterFavourites, FilterDetected, FilterUnreal, FilterUnity, FilterOther, FilterRenoDX, FilterLuma, FilterHidden })
        {
            bool isActive = ViewModel.ActiveFilters.Contains(b.Tag as string ?? "");
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
        if (_currentDetailCard != null) ViewModel.ToggleLumaMode(_currentDetailCard);
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
        string newLabel = card.UseUeExtended ? "UE Extended" : "Generic UE";
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

        // Update tooltip
        ToolTipService.SetToolTip(DetailUeExtendedBtn,
            card.UseUeExtended ? "Disable UE Extended" : "Enable UE Extended");

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

    private async void CardInfoLink_Click(object sender, RoutedEventArgs e)
    {
        var card = GetCardFromSender(sender);
        if (card?.NameUrl != null)
        {
            try { await Windows.System.Launcher.LaunchUriAsync(new Uri(card.NameUrl)); }
            catch (Exception ex) { CrashReporter.Log($"CardInfoLink_Click failed: {ex.Message}"); }
        }
    }

    private void CardNotesButton_Click(object sender, RoutedEventArgs e)
    {
        NotesButton_Click(sender, e);
    }

    private void Card_PointerPressed(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
    {
        try
        {
            if (sender is not Border b || b.Tag is not GameCardViewModel card) return;

            foreach (var c in ViewModel.DisplayedGames)
                c.CardHighlighted = false;

            card.CardHighlighted = true;
            ViewModel.SelectedGame = card;
        }
        catch { /* card may have been removed from visual tree */ }
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

    // --- Minimum window size enforcement via WndProc subclass ---
    private const int GWLP_WNDPROC = -4;
    private const int WM_GETMINMAXINFO = 0x0024;
    private const int MinWindowWidth = 900;
    private const int MinWindowHeight = 800;

    private IntPtr _hwnd;
    private IntPtr _origWndProc;
    private WndProcDelegate? _wndProcDelegate; // prevent GC

    private delegate IntPtr WndProcDelegate(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")]
    private static extern IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")]
    private static extern IntPtr GetWindowLongPtr(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll")]
    private static extern IntPtr CallWindowProc(IntPtr lpPrevWndFunc, IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

    [StructLayout(LayoutKind.Sequential)]
    private struct MINMAXINFO
    {
        public System.Drawing.Point ptReserved;
        public System.Drawing.Point ptMaxSize;
        public System.Drawing.Point ptMaxPosition;
        public System.Drawing.Point ptMinTrackSize;
        public System.Drawing.Point ptMaxTrackSize;
    }

    private IntPtr WndProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam)
    {
        if (msg == WM_GETMINMAXINFO)
        {
            var dpi = GetDpiForWindow(hWnd);
            var scale = dpi / 96.0;
            var mmi = Marshal.PtrToStructure<MINMAXINFO>(lParam);
            mmi.ptMinTrackSize = new System.Drawing.Point(
                (int)(MinWindowWidth * scale),
                (int)(MinWindowHeight * scale));
            Marshal.StructureToPtr(mmi, lParam, false);
            return IntPtr.Zero;
        }
        return CallWindowProc(_origWndProc, hWnd, msg, wParam, lParam);
    }

    [DllImport("user32.dll")]
    private static extern uint GetDpiForWindow(IntPtr hWnd);

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

                if (ViewModel.IsGridLayout)
                {
                    // In grid mode, scroll to and highlight the selected card
                    ScrollToCard(card);
                }
                else
                {
                    // In detail mode, populate the detail panel as before
                    PopulateDetailPanel(card);
                    DetailPanel.Visibility = Visibility.Visible;
                    BuildOverridesPanel(card);
                    OverridesContainer.Visibility = Visibility.Visible;
                }
            }
            else
            {
                ViewModel.SelectedGame = null;

                if (ViewModel.IsGridLayout)
                {
                    // Clear highlight from all cards
                    foreach (var child in CardGridPanel.Children)
                    {
                        if (child is Border b && b.Tag is GameCardViewModel c)
                            c.CardHighlighted = false;
                    }
                }
                else
                {
                    DetailPanel.Visibility = Visibility.Collapsed;
                    OverridesPanel.Children.Clear();
                    OverridesContainer.Visibility = Visibility.Collapsed;
                }
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
            // Set engine icon
            if (card.EngineHint.IndexOf("Unreal", StringComparison.OrdinalIgnoreCase) >= 0)
                DetailEngineIcon.Source = new Microsoft.UI.Xaml.Media.Imaging.BitmapImage(new Uri("ms-appx:///Assets/icons/unrealengine.ico"));
            else if (card.EngineHint.IndexOf("Unity", StringComparison.OrdinalIgnoreCase) >= 0)
                DetailEngineIcon.Source = new Microsoft.UI.Xaml.Media.Imaging.BitmapImage(new Uri("ms-appx:///Assets/icons/unity.ico"));
            else
                DetailEngineIcon.Source = null;
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
        var hasWikiLabel = !string.IsNullOrEmpty(card.WikiStatusLabel);
        DetailWikiText.Text = card.WikiStatusLabel;
        DetailWikiText.Foreground = new SolidColorBrush(ParseHexColor(card.WikiStatusBadgeForeground));
        DetailWikiBadge.Background = new SolidColorBrush(ParseHexColor(card.WikiStatusBadgeBackground));
        DetailWikiBadge.BorderBrush = new SolidColorBrush(ParseHexColor(card.WikiStatusBadgeBorderBrush));
        DetailWikiBadge.BorderThickness = new Thickness(1);
        DetailWikiBadge.Visibility = hasWikiLabel ? Visibility.Visible : Visibility.Collapsed;
        DetailSepPlatformStatus.Visibility = hasWikiLabel ? Visibility.Visible : Visibility.Collapsed;

        // Install path + installed file
        DetailInstallPath.Text = card.InstallPath;
        if (!string.IsNullOrEmpty(card.InstalledAddonFileName))
        {
            DetailInstalledFile.Text = $"{card.InstalledAddonFileName}";
            DetailInstalledFileBadge.Visibility = Visibility.Visible;
            DetailSepModPlatform.Visibility = Visibility.Visible;
        }
        else
        {
            DetailInstalledFileBadge.Visibility = Visibility.Collapsed;
            DetailSepModPlatform.Visibility = Visibility.Collapsed;
        }

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
        DetailHideIcon.Text = card.IsHidden ? "Show" : "Hide";
        DetailHideBtn.Foreground = card.IsHidden
            ? Brush("TextTertiaryBrush")
            : Brush("TextDisabledBrush");

        // Folder management buttons
        DetailFolderBtn.Tag = card;
        DetailChangeFolderBtn.Tag = card;
        DetailRemoveGameBtn.Tag = card;

        // Luma badge toggle
        if (card.LumaBadgeVisibility == Visibility.Visible)
        {
            DetailLumaBadgeContainer.Visibility = Visibility.Visible;
            DetailLumaToggle.IsChecked = card.IsLumaMode;
            UpdateLumaToggleStyle(card.IsLumaMode);
        }
        else
        {
            DetailLumaBadgeContainer.Visibility = Visibility.Collapsed;
        }

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
            DetailRsIniBtn.Opacity = card.RsIniExists ? 1 : 0.3;
            DetailRsDeleteBtn.Tag = card;
            var rsShow = card.RsDeleteVisibility == Visibility.Visible;
            DetailRsDeleteBtn.Opacity = rsShow ? 1 : 0;
            DetailRsDeleteBtn.IsHitTestVisible = rsShow;
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
            DetailDcIniBtn.Opacity = card.DcIniExists ? 1 : 0.3;
            DetailDcDeleteBtn.Tag = card;
            var dcShow = card.DcDeleteVisibility == Visibility.Visible;
            DetailDcDeleteBtn.Opacity = dcShow ? 1 : 0;
            DetailDcDeleteBtn.IsHitTestVisible = dcShow;
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
                DetailRdxDeleteBtn.Opacity = 0;
                DetailRdxDeleteBtn.IsHitTestVisible = false;
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
                var rdxShow = card.ReinstallRowVisibility == Visibility.Visible;
                DetailRdxDeleteBtn.Opacity = rdxShow ? 1 : 0;
                DetailRdxDeleteBtn.IsHitTestVisible = rdxShow;
            }
        }

        // Luma row
        if (isLumaMode)
        {
            DetailLumaRow.Visibility = Visibility.Visible;
            DetailLumaStatus.Text = card.LumaStatusText;
            DetailLumaStatus.Foreground = new SolidColorBrush(ParseColor(card.LumaStatusColor));
            DetailLumaInstallBtn.Tag = card;
            DetailLumaInstallBtn.Content = card.LumaActionLabel;
            DetailLumaInstallBtn.IsEnabled = card.IsLumaNotInstalling;
            DetailLumaInstallBtn.Background = new SolidColorBrush(ParseHexColor(card.LumaBtnBackground));
            DetailLumaInstallBtn.Foreground = new SolidColorBrush(ParseHexColor(card.LumaBtnForeground));
            DetailLumaInstallBtn.BorderBrush = new SolidColorBrush(ParseHexColor(card.LumaBtnBorderBrush));
            DetailLumaInstallBtn.BorderThickness = new Thickness(1);
            DetailLumaDeleteBtn.Tag = card;
            var lumaShow = card.LumaReinstallVisibility == Visibility.Visible;
            DetailLumaDeleteBtn.Opacity = lumaShow ? 1 : 0;
            DetailLumaDeleteBtn.IsHitTestVisible = lumaShow;
        }
        else DetailLumaRow.Visibility = Visibility.Collapsed;

        // UE-Extended flyout (inline in RenoDX row, column 3)
        if (card.UeExtendedToggleVisibility == Visibility.Visible && !isLumaMode)
        {
            DetailUeExtendedBtn.Opacity = 1;
            DetailUeExtendedBtn.IsHitTestVisible = true;
            DetailUeExtendedBtn.Tag = card;
            ToolTipService.SetToolTip(DetailUeExtendedBtn,
                card.UseUeExtended ? "Disable UE Extended" : "Enable UE Extended");
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
        else
        {
            DetailUeExtendedBtn.Opacity = 0;
            DetailUeExtendedBtn.IsHitTestVisible = false;
        }

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
            if (_currentDetailCard == null) return;
            UpdateDetailComponentRows(_currentDetailCard);

            // Refresh addon file badge when install state changes
            if (e.PropertyName is "InstalledAddonFileName" or "Status" or "ActionMessage")
            {
                if (!string.IsNullOrEmpty(_currentDetailCard.InstalledAddonFileName))
                {
                    DetailInstalledFile.Text = $"{_currentDetailCard.InstalledAddonFileName}";
                    DetailInstalledFileBadge.Visibility = Visibility.Visible;
                    DetailSepModPlatform.Visibility = Visibility.Visible;
                }
                else
                {
                    DetailInstalledFileBadge.Visibility = Visibility.Collapsed;
                    DetailSepModPlatform.Visibility = Visibility.Collapsed;
                }
            }

            // Refresh Luma mode buttons when luma state changes
            if (e.PropertyName is "IsLumaMode" or "LumaStatus" or "LumaBadgeVisibility" or "LumaBadgeLabel")
            {
                DetailLumaToggle.IsChecked = _currentDetailCard.IsLumaMode;
                UpdateLumaToggleStyle(_currentDetailCard.IsLumaMode);
            }
        });
    }

    private void UpdateLumaToggleStyle(bool isLumaMode)
    {
        DetailLumaToggleText.Text = isLumaMode ? "Luma Enabled" : "Luma Disabled";
        if (isLumaMode)
        {
            DetailLumaToggle.Background = Brush("AccentGreenBgBrush");
            DetailLumaToggle.Foreground = Brush("AccentGreenBrush");
            DetailLumaToggle.BorderBrush = Brush("AccentGreenBorderBrush");
        }
        else
        {
            DetailLumaToggle.Background = Brush("SurfaceOverlayBrush");
            DetailLumaToggle.Foreground = Brush("TextTertiaryBrush");
            DetailLumaToggle.BorderBrush = Brush("BorderStrongBrush");
        }
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
            Text = "Overrides",
            FontSize = 13,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            Foreground = Brush("TextPrimaryBrush"),
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

        // ── Per-game DC Mode + Shader mode (side by side) ─────────────────────────
        int? currentDcMode = ViewModel.GetPerGameDcModeOverride(gameName);
        var globalDcLabel = ViewModel.DcModeLevel switch { 1 => "DC Mode 1", 2 => "DC Mode 2", _ => "Off" };
        var dcModeOptions = new[] { $"Global ({globalDcLabel})", "Exclude (Off)", "DC Mode 1", "DC Mode 2" };
        var dcModeCombo = new ComboBox
        {
            ItemsSource = dcModeOptions,
            SelectedIndex = currentDcMode switch { null => 0, 0 => 1, 1 => 2, 2 => 3, _ => 0 },
            FontSize = 12,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Header = "DC Mode",
        };
        ToolTipService.SetToolTip(dcModeCombo,
            "Global = use the Settings DC Mode. Exclude (Off) = always use normal naming. " +
            "DC Mode 1 = force dxgi.dll proxy. DC Mode 2 = force winmm.dll proxy.");

        string currentShaderMode = ViewModel.GetPerGameShaderMode(gameName);
        var globalShaderLabel = ViewModel.ShaderDeployMode.ToString();
        var shaderModeValues = new[] { "Global", "Off", "Minimum", "All", "User" };
        var shaderModeDisplay = new[] { $"Global ({globalShaderLabel})", "Off", "Minimum", "All", "User" };
        int shaderSelectedIdx = Array.IndexOf(shaderModeValues, currentShaderMode);
        if (shaderSelectedIdx < 0) shaderSelectedIdx = 0;
        var shaderModeCombo = new ComboBox
        {
            ItemsSource = shaderModeDisplay,
            SelectedIndex = shaderSelectedIdx,
            FontSize = 12,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Header = "Shader Mode",
        };
        ToolTipService.SetToolTip(shaderModeCombo,
            "Global = follow the Settings toggle. Off = no shaders. Minimum = Lilium only. All = all packs. User = custom folder only.\n" +
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

        // ── DLL naming override (grouped in a border) ────────────────────────────
        bool isDllOverride = ViewModel.HasDllOverride(gameName);
        var existingCfg = ViewModel.GetDllOverride(gameName);
        bool is32Bit = card.Is32Bit;
        var defaultRsName = is32Bit ? "ReShade32.dll" : "ReShade64.dll";
        var defaultDcName = is32Bit ? "zzz_display_commander.addon32" : "zzz_display_commander.addon64";

        var dllOverrideToggle = new ToggleSwitch
        {
            Header = "DLL naming override",
            IsOn = isDllOverride,
            IsEnabled = !isLumaMode,
            OnContent = "Custom filenames enabled",
            OffContent = "Using default filenames",
            Foreground = Brush("TextSecondaryBrush"),
            FontSize = 12,
        };
        ToolTipService.SetToolTip(dllOverrideToggle,
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
        dllOverrideToggle.Toggled += (s, ev) => { rsNameBox.IsEnabled = dllOverrideToggle.IsOn; dcNameBox.IsEnabled = dllOverrideToggle.IsOn; };

        var dllNameGrid = new Grid { ColumnSpacing = 8, Margin = new Thickness(0, 4, 0, 0) };
        dllNameGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        dllNameGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        Grid.SetColumn(rsNameBox, 0);
        Grid.SetColumn(dcNameBox, 1);
        dllNameGrid.Children.Add(rsNameBox);
        dllNameGrid.Children.Add(dcNameBox);

        var dllGroupPanel = new StackPanel { Spacing = 4 };
        dllGroupPanel.Children.Add(dllOverrideToggle);
        dllGroupPanel.Children.Add(dllNameGrid);
        var dllGroupBorder = new Border
        {
            Child = dllGroupPanel,
            Background = Brush("SurfaceOverlayBrush"),
            BorderBrush = Brush("BorderSubtleBrush"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(12, 10, 12, 12),
        };
        OverridesPanel.Children.Add(dllGroupBorder);
        OverridesPanel.Children.Add(MakeSeparator());

        // ── Update All ───────────────────────────────────────────────────────────
        var updateAllToggle = new ToggleSwitch
        {
            Header = "Update All",
            IsOn = !ViewModel.IsUpdateAllExcluded(gameName),
            OnContent = "Included in bulk updates",
            OffContent = "Excluded from bulk updates",
            Foreground = Brush("TextSecondaryBrush"),
            FontSize = 12,
        };
        ToolTipService.SetToolTip(updateAllToggle,
            "When enabled, this game is included in Update All RenoDX, Update All ReShade, and Update All DC.");
        OverridesPanel.Children.Add(updateAllToggle);
        OverridesPanel.Children.Add(MakeSeparator());

        // ── Wiki exclusion ────────────────────────────────────────────────────────
        var wikiExcludeToggle = new ToggleSwitch
        {
            Header = "Wiki exclusion",
            IsOn = ViewModel.IsWikiExcluded(gameName),
            OnContent = "Excluded from wiki lookups",
            OffContent = "Included in wiki lookups",
            Foreground = Brush("TextSecondaryBrush"),
            FontSize = 12,
        };
        ToolTipService.SetToolTip(wikiExcludeToggle,
            "When enabled, this game will not be looked up on the RenoDX wiki. " +
            "Useful for games that share a name with an unrelated wiki entry.");
        OverridesPanel.Children.Add(wikiExcludeToggle);
        OverridesPanel.Children.Add(MakeSeparator());

        // ── Save button ──────────────────────────────────────────────────────────
        var saveBtn = new Button
        {
            Content = "Save Overrides",
            FontSize = 12,
            Padding = new Thickness(16, 8, 16, 8),
            Background = Brush("AccentBlueBgBrush"),
            Foreground = Brush("AccentBlueBrush"),
            BorderBrush = Brush("AccentBlueBorderBrush"),
            BorderThickness = new Thickness(1),
            HorizontalAlignment = HorizontalAlignment.Right,
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

            // Update All (toggle is inverted: IsOn = included, so excluded = !IsOn)
            bool nowUaExcluded = !updateAllToggle.IsOn;
            if (!string.IsNullOrEmpty(det) && nowUaExcluded != ViewModel.IsUpdateAllExcluded(det))
                ViewModel.ToggleUpdateAllExclusion(det);

            // Shader mode
            var shaderModeIdx = shaderModeCombo.SelectedIndex;
            var newShaderMode = shaderModeIdx >= 0 && shaderModeIdx < shaderModeValues.Length
                ? shaderModeValues[shaderModeIdx] : "Global";
            if (!string.IsNullOrEmpty(det) && newShaderMode != ViewModel.GetPerGameShaderMode(det))
            {
                ViewModel.SetPerGameShaderMode(det, newShaderMode);
                ViewModel.DeployShadersForCard(det);
            }

            // Wiki exclusion
            if (!string.IsNullOrEmpty(det) && wikiExcludeToggle.IsOn != ViewModel.IsWikiExcluded(det))
                ViewModel.ToggleWikiExclusion(det);

            // DLL naming override
            bool nowDllOverride = dllOverrideToggle.IsOn;
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
