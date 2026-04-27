# DLSS Enabler Integration Research for RHI

## What is DLSS Enabler

DLSS Enabler is an open-source mod that injects NVIDIA DLSS upscaling and DLSS-G Frame Generation into DirectX 12 games — including on AMD and Intel GPUs. It bundles OptiScaler (for upscaling replacement) and Nukem's dlssg-to-fsr3 mod (for frame generation via AMD FSR 3) into a single installer.

- **Repository**: [github.com/artur-graniszewski/DLSS-Enabler](https://github.com/artur-graniszewski/DLSS-Enabler)
- **Current version**: Auto-built releases tracking OptiScaler v0.9.0 (as of April 2026)
- **License**: Open source

### What It Does
1. **Enables DLSS upscaling on non-NVIDIA GPUs** — AMD and Intel users get DLSS-quality upscaling via OptiScaler translating DLSS calls to FSR/XeSS
2. **Enables Frame Generation on any GPU** — Injects FSR 3 Frame Generation into games that only support DLSS-FG, or adds FG to games that don't support it at all
3. **Latency reduction** — AntiLag2 on AMD RDNA1+, LatencyFlex on Intel/older AMD, native Reflex on NVIDIA
4. **Upscaler swapping** — Replace any game's native upscaler (DLSS/FSR/XeSS) with any other

---

## Relationship with OptiScaler

**DLSS Enabler IS a wrapper around OptiScaler.** RHI already manages OptiScaler installation. The key differences:

| Feature | OptiScaler (standalone) | DLSS Enabler |
|---|---|---|
| Upscaler replacement | ✅ | ✅ (bundles OptiScaler) |
| Frame Generation | ❌ (needs Nukem mod separately) | ✅ (bundles dlssg-to-fsr3) |
| FG in non-FG games | ❌ | ✅ (via OptiScaler 0.7+ HUD detection) |
| Latency reduction | ❌ | ✅ (AntiLag2/LatencyFlex/Reflex) |
| FakeNVAPI | ❌ (separate install) | ✅ (bundled) |
| Installer | Manual DLL copy | Inno Setup installer |
| Auto-updates | Manual | Auto-built from OptiScaler releases |

**Key insight**: DLSS Enabler is essentially "OptiScaler + Frame Generation + Latency tools" in one package. Since RHI already manages OptiScaler, adding DLSS Enabler support would mean extending the existing OptiScaler flow to optionally include frame generation components.

---

## Components Bundled in DLSS Enabler

The installer deploys these files to the game directory:

### Core Files
| File | Purpose |
|---|---|
| `dlss-enabler.dll` | Main DLSS Enabler module — hooks into Streamline/DLSS pipeline |
| `dlss-enabler-upscaler.dll` | OptiScaler (renamed from nvngx.dll) — handles upscaler replacement |
| `nvngx.ini` | Configuration file for DLSS Enabler settings |
| `dlssg_to_fsr3_amd_is_better.dll` | Nukem's mod — replaces DLSS-FG with FSR 3 Frame Generation |

### Supporting Libraries
| File | Purpose |
|---|---|
| `libxess.dll` | Intel XeSS library — provides XeSS upscaling backend |
| `amd_fidelityfx_dx12.dll` | AMD FidelityFX SDK — provides FSR upscaling/FG backend |
| `amd_fidelityfx_vk.dll` | AMD FidelityFX Vulkan variant |
| `amd_fidelityfx_framegeneration_dx12.dll` | FSR 3 Frame Generation module |
| `amd_fidelityfx_upscaler_dx12.dll` | FSR upscaler module |
| `fakenvapi.dll` | FakeNVAPI — spoofs NVIDIA GPU on AMD/Intel for DLSS compatibility |
| `fakenvapi.ini` | FakeNVAPI configuration |

### Installation Options (DLL proxy names)
DLSS Enabler can be installed as different DLL proxy names depending on the game:
- `dxgi.dll` (most common — same as OptiScaler)
- `winmm.dll`
- `d3d12.dll`
- `d3d11.dll`
- `dbghelp.dll`
- `version.dll`
- `wininet.dll`
- `winhttp.dll`

---

## Configuration (nvngx.ini)

Key settings in `nvngx.ini`:

```ini
[FrameGeneration]
; Enable/disable frame generation
Enabled=auto          ; auto|true|false

; FSR version for frame generation
FGMode=fsr31          ; fsr30|fsr31

; Frame rate limiter
FramerateLimit=0      ; 0=unlimited, vsync, or specific FPS value

[Upscaler]
; Override the game's upscaler
Backend=auto          ; auto|dlss|fsr21|fsr31|xess

[Spoofing]
; Spoof NVIDIA GPU on AMD/Intel
NvidiaGPU=auto        ; auto|true|false

; Spoof DeepDVC support
DeepDVC=auto          ; auto|true|false

[Reflex]
; Latency reduction mode
ReflexEmulation=auto  ; auto|on|off
; auto/off = AntiLag2 on AMD, LatencyFlex on Intel
; on = force LatencyFlex on all GPUs
```

---

## GPU Compatibility

| GPU Vendor | Upscaling | Frame Generation | Latency Reduction |
|---|---|---|---|
| NVIDIA RTX 40xx | DLSS native | DLSS-FG native or FSR 3 | Reflex native |
| NVIDIA RTX 20xx/30xx | DLSS native | FSR 3 (via Nukem mod) | Reflex native |
| NVIDIA GTX 10xx/16xx | FSR/XeSS (via OptiScaler) | FSR 3 (limited) | LatencyFlex |
| AMD RDNA 1+ | FSR/XeSS (via OptiScaler) | FSR 3 | AntiLag2 |
| AMD pre-RDNA1 | FSR/XeSS (via OptiScaler) | FSR 3 (limited) | LatencyFlex |
| Intel Arc | FSR/XeSS (via OptiScaler) | FSR 3 | LatencyFlex |

---

## Game Compatibility

### Requirements
- **DirectX 12** game (DX11 has limited/experimental support)
- Game must natively support **DLSS2 or DLSS3** for full functionality
- Games without any DLSS support can still get FG via OptiScaler 0.7+ HUD detection (experimental)

### Known Working Games (from changelog)
- God of War: Ragnarök, Horizon Forbidden West, Ghost of Tsushima, Spider-Man Remastered/Miles Morales/2
- Baldur's Gate 3 (Vulkan mode), Black Myth Wukong, The Witcher 3
- Rise of the Tomb Raider, No Man's Sky, Doom Eternal
- Dragon Age: The Veilguard, The Last of Us Part 1
- Bright Memory: Infinite, Ready or Not, Destroy All Humans 2

### Known Issues
- Anti-cheat games will likely block it (same as OptiScaler)
- Some games crash with specific DLL proxy names — may need to try different options
- Antivirus false positives are common due to DLL injection techniques
- AMD Windows Vulkan driver has compatibility issues with some games

---

## Pros of DLSS Enabler Integration in RHI

1. **Frame Generation for everyone** — AMD/Intel users get FG in DLSS-only games
2. **Builds on existing OptiScaler infrastructure** — RHI already manages OptiScaler DLLs, staging, and configuration
3. **Single-click experience** — Users currently have to manually run the DLSS Enabler installer; RHI could automate this
4. **Latency reduction** — AntiLag2/LatencyFlex/Reflex bundled automatically
5. **Upscaler flexibility** — Users can swap between DLSS/FSR/XeSS regardless of what the game supports
6. **High demand** — Frame generation is one of the most requested features in the gaming modding community

## Cons of DLSS Enabler Integration in RHI

1. **Overlap with existing OptiScaler** — RHI already installs OptiScaler; DLSS Enabler bundles its own copy. Need to avoid conflicts.
2. **Complexity** — Many more DLLs to manage, more configuration options, more potential failure points
3. **Antivirus issues** — DLL injection triggers false positives; users will blame RHI
4. **Game-specific quirks** — Different games need different DLL proxy names, different settings
5. **Experimental features** — Frame generation in non-FG games is experimental and may cause crashes
6. **Support burden** — Frame generation issues are hard to debug and users will report them as RHI bugs
7. **Installer format** — DLSS Enabler uses Inno Setup installer, not a simple zip. Would need to either extract the installer or build our own deployment from the component DLLs.

---

## Proposed Implementation for RHI

### Option A: Extend OptiScaler with Frame Generation Toggle
Since RHI already manages OptiScaler, the simplest approach:
1. Add a "Frame Generation" toggle in the OptiScaler section of the overrides panel
2. When enabled, deploy the additional FG components (dlssg_to_fsr3, FakeNVAPI, FidelityFX DLLs)
3. Generate/update nvngx.ini with FG settings
4. When disabled, remove FG components, keep OptiScaler

**Pros**: Minimal new infrastructure, leverages existing OptiScaler staging
**Cons**: Doesn't include DLSS Enabler's own dlss-enabler.dll which handles Streamline hooking

### Option B: Full DLSS Enabler Integration
Deploy DLSS Enabler as a separate component alongside OptiScaler:
1. Download DLSS Enabler releases from GitHub
2. New "DLSS Enabler" component row in the detail panel (like OptiScaler, ReLimiter, etc.)
3. Install/uninstall manages all DLSS Enabler files
4. Configuration UI for FG mode, upscaler backend, latency settings

**Pros**: Full feature set, matches what users get from the standalone installer
**Cons**: More complex, potential conflicts with standalone OptiScaler install

### Option C: Frame Generation as OptiScaler Add-on
Since DLSS Enabler auto-builds from OptiScaler releases:
1. When OptiScaler is installed, show an additional "Enable Frame Generation" option
2. Download the Nukem dlssg-to-fsr3 mod and FakeNVAPI separately
3. Deploy alongside existing OptiScaler installation
4. OptiScaler 0.7+ already supports FG via its own menu — just need the supporting DLLs

**Pros**: Cleanest integration, no duplicate OptiScaler, leverages OptiScaler's built-in FG support
**Cons**: May not cover all DLSS Enabler features (Streamline hooking, etc.)

### Recommended: Option C
This is the most practical approach because:
- RHI already manages OptiScaler
- OptiScaler 0.7+ has built-in frame generation support
- The main missing pieces are just the Nukem mod DLL and FakeNVAPI
- No need to manage a separate DLSS Enabler component

---

## Technical Details for Implementation

### Files to Stage (in addition to existing OptiScaler)
1. **dlssg_to_fsr3_amd_is_better.dll** — Already bundled in RHI's OptiScaler packages (OPTI folder)
2. **fakenvapi.dll** + **fakenvapi.ini** — Already bundled in RHI's OptiScaler packages
3. **amd_fidelityfx_framegeneration_dx12.dll** — Already bundled in RHI's OptiScaler packages

### What RHI Already Has
Looking at the OPTI folder in the repository, RHI already bundles:
- `dlssg_to_fsr3_amd_is_better.dll`
- `fakenvapi.dll` + `fakenvapi.ini`
- `amd_fidelityfx_dx12.dll`
- `amd_fidelityfx_framegeneration_dx12.dll`
- `amd_fidelityfx_upscaler_dx12.dll`
- `amd_fidelityfx_vk.dll`
- `libxess.dll`, `libxess_dx11.dll`, `libxess_fg.dll`

**These are already deployed by OptiScaler install.** The frame generation capability is already present in the deployed files — it just needs to be enabled in OptiScaler's configuration.

### Enabling Frame Generation
OptiScaler 0.7+ supports frame generation through its in-game menu (OptiMenu). The relevant OptiScaler.ini settings:

```ini
[FrameGeneration]
Enabled=true
HudFix=true           ; HUD detection to prevent FG artifacts on UI elements
```

This means RHI may not need to integrate DLSS Enabler at all — just expose OptiScaler's built-in FG settings through the RHI UI.

---

## Key Repositories

| Project | URL | Purpose |
|---|---|---|
| DLSS Enabler | [github.com/artur-graniszewski/DLSS-Enabler](https://github.com/artur-graniszewski/DLSS-Enabler) | All-in-one installer |
| OptiScaler | [github.com/optiscaler/OptiScaler](https://github.com/optiscaler/OptiScaler) | Upscaler replacement + FG |
| dlssg-to-fsr3 | [github.com/Nukem9/dlssg-to-fsr3](https://github.com/Nukem9/dlssg-to-fsr3) | DLSS-FG to FSR 3 FG replacement |
| FakeNVAPI | [github.com/FakeMichau/FakeNVAPI](https://github.com/FakeMichau/FakeNVAPI) | NVIDIA GPU spoofing for AMD/Intel |
| DLSS Finder | [github.com/artur-graniszewski/DLSS-Finder](https://github.com/artur-graniszewski/DLSS-Finder) | Utility to find DLSS/Streamline in games |

---

## Summary

DLSS Enabler is essentially a convenience wrapper around tools RHI already deploys (OptiScaler, dlssg-to-fsr3, FakeNVAPI). The frame generation capability is already present in the files RHI installs with OptiScaler — it just needs UI exposure. The most practical integration path is adding a "Frame Generation" toggle to the existing OptiScaler section rather than integrating DLSS Enabler as a separate component.
