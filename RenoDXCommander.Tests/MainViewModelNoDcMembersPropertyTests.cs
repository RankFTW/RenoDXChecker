using System.Reflection;
using FsCheck;
using FsCheck.Xunit;
using RenoDXCommander.ViewModels;

namespace RenoDXCommander.Tests;

/// <summary>
/// Property-based test verifying that MainViewModel has no legacy Display Commander members.
/// Feature: dc-removal, Property 3: MainViewModel has no legacy DC members
/// **Validates: Requirements 6.1, 6.2, 6.3, 6.4**
///
/// NOTE: Updated for display-commander-reintegration spec. DC is now reintegrated
/// as a LITE addon (not the legacy mode-based system). The following members are
/// intentionally back: InstallDcAsync, UninstallDc, UpdateAllDcAsync, etc.
/// This test now only checks that the LEGACY DC mode members remain absent.
/// </summary>
public class MainViewModelNoDcMembersPropertyTests
{
    /// <summary>
    /// Legacy DC member names that must NOT exist on MainViewModel.
    /// These are from the old DC mode-based system that was removed in dc-removal.
    /// The new DC LITE reintegration uses different members (InstallDcAsync, etc.)
    /// which are intentionally present and NOT listed here.
    /// </summary>
    private static readonly string[] LegacyDcMemberNames =
    [
        // ── Legacy DC mode properties (Requirement 6.1) ──────────────────────
        "DcModeEnabled",
        "DcDllFileName",
        "DcLegacyMode",
        "DcLegacySettingsVisibility",
        "DcLegacyHiddenVisibility",
        "DcDllPickerVisibility",

        // ── Legacy DC commands (Requirement 6.2) ─────────────────────────────
        "InstallDcCommand",
        "UninstallDcCommand",

        // ── Legacy DC mode methods (Requirement 6.3) ─────────────────────────
        "ApplyDcModeSwitch",
        "ApplyDcModeSwitchForCard",
        "ResolveEffectiveDcMode",
        "OnDcModeEnabledChanged",
        "OnDcLegacyModeChanged",
        "OnDcDllFileNameChanged",

        // ── Legacy per-game DC accessors (Requirement 6.4) ───────────────────
        "GetPerGameDcModeOverride",
        "SetPerGameDcModeOverride",
        "GetDcCustomDllFileName",
        "SetDcCustomDllFileName",
    ];

    private static readonly Gen<string> GenLegacyDcMemberName =
        Gen.Elements(LegacyDcMemberNames);

    // ── Property 3: MainViewModel has no legacy DC members ────────────────────────
    // Feature: dc-removal, Property 3: MainViewModel has no legacy DC members
    // **Validates: Requirements 6.1, 6.2, 6.3, 6.4**
    [Property(MaxTest = 100)]
    public Property MainViewModel_Has_No_DC_Members()
    {
        var type = typeof(MainViewModel);
        const BindingFlags allMembers =
            BindingFlags.Public | BindingFlags.NonPublic |
            BindingFlags.Instance | BindingFlags.Static |
            BindingFlags.DeclaredOnly;

        return Prop.ForAll(
            Arb.From(GenLegacyDcMemberName),
            (string memberName) =>
            {
                var members = type.GetMember(memberName, allMembers);
                return (members.Length == 0)
                    .Label($"Legacy DC member '{memberName}' still exists on MainViewModel");
            });
    }
}
