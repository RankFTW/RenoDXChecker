# RenoDX Mod Manager

A WinUI 3 desktop app that automatically detects your installed games, checks for available RenoDX HDR mods, installs them in the correct location, and checks for updates on launch.

## Features

| Feature | Details |
|---|---|
| **Auto game detection** | Scans Steam, GOG, Epic Games, and EA App libraries automatically |
| **Mod matching** | Fuzzy-matches your games against the full RenoDX wiki mod list |
| **1-click install** | Downloads `.addon64`/`.addon32` directly from Snapshot URLs |
| **Update checking** | Compares remote file size/date against local install on every launch |
| **Manual folder picker** | For games not auto-detected, set the folder yourself |
| **Filter tabs** | All / On My PC / Installed / Updates Available / Available |
| **Status tracking** | Per-game status: Not Detected → Available → Installed → Update Available |
| **Uninstall** | Removes the addon file and clears the install record |

## How RenoDX Mods Work

RenoDX mods are ReShade addons (`.addon64` or `.addon32` files). To use them:
1. Install [ReShade with Addon Support](https://reshade.me/downloads/ReShade_Setup_Addon.exe) in your game folder
2. Place the `.addon64` file in the **same folder as ReShade** (your game's exe folder)
3. Launch the game → press **Home** to open ReShade → find RenoDX in the Addons tab

This app automates step 2 — it downloads and places the correct file for you.

## Requirements

- **Windows 10** 1809 (build 17763) or later / Windows 11
- **.NET 8 SDK** — https://dotnet.microsoft.com/download/dotnet/8.0
- **Windows App SDK 1.5** — installs automatically via NuGet
- **Visual Studio 2022** (v17.8+) with **Windows App SDK** workload, OR
- **dotnet CLI** — no IDE needed

## Building & Running

### Option A: Visual Studio 2022
1. Open `RenoDXChecker.sln`
2. Set platform to **x64**
3. Press **F5** → Done

### Option B: Command Line
```powershell
cd RenoDXChecker
dotnet restore
dotnet build -c Release
dotnet run --project RenoDXChecker/RenoDXChecker.csproj -c Release
```

### Option C: Publish as single EXE
```powershell
dotnet publish RenoDXChecker/RenoDXChecker.csproj -c Release -r win-x64 --self-contained false
```

## NuGet Packages

| Package | Purpose |
|---|---|
| `Microsoft.WindowsAppSDK 1.5` | WinUI 3 framework |
| `HtmlAgilityPack 1.11` | Parses the GitHub wiki HTML table |
| `CommunityToolkit.Mvvm 8.2` | MVVM source generators (`[ObservableProperty]`, `[RelayCommand]`) |
| `Microsoft.Win32.Registry 5.0` | Cross-platform registry access for game detection |

## Architecture

```
RenoDXChecker/
├── Models/
│   └── GameMod.cs          — Data models: GameMod, InstalledModRecord, DetectedGame
├── Services/
│   ├── WikiService.cs      — HTTP fetch + HTML parse of the RenoDX wiki table
│   ├── GameDetectionService.cs — Steam/GOG/Epic/EA scanning + fuzzy game matching
│   └── ModInstallService.cs — Download, install, update-check, uninstall + local DB
├── ViewModels/
│   ├── MainViewModel.cs    — Orchestrates all services, filter state, commands
│   └── GameCardViewModel.cs — Per-game observable state for the UI card
├── Converters/
│   └── ValueConverters.cs  — XAML value converters
├── MainWindow.xaml(.cs)    — Two-panel UI: filter bar + scrollable game card grid
└── App.xaml(.cs)           — Application entry, resources
```

## Data Storage

Install records are saved at:
```
%LOCALAPPDATA%\RenoDXChecker\installed.json
```

This tracks:
- Which game folder each mod was installed into
- The file hash at install time
- The snapshot URL used (for update checking)
- Install timestamp (compared against HTTP `Last-Modified` header)

## Troubleshooting

**Game not detected:**
Click the "📁 Set folder" button on the card and navigate to the game's exe directory manually.

**Install fails:**
Make sure you've selected the folder that contains the game's `.exe` file (same folder where ReShade is or will be installed). Some games put their exe in a subfolder like `Binaries/Win64/`.

**Mod shows "Not Detected" but game is installed:**
This can happen if Steam/GOG registry entries are missing. Use "📁 Set folder" to manually set the path.

**"In Progress" status (🚧) on wiki:**
The mod exists but may have known issues. It's still installable — check the [RenoDX Discord](https://discord.gg/gF4GRJWZ2A) or [wiki](https://github.com/clshortfuse/renodx/wiki/Mods) for notes.
