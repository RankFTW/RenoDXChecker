# RenoDX Mod Manager

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
| 🔄 Update detection | Checks if a newer snapshot is available and highlights games with updates |
| 🎮 Generic engine mods | Offers Generic Unreal Engine and Generic Unity Engine plugins for unlisted games |
| ℹ Game notes | Shows per-game setup notes pulled live from the RenoDX wiki |
| 🌐 Extra links | Shows Nexus Mods / Discord links alongside the install button when available |
| ➕ Manual add | Add any game manually if it wasn't auto-detected |
| 🚫 Hide games | Hide games you don't need from the list; reveal them again anytime |
| 🔧 Unity 32/64 split | Separate install buttons for 32-bit and 64-bit Unity games |
| 📦 Installed badge | Shows the installed addon filename on each card |

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

### 2. Open RenoDX Mod Manager
The app scans your installed games on startup. This takes a few seconds the first time;
subsequent launches use a cached library and are much faster.

### 3. Find your game
Use the search bar or scroll through the cards. Filter to **✓ Installed** to see only
games with mods already installed.

### 4. Check the ℹ button (important for generic mods)
For Unreal Engine and Unity games using the generic plugin, tap **ℹ** to read
game-specific settings and any known warnings before installing.

### 5. Click Install
The app downloads the `.addon64` file and places it in the correct location:
- **Unreal Engine games:** `GameName\Binaries\Win64\` (or `WinGDK`)
- **Unity games:** next to `UnityPlayer.dll` (usually the game root)

If the folder couldn't be detected automatically, you'll be prompted to pick it.

### 6. Launch the game
Press **Home** to open the ReShade menu. Go to the **Add-ons** tab and configure RenoDX.

---

## Folder Structure (Unreal Engine)

Unreal Engine games follow a specific layout. The mod goes in the `Binaries\Win64` (or `WinGDK`)
folder that is **not** inside the `Engine` folder:

```
GameRoot\
  MyGame\             ← game-specific folder (or codename)
    Binaries\
      Win64\          ← ✅ mod goes here (next to MyGame-Win64-Shipping.exe)
  Engine\
    Binaries\
      Win64\          ← ❌ NOT here — this is the engine
```

---

## Cards Reference

| Button | Action |
|--------|--------|
| ⬇ Install | Download and install the mod |
| ↺ Reinstall | Re-download and overwrite the existing mod |
| ⬆ Update | A newer snapshot is available — click to update |
| 🗑 | Remove the installed mod file |
| ℹ | View game-specific notes and setup instructions |
| 🚫 | Hide this game from the list |
| 📁 | Open install folder in Explorer / change the folder |
| 🌐 | Open Nexus Mods or Discord page for this mod |

---

## Notes on Generic Mods

When a game doesn't have its own specific RenoDX mod, the app offers generic engine plugins:

### Generic Unreal Engine
- File: `renodx-unrealengine.addon64`
- Requires ReShade installed in the same folder as the game's `*-Win64-Shipping.exe`
- Common issues and fixes are shown in the ℹ dialog (black screen fix, DLSS FG fix)

### Generic Unity Engine  
- Files: `renodx-unityengine.addon64` / `renodx-unityengine.addon32`
- Install next to `UnityPlayer.dll`
- Use 64-bit for modern games; 32-bit only for older games

---

## Update Checking

On each launch the app does a background HEAD request against each installed mod's snapshot URL.
If the remote file is newer than when you last installed, the card shows **⬆ Update Available**
and the Reinstall button becomes **⬆ Update**.

---

## Data Storage

All app data is stored locally in `%LocalAppData%\RenoDXChecker\`:

| File | Contents |
|------|---------|
| `game_library.json` | Detected games, hidden games, manual games, addon scan cache |
| `installed.json` | Install records with file paths, hashes, and snapshot dates |

---

## Troubleshooting

**Game not detected?**
Use **➕ Add Game** to add it manually. Enter the game name as it appears on the
[RenoDX wiki](https://github.com/clshortfuse/renodx/wiki/Mods) and pick the install folder.

**Mod installed but ReShade doesn't load it?**
Make sure ReShade is installed in the *same folder* as the `.addon64` file.
For Unreal games this is `Binaries\Win64`, not the game root.

**Black screen on launch (Unreal Engine)?**
Open ReShade → Add-ons → RenoDX → Upgrade settings.
Set `R10G10B10A2_UNORM` output to `output size`.
Switch Settings Mode from Simple → Advanced if the slider is locked, then restart.

**Wrong install path shown?**
Click **📁** on the card and choose **Change install folder**.

**Downloads failing with 404?**
Click **⟳ Full Rescan** to refresh the wiki mod list. The mod's snapshot URL may have moved.

---

## Links

- [RenoDX GitHub](https://github.com/clshortfuse/renodx)
- [RenoDX Mod Wiki](https://github.com/clshortfuse/renodx/wiki/Mods)
- [ReShade](https://reshade.me)
- [RenoDX Discord](https://discord.gg/gF4GRJWZ2A)
