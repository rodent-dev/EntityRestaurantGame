# Player Layer — Handoff (2026-07-20)

Session goal: the game played like a pure simulation — the player loop
(favored pawn, influence) existed only in the old console UI and never made it
into the 3D/2D interface. This session brought it over and added a generic
relation-highlight system. **All code below builds clean and passed the 3D
smoke run** (screenshot verified: HUD, gold/yellow/blue outlines, dossier
panel all rendering).

## What shipped

### 1. Generic shape-outline highlight (relation colors)

Inverted-hull outline pass — works on **any `GpuMesh` with normals** (cubes
today, OBJ props/beasts the day they get models):

- `data/shaders/basic.vert` — new `uOutline` uniform: pushes vertices out
  along their normals (world units). 0 = normal render.
- `data/shaders/basic.frag` — new `uSolid` uniform: 1 = ignore vertex color,
  emit `uTint` flat (`mix(vColor * uTint, uTint, uSolid)`).
- `Render3D/Render3DHost.cs` `OnRender` — after the normal pawn draw loop, a
  second pass with `gl.CullFace(GLEnum.Front)`, `uSolid=1`,
  `uOutline=OutlineWidth` (const `0.08f` ≈ 1 canvas px) re-draws each entity
  in its relation color, then restores state. Both uniforms are also reset at
  frame start next to the existing `uTint` reset.

- **`Render3D/RelationTint.cs` (new)** — pure relation→color mapping,
  presentation-only:
  - Favored pawn → gold (`RelationTint.Self`, also the HUD accent color).
  - Everyone else → `entity.EntityRelationships[favoredId]` (their feeling
    toward you) clamped over `Saturation = 5` and lerped
    red(−) ↔ grey(0) ↔ blue(+).
  - **Yellow "potentially evil" tier**: temperament, not species (keywords,
    not names) — `Behaviors.Any(b => b.Affect > 0 && b.Keywords.Contains("Aggressive"))`
    (same test `DialogService.cs` uses for aggressive tone). Applies only
    while `|rel| <= StrangerBand (1)`: a neutral bear reads yellow, one that
    grows to hate you reads red, a tamed one reads blue. Emergent by design.
  - No outlines at all until a pawn is picked (`FavoredEntityId == null`).

### 2. Player loop in the 3D interface

The console flow (`UI/Screens/PawnSelectScreen` + `InfluenceScreen`) now has
windowed heirs:

- **`Scenes/PawnSelectScene.cs` (new)** — blocking overlay intro ("CHOOSE
  YOUR PAWN"): MenuList of up to `MaxChoices = 8` active entities with a
  traits/inventory dossier line for the highlighted pick. Sets
  `FavoredEntityId`, announces, pops. No cancel path. Pushed by the host at
  load (non-smoke) and **re-pushed when the favored pawn dies** (host clears
  `FavoredEntityId` first; guard: `Top is not PawnSelectScene`). Empty-board
  case draws "The board is empty" and quits on confirm.
- **`Scenes/InfluenceScene.cs` (new)** — the whisper micromenu, left-docked,
  **non-blocking** overlay (sim keeps deciding — a whisper is a bid, not a
  command). Lists the pawn's actions → sets `PendingInfluence`
  (+`EntityController.InfluenceStrength`), plus a "(clear whisper)" item.
  Shows mood-scaled effectiveness like the console screen. Esc pops;
  auto-pops if the pawn dies.
- **`Render3D/PlayerHud.cs` (new)** — persistent bottom-left strip, DebugHud
  pattern (host-owned, passive, never pauses). Shows name/type, hp bar,
  hunger/mood, last decision, standing whisper, key hints. Hidden until a
  pawn exists. Draws in the speech-bubble pass: above bubbles, below menus.

### 3. Wiring / small changes

- `Render3D/Render3DHost.cs`:
  - `GameAction` enum: + `PlayerMenu`, `FocusPawn`.
  - `OnLoad`: smoke auto-picks the first entity as favored (screenshot proves
    the player layer); otherwise pushes `PawnSelectScene`.
  - `OnUpdate`: favored-lost re-push (above); `worldLive` now includes
    `InfluenceScene` as a live-world sidecar; **Tab** opens the whisper menu
    (favored alive, not already open); **F** snaps `_camera.Target` to the
    pawn's visual position.
- `data/input/bindings.json`: `"PlayerMenu"` = Tab, `"FocusPawn"` = F.
- `EntityController.cs`: hoisted `public const int InfluenceStrength = 12`
  (was private in the console `InfluenceScreen`, which now references it).

## Verified

- `dotnet build` — 0 warnings, 0 errors.
- `dotnet run -- --3d --smoke` — passes; `smoke.bmp` shows the PlayerHud
  (Sera the Guard, hp bar, hints), gold outline on the favored pawn, yellow
  outline on the aggressive-tempered Warrior, inspect panel intact.

## NOT yet done — resume here

1. **Interactive pass**: the pawn-select intro, Tab whisper menu, and F
   follow have only been smoke/screenshot-verified, not hand-driven. Run
   `dotnet run --project src\EntityARestaurantGame -- --3d` and exercise all
   three (also: pawn death → intro re-opens; whisper shows `[influenced]` tag
   in announcements when it lands).
2. **Headless `tools\SmokeTest`** not re-run this session (sim logic changes
   were minimal — one hoisted const — so risk is low).
3. **Docs**: STATUS.md and specs/UI_2D_Interface_Plan.md don't mention the
   player layer yet; this file is the interim record.
4. **Worldgen worlds**: `PawnSelectScene` caps at 8 choices (MenuList doesn't
   scroll) — a live settlement has more entities than that; needs paging or
   scrolling eventually.
5. **Tuning candidates**: outline width vs zoom (constant world-units, so it
   thickens on zoom-in — PS2-authentic but review), `Saturation = 5` /
   `StrangerBand = 1` thresholds, HUD/whisper-panel styling.
6. Console UI untouched by design (debug fallback) — it still has its own
   parallel flow.
