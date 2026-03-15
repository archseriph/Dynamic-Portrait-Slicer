## Portrait pack guidance (DDFC vs Portraiture)

Dynamic Portrait Slicer is intended as a bridge for **Dialogue Display Framework (DDFC)** portrait layouts: it helps DDFC render portrait sheets cleanly (no stretching / wrong cropping) and lets you keep a consistent dialogue UI feel across NPCs.

### Recommendation: stick to one portrait system per NPC
To avoid glitches (missing portraits, unexpected cropping, inconsistent scaling), try to keep each NPC’s portraits coming from **one system**:

- **If you use DDFC portrait packs:**  
  Prefer DDFC-formatted packs for that NPC, and avoid also cycling Portraiture variants for the same NPC during dialogue.

- **If you use Portraiture portrait packs:**  
  Prefer Portraiture-formatted packs for that NPC, and avoid using DDFC packs that use nonstandard portrait sheet layouts for the same NPC.

Mixing DDFC-style “large/nonstandard” portrait sheets with Portraiture’s cycling/toggle modes can be unpredictable, especially when the portrait images have inconsistent formats (single-image portraits, unusual grids, or very large frames).

### Practical “rules of thumb”
- If a portrait pack is labeled for **Portraiture**, use it through Portraiture.
- If a portrait pack is labeled for **DDFC**, use it through DDFC.
- If you must mix systems, do it by NPC:
  - lock a given NPC to one system’s portraits (don’t cycle across systems for that NPC),
  - and avoid switching portrait modes mid-dialogue.

### Why this matters
Portraiture and DDFC don’t always interpret portrait textures the same way. Some packs contain a mix of formats (different dimensions/layouts), which can cause a portrait to disappear when cycling, or cause odd rendering in large/overlay portrait modes.
