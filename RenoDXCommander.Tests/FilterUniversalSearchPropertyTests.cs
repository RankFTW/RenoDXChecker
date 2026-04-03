using FsCheck;
using FsCheck.Xunit;
using RenoDXCommander.Collections;
using RenoDXCommander.Models;
using RenoDXCommander.ViewModels;

namespace RenoDXCommander.Tests;

/// <summary>
/// Property-based tests for universal search filtering in FilterViewModel.
/// </summary>
public class FilterUniversalSearchPropertyTests
{
    // ── Generators ────────────────────────────────────────────────────────────────

    private static readonly Gen<string> GenSource =
        Gen.Elements("Steam", "GOG", "Epic", "Xbox", "Ubisoft", "EA", "Rockstar", "BattleNet", "");

    private static readonly Gen<string> GenEngineHint =
        Gen.Elements("Unreal 5", "Unreal 4", "Unity", "RE Engine", "Frostbite", "idTech", "");

    private static readonly Gen<GraphicsApiType> GenGraphicsApi =
        Gen.Elements(
            GraphicsApiType.Unknown, GraphicsApiType.DirectX8, GraphicsApiType.DirectX9,
            GraphicsApiType.DirectX10, GraphicsApiType.DirectX11, GraphicsApiType.DirectX12,
            GraphicsApiType.Vulkan, GraphicsApiType.OpenGL);

    private static readonly Gen<string> GenVulkanRenderingPath =
        Gen.Elements("DirectX", "Vulkan", "");

    private static readonly Gen<string> GenGameName =
        Gen.Elements("Cyberpunk 2077", "ELDEN RING", "Starfield", "Baldur's Gate 3",
                     "Alan Wake 2", "RETURNAL", "Hades II", "The Witcher 3");

    private static readonly Gen<string> GenMaintainer =
        Gen.Elements("ShortFuse", "ERSH", "pumbo", "MARAT", "TestDev", "");

    private static readonly Gen<GameMod?> GenMod =
        Gen.OneOf(
            Gen.Constant<GameMod?>(null),
            from name in Gen.Elements("RenoDX Core", "HDR Mod", "Tonemapper Fix", "")
            from maintainer in Gen.Elements("ShortFuse", "pumbo", "ModAuthor", "")
            select (GameMod?)new GameMod { Name = name, Maintainer = maintainer });

    private static readonly Gen<LumaMod?> GenLumaMod =
        Gen.OneOf(
            Gen.Constant<LumaMod?>(null),
            from name in Gen.Elements("Luma HDR", "Luma Tonemapper", "LumaFX", "")
            from author in Gen.Elements("Filoppi", "LumaAuthor", "TestAuthor", "")
            select (LumaMod?)new LumaMod { Name = name, Author = author });

    /// <summary>
    /// Generates a GameCardViewModel with varied searchable properties.
    /// </summary>
    private static readonly Gen<HashSet<GraphicsApiType>> GenDetectedApis =
        Gen.SubListOf(new[] {
            GraphicsApiType.DirectX9, GraphicsApiType.DirectX10,
            GraphicsApiType.DirectX11, GraphicsApiType.DirectX12,
            GraphicsApiType.Vulkan, GraphicsApiType.OpenGL })
        .Select(l => new HashSet<GraphicsApiType>(l));

    private static readonly Gen<GameCardViewModel> GenCard =
        from gameName in GenGameName
        from maintainer in GenMaintainer
        from source in GenSource
        from engineHint in GenEngineHint
        from graphicsApi in GenGraphicsApi
        from detectedApis in GenDetectedApis
        from is32Bit in Arb.Default.Bool().Generator
        from isHidden in Arb.Default.Bool().Generator
        from isREEngine in Arb.Default.Bool().Generator
        from mod in GenMod
        from lumaMod in GenLumaMod
        from vulkanPath in GenVulkanRenderingPath
        select new GameCardViewModel
        {
            GameName = gameName,
            Maintainer = maintainer,
            Source = source,
            EngineHint = engineHint,
            GraphicsApi = graphicsApi,
            DetectedApis = detectedApis,
            Is32Bit = is32Bit,
            IsHidden = isHidden,
            IsREEngineGame = isREEngine,
            Mod = mod,
            LumaMod = lumaMod,
            VulkanRenderingPath = vulkanPath
        };

    /// <summary>
    /// Generates a list of 0–20 GameCardViewModels.
    /// </summary>
    private static readonly Gen<List<GameCardViewModel>> GenCardList =
        GenCard.ListOf().Select(l => l.ToList())
               .Where(l => l.Count <= 20);

    /// <summary>
    /// Generates search queries that include substrings of known property values
    /// and random strings unlikely to match.
    /// </summary>
    private static readonly Gen<string> GenSearchQuery =
        Gen.OneOf(
            // Substrings of Source values
            Gen.Elements("steam", "GOG", "Epic", "xbox"),
            // Substrings of EngineHint values
            Gen.Elements("unreal", "Unity", "Frostbite"),
            // GraphicsApi substrings (enum names and display labels)
            Gen.Elements("DirectX11", "DirectX12", "Vulkan", "OpenGL", "DX11", "DX9", "VLK", "OGL"),
            // Bitness strings
            Gen.Elements("32-bit", "64-bit"),
            // Mod/LumaMod name substrings
            Gen.Elements("RenoDX", "HDR", "Luma", "Filoppi"),
            // VulkanRenderingPath
            Gen.Elements("DirectX", "Vulkan"),
            // Game name / maintainer substrings
            Gen.Elements("cyber", "ELDEN", "ShortFuse", "pumbo"),
            // RE Engine / RE Framework
            Gen.Elements("RE Engine", "RE Framework"),
            // Unlikely to match
            Gen.Elements("xyz_nomatch", "qqq_nothing"));

    /// <summary>
    /// Generates a GameCardViewModel with filter-relevant properties varied
    /// (IsFavourite, RsStatus, Status, Mod with IsGenericUnreal/IsGenericUnity, LumaMod).
    /// </summary>
    private static readonly Gen<GameCardViewModel> GenFilterCard =
        from gameName in GenGameName
        from maintainer in GenMaintainer
        from source in GenSource
        from engineHint in GenEngineHint
        from graphicsApi in GenGraphicsApi
        from is32Bit in Arb.Default.Bool().Generator
        from isHidden in Arb.Default.Bool().Generator
        from isFavourite in Arb.Default.Bool().Generator
        from status in Gen.Elements(GameStatus.NotInstalled, GameStatus.Available, GameStatus.Installed, GameStatus.UpdateAvailable)
        from rsStatus in Gen.Elements(GameStatus.NotInstalled, GameStatus.Available, GameStatus.Installed, GameStatus.UpdateAvailable)
        from mod in GenFilterMod
        from lumaMod in GenLumaMod
        from vulkanPath in GenVulkanRenderingPath
        select new GameCardViewModel
        {
            GameName = gameName,
            Maintainer = maintainer,
            Source = source,
            EngineHint = engineHint,
            GraphicsApi = graphicsApi,
            Is32Bit = is32Bit,
            IsHidden = isHidden,
            IsFavourite = isFavourite,
            Status = status,
            RsStatus = rsStatus,
            Mod = mod,
            LumaMod = lumaMod,
            VulkanRenderingPath = vulkanPath
        };

    /// <summary>
    /// Generates GameMod with IsGenericUnreal/IsGenericUnity flags for filter testing.
    /// </summary>
    private static readonly Gen<GameMod?> GenFilterMod =
        Gen.OneOf(
            Gen.Constant<GameMod?>(null),
            from name in Gen.Elements("RenoDX Core", "HDR Mod", "Tonemapper Fix", "")
            from maintainer in Gen.Elements("ShortFuse", "pumbo", "ModAuthor", "")
            from isGenericUnreal in Arb.Default.Bool().Generator
            from isGenericUnity in Arb.Default.Bool().Generator
            select (GameMod?)new GameMod
            {
                Name = name,
                Maintainer = maintainer,
                IsGenericUnreal = isGenericUnreal,
                IsGenericUnity = isGenericUnity
            });

    /// <summary>
    /// Generates a list of 0–20 GameCardViewModels with filter-relevant properties.
    /// </summary>
    private static readonly Gen<List<GameCardViewModel>> GenFilterCardList =
        GenFilterCard.ListOf().Select(l => l.ToList())
                     .Where(l => l.Count <= 20);

    /// <summary>
    /// Generates a valid filter combination: either a single exclusive filter
    /// or one or more combinable filters.
    /// </summary>
    private static readonly Gen<HashSet<string>> GenFilterCombination =
        Gen.OneOf(
            // Exclusive filters (single)
            Gen.Elements("Detected", "Favourites", "Hidden", "Installed")
               .Select(f => new HashSet<string>(StringComparer.OrdinalIgnoreCase) { f }),
            // Combinable filters (1–5 from the set)
            Gen.SubListOf(new[] { "Unreal", "Unity", "Other", "RenoDX", "Luma" })
               .Where(l => l.Count > 0)
               .Select(l => new HashSet<string>(l, StringComparer.OrdinalIgnoreCase)));

    // ── Helpers ───────────────────────────────────────────────────────────────────

    /// <summary>Creates a fresh, initialized FilterViewModel with "Detected" filter active.</summary>
    private static (FilterViewModel vm, BatchObservableCollection<GameCardViewModel> displayed) CreateFilterViewModel(
        IReadOnlyList<GameCardViewModel> cards)
    {
        var displayed = new BatchObservableCollection<GameCardViewModel>();
        var vm = new FilterViewModel();
        vm.Initialize(displayed);
        vm.SetAllCards(cards);
        return (vm, displayed);
    }

    /// <summary>
    /// Reference implementation of filter matching logic, independent of FilterViewModel.
    /// Returns true if the card passes the given filter combination (ignoring search).
    /// </summary>
    private static bool MatchesFilter(GameCardViewModel c, HashSet<string> filters)
    {
        // Hidden tab always shows hidden games
        if (filters.Contains("Hidden")) return c.IsHidden;

        // Favourites tab: show favourited games (even if hidden)
        if (filters.Contains("Favourites")) return c.IsFavourite;

        // Installed tab: show games with ReShade installed, not hidden
        if (filters.Contains("Installed"))
        {
            bool rsInstalled = c.RsStatus == GameStatus.Installed || c.RsStatus == GameStatus.UpdateAvailable;
            return rsInstalled && !c.IsHidden;
        }

        // Detected (All Games): show everything except hidden
        if (filters.Contains("Detected"))
        {
            return !c.IsHidden;
        }

        // Combinable filters — match ANY active filter (OR logic), exclude hidden
        bool matched = false;

        if (filters.Contains("Unity"))
        {
            var isUnity = (!string.IsNullOrEmpty(c.EngineHint) && c.EngineHint.IndexOf("Unity", StringComparison.OrdinalIgnoreCase) >= 0)
                          || (c.Mod?.IsGenericUnity == true);
            if (isUnity) matched = true;
        }
        if (filters.Contains("Unreal"))
        {
            var isUnreal = (!string.IsNullOrEmpty(c.EngineHint) && c.EngineHint.IndexOf("Unreal", StringComparison.OrdinalIgnoreCase) >= 0)
                           || (c.Mod?.IsGenericUnreal == true);
            if (isUnreal) matched = true;
        }
        if (filters.Contains("Other"))
        {
            var isUnity = (!string.IsNullOrEmpty(c.EngineHint) && c.EngineHint.IndexOf("Unity", StringComparison.OrdinalIgnoreCase) >= 0)
                          || (c.Mod?.IsGenericUnity == true);
            var isUnreal = (!string.IsNullOrEmpty(c.EngineHint) && c.EngineHint.IndexOf("Unreal", StringComparison.OrdinalIgnoreCase) >= 0)
                           || (c.Mod?.IsGenericUnreal == true);
            if (!isUnity && !isUnreal) matched = true;
        }
        if (filters.Contains("Luma"))
        {
            if (c.LumaMod != null) matched = true;
        }
        if (filters.Contains("RenoDX"))
        {
            if (c.Mod != null) { matched = true; }
            else
            {
                var isUnreal = !string.IsNullOrEmpty(c.EngineHint) && c.EngineHint.IndexOf("Unreal", StringComparison.OrdinalIgnoreCase) >= 0;
                var isUnity  = !string.IsNullOrEmpty(c.EngineHint) && c.EngineHint.IndexOf("Unity", StringComparison.OrdinalIgnoreCase) >= 0;
                if (isUnreal || isUnity) matched = true;
                if (c.Status == GameStatus.Installed || c.Status == GameStatus.UpdateAvailable) matched = true;
            }
        }

        if (!matched) return false;
        return !c.IsHidden;
    }

    // ── Property 1 ────────────────────────────────────────────────────────────────
    // Feature: universal-search-filters, Property 1: Universal search produces correct subset
    // **Validates: Requirements 1.1, 1.2, 1.3, 1.7**

    [Property(MaxTest = 10)]
    public Property ApplyFilter_UniversalSearch_ProducesCorrectSubset()
    {
        return Prop.ForAll(
            Arb.From(GenCardList),
            Arb.From(GenSearchQuery),
            (List<GameCardViewModel> cards, string query) =>
            {
                // Arrange: FilterViewModel with "Detected" filter (default)
                var (vm, displayed) = CreateFilterViewModel(cards);
                vm.SearchQuery = query;

                // Act
                vm.ApplyFilter();

                // Compute expected: non-hidden cards where MatchesUniversalSearch is true
                var trimmedQuery = query.Trim();
                var expected = cards
                    .Where(c => !c.IsHidden)
                    .Where(c => string.IsNullOrEmpty(trimmedQuery)
                                || FilterViewModel.MatchesUniversalSearch(c, trimmedQuery))
                    .OrderBy(c => c.GameName, StringComparer.OrdinalIgnoreCase)
                    .ToList();

                // Assert: displayed matches expected exactly
                var displayedList = displayed.ToList();

                bool countMatch = displayedList.Count == expected.Count;
                bool elementsMatch = displayedList.SequenceEqual(expected);

                // No matching card should be excluded
                var displayedSet = new HashSet<GameCardViewModel>(displayedList);
                bool noFalseExclusions = expected.All(c => displayedSet.Contains(c));

                // No non-matching card should be included
                bool noFalseInclusions = displayedList.All(c =>
                    !c.IsHidden &&
                    (string.IsNullOrEmpty(trimmedQuery) || FilterViewModel.MatchesUniversalSearch(c, trimmedQuery)));

                return (countMatch && elementsMatch && noFalseExclusions && noFalseInclusions)
                    .Label($"countMatch={countMatch}, elementsMatch={elementsMatch}, " +
                           $"noFalseExclusions={noFalseExclusions}, noFalseInclusions={noFalseInclusions} " +
                           $"(cards={cards.Count}, displayed={displayedList.Count}, expected={expected.Count}, query='{query}')");
            });
    }

    // ── Property 2 ────────────────────────────────────────────────────────────────
    // Feature: universal-search-filters, Property 2: Search and filter intersection
    // **Validates: Requirements 2.1**

    [Property(MaxTest = 10)]
    public Property ApplyFilter_SearchAndFilter_ReturnsIntersection()
    {
        return Prop.ForAll(
            Arb.From(GenFilterCardList),
            Arb.From(GenSearchQuery),
            Arb.From(GenFilterCombination),
            (List<GameCardViewModel> cards, string query, HashSet<string> filters) =>
            {
                // Arrange: FilterViewModel with the given filter combination
                var (vm, displayed) = CreateFilterViewModel(cards);

                // Apply the filter combination by calling SetFilter for each filter.
                // First set an exclusive filter to clear state, then set combinable ones.
                bool isExclusive = filters.Any(f =>
                    new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Detected", "Favourites", "Hidden", "Installed" }.Contains(f));

                if (isExclusive)
                {
                    vm.SetFilter(filters.First());
                }
                else
                {
                    // For combinable filters, set each one. First one removes "Detected".
                    foreach (var f in filters)
                        vm.SetFilter(f);
                }

                // Set the search query (this triggers ApplyFilter via OnSearchQueryChanged)
                vm.SearchQuery = query;

                // Act
                vm.ApplyFilter();

                // Compute expected: intersection of search match and filter match
                var trimmedQuery = query.Trim();

                var matchingSearch = cards
                    .Where(c => string.IsNullOrEmpty(trimmedQuery)
                                || FilterViewModel.MatchesUniversalSearch(c, trimmedQuery))
                    .ToHashSet();

                var matchingFilter = cards
                    .Where(c => MatchesFilter(c, filters))
                    .ToHashSet();

                var expected = cards
                    .Where(c => matchingSearch.Contains(c) && matchingFilter.Contains(c))
                    .OrderBy(c => c.GameName, StringComparer.OrdinalIgnoreCase)
                    .ToList();

                // Assert
                var displayedList = displayed.ToList();

                bool countMatch = displayedList.Count == expected.Count;
                bool elementsMatch = displayedList.SequenceEqual(expected);

                return (countMatch && elementsMatch)
                    .Label($"countMatch={countMatch}, elementsMatch={elementsMatch} " +
                           $"(cards={cards.Count}, displayed={displayedList.Count}, expected={expected.Count}, " +
                           $"query='{query}', filters=[{string.Join(",", filters)}])");
            });
    }
}
