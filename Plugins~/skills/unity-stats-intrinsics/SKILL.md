---
name: unity-stats-intrinsics
description: The designer's mental model for BovineLabs Essence numbers — Stat (scaling/modifiable, ×100 fixed-point, base 100 = 1.0×) vs Intrinsic (live integer counter with default/range/clamp-to-stat), the StatSchemaObject/IntrinsicSchemaObject asset shapes, StatAuthoring (AddStats/AddIntrinsics, defaults, CopyFrom snapshot), the Added/Subtracted/Increased/More modify types, and the two-StaggerMeter same-name-different-key trap. Read before reasoning about any gameplay number — what it is, where its schema lives, how it's seeded and modified. Portable to any project with com.bovinelabs.essence; worked example from vex-ee.
---

# Essence numbers: Stats & Intrinsics (designer concept)

Behave per `unity-agent-protocol`; inspect/mutate the editor per `unity-cli`.
This is NOT a Timeline track — it's the number layer every mechanic stands on.
For the tracks that READ/WRITE these numbers at runtime, defer to the siblings:
`unity-track-essence-stat` (while-clip stat buff/nerf), `unity-track-essence-intrinsic`
(one-shot counter grant/spend), `unity-track-timeline-timescale` /
`unity-track-world-timescale` (a stat driving playback speed). For who an action
lands on, `unity-targets`; for the spawn/reaction chain that mutates them,
`unity-reactions` / `unity-tra-payloads` / `unity-augment-architecture`.

## 1. The one distinction a designer must hold

| | **Stat** | **Intrinsic** |
|---|---|---|
| Designer meaning | a *scaling dial*: speed, damage, max-health, "slow %", "how staggerable" | a *live count / gauge*: current health, ammo, dash charges, combo points, a meter filling up |
| Mutated by | layered modifiers that stack and can be removed | direct integer +N / −N |
| Schema asset | `StatSchemaObject` | `IntrinsicSchemaObject` |
| Has a default value? | no (schema is just an id) — seeded on the entity | yes (in the schema), plus a range and optional clamp |
| Fixed-point | **yes, ×100** (see §4) | no — plain integers |
| Typical track | `TimelineEssenceStatTrack`, time-scale tracks | `TimelineEssenceIntrinsicTrack` |

Rule of thumb: if the designer says "buff/nerf/scale/multiply" → Stat. If they say
"gain/spend/charge/fill/it hits a threshold" → Intrinsic.

## 2. Where they live (rediscover — names below are vex-ee examples)

Schema ScriptableObjects, one asset per number, auto-registered into one settings
registry (the `[AutoRef]` on each schema names the registry + folder):

- Stat schemas → `Assets/Settings/Schemas/Stats/<Name>.asset` (worked example).
- Intrinsic schemas → `Assets/Settings/Schemas/Intrinsics/<Name>.asset`.
- The registry that both feed into → `Assets/Settings/Settings/EssenceSettings.asset`
  (`statSchemas` + `intrinsicSchemas` lists). A schema NOT in the registry has no
  runtime key → silent no-op.

Type homes (external, in `Library/PackageCache/com.bovinelabs.essence*`):
`StatSchemaObject`, `IntrinsicSchemaObject`, `StatAuthoring` →
`BovineLabs.Essence.Authoring`. **Discover the actual folder/registry names in THIS
project by query — never hardcode the paths above.** To list what exists:
`unity-cli` find assets of type `StatSchemaObject` / `IntrinsicSchemaObject`, or read
`EssenceSettings.asset`.

## 3. What's inside each schema asset

**StatSchemaObject** — almost nothing. Just an id:
```yaml
m_EditorClassIdentifier: ...StatSchemaObject
isGlobal: 0
key: 113            # bare ushort, auto-assigned by the registry
```
A stat carries no default and no range. Its starting value is whatever an entity's
`StatAuthoring` seeds (§5); absent → reads as the engine default (multiplier 1.0,
i.e. ×100 internal — see §4).

**IntrinsicSchemaObject** — a fuller definition:
```yaml
m_EditorClassIdentifier: ...IntrinsicSchemaObject
key: { Value: 8 }          # NOTE the wrapper — intrinsic key is {Value: N}, NOT a bare int
defaultValue: 0            # starting count if the entity seeds none
range: { x: 0, y: 999999 } # hard clamp (min, max)
minStat: { fileID: 0 }     # optional: clamp the floor to a Stat's live value
maxStat: { guid: ... }     # optional: clamp the ceiling to a Stat's live value
```
`minStat`/`maxStat` let a *count* be bounded by a *dial*: e.g. the StaggerMeter
intrinsic's `maxStat` points at the `EnemyMaxStagger` stat, so the meter's ceiling
scales with that enemy's resilience. Spending/granting past `range` or the stat
clamp is silently capped — a designer who "added 1000 but it stuck at the max" is
hitting this, not a bug.

The struct shape difference (`key: 113` vs `key: {Value: 8}`) is the fastest way to
tell, from raw YAML, whether a `.asset` is a Stat or an Intrinsic.

## 4. The ×100 fixed-point (Stats only) — base 100 means 1.0×

Stats are stored as integers internally and read back divided by 100
(`StatValue.ToInt = 100`, read value = `Added × Multi × 0.01`). So:

- **A stat whose neutral value is "1.0× / 100%" is authored/seeded as `100`.**
  vex-ee's movement-speed dial sits at `100` and reads as a 1.0 multiplier; a "slow
  to 30%" dial reads `30`. "MovementSpeedMultiplier base 100" in the wikis IS this
  convention (note: the actual asset in vex-ee is named `MovementSpeed` — names drift,
  rediscover them).
- An `Added +20` to such a stat ⇒ internal 120 ⇒ reads as 1.2× (a +20% boost).
- **`SlowMo`** is the same idea: baseline `100`, a `Subtracted 70` payload drops it to
  `30`, and a time-scale track reads `SlowMo / 100 = 0.3×` to slow an enemy's animation.
  (Designer says "slow the enemy's animation" → a `SlowMo` *stat* feeding a
  `TimelineTimeScaleTrack`; see `unity-track-timeline-timescale`.)
- **Stagger threshold "5"** is really `StaggerMeter >= 500` because of ×100.

Intrinsics are NOT scaled — a count of 5 is literally 5.

## 5. Seeding the numbers: StatAuthoring

`StatAuthoring` (a component on the entity that owns the numbers, usually the player/
enemy **Essence** object) is what actually gives an entity its starting stats and
intrinsics. Fields a designer touches:

- **`AddStats`** (bool) — gate; off = this entity gets no stat buffer.
- **`StatDefaults[]`** — list of `{ Stat schema, ModifyType, Value }`. Each is the
  *seed* layer. Additive across the list (and across `StatDefaultGroups`, reusable
  bundles). Seed the neutral dial here, e.g. MovementSpeed `Added 100`.
- **`StatsCanBeModified`** (bool) — **must be ON** for any runtime modifier (a Stagger
  Damage payload, a buff track) to stick. If OFF, the stats are frozen at their seed
  and every later `ActionStat` / stat track silently does nothing.
- **`AddIntrinsics`** (bool) + **`IntrinsicDefaults[]`** — `{ Intrinsic schema, Value }`
  seeds (also additive, also have `IntrinsicDefaultGroups`). Absent → the schema's own
  `defaultValue` is used.
- **`Initialize.CopyFrom`** (a `Target`, default `Source`) — only used when
  `StatsCanBeModified` is OFF: on spawn, snapshot the stats from another entity (e.g.
  a projectile copies its `Source`/owner's stats so its damage reflects the shooter at
  fire time). Frozen-but-copied, by design. See `unity-targets` for the `Target` enum.

## 6. Modify types (the runtime change vocabulary)

When a track or `ActionStat` changes a **Stat**, the designer picks a *ModifyType*
(authoring enum `StatAuthoringType`). Four a designer uses (+ their negatives):

| Authoring | Effect | Stacks as |
|---|---|---|
| **Added** | flat `+Value` to the integer base | sum of all Added |
| **Subtracted** | flat `−Value` (same channel as Added, just negated) | sum |
| **Increased** | `+Value%` additively with other Increased | `(1 + ΣIncreased)` |
| **More** | `×(1+Value)` compounding, multiplies with other More | `Π(1 + More)` |
| (`Reduced`/`Less` = the negative twins of Increased/More) |

Final stat = **`ΣAdded × (1 + ΣIncreased) × Π(1 + More)`** (then ×0.01 to read).
So "two +50% Increased" = +100% (×2), but "two +50% More" = ×1.5×1.5 = ×2.25.
`Added`/`Subtracted` take an integer value (remember ×100); `Increased`/`More` take a
fraction (0.5 = 50%). **Intrinsics have no ModifyType** — `ActionIntrinsic` just adds a
signed integer amount.

## 7. The two-StaggerMeter trap (the headline gotcha)

A single human-readable name can exist as **both** a Stat schema and an Intrinsic
schema — two different assets, two different key spaces, two different runtime buffers.
vex-ee ships exactly this: a `StaggerMeter` *Stat* (the one the working stagger
reaction reads, `>= 500`) **and** a `StaggerMeter` *Intrinsic* (clamped by
`EnemyMaxStagger`). They look identical in a dropdown by name.

Failure mode: a designer's reaction condition points at the Stat `StaggerMeter` while
their damage action adds to the Intrinsic `StaggerMeter` (or vice versa). Nothing
errors — the action writes one buffer, the condition reads the other, and the meter
"never fills." **Always verify by the asset/type, not the displayed name**: confirm
the reaction's schema and the action's schema are the *same asset* (same file, same
type). When inspecting via `unity-cli`, read the schema's class identifier
(`StatSchemaObject` vs `IntrinsicSchemaObject`) and key, not just its `m_Name`.

This generalizes: any "I set the number but the gate never triggers" bug should first
check Stat-vs-Intrinsic mismatch, then registry membership (§2), then
`StatsCanBeModified` (§5), then the clamp ceiling (§3).

## 8. Designer-intent quick map

- "make the player 20% faster after a dash" → seed MovementSpeed `Added 100`, then a
  stat track adds `Added 20` (→120→1.2×) for the duration.
- "slow this enemy's animation to 30%" → `SlowMo` stat, `Subtracted 70` from base 100,
  read by a time-scale track as 0.3×.
- "stagger when the bar hits 5" → it's `>= 500` on the StaggerMeter **stat** (×100);
  make sure damage feeds that same stat asset (§7).
- "give 3 dash charges, can't exceed max" → DashCharges **intrinsic**, `defaultValue`
  or seeded `3`, `range`/`maxStat` enforcing the cap.
- "this projectile's damage should match my power at fire time" → projectile's
  `StatAuthoring` with `StatsCanBeModified` OFF + `Initialize.CopyFrom = Source`.

## 9. Honest limits

These schema assets and `StatAuthoring` are *authoring/baked* data — changing a
schema's range/default or an entity's seeds is a SubScene/asset edit, undone by
restoring the prior asset/component state (capture PRE-state per
`unity-timeline-track-authoring`'s convention). Per-frame value changes at *runtime*
come from the action/track siblings, not from here. If you cannot confirm a schema's
type or registry membership from the live editor, say so rather than guessing which
`StaggerMeter` the designer meant.
