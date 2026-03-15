## Using Portraiture

Dynamic Portrait Slicer modifies Dialogue Display Framework (DDFC) portrait rendering.

If you use **Portraiture**:
- Portraiture’s **large/overlay portraits** are drawn by Portraiture itself (not by DDFC `PortraitData`), so Dynamic Portrait Slicer’s portrait move/scale hotkeys may not affect them.
- Some portrait mods are authored primarily for **DDFC** (Dialogue Display Framework) and may use **nonstandard portrait sheet layouts** (single images, unusual grids, very large frames like 1024×1024, etc.). Those mods can behave unpredictably when loaded through Portraiture (e.g. cycling portraits with `P` may cause some variants to disappear).

If Portraiture starts acting glitchy (missing portraits, lag, weird UI):
1) Exit the game completely.
2) Delete/reset Portraiture’s config file.
3) Relaunch and test again.

Advanced: Dynamic Portrait Slicer can skip its portrait clamp/normalize patch when Portraiture is installed:
- `DisableWhenPortraitureInstalled` (config.json, default true)
