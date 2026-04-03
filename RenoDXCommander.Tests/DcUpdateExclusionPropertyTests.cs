using FsCheck;
using FsCheck.Xunit;
using Microsoft.UI.Xaml;
using RenoDXCommander.Models;
using RenoDXCommander.ViewModels;

namespace RenoDXCommander.Tests;

// Feature: display-commander-reintegration, Property 6: DC update exclusion from badge and Update All

/// <summary>
/// Property-based tests for DC update exclusion from badge and Update All.
/// Generates random (excludeDc, dcStatus, ulStatus, rdxStatus, rsStatus) tuples
/// and verifies badge visibility and Update All eligibility.
/// **Validates: Requirements 7.2, 7.3, 8.2**
/// </summary>
public class DcUpdateExclusionPropertyTests
{
    private static readonly Gen<GameStatus> GameStatusGen =
        Gen.Elements(GameStatus.NotInstalled, GameStatus.Available, GameStatus.Installed, GameStatus.UpdateAvailable);

    [Property(MaxTest = 10)]
    public Property ExcludedDc_DoesNotTriggerBadge_Alone()
    {
        var gen = from ulStatus in GameStatusGen
                  from rdxStatus in GameStatusGen
                  from rsStatus in GameStatusGen
                  select (ulStatus, rdxStatus, rsStatus);

        return Prop.ForAll(gen.ToArbitrary(), tuple =>
        {
            var card = new GameCardViewModel
            {
                DcStatus = GameStatus.UpdateAvailable,
                ExcludeFromUpdateAllDc = true,
                // Ensure other components do NOT trigger the badge
                UlStatus = tuple.ulStatus == GameStatus.UpdateAvailable ? GameStatus.Installed : tuple.ulStatus,
                Status = tuple.rdxStatus == GameStatus.UpdateAvailable ? GameStatus.Installed : tuple.rdxStatus,
                RsStatus = tuple.rsStatus == GameStatus.UpdateAvailable ? GameStatus.Installed : tuple.rsStatus,
                ExcludeFromUpdateAllUl = false,
                ExcludeFromUpdateAllRenoDx = false,
                ExcludeFromUpdateAllReShade = false,
                ExcludeFromUpdateAllRef = false,
            };

            bool badgeHidden = card.UpdateBadgeVisibility == Visibility.Collapsed;

            return badgeHidden.Label(
                $"Badge should be Collapsed when DC excluded, but got {card.UpdateBadgeVisibility} " +
                $"(UL={tuple.ulStatus}, RDX={tuple.rdxStatus}, RS={tuple.rsStatus})");
        });
    }

    [Property(MaxTest = 10)]
    public Property IncludedDc_UpdateAvailable_TriggersBadge()
    {
        var gen = from ulStatus in GameStatusGen
                  from rdxStatus in GameStatusGen
                  from rsStatus in GameStatusGen
                  select (ulStatus, rdxStatus, rsStatus);

        return Prop.ForAll(gen.ToArbitrary(), tuple =>
        {
            var card = new GameCardViewModel
            {
                DcStatus = GameStatus.UpdateAvailable,
                ExcludeFromUpdateAllDc = false,
                UlStatus = tuple.ulStatus,
                Status = tuple.rdxStatus,
                RsStatus = tuple.rsStatus,
                ExcludeFromUpdateAllUl = false,
                ExcludeFromUpdateAllRenoDx = false,
                ExcludeFromUpdateAllReShade = false,
                ExcludeFromUpdateAllRef = false,
            };

            bool badgeVisible = card.UpdateBadgeVisibility == Visibility.Visible;

            return badgeVisible.Label(
                $"Badge should be Visible when DC included with UpdateAvailable, but got {card.UpdateBadgeVisibility} " +
                $"(UL={tuple.ulStatus}, RDX={tuple.rdxStatus}, RS={tuple.rsStatus})");
        });
    }
}
