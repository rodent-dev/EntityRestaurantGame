# World, Economy & Narrator — Design Checkpoint

A checkpoint of the systems settled since the tick/event work. Scope frame:
a story-builder in the vein of Sands of Salzaar / Kenshi / RimWorld — few regions,
settlements as trade/production anchors, mobile parties as the unit of agency,
the player as a disembodied storyteller acting *through* a favored party by
influence rather than command.

Everything here reuses the existing engine (timer wheel, events, intents,
relationship registry, places). No new bespoke subsystems were introduced.

---

## 1. World structure & scale

The hierarchy, from largest to smallest:

- **Regions** — a few, each a map.
- **Settlements** — live within regions; the primary trade and production anchors.
  These are `Place`s. They produce resources in the background and host crafting.
- **Parties** — the mobile **unit of agency**. They move, fight, trade, and exist
  on the map. Pawns are *contents* of parties; factions are *alliances* of parties.
- **Factions** — one or more parties under a single **leader party**. Conflict is
  uniformly **party-vs-party**.

The party is the atom. This single choice resolves combat, capture, and faction
growth (below).

---

## 2. Crafting — flat, two-tier (NOT a deep survival tree)

Deliberately shallow, Kenshi/RimWorld depth — *technical enough*, not a 7-stage
tech ladder (no wood→plank→table→bench→furnace→anvil→gear).

- **Settlements passively produce base resources** (iron, cloth, food) — tick-driven.
- **Production buildings convert base resources → finished goods** via *flat
  recipes*: input set → output, one hop. Output is sometimes an item (weapon,
  armor), sometimes a **building** (a recipe whose output is a production capability).

So the entire crafting model is "flat recipes: inputs → output." The item tree is
purely **what-unlocks-what**, not an economy/valuation model. No value propagation,
no tree depth.

---

## 3. Economy — fixed prices + relationship discounts

Chosen simplification (over emergent valuation): prices are **fixed**, modulated
by **relationship goodwill** (better standing → better price). This skips the
valuation/negotiation inference layer entirely.

- **Trade executor** (already built, trivial): settles an agreed `IItem[]`-for-
  `IItem[]` swap atomically. It never negotiates — it only settles an agreement.
- **Money** stays item-based for anything that moves through the world (per earlier
  design), with the ledger layer reserved for relationship-held claims (debts).

---

## 4. Resource development formula

Settlement output is driven by several factors. **Not** a raw product of all of
them (that explodes); each factor is shaped independently so they interlock.

Factors:
- **Workers** assigned (more → more, up to a ceiling)
- **Land/resource upgrades** — the unlimited, exponential-cost / diminishing-return track
- **Production rate** — throughput multiplier (own upgrade track)
- **Employee-per-product ratio** — per-product constant; wheat is cheap per worker,
  cows expensive (same workers can't make 200 wheat and 200 cows in the same time)

Shape:

```
output = base
       × min(workers / ratio, ceiling(upgrades))   // labor SATURATES at the upgrade ceiling
       × rateMultiplier                              // throughput, own diminishing track
```

Key property: the `min()` makes **workers and upgrades interlock** — you can't
brute-force with workers alone (you hit the upgrade ceiling) and can't idle-upgrade
without workers to use it. This creates the real decision ("hire vs. upgrade")
instead of one number to maximize.

Upgrades are unlimited levels with **exponential cost**, so output rises
sub-linearly in *investment* even though it's unbounded in *levels* — the "always
can upgrade, never pays for itself like the last one" feel.

Runs as a per-settlement `WorldObligation` event firing every rate-interval;
formulas live inside `Fire` and inside the upgrade-purchase check. No new system.

---

## 5. Sieges — intentionally NOT supported

Conflict is **uniformly party-vs-party**. A siege would be party-vs-*place*, a
separate combat model — a bespoke exception.

Instead: **capturing a settlement = defeating the party/parties garrisoning it,
then flipping the `control` edge.** No wall HP, no siege engines. Territory control
is a relationship-registry edge; capture is "win the fight → rewrite the edge."
Consistent with everything else.

---

## 6. Party size caps — required, structural (not balance)

Parties have a **size cap** (a number, optionally nudged by the leader's traits).

The cap is load-bearing, not a balance knob: without it, the optimal play is one
giant deathball and the faction/party distinction collapses. The cap is what forces
a faction to **grow by adding parties**, not by inflating one — which keeps the
faction→party→pawn hierarchy meaningful, makes party-vs-party conflict a real
force-comparison, and makes the **leader party special** (its defeat threatens the
faction itself).

---

## 7. Narrator / influence model

The player is a **disembodied storyteller above the board** who acts **through a
favored leader/party** — not embodied in it, not an omnipotent hand.

Core verb is **influence, not command.** The favored party has its **own agency**
(goals, leader disposition, the same party-AI any other party runs). Narrator
skills **bias** that agency; they don't override it.

- Narrator influence = **another weight source** feeding the same weighted
  decision-selection a party already uses to choose actions (same shape as the
  relationship resolver: options × weights → pick). "Incite aggression" raises the
  weight of hostile options; it does not force an attack.
- The party can **resist** when its own goals strongly conflict (a cowardly leader
  resists incitement). **That gap — between what you nudge and what they do — is
  the story.**

**Design rule (mirrors the siege decision):** narrator skills are **amplified uses
of existing levers**, never new kinds of action. A "bolster" skill adds to the same
combat factors a party already uses; "incite conflict" lowers the same standing edge
a raid would. Never anything no in-world actor could do (no teleport/undo/omniscience)
— those can't route through existing systems and become special-case bypasses.

This makes power **emergence-safe**: even a maxed narrator is *very persuasive*, not
a puppeteer, because influence-over-autonomous-agents has a ceiling command doesn't.
**Optional by design** = how much the player invests in the influence track; ignoring
it sets a weight to zero, not a system to off.

---

## 8. Start & death model

**Start:** you begin as **one pawn — a nobody, no faction, a schmuck.**

**Death:** the **world persists; only you reset.** You are the transient thing, the
world is the permanent thing — the literal lifecycle expression of the
storyteller stance (you were never the protagonist the world revolves around).

**Chosen resolution: "start anew."** You respawn as a fresh pawn, **known by no-one**,
neutral/default standing toward everything. No legacy, no residual identity.

Why this (not "continue the legacy"): start-anew is **philosophically consistent**
with the disembodied-storyteller premise — the world genuinely doesn't revolve
around you, proven by its indifference to your return. "Continue the legacy" would
re-center you as a tracked special individual, fighting the premise. Start-anew is
also the **simplest** to build: spawn a default pawn, clear player identity, done —
no "do they remember you" logic.

- **No time-skip.** Death drops you back into the world **as it was**, minus your
  position. A time-skip doesn't fit — that the faction can live without you should
  be *evident*, not demonstrated by fast-forward. The player keeps a stable board.
- **Faction policy handles you as any stray.** You're treated however a faction
  treats unknown pawns — not a special death rule, just the faction's general
  disposition applied to a stranger who happens to be you.
- **Closed factions lock you out too.** If you built a xenophobic closed faction at
  the height of your power, its walls now keep *you* out. Not a punishment — the
  faction *working as designed*, applied to a stranger. Hard to re-indoctrinate by
  consequence, not by arbitrary knob. The story bites back.

---

## 9. Inner conflict, wrongs & rigs (storytelling layer)

With "start anew" and a persistent world, **faction roles switch** after your death
(your old leader seat is filled by someone else), which naturally seeds **inner
conflict**.

Direction (kept simple): a light layer of **wrongs** (grievances) and **rigs**
(arrangements / who-owes-who / who's-positioned-against-whom) that drives intra- and
inter-party tension. These are **relationship-registry edges** — grievance edges,
obligation edges — read by the drama/decision systems, not a new subsystem. Goal is
*good storytelling from simple state*, consistent with the rest of the design.

*(Open thread — to be developed further in discussion.)*

---

## Cross-cutting: why this all stays contained

Every system above is built from pieces already in place:
- settlements = `Place`s; territory = `control` edges
- production / drama / collection = tick-driven events
- combat outcomes & captures = registry edge rewrites
- narrator influence & relationship blends = the same weighted-selection shape
- death = spawn default pawn + clear identity (no special-case machinery)

No siege model, no economic valuation engine, no deep tech tree, no legacy/identity
tracking. The scope is small *because* the abstractions are shared.
