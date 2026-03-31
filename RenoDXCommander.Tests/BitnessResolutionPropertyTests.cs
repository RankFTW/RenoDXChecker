using FsCheck;
using FsCheck.Xunit;
using RenoDXCommander.Services;
using RenoDXCommander.ViewModels;

namespace RenoDXCommander.Tests;

// Feature: override-bitness-api, Property 1: Bitness resolution respects override priority

/// <summary>
/// Property-based tests for bitness resolution priority.
/// For any game name, any detected machine type (I386 or AMD64), and any bitness
/// override value ("Auto", "32", or "64"), the resolved Is32Bit value should be:
/// true when the override is "32", false when the override is "64", and equal to
/// the auto-detected value (based on PE header / manifest logic) when the override
/// is "Auto" or absent.
/// **Validates: Requirements 2.3, 2.4, 2.5, 2.6, 4.3**
/// </summary>
public class BitnessResolutionPropertyTests
{
    // ── Generators ────────────────────────────────────────────────────────────────

    /// <summary>
    /// Generates a safe game name: non-empty alphanumeric string that is valid
    /// as a dictionary key and won't collide with manifest overrides.
    /// </summary>
    private static Gen<string> GenGameName()
    {
        return Gen.Elements(
            "CyberGame", "SpaceShooter", "RacingPro", "PuzzleMaster",
            "RPGWorld", "FPSArena", "StrategyKing", "PlatformJump",
            "HorrorNight", "SimCity2K", "AdventureQuest", "SportsChamp");
    }

    /// <summary>
    /// Generates a MachineType value relevant to bitness: I386 (32-bit) or x64 (64-bit).
    /// </summary>
    private static Gen<MachineType> GenMachineType()
    {
        return Gen.Elements(MachineType.I386, MachineType.x64);
    }

    /// <summary>
    /// Generates a bitness override value: "Auto" (no override), "32", or "64".
    /// </summary>
    private static Gen<string> GenBitnessOverride()
    {
        return Gen.Elements("Auto", "32", "64");
    }

    // ── Property 1: Bitness resolution respects override priority ─────────────

    /// <summary>
    /// For any game name, any detected MachineType, and any bitness override value,
    /// ResolveIs32Bit returns:
    ///   - true  when override is "32"
    ///   - false when override is "64"
    ///   - the auto-detected value (detectedMachine == I386) when override is "Auto"
    ///
    /// This test uses a fresh MainViewModel with no manifest overrides, so the
    /// fallback path is purely PE header detection (detectedMachine == I386).
    /// </summary>
    [Property(MaxTest = 30)]
    public Property ResolveIs32Bit_RespectsOverridePriority()
    {
        return Prop.ForAll(
            GenGameName().ToArbitrary(),
            GenMachineType().ToArbitrary(),
            GenBitnessOverride().ToArbitrary(),
            (gameName, machineType, overrideValue) =>
            {
                // Arrange: create a fresh MainViewModel with stub services
                var vm = TestHelpers.CreateMainViewModel();

                // Set the bitness override (SetBitnessOverride removes entry for "Auto")
                vm.SetBitnessOverride(gameName, overrideValue);

                // Act: resolve the bitness
                bool result = vm.ResolveIs32Bit(gameName, machineType);

                // Determine expected value based on override priority
                bool expected = overrideValue switch
                {
                    "32" => true,
                    "64" => false,
                    // "Auto" — no override, falls through to PE header detection
                    // With no manifest overrides loaded, the fallback is: detectedMachine == I386
                    _ => machineType == MachineType.I386
                };

                if (result != expected)
                    return false.Label(
                        $"Override='{overrideValue}', Machine={machineType}: " +
                        $"expected Is32Bit={expected}, got {result}");

                return true.Label(
                    $"OK: Override='{overrideValue}', Machine={machineType} → Is32Bit={result}");
            });
    }

    // ── Property 1b: Override "32" always returns true regardless of machine ──

    /// <summary>
    /// When the bitness override is "32", ResolveIs32Bit always returns true
    /// regardless of the detected MachineType.
    /// </summary>
    [Property(MaxTest = 30)]
    public Property ResolveIs32Bit_Override32_AlwaysTrue()
    {
        return Prop.ForAll(
            GenGameName().ToArbitrary(),
            GenMachineType().ToArbitrary(),
            (gameName, machineType) =>
            {
                var vm = TestHelpers.CreateMainViewModel();
                vm.SetBitnessOverride(gameName, "32");

                bool result = vm.ResolveIs32Bit(gameName, machineType);

                if (!result)
                    return false.Label(
                        $"Override='32', Machine={machineType}: expected true, got false");

                return true.Label($"OK: Override='32', Machine={machineType} → true");
            });
    }

    // ── Property 1c: Override "64" always returns false regardless of machine ─

    /// <summary>
    /// When the bitness override is "64", ResolveIs32Bit always returns false
    /// regardless of the detected MachineType.
    /// </summary>
    [Property(MaxTest = 30)]
    public Property ResolveIs32Bit_Override64_AlwaysFalse()
    {
        return Prop.ForAll(
            GenGameName().ToArbitrary(),
            GenMachineType().ToArbitrary(),
            (gameName, machineType) =>
            {
                var vm = TestHelpers.CreateMainViewModel();
                vm.SetBitnessOverride(gameName, "64");

                bool result = vm.ResolveIs32Bit(gameName, machineType);

                if (result)
                    return false.Label(
                        $"Override='64', Machine={machineType}: expected false, got true");

                return true.Label($"OK: Override='64', Machine={machineType} → false");
            });
    }

    // ── Property 1d: "Auto" override falls through to PE detection ────────────

    /// <summary>
    /// When the bitness override is "Auto" (removed), ResolveIs32Bit falls through
    /// to the auto-detection path. With no manifest overrides, this means
    /// Is32Bit == (detectedMachine == I386).
    /// </summary>
    [Property(MaxTest = 30)]
    public Property ResolveIs32Bit_AutoOverride_FallsThrough()
    {
        return Prop.ForAll(
            GenGameName().ToArbitrary(),
            GenMachineType().ToArbitrary(),
            (gameName, machineType) =>
            {
                var vm = TestHelpers.CreateMainViewModel();
                // "Auto" removes the override entry
                vm.SetBitnessOverride(gameName, "Auto");

                bool result = vm.ResolveIs32Bit(gameName, machineType);
                bool expected = machineType == MachineType.I386;

                if (result != expected)
                    return false.Label(
                        $"Override='Auto', Machine={machineType}: " +
                        $"expected Is32Bit={expected}, got {result}");

                return true.Label(
                    $"OK: Override='Auto', Machine={machineType} → Is32Bit={result}");
            });
    }
}
