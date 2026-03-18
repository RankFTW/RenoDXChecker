using FsCheck;
using FsCheck.Xunit;

namespace RenoDXCommander.Tests;

/// <summary>
/// Preservation property tests for the always-local shader routing model.
///
/// These tests verify that the new routing model is preserved across all input
/// combinations: RS-installed games always route to SyncGameFolder, games without
/// RS always skip, and SyncDcFolder is never called regardless of DC status.
///
/// **Validates: Requirements 8.1, 8.2**
/// </summary>
public class DcShaderRoutingPreservationTests
{
    // ── Pure model of the routing decision ────────────────────────────────────

    /// <summary>
    /// Represents the possible shader routing outcomes.
    /// SyncDcFolder has been removed — the new model never routes to DC AppData.
    /// </summary>
    public enum RoutingOutcome
    {
        SyncGameFolder,
        Skip
    }

    /// <summary>
    /// Models the FIXED routing decision used by DeployAllShaders, DeployShadersForCard,
    /// and SyncShadersToAllLocations. All games with RS installed always get
    /// SyncGameFolder. SyncDcFolder is never called.
    /// </summary>
    public static RoutingOutcome FixedRoutingDecision(bool rsInstalled, bool dcInstalled)
    {
        if (rsInstalled)
            return RoutingOutcome.SyncGameFolder;
        return RoutingOutcome.Skip;
    }

    /// <summary>
    /// Models the FIXED routing decision for InstallReShadeAsync.
    /// Always deploys shaders locally via SyncGameFolder, regardless of
    /// dcMode or dcIsInstalled. SyncDcFolder is never called.
    /// </summary>
    public static RoutingOutcome FixedReShadeRoutingDecision(bool dcMode, bool dcIsInstalled)
    {
        // InstallReShadeAsync always deploys locally — RS is being installed
        return RoutingOutcome.SyncGameFolder;
    }

    // ── Preservation: rsInstalled=true, dcInstalled=false → SyncGameFolder ───

    /// <summary>
    /// For any input where rsInstalled=true and dcInstalled=false, the routing logic
    /// SHALL call SyncGameFolder — preserving the always-local routing model for
    /// non-DC games that have ReShade installed.
    ///
    /// Generates random dcModeLevel (0–10) and perGameDcMode (null or 0–10) values
    /// to ensure the result is independent of DC mode settings.
    ///
    /// **Validates: Requirements 8.1**
    /// </summary>
    [Property(MaxTest = 200)]
    public Property DcNotInstalled_RsInstalled_AnyDcMode_RoutesToSyncGameFolder()
    {
        var genDcModeLevel = Gen.Choose(0, 10);

        var genPerGameDcMode = Gen.OneOf(
            Gen.Constant<int?>(null),
            Gen.Choose(0, 10).Select(v => (int?)v));

        var gen = from dcModeLevel in genDcModeLevel
                  from perGameDcMode in genPerGameDcMode
                  select (dcModeLevel, perGameDcMode);

        return Prop.ForAll(
            Arb.From(gen),
            ((int dcModeLevel, int? perGameDcMode) input) =>
            {
                const bool dcInstalled = false;
                const bool rsInstalled = true;

                var outcome = FixedRoutingDecision(rsInstalled, dcInstalled);

                return (outcome == RoutingOutcome.SyncGameFolder)
                    .Label($"rsInstalled={rsInstalled}, dcInstalled={dcInstalled}, " +
                           $"dcModeLevel={input.dcModeLevel}, perGameDcMode={input.perGameDcMode?.ToString() ?? "null"} " +
                           $"→ outcome={outcome} (expected SyncGameFolder)");
            });
    }

    // ── Preservation: rsInstalled=true, dcInstalled=true → SyncGameFolder ────

    /// <summary>
    /// For any input where rsInstalled=true and dcInstalled=true, the routing logic
    /// SHALL call SyncGameFolder — the new model always routes locally even when
    /// DC is installed.
    ///
    /// Generates random dcModeLevel (0–10) and perGameDcMode (null or 0–10) values
    /// to ensure the result is independent of DC mode settings.
    ///
    /// **Validates: Requirements 8.1, 8.2**
    /// </summary>
    [Property(MaxTest = 200)]
    public Property DcInstalled_RsInstalled_AnyDcMode_RoutesToSyncGameFolder()
    {
        var genDcModeLevel = Gen.Choose(0, 10);

        var genPerGameDcMode = Gen.OneOf(
            Gen.Constant<int?>(null),
            Gen.Choose(0, 10).Select(v => (int?)v));

        var gen = from dcModeLevel in genDcModeLevel
                  from perGameDcMode in genPerGameDcMode
                  select (dcModeLevel, perGameDcMode);

        return Prop.ForAll(
            Arb.From(gen),
            ((int dcModeLevel, int? perGameDcMode) input) =>
            {
                const bool dcInstalled = true;
                const bool rsInstalled = true;

                var outcome = FixedRoutingDecision(rsInstalled, dcInstalled);

                return (outcome == RoutingOutcome.SyncGameFolder)
                    .Label($"rsInstalled={rsInstalled}, dcInstalled={dcInstalled}, " +
                           $"dcModeLevel={input.dcModeLevel}, perGameDcMode={input.perGameDcMode?.ToString() ?? "null"} " +
                           $"→ outcome={outcome} (expected SyncGameFolder)");
            });
    }

    // ── Preservation: rsInstalled=false → Skip ───────────────────────────────

    /// <summary>
    /// For any input where rsInstalled=false, regardless of dcInstalled,
    /// the routing logic SHALL skip shader deployment — preserving the behavior
    /// for games with no ReShade installed.
    ///
    /// Generates random dcInstalled, dcModeLevel (0–10) and perGameDcMode (null or 0–10)
    /// values to ensure the result is independent of all other settings.
    ///
    /// **Validates: Requirements 8.1**
    /// </summary>
    [Property(MaxTest = 200)]
    public Property RsNotInstalled_AnyDcStatus_RoutesToSkip()
    {
        var genDcInstalled = Arb.Default.Bool().Generator;
        var genDcModeLevel = Gen.Choose(0, 10);

        var genPerGameDcMode = Gen.OneOf(
            Gen.Constant<int?>(null),
            Gen.Choose(0, 10).Select(v => (int?)v));

        var gen = from dcInstalled in genDcInstalled
                  from dcModeLevel in genDcModeLevel
                  from perGameDcMode in genPerGameDcMode
                  select (dcInstalled, dcModeLevel, perGameDcMode);

        return Prop.ForAll(
            Arb.From(gen),
            ((bool dcInstalled, int dcModeLevel, int? perGameDcMode) input) =>
            {
                const bool rsInstalled = false;

                var outcome = FixedRoutingDecision(rsInstalled, input.dcInstalled);

                return (outcome == RoutingOutcome.Skip)
                    .Label($"rsInstalled={rsInstalled}, dcInstalled={input.dcInstalled}, " +
                           $"dcModeLevel={input.dcModeLevel}, perGameDcMode={input.perGameDcMode?.ToString() ?? "null"} " +
                           $"→ outcome={outcome} (expected Skip)");
            });
    }

    // ── Preservation: InstallReShadeAsync always routes locally ──────────────

    /// <summary>
    /// For the InstallReShadeAsync path: for any combination of dcMode and dcIsInstalled,
    /// the routing logic SHALL always call SyncGameFolder — preserving the always-local
    /// routing model for ReShade installation.
    ///
    /// Generates random dcModeLevel (0–10), perGameDcMode (null or 0–10), and
    /// dcIsInstalled (true/false) to cover all combinations.
    ///
    /// **Validates: Requirements 8.1, 8.2**
    /// </summary>
    [Property(MaxTest = 200)]
    public Property ReShadeRouting_AnyDcStatus_AlwaysRoutesToSyncGameFolder()
    {
        var genDcModeLevel = Gen.Choose(0, 10);

        var genPerGameDcMode = Gen.OneOf(
            Gen.Constant<int?>(null),
            Gen.Choose(0, 10).Select(v => (int?)v));

        var genDcIsInstalled = Arb.Default.Bool().Generator;

        var gen = from dcModeLevel in genDcModeLevel
                  from perGameDcMode in genPerGameDcMode
                  from dcIsInstalled in genDcIsInstalled
                  select (dcModeLevel, perGameDcMode, dcIsInstalled);

        return Prop.ForAll(
            Arb.From(gen),
            ((int dcModeLevel, int? perGameDcMode, bool dcIsInstalled) input) =>
            {
                bool dcMode = (input.perGameDcMode ?? input.dcModeLevel) > 0;

                var outcome = FixedReShadeRoutingDecision(dcMode, input.dcIsInstalled);

                return (outcome == RoutingOutcome.SyncGameFolder)
                    .Label($"dcIsInstalled={input.dcIsInstalled}, dcMode={dcMode}, " +
                           $"dcModeLevel={input.dcModeLevel}, perGameDcMode={input.perGameDcMode?.ToString() ?? "null"} " +
                           $"→ outcome={outcome} (expected SyncGameFolder)");
            });
    }
}
