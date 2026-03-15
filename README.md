# Dynamic Portrait Slicer

## Compatibility: Portraiture

Dynamic Portrait Slicer is designed for Dialogue Display Framework (DDFC) portrait layouts and portrait *tilesheets*.

If you use **Portraiture**:
- Portraiture’s **small/standard tilesheet portraits** usually work fine.
- Portraiture’s **large portraits / overlay portraits** may use nonstandard image dimensions or layouts. Some portrait packs (example: “Anime Style Skimpy Portraits for Non-marriageable NPCs”) contain portraits with wildly different formats (single-image, unusual grids, 1024x1024 frames, etc.). Those can cause missing portraits, visual glitches, or performance issues when Portraiture toggles large portrait mode.

For safety, Dynamic Portrait Slicer can automatically disable its portrait clamp/normalize patch when Portraiture is installed:
- Config: `DisableWhenPortraitureInstalled` (default: `true`)

If you want to use Portraiture large portraits, it is recommended to:
1) Keep `DisableWhenPortraitureInstalled=true`
2) Avoid portrait packs with inconsistent portrait sheet formats
3) Toggle Portraiture’s large portrait mode only with packs known to support it
