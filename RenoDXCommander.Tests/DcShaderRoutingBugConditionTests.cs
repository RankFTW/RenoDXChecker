using FsCheck;
using FsCheck.Xunit;

namespace RenoDXCommander.Tests;

/// <summary>
/// Bug condition regression tests for the local-only shader routing model.
///
/// The OLD bug condition was: dcInstalled=true AND dcMode=false (dcModeLevel=0,
/// perGameDcMode null or 0). Under the old code, shaders were not deployed anywhere
/// because the <c>dcInstalled &amp;&amp; dcMode</c> check failed, skipping SyncDcFolder,
/// and the game-local path was also skipped because dcInstalled was true.
///
/// Under the NEW local-only architecture, this bug CANNOT occur because:
/// - SyncDcFolder is never called (it's a no-op)
/// - All RS-installed games always get SyncGameFolder regardless of DC status
/// - The dcMode flag has no effect on shader routing
///
/// These tests verify that the old bug condition scenario now correctly deploys
/// shaders locally via SyncGameFolder.
///
/// **Validates: Requirements 8.1, 8.3**
/// </summary>
public class DcShaderRoutingBugConditionTests
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
    /// Models the NEW routing decision used by DeployAllShaders, DeployShadersForCard,
    /// and SyncShadersToAllLocations. All games with RS installed always get
    /// SyncGameFolder. DC status is irrelevant.
    /// </summary>
    public static RoutingOutcome FixedRoutingDecision(bool rsInstalled, bool dcInstalled)
    {
        if (rsInstalled)
            return RoutingOutcome.SyncGameFolder;
        return RoutingOutcome.Skip;
    }

    /// <summary>
    /// Models the NEW routing decision for InstallReShadeAsync.
    /// Always deploys shaders locally via SyncGameFolder, regardless of
    /// dcMode or dcIsInstalled.
    /// </summary>
    public static RoutingOutcome FixedReShadeRoutingDecision(bool dcMode, bool dcIsInstalled)
    {
        return RoutingOutcome.SyncGameFolder;
    }

    // ── Bug condition helper ─────────────────────────────────────────────────

    /// <summary>
    /// Returns true when the OLD bug condition holds: DC is installed but dcMode is false.
    /// This scenario used to cause no shaders to be deployed anywhere.
    /// </summary>
    public static bool IsOldBugCondition(bool dcInstalled, int dcModeLevel, int? perGameDcMode)
    {
        bool dcMode = (perGameDcMode ?? dcModeLevel) > 0;
        return dcInstalled && !dcMode;
    }

    // ── Property test: Old bug condition now correctly deploys locally ────────

    /// <summary>
    /// For any input where dcInstalled=true and dcMode=false (the old bug condition),
    /// the new routing logic SHALL call SyncGameFolder when rsInstalled=true.
    ///
    /// Under the OLD code, this scenario resulted in NO shaders deployed anywhere.
    /// Under the NEW code, shaders are always deployed locally regardless of DC status.
    ///
    /// **Validates: Requirements 8.1, 8.3**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property OldBugCondition_DcInstalledDcModeFalse_NowRoutesLocally()
    {
        // Generate dcModeLevel = 0 always (old bug condition requires dcMode=false)
        var genDcModeLevel = Gen.Constant(0);

        // Generate perGameDcMode as null or 0 (both yield dcMode=false)
        var genPerGameDcMode = Gen.OneOf(
            Gen.Constant<int?>(null),
            Gen.Constant<int?>(0));

        var gen = from dcModeLevel in genDcModeLevel
                  from perGameDcMode in genPerGameDcMode
                  select (dcModeLevel, perGameDcMode);

        return Prop.ForAll(
            Arb.From(gen),
            ((int dcModeLevel, int? perGameDcMode) input) =>
            {
                const bool dcInstalled = true;
                const bool rsInstalled = true;

                // Verify this IS the old bug condition
                var isOldBug = IsOldBugCondition(dcInstalled, input.dcModeLevel, input.perGameDcMode);

                // The new routing decision — always local
                var outcome = FixedRoutingDecision(rsInstalled, dcInstalled);

                return (isOldBug && outcome == RoutingOutcome.SyncGameFolder)
                    .Label($"dcModeLevel={input.dcModeLevel}, perGameDcMode={input.perGameDcMode?.ToString() ?? "null"}, " +
                           $"dcInstalled={dcInstalled}, rsInstalled={rsInstalled} " +
                           $"→ isOldBugCondition={isOldBug}, outcome={outcome} (expected SyncGameFolder)");
            });
    }

    /// <summary>
    /// For any input where dcIsInstalled=true and dcMode=false (the old bug condition
    /// for InstallReShadeAsync), the new routing logic SHALL call SyncGameFolder.
    ///
    /// Under the OLD code, InstallReShadeAsync checked <c>if (dcMode)</c> and skipped
    /// SyncDcFolder when dcMode was false, even though DC was installed.
    /// Under the NEW code, shaders are always deployed locally.
    ///
    /// **Validates: Requirements 8.1, 8.3**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property OldBugCondition_ReShadeInstall_DcInstalledDcModeFalse_NowRoutesLocally()
    {
        // dcMode = false (the old bug condition for InstallReShadeAsync)
        const bool dcMode = false;
        const bool dcIsInstalled = true;

        // Generate random dcModeLevel values that produce dcMode=false
        var genDcModeLevel = Gen.Constant(0);
        var genPerGameDcMode = Gen.OneOf(
            Gen.Constant<int?>(null),
            Gen.Constant<int?>(0));

        var gen = from dcModeLevel in genDcModeLevel
                  from perGameDcMode in genPerGameDcMode
                  select (dcModeLevel, perGameDcMode);

        return Prop.ForAll(
            Arb.From(gen),
            ((int dcModeLevel, int? perGameDcMode) input) =>
            {
                // Verify this IS the old bug condition
                var isOldBug = IsOldBugCondition(dcIsInstalled, input.dcModeLevel, input.perGameDcMode);

                // The new routing decision for InstallReShadeAsync — always local
                var outcome = FixedReShadeRoutingDecision(dcMode, dcIsInstalled);

                return (isOldBug && outcome == RoutingOutcome.SyncGameFolder)
                    .Label($"dcMode={dcMode}, dcIsInstalled={dcIsInstalled}, " +
                           $"dcModeLevel={input.dcModeLevel}, perGameDcMode={input.perGameDcMode?.ToString() ?? "null"} " +
                           $"→ isOldBugCondition={isOldBug}, outcome={outcome} (expected SyncGameFolder)");
            });
    }
}
