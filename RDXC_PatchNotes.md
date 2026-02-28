## v1.2.8

## New Features

**Luma Framework integration (Experimental)**
- Luma is a DX11 modding framework by Pumbo (Filoppi) that adds HDR support and graphics improvements to games via the ReShade addon system. RDXC now integrates with the Luma wiki to detect compatible games and manage Luma mod installation.
- Luma support is **disabled by default**. Enable it from **About → Settings → Luma (Experimental)** to reveal all Luma UI across the app.
- When enabled, compatible games show a green **Luma** toggle badge on the card tag line. Clicking the badge puts the game into Luma mode.
- Luma mode is **mutually exclusive** with RenoDX, ReShade, and Display Commander. Enabling Luma on a game automatically uninstalls all three and hides their install rows. The only available action is Install Luma.
- Installing Luma extracts the mod's zip contents to the game folder and also deploys the bundled `reshade.ini` and the Lilium HDR shader pack alongside it. Everything is self-contained.
- Uninstalling Luma or switching out of Luma mode removes all installed files including the mod files, reshade.ini, and the shader pack folder.
- Luma toggle state is persisted per-game across sessions.
- The **ℹ** info popup shows Luma-specific notes (status, author, special notes, and feature notes fetched from the wiki) when a game is in Luma mode.
- A new **Luma** filter tab appears in the header bar showing all games with Luma mods available.
- The **🎯 Overrides** dialog disables "Exclude from wiki" and "32-bit mode" when a game is in Luma mode.
- Display Commander is forced to addon mode (never dxgi.dll) on Luma-compatible games when Luma is active.
- Luma mod data is fetched at runtime from the Luma wiki — nothing is hardcoded.

**Game rename**
- The game name field in the **🎯 Overrides** dialog is now editable. Changing the name renames the game everywhere: card display name, all settings (hidden, favourites, exclusions, shader mode, 32-bit, DC mode, Luma, etc.), all persisted install records (RenoDX, DC, ReShade, Luma), and the library file.
- Renames are keyed by install path and persist across Refresh and app restarts.

**Drag-and-drop addon install**
- Dragging a `.addon64` or `.addon32` file onto the RDXC window now opens an install dialog. A game picker (sorted alphabetically) lets you choose which game to install the addon to. RDXC attempts to auto-select a matching game based on words in the addon filename.
- A confirmation dialog shows the addon filename, game name, and install path. If an existing RenoDX addon is already present in the game folder, the dialog warns that it will be replaced.
- On confirm, existing RenoDX addon files are removed (Display Commander addons are preserved) and the new addon is copied in.

## Bug Fixes

**RenoDX update detection**
- Fixed an inverted logic check in `ModInstallService.CheckForUpdateAsync` that suppressed real updates. When the remote file size differed from the stored install-time size, the method incorrectly returned "no update" if the local file still matched the original size. This check has been removed — a size difference between remote and stored now correctly triggers an update notification.

**Display Commander update detection**
- Fixed DC update button never turning purple. The `DcRecord` property was not being assigned to game cards during the main `BuildCards` path — it was only set after explicit install/uninstall operations. Cards now receive their `DcRecord` during initial construction so the update check runs correctly on app launch.
- Added a robust three-tier update check for DC: HEAD request, Range GET fallback, and full download with byte-level comparison. This handles GitHub redirect URLs that may not return Content-Length on HEAD, and also catches same-size-different-content updates.
- Disk-detected DC records now populate `SourceUrl` and `RemoteFileSize` so update detection works for games where DC was installed outside RDXC.

**Update All button colour**
- The Update All button now correctly turns purple when any game has an update available. The button colour property change notifications were missing after update checks completed.

**Luma false matching**
- Fixed games with similar names incorrectly showing Luma as available (e.g. "Nioh 3" matching "Nioh"). Replaced substring-based matching with exact normalised equality matching.

**Luma info notes overflow**
- Fixed the ℹ info popup showing feature notes from every game below the current one on the wiki page. The feature notes extractor now collects all section anchors first and stops reading at the boundary of the next game's section.

**Luma uninstall on mode toggle**
- Fixed Luma files not being removed when switching out of Luma mode. Added `ShaderPackService.RemoveFromGameFolder` call during uninstall for proper shader cleanup. Added fallback cleanup path that scans for and removes known Luma artifacts even when the install record is missing or incomplete.

**EA App game detection**
- Added registry-based detection scanning `HKLM\Software\Wow6432Node\Origin Games` and publisher-specific keys (`EA Games`, `Criterion Games`, `Respawn`, `BioWare`, `DICE`, `PopCap`, `Ghost Games`, `Electronic Arts`) for `Install Dir` values.
- Added default EA Games folder scanning and EA Desktop local config path discovery for games installed to custom locations.

## Changes

**Overrides dialog**
- 32-bit mode toggle moved above the shader mode dropdown.
- Game name field label changed from "Detected game name" to "Game name (editable)".
