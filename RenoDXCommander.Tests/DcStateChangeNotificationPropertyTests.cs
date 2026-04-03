using FsCheck;
using FsCheck.Xunit;
using RenoDXCommander.Models;
using RenoDXCommander.ViewModels;

namespace RenoDXCommander.Tests;

// Feature: display-commander-reintegration, Property 7: DC state change notifications

/// <summary>
/// Property-based tests for DC state change notifications.
/// When DcStatus or DcIsInstalling changes on a GameCardViewModel,
/// PropertyChanged fires for all expected dependent property names.
/// **Validates: Requirements 10.3, 10.4**
/// </summary>
public class DcStateChangeNotificationPropertyTests
{
    private static readonly Gen<GameStatus> GameStatusGen =
        Gen.Elements(GameStatus.NotInstalled, GameStatus.Available, GameStatus.Installed, GameStatus.UpdateAvailable);

    /// <summary>
    /// Expected property names fired when DcStatus changes.
    /// Matches NotifyDcStatusDependents() in GameCardViewModel.DisplayCommander.cs.
    /// </summary>
    private static readonly HashSet<string> DcStatusDependents = new(StringComparer.Ordinal)
    {
        "DcStatusDot", "DcActionLabel", "DcBtnBackground", "DcBtnForeground",
        "DcBtnBorderBrush", "DcDeleteVisibility", "DcStatusText", "DcStatusColor",
        "IsDcInstalled", "DcInstallEnabled", "UpdateBadgeVisibility",
        "IsManaged", "SidebarItemForeground", "CardPrimaryActionLabel",
        "CardDcStatusDot", "CardDcInstallEnabled"
    };

    /// <summary>
    /// Expected property names fired when DcIsInstalling changes.
    /// Matches NotifyDcIsInstallingDependents() in GameCardViewModel.DisplayCommander.cs.
    /// </summary>
    private static readonly HashSet<string> DcIsInstallingDependents = new(StringComparer.Ordinal)
    {
        "DcActionLabel", "DcProgressVisibility", "IsDcNotInstalling",
        "DcInstallEnabled", "DcStatusText", "DcStatusColor", "DcShortAction",
        "CardDcStatusDot", "CardDcInstallEnabled", "CanCardInstall"
    };

    [Property(MaxTest = 10)]
    public Property DcStatus_Change_FiresAllDependentProperties()
    {
        var gen = from initial in GameStatusGen
                  from target in GameStatusGen
                  where initial != target
                  select (initial, target);

        return Prop.ForAll(gen.ToArbitrary(), tuple =>
        {
            var card = new GameCardViewModel { DcStatus = tuple.initial };

            var fired = new HashSet<string>(StringComparer.Ordinal);
            card.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName is not null)
                    fired.Add(e.PropertyName);
            };

            card.DcStatus = tuple.target;

            var missing = DcStatusDependents.Except(fired).ToList();

            return (!missing.Any()).Label(
                $"DcStatus {tuple.initial}->{tuple.target}: missing notifications: [{string.Join(", ", missing)}]");
        });
    }

    [Property(MaxTest = 10)]
    public Property DcIsInstalling_Change_FiresAllDependentProperties()
    {
        var gen = from status in GameStatusGen
                  select status;

        return Prop.ForAll(gen.ToArbitrary(), status =>
        {
            // Start with DcIsInstalling = false, then set to true
            var card = new GameCardViewModel { DcStatus = status, DcIsInstalling = false };

            var fired = new HashSet<string>(StringComparer.Ordinal);
            card.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName is not null)
                    fired.Add(e.PropertyName);
            };

            card.DcIsInstalling = true;

            var missing = DcIsInstallingDependents.Except(fired).ToList();

            return (!missing.Any()).Label(
                $"DcIsInstalling false->true (status={status}): missing notifications: [{string.Join(", ", missing)}]");
        });
    }
}
