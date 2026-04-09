using FsCheck;
using FsCheck.Xunit;
using RenoDXCommander.Models;
using RenoDXCommander.ViewModels;

namespace RenoDXCommander.Tests;

/// <summary>
/// Property-based tests for addon UI disabled state when UseNormalReShade is active.
/// Feature: reshade-no-addon-support, Property 3: Addon UI disabled when UseNormalReShade is active
/// **Validates: Requirements 4.1, 4.3**
/// </summary>
public class NormalReShadeAddonUiDisabledPropertyTests
{
    // ── Generators ────────────────────────────────────────────────────────────────

    private static readonly Gen<GameStatus> GenStatus =
        Gen.Elements(GameStatus.NotInstalled, GameStatus.Available,
                     GameStatus.Installed, GameStatus.UpdateAvailable);

    /// <summary>
    /// Generates a GameCardViewModel with random DC/UL state combinations.
    /// </summary>
    private static readonly Gen<GameCardViewModel> GenCard =
        from dcStatus in GenStatus
        from ulStatus in GenStatus
        from dcInstalling in Arb.Default.Bool().Generator
        from ulInstalling in Arb.Default.Bool().Generator
        from is32Bit in Arb.Default.Bool().Generator
        select new GameCardViewModel
        {
            DcStatus = dcStatus,
            UlStatus = ulStatus,
            DcIsInstalling = dcInstalling,
            UlIsInstalling = ulInstalling,
            Is32Bit = is32Bit
        };

    // ── Property 3a: When UseNormalReShade is true, addon install buttons are disabled ──
    // Feature: reshade-no-addon-support, Property 3: Addon UI disabled when UseNormalReShade
    // **Validates: Requirements 4.1, 4.3**

    [Property(MaxTest = 100)]
    public Property WhenNormalReShadeActive_DcInstallEnabled_IsFalse()
    {
        return Prop.ForAll(GenCard.ToArbitrary(), card =>
        {
            card.UseNormalReShade = true;

            return (!card.DcInstallEnabled)
                .Label($"DcInstallEnabled should be false when UseNormalReShade=true, " +
                       $"but was true (DcIsInstalling={card.DcIsInstalling}, " +
                       $"IsUlInstalled={card.IsUlInstalled})");
        });
    }

    [Property(MaxTest = 100)]
    public Property WhenNormalReShadeActive_UlInstallEnabled_IsFalse()
    {
        return Prop.ForAll(GenCard.ToArbitrary(), card =>
        {
            card.UseNormalReShade = true;

            return (!card.UlInstallEnabled)
                .Label($"UlInstallEnabled should be false when UseNormalReShade=true, " +
                       $"but was true (UlIsInstalling={card.UlIsInstalling}, " +
                       $"IsDcInstalled={card.IsDcInstalled}, Is32Bit={card.Is32Bit})");
        });
    }

    [Property(MaxTest = 100)]
    public Property WhenNormalReShadeActive_AddonsDisabled_IsTrue()
    {
        return Prop.ForAll(GenCard.ToArbitrary(), card =>
        {
            card.UseNormalReShade = true;

            return card.AddonsDisabled
                .Label("AddonsDisabled should be true when UseNormalReShade=true");
        });
    }

    // ── Property 3b: When UseNormalReShade is false, mutual-exclusivity rules apply ──
    // Feature: reshade-no-addon-support, Property 3: Addon UI disabled when UseNormalReShade
    // **Validates: Requirements 4.3**

    [Property(MaxTest = 100)]
    public Property WhenNormalReShadeInactive_DcInstallEnabled_FollowsMutualExclusivity()
    {
        return Prop.ForAll(GenCard.ToArbitrary(), card =>
        {
            card.UseNormalReShade = false;

            // DcInstallEnabled => !DcIsInstalling && !IsUlInstalled && !UseNormalReShade
            bool expected = !card.DcIsInstalling && !card.IsUlInstalled;

            return (card.DcInstallEnabled == expected)
                .Label($"DcInstallEnabled={card.DcInstallEnabled}, expected={expected} " +
                       $"(DcIsInstalling={card.DcIsInstalling}, IsUlInstalled={card.IsUlInstalled})");
        });
    }

    [Property(MaxTest = 100)]
    public Property WhenNormalReShadeInactive_UlInstallEnabled_FollowsMutualExclusivity()
    {
        return Prop.ForAll(GenCard.ToArbitrary(), card =>
        {
            card.UseNormalReShade = false;

            // UlInstallEnabled => !UlIsInstalling && !IsDcInstalled && !Is32Bit && !UseNormalReShade
            bool expected = !card.UlIsInstalling && !card.IsDcInstalled && !card.Is32Bit;

            return (card.UlInstallEnabled == expected)
                .Label($"UlInstallEnabled={card.UlInstallEnabled}, expected={expected} " +
                       $"(UlIsInstalling={card.UlIsInstalling}, IsDcInstalled={card.IsDcInstalled}, " +
                       $"Is32Bit={card.Is32Bit})");
        });
    }

    [Property(MaxTest = 100)]
    public Property WhenNormalReShadeInactive_AddonsDisabled_IsFalse()
    {
        return Prop.ForAll(GenCard.ToArbitrary(), card =>
        {
            card.UseNormalReShade = false;

            return (!card.AddonsDisabled)
                .Label("AddonsDisabled should be false when UseNormalReShade=false");
        });
    }
}
