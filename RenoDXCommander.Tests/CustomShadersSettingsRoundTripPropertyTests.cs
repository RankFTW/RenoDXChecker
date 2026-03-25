using System.Text.Json;
using FsCheck;
using FsCheck.Xunit;
using RenoDXCommander.ViewModels;
using Xunit;

namespace RenoDXCommander.Tests;

/// <summary>
/// Property-based tests for custom shaders settings persistence round-trip.
/// Feature: custom-shaders, Property 1: Settings round-trip preserves custom shader state
/// **Validates: Requirements 1.3, 1.4, 1.5, 4.2, 4.3, 7.1, 7.3**
/// </summary>
public class CustomShadersSettingsRoundTripPropertyTests
{
    // ── Generators ────────────────────────────────────────────────────────────────

    /// <summary>
    /// Generates a random game name from a pool of realistic names.
    /// </summary>
    private static readonly Gen<string> GenGameName =
        Gen.Elements(
            "Cyberpunk 2077", "Elden Ring", "Starfield", "Hogwarts Legacy",
            "Alan Wake 2", "Baldur's Gate 3", "Final Fantasy XVI",
            "The Witcher 3", "Red Dead Redemption 2", "Halo Infinite",
            "Doom Eternal", "Resident Evil 4", "God of War",
            "Death Stranding", "Control", "Returnal", "Sifu");

    /// <summary>
    /// Generates a random per-game shader mode value from the valid set.
    /// </summary>
    private static readonly Gen<string> GenShaderMode =
        Gen.Elements("Custom", "Select", "Global");

    /// <summary>
    /// Generates a random Dictionary&lt;string, string&gt; of per-game shader modes
    /// with 0–8 entries, using realistic game names and valid mode values.
    /// </summary>
    private static readonly Gen<Dictionary<string, string>> GenPerGameShaderModes =
        from count in Gen.Choose(0, 8)
        from keys in Gen.ListOf(count, GenGameName)
        from values in Gen.ListOf(count, GenShaderMode)
        select keys.Zip(values)
            .GroupBy(kv => kv.First, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First().Second, StringComparer.OrdinalIgnoreCase);

    // ── Property 1: Settings round-trip preserves custom shader state ─────────────

    /// <summary>
    /// For any boolean UseCustomShaders value and any set of per-game shader mode
    /// overrides (including "Custom", "Select", "Global"), serializing the settings
    /// via SaveSettingsToDict and then deserializing via LoadSettingsFromDict shall
    /// produce an equivalent UseCustomShaders state. The per-game shader modes are
    /// injected into the dictionary to verify they do not corrupt the round-trip.
    /// **Validates: Requirements 1.3, 1.4, 1.5, 4.2, 4.3, 7.1, 7.3**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property UseCustomShaders_RoundTrips_WithPerGameShaderModes()
    {
        return Prop.ForAll(
            Arb.Default.Bool(),
            Arb.From(GenPerGameShaderModes),
            (bool useCustomShaders, Dictionary<string, string> perGameModes) =>
            {
                // Arrange: create a SettingsViewModel and set UseCustomShaders
                var source = new SettingsViewModel { IsLoadingSettings = true };
                source.UseCustomShaders = useCustomShaders;
                source.IsLoadingSettings = false;

                // Act: save to dictionary
                var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                source.SaveSettingsToDict(dict);

                // Inject per-game shader modes into the same dictionary (as GameNameService does)
                dict["PerGameShaderMode"] = JsonSerializer.Serialize(perGameModes);

                // Act: load from dictionary into a fresh SettingsViewModel
                var target = new SettingsViewModel { IsLoadingSettings = true };
                target.LoadSettingsFromDict(dict);
                target.IsLoadingSettings = false;

                // Assert: UseCustomShaders round-trips correctly
                if (target.UseCustomShaders != useCustomShaders)
                    return false.Label(
                        $"UseCustomShaders mismatch: original={useCustomShaders}, " +
                        $"saved='{dict.GetValueOrDefault("UseCustomShaders")}', " +
                        $"loaded={target.UseCustomShaders}");

                // Assert: PerGameShaderMode survives in the dictionary (not corrupted by SettingsViewModel)
                if (!dict.TryGetValue("PerGameShaderMode", out var pgsmJson))
                    return false.Label("PerGameShaderMode key missing from dictionary after round-trip");

                var reloaded = JsonSerializer.Deserialize<Dictionary<string, string>>(pgsmJson);
                if (reloaded is null)
                    return false.Label("PerGameShaderMode deserialized to null");

                if (reloaded.Count != perGameModes.Count)
                    return false.Label(
                        $"PerGameShaderMode count mismatch: original={perGameModes.Count}, " +
                        $"reloaded={reloaded.Count}");

                foreach (var kv in perGameModes)
                {
                    if (!reloaded.TryGetValue(kv.Key, out var val) ||
                        !string.Equals(val, kv.Value, StringComparison.Ordinal))
                        return false.Label(
                            $"PerGameShaderMode entry mismatch for '{kv.Key}': " +
                            $"expected='{kv.Value}', got='{val}'");
                }

                return true.Label(
                    $"OK: UseCustomShaders={useCustomShaders}, " +
                    $"PerGameShaderModes={perGameModes.Count} entries");
            });
    }
}
