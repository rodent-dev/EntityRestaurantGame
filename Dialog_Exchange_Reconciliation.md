# Dialog / Exchange / Desire — Reconciliation Note

**Authority:** session synthesis, real engine vocabulary. Supersedes `DialogSystem_Spec.md` and `Interiority_Spec.md` where they conflict. Not the bible — source on PC remains canonical. Tags: **[INTENT]** = confirmed design intent, dissolves into existing rails · **[NEW]** = genuinely unbuilt, earns its place · **[UNWRITTEN]** = agreed shape, function not yet specified · **[DEFER]** = parked until runtime evidence.

---

## 0. The spine, one breath

One per-beat weighted **selector**. Two atom shapes with one **operator** between them. An utterance is a *bag* of assertion atoms wearing an **intent-type envelope** that routes which handler digests. Emission and reception are the *same* pipeline. Items are just high-salience objects the whole thing orbits. Everything below is that sentence expanded — and most of it is the operator or the selector wearing a costume.

## 1. Two atoms, one operator **[INTENT]**

- **Assertion side:** `(object, (axis, valence))` — a claim/descriptor/effect/reputation-entry. Never a bare scalar; that's the +3-affinity gift trap. Keep it axis-keyed always.
- **Valuation side:** `(axis, weight)` — a preference/value. The receiver's sheet.
- The **connection operator** crosses the gap: fact on one side, valuation on the other, additive, axis-grouped. Same brick builds two walls; do **not** unify the *roles* or the operator loses figure-and-ground.

```
S_R(o) = Σ_axis  w_R(axis) · ( Σ_{claims about o on axis} valence )
```

Past + present claims on one axis simply sum — this is why "but since March" needs zero connective structure.

## 2. The operator runs three ways **[INTENT]**

Same formula, different target:

- **Feeling** — `S_R(o)` over incoming claims. How R feels about content.
- **Want** — over a **counterfactual**: how much possessing `o` raises satisfied preference.
  ```
  want_R(o) = Σ_axis w_R(axis) · [ sat(axis | have o) − sat(axis | ¬have o) ]
  ```
  Satiation falls out of the delta for free: already-armored paladin → delta ≈ 0. Rogue vs armor → low weight, small delta. No class tables — the table is the character sheet.
- **Need** — the operator turned **inward**. Actor is `IDescribable`, so it emits its own state (health/warmth/wealth). Axes where own descriptors fall short of own weights *are* needs. Need = `want_R(self-improvement)`.

Generic across the whole ontology: person, faction, settlement, region are all descriptor-bearing objects, so "want a region" uses the identical formula as "want a dagger."

## 3. Stats fold *into* the projection **[UNWRITTEN → now shaped]**

Stat-scaling is **not** a preference substitute — it's the second, orthogonal input, and it lives *inside* the counterfactual, not beside the weight:

- **Preference** = taste ("do I care about defense").
- **Stat** = competence ("does this item do more in my hands"). High CON → "have plate" projects a larger defense descriptor *for this body* → larger delta → larger want.

Keep them separate: a high-CON character who *prefers* mobility (competent at armor, doesn't want it) only exists if both inputs stay orthogonal. This is the honest version of the `have it vs ¬have it` projector flagged as the one unwritten function — stats are *how you compute an item's worth in a specific body*.

## 4. Credibility — no global dial **[INTENT]**

Read from R's existing relation to object and speaker, so it varies per receiver for free (retired the flat 1.0 trust weight):

```
cred = f( rel_R(object), rel_R(speaker) )
accepted → Δbelief_R(object, axis) += cred · valence
rejected → Δaffect_R(speaker)      += (1−cred) · −|w_R(axis)·valence|   // reflects onto speaker
```

High Marley-aptitude → smear of Marley reflects onto the *speaker* instead of landing.

## 5. Interpretation = weighted selection **[INTENT]**

Not NLU. Intent is canonical; text is a *render* of it (intent → text via templates, never text → intent). "Reading a sentence" is a stance-selection over `{accept, doubt-subject, doubt-speaker}`, gated by belief-mismatch `m` and character-stable traits:

```
m = | valence_claimed − belief_R(o,axis) |
P(doubt_subject)      = σ( k·( m·skepticism_R − θ ) )
weight(doubt_speaker) ∝ m·(1 − trust_R(speaker))
```

The logistic **is** the "randomized yet predictable threshold": traits/θ are in-character (predictable), the draw is stochastic (randomized), sigmoid is soft not cliff. The delusion door is always cracked — high θ + low update rate, never immovable; enough contrary claims sum past the knee and the stance flips.

## 6. One selector — emission = reception **[INTENT]**

No dialog state machine. Incoming bag is deposited as a **situation variable** that reweights R's *next emission*. Turn-taking, topic-drift, termination are all weights rising/falling in the one selector; conversation ends by attrition when `response_pull` decays below other candidates.

```
score(u) = goal_align + engage_relevance + background_affinity
         + salience(objects) + response_pull(deposited claim) + noise
select via softmax(score / T)
```

## 7. Intent-type envelope + the discipline **[INTENT]**

Type is the **verb** (routing); claim-bag is the **payload**. Type decides *which handler digests*; the operator still runs inside handlers that care about feeling. Spine set: **subject-matter, rumor, question, quest, threat**, split under two top-level paths — **Dialog** (surfaces objects, boasts, questions, spreads rumor; desire moves) and **Exchange** (what desire-crossing-threshold selects into; barter, covet/coerce, talk-out).

**Test for a new type:** does it *route differently*, or just *feel differently*? Boast = subject-matter + `(speaker, arrogant, high)` leaf (same handler). Insult = subject-matter, hostile valence. Threat = its own type (conditions future behavior, not present feeling). This is the dissolve test aimed at the type list — keeps it a small immersive spine, not one-per-speech-act.

## 8. The unified exchange loop **[INTENT]**

Barter / coerce / threat / quest are **one loop under different weights** — an offer-effect whose leaves point at a **want**, **fear**, or **obligation** axis:

- **Barter** — offer targets a want axis.
- **Coerce** — same offer, one leaf targets a fear axis `(you→me, harm, high, conditional)`.
- **Threat** — coerce with the transfer stripped; conditional-harm claim, no sweetener.
- **Quest-hook** — offer where *obligation* is payload, item is reward-on-completion (deferred barter).

Reception is one roll:
```
pull = w_R(target_axis)·valence·relation_R(speaker)
cost = w_R(asked_axis)·asked_value
P(accept) = σ( (pull − cost) / T )
```

"Smart characters" are free: the counterparty runs the *same emission selector*, so `{comply, counter-threat, call allies (broadcast!), flee, lie}` compete by their own relations/traits. Cunning scales with actual relations, not a difficulty slider. Bandit king coerces from high relation-leverage; a nobody's identical threat gets laughed off.

## 9. Items as focal objects **[INTENT]**

An item is already an `object` — subject of claims, payload of offers. No barter *subsystem* (that's the flat-encounter trap in a merchant's hat). **Salience** (read from the item's own provenance descriptors) gates what surfaces conversationally: milestone blade → high-weight talk/offer/coerce candidates; iron ore → routes to a shop. **Inventory = standing candidate pool**: each carried item emits self-claims (`this blade, off the bandit king → (self, glory, high, past)`) as candidates, gated by background (merchant weights commerce axes, warrior weights trophies). The item isn't traded — it's *argued over*, and which argument you get depends on who wants it and how they're built. Surfacing and transacting are **different beats**: dialog surfaces → desire accrues → selector flips path.

## 10. Engagement + tavern **[INTENT]**

Engagement = attention binding (actor → focus target), **recomputed per beat** from proximity + recent-address + open intent (standing recompute, not stored accrual — same rule as place→mood). Mutual + reciprocated = **conversation**. Independent per-actor bindings (asymmetry emerges free; group = pairwise until a 3rd speaker forces the question — **[DEFER]**). Tavern = **broadcast the claim to all proximate**, each witness digests through their *own* operator; sides emerge because deltas point different ways and bias each witness's next beat.

## 11. Obsession — the parked primitive, finally spent **[NEW]**

Stateless recompute is **the rule**. Obsession is the sanctioned exception: a **trait-gated, low-probability** mood spike that *pins* `want_R(o)` high regardless of recompute, then decays. This is the long-deferred **amplifier-with-decay: buff-as-IEvent on the tick wheel** — it earns existence here and nowhere else.

```
each beat:  want_R(o) = recompute(...)                       // rule
on surface: P(obsess) = σ( k·( impulsive_R·salience(o) − θ ) )  // rare gate
if obsess:  spawn WantSpike(o) as IEvent → pins want, decays on tick wheel
```

**Discipline:** obsession must never be the path every want flows through, or it's accrual-for-everyone by the back door. Gives the *second* flavor of greed: the calm high-want paladin *values* the armor; the impulsive one got *possessed* by it.

## 12. Desires as persistent referents **[INTENT]**

"Remember what I want" = store the **referent** `(object_ref, first_surfaced, context)` — a bookmark saying *this is live for me*. **Recompute the magnitude** each beat. A rogue who finds a better dagger stops wanting the old one with no cleanup — recompute handles it; a stored accrual would leave a stale craving. (Store the pointer, never the score. Same rule, third time.)

---

## Parked-primitive ledger

- Amplifier-with-decay (buff-as-IEvent) — **spent** on obsession (§11).
- Preference → affinity read — **absorbed** into believed-descriptors + the operator (§4).
- Old Dialog/Interiority spec reconciliation — **this document**.

## The one genuinely unwritten function **[UNWRITTEN]**

**Magnitude / accrual + counterfactual projection.** Given a digested, credibility-passed claim or accepted offer: *how far* does relation/belief/obligation move, how fast does desire trickle by default and spike on surfacing, and how is `sat(axis | have o)` projected (with stats, §3). It reads **only** from relation + preference + stat state that already exists — bounded, not a new input space. Everything's pacing and payoff hangs off this curve. This is the next spec.

---

## Left for discussion (next session)

1. **Open-encounter interjection.** Your party overhears you about to buy something and chimes in from their own preferences. Note: this looks like it's *already* §10's broadcast + per-witness digestion — a bystander's digestion of your offer-intent produces a high-weight emission candidate ("don't, we can't afford it" / "buy it, we'll need it"). Question is whether that needs anything beyond raising bystander salience for offers made *by allies*, or whether party-membership deserves its own weighting channel into the selector.
2. **Clean mental + mathematical models.** Consolidate §§1–2, 4–5, 8, and the §-throughout math into one rigorous, presentable reference — the operator, the three runs, the two softmax gates (interpret, accept), the counterfactual — stated once, cleanly, with consistent notation. Partly the job of the unwritten magnitude function above; partly just a consolidation pass.
