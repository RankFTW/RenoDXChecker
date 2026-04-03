using FsCheck;
using FsCheck.Xunit;
using RenoDXCommander.ViewModels;

namespace RenoDXCommander.Tests;

// Feature: display-commander-reintegration, Property 1: Bitness selects correct DC filename

/// <summary>
/// Property-based tests for DC bitness filename resolution.
/// For any boolean is32Bit value, GetDcFileName returns the correct addon filename
/// and GetDcCachePath returns distinct paths for each bitness.
/// **Validates: Requirements 1.2, 1.3**
/// </summary>
public class DcBitnessFilenamePropertyTests
{
    [Property(MaxTest = 10)]
    public Property GetDcFileName_ReturnsCorrectFilenameForBitness()
    {
        return Prop.ForAll(
            Arb.Default.Bool(),
            is32Bit =>
            {
                var result = MainViewModel.GetDcFileName(is32Bit);

                var expected = is32Bit
                    ? "zzz_display_commander_lite.addon32"
                    : "zzz_display_commander_lite.addon64";

                if (result != expected)
                    return false.Label(
                        $"is32Bit={is32Bit}: expected '{expected}', got '{result}'");

                return true.Label($"OK: is32Bit={is32Bit} → '{result}'");
            });
    }

    [Property(MaxTest = 10)]
    public Property GetDcCachePath_ReturnsDistinctPathsPerBitness()
    {
        return Prop.ForAll(
            Arb.Default.Bool(),
            _ =>
            {
                var path32 = MainViewModel.GetDcCachePath(true);
                var path64 = MainViewModel.GetDcCachePath(false);

                if (path32 == path64)
                    return false.Label(
                        $"Cache paths should differ but both are '{path32}'");

                // Each path should end with the correct filename
                if (!path32.EndsWith("zzz_display_commander_lite.addon32"))
                    return false.Label(
                        $"32-bit cache path should end with addon32, got '{path32}'");

                if (!path64.EndsWith("zzz_display_commander_lite.addon64"))
                    return false.Label(
                        $"64-bit cache path should end with addon64, got '{path64}'");

                return true.Label($"OK: paths are distinct — '{path32}' vs '{path64}'");
            });
    }
}
