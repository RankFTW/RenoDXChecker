# RDXC — RenoDX Mod Manager v1.0.3

An unofficial companion app for the [RenoDX](https://github.com/clshortfuse/renodx) HDR mod project.
Automatically detects your installed games, matches them against the RenoDX wiki, and lets you
install or update HDR mods with one click.

> **Disclaimer:** This is an unofficial third-party tool. It is not affiliated with or endorsed by
> the RenoDX project. All mod files are downloaded directly from official RenoDX GitHub snapshots.

---

## Features

| Feature | Details |
|---------|---------|
| 🔍 Auto-detection | Finds games from Steam, GOG, Epic Games, and EA App |
| ⬇ One-click install | Downloads and places `.addon64` / `.addon32` in the correct folder |
| 📦 Download cache | Addon files are cached locally — reinstalling skips the download entirely |
| 🔄 Update detection | Compares stored install-time file size against remote; flags only real updates |
| 🎮 Generic engine mods | Offers Generic Unreal Engine and Generic Unity Engine plugins for unlisted games |
| ℹ Game notes | Per-game setup notes pulled live from the RenoDX wiki |
| 💬 Discussion links | Shown before the game name for easy access; opens the wiki discussion thread |
| 💬 Named mod fallback | Games with a named addon but no wiki entry show a Discord link for support |
| 🌐 Extra links | Nexus Mods / Discord links shown on installed cards when available |
| ➕ Manual add | Add any game manually if it wasn't auto-detected |
| 🚫 Hide games | Hide games from the list; toggle back via the Hidden tab |
| 🔧 Unity 32/64 split | Separate install buttons for 32-bit and 64-bit Unity games |
| 📦 Installed badge | Shows the installed addon filename on each card |
| 🔎 Filter tabs | Filter by All Games, Installed, Hidden, Unity, Unreal, or Other |
| 🎯 Per-card name mapping | Override wiki name matching on individual cards using the 🎯 button |
| 💾 Window memory | The main window remembers its size and position between sessions |
| ❓ Unknown status | Games with no RenoDX mod show ❓ Unknown status |
| 🪲 Crash reporting | Unhandled errors saved automatically to the logs folder |
| ⚡ Instant card update | Installing a mod updates only that card — the rest of the UI is untouched |

---

## Requirements

- Windows 10/11 (x64)
- [.NET 8 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/8.0)
- [ReShade 6.5.1+ with Addon Support](https://reshade.me/downloads/ReShade_Setup_Addon.exe)

---

## Getting Started

### 1. Install ReShade with Addon Support
Download from [reshade.me](https://reshade.me) and run the installer.
Choose **"with full add-on support"**. No shader packages needed.
Point it at your game's main executable (`Binaries\Win64` for Unreal games).

### 2. Open RDXC
The app scans your installed games on startup. First launch takes a few seconds;
subsequent launches use a cached library and are much faster.

### 3. Find your game
Use the search bar or filter tabs (All Games, Installed, Unity, Unreal, Other, Hidden).

### 4. Check ℹ and 💬 before installing
- **💬** (before the game name) — opens the wiki discussion thread; may contain required setup steps
- **ℹ** — shows game-specific notes and known issues

### 5. Click Install
The app checks the local download cache first and copies from there if available.
After installing the card updates instantly — no full refresh occurs.

### 6. Launch the game
Press **Home** to open the ReShade menu → **Add-ons** tab → configure RenoDX.

---

## Game Status Indicators

| Badge | Meaning |
|-------|---------|
| ✅ Working | Listed on the RenoDX wiki with a confirmed working mod |
| 🚧 In Progress | Listed on the wiki but still being developed |
| 💬 Discord | A Discord link replaces the install button — check the community for status |
| ❓ Unknown | Not on the wiki — no dedicated RenoDX mod known for this game |

The **💬 Discord** status appears when:
- The wiki lists the game but only provides a Discord link (no direct download)
- A named RenoDX addon is installed but the game has no wiki entry
- You have manually excluded a game from wiki matching via the 🎯 dialog

---

## Card Layout (left to right, top row)

| Element | Position |
|---------|---------|
| 💬 | Far left — only shown when a wiki discussion thread exists |
| Game name | Fills remaining space |
| ℹ | Notes button — only shown when notes exist |
| 🚫 | Hide / unhide this game |
| 🎯 | Override wiki name matching for this game |
| ✅ / 🚧 / ❓ | Wiki status badge — far right |

---

## Unreal Engine Version Handling

RenoDX requires **Unreal Engine 4 or later**. Games on UE3 or below (e.g. Rocket League)
are detected via `.u`/`.upk` files and other markers, shown with an **Unreal (Legacy)**
badge and ❓ Unknown status — no install button offered.

---

## Download Cache

Downloaded addon files are stored in `%LocalAppData%\RenoDXChecker\downloads\`.

The app checks this before downloading. If the cached file matches the remote size it
copies from cache directly. To force a fresh download: About → **📦 Open Downloads Cache**
and delete the file.

---

## Folder Structure (Unreal Engine)

```
GameRoot\
  MyGame\
    Binaries\
      Win64\          ← ✅ mod goes here
  Engine\
    Binaries\
      Win64\          ← ❌ not here
```

---

## Cards Reference

| Button | Action |
|--------|--------|
| ⬇ Install | Download (or copy from cache) and install |
| ↺ Reinstall | Re-copy from cache or re-download and overwrite |
| ⬆ Update | Newer version available — button turns purple to stand out; reverts to blue after updating |
| 🗑 | Remove the installed file (cache copy kept) |
| ℹ | View game-specific notes |
| 💬 | Open wiki discussion thread (before game name) |
| 🚫 | Hide / unhide |
| 📁 | Open install folder / change folder |
| 🌐 | Open Nexus Mods or Discord page |
| 🎯 | Override wiki name matching for this card |

---

## Generic Mods

### Generic Unreal Engine
- File: `renodx-unrealengine.addon64`
- ReShade must be in the same folder as `*-Win64-Shipping.exe`
- UE4/5 only — UE3 and below are not supported

### Generic Unity Engine
- Files: `renodx-unityengine.addon64` / `renodx-unityengine.addon32`
- Install next to `UnityPlayer.dll`
- 64-bit for modern games; 32-bit for older 32-bit builds

---

## Wiki Name Matching Override

If a game isn't matching its wiki mod (e.g. different edition name), click 🎯 on the card.
Enter the detected game name and the exact wiki name. The app re-matches immediately.

---

## Data Storage

All under `%LocalAppData%\RenoDXChecker\`:

| Path | Contents |
|------|---------|
| `game_library.json` | Detected games, hidden list, manual games, scan cache |
| `installed.json` | Install records including stored remote file sizes |
| `window_main.json` | Window size and position |
| `settings.json` | Name mappings and preferences |
| `downloads\` | Cached addon files |
| `logs\` | Crash reports (max 10, oldest deleted automatically) |

---

## Crash & Error Reporting

Exceptions are automatically written to:
```
%LocalAppData%\RenoDXChecker\logs\crash_YYYY-MM-DD_HH-mm-ss.txt
```

Open **About → 📂 Open Logs Folder** and attach the latest file when reporting a bug.

---

## Troubleshooting

**Game not detected?** Use ➕ Add Game. Match the wiki name exactly, or use 🎯 for a custom mapping.

**Game shows ❓ Unknown?** No wiki entry. May still work with a generic plugin. Check Discord.

**Game shows Unreal (Legacy)?** UE3 or below — not compatible with RenoDX addons.

**ReShade not loading the mod?** ReShade must be in the same folder as the `.addon64` file. For Unreal: `Binaries\Win64`.

**Black screen on launch (Unreal)?** ReShade → Add-ons → RenoDX → set `R10G10B10A2_UNORM` to `output size`.

**Downloads failing?** Click ↻ Refresh. To clear a bad cache file: About → 📦 Open Downloads Cache.

**Wrong install path?** Click 📁 → Change install folder.

---

## Links

- [RenoDX GitHub](https://github.com/clshortfuse/renodx)
- [RenoDX Mod Wiki](https://github.com/clshortfuse/renodx/wiki/Mods)
- [ReShade](https://reshade.me)
- [RenoDX Discord](https://discord.gg/gF4GRJWZ2A)
- [RankFTW GitHub](https://github.com/RankFTW)
