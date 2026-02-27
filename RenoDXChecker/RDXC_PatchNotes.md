# RenoDXCommander (RDXC) — Patch Notes

## v1.2.6

### New Features

**Patch Notes dialog**
- On the first launch after updating to a new version, a Patch Notes dialog automatically appears showing the last 3 version changelogs.
- A "Patch Notes" link is now visible at the bottom-right of the app window. Clicking it opens the same dialog at any time.
- The dialog includes a scrollable view and a Close button.

**Anti-cheat warning**
- A persistent warning is now displayed in the center of the status bar: "⚠ Single-player only — ReShade with addon support may trigger anti-cheat in online/multiplayer games."
- The same warning has been added to both README.md and the Nexus Mods BBCode README.

### Fixes

**Favourite star icon visibility**
- The favourite star now uses two distinct icons: a filled gold ⭐ when favourited and a dim outlined ☆ when not. Previously both states looked identical because the string-based colour binding was not applied correctly by WinUI.

---

## v1.2.5

### Changes

**Updated bundled ReShade.ini**
- The bundled `ReShade.ini` has been updated with new defaults: Generic Depth and Effect Runtime Sync addons are now disabled, gamepad navigation is off, and the overlay key is set to Home.

**Auto-deploy reshade.ini to game folders**
- When ReShade is installed to a game folder, the bundled `reshade.ini` is now automatically copied alongside it — but only if a `reshade.ini` does not already exist in the game folder. This gives ReShade sensible defaults on first launch without overwriting any user customisations.
- The deployed `reshade.ini` is intentionally left in place if ReShade is later uninstalled, so user changes are preserved.

---

## v1.2.4

### New Features

**Favourites**
- Each game card now has a ⭐ star icon as the first element on the card header row (before 💬, game name, 🎯, ℹ, 🚫).
- Click the star to toggle a game as a favourite. Favourited stars are gold, unfavourited stars are dim.
- A new **⭐ Favourites** filter tab is the first option on the filter row. It shows all favourited games, including hidden games that have been favourited.
- Favourites are persisted in `game_library.json` across sessions.

### Fixes

**UE-Extended whitelisted games — ℹ info card**
- Games whitelisted for UE-Extended (Avowed, Lies of P, etc.) now always show the "In-game HDR must be turned ON" warning in the ℹ popup, regardless of whether the game has a specific wiki mod entry or uses the generic UE fallback. Previously, games with a wiki match (like Avowed) bypassed the native HDR branch in `BuildNotes` due to an early return.
- Generic Unreal addon game-specific fixes (from the wiki `_genericNotes` dictionary) are no longer shown for UE-Extended whitelisted games. These notes apply to the generic addon, not UE-Extended.

**Per-game shader mode — switching now fully syncs**
- Changing a game's shader mode in Overrides and reinstalling ReShade now correctly removes files from the previous mode before deploying the new one. Previously, `DeployToGameFolder` (add-only) was used instead of `SyncGameFolder` (prune + add), so switching from User to Minimum left custom shaders in place.
- The same fix applies to DC installs — shader folder syncs now prune before deploying.

**Per-game shader mode — DC Mode clarification**
- The shader mode dropdown tooltip now notes that per-game shader mode only applies when DC Mode is OFF (ReShade standalone). When DC Mode is ON, all DC-mode games share the DC global shader folder at `%LOCALAPPDATA%\Programs\Display_Commander\Reshade\`, so per-game overrides do not affect DC-mode games.

---

## v1.2.3

### UI Cleanup

**Streamlined game cards**
- The game status badge (Working, In Progress, Discord, Unknown, etc.) has been moved from the main card into the ℹ info popup. It now appears at the top-left of the info dialog. This reduces visual clutter on every card.
- An ℹ info button is now present on every game card (previously only on games with notes). It is placed second from the right, next to the hide button.
- Removed the verbose "this game uses the Generic Unreal Engine plugin..." installation guidance text from the info popup for Generic UE games.
- Removed the verbose "Generic Unity plugin..." information text from the info popup for Generic Unity games.
- Games on the UE-Extended whitelist now show "In-game HDR must be turned ON for UE-Extended to work correctly" instead of generic Unreal warnings.

**Condensed Overrides dialog**
- All toggle description text has been moved to hover tooltips on each toggle button.
- The dialog is significantly shorter and less cluttered — hover any toggle to read what it does.

**Per-game shader mode override**
- The shader management toggle in Overrides has been replaced with a dropdown that cycles between: **Global** (follow the header toggle), **Off** (no shaders), **Minimum** (Lilium only), **All** (all packs), and **User** (custom folder only).
- This allows individual games to use a different shader set than the global default — e.g. setting one game to "User" while the rest follow the global "Minimum" setting.
- Existing "exclude from shaders" settings are automatically migrated to "Off" mode.

**Skip update check**
- A new toggle in the About panel (Settings section) allows disabling the automatic update check on launch.
- When enabled, RDXC will not query GitHub for new versions and the update dialog will never appear.
- The setting persists across sessions.

### New Features

**Drag-and-drop game adding**
- Drag a game's `.exe` file directly onto the RDXC window to add it as a new game.
- RDXC automatically detects the engine type (Unreal, Unity, or Unknown) from the file structure.
- The game root folder and correct install path are inferred by walking up from the exe location. Recognises store markers from Steam, GOG, Epic, EA, and Xbox alongside engine layouts (`Binaries\Win64`, `UnityPlayer.dll`, etc.).
- The game name is inferred from folder structure and exe name (strips Unreal suffixes like `-Win64-Shipping`, cleans up underscores and camelCase).
- A confirmation dialog shows the detected name, engine, and install path — the name can be edited before adding.
- Duplicate detection prevents adding a game that already exists (checks both normalized name and install path).
- Existing ReShade, Display Commander, and RenoDX installations are automatically detected in the game folder.
- The game is matched against the wiki mod list and UE-Extended whitelist, just like auto-detected games.

### Fixes

- Fixed `icon.ico` not being copied to the build output directory.
- Shader deployment status now shown in the status bar during refresh ("Deploying shaders to installed games...").

---

## v1.2.2

### New Features

**Auto-update**
- RDXC now checks for updates automatically on launch by querying the GitHub release at `RankFTW/RenoDXChecker/releases/tag/RDXC`.
- The check runs silently in the background — no loading indicator or delay to the normal startup flow.
- If a newer version is found, a dialog appears showing the installed vs available version and offering "Update Now" or "Later".
- Choosing "Update Now" downloads `RDXC-Setup.exe` with a progress bar, then launches the installer and closes RDXC automatically.
- Choosing "Later" dismisses the dialog and RDXC continues normally.
- Version comparison uses the assembly version from .csproj (`AssemblyVersion`) against the version parsed from the GitHub release name (supports formats like `v1.2.3`, `RDXC-1.2.3`, etc.).
- If the update check fails (no internet, GitHub down, etc.) it is silently ignored — RDXC continues normally.

**Xbox / Game Pass game detection**
- RDXC now detects games installed via the Xbox app / Game Pass using the Windows `PackageManager` API — the same system-level API used by Playnite and other game library managers.
- This detects all installed Xbox/Game Pass games regardless of install location (custom folders, different drives, etc.).
- Games are identified by the presence of `MicrosoftGame.config` (GDK games) or by package heuristics for older UWP titles.
- Proper display names are pulled from the package metadata (not folder names).
- Handles the Xbox `Content\InternalName` subfolder structure for correct install path resolution.
- Falls back to filesystem scanning (.GamingRoot files, registry, common folder names) if the PackageManager API is unavailable.
- Xbox-detected games show an "Xbox" source badge on their card.

**Automatic new game detection on startup**
- RDXC now re-scans for games on every startup and merges newly found titles into the cached library.
- Previously, the cached `game_library.json` was used exclusively on normal startup, meaning newly installed games (especially Xbox/Game Pass titles) would not appear until the user manually deleted cache files or clicked Refresh.
- New games are now detected automatically. Games from a disconnected drive are preserved from the cache until the drive is reconnected.

**Bundled default ReShade.ini**
- A pre-configured `reshade.ini` is now bundled with RDXC and seeded into `%LOCALAPPDATA%\RenoDXCommander\inis\` on first launch.
- The file is only copied if `reshade.ini` does not already exist in the inis folder — an existing user-modified file is never overwritten.
- If the file is ever deleted from the inis folder, the bundled default is re-seeded on next launch.

**Extended UE Native HDR — automatic UE-Extended for known native HDR games**
- The following games now default to the UE-Extended addon automatically, without requiring the user to toggle it manually:
  Avowed, Lies of P, Lost Soul Aside, Hell is Us, Mafia: The Old Country, Returnal, Marvel's Midnight Suns, Mortal Kombat 1, Alone in the Dark, Still Wakes the Deep.
- These cards display **"Extended UE Native HDR"** instead of "Generic UE" next to the engine badge.
- The UE-Extended toggle is hidden on these cards — they always use UE-Extended and cannot be switched to the generic addon.
- The games are persisted into the `UeExtendedGames` set on first build so reinstalls pick up the correct URL automatically.

**32-bit mode per game (Overrides toggle)**
- Each game card now has a **"32-bit mode"** toggle in the Overrides dialog.
- When enabled, RDXC installs 32-bit variants for all supported components:
  - **ReShade**: installs `ReShade32.dll` (bundled) instead of `ReShade64.dll`.
  - **Display Commander**: downloads and installs `zzz_display_commander.addon32` from the pmnoxx releases endpoint.
  - **Unity addon**: downloads `renodx-unityengine.addon32` from the clshortfuse CDN.
  - **Unreal Engine addon**: shows a **WIP** disabled button — 32-bit UE support is not yet available.
- A **32-bit** badge appears next to the source/engine badges on the card when 32-bit mode is active.
- The setting is persisted in `settings.json` under `Is32BitGames`.
- Only enable this if you know the specific game process is 32-bit.

**Unity addon defaults to 64-bit on main card**
- Unity generic cards now always show a single install button targeting `renodx-unityengine.addon64`.
- The previous dual 64-bit / 32-bit install row is removed. 32-bit Unity installs are done via the Overrides → 32-bit mode toggle.

**Foreign dxgi.dll detection and warning**
- RDXC now identifies whether an existing `dxgi.dll` belongs to ReShade, Display Commander, or something else (e.g. DXVK, Special K, ENB).
- Detection uses strict positive identification only: exact size match against known staged/cached binaries AND binary string scanning for definitive markers (e.g. "ReShade", "reshade.me", "display_commander"). A file must positively match — size heuristics alone are never sufficient.
- The previous heuristic (anything >2MB = ReShade, anything <2MB = DC) has been replaced. Random or third-party dxgi.dll files are now correctly classified as "Unknown".
- **Disk scan**: unknown dxgi.dll files are no longer falsely attributed to DC or ReShade during the initial game scan. Only positively identified files create install records.
- **Manual install/reinstall**: if an unrecognised dxgi.dll is found, RDXC shows a warning dialog asking for confirmation before overwriting.
- **Update All**: games with unrecognised dxgi.dll files are automatically skipped. The update button remains purple to indicate the update is still pending — the user can install manually per game and confirm the overwrite.
- **Uninstall crash protection**: DC and ReShade uninstall operations are now wrapped in try/catch to prevent app crashes when records point to unexpected files.

**Default shader mode changed to Minimum**
- The Shaders toggle now defaults to **Minimum** (Lilium only) for new installations, instead of Off. Existing users who have previously saved a shader mode are unaffected.

### Bug Fixes

**DC Mode 32-bit — wrong executables installed**
- When DC Mode was enabled and a game was flagged as 32-bit, RDXC was installing the 64-bit ReShade and DC executables instead of the 32-bit versions.
- Fix: In DC Mode + 32-bit, ReShade now installs as `ReShade32.dll` and Display Commander installs the 32-bit binary as `dxgi.dll`.

**Cyberpunk 2077 — wrong install folder**
- RDXC was installing mod files to the Cyberpunk 2077 root folder instead of the correct `\Cyberpunk 2077\bin\x64\` subdirectory.
- Fix: Added per-game install path overrides. Cyberpunk 2077 now correctly resolves to `bin\x64`.

**Overrides dialog overflow**
- The Overrides dialog content was growing beyond the visible area on smaller screens, with toggles cut off at the bottom.
- Fix: Dialog content is now wrapped in a ScrollViewer with a max height of 480px and auto scroll bar.

**UE-Extended whitelist not showing correctly for some games**
- Games on the UE-Extended whitelist (e.g. Avowed, Lost Soul Aside™) were not showing as UE-Extended in the row next to the Steam badge, and were still showing Nexus links.
- Root cause 1: Trademark symbols (™, ®, ©) in store-detected game names prevented matching against the whitelist.
- Root cause 2: UE-Extended whitelist games were not having their Nexus/Discord links stripped, causing them to show external buttons instead of install/update/reinstall.
- Root cause 3: Manually added games (via Add Game button) were not processed through the NativeHdr/UE-Extended whitelist logic at all — missing `UseUeExtended`, `IsNativeHdrGame`, `DcModeExcluded`, `ExcludeFromUpdateAll`, `ExcludeFromShaders`, `Is32Bit` fields, install path overrides, and the UE-Extended mod swap.
- Fix: UE-Extended whitelist matching now strips ™/®/© and uses normalised comparison. Whitelisted games have Nexus/Discord links nullified, ensuring the install/update/reinstall buttons appear. The `AddManualGame` method now applies the same full card-building pipeline as `BuildCards`, including NativeHdr detection, UE-Extended whitelist, per-game settings, and install path overrides.

**reshade-shaders folder not deleted on ReShade uninstall**
- When ReShade was uninstalled via RDXC, the RDXC-managed `reshade-shaders` folder in the game directory was not being removed, and `reshade-shaders-original` was not being restored.
- Root cause: `Uninstall()` calls `RestoreOriginalIfPresent()`, which only renames the original back when the managed folder is already gone — but nothing was removing the managed folder before that call.
- Fix: `UninstallReShade` now explicitly calls `ShaderPackService.RemoveFromGameFolder()` before delegating to `Uninstall()`. This deletes the managed folder (identified by the `Managed by RDXC.txt` marker), then `RestoreOriginalIfPresent()` correctly renames `reshade-shaders-original` back to `reshade-shaders`.
- DC uninstall is unaffected — when DC is installed, shaders live in the DC global path, not the game-local folder.

**UE-Extended update detection never flagging**
- Games using the UE-Extended addon (hosted on `marat569.github.io`) were never showing "Update Available" even when an update existed.
- Root cause: the `marat569.github.io` CDN does not serve a reliable `Content-Length` header on HTTP HEAD requests. The existing update check falls back to comparing the local file size against the stored install-time size — but since the local file was installed from that same download, they always match, so the check always returned false.
- Fix: a `_downloadCheckUrls` set identifies CDNs where HEAD is unreliable. For URLs in this set, `CheckForUpdateAsync` delegates to a new `CheckForUpdateByDownloadAsync` method which actually downloads the remote file to a temp path and compares its byte size against the installed file. If the sizes differ, a real update is detected; the downloaded file is moved into the cache so the next Install call reuses it without re-downloading. If no update, the temp file is deleted.

----

## v1.2.1

### New Features

**🎨 Per-game shader management exclusion**
- New toggle in each game's Overrides dialog: **"Exclude from shader management"**.
- When enabled, RDXC never creates, modifies or deletes the `reshade-shaders` folder for that game — the user manages shaders for it manually.
- Toggle is persisted in `settings.json` under `ShaderExcluded` and honoured by all install, reinstall, and Update All operations.

**🎨 reshade-shaders folder preservation**
- When RDXC deploys shaders to a game directory and a `reshade-shaders` folder already exists that was **not** placed by RDXC, it is renamed to `reshade-shaders-original` rather than deleted.
- RDXC identifies its own folders by a `Managed by RDXC.txt` marker file inside the `reshade-shaders` folder.
- When ReShade or Display Commander is uninstalled via RDXC, the managed `reshade-shaders` folder is removed and `reshade-shaders-original` (if present) is automatically renamed back to `reshade-shaders`.

**🎨 Refresh syncs shader deployments globally**
- Clicking ↻ Refresh now calls `SyncShadersToAllLocations` on a background thread after rebuilding the card list.
- Changing the Shaders mode button (Off / Minimum / All / User) and pressing Refresh adds or removes shaders from every installed game location and the DC global folder accordingly.
- Files belonging to de-selected packs are pruned from `Display_Commander\Reshade\Shaders`, `Display_Commander\Reshade\Textures`, and each game's `reshade-shaders\` folder.

**🎨 Shader mode — User**
- New 4th state on the Shaders cycle button: **User**.
- In User mode, no auto-downloaded packs are deployed. RDXC copies whatever you have placed in `%LocalAppData%\RenoDXCommander\reshade\Custom\Shaders\` and `\Textures\` to each game and/or the DC global folder.

**⬆ Update All button**
- New button on the header bar, positioned between DC Mode and Shaders.
- Clicking opens a flyout with three options: **Update all RenoDX**, **Update all ReShade**, **Update all Display Commander**.
- Correctly respects the global DC Mode toggle and per-game DC Mode exclusion.
- Button background is **purple** when any update is detected; reverts to dim/idle when nothing needs updating.

**Exclude from Update All (per-game override)**
- New toggle in each game's Overrides dialog: **"Exclude from Update All"**.
- When on, that game is skipped by all three Update All actions.
- Setting is persisted in `settings.json` under `UpdateAllExcluded`.

**HDR Shader Pack manager — 7 packs, automatic download**
- RDXC downloads and maintains 7 HDR ReShade shader packs on every launch:
  - **ReShade HDR Shaders** by EndlesslyFlowering (Lilium) — GitHub Releases
  - **PumboAutoHDR** by Filoppi (Pumbo) — GitHub Releases
  - **smolbbsoop shaders** by smolbbsoop — GitHub main branch
  - **Reshade Simple HDR Shaders** by MaxG2D — direct release asset
  - **reshade-shaders** by clshortfuse — GitHub main branch
  - **potatoFX** by CreepySasquatch — GitHub main branch
  - **reshade-shaders (slim)** by crosire — GitHub slim branch
- Version tracking: GitHub Release packs use asset filename; direct-URL packs use ETag/Last-Modified.
- If the cache zip or extracted files are deleted, the pack is re-downloaded on next launch.
- Full subdirectory structure inside `Shaders/` and `Textures/` is preserved.

**🎨 Shaders cycle button**
- New **🎨 Shaders** button in the header bar (between Update All and Add Game).
- Cycles: **Off** (default) → **Minimum** (Lilium only) → **All** → **User** → Off.
- Setting is persisted between sessions.

### Bug Fixes

**DC dxgi.dll deletion bug**
- Fixed: DC mode-switch cleanup now checks both DC and ReShade records before deleting any opposing file.

**ReShade & Display Commander disk detection**
- RDXC now scans game folders for existing ReShade and DC installations on every refresh.

**Subdirectory structure flattening in shader extraction**
- Fixed: shader archives no longer have their folder structure flattened — `Shaders/HDR/Foo.fx` stays as `Shaders/HDR/Foo.fx`.

### Changes

- All version references updated to v1.2.1
- All remaining `RenoDXChecker` display references replaced with `RenoDXCommander`
- Compiled exe renamed to `RenoDXCommander.exe`
- Assembly version and file version set to `1.2.1.0`
- Bottom status bar text and "here" link removed from UI
- README disclaimer updated to reflect ReShade 6.7.2 is bundled (not downloaded from reshade.me)
