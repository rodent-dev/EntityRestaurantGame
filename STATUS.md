# Project Status — Entity A Restaurant Game

Snapshot date: **2026-07-16**. Companion doc: [RECOMMENDATIONS.md](RECOMMENDATIONS.md)
(formal review + future work, kept separate on purpose).

The diagrams below zoom in layer by layer: solution map → runtime composition →
the sim heartbeat → individual subsystems.

---

## Layer 0 — Solution map (who references whom)

Compiler-enforced: `Engine.*` never references game code; Contracts hold state,
Services hold logic, the game holds time + composition.

```mermaid
flowchart TD
    subgraph ENGINE["Engine tier (generic, reusable)"]
        Ticks["Engine.Ticks<br/>Clock · Wheel · SimLoop · IEvent"]
        Items["Engine.Items<br/>IItem · ItemFactory · EffectRegistry"]
        Spatial["Engine.Spatial<br/>Location · LocationRegistry"]
        Render["Engine.Render<br/>GameWindow · Shader · GpuMesh · Primitives<br/>Texture2D · SpriteFont · Renderer2D"]
        Input["Engine.Input<br/>BindingSet · InputState · SilkInputAdapter"]
        Ui["Engine.Ui<br/>Scene · SceneManager · UiContext · MenuList"]
    end

    subgraph GAME["Game tier"]
        Contracts["Game.Contracts<br/>Entity · Mind · IAction · Influence · GameSignal"]
        Services["Game.Services<br/>EntityService · MindService · DialogService<br/>Actions/ · Effects/"]
        App["EntityARestaurantGame<br/>Program · EntityController · Simulation/<br/>UI/ (console) · Render3D/ · Scenes/"]
    end

    Contracts --> Items
    Services --> Contracts
    Services --> Spatial
    Ui --> Render
    App --> Services
    App --> Ticks
    App --> Render
    App --> Input
    App --> Ui

    subgraph DATA["data/ (linked into build output)"]
        DItems["items/ (12 item JSONs)<br/>entities/ + world/locations.json"]
        DShaders["shaders/ · input/ · models/ · ui/ · fonts/"]
    end
    App -.loads at runtime.-> DATA
```

External packages (deliberately few): `Silk.NET.{Windowing,OpenGL,Input,Maths}`,
`StbImageSharp`, `StbTrueTypeSharp` — all in Engine.Render/Input only.

---

## Layer 1 — Runtime composition (who owns whom while playing)

Two front-ends over one sim. `--3d` opens the OpenGL view; the default is the
console UI, which stays alive as the debugging fallback.

```mermaid
flowchart LR
    Program["Program.cs → WorldBuilder<br/>(one shared composition path:<br/>effects · items · locations.json ·<br/>entities/*.json · drifter template)"]

    Program -->|"default"| SM["ScreenManager<br/>(console screen stack)"]
    Program -->|"--3d"| Host["Render3DHost"]

    subgraph SIM["The one sim"]
        Controller["EntityController<br/>owns Clock · Wheel · SimLoop<br/>rosters · signal log · decision history"]
        ES["EntityService (HOW)"]
        DS["DialogService"]
        Controller --> ES
        Controller --> DS
    end

    SM -->|"Pump / AdvanceToNextBeat"| Controller
    Host -->|"Pump(dt) — render loop is master clock"| Controller

    subgraph HOSTSIDE["Render3DHost owns"]
        Cam["OrbitCamera"]
        RT["RenderTarget 480×270<br/>(PS2 upscale)"]
        Scenes["SceneManager<br/>(pause / settings stack)"]
        Hud["DebugHud (F3)<br/>reads sim truth as text"]
    end
    Host --- HOSTSIDE
    Scenes -->|"SimPaused ⇒ stop pumping"| Host
```

Rule that holds everywhere: **renderers only READ sim truth.** The sim never
knows a renderer exists. Pausing = not feeding the accumulator (both UIs use
the same trick).

---

## Layer 2 — The sim heartbeat (one decision beat, exact call chain)

Everything is an event arriving at its tick-wheel bucket; nothing polls.
Pacing: 2 ticks/sec · decisions every 5 ticks per entity (staggered) · hunger
pulse every 10.

```mermaid
sequenceDiagram
    participant W as Wheel
    participant E as EntityDecisionEvent
    participant C as EntityController.ActOnce
    participant S as EntityService
    participant K as CommitmentCalculator
    participant D as DialogService
    participant A as IAction
    participant X as EventDistributor

    W->>E: Fire (bucket reached)
    E->>C: ActOnce(entity)
    C->>S: BuildCandidates(entity, roster)
    C->>S: ApplyBehaviorWeights + ApplyStatusWeights
    C->>D: ApplySituationWeights (reply-pull, covet-pull)
    C->>S: ApplyInfluence (player nudge, stranger ÷2)
    C->>S: SelectWeighted → the CHALLENGER
    alt entity.Current is live (a long action underway)
        C->>K: Weigh(actor, action, activity) → Commitment
        alt commitment.Total >= challenger.TotalWeight
            C->>A: Sustain — one beat: commute, drain, grind
            C->>C: Progress += Focus(entity)  (only if the beat counted)
            C->>A: Resolve(performance) if Elapsed hits Duration
        else walked out
            C->>A: Resolve(partial performance) — paid for what it managed
        end
    end
    alt target out of InteractionRange (3m)
        C->>S: MoveToward — beat becomes movement, intent persists
        C-->>W: (announce "moves toward")
    else in range
        C->>S: SelectItem (viable, random)
        C->>C: Begin → Activity, then Advance one beat
        A->>A: item.Apply → registered effects run
        C->>S: ApplyMoodFallout (keyword-driven)
        C->>C: RecordDecision · AnnounceRun
        C->>X: Publish(WorldEvent) → actor · participants · witnesses (12m)
        C->>D: FlushDialog (drain lines, schedule WantSpikeEvents)
        C->>C: SweepDeaths (lazy-cancel dead entities' events)
    end
    E-->>W: reschedule at FireTick + 5 (drift-free)
```

**Duration 1 is the norm and costs nothing.** An instantaneous verb begins and
settles inside one `ActOnce`, so the commitment branch never runs for it and
the beat is exactly what it was before activities existed. Only an action that
asks for more beats is ever observed in `Entity.Current`.

### Commitment (what makes an entity STAY)

Resolved by comparison against the challenger's weight, never by folding the
incumbent into the roll — a shift weighed 60 against food weighed 20 would be
abandoned with probability 0.25 on *every* beat, and over a whole shift that is
a certainty rather than a risk. Per-beat rolls compound; thresholds do not.

| Term | Weight | When |
|---|---|---|
| `Baseline` | +4 | always — unwillingness to reopen a settled question |
| `Momentum` | +18 | ramps in over the first quarter of the duration, then flat |
| `SettlingPeak` | +10, −3/beat | covers the start, where momentum is weakest |
| `NearlyThere` | +6 | past 75% elapsed |
| `DistressMax` | up to −18 | worst need below the 25% critical line |
| `AddressedBy` | −6 | an un-stale utterance is sitting in `Mind.Deposited` |
| `WhisperPull` | −10 (÷2 stranger) | a player whisper the activity does not already answer |

### The weight pipeline (what makes an entity "want" an action)

```mermaid
flowchart LR
    B["Base<br/>(Idle 8, Converse 2)"] --> T["ActionCandidate<br/>TotalWeight"]
    BH["Behaviors<br/>affect × scalar × keyword matches"] --> T
    R["Relationship<br/>affinity toward target"] --> T
    ST["Status<br/>HP/hunger/mood bands →<br/>keyword boosts/penalties"] --> T
    SIT["Situation (dialog)<br/>reply-pull · covet-pull"] --> T
    P["PlayerInfluence<br/>strength × mood ratio<br/>÷2 stranger · ÷2 out-of-character"] --> T
    T --> SEL["SelectWeighted<br/>(weighted random)"]
```

Everything keys off **keywords**, never action names — a new keyworded action
automatically participates in status/mood/behavior systems.

---

## Layer 3 — Subsystems

### 3a. Items: JSON → inventory → effects

```mermaid
flowchart LR
    J["data/items/steak.json<br/>enablesActions · effects · descriptors"]
    F["ItemFactory.LoadFromFile<br/>(validates every effect type<br/>is registered)"]
    DI["DataDrivenItem"]
    EX["action.Execute →<br/>item.Apply(actionName)"]
    FX["effects with trigger == action:<br/>Damage / Heal / Feed / Relationship<br/>ConsumeItem = marker → uses−−"]

    J --> F --> DI -->|"in Entity.Inventory"| EX --> FX
```

Descriptors (axis → valence) double as the mind's vocabulary: the same JSON
block feeds item effects *and* what entities believe/want about the item.

### 3b. Dialog: one Converse beat

```mermaid
sequenceDiagram
    participant Sp as Speaker
    participant DS as DialogService
    participant Wit as Everyone in earshot (6m)

    Sp->>DS: Converse(speaker, addressee)
    DS->>DS: BuildUtterances — reply / self-claim / boast /<br/>rumor / offer / threat / pleasantry
    DS->>DS: SoftmaxPick (temperature 1.5)
    DS->>Wit: Deliver — one rendered signal line, broadcast
    Wit->>Wit: Digest → doubt gate (skepticism) →<br/>NudgeBelief · mood · relationship
    Wit->>Wit: SurfaceDesire → Want > 0.35 ⇒ DesireReferent<br/>obsession gate (impulsiveness) ⇒ PinnedWant + spike
    Note over DS: offers route to ReceiveOffer:<br/>sigmoid accept → item transfer
    DS-->>Sp: signals + spikes buffered — controller drains via FlushDialog
```

`WantSpikeEvent` then decays obsessions on the wheel (×0.65 every 10 ticks
until below 0.3). The player's `Whisper` (rumor screen) enters this exact
pipeline; the player's `Influence` lever instead writes
`Entity.PendingInfluence`, consumed in the weight pipeline above.

### 3c. Frame pipeline (3D + 2D share one canvas)

```mermaid
flowchart LR
    U["OnUpdate<br/>Pump sim (unless SimPaused)<br/>route input: world vs menus"]
    R3["3D pass into 480×270 target:<br/>grid · axes · floors · props · pawn cubes"]
    R2["2D pass, same target:<br/>SceneManager.Draw (menus)<br/>DebugHud (F3)"]
    BL["Nearest-upscale blit<br/>to window"]
    U --> R3 --> R2 --> BL
```

### 3d. Input: device → intent

```mermaid
flowchart LR
    Dev["Silk keyboard/mouse"] --> Ad["SilkInputAdapter"]
    Ad --> IS["InputState&lt;GameAction&gt;<br/>held / pressed / released<br/>mouse delta · scroll"]
    BJ["data/input/bindings.json<br/>(rebindable, no recompile)"] --> IS
    IS -->|"world mode"| Cam2["camera: orbit / pan / zoom<br/>Esc ⇒ push PauseScene"]
    IS -->|"menu mode"| UIIN["UiInput (Up/Down/Confirm/Cancel)<br/>→ top scene only"]
```

---

## Feature status table

| System | State | Proof |
|---|---|---|
| Tick wheel / events (Engine.Ticks) | ✅ Solid — wrap-aware, lazy-cancel, drift-free | headless SmokeTest run |
| Item pipeline (Engine.Items + data) | ✅ Solid — validated JSON, 12 items | items load in both UIs |
| Data-driven world (entities + locations) | ✅ WorldBuilder + data/entities, data/world (2026-07-17) | both harnesses run it |
| Procedural worldgen (regions/settlements/residents) | ✅ Seeded, pool-driven, deterministic; one settlement simulated live (2026-07-18) — see specs/implemented/WorldGen_Design.md | `--gen 42` smoke + dump |
| Purpose systems: Work-as-choice, factions, wildlife, loot-on-kill | ✅ Landed 2026-07-18 — see specs/implemented/SimDepth_Purpose_Plan.md outcome log; keyword-aware clamped relationship weighting fixed en route | SoakTest: first surviving settlement |
| Soak/stress harness (tools/SoakTest) | ✅ Headless `--gen` runs, N-thousand ticks, economy/violence stats, `-v` verbose | 4000-tick runs at ~20–50k ticks/s |
| Personal space / separation | ✅ 2026-07-18 — `Entity.Radius` footprint (default 0.6); `EntityService.ResolveOverlaps` relaxation pass after every decision beat keeps pairs ≥ sum-of-radii apart. Interactions still happen at range (3m); entities never envelop each other | smoke screenshot + soak |
| Arrivals/departures (Track D) · status-band rethink (P2.19) | 🔲 Next — deprioritized/flagged in design review | — |
| Decision loop / weights | ✅ Solid — 6 weight sources | SmokeTest + console |
| Long-running actions (jobs) | ✅ 2026-08-26 — `IAction.Duration/Sustain/Resolve` (defaults keep every existing verb instantaneous), `Entity.Current` activity, `CommitmentCalculator`, `EntityService.Focus`; `WorkAction` is a shift paid on performance. See specs/implemented/Activities_And_Events.md | soak `-v`: `Work (1.00)` … `Work, cut short (0.50)`; probe selftest PASS |
| Event spine / distributor | ✅ 2026-08-26 — typed `WorldEvent` ledger behind every announce, audience tiers actor/participant/witness (12m), `IEventReaction` seam. Witnesses reach reactions always, logs only when loud | SmokeTest: 293 witness slots reached in a 3-entity world |
| Dialog / minds / desires | ✅ Implemented (see specs/implemented/Dialog_Implementation_Map.md) | console MindScreen |
| Influence / rumor (player verbs) | ✅ Implemented | console screens |
| Console UI (9 screens) | ✅ Working — now the debug fallback | default run path |
| 3D window/camera/meshes/OBJ | ✅ Working | smoke screenshot |
| Rebindable input | ✅ Working | bindings.json |
| 2D UI foundation (scenes, font, images) | ✅ Working — pause + settings stub | smoke screenshot |
| Debug HUD (text over 3D) | ✅ Working — F3: roster stats, decisions, full signal log | smoke screenshot |
| Per-entity logs + tiered speech bubbles | ✅ 2026-07-18 — every announce writes actor/other dossiers; LOUD tier (speech, deals, violence, deaths) renders as bubbles over pawns; quiet tier is dossier-only. Replaced the bottom SignalTicker (removed as too noisy) | smoke screenshot |
| Click-to-inspect (mouse picking) | ✅ 2026-07-18 — left-click picks the nearest projected pawn; 2-page micromenu (Log / Items, Enter flips, Up/Down scrolls) rides beside the live sim; click another pawn to retarget | smoke screenshot |
| Title / settings / inventory-verbs / interaction / conversation UI | 🔲 Next — see specs/active/UI_2D_Interface_Plan.md build order | — |
| Regions/settlements view | 🔲 Deliberately separate controller (not UI scenes) | project decision |
| Item PNGs beside bubbles | 🔲 Future project (user-flagged) | — |
| Audio, save/load | 🔲 Not started | — |

## How to run

```
dotnet run --project src\EntityARestaurantGame            # console UI (debug fallback)
dotnet run --project src\EntityARestaurantGame -- --3d    # 3D + 2D interface
dotnet run --project src\EntityARestaurantGame -- --3d --smoke   # auto-close + screenshot
dotnet run --project src\EntityARestaurantGame -- --gen 42 --3d  # procedurally generated world
                                                                  # (--gen alone = fresh seed; --dump
                                                                  #  writes it out as content files)
dotnet run --project tools\SmokeTest                      # headless sim exercise
```

In 3D: WASD pan · right-drag orbit · scroll zoom · **Esc** pause menu ·
**F3** debug HUD. Fonts: drop a `.ttf` in `data/fonts/`. Menu art: drop images
in `data/ui/`.
