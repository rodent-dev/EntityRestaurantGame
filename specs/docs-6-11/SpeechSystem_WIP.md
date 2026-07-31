# Speech, Dialog & Storytelling — Design Doc (INCOMPLETE / IN PROGRESS)

> **Status: incomplete.** This system is the deepest rabbit hole in the project
> and is only partially specified. The *architecture* (the three layers, the
> authored-fragment stance, the reuse of existing machinery) is settled. The
> *content model details* (fragment schema, selection scoring, memory schema,
> romance staging) are sketched but not finalized. Open questions are flagged
> inline with **[OPEN]**.

---

## Guiding stance

Target feel: **STALKER-like ambient aliveness** — the world feels alive not
because NPCs generate novel prose, but because **authored fragments are triggered
by a living simulation.** The cleverness is in *which* written line surfaces *when*,
against a world that's actually doing things.

Decision: dialog content is **authored, not generated** ("I don't even think it's
generated — it's possible it's already written"). Cleverness lives in *selection
and triggering*, not in a sentence generator. An optional LLM phrasing skin is
left as a *later, swappable* layer — never the spine.

---

## The three layers (the core architecture)

Dialog is not one system. It is three, and they fail for different reasons.
Conflating them is what makes dialog feel impossibly hard.

### Layer 1 — Content (what is true / what a character could convey)
Pure **game state**, no language. "I am hostile because your faction wronged mine."
"I have a task." "I know the lord is corrupt." This is the **registries, intents,
relationships you already have.** A character doesn't need AI to know it resents
you — it reads a grievance edge.

### Layer 2 — Selection (what it chooses to say, given what's true)
Out of everything true, what's **relevant and in-character** right now? Same
**weighted-selection machinery** used everywhere else (options × weights → pick),
where weights come from disposition, relationship, occupation, memory. **The
aliveness lives here.**

### Layer 3 — Surface (the actual words)
The only layer where natural language lives. Primary implementation = **authored
fragments** (written lines/arcs with reference slots). Optional later =
**LLM phrasing** of the same structured intents.

> Spell-system parallel: meaningful decision (layers 1–2) = sim truth, like the
> impact tick; words (layer 3) = cosmetic render, like the render cue. Same
> truth/presentation split. This is why "emergent dialog" collapses into "a
> renderer for structured speech intents."

---

## Speech intent (the unit)

A unit of dialog is a **structured speech-intent object**, not a string — a
"micro-story" with slots:

```
SpeechIntent {
    type:       enum   // Advice | Quest | Threat | Information | Random | Flirt | ...
    subject:    ref    // the grievance / quest / fact it's about
    target:     ref    // who it's aimed at
    intensity:  scalar
    references: ref[]   // in-world objects to slot in (place, faction, person)
    voice:      tag     // selects phrasing register (gruff merc vs. courtly lord)
}
```

Speech intents are **emitted by existing systems reading state** — the drama /
relationship systems already know there's a grievance; emitting a `Threat`
speech-intent about it is just those systems *narrating themselves* in structured
form. Not a separate dialog AI.

**[OPEN]** Exact field set / schema for SpeechIntent. The above is a sketch.

---

## Conversation structure

A conversation = a **sequence of speech-intents (message-beats)**, fully resolved,
not one blob. This reuses the **channel / interrupt pattern** wholesale:

- A dialog is an **intent** ("this dialog arc"); each message is a **beat** that
  advances it.
- Interruptible (`Cancel` — someone walks off, combat starts), stallable, resolvable.
- **Player replies are also beats** that branch the sequence and **write back to
  state** (a threatening reply lowers standing). Conversation is thus a two-way
  exchange of speech-intents that modify the registries — looping back into
  everything else.
- Triggered by **events** (enter a place, a drama fires). A line of dialog is an
  event firing with a speech-intent payload.

**[OPEN]** Branching model for player replies — fixed reply options per beat,
or reply *intents* the player assembles? Leaning fixed options that map to intents.

---

## Content authoring model ("everyone has a story")

The illusion of "every character has their own story" comes from:

- **Shared pool** of generic small-stories for random NPCs (a merchant grumbling
  about bandits, a soldier missing home), written once, assigned to many,
  **personalized by slotting in their faction / place / grievance** via references.
- **Bespoke arcs** for a handful of named/important characters.
- Not all dialog is productive — **random small-stories are engaging on their own.**

So: shared pool + per-character reference slots + a few bespoke arcs. **Content-
heavy, not system-heavy** — fillable incrementally forever.

Character identity feeding selection: **behaviors, factions, and (new) an
occupation list.** Occupation gives a character a domain of things to talk about.

**[OPEN]** Occupation list contents and how strongly occupation gates fragment pools.

---

## Memory (NEW SYSTEM — we don't have this yet)

Memory turns "NPC says a line" into "NPC remembers what you did." Critical
restraint: **memory is NOT transcript storage.** You store *what changed and what
happened*, as state — not what was said.

- A character "remembers" you killed their friend via a **grievance edge with a
  cause-tag and timestamp**, not a stored sentence.
- Memory = **tagged relationship edges + a small, capped per-character log of
  significant interactions.** Old/minor memories **decay out**; sharp/high-magnitude
  ones persist.
- "Feeling remembered" = the speech-intent generator can **read your shared
  history** off these edges/log. No transcripts, no LLM memory.

Significance: **high-magnitude interactions write durable memory** (betrayals,
rescues, torn-loyalty incidents); small-talk doesn't. The **drama events that
already detect significance are exactly the ones that should leave memory** — ties
memory to existing systems.

> This is the same continuous-vs-discrete / store-the-significant-discrete-thing
> pattern as dormancy and debts. Memory wants to be discrete tagged events.

**[OPEN] — the decision that shapes the data model:** memory as **purely
relational** (only edges with cause-tags) vs. **edges + an event log** (the edge
*and* a short list of specific incidents).
- Pure edges → cheaper; enough for ambient barks ("cold to you because of a wrong").
- Edges + log → lets a character **recount the specific incident** (the night, the
  place). Needed for characters who recall *what you did* in detail.
- Likely answer: pure tagged edges as the baseline, a capped log for characters
  where detailed recollection matters. **Not finalized.**

**[OPEN]** Memory cap size per character; decay curve / what triggers forgetting.

---

## Dating / romance (NEW — but not a special system)

Romance is **an arc over the affinity edge**, with stages
(stranger → interested → courting → together → …). Each stage gates different
dialog fragments and interactions; progression driven by **affinity crossing
thresholds + compatible disposition.**

It is **not a dating-sim bolted on** — it's affinity edges progressing through
stages and **colliding with faction standing and grievances**:
- Romance across faction lines = a **torn-loyalty incident** waiting to fire.
- A partner whose party goes to war with yours = the **affinity-vs-standing
  conflict** already designed.
- Jealousy = a **grievance edge** spawned when affinity edges cross.

The authored content is the stage-gated fragments (flirtation, confession,
conflict, breakup); the **drama is emergent** from edge collisions.

**Tragedy for free:** build a relationship → die → start anew as a nobody → your
former partner **doesn't know you.** The persistent-world + start-anew + dating
systems intersect to produce tragedy nobody had to author. (Sign the design is
coherent.)

**[OPEN]** Romance stage list and threshold/disposition gates per stage.

---

## Optional LLM surface (LATER — explicitly deferred)

Layer 3 stays behind a single swappable interface:

```
string Render(SpeechIntent intent, CharacterVoice voice)
```

- **Now:** authored-fragment implementation (template/reference-slot fill).
- **Later (optional):** LLM implementation — hand the model the *structured intent*
  and a voice descriptor; it phrases the sentence only. **Caged to surface** — it
  cannot invent facts (only phrases a constructed intent), cannot decide
  relationships (decided below). The model is a *renderer*, not a brain.

Same intents either way; players with a local model could flip to LLM phrasing of
the exact same emergent story. (Same "interface, two implementations" pattern as
`ICurrency` and the renderer.)

---

## What's settled vs. what's open

**Settled:**
- Three-layer architecture (content / selection / surface).
- Authored fragments as the primary surface; LLM as optional later skin behind one interface.
- Speech-intent as the structured unit; systems narrate themselves into intents.
- Conversation = sequence of message-beats reusing the channel/interrupt pattern; player replies write back to state.
- Shared-pool + reference-slot + bespoke-arc content model.
- Memory = tagged significant-event state (edges ± log), decaying, written by drama events — NOT transcripts.
- Dating = affinity-edge staging colliding with existing standing/grievance systems.

**Open (flagged [OPEN] above):**
- SpeechIntent exact schema.
- Player reply branching model.
- Occupation list + how it gates pools.
- Memory: pure-edges vs. edges+log; cap size; decay.
- Romance: stage list + gates.
- Selection scoring: how fragments are weighted/matched (not yet specified — this
  is layer 2's core and needs its own pass).

> **Biggest unspecified piece:** the **selection scorer** — given a character +
> context + memory, how candidate fragments are weighted and chosen. This is where
> the aliveness lives and it has not been designed yet. Next pass should focus here.
