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
| 🔄 Update detection | Checks snapshot dates on launch and flags games with newer versions available |
| 🎮 Generic engine mods | Offers Generic Unreal Engine and Generic Unity Engine plugins for unlisted games |
| ℹ Game notes | Shows per-game setup notes pulled live from the RenoDX wiki |
| 💬 Discussion links | Games with a wiki discussion link show a chat button that opens it in the browser |
| 🌐 Extra links | Shows Nexus Mods / Discord links alongside the install button when available |
| ➕ Manual add | Add any game manually if it wasn't auto-detected |
| 🚫 Hide games | Hide games you don't need from the list; toggle them back anytime |
| 🔧 Unity 32/64 split | Separate install buttons for 32-bit and 64-bit Unity games |
| 📦 Installed badge | Shows the installed addon filename on each card |
| 🔎 Filter tabs | Filter by All Games, Installed, Hidden, Unity, Unreal, or Other |
| 🗂 Name mapping | Use the Tune button to add fuzzy-match overrides for games with unusual names |
| 💾 Window memory | Both the main window and About window remember their size and position between sessions |

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
Use the search bar or scroll through the cards. Use the filter tabs to narrow the list
by install status or engine type.

### 4. Check the ℹ and 💬 buttons before installing
- **ℹ** — shows game-specific notes and known warnings (especially important for generic engine mods)
- **💬** — opens the RenoDX discussion thread for that game, which may include extra setup steps

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
| 💬 | Open the RenoDX discussion thread for this game |
| 🚫 | Hide / unhide this game in the list |
| 📁 | Open install folder in Explorer / change the folder |
| 🌐 | Open Nexus Mods or Discord page for this mod |

---

## Notes on Generic Mods

When a game doesn't have its own specific RenoDX mod, the app offers generic engine plugins:

### Generic Unreal Engine
- File: `renodx-unrealengine.addon64`
- Requires ReShade installed in the same folder as the game's `*-Win64-Shipping.exe`
- Common issues and fixes are shown in the ℹ dialog (black screen fix, DLSS FG fix)
- Always check the 💬 button if available — game-specific workarounds may be documented there

### Generic Unity Engine
- Files: `renodx-unityengine.addon64` / `renodx-unityengine.addon32`
- Install next to `UnityPlayer.dll`
- Use 64-bit for modern games; 32-bit only for older 32-bit games

---

## Update Checking

On each launch the app does a background HEAD request against each installed mod's snapshot URL.
If the remote file is newer than when you last installed, the card shows **⬆ Update Available**
and the action button changes to **⬆ Update**.

---

## Adding Games Manually

If a game wasn't auto-detected (e.g. installed to a custom path or via a launcher not yet supported):

1. Click **➕ Add Game** in the header
2. Enter the game name **exactly as it appears on the RenoDX wiki mod list**
3. Pick the game's install folder

The app will attempt to match the name against the wiki and detect the engine automatically.
If the install path can't be determined, click **📁** on the card to set it manually.

---

## Hiding Games

Click 🚫 on any card to hide that game from the main list. Hidden games are tracked separately
and won't appear unless you click **👁 Show Hidden** in the filter bar. Clicking 🚫 again on
a hidden game (while Show Hidden is active) will unhide it.

---

## Fuzzy Match Tuning

Some games have slightly different names in your library versus the RenoDX wiki
(e.g. edition suffixes, regional titles). Click the **Tune** button in the header to add a
custom name mapping: enter the detected name on the left and the wiki key on the right.

---

## Data Storage

All app data is stored locally in `%LocalAppData%\RenoDXChecker\`:

| File | Contents |
|------|---------|
| `game_library.json` | Detected games, hidden games, manually added games, addon scan cache |
| `installed.json` | Install records with file paths and snapshot dates |
| `window_main.json` | Main window size and position |
| `window_about.json` | About window size and position |

---

## Troubleshooting

**Game not detected?**
Use **➕ Add Game** to add it manually. Enter the game name as it appears on the
[RenoDX wiki](https://github.com/clshortfuse/renodx/wiki/Mods) and pick the install folder.
If the name format doesn't match, use **Tune** to add a name mapping.

**Mod installed but ReShade doesn't load it?**
Make sure ReShade is installed in the *same folder* as the `.addon64` file.
For Unreal games this is `Binaries\Win64`, not the game root.

**Black screen on launch (Unreal Engine)?**
Open ReShade → Add-ons → RenoDX → Upgrade settings.
Set `R10G10B10A2_UNORM` output to `output size`.
Switch Settings Mode from Simple → Advanced if the slider is locked, then restart.

**DLSS Frame Generation flickering or not working?**
Replace your DLSSG DLL with the older 3.8.x version (locks to FG ×2), or check the
RenoDX Discord for the DLSS FIX beta.

**Wrong install path shown?**
Click **📁** on the card and choose **Change install folder**.

**Downloads failing with 404?**
Click **↻ Refresh** to re-fetch the latest wiki mod list. The snapshot URL may have changed.

**No install button on a generic Unreal game?**
The game may have a discussion link (💬) but no specific mod. The install button uses the
Generic Unreal Engine plugin — make sure the card doesn't show as External Only. If it does,
click **📁** to set the correct install folder and try refreshing.

**Window opens very large on first launch?**
On first launch there is no saved size, so the app opens at its built-in default (1280×880).
Resize it to your preference and it will remember that size from then on.

---

## Links

- [RenoDX GitHub](https://github.com/clshortfuse/renodx)
- [RenoDX Mod Wiki](https://github.com/clshortfuse/renodx/wiki/Mods)
- [ReShade](https://reshade.me)
- [RenoDX Discord](https://discord.gg/gF4GRJWZ2A)
