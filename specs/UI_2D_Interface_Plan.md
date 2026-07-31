# 2D Interface — Component Inventory & Plan

Drafted 2026-07-16. The base-level interface for the 3D game: everything the
player touches that is not the world itself. Renders into the same 480×270
low-res target as the 3D scene, so all UI inherits the chunky PS2 upscale.

**Boundary rule (user decision):** regions / settlements / world-map are NOT
scenes in this controller. They belong to a separate (future) world-view
controller. This controller owns menus, overlays, and in-world interaction UI
only.

---

## 1. Foundation layer (build first — everything else stands on it)

| Component | What it is | Home |
|---|---|---|
| `Texture2D` | Image import (PNG/JPG/BMP/TGA → GL texture, nearest-filtered). Menu backgrounds are just image files dropped in `data/ui/`. | `Engine.Render` |
| `SpriteFont` | TTF → baked glyph atlas at a fixed pixel size. Slightly pixely, PS2-almost-realistic: a real (serif) typeface rasterized small, upscaled nearest. Drop any `.ttf` in `data/fonts/` to override; falls back to a Windows serif (Palatino/Georgia). | `Engine.Render` |
| `Renderer2D` | Ortho 2D pass over the virtual canvas: colored rects, textured rects (with source-rect for atlases), text. Runs after the 3D pass into the same render target. | `Engine.Render` |
| `Scene` / `SceneAction` | The 2D counterpart of console `IScreen`/`ScreenAction`: Stay · Pop · Push · Replace · Quit. Scenes declare `BlocksSim` (opening one pauses the world for free, same trick as `IRealtimeScreen`) and `IsOverlay` (scenes beneath still draw). | `Engine.Ui` (new) |
| `SceneManager` | The official scene controller: stack, routes input to top scene only, draws from the deepest non-overlay scene up, reports `SimPaused` to the host. | `Engine.Ui` |
| `UiInput` | Device-free per-frame intent snapshot (Up/Down/Confirm/Cancel, later mouse pos/click). The game maps its `InputState<GameAction>` onto it, so `Engine.Ui` stays generic. | `Engine.Ui` |
| `UiContext` | What scenes get handed: renderer, fonts, texture cache rooted at `data/ui/`, canvas size. | `Engine.Ui` |

## 2. Widget kit (shared pieces screens are assembled from)

- **Label / TextBlock** — single-line + word-wrapped multi-line text. *(text drawing lands with foundation)*
- **MenuList** — vertical selectable options w/ disabled states; keyboard-driven. *(lands with foundation — pause menu needs it)*
- **Panel / frame** — background image or tinted rect behind content; later a nine-patch border so panels scale without stretching corners.
- **Bar** — needs/HP/hunger meter (fill + frame). Also usable as progress bar.
- **Slider** — settings values (volume, mouse sensitivity, sim speed).
- **Toggle** — on/off settings rows.
- **TextBox (typed input)** — needs Silk keyboard *char* events plumbed through `Engine.Input`; caret, backspace. For naming pawns, rumor text, etc.
- **Choice row** — horizontal left/right option cycler (resolution, difficulty-style settings).
- **ScrollList** — long lists (inventory, signal log) with a viewport + scroll marker.
- **DialogBox** — bottom-third conversation box: speaker name plate, typewriter text reveal, "more" indicator, response choices. Portrait slot reserved.
- **Speech bubble** — ✅ world-anchored text box over a pawn showing its latest
  LOUD log entry (the broadcast tier: speech, deals, violence, deaths — quiet
  entries are inspect-only). The stand-in for action animations; item PNGs
  alongside are a future project. *(A bottom-screen toast ticker was built
  first and removed — too much motion on screen.)*
- **Tooltip** — small hover/selection detail box (item stats, axis meanings).
- **Cursor** — mouse pointer sprite (once mouse-driven menus arrive).
- **Icon** — item/entity glyph drawn from a texture atlas.

## 3. Screens (assembled from the kit; each is one `Scene`)

**System chrome**
- **Title / main menu** — background art, New / Continue / Settings / Quit.
- **Pause menu** — overlay over the live 3D view; Resume / Settings / Quit. *(the foundation's proof scene)*
- **Settings** — sub-pages or one list: video (window size, upscale mode), audio (stub until sound exists), controls (rebind viewer over `bindings.json`), gameplay (sim pacing).
- **Confirm modal** — small yes/no ("Quit without saving?").
- **Loading / transition** — fade-to-black scene swap; trivial but makes everything feel intentional.

**Gameplay**
- **HUD** — the one persistent overlay (not on the stack, or a permanent bottom scene): selected pawn name plate, needs bars, tick clock.
- **Entity micromenu** — ✅ click a pawn (screen-space pick): vitals + 2-page
  body (personal log / inventory), non-blocking overlay beside the live sim;
  Enter flips pages, Up/Down scrolls, Esc closes, clicking another pawn
  retargets. May grow more pages later.
- **Inventory verbs** — detail pane on the micromenu's Items page (weight, keywords, effects) with Use / Give / Drop actions.
- **Interaction menu** — context menu on a targeted entity/object: the reachable actions (Attack, Converse, Consume…) fed by the existing candidate system.
- **Conversation** — DialogBox wired to the exchange loop (`DialogService`); utterances stream in, player picks responses for their pawn.
- **Entity inspect (deep)** — port of console `EntityInspectScreen`: stats, behaviors, mind axes, relationships — likely more micromenu pages.
- **Influence / rumor composer** — port of `InfluenceScreen`/`RumorScreen`: axis sliders + target picker.
- **Pawn select / start-anew** — port of `PawnSelectScreen`/`StartAnewScreen`; becomes part of the title flow.

**Dev-only**
- **Debug overlay** — FPS, entity count, sim tick, camera coords. Toggle key, drawn above everything.

**Explicitly out of scope for this controller:** region/settlement/world-map
views — separate controller, per project decision.

## 4. Asset conventions

- `data/ui/` — menu background images (any PNG/JPG/BMP/TGA). Referenced by
  filename from scenes; missing file = scene falls back to a tinted rect, never
  a crash.
- `data/fonts/` — drop-in `.ttf` files. First file found (alphabetical) becomes
  the game font; otherwise Windows fallback chain. Baked at two sizes: body and
  heading.
- Backgrounds are authored at (or will be stretched to) 480×270 — the canvas
  resolution. Pixel-exact art should be authored at exactly that size.

## 5. Build order

1. ✅ Foundation layer: Texture2D, SpriteFont, Renderer2D, Engine.Ui scene
   controller, pause menu proof over the 3D view.
2. ✅ Live sim-truth UI: per-entity logs, tiered speech bubbles, mouse picking
   (`MousePosition` in Engine.Input + Select binding), entity micromenu
   (Log / Items pages).
3. Title/main-menu + settings skeleton (exercises Panel, Slider, Toggle, Choice row).
4. Interaction menu + inventory verbs (first gameplay verbs through the new UI).
5. TextBox + conversation screen (needs char-event plumbing in Engine.Input).
6. Deep entity inspect / influence / rumor ports; console UI then retires to a
   debug tool.
