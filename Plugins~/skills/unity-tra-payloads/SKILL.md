---
name: unity-tra-payloads
description: "How a designer BUILDS a TRA payload — the spawned \"effect object\" (Trigger · Reaction · Action) that lands damage/heal/buff on whatever it touched. Covers the four key authoring components (TargetsAuthoring, ReactionAuthoring, an Action* component, ObjectDefinitionAuthoring) plus LifeCycleAuthoring, the three canonical chains that all feed a payload (trigger-spawn, input-event, counter/ expiry), and a full worked example (Cube Weapon → Cube Damage TRA, −30 health). Use when a designer says \"when X touches Y, do Z to Y\" and you need the payload prefab itself, not the whole augment wiring (see unity-augment-architecture) or one isolated Timeline clip. Portable to any project with the BovineLabs Core/ Reaction/Essence/ObjectManagement packages; worked example from vex-ee."
---

# unity-tra-payloads — building the effect object

A **TRA payload** is the small prefab that actually *does the thing* — subtract
health, add a buff, knock back, heal — to a resolved target. **T·R·A** =
**Trigger** (the impetus that spawns it), **Reaction** (the conditions it checks
before acting), **Action** (the effect it applies). A designer reasons about it
as "the damage object" or "the heal pulse"; it is one prefab you author once and
spawn many times.

This skill is the **payload prefab** itself. The *spawning* of it (input → event,
trigger zones, counters, links, ObjectDefinition registration) is the surrounding
**augment** — see `unity-augment-architecture` for the five-layer composition and
the spawn chains; do not re-derive that here. Per-clip Timeline work (force,
timescale, transform…) belongs to the matching `unity-track-*` specialist. Behave
per `unity-agent-protocol`; operate the editor per `unity-cli`. **Rediscover every
name/path/id below in THIS project — the vex-ee names are worked examples, not
constants.**

## The four key components (+ one) — what each one is FOR

Every TRA prefab carries these. Verify the type's real namespace/fields by reading
the package, not by trusting this table.

| Component | Designer meaning | Verified home & key fields |
|---|---|---|
| **TargetsAuthoring** | *Who* the effect relates to | `BovineLabs.Reaction.Authoring.Core`. GameObject slots `Owner / Source / Target / Custom` + `Initialize.Target` (a `Target` enum, **default `Target`**) = what the Target slot is set to when this prefab spawns. |
| **ReactionAuthoring** | *Whether* it's allowed to act | `BovineLabs.Reaction.Authoring.Core`. Holds `Active` (duration/cooldown/trigger) + `Conditions` (the gate). **`[RequireComponent]` pulls in LifeCycleAuthoring + TargetsAuthoring automatically.** |
| **one+ Action\*Authoring** | *What* it does | The effect. `ActionIntrinsicAuthoring` / `ActionStatAuthoring` (`BovineLabs.Essence.Authoring.Actions`) change numbers; `ActionCreateAuthoring` / `ActionTimelineAuthoring` (`BovineLabs.Reaction.Authoring(.Timeline)`) spawn / play. Each is `[RequireComponent(typeof(ReactionAuthoring))]`. |
| **ObjectDefinitionAuthoring** | *Identity* — what this prefab IS | `BovineLabs.Core.Authoring.ObjectManagement`. Points at the `ObjectDefinition` asset; the asset points back at this prefab (two-way link — see below). |
| **LifeCycleAuthoring** (the +1) | *Cleanup* — it can be despawned | `BovineLabs.Core.Authoring.LifeCycle`. On a **prefab** it bakes `InitializeEntity` (vs `InitializeSubSceneEntity` for scene objects) + a disabled `DestroyEntity`. ReactionAuthoring already requires it. |

**The two-way ObjectDefinition link is the #1 fragility.** `ObjectDefinition.asset`
→ prefab, and the prefab's `ObjectDefinitionAuthoring` → the same asset. The asset
is also registered in `ObjectManagementSettings`. Break either side, or duplicate
an id, and the spawn silently produces null / the wrong prefab. (Full registration
mechanics: `unity-augment-architecture`; deep ObjectDefinition detail belongs to a
`unity-object-definitions` sibling.)

## The Reaction gate (what makes it act, or silently not)

`Conditions` is an AND'd list (max 8). Each `ConditionData`:
- **Condition** — a `ConditionSchemaObject` (an event or a stat/intrinsic schema).
- **Target** — which `Target` slot to read the condition off (default `Target`).
- **Operation** — an `Equality` (`Equal / GreaterThan / GreaterThanEqual / Between
  / Any / …`); verify the enum members in `BovineLabs.Reaction.Data.Core`.
- **Value** (+ `ValueMin/ValueMax` for `Between`).
- **Features** — a `ConditionFeature`: **`Value`** records the number for a numeric
  comparison (`CurrentHealth > 0`); **`Condition`** (the default) treats it as a
  binary event-present check (`OnInputUnleashed`); `Accumulate` sums event values.
  *Mixing these up is a classic silent no-op.*

`Active`: `Duration` (how long it stays active; **0 = one-shot**), `Cooldown`,
`Trigger` (requires external manual fire), `Cancellable`. A pure damage payload
usually leaves `Duration = 0`. (Stat/intrinsic action arithmetic — ×100 fixed
point, Added/Increased/More — is owned by the `unity-stats-intrinsics` /
`unity-essence-actions` siblings; don't re-derive it here.)

## Owner / Source / Target / Custom (flip one and you hit the wrong entity)

The `Target` enum (verified): `None=0, Target=1, Owner=2, Source=3, Self=4,
Custom=6` (note **5 is unused**). A payload's action almost always targets
**`Target`** = the entity it landed on. Set an action's Target to `Owner` and
"damage the enemy" becomes "damage myself" — the single most common designer slip.
`Initialize.Target` on `TargetsAuthoring` decides how the Target slot is *filled at
spawn*. (Relationship semantics in depth: `unity-targets` sibling +
`unity-augment-architecture`.)

## The three canonical chains — all converge on a payload

A payload is spawned by one of these. You build the payload the same way regardless;
the chain only decides what fills its Target and where it appears. (The chains are
fully wired in `unity-augment-architecture`; named here so a designer can place
their request.)

1. **Trigger-spawn (T):** a physics body / zone set to **Raise Trigger Events** →
   a `StatefulTriggerTrack` + `PhysicsTriggerInstantiateClip` (Trigger State
   `Enter`, `targetLinkOverride = Essence Link`) spawns the payload **on the thing
   it touched**, resolving that entity's Essence as Target. → *Cube Damage, Trap,
   Healing Area, AreaLock, Stagger Damage.*
2. **Input-event (R):** an input action → `CommandSequenceClip` emits a
   `ConditionEvent` (e.g. `OnInputUnleashed=1`) routed onto the player Essence →
   a reaction's `ActionCreate` spawns the payload **from the player**. → *Orb
   Shooter.*
3. **Counter / expiry (A):** an intrinsic is mutated (a countdown clip, or `±1`
   action) → a reaction checks it (`==0`, `==N`, `>=threshold`) → spawns the
   payload / fires the next stage. → *Clone Expiry, Chain Lightning, Stagger Meter.*

## Worked example — Cube Weapon → Cube Damage TRA (−30 health)

A falling **Cube Weapon** drops onto an enemy; on contact it spawns a **Cube Damage
TRA** payload that subtracts 30 from the enemy's health. Chain (1), trigger-spawn.
(vex-ee specifics — rediscover here.)

**Spawn side (the augment, summarized):** `Cube Weapon` (PhysicsBody +
PhysicsShape with Collision Response = *Raise Trigger Events* + a collision filter
that includes the enemy + `StatefulTriggerEventAuthoring`) is bound to a
`StatefulTriggerTrack` on a looping director (`Cube Stateful CheckTimeline` under
`Player - Arvex/Simulated Attack/Cube Stateful Check`, kept alive by
`TimelineReferenceAuthoring`). Its `PhysicsTriggerInstantiateClip`: ObjectDefinition
`Cube Damage TRA`, Trigger State `Enter`, `targetLinkOverride = Essence Link`. On
the first contact frame it resolves the enemy's Essence and spawns the payload.

**The payload prefab — `Assets/Prefabs/Cube Damage TRA.prefab` — is what you build:**

- `ObjectDefinitionAuthoring` → `Assets/Settings/ObjectDefinitions/Cube Damage
  TRA.asset` (which points back at this prefab; asset registered in
  `ObjectManagementSettings`).
- `LifeCycleAuthoring` (present → spawned entity can be despawned).
- `TargetsAuthoring` with `Initialize.Target` enabled → the spawned payload's
  Target is filled from the resolved (struck) Essence.
- `ReactionAuthoring`:
  - Condition: `CurrentHealth`, Target = `Target`, Operation = *GreaterThan*
    (serialized `3`), Value = `0`, Features = `Value`. → "only act on something
    that is still alive."
- `ActionIntrinsicAuthoring` → one entry: Intrinsic = `CurrentHealth`,
  Amount = `-30`, Target = `Target`.

**Result:** Cube Weapon hits "Enemy" → clip spawns Cube Damage TRA on the enemy's
Essence → reaction confirms `CurrentHealth > 0` → action does `CurrentHealth −30`
→ LifeCycle lets it despawn. (Enemy Essence starts `CurrentHealth = 1000`.)

**Designer dials on this one prefab:**
- `Amount: -30 → +30` flips **damage into healing** (this is literally how
  *Healing Area* differs from *Trap*).
- swap the Action: `ActionStatAuthoring` adding `StaggerMeter` → a stagger payload;
  add a second action entry → does both (e.g. Tornado: `CurrentHealth −20` **and**
  `LightKnockbackCounter +1`).
- Action `Target: Target → Owner` → it hits *you* instead of the enemy (the slip).
- Condition `Features` set to `Condition` when you meant `Value` → numeric gate
  never reads, payload silently does nothing.

## Build / verify / undo

Author the prefab and its ObjectDefinition via `unity-cli exec`, capturing `PRE|`
state for anything you touch, per `unity-agent-protocol`. The payload itself lives
in `Assets/Prefabs/`; do **not** edit a SubScene to make a payload (that's the
augment's job — `unity-augment-architecture`). Then:

1. **Read-back the prefab:** the four components present + enabled, LifeCycle
   present, Action Target/Amount/Intrinsic correct, Reaction condition
   schema/Target/Operation/Value/Features correct, `ObjectDefinitionAuthoring`
   pointing at the right asset.
2. **Read-back the link both ways:** ObjectDefinition asset → this prefab, and the
   asset is registered. A broken/duplicate id is invisible until runtime.
3. **Runtime:** in play mode the payload only proves out when actually spawned —
   the struck target's intrinsic must change by the authored amount, and payload
   entities must exist (a near-empty Default World means the SubScene didn't
   convert — a load problem, not a payload problem).

**Honest stop:** if the Target Essence has no such intrinsic/stat schema, or the
ObjectDefinition can't be registered, or the spawning chain needs a Timeline clip
you can't author safely — report it plainly and stop ("no egg"); don't improvise
the missing piece.

**Undo journal:** every created asset (prefab, `.meta`, ObjectDefinition,
registry entry) gets an inverse-delete block, and every `PRE|` value a restore
block, replayable top-to-bottom from a fresh load, with no model call — per
`unity-agent-protocol`.

## Cross-references (don't re-derive)

- `unity-augment-architecture` — the whole mechanic: how a payload gets spawned,
  the five layers, ObjectDefinition registration, EntityLink targeting, the spawn
  chains in full.
- `unity-targets` / `unity-reactions` / `unity-stats-intrinsics` /
  `unity-object-definitions` / `unity-essence-actions` — sibling deep-dives on each
  shared piece (rediscover whether they exist in THIS project's skill set).
- `unity-track-*` — the per-clip Timeline specialists you hand off to for any
  Timeline a payload's action plays (force, timescale, transform, essence-stat…).
