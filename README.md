# RenoDXCommander (RDXC) v1.2.1

An unofficial companion app for the [RenoDX](https://github.com/clshortfuse/renodx) HDR mod project.
Automatically detects your installed games and manages all three components needed for HDR modding:
**ReShade**, **Display Commander**, and the **RenoDX HDR mod** — all from one place.

> **Disclaimer:** RenoDXCommander is an unofficial third-party tool not affiliated with or endorsed by
> the RenoDX project, Crosire, or pmnoxx. ReShade 6.7.2 is bundled with RDXC and redistributed under
> its BSD 3-Clause licence. Display Commander and RenoDX mods are downloaded directly from their official
> GitHub sources at runtime. Nothing else is modified or redistributed.

---

## Features

| Feature | Details |
|---------|---------|
| 🎮 ReShade 6.7.2 | ReShade 6.7.2 (addon support) is bundled with RDXC and installed per game — no download needed |
| 🖥 Display Commander | Downloads and installs Display Commander per game |
| ⚙ DC Mode toggle | Global toggle that swaps how ReShade and DC name their files |
| 🎯 Per-game DC exclusion | Exclude individual games from DC Mode via the Overrides dialog |
| 📋 INI presets | Copy your reshade.ini / DisplayCommander.toml to any game folder with one click |
| ⬇ RenoDX one-click install | Downloads and places `.addon64` / `.addon32` in the correct folder |
| 🔍 Auto-detection | Finds games from Steam, GOG, Epic Games, and EA App |
| 📦 Download cache | All files cached locally — reinstalling skips the download |
| 🔄 Update detection | Compares stored file size against remote; flags only real updates |
| 🎨 Shader packs | Downloads 7 HDR shader packs from GitHub on launch; Off/Minimum/All/User deploy mode via header button |
| 🎮 Generic engine mods | Generic Unreal Engine and Unity plugins for unlisted games |
| ⚡ UE-Extended toggle | Switch any Generic UE card to use the extended UE addon |
| ℹ Game notes | Per-game setup notes from the RenoDX wiki |
| 💬 Support button | Direct link to the RDXC support channel on Discord |
| ➕ Manual add | Add any game manually |
| 🚫 Hide games | Hide games from the list |
| 🔎 Filter tabs | All Games, Installed, Not Installed, Unity, Unreal, Other, Hidden |
| 🪲 Crash reporting | Unhandled errors saved automatically to the logs folder |

---

## Requirements

- Windows 10/11 (x64)
- [.NET 8 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/8.0)

> ReShade no longer needs to be installed manually — RDXC handles it for you.

---

## Getting Started

### 1. Open RDXC
The app scans your installed games on startup. First launch takes a few seconds; subsequent launches use a cached library.

### 2. Find your game
Use the search bar or filter tabs. If your game isn't detected automatically, click **➕ Add Game**.

### 3. Install ReShade
Click **⬇ Install ReShade** on any game card. RDXC:
- Copies the bundled **ReShade 6.7.2** `ReShade64.dll` to the staging folder in AppData (first use)
- Restores from bundle if the staging copy is ever deleted
- Installs it as `dxgi.dll` in the game's folder
- No download needed — ReShade 6.7.2 is bundled and always available offline

### 4. Install Display Commander
Click **⬇ Install Display Commander**. Downloaded from pmnoxx's GitHub and placed in the game folder.

### 5. Install RenoDX
Click **⬇ Install RenoDX** (bottom button, supported games only). Placed in the same folder as dxgi.dll.

### 6. Launch the game
Press **Home** to open ReShade → **Add-ons** tab → configure RenoDX.

---

## DC Mode

The **⚙ DC Mode** toggle in the header changes how files are named on install:

| Toggle | ReShade filename | Display Commander filename |
|--------|-----------------|---------------------------|
| OFF (default) | `dxgi.dll` | `zzz_display_commander.addon64` |
| ON | `ReShade64.dll` | `dxgi.dll` |

When you switch modes and click Install, the old file is automatically removed and replaced with the correctly named one.

### Per-game DC exclusion
Click 🎯 on a card → **Overrides** → toggle **"Exclude from global DC Mode"**. That game will always use normal naming regardless of the global toggle.

---

## INI Presets

Place your own config files in `%LocalAppData%\RenoDXCommander\inis\`:

| File | Copied to |
|------|-----------|
| `reshade.ini` | Game install folder (as `reshade.ini`) |
| `DisplayCommander.toml` | Game install folder (as `DisplayCommander.toml`) |

The 📋 button on each row is **greyed out** when the file is absent and becomes **active** the moment you place a file there. Clicking it copies a fresh copy of your preset to that game's folder, overwriting any existing config.

## ReShade Shaders & Textures

RDXC automatically downloads and maintains a curated set of HDR-compatible ReShade shader packs on every launch. All packs are merged into a shared staging folder and deployed per-game when you install ReShade or Display Commander.

### Included shader packs

| Pack | Author | Source |
|------|--------|--------|
| [ReShade HDR Shaders](https://github.com/EndlesslyFlowering/ReShade_HDR_shaders) | EndlesslyFlowering (Lilium) | GitHub Releases — versioned by release asset filename |
| [PumboAutoHDR](https://github.com/Filoppi/PumboAutoHDR) | Filoppi (Pumbo) | GitHub Releases |
| [smolbbsoop shaders](https://github.com/smolbbsoop/smolbbsoopshaders) | smolbbsoop | GitHub main branch |
| [Reshade Simple HDR Shaders](https://github.com/MaxG2D/ReshadeSimpleHDRShaders) | MaxG2D | Direct release asset |
| [reshade-shaders](https://github.com/clshortfuse/reshade-shaders) | clshortfuse | GitHub main branch |
| [potatoFX](https://github.com/CreepySasquatch/potatoFX) | CreepySasquatch | GitHub main branch |
| [reshade-shaders (slim)](https://github.com/crosire/reshade-shaders/tree/slim) | crosire | GitHub slim branch |

All shader files are downloaded directly from their official repositories and are not modified or redistributed by RDXC.

### Where shaders are stored

Staging folder (all packs merged here on download):
```
%LocalAppData%\RenoDXCommander\reshade\Shaders\
%LocalAppData%\RenoDXCommander\reshade\Textures\
```

### Shader deploy mode

The **🎨 Shaders** button in the header cycles through three modes:

| Mode | Behaviour |
|------|-----------|
| **Off** (default) | RDXC does not deploy any shaders. Manage your own shaders manually. |
| **Minimum** | Only the Lilium HDR Shaders pack is deployed. |
| **All** | All 7 included shader packs are deployed. |
| **User** | Only files you place in the Custom folder below are deployed. No auto-downloaded packs are used. |

The setting is persisted between sessions.

Clicking ↻ **Refresh** re-evaluates the current mode against every installed game and DC folder. It both **adds** missing files and **removes** files that belong to packs no longer selected by the current mode. This is how you apply a mode change to existing installs.

### Custom shader folder (User mode)

Place your own shader files in:
```
%LocalAppData%\RenoDXCommander\reshade\Custom\Shaders\
%LocalAppData%\RenoDXCommander\reshade\Custom\Textures\
```
When Shaders mode is set to **User**, RDXC deploys the contents of this folder to each game and/or the DC global folder, preserving the subfolder structure exactly. No auto-downloaded packs are used in User mode — only your files.

### Per-game shader exclusion

Click 🎯 on a card → **Overrides** → toggle **"Exclude from shader management"**. RDXC will never create, modify or delete the `reshade-shaders` folder for that game — you manage shaders manually for it.

### Preserving existing reshade-shaders folders

If a `reshade-shaders` folder already exists in a game directory when RDXC deploys shaders, and it was **not** placed there by RDXC (identified by the presence of a `Managed by RDXC.txt` file inside), RDXC renames it to `reshade-shaders-original` before creating its own managed folder. When ReShade or Display Commander is uninstalled via RDXC, the managed folder is removed and `reshade-shaders-original` is renamed back to `reshade-shaders` so your original shaders are restored automatically.

### Where shaders are deployed

| Scenario | Destination |
|----------|-------------|
| DC Mode ON — any game | `%LOCALAPPDATA%\Programs\Display_Commander\Reshade\Shaders\` and `\Textures\` (copied once; missing files filled in on subsequent installs) |
| DC Mode OFF — ReShade installed, no DC | `<game folder>\reshade-shaders\Shaders\` and `\Textures\` |
| DC then installed to a game with reshade-shaders | `reshade-shaders\` folder is removed (ReShade uses the DC global path) |

The full subdirectory structure from inside each `Shaders/` and `Textures/` folder in the archive is preserved exactly as-is. Files that already exist at the destination are never overwritten, preserving any manual edits.

See also [Creepy's Wiki — HDR-Compatible Shaders](https://www.hdrmods.com/HDR-Link-Library#hdr-compatible-shaders) for a broader curated list.

## Card Layout

| Row | Content |
|-----|---------|
| Top | **ReShade** — Install / Reinstall / Update + 🗑 |
| Middle | **Display Commander** — Install / Reinstall / Update + 🗑 |
| Bottom | **RenoDX mod** — Install / Reinstall / Update + 🌐 + ⚡ + 🗑 |

Buttons round their right corners automatically when no adjacent button follows. The ⚡ UE-Extended button rounds its right side when it's the last button in the row.

---

## Download Cache

Stored in `%LocalAppData%\RenoDXCommander\downloads\`:

| File | Description |
|------|-------------|
| `ReShade_Setup_X.Y.Z_Addon.exe` | Downloaded ReShade installer |
| `ReShade64_extracted.dll` | Extracted ReShade DLL (reused across all games) |
| `zzz_display_commander.addon64` | Cached DC addon |
| `*.addon64` / `*.addon32` | Cached RenoDX addon files |

---

## Data Storage

All under `%LocalAppData%\RenoDXCommander\`:

| Path | Contents |
|------|---------|
| `game_library.json` | Detected games, hidden list, manual games |
| `installed.json` | RenoDX install records |
| `aux_installed.json` | ReShade and DC install records |
| `settings.json` | Name mappings, exclusions, UE-Extended state, DC Mode, per-game DC exclusions |
| `downloads\` | Cached files |
| `inis\` | User-placed preset config files (`reshade.ini`, `DisplayCommander.toml`) |
| `logs\` | Crash reports |

> **Note:** The data folder is named `RenoDXCommander` for backwards compatibility with existing installs. All your data is preserved when upgrading from older versions.

---

## Buttons Reference

| Button | Action |
|--------|--------|
| 📋 (ReShade row) | Copy `reshade.ini` from inis folder to game folder |
| 📋 (DC row) | Copy `DisplayCommander.toml` from inis folder to game folder |
| ⬇ Install ReShade | Download latest ReShade, extract, install as dxgi.dll |
| ↺ Reinstall / ⬆ Update ReShade | Re-copy from cache or re-download |
| ⬇ Install Display Commander | Download DC addon and install |
| ↺ Reinstall / ⬆ Update DC | Re-copy or re-download |
| ⬇ Install RenoDX | Download and install RenoDX addon |
| ↺ Reinstall / ⬆ Update RenoDX | Re-copy or re-download (purple tint = update available) |
| 🗑 | Remove the installed file (cache kept) |
| ℹ | Game-specific notes |
| 💬 | Wiki discussion thread (before game name) |
| 🚫 | Hide / unhide |
| 📁 | Open or change install folder |
| 🌐 | Nexus Mods or Discord page |
| 🎯 | Overrides (wiki name matching, wiki exclusion, DC Mode exclusion, Update All exclusion, shader management exclusion) |
| ⚡ | Toggle UE-Extended (Generic UE cards only) |

---

## Troubleshooting

**Game not detected?** Use ➕ Add Game or 🎯 for a custom wiki name mapping.

**ReShade not loading?** `dxgi.dll` must be in the same folder as the `.addon64` file. For Unreal: `Binaries\Win64`.

**Black screen (Unreal)?** ReShade → Add-ons → RenoDX → set `R10G10B10A2_UNORM` to `output size`.

**Downloads failing?** Click ↻ Refresh. Clear cache: About → 📦 Open Downloads Cache.

**Wrong install path?** Click 📁 to change it.

**Game showing wrong update status?** Only mods installed via RDXC track updates. Manually-placed mods won't show updates.

---

## Third-Party Components

All open-source components are used in compliance with their respective licences.

| Component | Author | Licence | Use in RDXC |
|-----------|--------|---------|-------------|
| [ReShade](https://reshade.me) | Crosire | [BSD 3-Clause](https://github.com/crosire/reshade/blob/main/LICENSE.md) | Post-processing injection framework. `ReShade64.dll` / `ReShade32.dll` are bundled and redistributed under this licence. |
| [Display Commander](https://github.com/pmnoxx/display-commander) | pmnoxx | Source-available | Display, window, and audio management addon. Downloaded from official GitHub releases at runtime. |
| [RenoDX](https://github.com/clshortfuse/renodx) | clshortfuse & contributors | [MIT](https://github.com/clshortfuse/renodx/blob/main/LICENSE) | HDR mod framework. Mods fetched from official GitHub snapshots at runtime — not bundled. |
| [HtmlAgilityPack](https://github.com/zzzprojects/html-agility-pack) | ZZZ Projects Inc. | [MIT](https://github.com/zzzprojects/html-agility-pack/blob/master/LICENSE) | HTML parser used to scrape game data from the RenoDX wiki. |
| [CommunityToolkit.Mvvm](https://github.com/CommunityToolkit/dotnet) | Microsoft / .NET Foundation | [MIT](https://github.com/CommunityToolkit/dotnet/blob/main/License.md) | MVVM helpers (ObservableObject, RelayCommand, etc.). |
| [Microsoft.Win32.Registry](https://github.com/dotnet/runtime) | Microsoft / .NET Foundation | [MIT](https://github.com/dotnet/runtime/blob/main/LICENSE.TXT) | Windows Registry access for Steam/GOG/Epic/EA game detection. |
| [SharpCompress](https://github.com/adamhathcock/sharpcompress) | Adam Hathcock | [MIT](https://github.com/adamhathcock/sharpcompress/blob/master/LICENSE.txt) | Archive extraction (.7z, .zip) used for shader pack downloads. |
| [ReShade HDR Shaders](https://github.com/EndlesslyFlowering/ReShade_HDR_shaders) | EndlesslyFlowering (Lilium) | [GPL-3.0](https://github.com/EndlesslyFlowering/ReShade_HDR_shaders/blob/master/LICENSE) | HDR shader pack. Downloaded at runtime from official GitHub releases. |
| [PumboAutoHDR](https://github.com/Filoppi/PumboAutoHDR) | Filoppi (Pumbo) | See repo | HDR auto-grading shaders. Downloaded at runtime from official GitHub releases. |
| [smolbbsoop shaders](https://github.com/smolbbsoop/smolbbsoopshaders) | smolbbsoop | See repo | HDR ReShade shaders. Downloaded at runtime from GitHub main branch. |
| [Reshade Simple HDR Shaders](https://github.com/MaxG2D/ReshadeSimpleHDRShaders) | MaxG2D | See repo | Simple HDR shaders. Downloaded at runtime from official GitHub release. |
| [reshade-shaders](https://github.com/clshortfuse/reshade-shaders) | clshortfuse | See repo | ReShade shaders. Downloaded at runtime from GitHub main branch. |
| [potatoFX](https://github.com/CreepySasquatch/potatoFX) | CreepySasquatch | See repo | potatoFX ReShade shader suite. Downloaded at runtime from GitHub main branch. |
| [reshade-shaders (slim)](https://github.com/crosire/reshade-shaders/tree/slim) | crosire | [BSD 3-Clause](https://github.com/crosire/reshade/blob/main/LICENSE.md) | ReShade shader collection (slim branch). Downloaded at runtime from GitHub. |

> ReShade is © Crosire and licensed under the BSD 3-Clause licence. Redistribution of the compiled DLLs is permitted provided the licence notice is preserved. The full licence text is available at [github.com/crosire/reshade](https://github.com/crosire/reshade/blob/main/LICENSE.md).

---

## Links

- [RenoDX GitHub](https://github.com/clshortfuse/renodx) by clshortfuse
- [RenoDX Mod Wiki](https://github.com/clshortfuse/renodx/wiki/Mods)
- [ReShade](https://reshade.me) by Crosire
- [Display Commander](https://github.com/pmnoxx/display-commander) by pmnoxx
- [Creepy's HDR Guides](https://www.hdrmods.com)
- [RenoDX Discord](https://discord.gg/gF4GRJWZ2A)
- [RDXC Support Channel](https://discordapp.com/channels/1296187754979528747/1475173660686815374)
- [The Ultra Place / Ultra+ Discord](https://discord.gg/pQtPYcdE)
- [RankFTW GitHub](https://github.com/RankFTW)

### Shader Pack Authors
- [EndlesslyFlowering (Lilium) — ReShade HDR Shaders](https://github.com/EndlesslyFlowering/ReShade_HDR_shaders)
- [Filoppi (Pumbo) — PumboAutoHDR](https://github.com/Filoppi/PumboAutoHDR)
- [smolbbsoop — smolbbsoop shaders](https://github.com/smolbbsoop/smolbbsoopshaders)
- [MaxG2D — Reshade Simple HDR Shaders](https://github.com/MaxG2D/ReshadeSimpleHDRShaders)
- [clshortfuse — reshade-shaders](https://github.com/clshortfuse/reshade-shaders)
- [CreepySasquatch — potatoFX](https://github.com/CreepySasquatch/potatoFX)
- [crosire — reshade-shaders (slim)](https://github.com/crosire/reshade-shaders/tree/slim)
