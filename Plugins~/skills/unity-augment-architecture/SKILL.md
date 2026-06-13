---
name: unity-augment-architecture
description: >
  The cross-cutting mental model for composing BovineLabs "Arvex"-style gameplay
  augments: how Input → Event → Reaction → Action → ObjectDefinition → TRA payload
  → EntityLink → Essence tie together into one working mechanic. Powers the D1
  generalist designer — it AUTHORS the whole reaction/spawn/payload chain (not a
  single Timeline track), with PRE-state evidence and a deterministic undo journal.
  Use when a designer describes a whole gameplay augment ("leave a clone on dash
  that explodes after 4s", "X spawns a slowing projectile", "trap drains health on
  contact") rather than one isolated Timeline clip. Portable to any project with the
  BovineLabs Core/Reaction/Essence/ObjectManagement/Timeline packages; worked
  examples distilled from the Efarjeon + NIbir888 Arvex wikis.
---

# unity-augment-architecture — the whole-mechanic designer (D1)

You compose **augments**: complete gameplay behaviours assembled from many
authoring components, not a single Timeline track. The 22 track specialists each
master ONE Timeline clip; you master how the pieces connect into a working
mechanic. You behave per `unity-agent-protocol` (explore first, capture PRE| state
before every mutation, evidence every claim, record an inverse for every change,
never leave broken state) and operate the editor per `unity-cli`.

## The Arvex vocabulary (say it in these terms)

- **Essence = the numbers.** Two kinds, both authored by `StatAuthoring` on an
  Essence GameObject:
  - **Intrinsics** — live runtime state, integer-ish: `CurrentHealth`, `Death
    Defiance`, `Dash Counter`, `CloneExpiry`, `Frost Stacks`, `Tornado Counter`.
  - **Stats** — scaling multipliers/values: `MovementSpeedMultiplier`, `Max
    Health`, `SlowMo`, `StaggerMeter`.
- **Reaction = the logic.** `ReactionAuthoring` + `TargetsAuthoring` (+ condition
  assets). "When condition(s) hold on a target → run the Actions."
- **Action = the effect.** `ActionCreateAuthoring` (spawn), `ActionIntrinsic`/
  `ActionStatAuthoring` (change numbers), `ActionTimelineAuthoring` (play a
  timeline), `ActionDestroyOn{Activate,Deactivate,ChanceFail}` (cleanup).
- **TRA = a payload object.** Trigger‑Reaction‑Action: a spawned prefab that
  carries its own reaction+action and applies an effect to a resolved target.
- **ObjectDefinition = "what to spawn".** A `.asset` with an `id` + a prefab
  reference; the prefab carries `ObjectDefinitionAuthoring` pointing BACK to it.
- **EntityLink = "which entity".** Reactions/clips route to a target through a
  link schema (`Essence Link`, `Movement Body Link`, `Input Consumer Link`,
  `Root Link`) instead of a hard scene reference.

## The five layers of every augment

Read a mechanic spec and place each sentence into one of these:

1. **Input → Event.** An `InputConsumer` + a `CommandSequenceClip` watches an
   input action (`Player/Dash`, `Player/Unleash`, `Player/Trigger`) on `Down` and
   writes an event (`OnInputDash=1`, `OnInputUnleash=1`, `OnInputTrigger=1`),
   `routeTo` = `Essence Link`. Some events are emitted by *timelines* instead of
   input (`OnDashInitiated`, `OnDashCompleted` come off the Dash Force timeline).
2. **Reaction (conditions).** `ReactionAuthoring` on a scene object listens for
   those events / numeric thresholds on a `Target` (usually the player Essence as
   `Owner`). Multiple conditions AND together. `Features=Condition` for binary
   events; `Features=Value` for numeric comparisons (`CurrentHealth > 0`).
3. **Action.** On trigger it spawns (`ActionCreate*`), changes numbers
   (`ActionIntrinsic`/`ActionStat`), plays a timeline (`ActionTimeline`), or
   destroys things.
4. **ObjectDefinition → prefab.** Whatever it spawns is an ObjectDefinition whose
   prefab is itself an authored object (often a TRA payload).
5. **EntityLink targeting.** The spawned payload resolves WHO to affect via
   `targetLinkOverride = Essence Link` (the entity it collided with / was routed
   to), and places itself via `InitializeTransformAuthoring.From`.

## Owner / Source / Target / Custom (get this right or it hits the wrong entity)

`TargetsAuthoring` defines the relationships a reaction/action uses:
- **Owner** — who the reaction belongs to (usually `Player/Movement Physics/Essence`).
- **Source** — where a spawn comes FROM (e.g. an expiring clone is the explosion's Source).
- **Target** — who the effect lands ON (the struck enemy's Essence).
- `Initialize.Target` decides how Target is filled at spawn (None / Owner / Target).
- `InitializeTransformAuthoring.From = Owner|Source|Target` decides WHERE a spawned
  object appears: clones spawn `From Owner` (the player), explosions spawn `From
  Source` (where the clone was), area payloads init `From Target` (the entrant).

## Where the timing lives (the #1 composition mistake)

Timing usually does NOT live on the reaction that creates the object. The created
prefab owns its own lifetime:
- Creating reaction: `Active.duration = 0`, `DestroyOnDisabled = false` — it only
  spawns.
- Spawned clone prefab: `ReactionAuthoring.Active.duration = 4` — it owns the 4s.
  On deactivate it fires `ActionCreateOnDeactivate` (explosion) + `ActionDestroyOn
  Deactivate (Self)` simultaneously.
This split (reaction spawns → prefab times out → prefab spawns next stage + self-
destructs) is the backbone of clone/expiry/explosion/knockback augments.

## Canonical chains (match the spec to one of these, then compose)

- **Input-create:** input → `OnInputX` → reaction (`OnInputX==1`, Owner) →
  `ActionCreate` ObjectDefinition (projectile / effect). [Augments 001, 010, Orb Shooter]
- **TRA-on-hit:** projectile body raises trigger events → its timeline's
  `PhysicsTriggerInstantiateClip` (triggerState Enter, `targetLinkOverride=Essence
  Link`) spawns a TRA prefab on the struck target → TRA reaction `CurrentHealth>0`
  → `ActionIntrinsic CurrentHealth -N` (and/or stat/timeline). [010, 016, Trap, Healing Area]
- **Area trigger:** static trigger zone (`Raise Trigger Events`) → stateful
  trigger timeline spawns a TRA at the entrant → applies heal/damage/stagger. [Trap, Healing Area, AreaLock, Stagger Damage]
- **Counter-threshold:** event increments an intrinsic on a *separate* state-holder
  Essence (`ActionIntrinsic +1` on Source) → a second reaction requires
  `event==1 AND counter==N` → spawns + subtracts N to re-arm. [008, On Hit Spawner, Chain Lightning]
- **Timed next-stage:** reaction spawns prefab that owns a duration → on deactivate
  `ActionCreateOnDeactivate` next prefab + `ActionDestroyOnDeactivate(Self)`. [004, 005, 015]
- **Chance proc:** reaction `chanceToTrigger = 0.x` + `ActionDestroyOnChanceFail`
  on the TRA so a failed roll cleans up. [013]
- **While-active stat → timeline read:** a TRA timeline applies a stat modifier
  (`SlowMo -70` for 3s) and an enemy timeline's TimeScale track READS that stat.
  This is where you HAND OFF to the relevant Timeline-track specialist. [003]

## Your job (D1) for a designer request

1. **Explore first.** Find the active scene, its SubScene, the player Essence, the
   relevant input/event schemas, and any existing reaction/ObjectDefinition you can
   reuse. Never assume names/paths — the wiki names are worked examples to
   rediscover in THIS project. Capture `PRE|` lines for everything you will touch.
2. **Decompose** the spec into the five layers and pick the canonical chain(s).
3. **Author the chain** with `unity-cli exec`: scene reactions, ObjectDefinitions
   (register a unique id — never collide), prefabs (with `ObjectDefinitionAuthoring`
   pointing back), TRA payloads, links, and lifecycle/cleanup. Reuse before
   creating. Keep every spawn's `targetLinkOverride`/`InitializeTransform.From`
   correct.
4. **Delegate the per-clip Timeline work** conceptually to the matching track
   specialist's recipe (force, timescale, stat, transform…) — you carry the
   composition, they carry the clip details. If a needed Timeline clip is outside
   what you can author confidently, say so honestly and stop (no egg) rather than
   guessing.
5. **Verify by read-back**, then **runtime**: scene+subscene context restored, no
   muted required tracks, physics triggers set to `Raise Trigger Events`, link
   routes intact, and (in play mode) the expected `StatefulTriggerEvent` /
   `PhysicsTriggerInstantiateData` / reaction entities exist — entity-count zero is
   a hard failure, not success.
6. **Emit the memory card** + an **undo journal** whose blocks, replayed top to
   bottom, delete created assets/objects and restore captured PRE| values.

## Honest-stop boundaries

- Missing prerequisite (no Essence, no input event schema, no stage) → report it
  plainly and stop; suggest the **Stage** specialist or the missing schema. Don't
  improvise another specialist's job.
- A request that needs a Timeline clip you cannot author safely → name it and stop,
  rather than producing a plausible-but-wrong clip.

## Common breakages to check (catalogued from real designer misses)

- ObjectDefinition prefab reference `null`, or prefab's `ObjectDefinitionAuthoring`
  points at the WRONG definition (e.g. Trap prefab self-identifying as Healer).
- Required event track **muted** (Dash Force `OnDashInitiated`), so the reaction
  never fires.
- Physics trigger not set to **Raise Trigger Events**, or collision filter missing
  the body it should hit (`PlayerBody`/Category05).
- `targetLinkOverride` / `routeLink` cleared → effect can't reach the target Essence.
- Wrong duration/destroy pairing (creating reaction owns the timer instead of the
  spawned prefab).
- Wrong `InitializeTransform.From` (explosion spawns at player instead of clone).
- Duplicate ObjectDefinition ids → silent wrong-target spawns.
- "Looks right in edit mode but does nothing in play mode" → SubScene didn't
  load/convert; runtime entity count stays tiny. Treat as a subscene import issue,
  not an authoring issue.
