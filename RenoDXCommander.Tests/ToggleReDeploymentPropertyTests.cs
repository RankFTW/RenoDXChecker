using System.Reflection;
using FsCheck;
using FsCheck.Xunit;
using RenoDXCommander.Models;
using RenoDXCommander.ViewModels;

namespace RenoDXCommander.Tests;

/// <summary>
/// Property-based tests for toggle change triggering shader re-deployment.
/// Feature: custom-shaders, Property 4: Toggle change triggers shader re-deployment
/// **Validates: Requirements 6.2**
/// </summary>
public class ToggleReDeploymentPropertyTests
{
    // ── Generators ────────────────────────────────────────────────────────────────

    private static readonly Gen<string> GenGameName =
        Gen.Elements(
            "Cyberpunk 2077", "Elden Ring", "Starfield", "Hogwarts Legacy",
            "Alan Wake 2", "Baldur's Gate 3", "Final Fantasy XVI",
            "The Witcher 3", "Red Dead Redemption 2", "Halo Infinite");

    private static readonly Gen<string> GenInstallPath =
        Gen.Elements(
            @"C:\Games\Game1", @"C:\Games\Game2", @"C:\Games\Game3",
            @"C:\Games\Game4", @"C:\Games\Game5");

    /// <summary>
    /// Generates a game card with ReShade installed (non-Vulkan) so deployment will proceed.
    /// </summary>
    private static Gen<GameCardViewModel> GenInstalledCard() =>
        from name in GenGameName
        from path in GenInstallPath
        select new GameCardViewModel
        {
            GameName = name,
            InstallPath = path,
            RsStatus = GameStatus.Installed,
            // Non-Vulkan game: IsVulkanOnly=false, IsDualApiGame=false → RequiresVulkanInstall=false
            GraphicsApi = GraphicsApiType.DirectX11,
            IsDualApiGame = false,
        };

    /// <summary>
    /// Generates a list of 1–4 unique game cards with ReShade installed.
    /// </summary>
    private static Gen<List<GameCardViewModel>> GenCardList() =>
        from count in Gen.Choose(1, 4)
        from cards in Gen.ListOf(count, GenInstalledCard())
        select cards.GroupBy(c => c.GameName).Select(g => g.First()).ToList();

    // ── Helpers ───────────────────────────────────────────────────────────────────

    /// <summary>
    /// Injects cards into the private _allCards field of MainViewModel via reflection.
    /// </summary>
    private static void InjectCards(MainViewModel vm, List<GameCardViewModel> cards)
    {
        var field = typeof(MainViewModel).GetField("_allCards", BindingFlags.NonPublic | BindingFlags.Instance)!;
        field.SetValue(vm, cards);
    }

    /// <summary>
    /// Gets the StubShaderPackService from the MainViewModel via the public property.
    /// </summary>
    private static TestHelpers.StubShaderPackService GetStubShaderPackService(MainViewModel vm)
        => (TestHelpers.StubShaderPackService)vm.ShaderPackServiceInstance;

    // ── Property 4: Toggle change triggers shader re-deployment ───────────────────

    /// <summary>
    /// For any set of games with ReShade installed, when the global UseCustomShaders
    /// toggle is changed, DeployAllShaders shall be invoked, causing SyncGameFolder
    /// to be called for each installed game.
    /// **Validates: Requirements 6.2**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property GlobalToggleChange_TriggersReDeployment()
    {
        return Prop.ForAll(
            GenCardList().ToArbitrary(),
            Arb.Default.Bool(),
            (List<GameCardViewModel> cards, bool initialCustomState) =>
            {
                // Arrange
                var vm = TestHelpers.CreateMainViewModel();
                var stub = GetStubShaderPackService(vm);

                // Set initial state without triggering deployment
                vm.Settings.IsLoadingSettings = true;
                vm.Settings.UseCustomShaders = initialCustomState;
                vm.Settings.IsLoadingSettings = false;

                // Inject cards and mark as initialized
                InjectCards(vm, cards);
                vm.MarkInitialized();

                // Clear any prior calls
                stub.SyncGameFolderCalls.Clear();

                // Act: toggle UseCustomShaders to the opposite value
                vm.Settings.UseCustomShaders = !initialCustomState;

                // DeployAllShaders runs on Task.Run — give it time to complete
                Thread.Sleep(200);

                // Assert: SyncGameFolder was called for each card
                var calledPaths = stub.SyncGameFolderCalls.Select(c => c.GameDir).ToList();

                foreach (var card in cards)
                {
                    if (!calledPaths.Contains(card.InstallPath))
                        return false.Label(
                            $"SyncGameFolder not called for '{card.GameName}' at '{card.InstallPath}' " +
                            $"after global toggle change. Called paths: [{string.Join(", ", calledPaths)}]");
                }

                return true.Label(
                    $"OK: Global toggle {initialCustomState}→{!initialCustomState} triggered deployment for {cards.Count} game(s)");
            });
    }

    /// <summary>
    /// For any game with ReShade installed, when DeployShadersForCard is called
    /// (as happens when the per-game custom shaders toggle changes), SyncGameFolder
    /// shall be invoked for that game.
    /// **Validates: Requirements 6.2**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property PerGameToggleChange_TriggersReDeploymentForCard()
    {
        return Prop.ForAll(
            GenInstalledCard().ToArbitrary(),
            Arb.Default.Bool(),
            (GameCardViewModel card, bool useGlobalCustom) =>
            {
                // Arrange
                var vm = TestHelpers.CreateMainViewModel();
                var stub = GetStubShaderPackService(vm);

                vm.Settings.IsLoadingSettings = true;
                vm.Settings.UseCustomShaders = useGlobalCustom;
                vm.Settings.IsLoadingSettings = false;

                // Inject the card and mark as initialized
                InjectCards(vm, new List<GameCardViewModel> { card });
                vm.MarkInitialized();

                // Clear any prior calls
                stub.SyncGameFolderCalls.Clear();

                // Act: call DeployShadersForCard (this is what the per-game toggle handler calls)
                vm.DeployShadersForCard(card.GameName);

                // DeployShadersForCard runs on Task.Run — give it time to complete
                Thread.Sleep(200);

                // Assert: SyncGameFolder was called for this card's install path
                var calledPaths = stub.SyncGameFolderCalls.Select(c => c.GameDir).ToList();

                if (!calledPaths.Contains(card.InstallPath))
                    return false.Label(
                        $"SyncGameFolder not called for '{card.GameName}' at '{card.InstallPath}' " +
                        $"after DeployShadersForCard. Called paths: [{string.Join(", ", calledPaths)}]");

                return true.Label(
                    $"OK: DeployShadersForCard triggered deployment for '{card.GameName}'");
            });
    }
}
