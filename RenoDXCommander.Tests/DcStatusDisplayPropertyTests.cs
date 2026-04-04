using FsCheck;
using FsCheck.Xunit;
using Microsoft.UI.Xaml;
using RenoDXCommander.Models;
using RenoDXCommander.ViewModels;

namespace RenoDXCommander.Tests;

// Feature: display-commander-reintegration, Property 3: DC status display consistency

/// <summary>
/// Property-based tests for DC status display consistency.
/// For any combination of DcStatus, DcIsInstalling, DcInstalledVersion, and Luma mode,
/// computed properties match the status mapping rules.
/// **Validates: Requirements 3.3, 5.1, 5.2, 5.3, 5.4, 5.5, 5.6, 8.4**
/// </summary>
public class DcStatusDisplayPropertyTests
{
    private static readonly Gen<GameStatus> GameStatusGen =
        Gen.Elements(GameStatus.NotInstalled, GameStatus.Available, GameStatus.Installed, GameStatus.UpdateAvailable);

    private static readonly Gen<string?> VersionGen =
        Gen.OneOf(Gen.Constant<string?>(null), Gen.Elements<string?>("1.0.0", "2.3.1"));

    [Property(MaxTest = 10)]
    public Property NotInstalled_NotInstalling_ShowsReady()
    {
        var gen = from status in Gen.Elements(GameStatus.NotInstalled, GameStatus.Available)
                  from version in VersionGen
                  select (status, version);

        return Prop.ForAll(gen.ToArbitrary(), tuple =>
        {
            var card = new GameCardViewModel
            {
                DcStatus = tuple.status,
                DcIsInstalling = false,
                DcInstalledVersion = tuple.version
            };

            bool textOk = card.DcStatusText == "Ready";
            bool colorOk = card.DcStatusColor == "#A0AABB";
            bool dotOk = card.DcStatusDot == "⚪";

            return (textOk && colorOk && dotOk).Label(
                $"Expected Ready/#A0AABB/⚪ but got {card.DcStatusText}/{card.DcStatusColor}/{card.DcStatusDot}");
        });
    }

    [Property(MaxTest = 10)]
    public Property Installed_NotInstalling_ShowsVersionOrInstalled()
    {
        var gen = from version in VersionGen
                  select version;

        return Prop.ForAll(gen.ToArbitrary(), version =>
        {
            var card = new GameCardViewModel
            {
                DcStatus = GameStatus.Installed,
                DcIsInstalling = false,
                DcInstalledVersion = version
            };

            string expectedText = version ?? "Installed";
            bool textOk = card.DcStatusText == expectedText;
            bool colorOk = card.DcStatusColor == "#5ECB7D";
            bool dotOk = card.DcStatusDot == "🟢";

            return (textOk && colorOk && dotOk).Label(
                $"Expected {expectedText}/#5ECB7D/🟢 but got {card.DcStatusText}/{card.DcStatusColor}/{card.DcStatusDot}");
        });
    }

    [Property(MaxTest = 10)]
    public Property UpdateAvailable_NotInstalling_ShowsUpdate()
    {
        var gen = from version in VersionGen
                  select version;

        return Prop.ForAll(gen.ToArbitrary(), version =>
        {
            var card = new GameCardViewModel
            {
                DcStatus = GameStatus.UpdateAvailable,
                DcIsInstalling = false,
                DcInstalledVersion = version
            };

            bool textOk = card.DcStatusText == "Update";
            bool colorOk = card.DcStatusColor == "#B898E8";
            bool dotOk = card.DcStatusDot == "🟢";

            return (textOk && colorOk && dotOk).Label(
                $"Expected Update/#B898E8/🟢 but got {card.DcStatusText}/{card.DcStatusColor}/{card.DcStatusDot}");
        });
    }

    [Property(MaxTest = 10)]
    public Property AnyStatus_Installing_ShowsInstallingAmber()
    {
        var gen = from status in GameStatusGen
                  from version in VersionGen
                  select (status, version);

        return Prop.ForAll(gen.ToArbitrary(), tuple =>
        {
            var card = new GameCardViewModel
            {
                DcStatus = tuple.status,
                DcIsInstalling = true,
                DcInstalledVersion = tuple.version
            };

            bool textOk = card.DcStatusText == "Installing…";
            bool colorOk = card.DcStatusColor == "#D4A856";

            return (textOk && colorOk).Label(
                $"Expected Installing…/#D4A856 but got {card.DcStatusText}/{card.DcStatusColor}");
        });
    }

    [Property(MaxTest = 10)]
    public Property LumaMode_DcRowVisible()
    {
        var gen = from status in GameStatusGen
                  from isInstalling in Arb.Default.Bool().Generator
                  from version in VersionGen
                  select (status, isInstalling, version);

        return Prop.ForAll(gen.ToArbitrary(), tuple =>
        {
            var card = new GameCardViewModel
            {
                DcStatus = tuple.status,
                DcIsInstalling = tuple.isInstalling,
                DcInstalledVersion = tuple.version,
                IsLumaMode = true,
                LumaFeatureEnabled = true
            };

            bool visible = card.DcRowVisibility == Visibility.Visible;

            return visible.Label(
                $"DcRowVisibility should be Visible in Luma mode but got {card.DcRowVisibility}");
        });
    }
}
