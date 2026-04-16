using FsCheck;
using FsCheck.Xunit;
using RenoDXCommander.Models;
using RenoDXCommander.ViewModels;

namespace RenoDXCommander.Tests;

// Feature: display-commander-reintegration, Property 2: Mutual exclusion of DC and ReLimiter

/// <summary>
/// Property-based tests for mutual exclusion of DC and ReLimiter.
/// For any combination of DC/UL status and installing flags, DcInstallEnabled and
/// UlInstallEnabled are mutually exclusive when one limiter is installed.
/// **Validates: Requirements 2.1, 2.2, 2.5, 2.6, 2.7, 2.8**
/// </summary>
public class DcMutualExclusionPropertyTests
{
    private static readonly Gen<GameStatus> GameStatusGen =
        Gen.Elements(GameStatus.NotInstalled, GameStatus.Available, GameStatus.Installed, GameStatus.UpdateAvailable);

    [Property(MaxTest = 10)]
    public Property WhenUlInstalled_DcInstallEnabled_IsFalse()
    {
        var gen = from dcStatus in GameStatusGen
                  from ulStatus in Gen.Elements(GameStatus.Installed, GameStatus.UpdateAvailable)
                  from dcInstalling in Arb.Default.Bool().Generator
                  from ulInstalling in Arb.Default.Bool().Generator
                  select (dcStatus, ulStatus, dcInstalling, ulInstalling);

        return Prop.ForAll(gen.ToArbitrary(), tuple =>
        {
            var card = new GameCardViewModel
            {
                DcStatus = tuple.dcStatus,
                UlStatus = tuple.ulStatus,
                DcIsInstalling = tuple.dcInstalling,
                UlIsInstalling = tuple.ulInstalling
            };

            if (card.DcInstallEnabled)
                return false.Label(
                    $"DcInstallEnabled should be false when UL is installed (UlStatus={tuple.ulStatus}, IsUlInstalled={card.IsUlInstalled})");

            return true.Label("OK: DcInstallEnabled is false when UL is installed");
        });
    }

    [Property(MaxTest = 10)]
    public Property WhenDcInstalled_UlInstallEnabled_IsFalse()
    {
        var gen = from dcStatus in Gen.Elements(GameStatus.Installed, GameStatus.UpdateAvailable)
                  from ulStatus in GameStatusGen
                  from dcInstalling in Arb.Default.Bool().Generator
                  from ulInstalling in Arb.Default.Bool().Generator
                  select (dcStatus, ulStatus, dcInstalling, ulInstalling);

        return Prop.ForAll(gen.ToArbitrary(), tuple =>
        {
            var card = new GameCardViewModel
            {
                DcStatus = tuple.dcStatus,
                UlStatus = tuple.ulStatus,
                DcIsInstalling = tuple.dcInstalling,
                UlIsInstalling = tuple.ulInstalling
            };

            if (card.UlInstallEnabled)
                return false.Label(
                    $"UlInstallEnabled should be false when DC is installed (DcStatus={tuple.dcStatus}, IsDcInstalled={card.IsDcInstalled})");

            return true.Label("OK: UlInstallEnabled is false when DC is installed");
        });
    }

    [Property(MaxTest = 10)]
    public Property WhenNeitherInstalled_NeitherInstalling_BothEnabled()
    {
        var notInstalledGen = Gen.Elements(GameStatus.NotInstalled, GameStatus.Available);

        var gen = from dcStatus in notInstalledGen
                  from ulStatus in notInstalledGen
                  select (dcStatus, ulStatus);

        return Prop.ForAll(gen.ToArbitrary(), tuple =>
        {
            var card = new GameCardViewModel
            {
                DcStatus = tuple.dcStatus,
                UlStatus = tuple.ulStatus,
                DcIsInstalling = false,
                UlIsInstalling = false,
                RsStatus = GameStatus.Installed  // ReShade must be installed for DC/UL to be enabled
            };

            if (!card.DcInstallEnabled)
                return false.Label(
                    $"DcInstallEnabled should be true when neither is installed (DcStatus={tuple.dcStatus}, UlStatus={tuple.ulStatus})");

            if (!card.UlInstallEnabled)
                return false.Label(
                    $"UlInstallEnabled should be true when neither is installed (DcStatus={tuple.dcStatus}, UlStatus={tuple.ulStatus})");

            return true.Label("OK: both install buttons enabled when neither limiter is installed");
        });
    }
}
