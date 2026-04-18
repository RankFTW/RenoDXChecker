// Feature: optiscaler-integration, Property 12: OptiScaler DLL Dropdown Conflict Filtering
using FsCheck;
using FsCheck.Xunit;
using RenoDXCommander.Services;

namespace RenoDXCommander.Tests;

/// <summary>
/// Property-based tests for OptiScaler DLL dropdown conflict filtering.
/// Uses FsCheck with xUnit.
///
/// **Validates: Requirements 9.3**
///
/// For any pair of RS and DC effective filenames drawn from supported DLL names,
/// GetAvailableOsDllNames returns a list excluding both RS and DC filenames
/// and including all other supported names.
/// </summary>
public class OptiScalerDllDropdownFilterPropertyTests
{
    private static readonly string[] SupportedNames = OptiScalerService.SupportedDllNames;

    // ── Generators ────────────────────────────────────────────────────────────────

    /// <summary>
    /// Generates a pair of RS and DC filenames drawn from supported DLL names.
    /// RS and DC may be the same name (testing the overlap case) or different.
    /// </summary>
    private static Gen<(string rsName, string dcName, bool is32Bit)> GenRsDcPair()
    {
        return from rsName in Gen.Elements(SupportedNames)
               from dcName in Gen.Elements(SupportedNames)
               from is32Bit in Arb.Default.Bool().Generator
               select (rsName, dcName, is32Bit);
    }

    // ── Property 12: OptiScaler DLL Dropdown Conflict Filtering ───────────────────

    /// <summary>
    /// Property 12: OptiScaler DLL Dropdown Conflict Filtering
    ///
    /// **Validates: Requirements 9.3**
    ///
    /// For any pair of RS and DC effective filenames drawn from supported DLL names,
    /// GetAvailableOsDllNames returns a list excluding both RS and DC filenames
    /// and including all other supported names.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property GetAvailableOsDllNames_ExcludesRsAndDcNames()
    {
        return Prop.ForAll(
            Arb.From(GenRsDcPair()),
            (tuple) =>
            {
                var (rsName, dcName, is32Bit) = tuple;

                // Arrange: fresh service with RS and DC overrides set
                var auxInstaller = new AuxInstallService(new HttpClient(), new TestHelpers.StubShaderPackService());
                var service = new DllOverrideService(auxInstaller);

                var gameName = "FilterTest_" + Guid.NewGuid().ToString("N")[..6];

                // Set RS and DC overrides so GetEffectiveRsName/GetEffectiveDcName return our values
                service.SetDllOverride(gameName, rsName, dcName);

                // Act
                var available = service.GetAvailableOsDllNames(gameName, is32Bit);

                // Assert: result must not contain RS or DC names (case-insensitive)
                bool containsRs = available.Any(n =>
                    n.Equals(rsName, StringComparison.OrdinalIgnoreCase));
                bool containsDc = available.Any(n =>
                    n.Equals(dcName, StringComparison.OrdinalIgnoreCase));

                // Assert: result must contain all other supported names
                var expectedNames = SupportedNames
                    .Where(n => !n.Equals(rsName, StringComparison.OrdinalIgnoreCase)
                             && !n.Equals(dcName, StringComparison.OrdinalIgnoreCase))
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);

                var actualSet = new HashSet<string>(available, StringComparer.OrdinalIgnoreCase);
                bool hasAllExpected = expectedNames.All(n => actualSet.Contains(n));
                bool noExtras = actualSet.All(n => expectedNames.Contains(n));

                return (!containsRs && !containsDc && hasAllExpected && noExtras)
                    .Label($"rsName='{rsName}', dcName='{dcName}', is32Bit={is32Bit} => " +
                           $"available=[{string.Join(", ", available)}], " +
                           $"containsRs={containsRs}, containsDc={containsDc}, " +
                           $"hasAllExpected={hasAllExpected}, noExtras={noExtras}");
            });
    }

    /// <summary>
    /// When RS and DC use the same filename, only one name is excluded
    /// and the result count equals SupportedDllNames.Length - 1.
    ///
    /// **Validates: Requirements 9.3**
    /// </summary>
    [Property(MaxTest = 50)]
    public Property GetAvailableOsDllNames_SameRsDc_ExcludesOnce()
    {
        return Prop.ForAll(
            Arb.From(Gen.Elements(SupportedNames)),
            Arb.Default.Bool(),
            (string sharedName, bool is32Bit) =>
            {
                var auxInstaller = new AuxInstallService(new HttpClient(), new TestHelpers.StubShaderPackService());
                var service = new DllOverrideService(auxInstaller);

                var gameName = "SameRsDc_" + Guid.NewGuid().ToString("N")[..6];
                service.SetDllOverride(gameName, sharedName, sharedName);

                var available = service.GetAvailableOsDllNames(gameName, is32Bit);

                int expectedCount = SupportedNames.Length - 1;

                return (available.Length == expectedCount)
                    .Label($"sharedName='{sharedName}', is32Bit={is32Bit} => " +
                           $"available.Length={available.Length}, expected={expectedCount}");
            });
    }

    /// <summary>
    /// When RS and DC use different supported names, both are excluded
    /// and the result count equals SupportedDllNames.Length - 2.
    ///
    /// **Validates: Requirements 9.3**
    /// </summary>
    [Property(MaxTest = 50)]
    public Property GetAvailableOsDllNames_DistinctRsDc_ExcludesBoth()
    {
        var genDistinctPair = from rsIdx in Gen.Choose(0, SupportedNames.Length - 1)
                              from dcIdx in Gen.Choose(0, SupportedNames.Length - 1)
                                            .Where(i => !SupportedNames[i].Equals(
                                                SupportedNames[rsIdx], StringComparison.OrdinalIgnoreCase))
                              select (SupportedNames[rsIdx], SupportedNames[dcIdx]);

        return Prop.ForAll(
            Arb.From(genDistinctPair),
            Arb.Default.Bool(),
            ((string rsName, string dcName) pair, bool is32Bit) =>
            {
                var auxInstaller = new AuxInstallService(new HttpClient(), new TestHelpers.StubShaderPackService());
                var service = new DllOverrideService(auxInstaller);

                var gameName = "DistinctRsDc_" + Guid.NewGuid().ToString("N")[..6];
                service.SetDllOverride(gameName, pair.rsName, pair.dcName);

                var available = service.GetAvailableOsDllNames(gameName, is32Bit);

                int expectedCount = SupportedNames.Length - 2;

                return (available.Length == expectedCount)
                    .Label($"rsName='{pair.rsName}', dcName='{pair.dcName}', is32Bit={is32Bit} => " +
                           $"available.Length={available.Length}, expected={expectedCount}");
            });
    }
}
