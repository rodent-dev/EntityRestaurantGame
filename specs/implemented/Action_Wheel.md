# The Action Wheel — surfacing candidates to the player

Landed 2026-08-26. Answers the standing gripe that the game "kind of plays for
you": the sim had a rich verb set and the player had no way to see it, let
alone address it precisely.

---

## The thing that was actually in the way

The whisper matched on the **action name**:

```csharp
(string.IsNullOrEmpty(influence.TargetActionName)
    || candidate.Action.ActionName == influence.TargetActionName)
```

`ApplyInfluence` is a weight source, and the project's standing rule is that
weight sources match keywords and never test `ActionName`. This was the one
place the rule was broken — and it is exactly the place that made a category
wheel impossible, because "the player's vocabulary" was a list of hardcoded
strings rather than a projection of what the sim already understands.

Fixing that is the feature. The wheel is not a UI bolted on top; it is the
influence system finally speaking the language everything else speaks.

## Where categories come from

Nowhere new. `EntityService` already held the taxonomy, private:

```csharp
private static readonly string[] HostileKeywords = ["Aggressive", "Violent", "Offensive"];
private static readonly string[] WarmKeywords    = ["Social", "Supportive", "Diplomatic"];
```

Those moved to `Game.Contracts/ActionTaxonomy.cs`, which now serves two readers
that must never disagree: relationship weighting (*does this verb threaten or
warm the person it lands on?*) and the wheel (*what drawer is it in?*). One
table, two consumers, no drift — and a new action lands in the right ring for
free, the same way it already picks up status and behaviour modifiers.

Rings: **Social · Combat · Self · Labor · Move**, ordered by how often a
narrator reaches for them rather than alphabetically, because the wheel is
muscle memory and the order must stop moving.

An action can belong to two rings and that is correct, not sloppy: `Defend` is
`SelfPreserving, Reactive, Defensive`, so it appears under both Combat and
Self. "Fight" and "look after yourself" should both surface it. The wheel is a
graph, not a tree.

## Superset matching, and why breadth is emergent

A candidate answers a whisper when its action carries **every** keyword the
whisper named (`ActionTaxonomy.Answers`).

That single choice gives specificity for free:

| Whispered | Carries | Also argues for |
|---|---|---|
| `Converse` | Social, Diplomatic | Recruit, Speak — they carry those plus one more |
| `Recruit` | Social, Diplomatic, **Binding** | only itself |
| `Speak` | Social, Diplomatic, **Persuasive** | only itself |
| `Attack` | Aggressive, Violent, Offensive | only itself |
| `Idle` | SelfPreserving | Consume, Defend — "look after yourself" |

"Go and talk to them" is vaguer than "ask them to join us" and nobody authored
that. It falls out of how specific the verb you pointed at was.

Overlap-matching would have collapsed it: `Attack` and `Grapple` share
`Aggressive`, so every whisper for one would argue for the other and precision
would be unreachable.

**`Binding` and `Persuasive` are new.** Converse, Recruit and Speak were all
exactly `[Social, Diplomatic]` — keyword-identical, and therefore indivisible
to a keyword whisper. One distinguishing keyword each fixed it. Nothing in
`data/worldgen/archetypes/` or `StatusWeightCalculator` references either
keyword, so no weights moved — confirmed by soak runs coming back
**byte-identical** across four sim seeds.

## The rings

```
ring 1  category    Social · Combat · Self · Labor · Move   (+ count of what's askable)
ring 2  verb        Attack · Grapple · Defend               (blocked ones greyed, with a reason)
ring 3  who         nearest first, "(no one in particular)" when self-usable
ring 4  what with   "(whatever comes to hand)" + each distinct item
```

**Leaf-only.** Rings 1 and 2 navigate; the whisper fires at the end of the
chain. One whisper is one specific act the narrator wanted, not a mood — rings
that could fire on their own would make "Combat" and "Attack Yorick with the
knife" the same kind of object at different breadths, and the standing order
would stop reading as a sentence.

A ring with nothing to choose is skipped, not shown empty. A self-targeted verb
has no *who*; a verb with one viable item has no *what with*, because asking
someone to confirm the only fist they own is a keystroke that buys nothing.

`ItemId` on `Influence` is the genuinely new axis. A beat is made of verb,
target and item, and the item was previously chosen by a die roll the narrator
had no voice in — "attack him" and "attack him with the kitchen knife" were the
same instruction. A named item that turns out not to be viable falls back to
the ordinary roll rather than failing the beat: a whisper is a bid, and bids
can be impractical.

## Blocked entries stay

`PlayerAffordances` keeps unavailable verbs on the list with a sentence:

```
Enchant  [Social,Supportive]        BLOCKED — nothing on you to do that with
Recruit  [Social,Diplomatic,Binding] BLOCKED — there's nobody for that
```

Stolen wholesale from Luna's `Affordances.cs`, whose argument is the right one:
knowing why you **can't** is most of what makes a limit feel like a world
rather than a missing feature. Reasons are ordered by what the player can act
on — "nobody for that" reads as *go somewhere*, "nothing on you" reads as *go
and get something*.

## What is cached, and why it can be

The verb set is fixed at world build — `ActionModule` is a `SortedDictionary`
populated once and never touched — so rings 1 and 2 are built **once per pawn**
and reused. Only rings 3 and 4 are resolved on entry, because targets and
viable items are the only part that churns beat to beat.

## Layout

The panel is docked to the left edge, which the party strip also owns along the
bottom. `PartyHud.Footprint(font, small)` publishes the strip's real height and
the scene is handed it, rather than carrying a magic number that silently goes
wrong the day the font changes.

At 480×270 the chrome is the constraint: an early version spent five lines on
header and footer and squeezed the list to three scrolling rows — the one part
anyone opens the panel to read. Two lines of header, one of footer, and a list
row an inch tighter than a prose row, and all five categories fit with no
scrolling.

## Verified

- `dotnet build` — 0 warnings across all four projects.
- `DecisionProbe --selftest` — PASS; nothing here rolls dice.
- `SoakTest` seeds 1–4 at 3000 ticks — **identical** to the pre-change numbers
  (survivors 1/0/1/2, same work/violence/loot/talk/death counts). The rewrite
  is behaviour-neutral for the sim, which is the point: it changed what the
  player can SAY, not what anyone does.
- `SmokeTest` prints the whole wheel for two pawns, keywords and blocked
  reasons included. A merchant's wheel has no Combat drawer at all — emergent
  from their verb set, authored nowhere.
- 3D smoke screenshot shows the panel over a live sim, all rings drawn, clear
  of the party strip.

## Not done

1. **It is a nested list, not a radial wheel.** The structure is what mattered;
   the presentation can become a real wheel without touching anything below the
   scene.
2. **Whisper strength and persistence are untouched.** A whisper is still ~+9
   against ~234 selection mass per beat and is still spent on the first landed
   action. The wheel makes the vocabulary reachable and precise; it does not
   make the player louder. Luna's `Influence.HoldsFor` is the port if that is
   wanted.
3. **The console `InfluenceScreen` stays a flat list** — it is the debug
   fallback. It shares the contract (keywords, never names), not the interface.
4. **`Labor` is thin.** Work's keywords come from the worldgen spec, so the ring
   exists but only fills for pawns with a job.
