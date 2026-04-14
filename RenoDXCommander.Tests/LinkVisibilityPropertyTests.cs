// Feature: nexus-pcgw-integration, Property 8: HasNexusModsUrl and HasPcgwUrl computed properties
using FsCheck;
using FsCheck.Xunit;
using RenoDXCommander.ViewModels;

namespace RenoDXCommander.Tests;

/// <summary>
/// Property-based tests for link visibility computed properties on GameCardViewModel.
/// Uses FsCheck with xUnit. Each property runs a minimum of 100 iterations.
/// </summary>
public class LinkVisibilityPropertyTests
{
    /// <summary>
    /// Feature: nexus-pcgw-integration, Property 8: HasNexusModsUrl and HasPcgwUrl computed properties
    ///
    /// **Validates: Requirements 8.3, 8.4**
    ///
    /// For any nullable string value assigned to NexusModsUrl,
    /// HasNexusModsUrl equals !string.IsNullOrEmpty(value).
    /// </summary>
    [Property(MaxTest = 100)]
    public Property HasNexusModsUrl_Equals_NotNullOrEmpty()
    {
        var nullableStringArb = Gen.OneOf(
            Gen.Constant<string?>(null),
            Gen.Constant<string?>(""),
            Arb.Default.NonEmptyString().Generator.Select(s => (string?)s.Get)
        ).ToArbitrary();

        return Prop.ForAll(nullableStringArb, (string? value) =>
        {
            var vm = new GameCardViewModel();
            vm.NexusModsUrl = value;

            var expected = !string.IsNullOrEmpty(value);
            return (vm.HasNexusModsUrl == expected)
                .Label($"NexusModsUrl=\"{value ?? "(null)"}\" => HasNexusModsUrl={vm.HasNexusModsUrl}, expected={expected}");
        });
    }

    /// <summary>
    /// Feature: nexus-pcgw-integration, Property 8: HasNexusModsUrl and HasPcgwUrl computed properties
    ///
    /// **Validates: Requirements 8.3, 8.4**
    ///
    /// For any nullable string value assigned to PcgwUrl,
    /// HasPcgwUrl equals !string.IsNullOrEmpty(value).
    /// </summary>
    [Property(MaxTest = 100)]
    public Property HasPcgwUrl_Equals_NotNullOrEmpty()
    {
        var nullableStringArb = Gen.OneOf(
            Gen.Constant<string?>(null),
            Gen.Constant<string?>(""),
            Arb.Default.NonEmptyString().Generator.Select(s => (string?)s.Get)
        ).ToArbitrary();

        return Prop.ForAll(nullableStringArb, (string? value) =>
        {
            var vm = new GameCardViewModel();
            vm.PcgwUrl = value;

            var expected = !string.IsNullOrEmpty(value);
            return (vm.HasPcgwUrl == expected)
                .Label($"PcgwUrl=\"{value ?? "(null)"}\" => HasPcgwUrl={vm.HasPcgwUrl}, expected={expected}");
        });
    }
}
