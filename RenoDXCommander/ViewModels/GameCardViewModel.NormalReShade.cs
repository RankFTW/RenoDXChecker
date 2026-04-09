namespace RenoDXCommander.ViewModels;

// Normal-ReShade computed properties – addons disabled / strikethrough
public partial class GameCardViewModel
{
    /// <summary>True when addons should be disabled for this game (normal ReShade mode).</summary>
    public bool AddonsDisabled => UseNormalReShade;

    /// <summary>Addon rows should show strikethrough text when normal ReShade is active.</summary>
    public bool AddonStrikethrough => UseNormalReShade;

    /// <summary>
    /// Called by CommunityToolkit.Mvvm when <see cref="UseNormalReShade"/> changes.
    /// Triggers property-change notifications for all addon-related computed dependents.
    /// </summary>
    partial void OnUseNormalReShadeChanged(bool value)
    {
        OnPropertyChanged(nameof(AddonsDisabled));
        OnPropertyChanged(nameof(AddonStrikethrough));
        OnPropertyChanged(nameof(DcInstallEnabled));
        OnPropertyChanged(nameof(UlInstallEnabled));
    }
}
