using FsCheck;
using FsCheck.Xunit;
using RenoDXCommander.Models;

namespace RenoDXCommander.Tests;

// Feature: override-bitness-api, Property 5: Primary GraphicsApi is deterministically derived from effective APIs

/// <summary>
/// Property-based tests for primary GraphicsApi derivation.
/// For any non-empty set of GraphicsApiType values representing a game's effective APIs,
/// the primary GraphicsApi value should be deterministically selected: preferring the highest
/// DirectX version when DX APIs are present, or the single API when only one exists,
/// or Unknown when the set is empty.
/// **Validates: Requirements 3.8**
/// </summary>
public class PrimaryGraphicsApiPropertyTests
{
    private static readonly GraphicsApiType[] AllApis = Enum.GetValues<GraphicsApiType>()
        .Where(a => a != GraphicsApiType.Unknown)
        .ToArray();

    private static readonly HashSet<GraphicsApiType> DxApis = new()
    {
        GraphicsApiType.DirectX8,
        GraphicsApiType.DirectX9,
        GraphicsApiType.DirectX10,
        GraphicsApiType.DirectX11,
        GraphicsApiType.DirectX12,
    };

    /// <summary>
    /// Generates a safe game name for dictionary keying.
    /// </summary>
    private static Gen<string> GenGameName()
    {
        return Gen.Elements(
            "CyberGame", "SpaceShooter", "RacingPro", "PuzzleMaster",
            "RPGWorld", "FPSArena", "StrategyKing", "PlatformJump");
    }

    /// <summary>
    /// Generates a random non-empty subset of non-Unknown GraphicsApiType values.
    /// </summary>
    private static Gen<List<string>> GenNonEmptyApiList()
    {
        return Gen.ListOf(AllApis.Length, Arb.Generate<bool>())
            .Select(flags =>
            {
                var flagList = flags.ToList();
                var subset = new List<string>();
                for (int i = 0; i < AllApis.Length && i < flagList.Count; i++)
                    if (flagList[i]) subset.Add(AllApis[i].ToString());
                return subset;
            })
            .Where(list => list.Count > 0);
    }

    /// <summary>
    /// Computes the expected primary API using the same logic as DetectGraphicsApi's
    /// override path: empty → Unknown, single → that element, multiple → prefer
    /// highest DX version (OrderByDescending on enum value), fallback to first element.
    /// </summary>
    private static GraphicsApiType ExpectedPrimary(HashSet<GraphicsApiType> apiSet)
    {
        if (apiSet.Count == 0)
            return GraphicsApiType.Unknown;
        if (apiSet.Count == 1)
            return apiSet.First();

        var dxInSet = apiSet
            .Where(a => a != GraphicsApiType.Vulkan && a != GraphicsApiType.OpenGL && a != GraphicsApiType.Unknown);
        if (dxInSet.Any())
            return dxInSet.OrderByDescending(a => a).First();

        return apiSet.First();
    }

    /// <summary>
    /// For any non-empty API override set, DetectGraphicsApi derives the primary API
    /// deterministically: prefer highest DX version when DX APIs are present,
    /// single API when only one exists.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property PrimaryApi_IsDeterministicallyDerived()
    {
        return Prop.ForAll(
            GenGameName().ToArbitrary(),
            GenNonEmptyApiList().ToArbitrary(),
            (gameName, overrideApis) =>
            {
                // Arrange
                var vm = TestHelpers.CreateMainViewModel();
                vm.SetApiOverride(gameName, overrideApis);

                // Act: DetectGraphicsApi with non-existent path — override takes priority
                var result = vm.DetectGraphicsApi(@"C:\NonExistent\Path\12345", EngineType.Unknown, gameName);

                // Expected: parse the override list and compute expected primary
                var parsed = new HashSet<GraphicsApiType>();
                foreach (var name in overrideApis)
                {
                    if (Enum.TryParse<GraphicsApiType>(name, out var apiType))
                        parsed.Add(apiType);
                }
                var expected = ExpectedPrimary(parsed);

                return (result == expected)
                    .Label($"APIs={{{string.Join(", ", overrideApis)}}}, expected={expected}, got={result}");
            });
    }

    /// <summary>
    /// For a single-element API override set, the primary API is that single element.
    /// </summary>
    [Property(MaxTest = 50)]
    public Property PrimaryApi_SingleElement_ReturnsThatElement()
    {
        var genSingleApi = Gen.Elements(AllApis).Select(a => new List<string> { a.ToString() });

        return Prop.ForAll(
            GenGameName().ToArbitrary(),
            genSingleApi.ToArbitrary(),
            (gameName, overrideApis) =>
            {
                var vm = TestHelpers.CreateMainViewModel();
                vm.SetApiOverride(gameName, overrideApis);

                var result = vm.DetectGraphicsApi(@"C:\NonExistent\Path\12345", EngineType.Unknown, gameName);
                var expected = Enum.Parse<GraphicsApiType>(overrideApis[0]);

                return (result == expected)
                    .Label($"Single API={overrideApis[0]}, expected={expected}, got={result}");
            });
    }

    /// <summary>
    /// When DX APIs are present in the set, the primary API is the highest DX version
    /// (highest enum value among DX APIs).
    /// </summary>
    [Property(MaxTest = 50)]
    public Property PrimaryApi_WithDxApis_PrefersHighestDxVersion()
    {
        // Generate sets that contain at least one DX API
        var genWithDx = GenNonEmptyApiList()
            .Where(list => list.Any(name =>
                Enum.TryParse<GraphicsApiType>(name, out var api) && DxApis.Contains(api)));

        return Prop.ForAll(
            GenGameName().ToArbitrary(),
            genWithDx.ToArbitrary(),
            (gameName, overrideApis) =>
            {
                var vm = TestHelpers.CreateMainViewModel();
                vm.SetApiOverride(gameName, overrideApis);

                var result = vm.DetectGraphicsApi(@"C:\NonExistent\Path\12345", EngineType.Unknown, gameName);

                // Expected: highest DX API by enum value
                var parsed = new HashSet<GraphicsApiType>();
                foreach (var name in overrideApis)
                {
                    if (Enum.TryParse<GraphicsApiType>(name, out var apiType))
                        parsed.Add(apiType);
                }
                var highestDx = parsed
                    .Where(a => DxApis.Contains(a))
                    .OrderByDescending(a => a)
                    .First();

                return (result == highestDx)
                    .Label($"APIs={{{string.Join(", ", overrideApis)}}}, expected highest DX={highestDx}, got={result}");
            });
    }

    /// <summary>
    /// An empty API override set results in GraphicsApi = Unknown.
    /// </summary>
    [Property(MaxTest = 10)]
    public Property PrimaryApi_EmptySet_ReturnsUnknown()
    {
        return Prop.ForAll(
            GenGameName().ToArbitrary(),
            (gameName) =>
            {
                var vm = TestHelpers.CreateMainViewModel();
                vm.SetApiOverride(gameName, new List<string>());

                var result = vm.DetectGraphicsApi(@"C:\NonExistent\Path\12345", EngineType.Unknown, gameName);

                return (result == GraphicsApiType.Unknown)
                    .Label($"Empty override for '{gameName}', expected Unknown, got={result}");
            });
    }

    /// <summary>
    /// Calling DetectGraphicsApi twice with the same override set produces the same result,
    /// confirming determinism.
    /// </summary>
    [Property(MaxTest = 50)]
    public Property PrimaryApi_IsDeterministic_SameInputSameOutput()
    {
        return Prop.ForAll(
            GenGameName().ToArbitrary(),
            GenNonEmptyApiList().ToArbitrary(),
            (gameName, overrideApis) =>
            {
                var vm = TestHelpers.CreateMainViewModel();
                vm.SetApiOverride(gameName, overrideApis);

                var result1 = vm.DetectGraphicsApi(@"C:\NonExistent\Path\12345", EngineType.Unknown, gameName);
                var result2 = vm.DetectGraphicsApi(@"C:\NonExistent\Path\12345", EngineType.Unknown, gameName);

                return (result1 == result2)
                    .Label($"APIs={{{string.Join(", ", overrideApis)}}}, run1={result1}, run2={result2}");
            });
    }
}
