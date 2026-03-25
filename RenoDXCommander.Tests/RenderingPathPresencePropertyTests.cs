using FsCheck;
using FsCheck.Xunit;
using RenoDXCommander.ViewModels;

namespace RenoDXCommander.Tests;

/// <summary>
/// Property-based tests for rendering path ComboBox conditional presence.
/// </summary>
public class RenderingPathPresencePropertyTests
{
    // ── Property 9 ────────────────────────────────────────────────────────────────
    // Feature: override-menu-redesign, Property 9: Rendering Path ComboBox presence iff dual-API
    // **Validates: Requirements 5.1, 5.2**

    [Property(MaxTest = 100)]
    public Property ShowRenderingPathToggle_EqualsIsDualApiGame(bool isDualApi)
    {
        var card = new GameCardViewModel { IsDualApiGame = isDualApi };

        return (card.ShowRenderingPathToggle == isDualApi)
            .Label($"IsDualApiGame={isDualApi}, ShowRenderingPathToggle={card.ShowRenderingPathToggle}");
    }
}
