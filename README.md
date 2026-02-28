# RenoDX Commander (RDXC) v1.2.9

An unofficial companion app for [RenoDX](https://github.com/clshortfuse/renodx) HDR modding on Windows. RDXC manages **ReShade**, **Display Commander**, and **RenoDX mods** across your entire game library from a single interface — no manual file juggling required.

> **Disclaimer:** RDXC is an unofficial third-party tool, not affiliated with or endorsed by the RenoDX project, Crosire, pmnoxx, or the Luma Framework. ReShade 6.7.3 is bundled under its BSD 3-Clause licence. Display Commander, RenoDX mods, and Luma Framework mods are downloaded from their official GitHub sources at runtime.

> **⚠ Single-player only:** RDXC installs ReShade with full addon support, which may be flagged by anti-cheat systems in online or multiplayer games. Do not use RDXC-installed ReShade in games with active anti-cheat. Uninstall ReShade from any game before playing online.

---

## Quick Start

1. **Run RDXC** — it automatically detects games from Steam, GOG, Epic, EA App, and Xbox / Game Pass on every launch.
2. **Find your game** using the search bar or filter tabs.
3. **Install ReShade** — top row button on any game card. Bundled with the app, no download needed.
4. **Install Display Commander** — middle row. Downloaded from GitHub on first install, cached locally after.
5. **Install RenoDX** — bottom row (supported games only). Downloaded from GitHub.
6. **Launch the game**, press **Home** to open ReShade, go to the **Add-ons** tab, and configure RenoDX.

---

## Requirements

- Windows 10 or 11 (x64)
- [.NET 8 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/8.0)

---

## Game Detection

RDXC re-scans all stores on every launch and merges newly installed games into its cached library automatically. New games just appear — no need to delete cache files.

| Store | Detection Method |
|-------|-----------------|
| **Steam** | Reads `libraryfolders.vdf` and `appmanifest_*.acf` files across all library folders |
| **GOG** | Registry keys under `HKLM\SOFTWARE\GOG.com\Games` |
| **Epic Games** | Manifest `.item` files in `ProgramData\Epic\EpicGamesLauncher\Data\Manifests` |
| **EA App** | `installerdata.xml` manifests, registry keys (`Origin Games`, `EA Games`, `Criterion Games`, `Respawn`, `BioWare`, `DICE`, `PopCap`, `Ghost Games`), default EA Games folders, and EA Desktop local config path discovery |
| **Xbox / Game Pass** | Windows `PackageManager` API — identifies games by `MicrosoftGame.config` presence. Falls back to `.GamingRoot` file parsing, registry, and folder scanning |

Games on a disconnected drive are preserved in the cache until the drive is reconnected.

### Adding Games Manually

Games not automatically detected can be added two ways:

- **➕ Add Game** button — enter the game name and pick the install folder.
- **Drag and drop** — drag a game's `.exe` file directly onto the RDXC window. RDXC automatically detects the engine type (Unreal, Unity, or Unknown), infers the game root folder by recognising store markers (Steam, GOG, Epic, EA, Xbox) and engine layouts (`Binaries\Win64`, `UnityPlayer.dll`, etc.), and guesses the game name from folder structure and exe name. A confirmation dialog shows all detected info and lets you edit the name before adding. Duplicate detection prevents adding a game that already exists.

### Drag-and-Drop Addon Install

Dragging a `.addon64` or `.addon32` file onto the RDXC window opens an install dialog. A game picker lets you choose which game to install the addon to — RDXC attempts to auto-select a matching game based on words in the addon filename. A confirmation dialog shows the addon filename, target game, and install path. If a RenoDX addon already exists in the game folder, the dialog warns that it will be replaced. On confirm, the existing addon is removed and the new one is copied in. Display Commander addon files are never touched.

---

## Game Cards

Each detected game gets a card with up to three rows:

| Row | Component | Controls |
|-----|-----------|----------|
| **Top** | ReShade | ⬇ Install / ↺ Reinstall / ⬆ Update — 📋 Copy INI — 🗑 Uninstall |
| **Middle** | Display Commander | ⬇ Install / ↺ Reinstall / ⬆ Update — 📋 Copy TOML — 🗑 Uninstall |
| **Bottom** | RenoDX Mod | ⬇ Install / ↺ Reinstall / ⬆ Update — 🌐 Links — ⚡ UE-Extended — 🗑 Uninstall |

Additional controls on each card:

| Button | Function |
|--------|----------|
| **⭐** | Toggle favourite — favourited games appear in the Favourites tab |
| **📁** | Open or change the game's install folder |
| **🎯** | Per-game overrides — hover each toggle for details |
| **ℹ** | Game info — wiki status, game-specific notes, and common warnings |
| **💬** | Link to the wiki discussion thread |
| **🚫** | Hide or unhide the game |

The engine type badge (Unreal, Unity, or Generic) and store source (Steam, GOG, Epic, EA App, Xbox) appear on each card.

---

## DC Mode

The **⚙ DC Mode** toggle in the header controls how ReShade and Display Commander files are named on install:

| Mode | ReShade Installed As | DC Installed As |
|------|---------------------|----------------|
| **OFF** (default) | `dxgi.dll` | `zzz_display_commander.addon64` |
| **ON** | `ReShade64.dll` | `dxgi.dll` |

Switching modes and reinstalling automatically removes the old file and places the correctly named one. Individual games can be excluded via **🎯 Overrides → Exclude from global DC Mode**.

### Why DC Mode is Recommended

DC Mode (loading Display Commander as `dxgi.dll`) is the preferred method of running Display Commander. As explained by pmnoxx (Display Commander's developer), loading DC as a ReShade addon causes it to hook too late, which prevents several features from working correctly. See the full [Display Commander feature list](https://github.com/pmnoxx/display-commander?tab=readme-ov-file#features) for details. The following issues are resolved by running DC as `dxgi.dll` via DC Mode:

1. **Streamline / DLSS integration** — In many games, it is not possible to swap DLSS files or view/control DLSS and DLSS-FG settings (such as render and quality profiles) when DC loads as an addon, because it hooks too late.
2. **FPS limiter** — In games without native Reflex, hooking directly into D3D11/D3D12 does not work when DC is an addon, resulting in extra latency.
3. **VSync control** — Toggling VSync on/off does not currently work when a RenoDX addon is also loaded, due to a ReShade limitation. A fix will be possible when DC runs as `dxgi.dll`.
4. **Flip swapchain upgrade** — Many games crash when upgrading the swapchain to flip mode. This will be fixable in the future when DC loads as `dxgi.dll`.
5. **ASI loader functionality** — DC supports loading DLL files as `.asi`. Some addons will not work if DC is not loaded as `dxgi.dll`, because they are loaded too late.
6. **General addon load order** — DLL load order is game-dependent and unpredictable. Some DC functionality may break when loading as an addon, which is impossible to diagnose due to the variable load order.

---

## Foreign dxgi.dll Protection

When installing ReShade or DC as `dxgi.dll`, RDXC checks whether an existing `dxgi.dll` belongs to another tool (DXVK, Special K, ENB, etc.) using binary signature scanning. If the file cannot be positively identified as ReShade or Display Commander, a confirmation dialog asks whether to overwrite it. During Update All, unidentified foreign files are silently skipped.

---

## UE-Extended & Native HDR

Unreal Engine games with native HDR support benefit from the UE-Extended addon instead of the generic UE plugin. The following games are automatically assigned UE-Extended and cannot be switched to the generic addon:

Avowed · Lies of P · Lost Soul Aside · Hell is Us · Mafia: The Old Country · Returnal · Marvel's Midnight Suns · Mortal Kombat 1 · Alone in the Dark · Still Wakes the Deep

These cards display **"Extended UE Native HDR"** as their engine label. The ℹ info popup for these games includes a note that in-game HDR must be turned on for UE-Extended to work correctly. Other Generic UE cards can be manually toggled to UE-Extended via the **⚡** button.

---

## INI Presets

RDXC bundles a default `reshade.ini` that is seeded into the inis folder on first launch. If you already have a customised file there, it is never overwritten. If deleted, the bundled default is re-seeded on next launch.

When ReShade is installed to a game folder, the bundled `reshade.ini` is also automatically deployed alongside the DLL — but only if a `reshade.ini` does not already exist in that game folder. This gives ReShade sensible defaults (disabled Generic Depth and Effect Runtime Sync addons, Home key overlay) on first launch without overwriting any existing user customisations. The deployed ini is left in place if ReShade is later uninstalled.

Config files in `%LOCALAPPDATA%\RenoDXCommander\inis\`:

| File | Copied When |
|------|-------------|
| `reshade.ini` | Automatically on ReShade install (if absent), or manually via 📋 on the ReShade row |
| `DisplayCommander.toml` | You click 📋 on the Display Commander row of any game card |

The 📋 button is greyed out when the corresponding file is absent and becomes active once the file exists.

---

## Shader Packs

RDXC downloads and maintains 7 HDR-compatible ReShade shader packs on every launch. All packs are merged into a shared staging folder and deployed per-game when you install ReShade or Display Commander.

### Included Packs

| Pack | Author |
|------|--------|
| [ReShade HDR Shaders](https://github.com/EndlesslyFlowering/ReShade_HDR_shaders) | EndlesslyFlowering (Lilium) |
| [PumboAutoHDR](https://github.com/Filoppi/PumboAutoHDR) | Filoppi (Pumbo) |
| [smolbbsoop shaders](https://github.com/smolbbsoop/smolbbsoopshaders) | smolbbsoop |
| [Reshade Simple HDR Shaders](https://github.com/MaxG2D/ReshadeSimpleHDRShaders) | MaxG2D |
| [reshade-shaders](https://github.com/clshortfuse/reshade-shaders) | clshortfuse |
| [potatoFX](https://github.com/CreepySasquatch/potatoFX) | CreepySasquatch |
| [reshade-shaders (slim)](https://github.com/crosire/reshade-shaders/tree/slim) | crosire |

### Deploy Modes

The **🎨 Shaders** button in the header cycles through four modes:

| Mode | Behaviour |
|------|-----------|
| **Off** | No shaders deployed. Manage your own manually. |
| **Minimum** (default) | Only the Lilium HDR Shaders pack is deployed. |
| **All** | All 7 packs are deployed. |
| **User** | Only files from your custom folder are deployed — no auto-downloaded packs. |

Custom shaders go in `%LOCALAPPDATA%\RenoDXCommander\reshade\Custom\Shaders\` and `\Textures\`.

Clicking **↻ Refresh** re-evaluates the current mode against all installed games, adding missing files and removing files from packs no longer selected. The status bar shows shader deployment progress.

### Deploy Destinations

| Scenario | Destination |
|----------|-------------|
| DC Mode ON | `%LOCALAPPDATA%\Programs\Display_Commander\Reshade\Shaders\` and `\Textures\` |
| DC Mode OFF (no DC installed) | `<game folder>\reshade-shaders\Shaders\` and `\Textures\` |

---

## Luma Framework (Experimental)

> **⚠ Experimental feature — not fully supported.** Luma integration is provided as-is. RDXC is not affiliated with or endorsed by the Luma Framework project. If you encounter issues with a Luma mod in-game, report them to the [Luma Framework GitHub](https://github.com/Filoppi/Luma-Framework) or the HDR Den Discord, not to RDXC.

[Luma Framework](https://github.com/Filoppi/Luma-Framework) by Pumbo (Filoppi) is a DX11 modding framework built on the ReShade addon system. It adds HDR support, improved rendering, and other graphics enhancements to supported games. RDXC can detect Luma-compatible games and manage mod installation.

### Enabling Luma

Luma UI is **hidden by default**. To enable it: **About → Settings → Luma (Experimental)**. This reveals Luma toggle badges on compatible game cards and adds a **Luma** filter tab to the header bar.

### How Luma Mode Works

Activating the Luma badge on a game card puts it into **Luma mode**:

- RenoDX, ReShade, and Display Commander are **automatically uninstalled** and their install rows are hidden. The only available action is **Install Luma**.
- Installing Luma downloads and extracts the mod's zip to the game folder, and also deploys the bundled `reshade.ini` and the Lilium HDR shader pack. Everything the game needs is self-contained.
- Uninstalling Luma or toggling Luma mode off removes **all** installed files: mod files, reshade.ini, and the shader pack folder.
- The **ℹ** info popup shows Luma-specific notes (mod status, author, and feature notes from the wiki).
- The **🎯 Overrides** dialog disables "Exclude from wiki" and "32-bit mode" while Luma mode is active.

Luma mode state is saved per-game and persists across app restarts. Luma mod data is fetched at runtime from the [Luma wiki](https://github.com/Filoppi/Luma-Framework/wiki/Mods-List) — nothing is hardcoded.

---

## Per-Game Overrides

Click **🎯** on any game card to access overrides. Hover each control for a description of what it does.

| Override | Effect |
|----------|--------|
| **Game name** | Editable — rename the game and the change persists across Refresh and app restarts |
| **Exclude from wiki** | Use a Discord link instead of install — ignore wiki matches |
| **Exclude from DC Mode** | Always use standard file naming for this game |
| **Exclude from Update All** | Skip this game during bulk update operations |
| **32-bit mode** | Install 32-bit ReShade, DC, and Unity addon for this game |
| **Shader mode** | Dropdown: **Global** (follow header toggle), **Off** (no shaders), **Minimum** (Lilium only), **All** (all packs), **User** (custom folder only). Overrides the global shader setting for this game only. Note: per-game shader mode only applies when DC Mode is OFF. When DC Mode is ON, all DC-mode games share the DC global shader folder. |
| **DLL naming override** | Override the filenames ReShade and Display Commander are installed as. When enabled, existing RS/DC installs are removed and the game is automatically excluded from DC Mode, Update All, and global shaders. Two text boxes set the ReShade and DC filenames side by side. Works in both normal and 32-bit mode. When toggled off, the custom-named files are removed. |

The dialog also includes wiki name mapping fields for manually matching a game to a different wiki entry.

---

## Update All

Three **Update All** buttons in the header update ReShade, Display Commander, and RenoDX mods across all eligible games in one click. Games excluded via overrides or with unidentified foreign `dxgi.dll` files are automatically skipped.

Update availability is indicated by a purple tint on the install/update buttons. RDXC compares stored file sizes against the remote source and only flags genuine changes. UE-Extended addons use download-based comparison for reliable detection.

---

## Auto-Update

On every launch, RDXC silently checks for new versions at the [GitHub release page](https://github.com/RankFTW/RenoDXChecker/releases/tag/RDXC). If a newer version is found, a dialog offers **Update Now** (downloads the installer, runs it, and closes RDXC) or **Later** (dismisses and continues normally). If the check fails, it is silently ignored.

To disable this check entirely, go to **About → Settings** and toggle **Skip update check on launch**. RDXC will no longer query GitHub for updates until the toggle is switched back off.

The Settings section also includes:

| Setting | Effect |
|---------|--------|
| **Luma (Experimental)** | When enabled, shows Luma toggle badges on game cards, adds a Luma filter tab, and allows installing Luma Framework mods. Disabled by default. |

---

## Filter Tabs

| Tab | Shows |
|-----|-------|
| **⭐ Favourites** | Games you've starred as favourites (includes hidden favourites) |
| **All Games** | All auto-detected and manually added games |
| **Installed** | Games with at least one component installed |
| **Not Installed** | Games with no components installed |
| **Unity** | Unity engine games only |
| **Unreal** | Unreal Engine games only |
| **Other** | Games with unknown engine type |
| **Luma** | Games with Luma Framework mods available (visible only when Luma is enabled in Settings) |
| **Hidden** | Games you've hidden with 🚫 |

The search bar filters within whichever tab is active.

---

## Data Storage

Everything is stored under `%LOCALAPPDATA%\RenoDXCommander\`:

| Path | Contents |
|------|---------|
| `game_library.json` | Detected games, hidden list, manually added games |
| `installed.json` | RenoDX mod install records (path, size, version) |
| `aux_installed.json` | ReShade and DC install records |
| `settings.json` | Name mappings, game renames, exclusions, UE-Extended toggles, DC Mode, Luma, per-game overrides |
| `downloads\` | Cached downloads |
| `inis\` | Preset config files (`reshade.ini`, `DisplayCommander.toml`) |
| `reshade\` | Staged shader packs and custom shaders |
| `logs\` | Crash reports with timestamps |

The download cache means reinstalling skips the download. All cached data can be cleared from About → Open Downloads Cache.

---

## Troubleshooting

**Game not detected?**
Click ➕ Add Game or drag the game's .exe onto the RDXC window. For wiki mod matching, use 🎯 to set a custom wiki name mapping.

**Xbox games not appearing?**
RDXC uses the Windows PackageManager API. Games should appear automatically on launch. If not, click ↻ Refresh.

**ReShade not loading in-game?**
The `dxgi.dll` file must be in the same folder as the game's main executable. For Unreal Engine games this is typically `Binaries\Win64` or `Binaries\WinGDK`. Check with 📁 that the install path is correct.

**Black screen in Unreal games?**
Open ReShade (Home key) → Add-ons → RenoDX → set `R10G10B10A2_UNORM` to `output size`.

**UE-Extended not working?**
Ensure in-game HDR is turned ON. UE-Extended requires the game's native HDR output to function correctly.

**Downloads failing?**
Click ↻ Refresh. If the problem persists, clear the cache from About → 📦 Open Downloads Cache.

**Wrong install path?**
Click 📁 on the game card to change it. Some games (e.g. Cyberpunk 2077) have automatic path overrides.

**Foreign dxgi.dll blocking install?**
RDXC detected a file from another mod (DXVK, Special K, ENB, etc.). Choose **Overwrite** in the confirmation dialog or cancel to keep the existing file.

---

## Third-Party Components

| Component | Author | Licence |
|-----------|--------|---------|
| [ReShade](https://reshade.me) | Crosire | [BSD 3-Clause](https://github.com/crosire/reshade/blob/main/LICENSE.md) |
| [Display Commander](https://github.com/pmnoxx/display-commander) | pmnoxx | Source-available |
| [RenoDX](https://github.com/clshortfuse/renodx) | clshortfuse & contributors | [MIT](https://github.com/clshortfuse/renodx/blob/main/LICENSE) |
| [Luma Framework](https://github.com/Filoppi/Luma-Framework) | Pumbo (Filoppi) | Source-available |
| [HtmlAgilityPack](https://github.com/zzzprojects/html-agility-pack) | ZZZ Projects Inc. | [MIT](https://github.com/zzzprojects/html-agility-pack/blob/master/LICENSE) |
| [CommunityToolkit.Mvvm](https://github.com/CommunityToolkit/dotnet) | Microsoft / .NET Foundation | [MIT](https://github.com/CommunityToolkit/dotnet/blob/main/License.md) |
| [SharpCompress](https://github.com/adamhathcock/sharpcompress) | Adam Hathcock | [MIT](https://github.com/adamhathcock/sharpcompress/blob/master/LICENSE.txt) |

ReShade 6.7.3 (`ReShade64.dll` / `ReShade32.dll`) is bundled and redistributed under the BSD 3-Clause licence. All shader packs, Luma Framework mods, and other components are downloaded from their official GitHub repositories at runtime and are not redistributed by RDXC.

---

## Links

- [RenoDX GitHub](https://github.com/clshortfuse/renodx) — HDR mod framework by clshortfuse
- [RenoDX Mod Wiki](https://github.com/clshortfuse/renodx/wiki/Mods) — per-game mod list and compatibility
- [ReShade](https://reshade.me) — post-processing injection framework by Crosire
- [Display Commander](https://github.com/pmnoxx/display-commander) — display management addon by pmnoxx
- [Display Commander Features](https://github.com/pmnoxx/display-commander?tab=readme-ov-file#features) — full feature list
- [Luma Framework](https://github.com/Filoppi/Luma-Framework) — DX11 modding framework by Pumbo (Filoppi)
- [Luma Mods List](https://github.com/Filoppi/Luma-Framework/wiki/Mods-List) — supported games and status
- [Creepy's HDR Guides](https://www.hdrmods.com) — setup guides and shader info
- [RenoDX Discord](https://discord.gg/gF4GRJWZ2A)
- [HDR Den Discord](https://discord.gg/k3cDruEQ)
- [HDR Den / Luma Wiki](https://github.com/Filoppi/Luma-Framework/wiki/Mods-List)
- [RDXC Support Channel](https://discordapp.com/channels/1296187754979528747/1475173660686815374)
- [The Ultra Place / Ultra+ Discord](https://discord.gg/pQtPYcdE)
- [RDXC GitHub](https://github.com/RankFTW/RenoDXChecker)
