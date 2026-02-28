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
- Luma is a DX11 modding framework by Pumbo (Filoppi) that adds HDR support and graphics improvements to games via the ReShade addon system. RDXC now integrates with the Luma wiki to detect compatible games and manage Luma mod installation.
- Luma support is **disabled by default**. Enable it from **About → Settings → Luma (Experimental)**.
- Disabling the Luma toggle in Settings removes all Luma files from all games and exits Luma mode everywhere.

**Game rename**
- The game name field in the **🎯 Overrides** dialog is now editable. Renames persist across Refresh and app restarts.

**Drag-and-drop addon install**
- Dragging a `.addon64` or `.addon32` file onto the RDXC window installs it for a chosen game with confirmation.

### Bug Fixes
- Fixed RenoDX update detection (inverted logic suppressing real updates).
- Fixed DC update button never turning purple (DcRecord not assigned during BuildCards).
- Fixed Update All button colour not updating after checks.
- Fixed Luma false matching (substring → exact normalised equality).
- Fixed Luma info notes showing all games below current one.
- Fixed Luma files not removed on mode toggle or feature disable.
- Fixed Luma installing all shader packs instead of only Lilium.
- Improved EA App game detection (registry, publisher keys, folder scanning).
