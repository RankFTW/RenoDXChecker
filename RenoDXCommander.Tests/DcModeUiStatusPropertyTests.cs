using FsCheck;
using FsCheck.Xunit;
using RenoDXCommander.Models;
using RenoDXCommander.ViewModels;

namespace RenoDXCommander.Tests;

/// <summary>
/// Property-based tests for the dc-mode-ui-enhancements feature.
/// Uses FsCheck with xUnit. Each property runs a minimum of 10 iterations.
/// </summary>
public class DcModeUiStatusPropertyTests
{
    // ── Generators ────────────────────────────────────────────────────────────────

    private static readonly Gen<GameStatus> GenStatus = Gen.Elements(
        GameStatus.NotInstalled,
        GameStatus.Available,
        GameStatus.Installed,
        GameStatus.UpdateAvailable);

    // ── Property 1: RS status text reflects DC install state under DC Mode ──
    // Feature: dc-mode-ui-enhancements, Property 1: RS status text reflects DC install state under DC Mode
    // **Validates: Requirements 1.1, 1.2**
    [Property(MaxTest = 10)]
    public Property RsStatusText_ReflectsDcInstallState_UnderDcMode()
    {
        var genState = from dcStatus in GenStatus
                       from rsStatus in GenStatus
                       from rsIsInstalling in Arb.Default.Bool().Generator
                       select (dcStatus, rsStatus, rsIsInstalling);

        return Prop.ForAll(
            Arb.From(genState),
            (tuple) =>
            {
                var (dcStatus, rsStatus, rsIsInstalling) = tuple;

                var card = new GameCardViewModel
                {
                    RsBlockedByDcMode = true,
                    DcStatus = dcStatus,
                    RsStatus = rsStatus,
                    RsIsInstalling = rsIsInstalling,
                };

                bool isDcInstalled = dcStatus is GameStatus.Installed or GameStatus.UpdateAvailable;

                if (isDcInstalled)
                {
                    bool textOk = card.RsStatusText == "Installed";
                    bool colorOk = card.RsStatusColor == "#5ECB7D";
                    return textOk && colorOk;
                }
                else
                {
                    bool textOk = card.RsStatusText == "DC Mode";
                    bool colorOk = card.RsStatusColor == "#6B7A8E";
                    return textOk && colorOk;
                }
            });
    }

    // ── Property 2: DC Mode RS labels contain no emoji ──
    // Feature: dc-mode-ui-enhancements, Property 2: DC Mode RS labels contain no emoji
    // **Validates: Requirements 2.1, 2.2**
    [Property(MaxTest = 10)]
    public Property DcModeRsLabels_ContainNoEmoji()
    {
        var genState = from dcStatus in GenStatus
                       from rsStatus in GenStatus
                       from rsIsInstalling in Arb.Default.Bool().Generator
                       select (dcStatus, rsStatus, rsIsInstalling);

        return Prop.ForAll(
            Arb.From(genState),
            (tuple) =>
            {
                var (dcStatus, rsStatus, rsIsInstalling) = tuple;

                var card = new GameCardViewModel
                {
                    RsBlockedByDcMode = true,
                    DcStatus = dcStatus,
                    RsStatus = rsStatus,
                    RsIsInstalling = rsIsInstalling,
                };

                bool shortActionOk = card.RsShortAction == "DC Mode";
                bool actionLabelOk = card.RsActionLabel == "DC Mode — ReShade managed globally";

                // Verify no emoji characters in either label
                bool shortActionNoEmoji = !ContainsEmoji(card.RsShortAction);
                bool actionLabelNoEmoji = !ContainsEmoji(card.RsActionLabel);

                return shortActionOk && actionLabelOk && shortActionNoEmoji && actionLabelNoEmoji;
            });
    }

    // ── Property 3: Non-DC-Mode RS labels retain emoji prefixes ──
    // Feature: dc-mode-ui-enhancements, Property 3: Non-DC-Mode RS labels retain emoji prefixes
    // **Validates: Requirements 2.3**
    [Property(MaxTest = 10)]
    public Property NonDcModeRsLabels_RetainEmojiPrefixes()
    {
        var genState = from rsStatus in GenStatus
                       select rsStatus;

        return Prop.ForAll(
            Arb.From(genState),
            (rsStatus) =>
            {
                var card = new GameCardViewModel
                {
                    RsBlockedByDcMode = false,
                    RsIsInstalling = false,
                    RsStatus = rsStatus,
                };

                string[] emojiPrefixes = ["⬇", "⬆", "↺"];

                bool shortActionStartsWithEmoji = emojiPrefixes.Any(e => card.RsShortAction.StartsWith(e));
                bool actionLabelStartsWithEmoji = emojiPrefixes.Any(e => card.RsActionLabel.StartsWith(e));

                return shortActionStartsWithEmoji && actionLabelStartsWithEmoji;
            });
    }

    // ── Helper ────────────────────────────────────────────────────────────────────

    private static bool ContainsEmoji(string text)
    {
        foreach (var rune in text.EnumerateRunes())
        {
            // Check for common emoji ranges
            if (rune.Value >= 0x1F600 && rune.Value <= 0x1F64F) return true; // Emoticons
            if (rune.Value >= 0x1F300 && rune.Value <= 0x1F5FF) return true; // Misc Symbols
            if (rune.Value >= 0x1F680 && rune.Value <= 0x1F6FF) return true; // Transport
            if (rune.Value >= 0x1F900 && rune.Value <= 0x1F9FF) return true; // Supplemental
            if (rune.Value >= 0x2600 && rune.Value <= 0x26FF) return true;   // Misc symbols
            if (rune.Value >= 0x2700 && rune.Value <= 0x27BF) return true;   // Dingbats
            if (rune.Value >= 0x2B50 && rune.Value <= 0x2B55) return true;   // Stars
            if (rune.Value == 0x2B06 || rune.Value == 0x2B07) return true;   // Arrows
            if (rune.Value >= 0x2190 && rune.Value <= 0x21FF) return true;   // Arrows block
            if (rune.Value == 0x2B05 || rune.Value == 0x2B06 || rune.Value == 0x2B07) return true;
            if (rune.Value == 0x1F6AB) return true; // 🚫
            if (rune.Value >= 0x2B00 && rune.Value <= 0x2BFF) return true;   // Misc symbols and arrows
            if (rune.Value >= 0x2300 && rune.Value <= 0x23FF) return true;   // Misc technical
            if (rune.Value >= 0xFE00 && rune.Value <= 0xFE0F) return true;   // Variation selectors
        }
        return false;
    }
}
