# RHI — ReShade HDR Installer

One app to manage HDR mods across your entire PC game library. RHI auto-detects games from eight storefronts, installs ReShade, RenoDX, frame limiters, OptiScaler, and more — all with one click per game.

![RHI](screenshots/game_view.png)

> **⚠ Single-player only.** RHI installs ReShade with addon support, which may trigger anti-cheat in online games. Uninstall before playing multiplayer.

## Why RHI?

- **Instant startup** — your game library loads from cache immediately. Background scanning merges new games silently.
- **8-store detection** — Steam, GOG, Epic, EA App, Ubisoft Connect, Xbox/Game Pass, Battle.net, and Rockstar. No manual setup needed.
- **One-click everything** — install, update, or remove ReShade, RenoDX, ReLimiter, Display Commander, OptiScaler, RE Framework, and Luma Framework per game.
- **Keeps things in sync** — Update All checks every component across every game. One button, done.
- **41 shader packs** — global or per-game shader selection with automatic dependency resolution.
- **Drag-and-drop** — drop an .exe to add a game, drop a mod to install it, drop a preset to deploy it with auto shader install.
- **OptiScaler built in** — upscaler redirection (DLSS/FSR/XeSS) with automatic DLSS DLL downloads, ReShade coexistence, and INI management.
- **UW Fix & Ultra+ links** — quick links to ultrawide fixes and Ultra+ mods appear right on game cards when available.
- **Three view modes** — Detail View, Grid View, and Compact View. Pick what fits your workflow.
- **Smart about updates** — rate-limit aware, cooldown-based update checks, and cached shader packs that skip unnecessary API calls.

## Quick Start

1. **Download and run RHI** — games appear automatically.
2. **Pick a game** from the sidebar. Search or use filter chips to narrow the list.
3. **Click Install** on the components you want — ReShade, RenoDX, a frame limiter.
4. **Launch the game**, press **Home** to open ReShade, go to **Add-ons**, and configure RenoDX.

## Download

Grab the latest release from the [GitHub Releases page](https://github.com/RankFTW/RHI/releases).

**Requires:** Windows 10/11 (x64) · [.NET 8 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/8.0)

## Troubleshooting

| Problem | Fix |
|---------|-----|
| Game not detected | **Add Game** in Settings, or drag the .exe onto the window |
| Xbox games missing | Click **Refresh** — Game Pass detection may need a moment |
| ReShade not loading | Check the install path via 📁 — the DLL must sit next to the game exe |
| Black screen (Unreal) | ReShade → Add-ons → RenoDX → set `R10G10B10A2_UNORM` to `output size` |
| UE-Extended not working | Enable HDR in the game's display settings first |
| Downloads failing | Click **Refresh**, or clear cache from Settings → Open Downloads Cache |
| Everything out of sync | Settings → **Full Refresh** clears all caches and re-scans |

For the full reference covering every feature, see the [Detailed Guide](docs/DETAILED_GUIDE.md).

## Third-Party Components

| Component | Author | Licence |
|-----------|--------|---------|
| [ReShade](https://reshade.me) | Crosire | [BSD 3-Clause](https://github.com/crosire/reshade/blob/main/LICENSE.md) |
| [RenoDX](https://github.com/clshortfuse/renodx) | clshortfuse & contributors | [MIT](https://github.com/clshortfuse/renodx/blob/main/LICENSE) |
| [ReLimiter](https://github.com/RankFTW/ReLimiter) | RankFTW | Source-available |
| [Display Commander](https://github.com/pmnoxx/display-commander?tab=readme-ov-file#display-commander) | pmnoxx | [GPL-3](https://github.com/pmnoxx/display-commander/blob/main/LICENSE) |
| [RE Framework](https://github.com/praydog/REFramework-nightly) | praydog | [MIT](https://github.com/praydog/REFramework/blob/master/LICENSE) |
| [Luma Framework](https://github.com/Filoppi/Luma-Framework) | Pumbo (Filoppi) | Source-available |
| [OptiScaler](https://github.com/optiscaler/OptiScaler) | OptiScaler contributors | Source-available |
| [7-Zip](https://www.7-zip.org/) | Igor Pavlov | [LGPL-2.1 / BSD-3-Clause](https://www.7-zip.org/license.txt) |

> RHI is an unofficial third-party tool, not affiliated with or endorsed by the RenoDX project, Crosire, or the Luma Framework. All mod files are downloaded from their official sources at runtime and are not redistributed.

## Acknowledgements

RHI would not be possible without the hard work of the entire RenoDX team and [Crosire](https://reshade.me), the creator of ReShade. Their dedication to open-source HDR modding is what makes tools like this one viable. Thank you to every mod author, contributor, and tester who keeps pushing PC HDR forward.

## Links

[RenoDX](https://github.com/clshortfuse/renodx) · [RenoDX Wiki](https://github.com/clshortfuse/renodx/wiki/Mods) · [ReShade](https://reshade.me) · [Luma Framework](https://github.com/Filoppi/Luma-Framework) · [Luma Mods List](https://github.com/Filoppi/Luma-Framework/wiki/Mods-List) · [ReLimiter](https://github.com/RankFTW/ReLimiter) · [HDR Guides](https://www.hdrmods.com)

[RenoDX Discord](https://discord.gg/gF4GRJWZ2A) · [HDR Den Discord](https://discord.gg/k3cDruEQ) · [RHI Support](https://discordapp.com/channels/1296187754979528747/1475173660686815374) · [Ultra+ Discord](https://discord.gg/pQtPYcdE)

[Support RHI on Ko-Fi ☕](https://ko-fi.com/rankftw)
