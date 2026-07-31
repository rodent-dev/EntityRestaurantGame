# Sim Depth — Purpose, Factions & Pressure (v2)

Revised 2026-07-18 after design review. Supersedes v1's stages with four
tracks reflecting the user's corrections:

1. **Duty must never be compulsion.** "Return to post!" would trade one blob
   for several blobs. Work is an *action candidate* competing in the same
   weighted selection as everything else — a choice, not an assignment.
2. **Bandits/threats mostly already exist** (archetype = behaviors + limited
   actions). What's actually missing is *relations at generation* (faction
   identity) and a working hostility channel.
3. **Arrivals/departures matter but come last.**

## 0. Verified findings that shape the plan (code-checked 2026-07-18)

- **F1 — Relationship weighting is action-blind** (`EntityService.cs:85`):
  the relationship score is added flat to EVERY candidate targeting that
  entity. Consequence: a bandit generated at −8 toward settlers is *less*
  likely to attack them than a stranger — hate currently means *ignore*, not
  *fight*; friendship boosts Attack-a-friend as much as Converse-with-friend.
  The 6-tier system exists; this tier is sign-blind. **Must fix first.**
- **F2 — Converse targets anyone alive** (`ConverseAction.CanTarget`):
  without a guard, entities will interrogate wolves (and wolves would digest
  the claims into their minds).
- **F3 — Attack requires an enabling item**: animals need a natural weapon
  item (`fangs`), same pattern as `fist`.

---

## Track A — Work: purpose as a choice

**`WorkAction`** — a real `IAction`, self-targeted, in the normal candidate
pool. *No override, no station reflex.*

- **Data** (archetype/EntityDefinition): `"work": { "location": "kitchen",
  "produces": ["bread_loaf", "steak"], "maxStock": 5 }`. No work block = no
  Work candidate (patrons don't get one).
- **`CanActorUse`**: has a work block, and stock below `maxStock`.
- **Keywords are job-suited, per the design review**: the work block carries
  its own keyword list — `"keywords": ["Industrious", "Pride", "Greedy"]` for
  a cook, `["Industrious", "Defensive"]` for a future patrol job. Different
  jobs appeal to different natures, as in life. `Industrious`/`Pride` are new
  open keywords (keywords are free strings); producer archetypes' behaviors
  gain them (e.g. a Craftsmanship behavior with `[Industrious, Pride]`).
  `Greedy` on food-work means the hunger-deficit status boost makes hungry
  producers work harder — need drives labor, emergently.
- **`Execute`**: outside the work location → one movement step toward it
  (new `EntityService.MoveTowardPoint`) + "Yorick heads to the kitchen";
  inside → create one item from `produces` (via the item factory, injected
  like ConverseAction's DialogService) + "Yorick bakes a loaf".
- **Distribution is the existing exchange loop** — zero new systems. Hungry
  entities already surface food wants; covet-pull already drives
  Converse-at-holder; barter/coercion already transfers items. Add a **`coin`
  item** (weight ~0, Commerce descriptors) to patron spawn inventories so
  they have something to offer. Broke aggressive patrons attempting coercion
  for bread = the story generator working as intended.
- **Hunting is NOT a bespoke action.** It emerges: hungry + Aggressive +
  negative stance toward prey (Track B) → Attack wolf → loot-on-kill
  (Track C) → eat `raw_meat`. If soak runs show hunting never fires, a thin
  Hunt specialization can come later — not before the emergence is given a
  chance.

**Emergence guards:** Work competes on equal terms — a charismatic patron can
lure the cook into gossip; a low-Industrious server shirks; hunger can make
the cook eat his own stock. All features, not bugs.

**Success test:** 2000+ tick soak — majority alive, `produced > 0`, coins
change hands, and *some* work beats lost to socializing (proof choice
survived).

## Track B — Relations that mean something

### B1 — Fix F1: keyword-aware relationship weighting
In `ApplyBehaviorWeights`, replace the flat add (source stays `Relationship`,
the tier structure is untouched):

- **The curve is superlinear** (design review): `magnitude = rel² / 8` —
  distaste (−3) contributes ~1, dislike (−6) ~4, hatred (−9) ~10. Mild
  feelings are conversational color (dialog trust already reads raw rel);
  only real hatred makes lashing out *tempting*, and only real love makes
  violence unthinkable.
- **rel > 0**: `+magnitude` on candidates vs that target with Social /
  Supportive / Diplomatic keywords; `−magnitude` on Aggressive / Violent ones.
- **rel < 0**: `+magnitude` on Aggressive / Violent / Offensive candidates;
  `−magnitude` on Social / Supportive / Diplomatic (you don't gossip with
  people you hate — you glare across the room, then act).
- rel == 0 / unknown: nothing, as today.

Pure keyword rule, same shape as `StatusWeightCalculator` — never checks
action names. Immediate payoff even before factions: friends stop randomly
knifing each other mid-conversation.

### B2 — Factions at generation
- Archetypes gain `"faction": "settlers"` (bandits → `"bandits"`, wolves →
  `"predators"`, deer → `"wildlife"`).
- **`data/worldgen/factions.json`** — a stance matrix of ranges, sampled per
  entity-pair at generation and materialized into `EntityRelationships`
  (both directions sampled independently — asymmetric grudges are flavor):

```jsonc
{
  "settlers":  { "settlers": [1, 6],  "bandits": [-9, -5], "predators": [-6, -2], "wildlife": [-2, 0] },
  "bandits":   { "settlers": [-8, -4], "bandits": [0, 5],   "predators": [-5, -2], "wildlife": [-3, 0] },
  "predators": { "settlers": [-6, -3], "bandits": [-6, -3], "predators": [0, 2],   "wildlife": [-8, -5] },
  "wildlife":  { "settlers": [-3, 0],  "bandits": [-3, 0],  "predators": [-9, -6], "wildlife": [0, 3] }
}
```

- Replaces the current 30%-random sprinkle (keep a little intra-faction
  noise on top). Nothing freezes: dialog/beliefs keep moving relations after
  generation, so faction drift and betrayal stay possible. Trust in dialog is
  already `Sigmoid(rel/4)` — faction stance flows into credibility and offer
  acceptance for free.

**Success test:** controlled beat test — a −8 bandit picks aggressive
candidates against settlers at a visibly higher rate than neutral strangers
do; co-faction violence drops in soak stats.

## Track C — Wildlife & hostiles (mostly content, three small mechanics)

- **The outdoors**: venue templates gain a surrounding `grounds` /
  `wilds` AABB ring (venue sits inside, e.g. wilds 44×40 around a 20×20
  tavern). Archetypes gain `"habitat": "wilds"` (spawn location tag; default
  = venue interior). Renders automatically as another floor pane; movement
  scale stays sane (~12 decisions to cross).
- **Animal archetypes** (`data/worldgen/archetypes/`): wolf (predators;
  `fangs`; carries `raw_meat`; Predatory `[Aggressive, Greedy]` + Skittish
  `[SelfPreserving, Reactive]`; actions Attack/Defend/Consume/Idle — **no
  Converse, no minds needed beyond defaults**), deer/boar (wildlife; passive
  Defend/Consume/Idle; meat carrier), bear (predators, tanky, rare `[0,1]`).
- **Bandit archetype**: bandits faction; Greedy/Aggressive behaviors; keeps
  Converse — bandit *threats and coercion offers* route through the existing
  exchange loop (shakedowns emerge, not scripted).
- **New items**: `fangs` (infinite natural weapon, enables Attack/Defend —
  fixes F3), `raw_meat` (Consume: Feed ~30, 1 use), `coin` (Track A).
- **Mechanic 1 — loot-on-kill** (one rule, two fantasies): in
  `ActOnce`, if the target was alive before `Execute` and dead after, the
  killer takes its inventory + "X loots Y". Hunting (wolf → meat) AND
  banditry (victim → coins) from the same rule.
- **Mechanic 2 — the comprehension spectrum** (supersedes the original
  "Converse guard" after design review — *never hard-wall an interaction
  channel; vary the flavor*): anyone may Converse at any creature. Verbal
  receivers run the full claims path; nonverbal receivers get the **tone
  read** (`DigestTone`): threats condition fear, soothing walks relationship
  up at odds scaled by the speaker's **beast-affinity (Wisdom/10 — Wisdom's
  first real job)**, and offers/coercion work unchanged (food held out to a
  wolf needs no words). Nonverbal witnesses ignore overheard language;
  utterances aimed at them auto-fall-back to wordless soothing so the beat
  is never wasted. **Taming is emergent**: repeated soothing → relationship
  climbs → the curve suppresses aggression both ways. Verified by the
  SoakTest `-tame` probe: wisdom-10 cook took a deer from rel −2 to +5 in
  30 attempts.
- **Mechanic 3 — habitat spawn** in the generator (a few lines: spawn
  position sampled inside the habitat tag's AABB instead of the venue's).

**Success test:** soak shows wolves hunted for meat when food runs short,
bandit coercion signals appear, and post-raid co-settler relationships trend
*up* (common-enemy bonding) — measured, not assumed.

## Track D — The door (explicitly last)

Seeded `ArrivalEvent` (spawn from archetype pools at the wilds edge — also
how bandits/wolves replenish), departure desire for sated patrons
(`MakeInactive` + signal on crossing the threshold). Population oscillates
instead of monotonically dying. Config knobs: `arrivalInterval`,
`maxPopulation`. Unchanged from v1 sketch otherwise.

## Build order & the soak harness

| # | What | Size | Why this order |
|---|---|---|---|
| 1 | B1 relationship-weighting fix | S | Verified defect; everything downstream depends on hostility working |
| 2 | `tools/SoakTest` — headless `--gen` run, N-thousand ticks, stats (population by faction over time, items produced/eaten, violence count, relationship drift). **This is also the stress-test vehicle** — crank archetype `perSettlement` and watch throughput | S | Can't evaluate any track without it |
| 3 | Track A — Work, produces, coin | M | Ends the heat death |
| 4 | Track B2 — factions.json + materialization | S | Rides on B1 |
| 5 | Track C — grounds, animals, bandits, loot rule, converse guard | M | Content + three small mechanics |
| 6 | Track D — arrivals/departures | S | Deprioritized per review |

## Outcome log — first implementation pass (2026-07-18)

Tracks A, B, C + the soak harness landed same-day; Track D deferred as
planned. Soak-driven findings and fixes along the way:

1. **Hunger pacing**: 5/pulse starved settlements in 200 ticks — faster than
   goods could circulate. Now 2/pulse (500 ticks), starvation 3→2 HP.
2. **Runaway murder spiral (important)**: the superlinear relationship curve
   fed back through combat's own relationship damage (each Grapple: −5 →
   rel −25 → curve weight ~78 → nothing else could win a roll — verbose soak
   showed the *cook* knifing patrons by t47). Fixed by clamping the curve's
   input to ±10 (stored grudges stay unbounded; *temptation* saturates) and
   softening fist-grapple's relationship hit to −2.
3. **Depleted items lingered in inventories** — spent consumables now removed
   after use (they were also inflating food counts in the soak stats).
4. **User-flagged, open (P2.19 in RECOMMENDATIONS.md)**: the status deficit
   bands need a rethink — Defend triple-matches the boosted keywords, so
   distressed entities over-defend instead of addressing the failing need.

**Result**: seed 42, 4000 ticks — first-ever surviving settlement. Violent
settling phase (~t0–1000, 6 dead) then stable equilibrium: cook bakes and
eats his own stock, deer grazes, violence flat (177→181 over 2500 ticks),
starvation pulses 283 → 17 across the iterations. The heat death is broken;
the settling-phase intensity is the next balance target (tied to P2.19).
Throughput ~30–50k ticks/s headless — ample stress-test headroom.

## Affinity pull — "when do I invest in a being?" (added 2026-07-18)

Design review generalized the taming question: growing a faction, courting a
regular, befriending a wolf are the SAME decision — willingness to spend
beats gaining someone. The user's four factors, mapped onto machinery that
already existed (only the trait was new):

| Factor | Mechanism |
|---|---|
| Relation with creature | The existing relationship weight tier (clamped curve) — unchanged, separate |
| Opinion (qualities you seek, personal bias) | `MindService.Regard`: Σ preference-weight × accumulated belief about the target, saturated |
| Value of creature | `MindService.Want` over the target's `SelfDescriptors` — the same counterfactual used for items ("person, item, faction are all descriptor-bearing objects" finally cashed in) |
| Difficulty of taming | Folded into descriptors per review: negative valences (wolf `Comfort −0.3`, bear `−0.5`) repel comfort-valuers through the same math; the Wisdom gate in `DigestTone` covers execution difficulty |

`pull = Sociability × (value + regard) × AffinityActionScale`, applied to
Social-keyword candidates in `ApplySituationWeights` (third term beside
reply-pull and covet-pull), clamped [−6, 12] — admiration is a pull, never a
compulsion. **`Mind.Sociability` is the introvert dial** (0..1): it scales
seeking, never replying. Archetypes now carry sociability spectra (merchants
0.6–0.9, bears 0.05–0.15) and animals carry descriptor sheets, so who
invests in whom varies per generated person.

**Open question (user's "descriptors with affects toward specific actions")**:
a generalized aura bridge — target descriptors adding weight to specific
action *keywords* against them (e.g. `Harm` on a bear discouraging
Aggressive candidates from the prudent, `Beauty` inviting Social ones) —
would be one data table + one loop in the weight pipeline. Sketched, not
built: awaiting a decision on whether the affinity pull already covers
enough of it.

**Note on trait plumbing**: adding Sociability exposed shotgun surgery in
the mind-trait pipeline — logged as P2.20 in RECOMMENDATIONS.md (traits
should become a dictionary; do before adding the next trait).

## Deliberately NOT doing

- No scripted schedules, no behavior trees, no AI director — purpose stays
  emergent from weights or influence/dialog stop mattering.
- No new need types; Nourishment/Safety/Comfort already cover jobs/threats.
- No parties/dormancy yet (checkpoint-doc altitude, after these tracks).
