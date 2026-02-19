using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI.Xaml;
using RenoDXChecker.Models;

namespace RenoDXChecker.ViewModels;

public enum GameStatus { NotInstalled, Available, Installed, UpdateAvailable }

public partial class GameCardViewModel : ObservableObject
{
    [ObservableProperty] private string _gameName = "";
    [ObservableProperty] private string _maintainer = "";
    [ObservableProperty] private string _source = "";
    [ObservableProperty] private string _installPath = "";
    [ObservableProperty] private string _wikiStatus = "✅";
    [ObservableProperty] private GameStatus _status = GameStatus.NotInstalled;
    [ObservableProperty] private bool _isInstalling = false;
    [ObservableProperty] private double _installProgress = 0;
    [ObservableProperty] private string _actionMessage = "";
    [ObservableProperty] private string? _installedAddonFileName;

    public bool IsGenericMod   { get; set; }
    public string EngineHint   { get; set; } = "";
    public bool IsExternalOnly { get; set; }
    public string ExternalUrl  { get; set; } = "";
    public string ExternalLabel { get; set; } = "";
    public string? Notes       { get; set; }

    public GameMod? Mod                        { get; set; }
    public DetectedGame? DetectedGame          { get; set; }
    public InstalledModRecord? InstalledRecord { get; set; }

    // ── Derived display ───────────────────────────────────────────────────────────

    public string WikiStatusLabel => WikiStatus == "✅" ? "✅ Working" : "🚧 In Progress";

    public string SourceIcon => Source switch
    {
        "Steam" => "🟦", "GOG" => "🟣", "Epic" => "🟤", "EA App" => "🟧", _ => "🎮"
    };

    public string InstallButtonLabel => IsInstalling ? "Installing..."
        : Status == GameStatus.Installed ? "↺  Reinstall"
        : "⬇  Install";

    public string GenericModLabel => IsGenericMod
        ? (EngineHint.Contains("Unity") ? "Generic Unity" : "Generic UE")
        : "";

    public string InstallPathDisplay
    {
        get
        {
            if (string.IsNullOrEmpty(InstallPath)) return "";
            var parts = InstallPath.TrimEnd('\\', '/').Split(new[] { '\\', '/' }, StringSplitOptions.RemoveEmptyEntries);
            return parts.Length > 2 ? $"...\\{parts[^2]}\\{parts[^1]}" : InstallPath;
        }
    }

    public string InstalledFileLabel => InstalledAddonFileName != null
        ? $"📦 {InstalledAddonFileName}" : "";

    public bool HasNotes         => !string.IsNullOrWhiteSpace(Notes);
    public bool CanInstall       => Mod?.SnapshotUrl != null && !IsInstalling && !IsExternalOnly;
    public bool IsUnityGeneric   => IsGenericMod && EngineHint.Contains("Unity");
    public bool HasDualBitMod    => Mod?.HasBothBitVersions == true;

    // ── Visibility ────────────────────────────────────────────────────────────────

    public Visibility DetectedVisibility        => DetectedGame != null || !string.IsNullOrEmpty(InstallPath) ? Visibility.Visible : Visibility.Collapsed;
    public Visibility NotDetectedVisibility     => DetectedGame != null || !string.IsNullOrEmpty(InstallPath) ? Visibility.Collapsed : Visibility.Visible;
    public Visibility SourceBadgeVisibility     => string.IsNullOrEmpty(Source) ? Visibility.Collapsed : Visibility.Visible;
    public Visibility GenericBadgeVisibility    => IsGenericMod ? Visibility.Visible : Visibility.Collapsed;
    public Visibility EngineBadgeVisibility     => !string.IsNullOrEmpty(EngineHint) ? Visibility.Visible : Visibility.Collapsed;
    public Visibility NotesButtonVisibility     => HasNotes ? Visibility.Visible : Visibility.Collapsed;
    public Visibility ProgressVisibility        => IsInstalling ? Visibility.Visible : Visibility.Collapsed;
    public Visibility MessageVisibility         => string.IsNullOrEmpty(ActionMessage) ? Visibility.Collapsed : Visibility.Visible;
    public Visibility ExternalBtnVisibility     => IsExternalOnly ? Visibility.Visible : Visibility.Collapsed;
    public Visibility InstallBtnVisibility      => (!IsExternalOnly && Mod?.SnapshotUrl != null) ? Visibility.Visible : Visibility.Collapsed;
    // Install-only (not yet installed) — shows plain ⬇ Install button
    public Visibility InstallOnlyBtnVisibility  => (!IsExternalOnly && Mod?.SnapshotUrl != null && Status != GameStatus.Installed && !HasDualBitMod) ? Visibility.Visible : Visibility.Collapsed;
    // Reinstall+Delete row — shows when already installed
    public Visibility ReinstallRowVisibility    => (!IsExternalOnly && Mod?.SnapshotUrl != null && Status == GameStatus.Installed) ? Visibility.Visible : Visibility.Collapsed;
    public Visibility InstalledFileLabelVisible => !string.IsNullOrEmpty(InstalledAddonFileName) ? Visibility.Visible : Visibility.Collapsed;
    // Unity generic: show split 32/64 buttons instead of single install
    public Visibility DualBitInstallVisibility => (!IsExternalOnly && HasDualBitMod && Status != GameStatus.Installed) ? Visibility.Visible : Visibility.Collapsed;

    public void NotifyAll()
    {
        OnPropertyChanged(nameof(InstallButtonLabel));
        OnPropertyChanged(nameof(CanInstall));
        OnPropertyChanged(nameof(InstallBtnVisibility));
        OnPropertyChanged(nameof(InstallOnlyBtnVisibility));
        OnPropertyChanged(nameof(ReinstallRowVisibility));
        OnPropertyChanged(nameof(DualBitInstallVisibility));
        OnPropertyChanged(nameof(ProgressVisibility));
        OnPropertyChanged(nameof(MessageVisibility));
        OnPropertyChanged(nameof(InstalledFileLabel));
        OnPropertyChanged(nameof(InstalledFileLabelVisible));
        OnPropertyChanged(nameof(DetectedVisibility));
        OnPropertyChanged(nameof(NotDetectedVisibility));
        OnPropertyChanged(nameof(InstallPathDisplay));
    }

    partial void OnStatusChanged(GameStatus v)              => NotifyAll();
    partial void OnIsInstallingChanged(bool v)              => NotifyAll();
    partial void OnInstalledAddonFileNameChanged(string? v) => NotifyAll();
    partial void OnActionMessageChanged(string v)           => OnPropertyChanged(nameof(MessageVisibility));
    partial void OnInstallPathChanged(string v)
    {
        OnPropertyChanged(nameof(DetectedVisibility));
        OnPropertyChanged(nameof(NotDetectedVisibility));
        OnPropertyChanged(nameof(InstallPathDisplay));
    }
    partial void OnSourceChanged(string v) => OnPropertyChanged(nameof(SourceBadgeVisibility));
}
