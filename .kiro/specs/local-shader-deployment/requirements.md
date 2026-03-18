# Requirements Document

## Introduction

RDXC (RenoDXCommander) currently deploys ReShade shaders to two locations: a global Display Commander AppData folder (`%LOCALAPPDATA%\Programs\Display_Commander\Reshade\`) and per-game local folders (`<GameDir>\reshade-shaders\`). The routing decision depends on whether DC mode is active. This feature eliminates the global DC shader path entirely. Shaders will always be deployed locally to each game's folder, regardless of DC mode. The DC AppData folder will continue to hold ReShade DLLs only. A one-time migration renames legacy global shader folders to prevent interference, and shader removal is restricted to ReShade uninstall only.

## Glossary

- **RDXC**: RenoDXCommander — the desktop application that manages ReShade, Display Commander, and shader packs for games.
- **ShaderPackService**: The service responsible for downloading, extracting, and deploying shader packs to staging, DC global, and game-local folders.
- **AuxInstallService**: The service that installs and manages Display Commander and ReShade DLLs for each game.
- **DC_AppData_Folder**: The Display Commander application data directory at `%LOCALAPPDATA%\Programs\Display_Commander\Reshade\`.
- **Game_Folder**: The installation directory of a specific game on the user's file system.
- **Reshade_Shaders_Folder**: The `reshade-shaders` directory inside a Game_Folder containing `Shaders\` and `Textures\` subdirectories.
- **Staging_Folder**: The RDXC local cache at `%LOCALAPPDATA%\RenoDXCommander\reshade\` where downloaded shader packs are extracted before deployment.
- **Managed_Marker**: The file `Managed by RDXC.txt` placed inside `reshade-shaders\` to indicate RDXC ownership of the folder.
- **DC_Mode**: A setting (levels 0, 1, 2) controlling how Display Commander DLLs are named and deployed. Level 0 means DC mode is off.
- **Deploy_Mode**: The shader selection mode (Off, Minimum, All, User, Select) controlling which shader packs are deployed.
- **Vulkan_ReShade**: ReShade installed as a global Vulkan implicit layer via `C:\ProgramData\ReShade\`, with `reshade.ini` placed in the game folder.

## Requirements

### Requirement 1: Eliminate Shader Deployment to DC AppData Folder

**User Story:** As a game modder, I want shaders to never be placed in the DC AppData folder, so that each game has its own independent shader set and global shaders do not interfere with local configurations.

#### Acceptance Criteria

1. THE ShaderPackService SHALL deploy shaders and textures exclusively to the Reshade_Shaders_Folder inside each Game_Folder.
2. THE ShaderPackService SHALL NOT copy shader files or texture files to the DC_AppData_Folder `Shaders\` or `Textures\` subdirectories.
3. THE AuxInstallService SHALL continue to copy ReShade DLL files (ReShade32.dll, ReShade64.dll) to the DC_AppData_Folder.
4. WHEN the `DeployToDcFolder` method is invoked, THE ShaderPackService SHALL perform no file operations on the DC_AppData_Folder shader or texture directories.
5. WHEN the `SyncDcFolder` method is invoked, THE ShaderPackService SHALL perform no file operations on the DC_AppData_Folder shader or texture directories.

### Requirement 2: One-Time Migration of Legacy Global Shaders

**User Story:** As a game modder, I want legacy global shaders in the DC AppData folder to be renamed on startup, so that Display Commander does not load stale global shaders that conflict with my local game shaders.

#### Acceptance Criteria

1. WHEN RDXC starts and a `Shaders` directory exists inside the DC_AppData_Folder, THE RDXC SHALL rename the `Shaders` directory to `Shaders.old`.
2. WHEN RDXC starts and a `Textures` directory exists inside the DC_AppData_Folder, THE RDXC SHALL rename the `Textures` directory to `Textures.old`.
3. WHEN RDXC starts and a `Shaders.old` directory already exists inside the DC_AppData_Folder, THE RDXC SHALL skip the rename of the `Shaders` directory.
4. WHEN RDXC starts and a `Textures.old` directory already exists inside the DC_AppData_Folder, THE RDXC SHALL skip the rename of the `Textures` directory.
5. IF the rename operation fails due to a file system error, THEN THE RDXC SHALL log the error and continue startup without crashing.

### Requirement 3: Local Shader Deployment to Game Folders

**User Story:** As a game modder, I want shaders deployed into each game's local `reshade-shaders` folder, so that each game has its own isolated shader configuration.

#### Acceptance Criteria

1. WHEN ReShade is installed to a Game_Folder, THE ShaderPackService SHALL deploy shaders to `<Game_Folder>\reshade-shaders\Shaders\` and textures to `<Game_Folder>\reshade-shaders\Textures\`.
2. WHEN a `reshade-shaders` folder already exists in the Game_Folder and the Managed_Marker is absent, THE ShaderPackService SHALL rename the existing folder to `reshade-shaders-original` before deploying.
3. WHEN a `reshade-shaders-original` folder already exists in the Game_Folder, THE ShaderPackService SHALL skip the rename and log a message.
4. THE ShaderPackService SHALL write the Managed_Marker file into the newly created Reshade_Shaders_Folder after deployment.
5. WHEN the Deploy_Mode is Off, THE ShaderPackService SHALL remove the RDXC-managed Reshade_Shaders_Folder and restore `reshade-shaders-original` if present.
6. THE ShaderPackService SHALL support all Deploy_Mode values (Off, Minimum, All, User, Select) when deploying to a Game_Folder.

### Requirement 4: Preserve Shaders During Display Commander Installation

**User Story:** As a game modder, I want my local shaders to remain intact when Display Commander is installed to a game, so that I do not lose my shader setup every time DC is installed or updated.

#### Acceptance Criteria

1. WHEN Display Commander is installed to a Game_Folder via `InstallDcAsync`, THE AuxInstallService SHALL NOT call `RemoveFromGameFolder` on the Reshade_Shaders_Folder.
2. WHEN Display Commander is installed to a Game_Folder via `InstallDcAsync`, THE AuxInstallService SHALL NOT call `SyncDcFolder`.
3. WHEN Display Commander is installed to a Game_Folder, THE AuxInstallService SHALL deploy shaders to the local Reshade_Shaders_Folder using `SyncGameFolder`.
4. WHEN Display Commander is updated in a Game_Folder, THE AuxInstallService SHALL preserve the existing Reshade_Shaders_Folder contents.

### Requirement 5: DC Mode Switching Does Not Affect Shaders

**User Story:** As a game modder, I want switching DC mode levels to leave my shaders untouched, so that changing DC configuration does not disrupt my shader setup.

#### Acceptance Criteria

1. WHEN the global DC_Mode level is changed (via settings or the override menu), THE RDXC SHALL NOT move, remove, or redeploy shader files in any Game_Folder.
2. WHEN a per-game DC_Mode override is changed, THE RDXC SHALL NOT move, remove, or redeploy shader files in that Game_Folder.
3. WHEN `ApplyDcModeSwitch` is executed, THE MainViewModel SHALL NOT invoke any ShaderPackService deployment or removal methods.
4. WHEN `ApplyDcModeSwitchForCard` is executed for a single game, THE MainViewModel SHALL NOT invoke any ShaderPackService deployment or removal methods.

### Requirement 6: Shader Removal Only on ReShade Uninstall

**User Story:** As a game modder, I want shaders removed from a game folder only when I explicitly uninstall ReShade from that game, so that shaders persist through all other operations.

#### Acceptance Criteria

1. WHEN ReShade is uninstalled from a Game_Folder via RDXC, THE ShaderPackService SHALL remove the RDXC-managed Reshade_Shaders_Folder.
2. WHEN ReShade is uninstalled and a `reshade-shaders-original` folder exists, THE ShaderPackService SHALL restore the original folder by renaming it back to `reshade-shaders`.
3. THE ShaderPackService SHALL NOT remove the Reshade_Shaders_Folder during any operation other than ReShade uninstall.
4. WHEN the Reshade_Shaders_Folder lacks the Managed_Marker, THE ShaderPackService SHALL NOT delete the folder during ReShade uninstall but SHALL rename it to `reshade-shaders-original`.

### Requirement 7: Vulkan ReShade Follows Local Shader Rules

**User Story:** As a game modder using Vulkan games, I want shaders deployed locally to the game folder when Vulkan ReShade is active, so that Vulkan games follow the same local-only shader rules as DirectX games.

#### Acceptance Criteria

1. WHEN Vulkan ReShade is installed for a game (via `reshade.ini` placed in the Game_Folder), THE ShaderPackService SHALL deploy shaders to the local Reshade_Shaders_Folder.
2. WHEN Vulkan ReShade is active for a game, THE ShaderPackService SHALL NOT deploy shaders to the DC_AppData_Folder.
3. WHEN Vulkan ReShade is uninstalled from a game, THE ShaderPackService SHALL remove the RDXC-managed Reshade_Shaders_Folder following the same rules as Requirement 6.
4. WHEN Vulkan ReShade is active and DC is installed to the same game, THE AuxInstallService SHALL preserve the local Reshade_Shaders_Folder.

### Requirement 8: SyncShadersToAllLocations Routes All Games Locally

**User Story:** As a game modder, I want the global shader sync (triggered on Refresh) to deploy shaders locally to every game, so that all games consistently use local shaders regardless of DC installation status.

#### Acceptance Criteria

1. WHEN `SyncShadersToAllLocations` is called, THE ShaderPackService SHALL call `SyncGameFolder` for every game that has ReShade installed, regardless of whether DC is also installed.
2. WHEN `SyncShadersToAllLocations` is called, THE ShaderPackService SHALL NOT call `SyncDcFolder`.
3. WHEN `SyncShadersToAllLocations` is called, THE ShaderPackService SHALL NOT call `RemoveFromGameFolder` for games that have DC installed.
4. WHEN `SyncShadersToAllLocations` is called for a game with DC installed and ReShade installed, THE ShaderPackService SHALL deploy shaders to the local Reshade_Shaders_Folder of that game.

### Requirement 9: ReShade Install Always Deploys Shaders Locally

**User Story:** As a game modder, I want ReShade installation to always deploy shaders to the game folder, so that shader deployment is consistent regardless of DC mode or DC installation status.

#### Acceptance Criteria

1. WHEN `InstallReShadeAsync` is called, THE AuxInstallService SHALL call `SyncGameFolder` to deploy shaders to the Game_Folder.
2. WHEN `InstallReShadeAsync` is called with dcMode=true, THE AuxInstallService SHALL still deploy shaders to the local Reshade_Shaders_Folder.
3. WHEN `InstallReShadeAsync` is called with dcIsInstalled=true, THE AuxInstallService SHALL still deploy shaders to the local Reshade_Shaders_Folder.
4. WHEN `InstallReShadeAsync` is called, THE AuxInstallService SHALL NOT call `SyncDcFolder`.
