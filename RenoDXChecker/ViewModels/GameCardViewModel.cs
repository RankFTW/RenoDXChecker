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

    [ObservableProperty] private bool _isExternalOnly;
    [ObservableProperty] private bool _isGenericMod;
    [ObservableProperty] private string _externalUrl   = "";
    [ObservableProperty] private string _externalLabel = "";
    [ObservableProperty] private string? _nexusUrl;
    [ObservableProperty] private string? _discordUrl;
    [ObservableProperty] private string? _notes;
    [ObservableProperty] private GameMod? _mod;

    // Plain properties — not mutated after card creation, no need to observe
    public string EngineHint    { get; set; } = "";
    public string? NameUrl      { get; set; }   // Discussion/instructions link from wiki game name cell
    public bool IsManuallyAdded { get; set; }
    public DetectedGame? DetectedGame         { get; set; }
    public InstalledModRecord? InstalledRecord { get; set; }

    // ── Derived display ───────────────────────────────────────────────────────────

    public string WikiStatusLabel => WikiStatus == "✅" ? "✅ Working"
                                   : WikiStatus == "🚧" ? "🚧 In Progress"
                                   : WikiStatus == "💬" ? "💬 Discord"
                                   : "❓ Unknown";

    // Badge colours change for the Discord status to make it visually distinct
    public string WikiStatusBadgeBackground => WikiStatus == "💬" ? "#1A1830" : "#1C2848";
    public string WikiStatusBadgeBorderBrush => WikiStatus == "💬" ? "#3A2860" : "#283C60";
    public string WikiStatusBadgeForeground  => WikiStatus == "💬" ? "#8878C8" : "#7A9AB8";

    // Update button colours — purple when an update is available, normal blue otherwise
    public string InstallBtnBackground  => Status == GameStatus.UpdateAvailable ? "#2A1A40" : "#22386A";
    public string InstallBtnForeground  => Status == GameStatus.UpdateAvailable ? "#C0A0E8" : "#AACCFF";
    public string InstallBtnBorderBrush => Status == GameStatus.UpdateAvailable ? "#6040A0" : "#3050A0";

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
    // Shown when the game has no known RenoDX mod and no install URL
    public Visibility NoModVisibility            => (Mod == null && string.IsNullOrEmpty(InstalledAddonFileName))
                                                     ? Visibility.Visible : Visibility.Collapsed;

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
        // Visibility props that depend on IsExternalOnly / Mod (plain computed properties)
        OnPropertyChanged(nameof(ExternalBtnVisibility));
        OnPropertyChanged(nameof(ExtraLinkVisibility));
        OnPropertyChanged(nameof(NoModVisibility));
        OnPropertyChanged(nameof(GenericBadgeVisibility));
        OnPropertyChanged(nameof(NotesButtonVisibility));
        OnPropertyChanged(nameof(HasNotes));
        OnPropertyChanged(nameof(HasDualBitMod));
        OnPropertyChanged(nameof(HasExtraLinks));
        OnPropertyChanged(nameof(WikiStatusLabel));
        OnPropertyChanged(nameof(WikiStatusBadgeBackground));
        OnPropertyChanged(nameof(WikiStatusBadgeBorderBrush));
        OnPropertyChanged(nameof(WikiStatusBadgeForeground));
        OnPropertyChanged(nameof(InstallBtnBackground));
        OnPropertyChanged(nameof(InstallBtnForeground));
        OnPropertyChanged(nameof(InstallBtnBorderBrush));
        OnPropertyChanged(nameof(GenericModLabel));
    }

    partial void OnStatusChanged(GameStatus v)              => NotifyAll();
    partial void OnIsInstallingChanged(bool v)              => NotifyAll();
    partial void OnInstalledAddonFileNameChanged(string? v) => NotifyAll();
    partial void OnActionMessageChanged(string v)           => OnPropertyChanged(nameof(MessageVisibility));
    partial void OnIsHiddenChanged(bool v)                  => OnPropertyChanged(nameof(HideButtonLabel));
    partial void OnInstallPathChanged(string v)             => OnPropertyChanged(nameof(InstallPathDisplay));
    partial void OnSourceChanged(string v)                  => OnPropertyChanged(nameof(SourceBadgeVisibility));
}
