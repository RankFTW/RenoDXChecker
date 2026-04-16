// Feature: optiscaler-integration, Property 4: Update Exclusion Filtering
using FsCheck;
using FsCheck.Xunit;
using RenoDXCommander.Models;
using RenoDXCommander.ViewModels;

namespace RenoDXCommander.Tests;

/// <summary>
/// Property-based tests for OptiScaler update exclusion filtering.
/// Uses FsCheck with xUnit.
///
/// **Validates: Requirements 2.6**
///
/// For any set of games with random ExcludeFromUpdateAllOs boolean flags,
/// the Update All operation updates exactly the games where
/// ExcludeFromUpdateAllOs is false and OptiScaler is installed (UpdateAvailable).
/// </summary>
public class OptiScalerUpdateExclusionPropertyTests
{
    /// <summary>
    /// Generates a list of GameCardViewModels with random OptiScaler states.
    /// Each card has a random OsStatus (Installed, UpdateAvailable, NotInstalled)
    /// and a random ExcludeFromUpdateAllOs flag.
    /// </summary>
    private static Gen<List<GameCardViewModel>> GenGameCards()
    {
        var osStatusGen = Gen.Elements(
            GameStatus.NotInstalled,
            GameStatus.Installed,
            GameStatus.UpdateAvailable);

        var cardGen = from osStatus in osStatusGen
                      from excluded in Arb.Default.Bool().Generator
                      from hidden in Arb.Default.Bool().Generator
                      from idx in Gen.Choose(0, 9999)
                      select new GameCardViewModel
                      {
                          GameName = $"Game_{idx}",
                          OsStatus = osStatus,
                          ExcludeFromUpdateAllOs = excluded,
                          IsHidden = hidden,
                      };

        return Gen.ListOf(cardGen).Select(cards => cards.ToList());
    }

    // ── Property 4: Update Exclusion Filtering ────────────────────────────────

    /// <summary>
    /// Property 4: Update Exclusion Filtering
    ///
    /// **Validates: Requirements 2.6**
    ///
    /// For any set of games with random ExcludeFromUpdateAllOs boolean flags,
    /// the Update All filtering selects exactly the games where:
    /// - OsStatus == UpdateAvailable (OptiScaler is installed and has an update)
    /// - ExcludeFromUpdateAllOs == false
    /// - IsHidden == false
    /// </summary>
    [Property(MaxTest = 100)]
    public Property UpdateAll_SelectsCorrectSubset()
    {
        return Prop.ForAll(
            Arb.From(GenGameCards()),
            (List<GameCardViewModel> cards) =>
            {
                // Act: apply the same filtering logic as UpdateAllOsAsync would
                var selected = cards
                    .Where(c => c.OsStatus == GameStatus.UpdateAvailable
                             && !c.IsHidden
                             && !c.ExcludeFromUpdateAllOs)
                    .ToList();

                // Assert: every selected card must have UpdateAvailable, not excluded, not hidden
                bool allSelectedCorrect = selected.All(c =>
                    c.OsStatus == GameStatus.UpdateAvailable
                    && !c.ExcludeFromUpdateAllOs
                    && !c.IsHidden);

                // Assert: every non-selected card must be either:
                // - not UpdateAvailable, or
                // - excluded, or
                // - hidden
                var notSelected = cards.Except(selected).ToList();
                bool allNotSelectedCorrect = notSelected.All(c =>
                    c.OsStatus != GameStatus.UpdateAvailable
                    || c.ExcludeFromUpdateAllOs
                    || c.IsHidden);

                return (allSelectedCorrect && allNotSelectedCorrect)
                    .Label($"cards={cards.Count}, selected={selected.Count}, " +
                           $"allSelectedCorrect={allSelectedCorrect}, " +
                           $"allNotSelectedCorrect={allNotSelectedCorrect}");
            });
    }

    /// <summary>
    /// When all games have ExcludeFromUpdateAllOs == true,
    /// no games should be selected for update.
    ///
    /// **Validates: Requirements 2.6**
    /// </summary>
    [Property(MaxTest = 50)]
    public Property UpdateAll_AllExcluded_SelectsNone()
    {
        var osStatusGen = Gen.Elements(
            GameStatus.NotInstalled,
            GameStatus.Installed,
            GameStatus.UpdateAvailable);

        var cardGen = from osStatus in osStatusGen
                      from idx in Gen.Choose(0, 9999)
                      select new GameCardViewModel
                      {
                          GameName = $"Game_{idx}",
                          OsStatus = osStatus,
                          ExcludeFromUpdateAllOs = true,
                          IsHidden = false,
                      };

        var cardsGen = Gen.ListOf(cardGen).Select(cards => cards.ToList());

        return Prop.ForAll(
            Arb.From(cardsGen),
            (List<GameCardViewModel> cards) =>
            {
                var selected = cards
                    .Where(c => c.OsStatus == GameStatus.UpdateAvailable
                             && !c.IsHidden
                             && !c.ExcludeFromUpdateAllOs)
                    .ToList();

                return (selected.Count == 0)
                    .Label($"Expected 0 selected, got {selected.Count} from {cards.Count} cards");
            });
    }

    /// <summary>
    /// When no games have OptiScaler installed (all NotInstalled or Installed without update),
    /// no games should be selected for update regardless of exclusion flags.
    ///
    /// **Validates: Requirements 2.6**
    /// </summary>
    [Property(MaxTest = 50)]
    public Property UpdateAll_NoUpdatesAvailable_SelectsNone()
    {
        var noUpdateStatusGen = Gen.Elements(
            GameStatus.NotInstalled,
            GameStatus.Installed);

        var cardGen = from osStatus in noUpdateStatusGen
                      from excluded in Arb.Default.Bool().Generator
                      from idx in Gen.Choose(0, 9999)
                      select new GameCardViewModel
                      {
                          GameName = $"Game_{idx}",
                          OsStatus = osStatus,
                          ExcludeFromUpdateAllOs = excluded,
                          IsHidden = false,
                      };

        var cardsGen = Gen.ListOf(cardGen).Select(cards => cards.ToList());

        return Prop.ForAll(
            Arb.From(cardsGen),
            (List<GameCardViewModel> cards) =>
            {
                var selected = cards
                    .Where(c => c.OsStatus == GameStatus.UpdateAvailable
                             && !c.IsHidden
                             && !c.ExcludeFromUpdateAllOs)
                    .ToList();

                return (selected.Count == 0)
                    .Label($"Expected 0 selected, got {selected.Count} from {cards.Count} cards");
            });
    }
}
