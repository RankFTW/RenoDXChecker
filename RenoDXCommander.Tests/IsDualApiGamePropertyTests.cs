using FsCheck;
using FsCheck.Xunit;
using RenoDXCommander.Models;
using RenoDXCommander.Services;

namespace RenoDXCommander.Tests;

// Feature: override-bitness-api, Property 4: IsDualApiGame is correctly derived from effective APIs

/// <summary>
/// Property-based tests for IsDualApiGame derivation.
/// For any set of GraphicsApiType values representing a game's effective APIs,
/// IsDualApiGame should be true if and only if the set contains at least one
/// DirectX API (DX8–DX12) and also contains Vulkan.
/// **Validates: Requirements 3.7**
/// </summary>
public class IsDualApiGamePropertyTests
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
    /// Generates a random subset of non-Unknown GraphicsApiType values.
    /// </summary>
    private static Gen<HashSet<GraphicsApiType>> GenApiSubset()
    {
        return Gen.SubListOf(AllApis)
            .Select(list => new HashSet<GraphicsApiType>(list));
    }

    /// <summary>
    /// For any random subset of GraphicsApiType values, IsDualApi returns true
    /// if and only if the set contains at least one DX API (DX8–DX12) AND Vulkan.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property IsDualApiGame_TrueIffHasDxAndVulkan()
    {
        return Prop.ForAll(GenApiSubset().ToArbitrary(), apiSet =>
        {
            bool result = GraphicsApiDetector.IsDualApi(apiSet);

            bool hasVulkan = apiSet.Contains(GraphicsApiType.Vulkan);
            bool hasDx = apiSet.Any(a => DxApis.Contains(a));
            bool expected = hasVulkan && hasDx;

            return (result == expected)
                .Label($"APIs={{{string.Join(", ", apiSet)}}}, expected={expected}, got={result}");
        });
    }

    /// <summary>
    /// Empty API sets should always yield IsDualApiGame = false.
    /// </summary>
    [Property(MaxTest = 10)]
    public Property IsDualApiGame_EmptySet_AlwaysFalse()
    {
        var emptySet = new HashSet<GraphicsApiType>();
        bool result = GraphicsApiDetector.IsDualApi(emptySet);
        return (!result).Label($"Empty set should be false, got {result}");
    }

    /// <summary>
    /// Sets containing only DX APIs (no Vulkan) should yield IsDualApiGame = false.
    /// </summary>
    [Property(MaxTest = 50)]
    public Property IsDualApiGame_DxOnly_AlwaysFalse()
    {
        var gen = Gen.SubListOf(DxApis.ToArray())
            .Where(list => list.Count > 0)
            .Select(list => new HashSet<GraphicsApiType>(list));

        return Prop.ForAll(gen.ToArbitrary(), apiSet =>
        {
            bool result = GraphicsApiDetector.IsDualApi(apiSet);
            return (!result).Label($"DX-only set {{{string.Join(", ", apiSet)}}} should be false, got {result}");
        });
    }

    /// <summary>
    /// Sets containing only Vulkan (no DX APIs) should yield IsDualApiGame = false.
    /// </summary>
    [Property(MaxTest = 10)]
    public Property IsDualApiGame_VulkanOnly_AlwaysFalse()
    {
        // Vulkan alone, or Vulkan + OpenGL (non-DX APIs)
        var gen = Gen.SubListOf(new[] { GraphicsApiType.Vulkan, GraphicsApiType.OpenGL })
            .Where(list => list.Contains(GraphicsApiType.Vulkan) && !list.Any(a => DxApis.Contains(a)))
            .Select(list => new HashSet<GraphicsApiType>(list));

        return Prop.ForAll(gen.ToArbitrary(), apiSet =>
        {
            bool result = GraphicsApiDetector.IsDualApi(apiSet);
            return (!result).Label($"Vulkan-only set {{{string.Join(", ", apiSet)}}} should be false, got {result}");
        });
    }
}
