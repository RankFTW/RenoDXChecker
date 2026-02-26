# RenoDX Commander (RDXC) v1.2.2

An unofficial companion app for [RenoDX](https://github.com/clshortfuse/renodx) HDR modding on Windows. RDXC manages **ReShade**, **Display Commander**, and **RenoDX mods** across your entire game library from a single interface — no manual file juggling required.

> **Disclaimer:** RDXC is an unofficial third-party tool, not affiliated with or endorsed by the RenoDX project, Crosire, or pmnoxx. ReShade 6.7.2 is bundled under its BSD 3-Clause licence. Display Commander and RenoDX mods are downloaded from their official GitHub sources at runtime.

---

## Quick Start

1. **Run RDXC** — it automatically detects games from Steam, GOG, Epic, EA App, and Xbox / Game Pass on every launch.
2. **Find your game** using the search bar or filter tabs. If it's not detected, click **➕ Add Game**.
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

RDXC re-scans all stores on every launch and merges newly installed games into its cached library automatically. You never need to delete cache files — new games just appear.

| Store | Detection Method |
|-------|-----------------|
| **Steam** | Reads `libraryfolders.vdf` and `appmanifest_*.acf` files across all library folders |
| **GOG** | Registry keys under `HKLM\SOFTWARE\GOG.com\Games` |
| **Epic Games** | Manifest `.item` files in `ProgramData\Epic\EpicGamesLauncher\Data\Manifests` |
| **EA App** | `installerdata.xml` files in `ProgramData\EA\content` |
| **Xbox / Game Pass** | Windows `PackageManager` API — identifies games by `MicrosoftGame.config` presence. Falls back to `.GamingRoot` file parsing, registry, and folder scanning |

Games on a disconnected drive are preserved in the cache until the drive is reconnected. Games not automatically detected can be added manually via **➕ Add Game**.

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
| **📁** | Open or change the game's install folder |
| **🎯** | Per-game overrides (name mapping, exclusions, 32-bit mode) |
| **ℹ** | Game-specific setup notes from the RenoDX wiki |
| **💬** | Link to the wiki discussion thread |
| **🚫** | Hide or unhide the game |

The engine type badge (Unreal, Unity, or Generic) appears on each card alongside the store source (Steam, GOG, Epic, EA App, Xbox).

---

## DC Mode

The **⚙ DC Mode** toggle in the header controls how ReShade and Display Commander files are named on install:

| Mode | ReShade Installed As | DC Installed As |
|------|---------------------|----------------|
| **OFF** (default) | `dxgi.dll` | `zzz_display_commander.addon64` |
| **ON** | `ReShade64.dll` | `dxgi.dll` |

Switching modes and reinstalling automatically removes the old file and places the correctly named one.

Individual games can be excluded from the global toggle via **🎯 Overrides → Exclude from global DC Mode**. Excluded games always use standard naming regardless of the global setting.

---

## Foreign dxgi.dll Protection

When installing ReShade or DC as `dxgi.dll`, RDXC checks whether an existing `dxgi.dll` belongs to another tool (DXVK, Special K, ENB, etc.) using binary signature scanning. If the file cannot be positively identified as ReShade or Display Commander, a confirmation dialog asks whether to overwrite it. During Update All, unidentified foreign files are silently skipped to avoid breaking other mods.

---

## UE-Extended & Native HDR

Unreal Engine games with native HDR support benefit from the UE-Extended addon instead of the generic UE plugin. The following games are automatically assigned UE-Extended and cannot be switched to the generic addon:

Avowed · Lies of P · Lost Soul Aside · Hell is Us · Mafia: The Old Country · Returnal · Marvel's Midnight Suns · Mortal Kombat 1 · Alone in the Dark · Still Wakes the Deep

These cards display **"Extended UE Native HDR"** as their engine label. Other Generic UE cards can be manually toggled to UE-Extended via the **⚡** button.

---

## INI Presets

RDXC bundles a default `reshade.ini` that is seeded into the inis folder on first launch. If you already have a customised `reshade.ini` there, it is never overwritten. If you delete it, the bundled default is re-seeded on next launch.

Config files in `%LOCALAPPDATA%\RenoDXCommander\inis\`:

| File | Copied When |
|------|-------------|
| `reshade.ini` | You click 📋 on the ReShade row of any game card |
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

Clicking **↻ Refresh** re-evaluates the current mode against all installed games, adding missing files and removing files from packs no longer selected.

### Deploy Destinations

| Scenario | Destination |
|----------|-------------|
| DC Mode ON | `%LOCALAPPDATA%\Programs\Display_Commander\Reshade\Shaders\` and `\Textures\` |
| DC Mode OFF (no DC installed) | `<game folder>\reshade-shaders\Shaders\` and `\Textures\` |

When DC is installed to a game that already has a local `reshade-shaders\` folder, the local folder is removed because ReShade will use the global DC path instead.

---

## Per-Game Overrides

Click **🎯** on any game card to access:

| Override | Effect |
|----------|--------|
| **Wiki name mapping** | Map a detected game name to a different wiki entry for correct mod matching |
| **Exclude from wiki** | Use only the generic engine mod — ignore wiki matches |
| **Exclude from DC Mode** | Always use standard file naming for this game |
| **Exclude from Update All** | Skip this game during bulk update operations |
| **Exclude from shader management** | RDXC will not deploy or remove shaders for this game |
| **32-bit mode** | Install 32-bit ReShade, DC, and Unity addon for this game |
| **Path override** | Displayed when the install path has been manually changed via 📁 |

---

## Update All

Three **Update All** buttons in the header update ReShade, Display Commander, and RenoDX mods across all eligible games in one click. Games excluded via overrides or with unidentified foreign `dxgi.dll` files are automatically skipped.

Update availability is indicated by a purple tint on the install/update buttons. RDXC compares stored file sizes against the remote source and only flags genuine changes.

---

## Auto-Update

On every launch, RDXC silently checks for new versions at the [GitHub release page](https://github.com/RankFTW/RenoDXChecker/releases/tag/RDXC). If a newer version is found, a dialog offers **Update Now** (downloads the installer, runs it, and closes RDXC) or **Later** (dismisses and continues normally). If the check fails (no internet, GitHub unreachable), it is silently ignored.

---

## Filter Tabs

| Tab | Shows |
|-----|-------|
| **Detected** | All auto-detected and manually added games |
| **Installed** | Games with at least one component installed |
| **Not Installed** | Games with no components installed |
| **Unity** | Unity engine games only |
| **Unreal** | Unreal Engine games only |
| **Other** | Games with unknown engine type |
| **Hidden** | Games you've hidden with 🚫 |

The search bar filters within whichever tab is active.

---

## Download Cache

All downloaded files are cached in `%LOCALAPPDATA%\RenoDXCommander\downloads\`. Reinstalling any component reuses the cached copy instead of re-downloading. The cache can be opened and cleared from **About → 📦 Open Downloads Cache**.

---

## Data Storage

Everything is stored under `%LOCALAPPDATA%\RenoDXCommander\`:

| Path | Contents |
|------|---------|
| `game_library.json` | Detected games, hidden list, manually added games |
| `installed.json` | RenoDX mod install records (path, size, version) |
| `aux_installed.json` | ReShade and DC install records |
| `settings.json` | Name mappings, exclusions, UE-Extended toggles, DC Mode, per-game overrides |
| `downloads\` | Cached downloads |
| `inis\` | Preset config files (`reshade.ini`, `DisplayCommander.toml`) |
| `reshade\` | Staged shader packs and custom shaders |
| `logs\` | Crash reports with timestamps |

---

## Troubleshooting

**Game not detected?**
Click ➕ Add Game to add it manually. For wiki mod matching on a manually added game, use 🎯 to set a custom wiki name mapping.

**Xbox games not appearing?**
RDXC uses the Windows PackageManager API. Games should appear automatically on launch. If not, try clicking ↻ Refresh in the header.

**ReShade not loading in-game?**
The `dxgi.dll` file must be in the same folder as the game's main executable. For Unreal Engine games this is typically `Binaries\Win64` or `Binaries\WinGDK`. Check with 📁 that the install path is correct.

**Black screen in Unreal games?**
Open ReShade (Home key) → Add-ons → RenoDX → set `R10G10B10A2_UNORM` to `output size`.

**Downloads failing?**
Click ↻ Refresh. If the problem persists, clear the cache from About → 📦 Open Downloads Cache and try again.

**Wrong install path?**
Click 📁 on the game card to change it. Some games (e.g. Cyberpunk 2077) have automatic path overrides to their correct executable directory.

**Foreign dxgi.dll blocking install?**
RDXC detected a file from another mod (DXVK, Special K, ENB, etc.). Choose **Overwrite** in the confirmation dialog to replace it, or cancel to keep the existing file.

---

## Third-Party Components

| Component | Author | Licence |
|-----------|--------|---------|
| [ReShade](https://reshade.me) | Crosire | [BSD 3-Clause](https://github.com/crosire/reshade/blob/main/LICENSE.md) |
| [Display Commander](https://github.com/pmnoxx/display-commander) | pmnoxx | Source-available |
| [RenoDX](https://github.com/clshortfuse/renodx) | clshortfuse & contributors | [MIT](https://github.com/clshortfuse/renodx/blob/main/LICENSE) |
| [HtmlAgilityPack](https://github.com/zzzprojects/html-agility-pack) | ZZZ Projects Inc. | [MIT](https://github.com/zzzprojects/html-agility-pack/blob/master/LICENSE) |
| [CommunityToolkit.Mvvm](https://github.com/CommunityToolkit/dotnet) | Microsoft / .NET Foundation | [MIT](https://github.com/CommunityToolkit/dotnet/blob/main/License.md) |
| [SharpCompress](https://github.com/adamhathcock/sharpcompress) | Adam Hathcock | [MIT](https://github.com/adamhathcock/sharpcompress/blob/master/LICENSE.txt) |

ReShade 6.7.2 (`ReShade64.dll` / `ReShade32.dll`) is bundled and redistributed under the BSD 3-Clause licence. All shader packs and other components are downloaded from their official GitHub repositories at runtime and are not redistributed by RDXC.

---

## Links

- [RenoDX GitHub](https://github.com/clshortfuse/renodx) — HDR mod framework by clshortfuse
- [RenoDX Mod Wiki](https://github.com/clshortfuse/renodx/wiki/Mods) — per-game mod list and compatibility
- [ReShade](https://reshade.me) — post-processing injection framework by Crosire
- [Display Commander](https://github.com/pmnoxx/display-commander) — display management addon by pmnoxx
- [Creepy's HDR Guides](https://www.hdrmods.com) — setup guides and shader info
- [RenoDX Discord](https://discord.gg/gF4GRJWZ2A)
- [RDXC Support Channel](https://discordapp.com/channels/1296187754979528747/1475173660686815374)
- [The Ultra Place / Ultra+ Discord](https://discord.gg/pQtPYcdE)
- [RDXC GitHub](https://github.com/RankFTW/RenoDXChecker)
