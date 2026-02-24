# RenoDXCommander (RDXC) v1.2.0

An unofficial companion app for the [RenoDX](https://github.com/clshortfuse/renodx) HDR mod project.
Automatically detects your installed games and manages all three components needed for HDR modding:
**ReShade**, **Display Commander**, and the **RenoDX HDR mod** — all from one place.

> **Disclaimer:** RenoDXCommander is an unofficial third-party tool not affiliated with or endorsed by
> the RenoDX project, Crosire, or pmnoxx. All files are downloaded directly from their official sources
> (reshade.me, pmnoxx's GitHub, and the RenoDX GitHub). Nothing is modified or redistributed.

---

## Features

| Feature | Details |
|---------|---------|
| 🎮 ReShade installer | Installs included version of ReShade 6.7.2 |
| 🖥 Display Commander | Downloads and installs Display Commander per game |
| ⚙ DC Mode toggle | Global toggle that swaps how ReShade and DC name their files |
| 🎯 Per-game DC exclusion | Exclude individual games from DC Mode via the Overrides dialog |
| 📋 INI presets | Copy your reshade.ini / DisplayCommander.toml to any game folder with one click |
| ⬇ RenoDX one-click install | Downloads and places `.addon64` / `.addon32` in the correct folder |
| 🔍 Auto-detection | Finds games from Steam, GOG, Epic Games, and EA App |
| 📦 Download cache | All files cached locally — reinstalling skips the download |
| 🔄 Update detection | Compares stored file size against remote; flags only real updates |
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
- Copies the bundled `ReShade64.dll` to the staging folder in AppData (first use)
- Restores from bundle if the staging copy is ever deleted
- Installs it as `dxgi.dll` in the game's folder
- No download needed — ReShade is always available offline

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

ReShade shaders and textures are **not supplied** by RDXC and must be sourced manually. See [Creepy's Wiki — HDR-Compatible Shaders](https://www.hdrmods.com/HDR-Link-Library#hdr-compatible-shaders) for a curated list.

### Global shader location (via Display Commander)

If you place your shaders and textures in:

```
%LOCALAPPDATA%\Programs\Display_Commander\Reshade
```

Display Commander will load them automatically for every game — you don't need to copy them into each individual game folder.

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
| 🎯 | Overrides (wiki name matching, wiki exclusion, DC Mode exclusion) |
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
