# Requirements Document

## Introduction

This feature enhances the DC Mode user interface in RenoDXCommander (RDXC) across three areas: (1) improving the component status display so DC Mode shows a green "Installed" indicator when Display Commander is actually installed for a game, and removing the emoji from the greyed-out ReShade install button in DC mode; (2) splitting the DC Mode and Global Shaders sections in the override window into a left/right layout for visual clarity; and (3) adding a new "DC Mode Custom" option to the per-game DC Mode dropdown with a companion DLL filename selector for per-game DC custom DLL naming.

## Glossary

- **RDXC**: RenoDXCommander — the WinUI 3 desktop application that manages ReShade, Display Commander, and RenoDX mod installations for games.
- **Component_Table**: The "Components" section in the detail panel that shows per-component status rows for ReShade, Display Commander, and RenoDX.
- **Install_Flyout**: The flyout panel that opens from the card's primary action button, showing per-component install rows with status text, action buttons, and uninstall controls.
- **Override_Panel**: The per-game overrides panel (built by `DetailPanelBuilder.BuildOverridesPanel`) that contains game name editing, DC Mode selection, shader mode, DLL naming overrides, and update inclusion toggles.
- **DC_Mode_Combo**: The per-game DC Mode dropdown (`ComboBox`) in the Override_Panel that selects between Global, Exclude (Off), DC Mode 1, and DC Mode 2.
- **DLL_Override_Section**: The existing DLL naming override section in the Override_Panel that contains a toggle switch and two editable ComboBoxes for ReShade and DC filenames, populated from `DllOverrideConstants.CommonDllNames`.
- **RsStatusText**: The computed property on `GameCardViewModel.ReShade.cs` that returns the short status label for the ReShade component row (currently returns "DC Mode" when `RsBlockedByDcMode` is true).
- **RsShortAction**: The computed property that returns the short action label for the ReShade install button in the Install_Flyout (currently returns "🚫" when `RsBlockedByDcMode` is true).
- **RsActionLabel**: The computed property that returns the full action label for the ReShade install button in the Component_Table (currently returns "🚫  DC Mode — ReShade managed globally" when `RsBlockedByDcMode` is true).
- **GameCardViewModel**: The view model representing a single game card, split across partial class files for ReShade, Display Commander, RenoDX, Luma, and UI concerns.
- **DllOverrideConstants**: The static class providing `CommonDllNames` — the list of common DLL filenames used to populate DLL naming override dropdowns.

## Requirements

### Requirement 1: DC Installed Indicator in Component Status

**User Story:** As a user, I want the ReShade status text in the Component_Table and Install_Flyout to show a green "Installed" indicator when DC Mode is active and Display Commander is actually installed for that game, so that I can clearly see DC is working rather than just seeing a generic "DC Mode" label.

#### Acceptance Criteria

1. WHILE DC Mode is active for a game (`RsBlockedByDcMode` is true) AND Display Commander is installed (`IsDcInstalled` is true), THE RsStatusText SHALL display "Installed" with the green color `#5ECB7D`.
2. WHILE DC Mode is active for a game (`RsBlockedByDcMode` is true) AND Display Commander is not installed (`IsDcInstalled` is false), THE RsStatusText SHALL display "DC Mode" with the muted color `#6B7A8E`.
3. WHEN the DC install status changes for a game while DC Mode is active, THE Component_Table and Install_Flyout SHALL update the ReShade status text and color to reflect the new DC install state without requiring a manual refresh.

### Requirement 2: Remove Emoji from Greyed-Out ReShade Button in DC Mode

**User Story:** As a user, I want the ReShade install button to show clean text without emoji icons when it is greyed out in DC Mode, so that the disabled state looks polished and not cluttered.

#### Acceptance Criteria

1. WHILE DC Mode is active for a game (`RsBlockedByDcMode` is true), THE RsShortAction SHALL display "DC Mode" as plain text without any emoji characters.
2. WHILE DC Mode is active for a game (`RsBlockedByDcMode` is true), THE RsActionLabel SHALL display "DC Mode — ReShade managed globally" as plain text without any emoji characters.
3. WHILE DC Mode is not active for a game, THE RsShortAction and RsActionLabel SHALL continue to display their current emoji-prefixed labels (e.g., "⬇ Install", "⬆ Update", "↺ Reinstall").

### Requirement 3: Split DC Mode and Shader Sections in Override Panel

**User Story:** As a user, I want the DC Mode controls and Global Shaders controls in the Override_Panel to be visually separated into a left column and right column within the same section, so that I can distinguish between DC mode settings and shader settings at a glance.

#### Acceptance Criteria

1. THE Override_Panel SHALL display the DC_Mode_Combo on the left side and the Global Shaders toggle on the right side of a two-column layout within the same row.
2. THE Override_Panel SHALL display a vertical divider between the DC Mode column and the Global Shaders column to visually separate the two areas.
3. THE Override_Panel SHALL place the "Select Shaders" button below the Global Shaders toggle on the right side, aligned within the shader column.
4. WHEN the Override_Panel is resized, THE two-column layout SHALL maintain equal column widths and the vertical divider SHALL remain centered between the columns.

### Requirement 4: Add "DC Mode Custom" Option to Per-Game DC Mode Dropdown

**User Story:** As a user, I want a new "DC Mode Custom" option in the per-game DC Mode dropdown, so that I can assign a custom DLL filename specifically for Display Commander on a per-game basis without using the full DLL override system.

#### Acceptance Criteria

1. THE DC_Mode_Combo SHALL include a fifth option labeled "DC Mode Custom" after the existing "DC Mode 2" option.
2. WHEN the user selects "DC Mode Custom" in the DC_Mode_Combo, THE Override_Panel SHALL display a DLL filename selector ComboBox below the DC_Mode_Combo, within the DC Mode column.
3. WHEN the user selects any option other than "DC Mode Custom" in the DC_Mode_Combo, THE Override_Panel SHALL hide the DLL filename selector ComboBox.
4. THE DLL filename selector ComboBox SHALL be editable, allowing the user to type a custom DLL filename in addition to selecting from the dropdown list.
5. THE DLL filename selector ComboBox SHALL be populated with the same DLL names from `DllOverrideConstants.CommonDllNames` that are used in the DLL_Override_Section.
6. WHEN the user selects or types a DLL filename in the DC Mode Custom selector, THE Override_Panel SHALL persist the chosen filename immediately (auto-save on selection change or Enter key press).
7. WHEN the user selects "DC Mode Custom" and provides a DLL filename, THE system SHALL rename the installed Display Commander file to the chosen filename in the game folder.
8. IF the user selects "DC Mode Custom" without providing a DLL filename, THEN THE system SHALL use the default DC filename as a fallback.
9. WHEN the user switches away from "DC Mode Custom" to another DC Mode option, THE system SHALL rename the Display Commander file back to the standard filename for the selected mode.

### Requirement 5: Persistence of DC Mode Custom Settings

**User Story:** As a user, I want my DC Mode Custom DLL filename selections to be saved and restored when I reopen the Override_Panel or restart RDXC, so that I do not lose my per-game custom configurations.

#### Acceptance Criteria

1. WHEN the user sets a DC Mode Custom DLL filename for a game, THE system SHALL persist the filename to the settings store alongside the per-game DC mode override value.
2. WHEN the Override_Panel is opened for a game with a saved DC Mode Custom configuration, THE DC_Mode_Combo SHALL show "DC Mode Custom" selected and the DLL filename selector SHALL display the previously saved filename.
3. WHEN the user clicks "Reset Overrides" in the Override_Panel, THE system SHALL clear the DC Mode Custom DLL filename and reset the DC_Mode_Combo to "Global".
