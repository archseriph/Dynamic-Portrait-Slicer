# Dynamic Portrait Slicer

Fixes portrait spritesheet cropping for Dialogue Display Framework (DDFC) layouts to prevent stretched or improperly cropped portraits, and provides runtime/per-NPC adjustments.

Overview
- Purpose: Help DDFC render portrait sheets cleanly (no stretching / wrong cropping) and let you keep a consistent dialogue UI feel across NPCs.
- Key features:
  - Automatic portrait sheet clamping to avoid stretched or cropped portraits
  - Runtime nudging and scaling hotkeys for live adjustments during dialogue
  - Per-NPC overrides that can be created/edited from in-game
  - GMCM support for friendly configuration

Quick recommendation
- Stick to one portrait system per NPC. Mixing Portraiture-style cycling and DDFC-style large/nonstandard portrait sheets for the same NPC can cause missing or misrendered portraits. Prefer portrait packs that target the system you use.

Requirements
- Stardew Valley + SMAPI (MinimumApiVersion 4.0.0 as set in the manifest)
- Dialogue Display Framework (DDFC) — UniqueID: Mangupix.DialogueDisplayFrameworkContinued
- (Useful but not required) Generic Mod Config Menu (GMCM) — UniqueID: spacechase0.GenericModConfigMenu
- Compatible with the modding setup that expects a manifest.json in the root of the mod folder.

Installation

Manual
1. Build the mod and copy the following into a folder named `Dynamic Portrait Slicer` (or a name of your choice):
   - `DynamicPortraitSlicer.dll` (the compiled mod DLL)
   - `manifest.json`
   - `README.md`
   - `LICENSE.txt`
   - `lib/` (if present with runtime dependencies)
2. Place the `Dynamic Portrait Slicer` folder in your SMAPI `Mods/` directory.
3. Start Stardew Valley via SMAPI.

Thunderstore / ModHub packaging (recommended)
- Zip the mod folder (root should contain manifest.json, DLL, README.md, LICENSE.txt).
- Upload the zip to Thunderstore / ModHub and fill in the metadata form using the same UniqueID as your manifest.

Suggested manifest author/ID
- The repository currently has `Author: "Matt"` and `UniqueID: "Matt.DynamicPortraitSlicer"`.
- For publishing, use a consistent identity such as your Thunderstore/GitHub handle. The user requested:
  - Author: `archeriph`
  - UniqueID: `archeriph.DynamicPortraitSlicer`

Configuration (summary)
Most configuration is available through GMCM at runtime. This is a short summary of notable config fields and defaults (see `ModConfig.cs` for complete definitions):

- PreferredFrameSizes (default): [1024, 512, 256, 200, 160, 128, 120, 100, 96, 80, 72, 64, 48, 32]  
- VerboseLogging: false
- NpcFrameSizeOverride: {} (per-NPC integer override)
- ForceFrameSize: 0 (0 = auto)
- ConservativeClamp: true
- OnlyClampWhenXySet: true
- RuntimeScaleMode: Absolute (other option: MultiplyOriginal)
- EnableRuntimeOverrides: true
- AutoCreateNpcOverrideOnTalk: true
- AutoEnableNpcOverrideOnTalk: false
- LiveNudgeEditsNpcOverride: true

Live tuning sliders & nudging
- PortraitSide (Default/Left/Right) — default: Default
- PortraitScaleMultiplier — default: 1.0
- PortraitOffsetX / PortraitOffsetY — default: 0

Hotkeys (defaults)
- OffsetLeft: LeftShift + Left
- OffsetRight: LeftShift + Right
- OffsetUp: LeftShift + Up
- OffsetDown: LeftShift + Down
- ScaleUp: LeftShift + OemPlus
- ScaleDown: LeftShift + OemMinus
- CommitNpcOverride: LeftShift + Enter (commit current speaker override)
- Dialogue UI nudge (Ctrl+Shift+Arrows) for moving the whole DDFC UI:
  - DialogueUiLeft: LeftControl + LeftShift + Left
  - DialogueUiRight: LeftControl + LeftShift + Right
  - DialogueUiUp: LeftControl + LeftShift + Up
  - DialogueUiDown: LeftControl + LeftShift + Down
- Nudge step sizes:
  - NudgePixels: 10
  - NudgeScale: 0.05 (5%)
  - DialogueUiNudgePixels: 10
- GMCM ranges: DialogueUiOffsetMaxAbs defaults to 1000

Per-NPC overrides
- Per-NPC overrides are stored in the mod config under NpcOverrides and can contain:
  - Enabled, Side, OffsetX, OffsetY, ScaleMultiplier, FrameWidth, FrameHeight, Columns, Rows, ForceSingleImage
- Use AutoCreateNpcOverrideOnTalk to create a per-NPC override when talking and CommitNpcOverride to commit edits.

Building & packaging instructions (for publishing)
1. Build the project in Release mode (Visual Studio / dotnet). The compiled DLL should be named `DynamicPortraitSlicer.dll`.
2. Ensure `manifest.json` is correct (UniqueID, Author, Version, MinimumApiVersion).
3. Include `README.md` and `LICENSE.txt` in the root of the mod folder along with the DLL. Include `lib/` if there are extra assemblies required at runtime.
4. Zip the mod folder with the folder name you want published (e.g., `Dynamic Portrait Slicer.zip` containing a folder `Dynamic Portrait Slicer/`).
5. Upload to Thunderstore / ModHub and fill in fields:
   - Version, Changelog, Dependencies (use the UniqueIDs listed in the manifest), and an appropriate category/tag (Dialogue UI, Portraits, UI).
6. After upload, test installing the zip via Thunderstore client or manual install to confirm the mod unpacks to a folder with manifest & DLL at root.

Changelog (example)
- 1.0.0 — Initial release: portrait clamping, runtime nudging / scaling, per-NPC overrides, GMCM support.

Troubleshooting
- Portraits missing or broken: verify the NPC’s portrait pack is in the system you intend to use (DDFC vs Portraiture). Mixing systems for the same NPC often causes issues.
- Hotkeys not responding: ensure mod is enabled and that hotkeys do not conflict with other mods or system input.
- Incompatible SMAPI: ensure your SMAPI is at or above the MinimumApiVersion given in manifest.json (4.0.0).
- If you run into an issue, include: the mod version, SMAPI version, Stardew Valley version, list of installed mods, and a brief reproduction scenario when filing an issue.

Credits & License
- Author: archeriph
- License: See LICENSE.txt in this repository.

Contact & reporting bugs
- Open issues here: https://github.com/archseriph/Dynamic-Portrait-Slicer/issues
- When opening an issue include steps to reproduce, your SMAPI/stardew versions, and a minimal set of installed mods if possible.

Notes for maintainers / packaging
- Consider updating manifest UniqueID to a handle you control before publishing (to avoid ID collisions). The manifest will be updated to use `archeriph.DynamicPortraitSlicer`.
- Keep LICENSE.txt in the mod root for automatic attribution.
- Add a small screenshot or animated GIF in the repo / Thunderstore page showing before/after to help users quickly understand the fix.
