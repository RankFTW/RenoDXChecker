# RDXC — RenoDX Mod Manager v1.0.2

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
| 📦 Download cache | Addon files are cached locally — reinstalling a mod skips the download entirely |
| 🔄 Update detection | Compares stored install-time file size against remote; flags only real updates |
| 🎮 Generic engine mods | Offers Generic Unreal Engine and Generic Unity Engine plugins for unlisted games |
| ℹ Game notes | Shows per-game setup notes pulled live from the RenoDX wiki |
| 💬 Discussion links | Games with a wiki discussion link show a chat button that opens it in the browser |
| 💬 Named mod fallback | Games with a named RenoDX addon installed but no wiki entry show a Discord link for support |
| 🌐 Extra links | Shows Nexus Mods / Discord links alongside the install button when available |
| ➕ Manual add | Add any game manually if it wasn't auto-detected |
| 🚫 Hide games | Hide games you don't need from the list; toggle them back anytime |
| 🔧 Unity 32/64 split | Separate install buttons for 32-bit and 64-bit Unity games |
| 📦 Installed badge | Shows the installed addon filename on each card |
| 🔎 Filter tabs | Filter by All Games, Installed, Hidden, Unity, Unreal, or Other |
| 🗂 Name mapping | Use the Tune button to add fuzzy-match overrides for games with unusual names |
| 💾 Window memory | The main window remembers its size and position between sessions |
| ❓ Unknown status | Games with no RenoDX mod and no known wiki entry show ❓ Unknown |
| 🪲 Crash reporting | Unhandled errors are automatically written to the logs folder for troubleshooting |

---

## Requirements

- Windows 10/11 (x64)
- [.NET 8 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/8.0)
- [ReShade 6.5.1+ with Addon Support](https://reshade.me/downloads/ReShade_Setup_Addon.exe)

---

## Getting Started

### 1. Install ReShade with Addon Support
Download ReShade from [reshade.me](https://reshade.me) and run the installer.
When prompted, choose **"with full add-on support"**. No shader packages are required.
Point it at your game's main executable (the one in `Binaries\Win64` for Unreal games).

### 2. Open RDXC
The app scans your installed games on startup. This takes a few seconds the first time;
subsequent launches use a cached library and are much faster.

### 3. Find your game
Use the search bar or scroll through the cards. Use the filter tabs to narrow the list
by install status or engine type.

### 4. Check the ℹ and 💬 buttons before installing
- **ℹ** — shows game-specific notes and known warnings (especially important for generic engine mods)
- **💬** — opens the RenoDX discussion thread for that game, which may include extra setup steps

### 5. Click Install
The app checks the local download cache first. If the file is already cached, it copies from
cache instead of downloading again. The `.addon64` file is placed in the correct location:
- **Unreal Engine games:** `GameName\Binaries\Win64\` (or `WinGDK`)
- **Unity games:** next to `UnityPlayer.dll` (usually the game root)

After installing the app automatically refreshes to reflect the new state.

### 6. Launch the game
Press **Home** to open the ReShade menu. Go to the **Add-ons** tab and configure RenoDX.

---

## Game Status Indicators

| Badge | Meaning |
|-------|---------|
| ✅ Working | Listed on the RenoDX wiki with a confirmed working mod |
| 🚧 In Progress | Listed on the wiki but the mod is still being developed |
| ❓ Unknown | Not on the wiki — no dedicated RenoDX mod is known for this game |

Games showing **❓ Unknown** may still work with Generic Unreal Engine or Generic Unity Engine
plugins if the engine is detected.

If a game shows **❓ Unknown** but already has a named RenoDX addon installed (e.g.
`renodx-avatarfop-swoutlaws.addon64`), a **💬 Discord** button appears for community support.

---

## Unreal Engine Version Handling

RenoDX requires **Unreal Engine 4 or later**. Games on UE3 or below (e.g. Rocket League)
are automatically detected via `.u`/`.upk` files and other markers, shown with an
**Unreal (Legacy)** badge and ❓ Unknown status — no install button is offered.

---

## Download Cache

Downloaded addon files are stored in `%LocalAppData%\RenoDXChecker\downloads\`.

When installing, the app checks this folder first. If the cached file matches the remote
file size it copies from cache — no download needed. This means:
- Installing the same mod on multiple games only downloads once
- Reinstalling after an uninstall is instant

To force a fresh download, open **About → 📦 Open Downloads Cache** and delete the file.

---

## Folder Structure (Unreal Engine)

```
GameRoot\
  MyGame\
    Binaries\
      Win64\          ← ✅ mod goes here (next to MyGame-Win64-Shipping.exe)
  Engine\
    Binaries\
      Win64\          ← ❌ NOT here
```

---

## Cards Reference

| Button | Action |
|--------|--------|
| ⬇ Install | Download (or copy from cache) and install the mod |
| ↺ Reinstall | Re-copy from cache or re-download and overwrite |
| ⬆ Update | A newer version is available — click to update |
| 🗑 | Remove the installed mod file (cache copy kept) |
| ℹ | View game-specific notes |
| 💬 | Open discussion thread or Discord |
| 🚫 | Hide / unhide this game |
| 📁 | Open install folder / change folder |
| 🌐 | Open Nexus Mods or Discord page |

---

## Notes on Generic Mods

### Generic Unreal Engine
- File: `renodx-unrealengine.addon64`
- ReShade must be in the same folder as `*-Win64-Shipping.exe`
- Check ℹ for black screen / DLSS FG fixes

### Generic Unity Engine
- Files: `renodx-unityengine.addon64` / `renodx-unityengine.addon32`
- Install next to `UnityPlayer.dll`
- Use 64-bit for modern games; 32-bit for older 32-bit games

---

## Update Checking

On each launch the app does a background HEAD request per installed mod. It compares the
current remote `Content-Length` against the size recorded at install time. Only a genuine
file size change triggers **⬆ Update Available**.

---

## Adding Games Manually

1. Click **➕ Add Game**
2. Enter the name as it appears on the RenoDX wiki
3. Pick the install folder

---

## Data Storage

| Path | Contents |
|------|---------|
| `game_library.json` | Detected games, hidden list, manual games, scan cache |
| `installed.json` | Install records including stored remote file sizes |
| `window_main.json` | Window size and position |
| `settings.json` | Name mappings and preferences |
| `downloads\` | Cached addon files |
| `logs\` | Crash reports (max 10, oldest deleted automatically) |

All under `%LocalAppData%\RenoDXChecker\`.

---

## Crash & Error Reporting

Unhandled exceptions are automatically written to:
```
%LocalAppData%\RenoDXChecker\logs\crash_YYYY-MM-DD_HH-mm-ss.txt
```

Open **About → 📂 Open Logs Folder** and attach the latest file when reporting a bug.

---

## Troubleshooting

**Game not detected?** Use ➕ Add Game. Match the name to the wiki exactly, or use Tune for a custom mapping.

**Game shows ❓ Unknown?** No wiki entry. May still work with a generic plugin. Check Discord.

**Game shows Unreal (Legacy)?** UE3 or below — not compatible with RenoDX addons.

**ReShade not loading the mod?** ReShade must be in the *same folder* as the `.addon64` file. For Unreal games: `Binaries\Win64`.

**Black screen on launch (Unreal)?** ReShade → Add-ons → RenoDX → set `R10G10B10A2_UNORM` to `output size`.

**Downloads failing?** Click ↻ Refresh to re-fetch the wiki. To clear a bad cache file: About → 📦 Open Downloads Cache.

**Wrong install path?** Click 📁 → Change install folder.

---

## Links

- [RenoDX GitHub](https://github.com/clshortfuse/renodx)
- [RenoDX Mod Wiki](https://github.com/clshortfuse/renodx/wiki/Mods)
- [ReShade](https://reshade.me)
- [RenoDX Discord](https://discord.gg/gF4GRJWZ2A)
