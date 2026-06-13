---
name: unity-mechanic-cookbook
description: A concrete recipe catalogue for the whole mechanics designers build repeatedly in Arvex — damage/heal zones, projectile/on-press spawners, expiring clones, threshold procs, meter->impulse responses, buff-on-event, rank LV1-4 scaling — each as designer-ask -> canonical chain -> concrete wiring -> defer-to skills -> gotcha. The concrete complement to unity-augment-architecture (which holds the abstract chains and the D1 composition engine). Portable; every name/path/id is a worked example from vex-ee to rediscover in THIS project.
---

# unity-mechanic-cookbook — copy-the-shape recipes

The bridge between the abstract chains in `unity-augment-architecture` (WHY the
pieces connect) and the per-component skills (HOW each piece is authored). Each
recipe is the **exact end-to-end shape** of a mechanic designers ask for over
and over — the wiring MAP and the gotcha, not the field-by-field authoring the
sibling skills own.

**How to use:** match the plain ask to a recipe → read its canonical chain →
follow the wiring (every name/path/id is a **vex-ee worked example to rediscover
in THIS project** via `unity-cli`, never assumed) → defer each piece's details
to its "Defer-to" skill → check the gotcha before claiming success. Behave per
`unity-agent-protocol`; use the editor per `unity-cli`; for the Timeline-clip
ceremony defer to `unity-timeline-track-authoring`.

## Index — recipe → canonical chain

| # | Recipe | Designer ask (example) | Canonical chain |
|---|--------|------------------------|-----------------|
| 1 | Damage / Heal Zone | "a zone that hurts/heals whoever enters" | Trigger-Spawn (area) |
| 2 | On-Contact Payload | "when my weapon touches an enemy, damage it" | Trigger-Spawn (on-hit, TRA) |
| 3 | Projectile / On-Press Spawner | "shoot orbs when I press a key" | Input-Event → ActionCreate |
| 4 | Expiring Spawn | "a clone that explodes after a few seconds" | Timed next-stage / Counter-Expiry |
| 5 | Threshold Proc | "every 2nd dash / 3rd hit, do X" | Counter-Threshold |
| 6 | Meter → Impulse | "knock the player when stagger maxes" | Stat-threshold → ActionTimeline → force |
| 7 | Buff-on-Event | "speed up after I dash" | Event → reaction → while-active stat |
| — | Rank LV1-4 scaling | "make it upgradeable 1→4" | (modifier on any of the above) |
| — | Bullet Time / Knockback | "global slow / knock back" | (pattern noted; wiki thin) |

---

## Recipe 1 — Damage / Heal Zone (area trigger)

**Designer ask.** "Standing in this zone hurts / heals / staggers / locks in
whoever enters." (Trap, Healing Area, AreaLock, Tornado, Stagger Damage.)

**Canonical chain.** Trigger-Spawn (area variant): a static trigger zone spawns
a TRA payload onto the entrant, which applies the effect.

**Concrete wiring** (order to build/verify):
1. **Zone object** (e.g. `Trap/Trap Trigger`, `Healing Area/Healer Trigger`) with
   `PhysicsShapeAuthoring` **Collision Response = Raise Trigger Events** +
   `StatefulTriggerEventAuthoring`. Collides-With must include the body's
   category (e.g. `PlayerBody`/Category05).
2. **Director + `TimelineReferenceAuthoring`** on the zone (so its timeline loops).
3. **`StatefulTriggerTrack`** bound to the zone's `StatefulTriggerEventAuthoring`.
4. **`PhysicsTriggerInstantiateClip`**: ObjectDefinition = the payload (e.g.
   `Trap.asset`, `Healer_Tra.asset`), **Trigger State = Enter**, **Target Link
   Override = Essence Link**.
5. **TRA payload prefab** (`Trap_Tra.prefab`, `Healer.prefab`): the four TRA
   components — `TargetsAuthoring`, `ReactionAuthoring` (`CurrentHealth > 0` on
   **Target**), an `Action*` (`ActionIntrinsic CurrentHealth -20` for Trap,
   `+20` for Healer; `ActionStat StaggerMeter Added +100` for Stagger Damage),
   `ObjectDefinitionAuthoring` pointing back to its own definition; +
   `LifeCycleAuthoring`.

**Defer-to.** Trigger source + clip → `unity-track-stateful-trigger`. Payload
shape → `unity-tra-payloads`. Reaction condition → `unity-reactions`. Number
change → `unity-essence-actions` + `unity-stats-intrinsics`. Spawn identity →
`unity-object-definitions`. Essence-Link target → `unity-targets`. Where assets
register → `unity-gameplay-config`.

**Gotchas.**
- No **Raise Trigger Events** on the zone shape → no enter event → nothing spawns.
- **Target Link Override = Essence Link** is what makes the payload hit the
  *entrant*, not the zone. Cleared → effect can't reach a target.
- **Two-`StaggerMeter` trap:** there are a Stat and an Intrinsic `StaggerMeter`
  schema. Damage payloads use the **Stat** one; mixing silently fails.
- `StaggerMeter` is ×100 fixed-point: "+100 stagger" is `Added 100` on the Stat.

---

## Recipe 2 — On-Contact Payload (on-hit TRA)

**Designer ask.** "When my sword / weapon / projectile body touches something
with health, damage it." This is the canonical TRA worked example (Cube Weapon →
Cube Damage TRA, −30).

**Canonical chain.** Trigger-Spawn (on-hit): a *moving* physics body raises
trigger events; a looping timeline's `PhysicsTriggerInstantiateClip` spawns the
TRA on the struck Essence.

**Concrete wiring.**
1. **Weapon/body** (`Cube Weapon`): `PhysicsBodyAuthoring` (Dynamic, gravity if
   it should fall), `PhysicsShapeAuthoring` **Raise Trigger Events** + collision
   filter, `TargetsAuthoring` (Owner = player Essence), `StatefulTriggerEventAuthoring`.
2. **Driver object** (`Cube Stateful Check`): `PlayableDirector` +
   `TimelineReferenceAuthoring` (loops the check timeline).
3. **`StatefulTriggerTrack`** bound to the weapon's trigger-event component;
   **`PhysicsTriggerInstantiateClip`** (ObjectDefinition = `Cube Damage TRA`,
   Trigger State = Enter, Target Link Override = Essence Link).
4. **TRA prefab** (`Cube Damage TRA.prefab`): the four TRA components; reaction
   `CurrentHealth > 0` on Target; `ActionIntrinsic CurrentHealth -30` on Target.

**Defer-to.** Same set as Recipe 1; the TRA-payload end-to-end is the worked
example inside `unity-tra-payloads`.

**Gotchas.**
- Gravity `0` on a weapon meant to fall → never contacts → never spawns.
- Wrong collision filter categories → no trigger raised.
- Flipping the action's **Target → Owner** turns "damage enemy" into "damage
  myself" (the #1 designer slip — see `unity-targets`).
- Broken two-way ObjectDefinition link (prefab self-identifying as the wrong
  definition, or null prefab ref) → wrong/no spawn (`unity-object-definitions`).

---

## Recipe 3 — Projectile / On-Press Spawner (input-event)

**Designer ask.** "When I press a key, shoot orbs / spawn a projectile from me."
(Orb Shooter, On Hit Spawner front-half.)

**Canonical chain.** Input-Event: input action → event → reaction → `ActionCreate`
spawns the projectile ObjectDefinition.

**Concrete wiring.**
1. **InputAction** (`Player/Unleashed`, binding `<Keyboard>/e`, phase Down) read
   by an **`InputConsumerAuthoring`**.
2. **Input director + `TimelineReferenceAuthoring`**; **`CommandSequenceTrack` +
   `CommandSequenceClip`** (~1/60s) watching the action → emits a `ConditionEvent`
   (`OnInputUnleashed = 1`), routed via **Essence Link** onto the player Essence.
3. **Reaction** (`Orb Shooter/Reaction`): condition `OnInputUnleashed == 1`
   (`Features = Condition`) on Owner → **`ActionCreateAuthoring`** spawning the
   projectile ObjectDefinition (`Orb.asset`).
4. **Projectile prefab** (`Orb.prefab`): `ObjectDefinitionAuthoring` → `Orb.asset`;
   `PhysicsBodyAuthoring` **MotionType = Kinematic**; **InitialLinearVelocity =
   (0,0,100)**; `InitializeTransformAuthoring.Position = From Target` (so it
   launches from the shooter).

**Defer-to.** Input → event → `unity-track-player-inputs` (and `unity-player-input`
to DRIVE a key for verification). Reaction → `unity-reactions`. Spawn action →
`unity-essence-actions`. Spawn pose/identity → `unity-object-definitions` +
`unity-targets`. Event schema registration → `unity-gameplay-config`.

**Gotchas.**
- Listen to the **emitted event** (`OnInputUnleashed`), not a raw key in the
  reaction. For dash-gated mechanics use **`OnDashInitiated`/`OnDashCompleted`**
  (timeline lifecycle), NOT raw `OnInputDash`.
- Kinematic body + `InitialLinearVelocity` is what makes the orb fly straight;
  a Dynamic body falls under gravity instead.
- `InitializeTransform.From/Position` wrong → orb spawns at world origin / the
  wrong actor.

---

## Recipe 4 — Expiring Spawn (timed next-stage)

**Designer ask.** "Leave a clone on dash that explodes after a few seconds."
(Clone Jutsu = spawn; Clone Expiry / Ashen Wraith = the timed explode.)

**Canonical chain.** Timed next-stage: the *creating* reaction only spawns; the
*spawned prefab* owns its own lifetime and, on deactivate, spawns the next stage
and destroys itself. (A countdown-Intrinsic variant exists — see below.)

**Concrete wiring (lifetime-on-prefab variant, the recommended one).**
1. **Creating reaction** (`Ashen Wraith/Reaction`): condition `OnDashInitiated == 1`;
   **`Active.duration = 0`**; `ActionCreateAuthoring` (Definition = `Ashen Wraith
   Clone`, Target = Owner, **`DestroyOnDisabled = false`** — it must NOT own the timer).
2. **Clone prefab** (`Ashen Wraith Clone.prefab`): `ObjectDefinitionAuthoring`;
   `InitializeTransformAuthoring.From = Owner` (spawn at player);
   **`ReactionAuthoring.Active.duration = 4`** (the clone owns the 4s, conditions
   empty); **`ActionCreateOnDeactivateAuthoring`** (Definition = explosion) +
   **`ActionDestroyOnDeactivateAuthoring` Target = Self** — fire simultaneously
   on deactivate. Keep `LinkedEntityGroupAuthoring` enabled.
3. **Explosion prefab** (`Ashen Wraith Explosion.prefab`):
   `InitializeTransformAuthoring.**From = Source**` (spawn where the clone was,
   not at the player). Add a `PhysicsForceTrack` impulse if it should knock back.

**Countdown-Intrinsic variant** (Clone Expiry): a `TimelineEssenceIntrinsicTrack`
clip subtracts `-1` from a `CloneExpiry` intrinsic at 5s (routeLink = Essence
Link); an Explosion reaction checks `CloneExpiry == 0` and an `ActionTimeline`
plays a `PhysicsForceTrack` impulse on `Movement Physics`.

**Defer-to.** Lifecycle/on-deactivate actions → `unity-essence-actions`. The
countdown clip → `unity-track-essence-intrinsic`. The explosion force →
`unity-track-physics-force`. Spawn pose → `unity-object-definitions` +
`unity-targets`. Composition → `unity-augment-architecture`.

**Gotchas.**
- **Timing lives on the spawned prefab, not the creating reaction.** Creating
  reaction `duration = 0` + `DestroyOnDisabled = false`; clone `duration = 4`.
  Putting the timer on the creator is the #1 composition mistake.
- Explosion `From = Source` (clone position), not `Owner` (player).
- The dash event track on the Dash Force timeline must **not be muted**, or the
  clone never spawns.

---

## Recipe 5 — Threshold Proc (counter)

**Designer ask.** "Every 2nd dash spawn an explosion" / "every 3rd hit proc
chain lightning." (Explosion-every-2-dashes 008; Chain Lightning at counter 3.)

**Canonical chain.** Counter-Threshold: an event increments an intrinsic on a
**separate state-holder Essence**; a second reaction requires `event AND
counter==N`, fires the effect, then subtracts N to re-arm.

**Concrete wiring** (vex-ee 008 worked example):
1. **Counter holder** (`Double Dash Explode/Dash Counter`): its own Essence
   (`StatAuthoring` adding the `Dash Counter` intrinsic) — persists the count
   independent of any single reaction activation.
2. **Increment reaction** (`Dash Counter Storing Mechanic`): condition
   `OnDashInitiated == 1` on **Owner** → `ActionIntrinsic Dash Counter +1` on
   **Source** (= the counter holder; TargetsAuthoring Owner = player Essence,
   Source = Dash Counter).
3. **Threshold reaction** (`Two Dash Explosion Summon`): conditions
   `OnDashInitiated == 1` (Owner) **AND** `Dash Counter == 2` (Source) →
   `ActionCreate` (`Two Dash Explosion`, Target = Owner) **AND** `ActionIntrinsic
   Dash Counter -2` (Source) to re-arm.

For **Chain Lightning** the same shape: increment a `LightningCounter`, a
reaction checks `LightningCounter == 3`, spawns a sphere at the target, resets.
(Wiki Chain Lightning is high-level — concrete timeline/prefab paths are NOT
documented; rediscover or build them.)

**Defer-to.** Counter intrinsic + its schema → `unity-stats-intrinsics` (and
`unity-track-essence-intrinsic` if a timeline drives it). Increment/spawn/reset
actions → `unity-essence-actions`. AND'd conditions + Owner-vs-Source targeting
→ `unity-reactions` + `unity-targets`. Counter recipes overview →
`unity-augment-architecture`.

**Gotchas.**
- The counter MUST live on a **separate Essence** used as `Source`, or it resets
  with the reaction and never accumulates.
- Re-arm by subtracting N in the SAME threshold reaction; forgetting it fires
  once and never again.
- For dash counting, gate on `OnDashInitiated`, not raw `OnInputDash`.

---

## Recipe 6 — Meter → Impulse Response

**Designer ask.** "When stagger maxes out, knock the player back / launch them."
(Stagger Meter / Staggered Response.)

**Canonical chain.** Stat-threshold → `ActionTimeline` → `PhysicsForceTrack`
impulse.

**Concrete wiring.**
1. **Reaction** (`Player/Staggered/Reaction`): condition `StaggerMeter` **Stat**,
   Target = Essence, Operation = **Greater Than Equal**, Value = **500** (×100
   fixed-point ⇒ designer's "stagger at 5"). → `ActionTimelineAuthoring` plays
   the force timeline.
2. **Force timeline** (`Staggered.playable`): a **`PhysicsForceTrack`** clip,
   Mode = Impulse, Linear Force = `(0,10,10)`, Space = Self, **bound to
   `PhysicsBodyAuthoring: Movement Physics`**.

**Defer-to.** Threshold condition → `unity-reactions`. The Stat itself + the
×100 rule → `unity-stats-intrinsics`. Play-a-timeline action →
`unity-essence-actions`. The force clip (modes, latching, one-impulse-per-
activation) → `unity-track-physics-force`.

**Gotchas.**
- Use the **Stat** `StaggerMeter` here (the threshold reads ×100: `>= 500` ==
  "5"). The two-`StaggerMeter` trap again — Stat vs Intrinsic.
- Force track must bind to the **physics body** (`Movement Physics`), not the
  Essence; binding the wrong object → no impulse.
- One impulse per activation — see `unity-track-physics-force` for re-fire rules.

---

## Recipe 7 — Buff-on-Event (timed while-active stat)

**Designer ask.** "After I dash, I'm faster for a few seconds." (Gain Movement
Speed OnDash; the while-active half of slow/buff augments.)

**Canonical chain.** Event → reaction → `ActionTimeline` playing a
`TimelineEssenceStatTrack` clip that adds a stat modifier for the clip's length,
then auto-removes it on clip end.

**Concrete wiring.**
1. **Source event.** Dash input timeline writes `OnInputDash = 1` (ideally use
   `OnDashCompleted` so the boost lands when the dash ends — the wiki notes the
   current impl fires on `OnInputDash` and delays the stat clip ~0.5s).
2. **Reaction** (`Movement Spd Rn/Reaction`): conditions `OnInputDash == 1` AND
   `Movement Speed LV == n` → `ActionTimeline` plays the rank's boost timeline.
3. **Boost timeline** (`Movement Increase Upon Dash/LVn.playable`):
   `TimelineEssenceStatTrack` clip adding `+20…+60` to `MovementSpeedMultiplier`
   for `3…5s`, **bound to the player `Essence`**. Base `MovementSpeedMultiplier =
   100`.

**Defer-to.** While-active stat clip + ×100 add math →
`unity-track-essence-stat`. Stat schema → `unity-stats-intrinsics`. Reaction
conditions → `unity-reactions`. Event source → `unity-track-player-inputs`.

**Gotchas.**
- The clip auto-removes its modifier on clip end — the clip LENGTH is the buff
  duration; don't try to remove it manually.
- Bind the stat clip to the **Essence**, not the physics body (the dash *force*
  clip binds to `Movement Physics` — different object, easy to swap).
- Prefer `OnDashCompleted` over raw `OnInputDash` when available.

---

## Modifier — Rank LV1-4 scaling

**Designer ask.** "Make it upgradeable: level 1 → 4, stronger each tier."

**Shape (not its own chain — layered onto recipes 1, 4, 6, 7).** Add a rank
intrinsic (e.g. `Trap LV`, `Healing Area LV`, `Movement Speed LV`,
`Dash_Force_LV`, range `0..4`). Author ONE reaction PER rank, each with an extra
condition `<Rank LV> == n`, each pointing at a different amount / ObjectDefinition
/ timeline:
- Trap LV1-4 → `-20 / -40 / -60 / -80` CurrentHealth (different payloads/amounts).
- Healing Area LV1-4 → `+20 / +40 / +60 / +80`.
- Movement Speed LV1-4 → `+20 (3s) / +30 / +50 / +60 (5s)`.
- Dash Force LV1-4 → force `z = 10 / 20 / 30 / 40`.

**Defer-to.** Rank intrinsic schema + range → `unity-stats-intrinsics` /
`unity-gameplay-config`. The `== n` condition → `unity-reactions`.

**Gotcha.** Rank conditions are **`Intrinsic == n` (Equal)**, one reaction per
rank — only the matching rank's reaction fires. Raising the rank means setting
the intrinsic; the highest active reaction wins because only its `== n` passes.

---

## Patterns the wiki leaves thin (don't invent specifics)

- **Bullet Time / global slow.** No worked area-mechanic recipe; the mechanism
  is a `SlowMo`/`WorldTimeScale` stat feeding a timescale track. Build the
  *pattern* (stat → timescale clip) and defer to `unity-track-world-timescale`
  (global) or `unity-track-timeline-timescale` (per-timeline); `unity-augment-
  architecture` notes the "while-active stat → timeline read" hand-off (e.g.
  `SlowMo -70` for 3s read by an enemy TimeScale track).
- **Knockback (standalone).** `Knockbacks.md` is only "(see attached image)" —
  **no concrete schemas, timelines, or prefab chain documented.** Use the
  Meter→Impulse pattern (Recipe 6) or the expiry-explosion force (Recipe 4) as
  the shape; do not invent a dedicated Knockback asset set. Related crumbs:
  Tornado adds `LightKnockbackCounter +1` for 1 frame alongside `CurrentHealth
  -20`; verify in-project before relying on either.
- **Tornado / Chain Lightning.** Designed and behaviourally described, but the
  wiki lacks concrete asset paths / scene hierarchy / exact schema paths.
  Inspect the project before recreating; the SHAPE is Recipe 1 (Tornado = area
  damage + knockback counter) and Recipe 5 (Chain Lightning = counter==3 spawn).
