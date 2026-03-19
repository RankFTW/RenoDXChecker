# Implementation Plan: DC Mode UI Enhancements

## Overview

Implement five DC Mode UI improvements in RenoDXCommander: (1) DC installed indicator in RS status, (2) emoji removal from DC Mode RS labels, (3) two-column override panel layout, (4) DC Mode Custom dropdown option with DLL filename selector, and (5) persistence of DC Mode Custom settings. Changes span `GameCardViewModel.ReShade.cs`, `GameCardViewModel.cs`, `GameCardViewModel.DisplayCommander.cs`, `DetailPanelBuilder.cs`, `MainViewModel.cs`, and settings serialization.

## Tasks

- [x] 1. Update ReShade computed properties for DC installed indicator and emoji removal
  - [x] 1.1 Modify `RsStatusText` in `GameCardViewModel.ReShade.cs` to return `"Installed"` when `RsBlockedByDcMode && IsDcInstalled`, and `"DC Mode"` when `RsBlockedByDcMode && !IsDcInstalled`
    - Update `RsStatusColor` to return `"#5ECB7D"` (green) when DC is installed under DC Mode, `"#6B7A8E"` (muted) otherwise
    - _Requirements: 1.1, 1.2_
  - [x] 1.2 Modify `RsShortAction` to return `"DC Mode"` (no emoji) when `RsBlockedByDcMode` is true
    - Modify `RsActionLabel` to return `"DC Mode — ReShade managed globally"` (no emoji) when `RsBlockedByDcMode` is true
    - Ensure non-DC-Mode labels retain their existing emoji prefixes (`⬇`, `⬆`, `↺`)
    - _Requirements: 2.1, 2.2, 2.3_
  - [x] 1.3 Update `OnRsBlockedByDcModeChanged` in `GameCardViewModel.ReShade.cs` to also fire `RsStatusText` and `RsStatusColor` notifications
    - Update `NotifyDcStatusDependents` in `GameCardViewModel.DisplayCommander.cs` to fire `RsStatusText` and `RsStatusColor` so the RS row updates when DC install state changes while DC Mode is active
    - _Requirements: 1.3_
  - [x] 1.4 Write property tests for RS status text under DC Mode (Properties 1, 2, 3)
    - **Property 1: RS status text reflects DC install state under DC Mode**
    - **Validates: Requirements 1.1, 1.2**
    - **Property 2: DC Mode RS labels contain no emoji**
    - **Validates: Requirements 2.1, 2.2**
    - **Property 3: Non-DC-Mode RS labels retain emoji prefixes**
    - **Validates: Requirements 2.3**

- [x] 2. Checkpoint — Verify RS status changes
  - Ensure all tests pass, ask the user if questions arise.

- [x] 3. Add `DcCustomDllFileName` observable property and extend DC Mode values
  - [x] 3.1 Add `[ObservableProperty] private string? _dcCustomDllFileName;` to `GameCardViewModel.cs`
    - This stores the per-game custom DLL filename when "DC Mode Custom" (value `3`) is selected
    - _Requirements: 4.1, 4.4, 4.5_
  - [x] 3.2 Add `DcCustomDllFileNames` dictionary to `MainViewModel` for persistence
    - Add `Dictionary<string, string> DcCustomDllFileNames` field
    - Add `GetDcCustomDllFileName(string gameName)` and `SetDcCustomDllFileName(string gameName, string fileName)` methods
    - Serialize/deserialize `DcCustomDllFileNames` in `SaveSettings`/`LoadSettings` alongside existing `PerGameDcModeOverrides`
    - _Requirements: 5.1_
  - [x] 3.3 Write property test for DC Custom DLL filename persistence round-trip
    - **Property 7: DC Custom DLL filename persistence round-trip**
    - **Validates: Requirements 4.6, 5.1**

- [x] 4. Refactor Override Panel to two-column layout with DC Mode Custom selector
  - [x] 4.1 Modify `BuildOverridesPanel` in `DetailPanelBuilder.cs` to use a two-column `Grid` for the DC Mode / Shader section
    - Left column: DC Mode combo + conditional DC Mode Custom DLL selector
    - Right column: Global Shaders toggle + Select Shaders button
    - Add a vertical `Border` divider between the two columns
    - Both columns should use `1*` width for equal sizing
    - _Requirements: 3.1, 3.2, 3.3, 3.4_
  - [x] 4.2 Add "DC Mode Custom" as the fifth option in the `dcModeCombo` items array
    - Update `dcModeOptions` to include `"DC Mode Custom"` at index 4
    - Update `SelectedIndex` mapping: index 4 maps to `PerGameDcMode = 3`
    - Update `SelectionChanged` handler to map index 4 → `newDcMode = 3`
    - _Requirements: 4.1_
  - [x] 4.3 Add the DC Mode Custom DLL filename selector below the DC Mode combo
    - Create an editable `ComboBox` populated with `DllOverrideConstants.CommonDllNames`
    - Show/hide based on `dcModeCombo.SelectedIndex == 4`
    - Auto-save on `SelectionChanged` and `KeyDown` (Enter) via `SetDcCustomDllFileName`
    - Pre-populate with saved filename when opening panel for a game with `PerGameDcMode == 3`
    - _Requirements: 4.2, 4.3, 4.4, 4.5, 4.6, 5.2_
  - [x] 4.4 Write property test for DC Mode Custom selector visibility
    - **Property 4: DC Mode Custom selector visibility matches selection**
    - **Validates: Requirements 4.2, 4.3**

- [x] 5. Checkpoint — Verify override panel layout and DC Mode Custom selector
  - Ensure all tests pass, ask the user if questions arise.

- [x] 6. Implement DC file rename logic for DC Mode Custom
  - [x] 6.1 Implement DC file rename when switching to DC Mode Custom
    - When `dcModeCombo` selection changes to index 4 and a DLL filename is provided, rename the installed DC file to the chosen filename using `File.Move` (following existing `DllOverrideService` patterns)
    - If no filename is provided, fall back to the default DC filename for the current mode
    - Log errors via `CrashReporter.Log` and show error in `DcActionMessage`
    - _Requirements: 4.7, 4.8_
  - [x] 6.2 Implement DC file rename when switching away from DC Mode Custom
    - When switching from DC Mode Custom (index 4) to another mode, rename the custom-named DC file back to the standard filename for the selected mode (dxgi.dll for Mode 1, winmm.dll for Mode 2)
    - Integrate with existing `ApplyDcModeSwitchForCard` logic in `MainViewModel`
    - _Requirements: 4.9_
  - [x] 6.3 Write unit tests for DC file rename on mode switch
    - Test rename to custom filename when switching to DC Mode Custom
    - Test rename back to standard filename when switching away
    - Test fallback when custom filename is empty
    - _Requirements: 4.7, 4.8, 4.9_

- [x] 7. Implement reset and restoration of DC Mode Custom settings
  - [x] 7.1 Update the "Reset" button handler in `BuildOverridesPanel` to clear `DcCustomDllFileName` and reset `PerGameDcMode` to null
    - Remove the saved custom DLL filename from `DcCustomDllFileNames` dictionary
    - Reset the DC Mode combo to index 0 ("Global")
    - _Requirements: 5.3_
  - [x] 7.2 Ensure panel open restores saved DC Mode Custom state
    - When `BuildOverridesPanel` is called for a game with `PerGameDcMode == 3`, set combo to index 4 and populate the DLL filename selector with the saved value from `DcCustomDllFileNames`
    - _Requirements: 5.2_

- [x] 8. Final checkpoint — Ensure all tests pass
  - Ensure all tests pass, ask the user if questions arise.

## Notes

- Tasks marked with `*` are optional and can be skipped for faster MVP
- Each task references specific requirements for traceability
- Checkpoints ensure incremental validation
- Property tests validate universal correctness properties from the design document
- The implementation language is C# (WinUI 3 / .NET 8) matching the existing codebase
