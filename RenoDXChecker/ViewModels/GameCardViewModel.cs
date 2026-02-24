using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI.Xaml;
using RenoDXCommander.Models;
using RenoDXCommander.Services;

namespace RenoDXCommander.ViewModels;

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
    [ObservableProperty] private bool _useUeExtended;

    // ── Display Commander state ──────────────────────────────────────────────────
    [ObservableProperty] private GameStatus _dcStatus  = GameStatus.NotInstalled;
    [ObservableProperty] private bool       _dcIsInstalling;
    [ObservableProperty] private double     _dcProgress;
    [ObservableProperty] private string     _dcActionMessage = "";
    [ObservableProperty] private string?    _dcInstalledFile;
    public AuxInstalledRecord? DcRecord { get; set; }

    // ── ReShade state ─────────────────────────────────────────────────────────────
    [ObservableProperty] private GameStatus _rsStatus  = GameStatus.NotInstalled;
    [ObservableProperty] private bool       _rsIsInstalling;
    [ObservableProperty] private double     _rsProgress;
    [ObservableProperty] private string     _rsActionMessage = "";
    [ObservableProperty] private string?    _rsInstalledFile;
    public AuxInstalledRecord? RsRecord { get; set; }

    // Plain properties — not mutated after card creation, no need to observe
    public string EngineHint    { get; set; } = "";
    public string? NameUrl      { get; set; }   // Discussion/instructions link from wiki game name cell
    /// <summary>When true this game ignores the global DC Mode toggle and always uses normal naming.</summary>
    public bool DcModeExcluded  { get; set; }

    // ── INI preset existence (re-checked on every NotifyAll call) ─────────────────
    /// <summary>True when reshade.ini is present in the inis folder — enables the 📋 button.</summary>
    public bool RsIniExists => File.Exists(AuxInstallService.RsIniPath);
    /// <summary>True when DisplayCommander.toml is present in the inis folder — enables the 📋 button.</summary>
    public bool DcIniExists => File.Exists(AuxInstallService.DcIniPath);

    // INI button corner radius: rounded right when it is the rightmost button (delete hidden)
    private bool RsDeleteVisible => RsStatus == GameStatus.Installed || RsStatus == GameStatus.UpdateAvailable;
    private bool DcDeleteVisible => DcStatus == GameStatus.Installed || DcStatus == GameStatus.UpdateAvailable;

    public string RsIniCornerRadius    => RsDeleteVisible ? "0"        : "0,10,10,0";
    public string RsIniBorderThickness => RsDeleteVisible ? "0,1,0,1"  : "0,1,1,1";
    public string RsIniMargin          => RsDeleteVisible ? "0,0,1,0"  : "0";

    public string DcIniCornerRadius    => DcDeleteVisible ? "0"        : "0,10,10,0";
    public string DcIniBorderThickness => DcDeleteVisible ? "0,1,0,1"  : "0,1,1,1";
    public string DcIniMargin          => DcDeleteVisible ? "0,0,1,0"  : "0";
    public string? NotesUrl     { get; set; }   // Clickable link embedded in the notes dialog
    public string? NotesUrlLabel { get; set; }  // Display label for the notes link
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

    // UE-Extended toggle label and styling
    public string UeExtendedLabel      => UseUeExtended ? "⚡ UE Extended ON" : "⚡ UE Extended";
    public string UeExtendedBackground => UseUeExtended ? "#241840" : "#141A2C";
    public string UeExtendedForeground => UseUeExtended ? "#B090E8" : "#404870";
    public string UeExtendedBorderBrush => UseUeExtended ? "#5030A0" : "#202840";
    // Visible only on Generic UE cards (not Unity, not specific-mod cards)
    public Visibility UeExtendedToggleVisibility =>
        (IsGenericMod && EngineHint.Contains("Unreal") && !EngineHint.Contains("Legacy"))
            ? Visibility.Visible : Visibility.Collapsed;

    // ── DC / ReShade action labels ───────────────────────────────────────────────
    // ── Dynamic corner radius for install buttons ────────────────────────────────
    // Row 7b: Install-only variant — round right side if UE-Extended not visible
    public string R7bInstallCornerRadius     => UeExtendedToggleVisibility == Visibility.Visible ? "10,0,0,10" : "10";
    public string R7bInstallBorderThickness  => UeExtendedToggleVisibility == Visibility.Visible ? "1,1,0,1"   : "1";
    public string R7bInstallMargin           => UeExtendedToggleVisibility == Visibility.Visible ? "0,0,1,0"   : "0";

    // ── Dynamic corner radius for install buttons ────────────────────────────────
    // When the delete button is hidden, the install button rounds its right corners
    public string RsInstallCornerRadius => (RsStatus == GameStatus.Installed || RsStatus == GameStatus.UpdateAvailable)
        ? "10,0,0,10" : "10";
    public string DcInstallCornerRadius => (DcStatus == GameStatus.Installed || DcStatus == GameStatus.UpdateAvailable)
        ? "10,0,0,10" : "10";
    // RS border: right border only when delete is hidden (full border when no delete)
    public string RsInstallBorderThickness => (RsStatus == GameStatus.Installed || RsStatus == GameStatus.UpdateAvailable)
        ? "1,1,0,1" : "1";
    public string DcInstallBorderThickness => (DcStatus == GameStatus.Installed || DcStatus == GameStatus.UpdateAvailable)
        ? "1,1,0,1" : "1";
    // RS/DC margin: right margin gap only when delete button follows
    public string RsInstallMargin => (RsStatus == GameStatus.Installed || RsStatus == GameStatus.UpdateAvailable)
        ? "0,0,1,0" : "0";
    public string DcInstallMargin => (DcStatus == GameStatus.Installed || DcStatus == GameStatus.UpdateAvailable)
        ? "0,0,1,0" : "0";

    // Negated installing flags — used for IsEnabled bindings to avoid converter in DataTemplate
    public bool IsNotInstalling   => !IsInstalling;
    public bool IsDcNotInstalling => !DcIsInstalling;
    public bool IsRsNotInstalling => !RsIsInstalling;

    public string DcActionLabel
    {
        get
        {
            if (DcIsInstalling) return "Installing...";
            return DcStatus == GameStatus.UpdateAvailable ? "⬆  Update Display Commander"
                 : DcStatus == GameStatus.Installed       ? "↺  Reinstall Display Commander"
                 : "⬇  Install Display Commander";
        }
    }
    public string RsActionLabel
    {
        get
        {
            if (RsIsInstalling) return "Installing...";
            return RsStatus == GameStatus.UpdateAvailable ? "⬆  Update ReShade"
                 : RsStatus == GameStatus.Installed       ? "↺  Reinstall ReShade"
                 : "⬇  Install ReShade";
        }
    }

    // Background colours for DC/RS buttons (purple tint when update available, blue otherwise)
    public string DcBtnBackground  => DcStatus == GameStatus.UpdateAvailable ? "#2A1A40" : "#22386A";
    public string DcBtnForeground  => DcStatus == GameStatus.UpdateAvailable ? "#C0A0E8" : "#AACCFF";
    public string DcBtnBorderBrush => DcStatus == GameStatus.UpdateAvailable ? "#6040A0" : "#3050A0";
    public string RsBtnBackground  => RsStatus == GameStatus.UpdateAvailable ? "#2A1A40" : "#22386A";
    public string RsBtnForeground  => RsStatus == GameStatus.UpdateAvailable ? "#C0A0E8" : "#AACCFF";
    public string RsBtnBorderBrush => RsStatus == GameStatus.UpdateAvailable ? "#6040A0" : "#3050A0";

    public Visibility DcProgressVisibility => DcIsInstalling ? Visibility.Visible : Visibility.Collapsed;
    public Visibility DcMessageVisibility  => string.IsNullOrEmpty(DcActionMessage) ? Visibility.Collapsed : Visibility.Visible;
    public Visibility DcInstalledVisible   => !string.IsNullOrEmpty(DcInstalledFile) ? Visibility.Visible : Visibility.Collapsed;
    public Visibility DcDeleteVisibility   => DcStatus == GameStatus.Installed || DcStatus == GameStatus.UpdateAvailable
                                               ? Visibility.Visible : Visibility.Collapsed;
    public Visibility RsProgressVisibility => RsIsInstalling ? Visibility.Visible : Visibility.Collapsed;
    public Visibility RsMessageVisibility  => string.IsNullOrEmpty(RsActionMessage) ? Visibility.Collapsed : Visibility.Visible;
    public Visibility RsInstalledVisible   => !string.IsNullOrEmpty(RsInstalledFile) ? Visibility.Visible : Visibility.Collapsed;
    public Visibility RsDeleteVisibility   => RsStatus == GameStatus.Installed || RsStatus == GameStatus.UpdateAvailable
                                               ? Visibility.Visible : Visibility.Collapsed;

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
            return Status == GameStatus.UpdateAvailable ? "⬆  Update RenoDX"
                 : Status == GameStatus.Installed       ? "↺  Reinstall RenoDX"
                 : "⬇  Install RenoDX";
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
        OnPropertyChanged(nameof(UeExtendedLabel));
        OnPropertyChanged(nameof(UeExtendedBackground));
        OnPropertyChanged(nameof(UeExtendedForeground));
        OnPropertyChanged(nameof(UeExtendedBorderBrush));
        OnPropertyChanged(nameof(UeExtendedToggleVisibility));
        OnPropertyChanged(nameof(IsNotInstalling));
        OnPropertyChanged(nameof(IsDcNotInstalling));
        OnPropertyChanged(nameof(IsRsNotInstalling));
        OnPropertyChanged(nameof(RsIniExists));
        OnPropertyChanged(nameof(DcIniExists));
        OnPropertyChanged(nameof(RsIniCornerRadius));
        OnPropertyChanged(nameof(RsIniBorderThickness));
        OnPropertyChanged(nameof(RsIniMargin));
        OnPropertyChanged(nameof(DcIniCornerRadius));
        OnPropertyChanged(nameof(DcIniBorderThickness));
        OnPropertyChanged(nameof(DcIniMargin));
        OnPropertyChanged(nameof(R7bInstallCornerRadius));
        OnPropertyChanged(nameof(R7bInstallBorderThickness));
        OnPropertyChanged(nameof(R7bInstallMargin));
        // DC/RS corner radius
        OnPropertyChanged(nameof(RsInstallCornerRadius));
        OnPropertyChanged(nameof(RsInstallBorderThickness));
        OnPropertyChanged(nameof(RsInstallMargin));
        OnPropertyChanged(nameof(DcInstallCornerRadius));
        OnPropertyChanged(nameof(DcInstallBorderThickness));
        OnPropertyChanged(nameof(DcInstallMargin));
        // DC
        OnPropertyChanged(nameof(DcActionLabel));
        OnPropertyChanged(nameof(DcBtnBackground));
        OnPropertyChanged(nameof(DcBtnForeground));
        OnPropertyChanged(nameof(DcBtnBorderBrush));
        OnPropertyChanged(nameof(DcProgressVisibility));
        OnPropertyChanged(nameof(DcMessageVisibility));
        OnPropertyChanged(nameof(DcInstalledVisible));
        OnPropertyChanged(nameof(DcDeleteVisibility));
        // ReShade
        OnPropertyChanged(nameof(RsActionLabel));
        OnPropertyChanged(nameof(RsBtnBackground));
        OnPropertyChanged(nameof(RsBtnForeground));
        OnPropertyChanged(nameof(RsBtnBorderBrush));
        OnPropertyChanged(nameof(RsProgressVisibility));
        OnPropertyChanged(nameof(RsMessageVisibility));
        OnPropertyChanged(nameof(RsInstalledVisible));
        OnPropertyChanged(nameof(RsDeleteVisibility));
    }

    partial void OnStatusChanged(GameStatus v)              => NotifyAll();
    partial void OnDcStatusChanged(GameStatus v)            => NotifyAll();
    partial void OnRsStatusChanged(GameStatus v)            => NotifyAll();
    partial void OnDcIsInstallingChanged(bool v)            => NotifyAll();
    partial void OnRsIsInstallingChanged(bool v)            => NotifyAll();
    partial void OnIsInstallingChanged(bool v)              => NotifyAll();
    partial void OnInstalledAddonFileNameChanged(string? v) => NotifyAll();
    partial void OnActionMessageChanged(string v)           => OnPropertyChanged(nameof(MessageVisibility));
    partial void OnDcActionMessageChanged(string v)         => OnPropertyChanged(nameof(DcMessageVisibility));
    partial void OnRsActionMessageChanged(string v)         => OnPropertyChanged(nameof(RsMessageVisibility));
    partial void OnIsHiddenChanged(bool v)                  => OnPropertyChanged(nameof(HideButtonLabel));
    partial void OnInstallPathChanged(string v)             => OnPropertyChanged(nameof(InstallPathDisplay));
    partial void OnSourceChanged(string v)                  => OnPropertyChanged(nameof(SourceBadgeVisibility));
}
