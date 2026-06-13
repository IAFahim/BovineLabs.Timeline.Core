---
name: unity-essence-actions
description: "The Action* family a designer composes as the \"do something\" half of a BovineLabs reaction — the always-on actions (ActionCreate spawn, ActionStat buff, ActionIntrinsic counter, ActionTimeline play) plus the lifecycle-keyed addon actions (ActionCreate/Destroy OnActivate · OnDeactivate · OnChanceFail) that turn a reaction into a self-cleaning spawn-and-chain machine. Covers the clone→expiry→explode→knockback pattern, the spawn-on-deactivate / destroy-Self / destroy-Source cleanup idioms, and the chance-fizzle path. Use when a designer says \"spawn X, then when it ends spawn Y and remove itself\", \"buff a stat while active\", \"grant +N of a counter\", or \"leave a thing that explodes/cleans-up after a timer\". Portable to any project with the BovineLabs Reaction/Essence/ObjectManagement packages + the reaction.addon; worked examples from vex-ee."
---

# unity-essence-actions — the "do something" half of a reaction

A **reaction** is "if X is true → do something" (see `unity-reactions`). The
*something* is one or more **Action\*** components sitting on the SAME GameObject
as the `ReactionAuthoring`. This skill is the designer's catalogue of those
actions and the cleanup/chaining patterns built from them. Behave per
`unity-agent-protocol`; operate the editor per `unity-cli`. For the whole
Input→Event→Reaction→Action→ObjectDefinition→payload picture see
`unity-augment-architecture`; for the chains those actions live inside see
`unity-reactions` and `unity-tra-payloads`.

> **Discovery, not assumption.** Every type, asset path, schema, and ID below is a
> WORKED EXAMPLE from vex-ee. In THIS project, rediscover the real names via
> `unity-cli` (component lists on the prefab, the ObjectDefinition registry, the
> schema folders) before you wire anything. Names drift; the shapes don't.

## How any action behaves (the rule a designer internalizes)

Actions only fire **while the reaction is active**, and most undo themselves when
it deactivates. A reaction's "active" window is set in its **Active** block:
`duration` (how long it stays active; `0` = a single tick) and `cooldown`. So the
lifetime of an action is the lifetime of the reaction that owns it.

Two timing flavours exist, and mixing them up is the #1 slip:

- **Always-on actions** (in the core Reaction + Essence packages) apply *the whole
  time the reaction is active* and reverse on deactivate. Good for buffs and
  while-channeling effects.
- **Lifecycle-keyed actions** (in `com.bovinelabs.reaction.addon`) fire **once**
  at a specific *edge*: the moment the reaction turns on (OnActivate), the moment
  it turns off (OnDeactivate), or when the chance roll fails (OnChanceFail). Good
  for "spawn now", "spawn when it ends", "clean myself up".

Every action takes a **Target** (`unity-targets`): which entity it acts on —
`Self` / `Owner` / `Source` / `Target` / `Custom` / `None`. Flipping it is how
"heal the enemy" silently becomes "heal myself"; verify it every time.

## The always-on action catalogue

| Action authoring                | Package           | What the designer gets                                                                 |
|---------------------------------|-------------------|----------------------------------------------------------------------------------------|
| `ActionCreateAuthoring`         | reaction (core)   | Spawn an **ObjectDefinition** when active. `DestroyOnDisabled` bool: tie the spawn's life to this reaction (true) or hand off ownership (false). |
| `ActionStatAuthoring`           | essence           | Modify a **Stat** while active, reversed on deactivate (Added/Increased/More/Subtracted; Fixed/Linear/Range). Details in `unity-stats-intrinsics`. |
| `ActionIntrinsicAuthoring`      | essence           | One-shot change a runtime **counter** by `Amount` (e.g. CurrentHealth +10). Details in `unity-stats-intrinsics`. |
| `ActionTimelineAuthoring`       | reaction.timeline | Play a DOTS **Timeline** (PlayableDirector) while active; per-track Bindings re-target by `Target`. See the Action Timeline notes below. |
| `ActionEnableableAuthoring` / `ActionTagAuthoring` | reaction (core) | Toggle an enableable component / add a zero-size tag while active (reference-counted). Niche; rarely the designer's first reach. |

`ActionCreateAuthoring` is a **list** (`Create[]`), each row = `{ Definition,
Target, DestroyOnDisabled }`. It NEVER takes a raw prefab — only an
`ObjectDefinition` registered in the ObjectManagement settings; the prefab carries
`ObjectDefinitionAuthoring` pointing back at the same asset (the two-way link, see
`unity-object-definitions`). A broken/duplicate link → silent wrong/null spawn.

### `DestroyOnDisabled` — the ownership decision (read this twice)

On `ActionCreateAuthoring`, `DestroyOnDisabled` answers *"who owns the spawned
thing's life?"*

- **`true`** → the spawn dies when THIS reaction deactivates. Use for transient
  VFX/hitboxes that should vanish the instant the trigger ends.
- **`false`** → the spawn lives on its own; *it* owns its lifetime (usually via its
  own `ReactionAuthoring.Active.duration` + its own deactivate-cleanup actions).

> There is **no** `ActionDestroyOnDisabledAuthoring` component. "OnDisabled"
> cleanup is this `DestroyOnDisabled` bool on `ActionCreate`, not a separate
> addon action. The addon's standalone destroy actions are keyed to
> Activate/Deactivate/ChanceFail only (next section).

The classic trap (from the clone augments): the dash reaction that spawns a clone
runs with `Active.duration = 0` and **`DestroyOnDisabled = false`** — because the
reaction's only job is to spawn; the *clone* owns the 4-second timer. Set
`DestroyOnDisabled = true` there and the clone is yanked the same tick it spawned.

## The lifecycle-keyed addon catalogue (`com.bovinelabs.reaction.addon`)

Six buffer-backed components, all requiring `ReactionAuthoring` on the same object.
This is the EXACT set — there is no OnEnabled/OnDisabled variant.

| Authoring component                    | Fires at the moment…                          | Field shape                          |
|----------------------------------------|-----------------------------------------------|--------------------------------------|
| `ActionCreateOnActivateAuthoring`      | the reaction turns ON                          | `Spawns[] = { Definition, Target }`  |
| `ActionCreateOnDeactivateAuthoring`    | the reaction turns OFF                         | `Spawns[] = { Definition, Target }`  |
| `ActionCreateOnChanceFailAuthoring`    | all conditions passed but the chance roll lost | `Spawns[] = { Definition, Target }`  |
| `ActionDestroyOnActivateAuthoring`     | the reaction turns ON                          | `Targets[]` (which entities to kill) |
| `ActionDestroyOnDeactivateAuthoring`   | the reaction turns OFF                         | `Targets[]`                          |
| `ActionDestroyOnChanceFailAuthoring`   | all conditions passed but the chance roll lost | `Targets[]`                          |

Runtime truths worth knowing (verified in the addon systems):

- **Create-on-activate** stamps the new entity's `Targets` from the spawner, so
  the spawn inherits Owner/Source/Target context — that's how a spawned explosion
  can later resolve `Source` = the clone that died.
- **Destroy** doesn't delete instantly; it enables a `DestroyEntity` lifecycle
  flag on the resolved target (so it goes through the normal teardown). If the
  resolved Target is `Entity.Null`, the destroy is skipped silently.
- **ChanceFail actions only fire when *every other* condition was satisfied** — the
  ONLY reason the reaction didn't activate was the random roll. They pair with the
  reaction's `Conditions.chanceToTrigger` (0..1). Use them for the "fizzle" feel
  (puff of smoke on a missed crit), never for general failure handling.

## The cleanup-and-chaining patterns (what a designer actually composes)

### 1. Spawn-then-handoff (don't let the trigger own the spawn)
On the trigger reaction: `ActionCreateAuthoring` with `DestroyOnDisabled = false`,
`Active.duration = 0`. The spawned prefab carries its OWN reaction + timer. The
trigger fires once and forgets; the spawn manages itself. This is the backbone of
every "leave a thing behind" augment.

### 2. Self-cleanup on finish (never manual deletion)
A spawned payload that has done its job removes itself with
`ActionDestroyOnDeactivateAuthoring`, **Target = `Self`**. This is the canonical
"clean up after yourself" idiom — you never hand-delete an entity; you let the
deactivate edge enable its `DestroyEntity`. Pairs with a reaction `duration` (timed
self-destruct) or a condition that goes false.

### 3. Destroy the source on use (consume the pickup)
A TRA payload that lands on a pickup destroys the thing that spawned it with
`ActionDestroyOnActivateAuthoring`, **Target = `Source`** — e.g. the healing orb is
consumed the instant its healing action activates (augment 014). Combine with
`ActionDestroyOnDeactivateAuthoring` Target = `Self` so the payload also removes
itself afterwards.

### 4. Clone → expiry → explode (the headline composition)
This is the worked example a designer should be able to recite (Efarjeon augments
004/005; NIbir888 Clone Expiry). Two distinct timer styles ship in the wiki:

**Style A — clone owns the timer via its own reaction (augments 004/005):**
```
Trigger reaction (on Player, gated OnDashInitiated == 1, duration 0):
  ActionCreateAuthoring → spawn "Clone" ObjectDefinition, Target = Owner,
                          DestroyOnDisabled = false        (handoff!)

Clone prefab:
  ReactionAuthoring  Active.duration = 4, Conditions = empty   (the 4s timer)
  ActionCreateOnDeactivateAuthoring   → spawn "Explosion", Target = None
  ActionDestroyOnDeactivateAuthoring  → Target = Self
  InitializeTransformAuthoring  From = Owner   (clone appears where the player was)

Explosion prefab:
  InitializeTransformAuthoring  From = Source  (explosion appears where the clone died)
  (+ for knockback: PhysicsBody + StatefulTriggerEvent + a timeline whose
     PhysicsTriggerForceClip applies a Radial impulse — see unity-track-physics-force
     and the trigger-spawn chain in unity-tra-payloads)
```
The whole point: clone expiry and explosion happen on the **same deactivate edge**,
so they're simultaneous. The trigger reaction does NOT wait 4s — the clone does.

**Style B — a countdown timeline drives a counter, a reaction watches it (NIbir888):**
```
Essence starts with CloneExpiry = 1
Countdown timeline subtracts 1 from CloneExpiry after 5s (TimelineEssenceIntrinsicClip)
Reaction condition: CloneExpiry == 0  →  ActionTimelineAuthoring plays an explosion-force timeline
```
Same outcome, different driver: here the timer is an **intrinsic countdown +
condition** (canonical chain C, "Counter/Expiry") rather than the clone's own
reaction duration. Pick A when the spawned thing should own its life; pick B when a
shared/scene timeline owns the schedule.

### 5. Fizzle on a missed chance
Reaction `Conditions.chanceToTrigger = 0.5` (a 50% effect). Add
`ActionCreateOnChanceFailAuthoring` (a smoke puff) and/or
`ActionDestroyOnChanceFailAuthoring` so a *failed* roll still produces feedback or
cleans up the attempt — without firing the real payload.

## Action Timeline notes (`ActionTimelineAuthoring`)

`Timelines[]`, each = `{ Director (a PlayableDirector), InitialTime,
DisableTimelineOnDeactivate (default true), ResetWhenActive, Bindings[] }`.
Bindings = `{ Track (a DOTSTrack), Target }` — they re-point a DOTS Timeline track
at a reaction Target (Target.None bindings are dropped). `ResetWhenActive` decides
re-trigger behaviour: true restarts an already-playing timeline; false ignores new
triggers until it finishes. In the clone-expiry "explosion-force" variant the
reaction (CloneExpiry == 0) plays the force timeline this way with
`DisableTimelineOnDeactivate = false` so the impulse isn't cut short.

## Where the pieces actually live (vex-ee worked example — rediscover in THIS project)

- Core spawn/tag/enableable actions: `com.bovinelabs.reaction` (external, PackageCache),
  `BovineLabs.Reaction.Authoring/Actions/`.
- Stat/Intrinsic actions: `com.bovinelabs.essence` (external),
  `BovineLabs.Essence.Authoring/Actions/`.
- Action Timeline: `com.bovinelabs.reaction.timeline` (external).
- **Lifecycle-keyed actions: `com.bovinelabs.reaction.addon`** (a LOCAL package in
  vex-ee's `Packages/`, namespace `BovineLabs.Reaction.Addon.Authoring`). If a
  project lacks this addon, only the always-on actions + the `DestroyOnDisabled`
  bool exist — the OnDeactivate/OnChanceFail idioms are unavailable; report that
  honestly rather than improvising.
- ObjectDefinitions referenced by Create actions live in the project's
  ObjectManagement settings + `Assets/Settings/ObjectDefinitions/`.

## Designer pitfalls (every one observed in the wikis or the addon source)

- **Wrong Target.** `Self` vs `Source` vs `Owner` flips who dies / who's buffed.
  Clone destroys `Self`; healing TRA destroys `Source` (the orb) and `Self` (itself).
- **`DestroyOnDisabled = true` on a handoff spawn** → the spawn vanishes the tick it
  appears. Use `false` and let the spawn own its life.
- **Expecting always-on to fire once, or OnActivate to keep applying.** A buff via
  `ActionStat` reverses on deactivate; a spawn via `ActionCreateOnActivate` fires
  exactly once at the on-edge.
- **ChanceFail used as general failure handling.** It fires only when *all* real
  conditions passed and just the dice lost; it's a fizzle, not an else-branch.
- **Manual entity deletion.** Always express cleanup as a Destroy action on the
  deactivate edge; never delete by hand — it bypasses the lifecycle teardown.
- **Broken ObjectDefinition two-way link / duplicate IDs** → Create actions spawn
  nothing or the wrong thing, silently. Verify both directions (see
  `unity-object-definitions`).
- **`InitializeTransformAuthoring.From` mismatch on the spawn** → it appears at the
  player/origin instead of where it should (clone: `From = Owner`; explosion:
  `From = Source`). The spawn's pose is the spawn's concern, not the action's.
