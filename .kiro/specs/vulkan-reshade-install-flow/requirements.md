# Requirements Document

## Introduction

When a game uses the Vulkan graphics API and the global Vulkan ReShade implicit layer is already registered in the Windows registry, the ReShade install button should offer a lightweight "Install Vulkan ReShade" action that only deploys the Vulkan INI file and footprint to the game folder — skipping the full layer installation. The full Vulkan layer install (DLL copy, manifest write, registry registration) should only occur when the layer is not yet detected. This streamlines the install experience for users who have already set up the Vulkan layer and just need to enable ReShade for additional games.

## Glossary

- **Commander**: The RenoDXCommander desktop application (WinUI 3 / .NET 8)
- **Vulkan_Layer**: The global Vulkan ReShade implicit layer registered in HKLM\SOFTWARE\Khronos\Vulkan\ImplicitLayers, consisting of a registry entry, ReShade64.json manifest, and ReShade64.dll in C:\ProgramData\ReShade\
- **Vulkan_Game**: A game where RequiresVulkanInstall is true (Vulkan-only or dual-API with VulkanRenderingPath set to "Vulkan")
- **VulkanLayerService**: The static service that checks layer installation status via IsLayerInstalled() and performs install/uninstall of the Vulkan_Layer
- **Vulkan_INI**: The reshade.vulkan.ini template merged into the game folder as reshade.ini, enabling ReShade to function via the Vulkan_Layer for that game
- **Vulkan_Footprint**: The RDXC_VULKAN_FOOTPRINT marker file created in the game directory to signal that Vulkan ReShade is active for that game
- **Install_Flyout**: The per-component install flyout panel shown when clicking the card's primary action button, containing individual RS/DC/RDX install rows
- **Detail_Panel**: The right-side detail panel shown when a game card is selected, containing component status and install buttons
- **RsShortAction**: The computed property on GameCardViewModel that provides the short label text for the ReShade install button in the Install_Flyout
- **RsActionLabel**: The computed property on GameCardViewModel that provides the full label text for the ReShade install button in the Detail_Panel

## Requirements

### Requirement 1: Conditional Button Label in Install Flyout

**User Story:** As a user with the Vulkan_Layer already installed, I want the ReShade install button in the Install_Flyout to say "Install Vulkan ReShade" instead of the generic install label, so that I understand the action will enable Vulkan ReShade for this game without reinstalling the layer.

#### Acceptance Criteria

1. WHILE the Vulkan_Layer is detected as installed via VulkanLayerService.IsLayerInstalled(), WHEN a Vulkan_Game card's Install_Flyout is displayed, THE Commander SHALL show "⬇ Vulkan RS" as the RsShortAction label on the ReShade row.
2. WHILE the Vulkan_Layer is not detected as installed, WHEN a Vulkan_Game card's Install_Flyout is displayed, THE Commander SHALL show "⬇ Install" as the RsShortAction label on the ReShade row.
3. WHILE the Vulkan_Layer is detected as installed AND Vulkan ReShade is already active for the game (reshade.ini exists in the game folder), WHEN a Vulkan_Game card's Install_Flyout is displayed, THE Commander SHALL show "↺ Reinstall" as the RsShortAction label on the ReShade row.

### Requirement 2: Conditional Button Label in Detail Panel

**User Story:** As a user with the Vulkan_Layer already installed, I want the ReShade install button in the Detail_Panel to say "Install Vulkan ReShade" instead of "Install Vulkan Layer", so that the label accurately reflects the lightweight action being performed.

#### Acceptance Criteria

1. WHILE the Vulkan_Layer is detected as installed AND Vulkan ReShade is not yet active for the game, WHEN a Vulkan_Game's Detail_Panel is displayed, THE Commander SHALL show "Install Vulkan ReShade" as the RsActionLabel on the ReShade install button.
2. WHILE the Vulkan_Layer is detected as installed AND Vulkan ReShade is already active for the game, WHEN a Vulkan_Game's Detail_Panel is displayed, THE Commander SHALL show "Reinstall Vulkan ReShade" as the RsActionLabel on the ReShade install button.
3. WHILE the Vulkan_Layer is not detected as installed, WHEN a Vulkan_Game's Detail_Panel is displayed, THE Commander SHALL show "Install Vulkan Layer" as the RsActionLabel on the ReShade install button.

### Requirement 3: Lightweight Vulkan ReShade Deploy When Layer Present

**User Story:** As a user with the Vulkan_Layer already installed, I want clicking the ReShade install button on a Vulkan_Game to only deploy the Vulkan_INI and Vulkan_Footprint to the game folder, so that the install is fast and does not require administrator privileges.

#### Acceptance Criteria

1. WHILE the Vulkan_Layer is detected as installed, WHEN the user clicks the ReShade install button for a Vulkan_Game, THE Commander SHALL merge the Vulkan_INI template into the game folder as reshade.ini.
2. WHILE the Vulkan_Layer is detected as installed, WHEN the user clicks the ReShade install button for a Vulkan_Game, THE Commander SHALL create the Vulkan_Footprint file in the game directory.
3. WHILE the Vulkan_Layer is detected as installed, WHEN the user clicks the ReShade install button for a Vulkan_Game, THE Commander SHALL deploy ReShadePreset.ini to the game folder if the preset file exists in the inis directory.
4. WHILE the Vulkan_Layer is detected as installed, WHEN the user clicks the ReShade install button for a Vulkan_Game, THE Commander SHALL deploy shaders locally to the game folder.
5. WHILE the Vulkan_Layer is detected as installed, WHEN the user clicks the ReShade install button for a Vulkan_Game, THE Commander SHALL NOT invoke VulkanLayerService.InstallLayer().
6. WHILE the Vulkan_Layer is detected as installed, WHEN the user clicks the ReShade install button for a Vulkan_Game, THE Commander SHALL NOT require administrator privileges.

### Requirement 4: Full Vulkan Layer Install When Layer Absent

**User Story:** As a user without the Vulkan_Layer installed, I want clicking the ReShade install button on a Vulkan_Game to perform the full layer installation (including DLL copy, manifest write, and registry registration), so that Vulkan ReShade is set up from scratch.

#### Acceptance Criteria

1. WHILE the Vulkan_Layer is not detected as installed, WHEN the user clicks the ReShade install button for a Vulkan_Game, THE Commander SHALL invoke VulkanLayerService.InstallLayer() to register the global Vulkan implicit layer.
2. WHILE the Vulkan_Layer is not detected as installed, WHEN the user clicks the ReShade install button for a Vulkan_Game, THE Commander SHALL require administrator privileges before proceeding.
3. WHILE the Vulkan_Layer is not detected as installed, WHEN the user clicks the ReShade install button for a Vulkan_Game, THE Commander SHALL merge the Vulkan_INI template, deploy ReShadePreset.ini, create the Vulkan_Footprint, and deploy shaders to the game folder after the layer is installed.

### Requirement 5: Card Status Update After Lightweight Deploy

**User Story:** As a user, I want the game card to reflect the correct ReShade status after a lightweight Vulkan ReShade deploy, so that I can see the install succeeded.

#### Acceptance Criteria

1. WHEN a lightweight Vulkan ReShade deploy completes successfully, THE Commander SHALL set the card's RsStatus to Installed.
2. WHEN a lightweight Vulkan ReShade deploy completes successfully, THE Commander SHALL read and display the installed ReShade version from the Vulkan_Layer directory.
3. WHEN a lightweight Vulkan ReShade deploy completes successfully, THE Commander SHALL display a success message indicating Vulkan ReShade was installed.
4. IF the lightweight Vulkan ReShade deploy fails due to an IO error, THEN THE Commander SHALL display an error message with the failure reason and leave the card status unchanged.
