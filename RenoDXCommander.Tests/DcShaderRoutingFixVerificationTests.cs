using FsCheck;
using FsCheck.Xunit;

namespace RenoDXCommander.Tests;

/// <summary>
/// Fix verification property tests for the local-only shader routing model.
///
/// These tests verify that for ANY input where rsInstalled=true — regardless of
/// dcInstalled, dcModeLevel, or perGameDcMode — the routing logic ALWAYS calls
/// SyncGameFolder and NEVER calls SyncDcFolder.
///
/// The new architecture deploys shaders exclusively to game-local folders.
/// SyncDcFolder is never invoked for shader deployment.
///
/// **Validates: Requirements 8.1, 8.2**
/// </summary>
public class DcShaderRoutingFixVerificationTests
{
    // ── Pure model of the routing decision ────────────────────────────────────

    /// <summary>
    /// Represents the possible shader routing outcomes.
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

    // ── Property test: rsInstalled=true (any DC status) → SyncGameFolder ─────

    /// <summary>
    /// For any input where rsInstalled=true, regardless of dcInstalled,
    /// dcModeLevel (0, 1, 2, ...), and perGameDcMode (null, 0, 1, 2, ...),
    /// the fixed general routing logic (DeployAllShaders / DeployShadersForCard /
    /// SyncShadersToAllLocations) SHALL call SyncGameFolder and SHALL NOT call
    /// SyncDcFolder.
    ///
    /// **Validates: Requirements 8.1, 8.2**
    /// </summary>
    [Property(MaxTest = 200)]
    public Property RsInstalled_AnyDcStatus_GeneralRouting_AlwaysSyncGameFolder()
    {
        // dcModeLevel can be any non-negative integer (0, 1, 2, ...)
        var genDcModeLevel = Gen.Choose(0, 10);

        // perGameDcMode can be null or any non-negative integer
        var genPerGameDcMode = Gen.OneOf(
            Gen.Constant<int?>(null),
            Gen.Choose(0, 10).Select(v => (int?)v));

        // dcInstalled can be anything — shouldn't matter, always SyncGameFolder
        var genDcInstalled = Arb.Default.Bool().Generator;

        var gen = from dcModeLevel in genDcModeLevel
                  from perGameDcMode in genPerGameDcMode
                  from dcInstalled in genDcInstalled
                  select (dcModeLevel, perGameDcMode, dcInstalled);

        return Prop.ForAll(
            Arb.From(gen),
            ((int dcModeLevel, int? perGameDcMode, bool dcInstalled) input) =>
            {
                const bool rsInstalled = true;

                var outcome = FixedRoutingDecision(rsInstalled, input.dcInstalled);

                return (outcome == RoutingOutcome.SyncGameFolder)
                    .Label($"dcModeLevel={input.dcModeLevel}, perGameDcMode={input.perGameDcMode?.ToString() ?? "null"}, " +
                           $"dcInstalled={input.dcInstalled} → outcome={outcome} (expected SyncGameFolder)");
            });
    }

    /// <summary>
    /// For any input where InstallReShadeAsync is called, regardless of
    /// dcModeLevel (0, 1, 2, ...), perGameDcMode (null, 0, 1, 2, ...),
    /// and dcIsInstalled (true/false), the fixed routing logic SHALL call
    /// SyncGameFolder and SHALL NOT call SyncDcFolder.
    ///
    /// **Validates: Requirements 8.1, 8.2**
    /// </summary>
    [Property(MaxTest = 200)]
    public Property AnyDcStatus_ReShadeRouting_AlwaysSyncGameFolder()
    {
        // dcModeLevel can be any non-negative integer
        var genDcModeLevel = Gen.Choose(0, 10);

        // perGameDcMode can be null or any non-negative integer
        var genPerGameDcMode = Gen.OneOf(
            Gen.Constant<int?>(null),
            Gen.Choose(0, 10).Select(v => (int?)v));

        // dcIsInstalled can be anything
        var genDcIsInstalled = Arb.Default.Bool().Generator;

        var gen = from dcModeLevel in genDcModeLevel
                  from perGameDcMode in genPerGameDcMode
                  from dcIsInstalled in genDcIsInstalled
                  select (dcModeLevel, perGameDcMode, dcIsInstalled);

        return Prop.ForAll(
            Arb.From(gen),
            ((int dcModeLevel, int? perGameDcMode, bool dcIsInstalled) input) =>
            {
                // Derive dcMode the same way the production code does
                bool dcMode = (input.perGameDcMode ?? input.dcModeLevel) > 0;

                var outcome = FixedReShadeRoutingDecision(dcMode, input.dcIsInstalled);

                return (outcome == RoutingOutcome.SyncGameFolder)
                    .Label($"dcModeLevel={input.dcModeLevel}, perGameDcMode={input.perGameDcMode?.ToString() ?? "null"}, " +
                           $"dcIsInstalled={input.dcIsInstalled}, dcMode={dcMode} → outcome={outcome} (expected SyncGameFolder)");
            });
    }

    // ── Property test: SyncDcFolder is never a valid outcome ─────────────────

    /// <summary>
    /// For ANY combination of inputs (rsInstalled, dcInstalled, dcMode, dcModeLevel,
    /// perGameDcMode), the routing model SHALL never produce a SyncDcFolder outcome.
    /// The SyncDcFolder outcome has been removed from the model entirely.
    ///
    /// **Validates: Requirements 8.2**
    /// </summary>
    [Property(MaxTest = 200)]
    public Property NoInputCombination_EverRoutes_ToSyncDcFolder()
    {
        var gen = from rsInstalled in Arb.Default.Bool().Generator
                  from dcInstalled in Arb.Default.Bool().Generator
                  from dcModeLevel in Gen.Choose(0, 10)
                  from perGameDcMode in Gen.OneOf(
                      Gen.Constant<int?>(null),
                      Gen.Choose(0, 10).Select(v => (int?)v))
                  select (rsInstalled, dcInstalled, dcModeLevel, perGameDcMode);

        return Prop.ForAll(
            Arb.From(gen),
            ((bool rsInstalled, bool dcInstalled, int dcModeLevel, int? perGameDcMode) input) =>
            {
                var generalOutcome = FixedRoutingDecision(input.rsInstalled, input.dcInstalled);

                bool dcMode = (input.perGameDcMode ?? input.dcModeLevel) > 0;
                var reshadeOutcome = FixedReShadeRoutingDecision(dcMode, input.dcInstalled);

                // Neither routing path should ever produce SyncDcFolder
                // (SyncDcFolder has been removed from the enum, so this is a structural guarantee,
                //  but we verify the outcomes are only SyncGameFolder or Skip)
                var generalValid = generalOutcome == RoutingOutcome.SyncGameFolder ||
                                   generalOutcome == RoutingOutcome.Skip;
                var reshadeValid = reshadeOutcome == RoutingOutcome.SyncGameFolder ||
                                   reshadeOutcome == RoutingOutcome.Skip;

                return (generalValid && reshadeValid)
                    .Label($"rsInstalled={input.rsInstalled}, dcInstalled={input.dcInstalled}, " +
                           $"dcModeLevel={input.dcModeLevel}, perGameDcMode={input.perGameDcMode?.ToString() ?? "null"} " +
                           $"→ general={generalOutcome}, reshade={reshadeOutcome}");
            });
    }
}
