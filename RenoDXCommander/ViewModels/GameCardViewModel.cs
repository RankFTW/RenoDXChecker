using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI.Xaml;
using RenoDXCommander.Models;
using RenoDXCommander.Services;

namespace RenoDXCommander.ViewModels;

public partial class GameCardViewModel : ObservableObject
{
    // ── Core observable properties ────────────────────────────────────────────────
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
    [ObservableProperty] private bool _isFavourite = false;

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
    [ObservableProperty] private string?    _dcInstalledVersion;
    public AuxInstalledRecord? DcRecord { get; set; }

    // ── ReShade state ─────────────────────────────────────────────────────────────
    [ObservableProperty] private GameStatus _rsStatus  = GameStatus.NotInstalled;
    [ObservableProperty] private bool       _rsIsInstalling;
    [ObservableProperty] private double     _rsProgress;
    [ObservableProperty] private string     _rsActionMessage = "";
    [ObservableProperty] private string?    _rsInstalledFile;
    [ObservableProperty] private string?    _rsInstalledVersion;
    public AuxInstalledRecord? RsRecord { get; set; }

    // Plain properties — not mutated after card creation, no need to observe
    public string EngineHint    { get; set; } = "";
    public string? NameUrl      { get; set; }
    /// <summary>Per-game DC Mode override: null = follow global, 0 = force off, 1 = force DC Mode 1, 2 = force DC Mode 2.</summary>
    public int? PerGameDcMode        { get; set; }
    /// <summary>True when any per-game DC Mode override is set (game does not follow global toggle).</summary>
    public bool DcModeExcluded       => PerGameDcMode.HasValue;
    public bool ExcludeFromUpdateAllReShade { get; set; }
    public bool ExcludeFromUpdateAllDc      { get; set; }
    public bool ExcludeFromUpdateAllRenoDx  { get; set; }
    public bool ExcludeFromShaders   { get; set; }
    /// <summary>Per-game shader mode override: null = follow global, "Off"/"Minimum"/"All"/"User".</summary>
    public string? ShaderModeOverride { get; set; }

    // ── 32-bit mode ───────────────────────────────────────────────────────────────
    [ObservableProperty] private bool _is32Bit = false;

    // ── DC Mode ReShade blocking ──────────────────────────────────────────────────
    /// <summary>True when DC Mode is active for this game — ReShade install is blocked on the card.</summary>
    [ObservableProperty] private bool _rsBlockedByDcMode;

    // ── DLL Naming Override ─────────────────────────────────────────────────────
    [ObservableProperty] private bool _dllOverrideEnabled = false;

    // ── Luma Framework state ──────────────────────────────────────────────────────
    [ObservableProperty] private bool _isLumaMode = false;
    [ObservableProperty] private bool _isLumaInstalling = false;
    [ObservableProperty] private double _lumaProgress = 0;
    [ObservableProperty] private string _lumaActionMessage = "";
    [ObservableProperty] private GameStatus _lumaStatus = GameStatus.NotInstalled;
    public LumaMod? LumaMod { get; set; }
    public LumaInstalledRecord? LumaRecord { get; set; }
    /// <summary>Global toggle — Luma UI is always enabled.</summary>
    [ObservableProperty] private bool _lumaFeatureEnabled = true;

    // ── Sidebar selection state ───────────────────────────────────────────────────
    [ObservableProperty] private bool _isSelected;

    // ── Card grid highlight state ─────────────────────────────────────────────────
    [ObservableProperty] private bool _cardHighlighted = false;

    // ── Component detail expand/collapse ──────────────────────────────────────────
    [ObservableProperty] private bool _componentExpanded;

    // ── Plain properties ──────────────────────────────────────────────────────────
    public string? NotesUrl     { get; set; }
    public string? NotesUrlLabel { get; set; }
    public string? LumaNotes     { get; set; }
    public string? LumaNotesUrl  { get; set; }
    public string? LumaNotesUrlLabel { get; set; }
    public bool IsManuallyAdded { get; set; }
    public DetectedGame? DetectedGame         { get; set; }
    public InstalledModRecord? InstalledRecord { get; set; }

    /// <summary>Set by MainViewModel for games that default to UE-Extended + Native HDR label.</summary>
    public bool IsNativeHdrGame { get; set; }

    /// <summary>Set by MainViewModel for games in the manifest ueExtendedGames list (but NOT native HDR).</summary>
    public bool IsManifestUeExtended { get; set; }

    /// <summary>
    /// Refreshes all computed properties. Called by external code after bulk state changes.
    /// Internally delegates to targeted notification methods for each component.
    /// </summary>
    public void NotifyAll()
    {
        // RenoDX dependents
        NotifyStatusDependents();
        NotifyIsInstallingDependents();

        // Display Commander dependents
        NotifyDcStatusDependents();
        NotifyDcIsInstallingDependents();

        // ReShade dependents
        NotifyRsStatusDependents();
        NotifyRsIsInstallingDependents();

        // Luma dependents
        NotifyLumaStatusDependents();
        NotifyIsLumaInstallingDependents();
        NotifyLumaFeatureEnabledDependents();
        NotifyIsLumaModeDependents();

        // Properties not covered by targeted methods above
        OnPropertyChanged(nameof(DualBitInstallVisibility));
        OnPropertyChanged(nameof(InstalledFileLabel));
        OnPropertyChanged(nameof(InstalledFileLabelVisible));
        OnPropertyChanged(nameof(InstallPathDisplay));
        OnPropertyChanged(nameof(HideButtonLabel));
        OnPropertyChanged(nameof(StarForeground));
        OnPropertyChanged(nameof(IsFavouriteVisibility));
        OnPropertyChanged(nameof(IsNotFavouriteVisibility));
        OnPropertyChanged(nameof(IsHiddenVisibility));
        OnPropertyChanged(nameof(IsNotHiddenVisibility));
        OnPropertyChanged(nameof(GenericBadgeVisibility));
        OnPropertyChanged(nameof(NotesButtonVisibility));
        OnPropertyChanged(nameof(HasNotes));
        OnPropertyChanged(nameof(HasDualBitMod));
        OnPropertyChanged(nameof(WikiStatusLabel));
        OnPropertyChanged(nameof(WikiStatusBadgeBackground));
        OnPropertyChanged(nameof(WikiStatusBadgeBorderBrush));
        OnPropertyChanged(nameof(WikiStatusBadgeForeground));
        OnPropertyChanged(nameof(Is32BitBadgeVisibility));
        OnPropertyChanged(nameof(Is32BitUeWipVisibility));
        OnPropertyChanged(nameof(UeExtendedLabel));
        OnPropertyChanged(nameof(UeExtendedBackground));
        OnPropertyChanged(nameof(UeExtendedForeground));
        OnPropertyChanged(nameof(UeExtendedBorderBrush));
        OnPropertyChanged(nameof(UeExtendedToggleVisibility));
        OnPropertyChanged(nameof(ComponentDetailVisibility));
        OnPropertyChanged(nameof(ExpandChevron));
        OnPropertyChanged(nameof(RsIniExists));
        OnPropertyChanged(nameof(DcIniExists));
        OnPropertyChanged(nameof(RsBlockedByDcMode));
        OnPropertyChanged(nameof(IsLumaAvailable));
        OnPropertyChanged(nameof(HasInfoIndicator));
    }
}
