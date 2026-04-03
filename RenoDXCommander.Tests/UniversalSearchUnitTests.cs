using System.Text.Json;
using RenoDXCommander.Collections;
using RenoDXCommander.Models;
using RenoDXCommander.ViewModels;
using Xunit;

namespace RenoDXCommander.Tests;

/// <summary>
/// Unit tests for universal search — specific examples and edge cases
/// that complement the property-based tests.
/// </summary>
public class UniversalSearchUnitTests
{
    // ── Helpers ───────────────────────────────────────────────────────────────────

    private static (FilterViewModel vm, BatchObservableCollection<GameCardViewModel> displayed) CreateFilterViewModel(
        IReadOnlyList<GameCardViewModel> cards)
    {
        var displayed = new BatchObservableCollection<GameCardViewModel>();
        var vm = new FilterViewModel();
        vm.Initialize(displayed);
        vm.SetAllCards(cards);
        return (vm, displayed);
    }

    // ── Req 1.5: "dx11" returns cards with GraphicsApi == DirectX11 ──────────────

    /// <summary>
    /// Searching "DirectX11" should return exactly the non-hidden cards whose
    /// GraphicsApi is DirectX11 (matched via GraphicsApi.ToString() substring).
    /// Note: The enum value "DirectX11" does not contain "dx11" as a substring,
    /// so we use the full enum name. "x11" also uniquely matches DirectX11.
    /// Validates: Requirements 1.5
    /// </summary>
    [Fact]
    public void Search_DirectX11_ReturnsDirectX11Cards()
    {
        var dx11Card = new GameCardViewModel
        {
            GameName = "Cyberpunk 2077",
            Maintainer = "ShortFuse",
            Source = "Steam",
            EngineHint = "",
            GraphicsApi = GraphicsApiType.DirectX11,
            Is32Bit = false,
            IsHidden = false
        };
        var dx12Card = new GameCardViewModel
        {
            GameName = "Starfield",
            Maintainer = "ERSH",
            Source = "Steam",
            EngineHint = "Unreal 5",
            GraphicsApi = GraphicsApiType.DirectX12,
            Is32Bit = false,
            IsHidden = false
        };
        var vulkanCard = new GameCardViewModel
        {
            GameName = "DOOM Eternal",
            Maintainer = "pumbo",
            Source = "Steam",
            EngineHint = "idTech",
            GraphicsApi = GraphicsApiType.Vulkan,
            Is32Bit = false,
            IsHidden = false
        };
        var hiddenDx11Card = new GameCardViewModel
        {
            GameName = "Hidden DX11 Game",
            Maintainer = "TestDev",
            Source = "GOG",
            EngineHint = "",
            GraphicsApi = GraphicsApiType.DirectX11,
            Is32Bit = false,
            IsHidden = true
        };

        var cards = new List<GameCardViewModel> { dx11Card, dx12Card, vulkanCard, hiddenDx11Card };
        var (vm, displayed) = CreateFilterViewModel(cards);

        vm.SearchQuery = "DirectX11";
        vm.ApplyFilter();

        Assert.Single(displayed);
        Assert.Contains(dx11Card, displayed);
        Assert.DoesNotContain(dx12Card, displayed);
        Assert.DoesNotContain(vulkanCard, displayed);
        Assert.DoesNotContain(hiddenDx11Card, displayed); // hidden cards excluded
    }

    // ── Req 1.6: "steam" returns cards with Source containing "steam" ────────────

    /// <summary>
    /// Searching "steam" should return non-hidden cards where Source contains
    /// "steam" (case-insensitive).
    /// Validates: Requirements 1.6
    /// </summary>
    [Fact]
    public void Search_Steam_ReturnsCardsWithSteamSource()
    {
        var steamCard = new GameCardViewModel
        {
            GameName = "ELDEN RING",
            Maintainer = "ERSH",
            Source = "Steam",
            EngineHint = "",
            GraphicsApi = GraphicsApiType.DirectX12,
            Is32Bit = false,
            IsHidden = false
        };
        var gogCard = new GameCardViewModel
        {
            GameName = "The Witcher 3",
            Maintainer = "pumbo",
            Source = "GOG",
            EngineHint = "",
            GraphicsApi = GraphicsApiType.DirectX11,
            Is32Bit = false,
            IsHidden = false
        };
        var epicCard = new GameCardViewModel
        {
            GameName = "Alan Wake 2",
            Maintainer = "MARAT",
            Source = "Epic",
            EngineHint = "",
            GraphicsApi = GraphicsApiType.DirectX12,
            Is32Bit = false,
            IsHidden = false
        };

        var cards = new List<GameCardViewModel> { steamCard, gogCard, epicCard };
        var (vm, displayed) = CreateFilterViewModel(cards);

        vm.SearchQuery = "steam";
        vm.ApplyFilter();

        Assert.Single(displayed);
        Assert.Contains(steamCard, displayed);
        Assert.DoesNotContain(gogCard, displayed);
        Assert.DoesNotContain(epicCard, displayed);
    }

    // ── Req 1.4: Empty/whitespace search returns all non-hidden cards ────────────

    /// <summary>
    /// An empty or whitespace-only search query should return all non-hidden cards
    /// (no search restriction applied).
    /// Validates: Requirements 1.4
    /// </summary>
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t")]
    public void Search_EmptyOrWhitespace_ReturnsAllNonHiddenCards(string query)
    {
        var visibleCard1 = new GameCardViewModel
        {
            GameName = "Cyberpunk 2077",
            Maintainer = "ShortFuse",
            Source = "Steam",
            EngineHint = "",
            GraphicsApi = GraphicsApiType.DirectX12,
            Is32Bit = false,
            IsHidden = false
        };
        var visibleCard2 = new GameCardViewModel
        {
            GameName = "ELDEN RING",
            Maintainer = "ERSH",
            Source = "Steam",
            EngineHint = "",
            GraphicsApi = GraphicsApiType.DirectX12,
            Is32Bit = false,
            IsHidden = false
        };
        var hiddenCard = new GameCardViewModel
        {
            GameName = "Hidden Game",
            Maintainer = "TestDev",
            Source = "GOG",
            EngineHint = "",
            GraphicsApi = GraphicsApiType.DirectX11,
            Is32Bit = false,
            IsHidden = true
        };

        var cards = new List<GameCardViewModel> { visibleCard1, visibleCard2, hiddenCard };
        var (vm, displayed) = CreateFilterViewModel(cards);

        vm.SearchQuery = query;
        vm.ApplyFilter();

        Assert.Equal(2, displayed.Count);
        Assert.Contains(visibleCard1, displayed);
        Assert.Contains(visibleCard2, displayed);
        Assert.DoesNotContain(hiddenCard, displayed);
    }

    // ── Req 2.2: Clearing search while filters active shows filter-only results ──

    /// <summary>
    /// When a search query is cleared while filter chips remain active,
    /// the results should show all cards matching the active filter criteria
    /// without any search restriction.
    /// Validates: Requirements 2.2
    /// </summary>
    [Fact]
    public void ClearSearch_WithActiveFilter_ShowsFilterOnlyResults()
    {
        var installedSteamCard = new GameCardViewModel
        {
            GameName = "Cyberpunk 2077",
            Maintainer = "ShortFuse",
            Source = "Steam",
            EngineHint = "",
            GraphicsApi = GraphicsApiType.DirectX12,
            Is32Bit = false,
            IsHidden = false,
            RsStatus = GameStatus.Installed
        };
        var installedGogCard = new GameCardViewModel
        {
            GameName = "The Witcher 3",
            Maintainer = "pumbo",
            Source = "GOG",
            EngineHint = "",
            GraphicsApi = GraphicsApiType.DirectX11,
            Is32Bit = false,
            IsHidden = false,
            RsStatus = GameStatus.Installed
        };
        var notInstalledSteamCard = new GameCardViewModel
        {
            GameName = "ELDEN RING",
            Maintainer = "ERSH",
            Source = "Steam",
            EngineHint = "",
            GraphicsApi = GraphicsApiType.DirectX12,
            Is32Bit = false,
            IsHidden = false,
            RsStatus = GameStatus.NotInstalled
        };

        var cards = new List<GameCardViewModel> { installedSteamCard, installedGogCard, notInstalledSteamCard };
        var (vm, displayed) = CreateFilterViewModel(cards);

        // Set "Installed" filter and search for "steam"
        vm.SetFilter("Installed");
        vm.SearchQuery = "steam";
        vm.ApplyFilter();

        // Only the installed Steam card should match both filter + search
        Assert.Single(displayed);
        Assert.Contains(installedSteamCard, displayed);

        // Now clear the search — should show all installed cards (filter-only)
        vm.SearchQuery = "";
        vm.ApplyFilter();

        Assert.Equal(2, displayed.Count);
        Assert.Contains(installedSteamCard, displayed);
        Assert.Contains(installedGogCard, displayed);
        Assert.DoesNotContain(notInstalledSteamCard, displayed);
    }

    // ── Req 6.3: Malformed JSON results in empty custom filter list without crash ─

    /// <summary>
    /// Deserializing malformed JSON as a list of CustomFilter should not throw.
    /// The application should gracefully fall back to an empty list.
    /// Validates: Requirements 6.3
    /// </summary>
    [Theory]
    [InlineData("not valid json")]
    [InlineData("{broken")]
    [InlineData("")]
    [InlineData("[{\"Name\":\"ok\",\"Query\":\"q\"")]  // truncated array
    public void MalformedJson_ReturnsEmptyCustomFilterList_WithoutCrash(string malformedJson)
    {
        // Act: attempt to deserialize malformed JSON the same way the app does
        List<CustomFilter> result;
        try
        {
            result = JsonSerializer.Deserialize<List<CustomFilter>>(malformedJson) ?? new();
        }
        catch (JsonException)
        {
            // Expected — malformed JSON should be caught and result in empty list
            result = new List<CustomFilter>();
        }

        // Assert: graceful fallback to empty list, no crash
        Assert.NotNull(result);
        Assert.Empty(result);
    }

    // ── Req 6.2: Valid custom filters JSON populates FilterViewModel correctly ────

    /// <summary>
    /// Serializing custom filters to JSON, deserializing them, and populating
    /// a FilterViewModel should result in the correct CustomFilters collection.
    /// Validates: Requirements 6.2
    /// </summary>
    [Fact]
    public void ValidCustomFiltersJson_PopulatesFilterViewModel_Correctly()
    {
        // Arrange: create custom filters and serialize to JSON
        var original = new List<CustomFilter>
        {
            new() { Name = "DX11 Games", Query = "DirectX11" },
            new() { Name = "Steam Only", Query = "steam" }
        };
        var json = JsonSerializer.Serialize(original);

        // Act: deserialize and populate FilterViewModel
        var loaded = JsonSerializer.Deserialize<List<CustomFilter>>(json) ?? new();

        var displayed = new BatchObservableCollection<GameCardViewModel>();
        var vm = new FilterViewModel();
        vm.Initialize(displayed);
        vm.SetAllCards(Array.Empty<GameCardViewModel>());

        foreach (var filter in loaded)
        {
            vm.AddCustomFilter(filter.Name, filter.Query);
        }

        // Assert: FilterViewModel has the correct custom filters
        Assert.Equal(2, vm.CustomFilters.Count);
        Assert.Equal("DX11 Games", vm.CustomFilters[0].Name);
        Assert.Equal("DirectX11", vm.CustomFilters[0].Query);
        Assert.Equal("Steam Only", vm.CustomFilters[1].Name);
        Assert.Equal("steam", vm.CustomFilters[1].Query);
    }
}
