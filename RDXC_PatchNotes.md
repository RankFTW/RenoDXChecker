## v1.3.2

### Bug Fixes

**Game rename persistence for games with folder overrides**
- Fixed games with custom folder overrides losing their rename after a Refresh. The rename is now stored under both the overridden and original install paths, so it survives the Init ordering where renames are applied before folder overrides.

### Changes

**Reset button in Overrides dialog**
- A new ↩ Reset button next to the Game Name and Wiki Name fields restores the game name to the original store-detected name and clears the wiki name mapping. Saving with an empty wiki field now also removes any existing mapping.

**Luma wiki name matching respects name mappings**
- The wiki name mapping (set in the Overrides dialog) now also applies to Luma mod matching. Previously, only RenoDX wiki matching consulted name mappings — Luma games with non-standard store names had to match exactly.

---

## v1.3.1

### Bug Fixes

**Settings persistence across updates**
- Fixed custom game folders, game renames, and DLL naming overrides being reset on every app launch. Observable-property change handlers were triggering a settings save before all fields had finished loading, overwriting persisted values with empty defaults.

**Foreign DLL protection during DC Mode switches**
- Switching DC Mode on no longer deletes foreign `dxgi.dll` or `winmm.dll` files (e.g. DXVK, Special K). Foreign DLLs are renamed to `.original` and automatically restored when DC vacates the slot (mode off or mode change).
- The same backup/restore logic applies during DC and ReShade installs and uninstalls.

**False Unity engine detection from artbook folders**
- Games bundling a digital artbook viewer (or similar bonus-content subfolder) with its own exe or `UnityPlayer.dll` are no longer misdetected as Unity. Artbook, soundtrack, manual, and bonus-content folders are now excluded from engine heuristic searches.

**ReShade button disabled during DC Mode**
- The ReShade install/reinstall button on each game card is now greyed out when DC Mode is active for that game, preventing accidental per-game ReShade installs that would conflict with the shared DC Mode ReShade path.

### Changes

**Faster startup — engine and addon detection caching**
- Engine detection results (Unreal, Unity, etc.) and resolved install paths are now cached in the game library. On subsequent launches, expensive filesystem traversals are skipped entirely for previously scanned games.
- Addon file scan results are cached by filename. Games with a known addon on disk skip the recursive directory search on subsequent launches.
- The Unreal Engine 3 legacy detection heuristic now checks cheap markers first (TAGame folder, Engine\Config\BaseEngine.ini) before any file searches, and uses depth-limited scans (3–4 levels) instead of full-tree traversals.
- The library save after card building now runs on a background thread instead of blocking the UI.

**↻ Full Refresh button**
- A new compact ↻ button next to the Refresh button clears all engine, path, and addon caches and re-scans everything from disk. Use this when game files have changed or detection results seem stale.

**Verbose Logging toggle**
- New toggle in About → Settings: **Verbose Logging**. When enabled, all activity is continuously logged to `rdxc_log.txt` in the logs folder. The log auto-rotates at 5 MB. Useful for diagnosing issues — send the log file to the developer alongside any bug reports.

---

## v1.3.0

### New Features

**Automatic ReShade updates from reshade.me**
- RDXC no longer bundles ReShade DLLs. Instead, it downloads the latest ReShade with full addon support directly from reshade.me on every launch.
- The latest version is detected by parsing the reshade.me homepage for the addon download link. The setup exe is downloaded to the AppData cache, and `ReShade64.dll` / `ReShade32.dll` are extracted using 7-Zip (bundled).
- If a newer version is available than what is currently installed in your games, a purple **⬆ Update ReShade** button appears — just like DC and RenoDX updates.
- Old installer exes are automatically cleaned up after a successful update. The version is tracked in a local marker file so redundant downloads are skipped.
- If the reshade.me check fails (e.g. offline), RDXC continues using whatever DLLs are already cached.

**DC Mode 2 (winmm.dll)**
- DC Mode now has three levels: Off → Mode 1 (`dxgi.dll`) → Mode 2 (`winmm.dll`) → Off.
- Mode 2 installs Display Commander as `winmm.dll` instead of `dxgi.dll`, freeing the `dxgi.dll` slot for other tools or proxy chains.
- The DC Mode button in the header cycles through all three states. Button label, colours, and tooltip update dynamically.
- A new **⚙ Deploy DC Mode** button next to the DC Mode button applies DC Mode file renames immediately without triggering a full library rescan.
- File renames during DC Mode switches are ordered to avoid `dxgi.dll` collisions: when turning ON, ReShade is renamed first to free `dxgi.dll`; when turning OFF, DC is renamed first.

**Per-game DC Mode override**
- The **🎯 Overrides** dialog now includes a DC Mode dropdown with four options: **Follow Global**, **Force Off**, **Force DC Mode 1**, and **Force DC Mode 2**.
- Per-game overrides take precedence over the global DC Mode level for both installs and DC Mode deploy operations.
- Saving the override applies the DC Mode switch for that game immediately — no Refresh needed.

**Xbox / Game Pass detection**
- RDXC now detects Xbox and Game Pass games via the Windows PackageManager API. Games with a `MicrosoftGame.config` are identified automatically; a filesystem fallback scans `.GamingRoot` files, registry, and common folder names.
- Xbox games appear alongside Steam, GOG, Epic, and EA App titles with an **Xbox** source badge.

**🎨 Shader deploy button**
- A new **🎨** button next to the Shaders cycle button deploys the current shader mode to all installed games immediately — no Refresh required.
- Saving a per-game shader override in the 🎯 Overrides dialog also triggers a shader deploy for that game automatically.

**Foreign winmm.dll protection**
- When installing Display Commander as `winmm.dll` (DC Mode 2), RDXC now checks whether an existing `winmm.dll` belongs to another tool using binary signature scanning — the same protection that already existed for `dxgi.dll`.
- If the file cannot be positively identified as Display Commander, a confirmation dialog asks whether to overwrite it. During Update All, unidentified foreign `winmm.dll` files are silently skipped.

### Bug Fixes

**Luma-only games showing Discord link when Luma is off**
- Games that only have a Luma mod (no RenoDX wiki mod) no longer show the Discord link button when the Luma feature is disabled.

**UE3 Win32 detection**
- Fixed Unreal Engine 3 games with only a `Binaries\Win32` folder not being detected as UE legacy. The engine heuristic now checks for `Binaries\Win32` alongside `Win64`.

**reshade.ini restoration for standalone ReShade installs**
- When ReShade is installed without DC Mode and no `reshade.ini` exists in the game folder, RDXC now copies the bundled INI automatically — matching the behaviour that already existed for DC Mode installs.

**DC Mode DLL override fix**
- Games with a custom DLL naming override that are also excluded from DC Mode no longer have their ReShade file incorrectly renamed during DC Mode deploy operations.

**Settings resilience across updates**
- All user settings (game renames, DLL overrides, folder overrides, hidden/favourite state) now load independently per-key. A single corrupt setting no longer wipes all other preferences.
- Hidden games and favourites are now also persisted in settings.json alongside all other preferences.

**Update check file lock errors**
- Fixed "file is being used by another process" errors during RenoDX UE-Extended and Display Commander update checks. Concurrent checks now use unique temp files.

### Changes

**Header button layout**
- Reordered header buttons: DC Mode → ⚙ Deploy DC → 🎨 Shaders → 🎨 Deploy Shaders → ⬆ Update All → ➕ Add Game → 💬 Support → ↻ Refresh → About.
- About button icon changed to 🧁.

**Header button tooltips**
- Added tooltips to the Add Game, Refresh, and About buttons for consistency with other header buttons.

**Blacklisted non-game apps**
- Added QuickPasta, Apple Music, DSX, PlayStation®VR2 App, SteamVR, Telegram Desktop, and Windows to the permanent exclusion list.
- System paths (e.g. `C:\WINDOWS`) are now filtered out from EA App detection to prevent OS directories appearing as games.

**7-Zip bundled**
- 7z.exe and 7z.dll are now bundled with RDXC for ReShade installer extraction. 7-Zip by Igor Pavlov, licensed under LGPL-2.1 / BSD-3-Clause.

---

## v1.2.9

### New Features

**DLL naming override**
- New per-game override in the **🎯 Overrides** dialog that lets you customise the filenames ReShade and Display Commander are installed as.
- A **📝 DLL naming override** toggle enables the feature. Two side-by-side text boxes set the ReShade and DC filenames. The boxes are greyed out until the toggle is switched on.
- When DLL override is enabled, existing ReShade and DC installations are automatically removed. The game is also automatically excluded from DC Mode, Update All, and global shader deployment — shader mode must be set manually.
- When DLL override is toggled off, the custom-named files are removed from the game folder.
- Works in both normal and 32-bit mode. Default placeholder names adjust based on 32-bit mode.
- Override settings persist across app restarts and game renames.

**Install folder override**
- Changing a game's install folder via the 📁 menu now persists across app restarts and Refresh. The chosen folder is used for all installations (ReShade, DC, RenoDX) and for "Open in Explorer".
- The **🔄 Reset folder / Remove game** menu option resets auto-detected games back to their original store-detected folder. For manual games, it removes the game entirely as before.

**ReShade 6.7.3**
- Bundled ReShade updated from 6.7.2 to 6.7.3. On first launch after updating, the new DLLs are automatically staged to the AppData cache. Games with ReShade installed will show a purple update notification.

### Bug Fixes

**Renamed games duplicating after app update**
- Fixed games appearing twice (renamed + original) after updating the app. The library merge now deduplicates by both normalized name and install path, preventing the cached renamed version from co-existing with the freshly-detected original.

### Changes

**Overrides dialog layout**
- Game name and Wiki name fields are now side-by-side in matching format.
- Removed description text at top.
- New layout order: Game name / Wiki name → DLL override → Exclude from wiki → Exclude from DC → Exclude from Update All → 32-bit mode → Shader mode.

---

## v1.2.8

### New Features

**Luma Framework integration (Experimental)**
- Luma support is disabled by default. Enable it from About → Settings → Luma (Experimental).

**Game rename**
- The game name field in the Overrides dialog is now editable. Renames persist across Refresh and app restarts.

**Drag-and-drop addon install**
- Dragging a .addon64 or .addon32 file onto the RDXC window installs it for a chosen game with confirmation.

### Bug Fixes
- Fixed RenoDX update detection (inverted logic suppressing real updates).
- Fixed DC update button never turning purple (DcRecord not assigned during BuildCards).
- Fixed Update All button colour not updating after checks.
- Fixed Luma false matching, info notes overflow, uninstall on mode toggle, shader pack install.
- Improved EA App game detection (registry, publisher keys, folder scanning).
