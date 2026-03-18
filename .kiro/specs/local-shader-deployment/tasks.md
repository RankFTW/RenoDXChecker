# Implementation Plan: Local Shader Deployment

## Overview

Eliminate the dual-path shader deployment model. All shader deployment routes through `SyncGameFolder` to per-game local folders. DC AppData folder methods become no-ops. A one-time migration renames legacy DC shaders. Shader removal is restricted to ReShade uninstall only. All changes are in C#.

## Tasks

- [x] 1. Make DC folder shader methods no-ops and add migration
  - [x] 1.1 Convert `DeployToDcFolder` to a no-op in `ShaderPackService.cs`
    - Replace the method body with an early return and a log message: `CrashReporter.Log("[ShaderPackService.DeployToDcFolder] No-op — local-only shader deployment")`
    - Do not remove the method signature from `IShaderPackService.cs` (backward compat)
    - _Requirements: 1.1, 1.2, 1.4_

  - [x] 1.2 Convert `SyncDcFolder` to a no-op in `ShaderPackService.cs`
    - Replace the method body with an early return and a log message: `CrashReporter.Log("[ShaderPackService.SyncDcFolder] No-op — local-only shader deployment")`
    - Do not remove the method signature from `IShaderPackService.cs` (backward compat)
    - _Requirements: 1.1, 1.2, 1.5_

  - [x] 1.3 Add `MigrateLegacyDcShaders()` static method to `ShaderPackService.cs`
    - Add a new public static method that renames `Shaders` → `Shaders.old` and `Textures` → `Textures.old` inside the DC AppData folder (`DcReshadeDir`)
    - Skip rename if `.old` target already exists
    - Wrap each rename in try/catch, log errors via `CrashReporter.Log`, never throw
    - _Requirements: 2.1, 2.2, 2.3, 2.4, 2.5_

  - [x] 1.4 Write unit tests for `MigrateLegacyDcShaders`
    - Test: renames `Shaders` to `Shaders.old` when `Shaders` exists and `Shaders.old` does not
    - Test: renames `Textures` to `Textures.old` when `Textures` exists and `Textures.old` does not
    - Test: skips rename when `.old` already exists
    - Test: does not crash when directories are absent
    - _Requirements: 2.1, 2.2, 2.3, 2.4, 2.5_

- [x] 2. Rewrite `SyncShadersToAllLocations` in `ShaderPackService.cs`
  - [x] 2.1 Remove DC folder routing from `SyncShadersToAllLocations`
    - Remove the `if (loc.dcInstalled)` branch that calls `RemoveFromGameFolder` + `RestoreOriginalIfPresent` + `SyncDcFolder`
    - Remove the `dcSynced` flag and all DC folder sync logic
    - Remove the trailing `if (!dcSynced && globalMode == DeployMode.Off ...)` block
    - For every location where `rsInstalled` is true (regardless of `dcInstalled`), call `SyncGameFolder(loc.installPath, effectiveMode, selectedPackIds)`
    - _Requirements: 8.1, 8.2, 8.3, 8.4_

  - [x] 2.2 Write property test: RS-installed games always get `SyncGameFolder`
    - For any game location tuple where `rsInstalled=true`, regardless of `dcInstalled` or `dcMode`, verify `SyncGameFolder` is called with the game's install path
    - _Requirements: 8.1, 8.4_

  - [x] 2.3 Write property test: `SyncDcFolder` is never called
    - For any set of game locations passed to `SyncShadersToAllLocations`, verify `SyncDcFolder` is never invoked
    - _Requirements: 8.2_

- [x] 3. Checkpoint
  - Ensure all tests pass, ask the user if questions arise.

- [x] 4. Fix `InstallDcAsync` shader section in `AuxInstallService.cs`
  - [x] 4.1 Remove DC shader routing from `InstallDcAsync`
    - Remove the call to `_shaderPackService.RemoveFromGameFolder(installPath)` (if present in the shader deployment section)
    - Remove the call to `_shaderPackService.SyncDcFolder(ShaderPackService.CurrentMode)`
    - Replace with `_shaderPackService.SyncGameFolder(installPath, effectiveShaderMode)` to deploy shaders locally
    - Ensure the `effectiveShaderMode` resolution logic is preserved
    - _Requirements: 4.1, 4.2, 4.3_

  - [x] 4.2 Write unit test for `InstallDcAsync` shader preservation
    - Verify that after `InstallDcAsync`, the game folder's `reshade-shaders` directory exists with shaders deployed
    - Verify `SyncDcFolder` is not called
    - _Requirements: 4.1, 4.2, 4.3_

- [x] 5. Fix `InstallReShadeAsync` shader section in `AuxInstallService.cs`
  - [x] 5.1 Remove conditional DC shader routing from `InstallReShadeAsync`
    - Remove the `if (dcMode || dcIsInstalled) _shaderPackService.SyncDcFolder(...)` branch
    - Remove the `else if (!dcIsInstalled)` guard
    - Always call `_shaderPackService.SyncGameFolder(installPath, effectiveShaderMode)` regardless of `dcMode` or `dcIsInstalled`
    - _Requirements: 9.1, 9.2, 9.3, 9.4_

  - [x] 5.2 Write property test: `InstallReShadeAsync` always deploys locally
    - For any combination of `dcMode` (true/false) and `dcIsInstalled` (true/false), verify `SyncGameFolder` is called and `SyncDcFolder` is not called
    - _Requirements: 9.1, 9.2, 9.3, 9.4_

- [x] 6. Fix `MainViewModel` shader deployment methods
  - [x] 6.1 Fix `DeployAllShaders` in `MainViewModel.cs`
    - Remove the `if (dcInstalled)` branch that calls `SyncDcFolder`
    - Remove the `dcSynced` flag
    - Change the `else if (rsInstalled)` to just `if (rsInstalled)` — for every game with RS installed (regardless of DC status), call `SyncGameFolder`
    - _Requirements: 1.1, 1.2, 8.1_

  - [x] 6.2 Fix `DeployShadersForCard` in `MainViewModel.cs`
    - Remove the `if (dcInstalled)` branch that calls `SyncDcFolder`
    - Change the `else if (rsInstalled)` to just `if (rsInstalled)` — always call `SyncGameFolder` when RS is installed
    - _Requirements: 1.1, 1.2_

  - [x] 6.3 Fix `InitializeAsync` shader sync section in `MainViewModel.cs`
    - Remove the `if (dcInstalled && dcMode)` branch that calls `SyncDcFolder`
    - Remove the `dcSynced` flag
    - Change the `else if (rsInstalled || ...)` to just `if (rsInstalled)` — for every game with RS installed, call `SyncGameFolder`
    - _Requirements: 1.1, 1.2, 8.1_

- [x] 7. Ensure DC mode switching does not affect shaders
  - [x] 7.1 Review and fix `ApplyDcModeSwitch` in `MainViewModel.cs`
    - Verify the method does NOT call any `ShaderPackService` methods (no `SyncDcFolder`, `SyncGameFolder`, `RemoveFromGameFolder`, `DeployToDcFolder`)
    - The `_auxInstaller.Uninstall(card.RsRecord)` call when DC mode is active uninstalls the ReShade DLL — verify this does NOT trigger shader removal (the `Uninstall` method should only handle DLL files, not shader folders)
    - If `Uninstall` calls `RestoreOriginalIfPresent` which touches shaders, that call path needs to be reviewed and potentially guarded
    - _Requirements: 5.1, 5.3_

  - [x] 7.2 Review and fix `ApplyDcModeSwitchForCard` in `MainViewModel.cs`
    - Same review as 7.1 but for the single-card variant
    - Ensure no shader deployment or removal methods are called
    - _Requirements: 5.2, 5.4_

  - [x] 7.3 Write unit test: DC mode switch does not touch shaders
    - Mock `IShaderPackService` and verify that after `ApplyDcModeSwitch`, none of the shader methods were called
    - _Requirements: 5.1, 5.2, 5.3, 5.4_

- [x] 8. Fix `UninstallReShade` shader removal guard in `MainViewModel.cs`
  - [x] 8.1 Remove the DC-installed guard from `UninstallReShade`
    - Remove the `if (card.DcStatus != GameStatus.Installed && card.DcStatus != GameStatus.UpdateAvailable)` guard around `RemoveFromGameFolder`
    - Always call `_shaderPackService.RemoveFromGameFolder(card.InstallPath)` when uninstalling ReShade, regardless of DC status
    - _Requirements: 6.1, 6.2, 6.3_

  - [x] 8.2 Write unit test: shader removal always happens on RS uninstall
    - Verify `RemoveFromGameFolder` is called even when DC is installed
    - _Requirements: 6.1, 6.3_

- [x] 9. Checkpoint
  - Ensure all tests pass, ask the user if questions arise.

- [x] 10. Call migration on startup and update interface
  - [x] 10.1 Add `MigrateLegacyDcShaders()` call to RDXC startup
    - In `App.xaml.cs` `OnLaunched` (or early in `MainViewModel.InitializeAsync`), call `ShaderPackService.MigrateLegacyDcShaders()` before any shader sync operations
    - This should run once per startup, early in the initialization sequence
    - _Requirements: 2.1, 2.2_

  - [x] 10.2 Update `IShaderPackService.cs` interface if needed
    - `DeployToDcFolder` and `SyncDcFolder` remain in the interface (implementations are now no-ops)
    - If `MigrateLegacyDcShaders` is static on `ShaderPackService`, no interface change is needed
    - If it needs to be instance-based, add it to `IShaderPackService`
    - _Requirements: 1.4, 1.5, 2.1_

- [x] 11. Update existing tests to validate new routing logic
  - [x] 11.1 Update `DcShaderRoutingFixVerificationTests.cs`
    - These tests validate the OLD routing logic where `dcInstalled → SyncDcFolder`
    - Rewrite to verify the NEW logic: all games with RS installed → `SyncGameFolder`, never `SyncDcFolder`
    - Update the `FixedRoutingDecision` model to reflect the new always-local routing
    - _Requirements: 8.1, 8.2_

  - [x] 11.2 Update `DcShaderRoutingPreservationTests.cs`
    - Update preservation tests to verify the new routing model is preserved (always local)
    - _Requirements: 8.1, 8.2_

  - [x] 11.3 Update `DcShaderRoutingBugConditionTests.cs`
    - Update or remove bug condition tests that validated the old DC routing bug
    - The bug condition (dcInstalled && !dcMode → no shaders) no longer applies
    - _Requirements: 8.1, 8.3_

  - [x] 11.4 Update `ShaderPackServicePropertyTests.cs`
    - Update tests for `SyncShadersToAllLocations` that expect DC folder sync behavior
    - Tests should now verify: RS-installed games always get `SyncGameFolder`, DC folder is never synced
    - _Requirements: 8.1, 8.2, 8.3, 8.4_

  - [x] 11.5 Update `TestHelpers.cs` mock `IShaderPackService`
    - Update the mock to track calls to `SyncGameFolder` and `SyncDcFolder` for assertion in tests
    - Add call-tracking fields (e.g., `SyncGameFolderCalls`, `SyncDcFolderCalls` lists) so tests can verify routing
    - _Requirements: 8.1, 8.2_

- [x] 12. Final checkpoint
  - Ensure all tests pass, ask the user if questions arise.

## Notes

- Tasks marked with `*` are optional and can be skipped for faster MVP
- Each task references specific requirements for traceability
- The project is C# (WinUI desktop app) — all code changes are in the `RenoDXCommander` and `RenoDXCommander.Tests` projects
- `DeployToDcFolder` and `SyncDcFolder` remain in the interface for backward compatibility but do nothing
- `MigrateLegacyDcShaders` is a static method — no DI wiring needed
