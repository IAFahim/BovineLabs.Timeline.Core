---
name: unity-designer-vocabulary
description: "The Rosetta Stone for BovineLabs \"Arvex\" gameplay — translates a designer's plain request (\"knock the enemy back\", \"crit for double\", \"slow the enemy\", \"explode every 2 dashes\") into the right chain + components + numbers, and catalogues the recurring \"designer says X / actually built as Y\" gaps and traps so an agent can decompose a request correctly BEFORE composing it. Read FIRST when a request is phrased in gameplay terms rather than as a specific Timeline clip; then hand the chosen chain to unity-augment-architecture (composition) and the matching unity-track-* specialist (per-clip). Portable: every name/number/path below is a worked example from vex-ee to rediscover in THIS project; verify schemas and ids live via unity-cli."
---

# unity-designer-vocabulary — translate the request, then compose

A designer never says "spawn a TRA whose `ActionStatAuthoring` adds 400 to the
Frost Stacks stat." They say **"apply 4 frost stacks."** Your job here is the
*translation*: turn a plain gameplay sentence into the right chain, the right
components, and the right numbers — and catch the recurring traps where the plain
words and the actual build diverge. Once translated, you **compose** the chain per
`unity-augment-architecture` (it owns the five layers, the canonical chains, the
Owner/Source/Target rules, the timing-split, and the authoring job), drive the
editor per `unity-cli`, and behave per `unity-agent-protocol`. This skill does NOT
re-derive those — it is the lookup that comes *before* them.

**Portability:** every schema name, asset path, id, and numeric default below is a
**worked example from the vex-ee wikis**. Names drift between scenes (the same
mechanic appears as `Clone Capsule` / `Clone Jutsu` / `Ashen Wraith Clone`). Always
**rediscover** the real schema assets, event keys, ObjectDefinition ids, and input
bindings in THIS project via `unity-cli` before acting. The *patterns* port; the
*names* do not.

## How to read a designer request (the decomposition order)

Walk the sentence and tag each clause to a layer (full layer model in
`unity-augment-architecture`):

1. **What starts it?** A button → an *input event*. A collision → a *trigger*. A
   number reaching a value → a *counter/threshold*. Time passing → a *duration on
   the spawned prefab*. → picks the **chain**.
2. **Who does it affect?** "the enemy" → `Target`. "me / the player" → `Owner`.
   "where it spawned from" → `Source`. → sets `TargetsAuthoring` + each action's
   Target enum. **Flipping Target↔Owner turns "damage enemy" into "damage myself" —
   the #1 slip.**
3. **What changes?** A live count (health, charges, stacks) → an **Intrinsic** via
   `ActionIntrinsicAuthoring`. A scaling/percentage value (speed, max HP, slow) → a
   **Stat** via `ActionStatAuthoring`. Spawn something → `ActionCreateAuthoring` +
   an ObjectDefinition. Play a motion/VFX → `ActionTimelineAuthoring` + a track.
4. **For how long / how much?** Convert the human number — see the numeric gotchas
   below. "Forever / once" = Intrinsic +N. "For t seconds" = a while-active
   stat/timeline of that duration. "Then clean up" = `ActionDestroyOn*`.

## The Rosetta Stone (designer phrase → build)

| Designer says… | Chain | Built as (components + key fields) |
|---|---|---|
| "when I press X, shoot a projectile" | input-create | `CommandSequenceClip` (action `Player/Unleash`, phase **Down**) emits `OnInputUnleash=1` routeTo **Essence Link** → reaction `OnInputUnleash==1` on **Owner** → `ActionCreateAuthoring` spawns a projectile ObjectDefinition (Target Owner). [010, Orb Shooter] |
| "it hits enemies along the way" | TRA-on-hit | projectile child has physics body + **Raise Trigger Events** + `StatefulTriggerEventAuthoring`; its own timeline's `PhysicsTriggerInstantiateClip` (triggerState **Enter**, `targetLinkOverride` **Essence Link**, ignore Owner) spawns a TRA on the struck Essence. [010, 016] |
| "deal 30 damage" | (action on TRA) | `ActionIntrinsicAuthoring` Intrinsic **CurrentHealth**, Amount **-30**, Target **Target**, gated by reaction `CurrentHealth > 0` (Features **Value**). [016] |
| "heal 10 / restore health" | action | same component, Amount **+10**. **Sign flip is the only difference between damage and heal.** [014] |
| "walk into a zone and X happens" | area-trigger | static trigger object (Raise Trigger Events) → stateful trigger timeline spawns a TRA at the entrant (Essence Link) → TRA applies the effect. [Trap, Healing Area, AreaLock, Stagger Damage] |
| "leave a clone when I dash" | input-create | dash event → reaction → `ActionCreate` clone ObjectDefinition; clone placed `InitializeTransform.From = Owner` (player). [001] |
| "the clone explodes after 4s" | timed-next-stage | the **clone prefab** owns `Active.duration=4`; on deactivate `ActionCreateOnDeactivate` (explosion, From **Source**) + `ActionDestroyOnDeactivate(Self)` fire together. [004, 005] |
| "explode every 2 dashes" | counter-threshold | dash event → `ActionIntrinsic +1` on a **separate counter Essence** (Target **Source**) → second reaction `OnDashInitiated==1 AND counter==2` → spawn + `ActionIntrinsic -2` to re-arm. [008] |
| "20% chance to explode on dash" | chance-proc | reaction `chanceToTrigger = 0.2`; pair with `ActionDestroyOnChanceFail(Self)` on the proc'd object to clean up failed rolls. [007, 013] |
| "crit = double damage" | chance-proc (extra instance) | NOT a damage-doubling field. Modeled as a **second damage TRA** with reaction `chanceToTrigger=0.5` applying another `CurrentHealth -30`; `ActionDestroyOnChanceFail` cleans the miss. [013] |
| "slow the enemy's animations for 3s" | while-active stat → timeline read | TRA timeline applies **SlowMo** stat **Subtracted 70** for 3s → the *enemy's own* animation timeline has a **TimelineTimeScale** track reading `SlowMo`, so it plays at `SlowMo/100`. The slow is INDIRECT through a stat. [003] |
| "speed me up after dashing" | input-create + timed buff | dash-**completed** event → reaction creates a buff prefab whose `ActionStatAuthoring` adds to **MovementSpeedMultiplier**; buff prefab `Active.duration` = the seconds, `ActionDestroyOnDeactivate(Self)` removes it. [002] |
| "stagger / juggle at a threshold" | counter-threshold + physics | hits add **Stagger Points** (or **StaggerMeter**) to the enemy; a reaction `>= threshold` plays a `PhysicsForce` impulse timeline (e.g. (0,3.5,5)). [012, Stagger Meter] |
| "knock it back / launch it" | action timeline | `ActionTimelineAuthoring` plays a `PhysicsForce` impulse clip on the body. [Clone Explosion, Stagger Meter] |
| "follow / attach / reparent / retarget" | EntityLink track | hand to the relevant `unity-track-entitylink-*` specialist. |
| "give +1 max HP on pickup" | action | `ActionStatAuthoring` **Max Health** Added **1** (Stat, not Intrinsic). [014] |

This table only tells you *which* sibling owns each cell — it does not re-derive
them. Defer:
- **the chain assembly** (layer model, timing-split, full breakage catalogue) →
  `unity-augment-architecture`.
- **WHO it affects** (Owner/Source/Target/Custom, `Initialize.Target`, Essence Link
  override) → `unity-targets`.
- **WHEN it fires** (condition list, Features=Value vs Condition, `Active.duration`/
  `cooldown`, `chanceToTrigger`, `doNotReset`) → `unity-reactions`.
- **WHAT it does** (`ActionCreate`/`ActionStat`/`ActionIntrinsic`/`ActionTimeline`
  + the lifecycle-keyed `ActionCreate/Destroy On*` cleanup idioms) →
  `unity-essence-actions`.
- **the spawned effect prefab itself** (the four key components + LifeCycle) →
  `unity-tra-payloads`.
- **the per-clip Timeline detail** of any "Built as" cell naming a track → that
  track's specialist: physics force/drag/pid/gravity/kinematic/filter, timescale
  (timeline/world), transform position/rotation/scale, essence
  stat/intrinsic/event, entitylink family, distance-to-stat, subdirector,
  animation, parenting.

## "Designer says X / actually built as Y" gaps (verify against this list)

- **"press Z/Space/X after dashing"** → gate on the **dash lifecycle event**
  (`OnDashInitiated` / `OnDashCompleted`, emitted by the **Dash Force timeline**),
  NOT the raw `OnInputDash` key. The augment then works on any binding (keyboard,
  gamepad) because it listens to the *system* event, not the physical key. If that
  event track on the Force timeline is **muted**, nothing fires. [002, 004, 007]
- **"crit / double damage"** → a 50%-chance **second damage instance**, not a
  multiplier. The intended long-term design is damage-graph-driven, but the worked
  build is an extra `CurrentHealth -30` gated by `chanceToTrigger=0.5`. [013]
- **"slow the enemy"** → a **SlowMo stat** the enemy's animation timeline reads via
  a TimeScale track — not global slow-mo (`WorldTimeScale`) and not a direct speed
  change. Changing the stat's modify type from **Subtracted** to **Added** speeds
  the enemy up instead. [003]
- **"4 frost stacks" / "+5 stagger"** → stats are **×100 fixed-point**: 4 stacks =
  Frost Stacks **+400**; "stagger at 5" = `StaggerMeter >= 500`. A designer's
  count is multiplied by 100 in the actual field. [010, 012, Stagger Meter]
- **"for 4 seconds" / "120 frames"** → time is expressed either as a reaction
  `Active.duration` in seconds, a clip duration in seconds, or 60fps frame integers
  (240 frames = 4s). Confirm which the field expects. Note worked scenes sometimes
  drift from the spec (a "5s" buff authored as 4s) — the **scene is the baseline**,
  not the prose. [002, 010, 012]
- **"the clone lasts 4s then explodes"** → the **spawned prefab** owns the timer,
  not the reaction that created it. Creating reaction: `duration=0`,
  `DestroyOnDisabled=false`. [004]
- **"explode at the player"** vs **"explode where the clone was"** → set by
  `InitializeTransformAuthoring.From`: `Owner` = player, `Source` = the expiring
  clone, `Target` = the entrant/struck thing. Wrong `From` spawns the effect in the
  wrong place. [004, 005]
- **"affect whatever I touched"** → `targetLinkOverride = Essence Link` on the
  trigger/instantiate clip: it resolves the contacted object's Essence as the
  payload's Target. Clearing it leaves the effect with no recipient. [016, 010]
- **"damage the enemy"** that secretly damages the player → an action's **Target**
  set to `Owner` instead of `Target` (or `Initialize.Target` mis-set). [016]
- **"a counter that persists across dashes"** → counter lives on a **dedicated
  state-holder Essence** (its own `StatAuthoring` + intrinsic), targeted as
  `Source`, not on the transient reaction object — else it resets each activation.
  [008]
- **"cooldown the dash 2s"** → use the reaction's built-in `Active.cooldown`, NOT a
  separate `Dash Cooldown` intrinsic gate. [011]

## Pick the right KIND of number (the translation step that bites)

Two decisions, both pure vocabulary, both with a silent-failure trap. The full
schema shapes and where each lives are owned by `unity-stats-intrinsics`; adding a
new one is owned by `unity-gameplay-config`. Here you only decide *which kind the
designer's word implies*:

- A **live count** you add/subtract and compare (health, charges, stacks,
  counters) → **Intrinsic** (integer, has default + range).
- A **scaling/percentage** modified by Added/Increased/More (speed, max HP, slow,
  multipliers; ×100 fixed-point, base 100 = 1.0×) → **Stat**. So "+50% speed" is
  not 50 — it depends on whether the field is Added/Increased on a ×100 stat.
- **Two-name trap:** a name can exist as **both** a Stat and an Intrinsic and
  mixing them silently fails — the reaction reads one buffer, the action writes the
  other. `StaggerMeter` exists as both; the worked stagger payloads use the **Stat**
  schema. Always pick the schema the reaction condition actually reads.

## Where the vocabulary lives (defer)

The Assets/Settings tree (Schemas for Stats / Intrinsics / Events / EntityLinks,
ObjectDefinitions) and the three auto-maintained registries (EssenceSettings,
ReactionSettings, ObjectManagementSettings) — plus how to *add* a new number,
event, or spawnable and have it auto-registered — are owned by
`unity-gameplay-config`. Spawnables specifically (ObjectDefinition id + prefab
back-link) are owned by `unity-object-definitions`. Don't restate them; rediscover
the real folders/ids/keys in THIS project (the vex-ee `Essence Link` id 8,
`CurrentHealth` defaults, etc. are illustrative).

## Honest-stop boundaries

- If the request needs a schema, event, input binding, or stage that doesn't exist
  in THIS project, report it plainly and stop — name the missing piece. Don't
  invent a schema or improvise the Stage specialist's job.
- If the translation lands on a Timeline clip you can't author confidently, name
  the track specialist and stop rather than guessing the clip.
- Treat the wiki numbers (ids, keys like Essence Link id 8, CurrentHealth defaults)
  as illustrative; the live project is the source of truth — read it back.
