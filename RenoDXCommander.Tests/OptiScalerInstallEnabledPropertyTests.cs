// Feature: optiscaler-integration, Property 11: OsInstallEnabled Boolean Logic
using FsCheck;
using FsCheck.Xunit;
using RenoDXCommander.ViewModels;

namespace RenoDXCommander.Tests;

/// <summary>
/// Property-based tests for OptiScaler install-enabled boolean logic.
/// Uses FsCheck with xUnit.
///
/// **Validates: Requirements 7.6, 8.3, 21.2, 21.3**
///
/// OsInstallEnabled must equal !Is32Bit &amp;&amp; !OsIsInstalling for every
/// combination of the two boolean inputs.
/// </summary>
public class OptiScalerInstallEnabledPropertyTests
{
    /// <summary>
    /// Property 11: OsInstallEnabled Boolean Logic
    ///
    /// **Validates: Requirements 7.6, 8.3, 21.2, 21.3**
    ///
    /// For any combination of Is32Bit (bool) and OsIsInstalling (bool),
    /// OsInstallEnabled equals !Is32Bit &amp;&amp; !OsIsInstalling.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property OsInstallEnabled_Equals_Not32Bit_And_NotInstalling()
    {
        return Prop.ForAll(
            Arb.Default.Bool(),
            Arb.Default.Bool(),
            (bool is32Bit, bool osIsInstalling) =>
            {
                var card = new GameCardViewModel
                {
                    Is32Bit = is32Bit,
                    OsIsInstalling = osIsInstalling
                };

                bool expected = !is32Bit && !osIsInstalling;

                return (card.OsInstallEnabled == expected)
                    .Label($"Is32Bit={is32Bit}, OsIsInstalling={osIsInstalling} => " +
                           $"OsInstallEnabled={card.OsInstallEnabled}, expected={expected}");
            });
    }

    /// <summary>
    /// CardOsInstallEnabled must match OsInstallEnabled for the same inputs.
    ///
    /// **Validates: Requirements 8.3, 8.5**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property CardOsInstallEnabled_Matches_OsInstallEnabled()
    {
        return Prop.ForAll(
            Arb.Default.Bool(),
            Arb.Default.Bool(),
            (bool is32Bit, bool osIsInstalling) =>
            {
                var card = new GameCardViewModel
                {
                    Is32Bit = is32Bit,
                    OsIsInstalling = osIsInstalling
                };

                return (card.CardOsInstallEnabled == card.OsInstallEnabled)
                    .Label($"Is32Bit={is32Bit}, OsIsInstalling={osIsInstalling} => " +
                           $"CardOsInstallEnabled={card.CardOsInstallEnabled}, " +
                           $"OsInstallEnabled={card.OsInstallEnabled}");
            });
    }
}
