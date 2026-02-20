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
    [ObservableProperty] private bool _isHidden = false;

    public bool IsGenericMod    { get; set; }
    public string EngineHint    { get; set; } = "";
    public bool IsExternalOnly  { get; set; }
    public string ExternalUrl   { get; set; } = "";
    public string ExternalLabel { get; set; } = "";
    public string? NexusUrl     { get; set; }
    public string? DiscordUrl   { get; set; }
    public string? NameUrl      { get; set; }   // Discussion/instructions link from wiki game name cell
    public string? Notes        { get; set; }
    public bool IsManuallyAdded { get; set; }
    public GameMod? Mod                       { get; set; }
    public DetectedGame? DetectedGame         { get; set; }
    public InstalledModRecord? InstalledRecord { get; set; }

    // ── Derived display ───────────────────────────────────────────────────────────

    public string WikiStatusLabel => WikiStatus == "✅" ? "✅ Working" : "🚧 In Progress";

    public string SourceIcon => Source switch
    {
        "Steam" => "🟦", "GOG" => "🟣", "Epic" => "🟤", "EA App" => "🟧",
        "Manual" => "🔧", _ => "🎮"
    };

    public string GenericModLabel => IsGenericMod
        ? (EngineHint.Contains("Unity") ? "Generic Unity" : "Generic UE") : "";

    public string InstallPathDisplay
    {
        get
        {
            if (string.IsNullOrEmpty(InstallPath)) return "";
            var parts = InstallPath.TrimEnd('\\', '/').Split(new[] { '\\', '/' }, StringSplitOptions.RemoveEmptyEntries);
            return parts.Length > 2 ? $"...\\{parts[^2]}\\{parts[^1]}" : InstallPath;
        }
    }

    public string InstalledFileLabel  => InstalledAddonFileName != null ? $"📦 {InstalledAddonFileName}" : "";
    public bool HasNotes              => !string.IsNullOrWhiteSpace(Notes);
    public bool CanInstall            => Mod?.SnapshotUrl != null && !IsInstalling && !IsExternalOnly;
    public bool IsUnityGeneric        => IsGenericMod && EngineHint.Contains("Unity");
    public bool HasDualBitMod         => Mod?.HasBothBitVersions == true;
    public bool HasExtraLinks         => NexusUrl != null || DiscordUrl != null;
    public bool HasNameUrl            => !string.IsNullOrEmpty(NameUrl);
    public string HideButtonLabel     => IsHidden ? "👁 Show" : "🚫 Hide";

    public string InstallActionLabel
    {
        get
        {
            if (IsInstalling) return "Installing...";
            return Status == GameStatus.UpdateAvailable ? "⬆  Update"
                 : Status == GameStatus.Installed       ? "↺  Reinstall"
                 : "⬇  Install";
        }
    }

    // ── Visibility ────────────────────────────────────────────────────────────────

    public Visibility SourceBadgeVisibility      => string.IsNullOrEmpty(Source) ? Visibility.Collapsed : Visibility.Visible;
    public Visibility GenericBadgeVisibility     => IsGenericMod ? Visibility.Visible : Visibility.Collapsed;
    public Visibility EngineBadgeVisibility      => !string.IsNullOrEmpty(EngineHint) ? Visibility.Visible : Visibility.Collapsed;
    public Visibility NotesButtonVisibility      => HasNotes ? Visibility.Visible : Visibility.Collapsed;
    public Visibility ProgressVisibility         => IsInstalling ? Visibility.Visible : Visibility.Collapsed;
    public Visibility MessageVisibility          => string.IsNullOrEmpty(ActionMessage) ? Visibility.Collapsed : Visibility.Visible;
    public Visibility ExternalBtnVisibility      => IsExternalOnly ? Visibility.Visible : Visibility.Collapsed;
    public Visibility ExtraLinkVisibility        => HasExtraLinks && !IsExternalOnly ? Visibility.Visible : Visibility.Collapsed;
    public Visibility InstalledFileLabelVisible  => !string.IsNullOrEmpty(InstalledAddonFileName) ? Visibility.Visible : Visibility.Collapsed;
    // Install single button (not unity dual-bit, not yet installed)
    public Visibility InstallOnlyBtnVisibility   => (!IsExternalOnly && Mod?.SnapshotUrl != null
                                                     && Status == GameStatus.Available
                                                     && !HasDualBitMod) ? Visibility.Visible : Visibility.Collapsed;
    // Reinstall/Update row (installed)
    public Visibility ReinstallRowVisibility     => (!IsExternalOnly && Mod?.SnapshotUrl != null
                                                     && (Status == GameStatus.Installed || Status == GameStatus.UpdateAvailable))
                                                     ? Visibility.Visible : Visibility.Collapsed;
    // Unity dual-bit install row
    public Visibility DualBitInstallVisibility   => (!IsExternalOnly && HasDualBitMod
                                                     && Status == GameStatus.Available) ? Visibility.Visible : Visibility.Collapsed;
    public Visibility UpdateBadgeVisibility      => Status == GameStatus.UpdateAvailable ? Visibility.Visible : Visibility.Collapsed;
    public Visibility IsHiddenVisibility         => IsHidden ? Visibility.Visible : Visibility.Collapsed;
    public Visibility IsNotHiddenVisibility      => IsHidden ? Visibility.Collapsed : Visibility.Visible;
    public Visibility NameLinkVisibility         => HasNameUrl ? Visibility.Visible : Visibility.Collapsed;

    public void NotifyAll()
    {
        OnPropertyChanged(nameof(InstallActionLabel));
        OnPropertyChanged(nameof(CanInstall));
        OnPropertyChanged(nameof(InstallOnlyBtnVisibility));
        OnPropertyChanged(nameof(ReinstallRowVisibility));
        OnPropertyChanged(nameof(DualBitInstallVisibility));
        OnPropertyChanged(nameof(ProgressVisibility));
        OnPropertyChanged(nameof(MessageVisibility));
        OnPropertyChanged(nameof(InstalledFileLabel));
        OnPropertyChanged(nameof(InstalledFileLabelVisible));
        OnPropertyChanged(nameof(InstallPathDisplay));
        OnPropertyChanged(nameof(UpdateBadgeVisibility));
        OnPropertyChanged(nameof(HideButtonLabel));
        OnPropertyChanged(nameof(IsHiddenVisibility));
        OnPropertyChanged(nameof(IsNotHiddenVisibility));
    }

    partial void OnStatusChanged(GameStatus v)              => NotifyAll();
    partial void OnIsInstallingChanged(bool v)              => NotifyAll();
    partial void OnInstalledAddonFileNameChanged(string? v) => NotifyAll();
    partial void OnActionMessageChanged(string v)           => OnPropertyChanged(nameof(MessageVisibility));
    partial void OnIsHiddenChanged(bool v)                  => OnPropertyChanged(nameof(HideButtonLabel));
    partial void OnInstallPathChanged(string v)             => OnPropertyChanged(nameof(InstallPathDisplay));
    partial void OnSourceChanged(string v)                  => OnPropertyChanged(nameof(SourceBadgeVisibility));
}
