# Dialog / Exchange / Desire — Implementation Map

**Companion to** `specs/implemented/Dialog_Exchange_Reconciliation.md`. That doc is the design authority; this one says where each section landed in code, what the v1 magnitude curves are, and which knobs to turn. Built 2026-07-10.

## Where everything lives

| Reconciliation § | What | Code |
|---|---|---|
| §1 Two atoms | Assertion atom `(object, (axis, valence))` | `Game.Contracts/Claim.cs` |
| §1 | Valuation atom `(axis, weight)` | `MindModule.Preferences` (`Game.Contracts/MindModule.cs`) |
| §1 | Axis vocabulary | `Game.Contracts/Axes.cs` (9 axes, plain strings like action keywords) |
| §2 Feeling | `S_R(o) = Σ w·Σvalence` | `MindService.Feeling` |
| §2 Want | counterfactual `sat(have) − sat(¬have)` | `MindService.Want` / `EffectiveWant` (pin-aware) / `KeepValue` (the give-it-up side) |
| §2 Need | operator turned inward | `MindService.NeedsOf` (health→Safety, hunger→Nourishment, mood→Comfort, material axes→tanh of holdings) |
| §3 Stat fold | competence inside the counterfactual | `MindService.Competence` (STR→Harm, CON→Safety/Nourishment, INT→Craft, CHA→Commerce/Beauty/Glory; stat/10) |
| §4 Credibility | `cred = f(rel_R(object), rel_R(speaker))` | `DialogService.DigestClaim`: `trust = σ(rel/4)`, `cred = trust · σ(1.5 − m)` where `m` = mismatch with held belief |
| §5 Interpretation | stance selection {accept, doubt-subject, doubt-speaker} | `DialogService.DigestClaim`: `P(doubt) = σ(K·(m·skepticism − θ))`, blame-speaker weight `∝ m·(1−trust)`; rejection reflects onto the speaker's relation |
| §6 One selector | utterance candidates + softmax, deposit reweights next beat | `DialogService.BuildUtterances` + `MindMath.SoftmaxPick`; deposit = `MindModule.Deposited`; `ApplySituationWeights` bridges the pull into the *action* selector (new `ActionWeightSource.Situation`) |
| §7 Envelope | intent-type routing | `IntentType` enum + `Utterance` (`Game.Contracts/Utterance.cs`); `Digest` routes offers to the exchange handler, everything else through the claim gates |
| §8 Exchange loop | barter/coerce/threat one loop | `DialogService.ReceiveOffer`: `P(accept) = σ((pull − cost)/T)`; barter pull = want(sweetener)·(0.5+trust), coerce pull = w(Safety)·menace, cost = `KeepValue(demanded)`; menace reads the speaker's actual Harm pile + receiver's fear beliefs |
| §9 Items focal | inventory = standing candidate pool, salience gates surfacing | descriptors on items (`data/items/*.json` → `ItemDefinition.Descriptors` → `IItem.Descriptors`); only descriptor-bearing items talk/trade (fist stays silent) |
| §10 Engagement/tavern | broadcast to proximate, per-witness digestion | `DialogService.Deliver` (EarshotRange 6); only the addressee gets the deposit, witnesses digest beliefs/desires — bystander chime-in emerges as their next beat |
| §11 Obsession | the spent primitive: buff-as-IEvent | gate in `DialogService.SurfaceDesire` (`σ(K·(impulsiveness·salience − θ))` on first surfacing only), pin on `DesireReferent.PinnedWant`, decay in `Simulation/WantSpikeEvent.cs` on the tick wheel |
| §12 Desire referents | store pointer, recompute magnitude | `DesireReferent` (objectId + first-surfaced + context, never a score); `EffectiveWant` recomputes each read |

Wiring: `ConverseAction` (attached to everyone incl. drifters) hands the beat to `DialogService.Converse`; `EntityController` passes the dialog service into the pipeline (`ApplySituationWeights`) and drains its rendered lines + spike requests after each act (`FlushDialog`). The controller stays the WHEN, the services the HOW.

## The v1 magnitude function (the one that was unwritten)

`sat(axis) = tanh( Σ owned descriptors on axis × competence )`, so:

```
want_R(o) = Σ_axis w_R(axis) · [ tanh(owned + v·comp) − tanh(owned) ]
```

- Satiation is the flat part of tanh — a second knife moves Aldric's Harm sat barely at all.
- Belief moves by `cred · valence` per accepted claim; rejection-onto-speaker costs `(1−trust)·|w·valence|·3` relation points.
- Desire trickle is zero by default (pure recompute); the spike pins `want × 2.0` and decays ×0.65 every 10 ticks until < 0.3.

Bounded as promised: it reads only relation, preference, stat, and inventory state.

## Knobs (all consts at the top of their file)

- `DialogService`: talk/accept temperatures, response-pull base/decay, offer-want threshold (0.5), earshot, doubt K/θ, obsess K/θ, covet→action scale.
- `MindService.DesireThreshold` (0.35) — what registers as a desire at all.
- `WantSpikeEvent`: decay interval/factor/floor.
- Per-character: `Mind.Skepticism`, `Mind.Impulsiveness`, `Preferences`, `SelfDescriptors`, stats — set in `Program.cs`.

## How to poke it in-game

1. Run the game, pick a pawn, auto-run — Converse beats now render as real lines in Events (talk-ups, rumors, offers, threats, deals, covets, ★ obsessions).
2. Inspect Entity → **View Mind**: live preferences, needs, desires (want recomputed, pins flagged), beliefs, and what they're mulling over.
3. Inspect Entity → **Whisper a Rumor**: subject → axis → slant, injected through the real digestion pipeline. Through your pawn it rides your pawn's credibility; without one it's an anonymous whisper at neutral trust. Watch a skeptic bounce it and a believer's beliefs drift.
4. `dotnet run --project tools\SmokeTest` for the headless version with a mind-state dump.

## Deferred (unchanged from the reconciliation)

- **Question / Quest generation** — types exist in the enum and route, but no candidate builder emits them yet.
- **Group engagement** (3rd speaker forces the pairwise question) — still pairwise + broadcast.
- **Party-membership weighting channel** — bystander chime-in currently rides plain witness digestion.
- **Believed vs. actual descriptors** — reception reads an item's true descriptor sheet when running the want counterfactual; a stricter version would project want from *believed* claims only.
