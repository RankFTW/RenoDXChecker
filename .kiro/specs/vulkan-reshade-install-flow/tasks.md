# Implementation Plan: Vulkan ReShade Install Flow

## Overview

Implement a two-tier install path for Vulkan ReShade games. When the global Vulkan implicit layer is already registered, the install button performs a lightweight deploy (INI + footprint + shaders only). When absent, the existing full install flow runs. Update ViewModel computed properties and the detail panel to show Vulkan-aware labels.

## Tasks

- [x] 1. Update GameCardViewModel.ReShade.cs — Vulkan-aware computed properties
  - [x] 1.1 Add `IsVulkanRsActive` helper property and update `RsShortAction` with Vulkan-aware label logic
    - Add `private bool IsVulkanRsActive => RequiresVulkanInstall && File.Exists(Path.Combine(InstallPath, "reshade.ini"));`
    - Modify `RsShortAction`: when `RequiresVulkanInstall` is true, check `VulkanLayerService.IsLayerInstalled()` and `IsVulkanRsActive` to return `"↺ Reinstall"`, `"⬇ Vulkan RS"`, or `"⬇ Install"` per the design
    - Existing non-Vulkan logic remains unchanged
    - _Requirements: 1.1, 1.2, 1.3_

  - [x] 1.2 Update `RsActionLabel` with Vulkan-aware label logic
    - When `RequiresVulkanInstall` is true: check `VulkanLayerService.IsLayerInstalled()` and `IsVulkanRsActive` to return `"Install Vulkan ReShade"`, `"Reinstall Vulkan ReShade"`, or `"Install Vulkan Layer"` per the design
    - Existing non-Vulkan logic remains unchanged
    - _Requirements: 2.1, 2.2, 2.3_

  - [x] 1.3 Write property test for RsShortAction label correctness (Property 1)
    - **Property 1: RsShortAction label correctness for Vulkan games**
    - Generate random booleans for `layerInstalled` and `reshadeIniExists`
    - Use temp directories and control `reshade.ini` existence; mock `VulkanLayerService.IsLayerInstalled()` via testable overload
    - Assert correct label for each combination when `RequiresVulkanInstall` is true
    - Use `[Property(MaxTest = 10)]`
    - **Validates: Requirements 1.1, 1.2, 1.3**

  - [x] 1.4 Write property test for RsActionLabel label correctness (Property 2)
    - **Property 2: RsActionLabel label correctness for Vulkan games**
    - Generate random booleans for `layerInstalled` and `reshadeIniExists`
    - Assert correct label for each combination when `RequiresVulkanInstall` is true
    - Use `[Property(MaxTest = 10)]`
    - **Validates: Requirements 2.1, 2.2, 2.3**

- [x] 2. Checkpoint — Verify ViewModel label logic
  - Ensure all tests pass, ask the user if questions arise.

- [x] 3. Update MainViewModel.InstallReShadeVulkanAsync — Branched install logic
  - [x] 3.1 Add early `VulkanLayerService.IsLayerInstalled()` branch for lightweight deploy
    - At the top of `InstallReShadeVulkanAsync`, check `VulkanLayerService.IsLayerInstalled()`
    - If layer is present: skip admin check, skip warning dialog, skip `InstallLayer()` call
    - Lightweight path: call `MergeRsVulkanIni`, `CopyRsPresetIniIfPresent`, `VulkanFootprintService.Create`, `SyncGameFolder`, then read version from layer directory and update card status with `RsStatus = Installed` and success message starting with "✅"
    - If layer is absent: existing full install flow runs unchanged (admin check, warning, `InstallLayer()`, then same deploy steps)
    - _Requirements: 3.1, 3.2, 3.3, 3.4, 3.5, 3.6, 4.1, 4.2, 4.3, 5.1, 5.2, 5.3, 5.4_

  - [x] 3.2 Write property test for lightweight deploy file creation (Property 3)
    - **Property 3: Lightweight deploy creates all expected files**
    - Generate random booleans for `presetExists` and `vulkanIniTemplateExists`
    - Use temp directories, create/skip preset file, invoke lightweight deploy steps
    - Assert `reshade.ini` and `RDXC_VULKAN_FOOTPRINT` exist; `ReShadePreset.ini` exists iff preset was present
    - Use `[Property(MaxTest = 10)]`
    - **Validates: Requirements 3.1, 3.2, 3.3, 3.4, 4.3**

  - [x] 3.3 Write property test for InstallLayer invocation guard (Property 4)
    - **Property 4: InstallLayer invoked if and only if layer is absent**
    - Generate random boolean for `layerInstalled`
    - Use a spy/mock to track whether `InstallLayer()` is called
    - Assert `InstallLayer()` called iff `layerInstalled` is false
    - Use `[Property(MaxTest = 10)]`
    - **Validates: Requirements 3.5, 4.1**

  - [x] 3.4 Write property test for card status after successful deploy (Property 5)
    - **Property 5: Card status updated after successful deploy**
    - Generate random boolean for `layerInstalled` (lightweight vs full path)
    - After successful deploy, assert `card.RsStatus == GameStatus.Installed` and `card.RsActionMessage` starts with "✅"
    - Use `[Property(MaxTest = 10)]`
    - **Validates: Requirements 5.1, 5.3**

- [x] 4. Update DetailPanelBuilder — Consume ViewModel labels for Vulkan branch
  - [x] 4.1 Simplify Vulkan branch in `UpdateDetailComponentRows` to read `card.RsActionLabel`
    - Replace the inline label computation (`vulkanLayerInstalled ? "Reinstall Vulkan Layer" : "Install Vulkan Layer"`) with `card.RsActionLabel`
    - Status text and color logic for the Vulkan case remain in the detail panel builder (they depend on `reshade.ini` existence checked at render time)
    - _Requirements: 2.1, 2.2, 2.3_

- [x] 5. Checkpoint — Verify all changes integrate correctly
  - Ensure all tests pass, ask the user if questions arise.

## Notes

- Tasks marked with `*` are optional and can be skipped for faster MVP
- CardBuilder requires no changes — `BuildInstallFlyoutContent` already reads `card.RsShortAction` and the new Vulkan-aware labels flow through automatically
- Property tests use FsCheck with `MaxTest = 10` for fast test runs
- Each property test references a specific correctness property from the design document
