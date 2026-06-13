---
name: unity-combos
description: The designer's craft of building melee combos + attack timing in Arvex — frame data (startup/active/recovery as 60fps frames), combo links/cancels/gatling + buffer/cancel windows, LMB/RMB combo trees, motion-input specials (236P) via CommandSequence->ConditionEvent->reaction, on-hit juggle/hitstop/crit, i-frame windows — orchestrating the input/animation/reaction tracks. Portable, worked examples from vex-ee.
---

# unity-combos — building melee combos & attack timing

## What this skill is

This is the **combo-design layer**. A designer says *"make the light attack
cancel into the heavy", "add a launcher that juggles", "the dragon punch comes
out on 236P", "put hitstop on heavy hits"* — and this skill turns that into the
right arrangement of input, animation, and reaction tracks.

It **wires together** specialists; it does not re-explain them. Read those first
when authoring a piece:
- **`unity-track-player-inputs`** — turns an input (or MOTION like 236P) into a
  `ConditionEvent` (CommandSequenceTrack/Clip) and opens the buffer/cancel WINDOW
  (InputBufferTrack). How a combo READS inputs.
- **`unity-track-animation`** — RukhankaAnimationTrack plays the attack swing;
  the visual + frame side of every node.
- **`unity-reactions`** + **`unity-essence-actions`** + **`unity-designer-vocabulary`**
  — the WHEN/DO/translate layer (conditions, cooldowns, chance, on-hit chaining).
- **`unity-track-world-timescale`** / **`unity-track-timeline-timescale`** — hitstop / slow-mo.
- **`unity-mechanic-diagrams`** — VISUALISE a node as a Gantt or a combo tree as
  a stateDiagram. Use it to draw; don't reproduce its syntax here.

Behave per **`unity-agent-protocol`**; drive the editor per **`unity-cli`**;
author timelines per **`unity-timeline-track-authoring`**.

**Portability:** every name, path, and number below is a WORKED EXAMPLE from
vex-ee. Frame counts are *illustrative design values*, not verified constants —
rediscover the real assets, action ids, event schemas, and tuning in THIS
project (per the discovery recipes in the skills above). Discovery over
assumption.

---

## 1. Frame data is the unit of combat design

A single attack is a **Timeline**, and the designer tunes it in **frames at
60fps** (integer frames; `1s == 1 frame` in the Gantt convention from
`unity-mechanic-diagrams`). Three phases:

- **Startup** — wind-up before the hitbox exists. Low startup = fast, "mashable";
  high startup = committal, telegraphed. *(e.g. ~12f for a jab, ~20f for a heavy.)*
- **Active** — the hitbox is live and can connect. This is the StatefulTrigger /
  hitbox window. *(e.g. 4–8f.)*
- **Recovery** — follow-through where you're committed and can't act yet, EXCEPT
  for the cancel window (below). Long recovery = punishable, "heavy"; short =
  safe, "light".

How a single node is actually built (one PlayableDirector / `.playable`):

| Phase | What plays it | Specialist |
|---|---|---|
| The swing animation across all 3 phases | RukhankaAnimationTrack clip | `unity-track-animation` |
| The **hitbox** during the Active window | a `StatefulTriggerTrack` clip that spawns a damage **TRA payload** on trigger `Enter` (e.g. `CurrentHealth -30`) | `unity-tra-payloads` |
| The **buffer / cancel window** during Recovery | `InputBufferTrack` window clip + a `CommandSequenceClip` listening for the next-attack input | `unity-track-player-inputs` |

**Feel-tuning (designer-facing):** shorter **startup** = faster/spammier; longer
**active** = easier to land (but can double-hit); shorter **recovery** or wider
**cancel window** = smoother combos, longer = heavier/committal.

Draw it as a Gantt (`unity-mechanic-diagrams`): one Animation section
(Startup/Active/Recovery), one Input section (Movement Locked / Next-Combo Buffer
/ Dash-Cancel Window) — the Input section is *where the next node connects*.

---

## 2. Combo links, cancels & gatling

A combo is a chain of nodes where the **next attack is buffered during the
current node's recovery and cancels into it.** The machinery is entirely
`unity-track-player-inputs`:

- **Buffer window** = an `InputBufferWindowClip` open during recovery. With no
  active window, *nothing is recorded* (the #1 silent dead-end). The window lets
  a designer "press early and have it come out on time."
- **Cancel** = the next node's `CommandSequenceClip` recognises the buffered
  input within that window and fires the event that starts the next attack's
  timeline (reaction → `ActionTimelineAuthoring`, per `unity-essence-actions`).
- **Gatling / chain** = each node opens a window for the *specific* next inputs
  it may cancel into; limit `AllowedActions` to control which moves are legal.

**Designer asks → build:**

| "Make light cancel into heavy" | open a buffer window in Light's recovery; Heavy's CommandSequence step reads Heavy-attack input during it → fires Heavy's reaction. |
| "Tighter combo (harder)" | shorten the buffer/cancel window. |
| "Looser combo (easier)" | lengthen the window, or open it earlier (earlier in recovery). |
| "Can't cancel light into light (no infinite)" | don't list the light-attack action in that window's `AllowedActions`, or set the node non-repeatable. |

**Dash-cancel window:** same idea — a window during recovery that allows the
dash action; a successful dash press cancels the recovery (and usually grants
i-frames, §5). This is what makes a combat system feel "fluid."

**Cancel order matters:** use the **Ordered** CommandMode family + `MaxGapTicks`
when a node requires a true sequence (see player-inputs §2). Plain Contains
matches anywhere in history and breaks true timing.

---

## 3. Combo trees — LMB vs RMB branching

Beyond a single chain, a node can branch: from the same recovery window, a
**different input picks a different next node** — the classic LMB-string vs
RMB-string tree, plus mid-string forks (light light → *heavy* finisher vs
*launcher*). Build it as **multiple sequences on one CommandSequenceClip** (or
multiple clips sharing the window), each step matching a different action and
emitting a different event, each event firing a different node's timeline.

This is exactly the AGENTS.md combo-tree mental model: a `stateDiagram-v2` where
each state is a node, each edge is an input pressed inside a **decision window**,
and timeout = return to neutral. The worked weapon-throw example: *Weapon Thrown
→ Peak →* (press LT within the 1.5s **decision window**) *Teleport* / (no input)
*Return*. Author the branch as the `Peak` node opening a window; one
CommandSequence step reads LT and fires the teleport (see §5 for the i-frames it
grants). Draw the tree with `unity-mechanic-diagrams`.

---

## 4. Special-move motion inputs (236P)

A "special" comes out on a **motion input** — e.g. `236P` (quarter-circle
forward + punch: Down → Down-Forward → Forward, then Punch). This is **Pattern B
of `unity-track-player-inputs`**, do not re-derive it:

1. The consumer must quantise the movement stick into eight-way directions
   (`InputConsumerAuthoring.TrackDirection` + `DirectionAction`).
2. One `CommandSequenceClip` over an open buffer window, steps all
   `OrderedConsume` / `Phase.Down`, each direction within a few `MaxGapTicks`
   (e.g. ~8–12f) so the motion must be continuous, ending with the attack
   action: Down → DownForward → Forward → Punch.
3. On full match it emits a `ConditionEvent` (e.g. `OnFireball`) routed via
   `Essence Link` to the player Essence.
4. A **reaction** on that event (`unity-reactions`) fires the special's timeline
   via `ActionTimelineAuthoring` — the special is just another attack node.

**Messatsu Goshoryu no Yume** (the named dragon-punch — "a Dragon Punch imbued
with the purest killing intent") is the flavour example for this pattern. **The
wiki specifies it as flavour only — no frame numbers, no concrete input motion
are documented.** Treat it as: a dragon-punch is a motion-input special (a DP is
classically `623` / `Z-motion`), authored exactly as above. Do **not** present
any specific startup/active/recovery or motion as if verified — rediscover the
real input asset and tune the frames as a design choice.

---

## 5. On-hit reactions — the FEEL of a combo

On-hit effects are *combo feel built as reactions + events*, not special combo
machinery. Each is a TRA payload spawned on the hitbox's trigger Enter, carrying
a reaction. Cite `unity-essence-actions` / `unity-reactions` for the wiring;
here is the combo-design intent → which pattern (all four are vex-ee worked
examples):

- **Juggle / launcher** (*"this attack pops them into the air"*): on hit, a
  stagger TRA adds `+1 Stagger Points` (a stat) to the enemy for ~5s; when the
  enemy's `Stagger Points >= 5`, the enemy's juggle reaction plays a
  `PhysicsForceClip` impulse (e.g. `(0, 3.5, 5)` — up AND forward; the forward
  `z` is intentional) that launches it. Gravity brings it down. *(009/012:
  "On-Hit Juggle".)* The juggle window — how long they stay airborne and
  re-hittable — is what makes air-combos possible. The launch force is a
  `unity-track-physics-force` clip.
- **Hitstop / slow-animation on hit** (*"heavy hits feel meaty"*): the wiki's
  worked path slows the *enemy's* animation, not the world — a TRA whose
  `TimelineEssenceStatClip` modifies a `SlowMo` *stat* (e.g. `-70` for 3s,
  routed to the Target via `Essence Link`); the enemy's Animation timeline has a
  `TimelineTimeScaleClip` bound to `SlowMo` (base 100 = 1.0×), so 30 → 0.3×
  speed. *(003: "OnHit Slow Animation".)* That's `unity-track-timeline-timescale`
  + `unity-track-essence-stat`. For a true global **freeze-frame / bullet-time**
  hitstop, use `unity-track-world-timescale` instead (and mind its timeScale-0
  GameTime deadlock). Designer slip to flag: *"slow the enemy"* is a **SlowMo
  stat → timeline timescale**, NOT world slow-mo.
- **Crit = extra damage** (*"50% to crit"*): modeled as a SECOND damage instance
  gated by chance — a separate crit TRA with `ReactionAuthoring`
  `chanceToTrigger = 0.5` applying another `CurrentHealth -30`; pair it with
  `ActionDestroyOnChanceFailAuthoring` so the fizzled roll cleans itself up.
  *(013: "Crit Chance +50%".)* Crit is NOT a damage multiplier in the worked
  setup — it's a chance-rolled extra hit (`unity-reactions` chance gating +
  `unity-essence-actions` cleanup).
- **Cooldown / charge gating** of a node: a node's reaction can carry
  `Active.cooldown` (e.g. the dash's own 2.0s built-in cooldown — use the
  reaction field, NOT a separate counter intrinsic). *(011: "On-Dash Damage +
  Double Cooldown".)*

Each of these is a TRA payload spawned by the hitbox clip — see
`unity-tra-payloads` for the payload prefab itself and
`unity-augment-architecture` for composing the whole chain.

---

## 6. i-frames / invincibility windows

A dash, dodge, or teleport usually grants **invincibility frames** — a window
during the move where the player can't be hit. In the weapon-throw example,
*Teleport* explicitly "provides invincibility frames." This is a defensive
window layered onto an offensive/movement node, authored as a **while-active
state modifier**: during the dash/teleport clip, a `TimelineEssenceStatClip`
(see `unity-track-essence-stat`) flips an invuln stat on the player (e.g. an
"Invulnerable"/damage-immunity stat), removed automatically when the clip ends.
Designer-facing tuning: align the i-frame window with the dash's active span
(too long → unpunishable dash; too short → trades). Verify the exact stat/flag
this project uses for damage immunity — rediscover it; don't assume a name.

---

## 7. Decomposing a combo request (the workflow)

1. **Translate** with `unity-designer-vocabulary` (catch the slips: "crit" =
   chance-rolled extra hit; "slow the enemy" = SlowMo stat; "explode after
   dashing" = listen to `OnDashInitiated`, not the raw key).
2. **Diagram** it (`unity-mechanic-diagrams`) — Gantt for one node's frame data,
   stateDiagram for the tree/branches.
3. **Audit the stage & consumer** — an InputConsumer and the player Essence must
   exist (`unity-stage-foundations`); zero consumers → a missing prerequisite,
   report it (protocol §6), don't improvise.
4. **Author per node:** animation clip + hitbox/TRA + buffer window &
   CommandSequence (`unity-track-animation` / `unity-tra-payloads` /
   `unity-track-player-inputs`).
5. **Wire links/branches:** reactions on each emitted event fire the next node's
   timeline (`unity-reactions` + `unity-essence-actions`).
6. **Layer feel:** on-hit juggle / hitstop / crit / i-frames (§5–6).
7. **Verify** end-to-end by driving the input via `unity-player-input` and
   reading back that the event fired and the reaction triggered (evidence rule,
   protocol). Record undo per `unity-timeline-track-authoring`.

**The honest boundary:** this skill owns the *arrangement and timing* of a
combo. It does NOT re-derive how a CommandSequence matches, how a TRA payload is
built, or how a stat is modified — defer to the named specialists. When the wiki
is thin (Messatsu has flavour only), describe the PATTERN and tell the designer
the frame numbers are theirs to choose.
