---
name: unity-reactions
description: The designer's mental model + concrete authoring of BovineLabs ReactionAuthoring — the "IF <conditions> THEN <actions>" brain of every TRA payload. Covers the AND'd condition list (schema · Target · Operation · Value · ComparisonMode), the Features=Value vs Features=Condition split, Active.duration / Active.cooldown timing, Conditions.chanceToTrigger gating (+ ActionDestroyOnChanceFailAuthoring cleanup), doNotReset latching, and the composite (OR/NOT) expression escape hatch. Portable to any project with com.bovinelabs.reaction; worked examples from vex-ee. Use when a designer reasons about WHEN an effect should fire ("only when health > 0", "50% of the time", "for 0.65s then 2s cooldown", "stagger at 5").
---

# Reactions — the "WHEN should this fire?" brain

`ReactionAuthoring` is the **decision component** sitting on a TRA payload or a player sub-object. It answers
one question for the designer: *given everything I can observe right now, should the attached Action(s) fire,
and for how long?* Actions (damage, spawn, force, stat buff) are separate components; this skill owns only the
**gate**. For how reactions slot into a whole mechanic (Input → Event → Reaction → Action → ObjectDefinition →
payload), see `unity-augment-architecture`. Behave per `unity-agent-protocol`; operate the editor per `unity-cli`.

This is NOT a Timeline track. A designer doesn't author it on a clip — they reason about it as "the rule." Lead
with the rule, then wire it.

## Discover, don't assume

Everything below is a **worked example from vex-ee**. In THIS project, rediscover via `unity-cli`:
- The condition **schemas** you compare against (event `ConditionEventObject`, stat `StatSchemaObject`, intrinsic
  `IntrinsicSchemaObject`) — their key/default/range. Schema homes and the registries live in
  `unity-augment-architecture` / `unity-stage-foundations`; do not re-derive them here.
- The exact inspector **enum display names** for `Operation` and `Features` (Unity may relabel; verify on a real
  component before claiming a value).
- Whether `ActionDestroyOnChanceFailAuthoring` (addon `com.bovinelabs.reaction.addon`) exists in this project.

Type homes (verify): `ReactionAuthoring`, `ConditionAuthoring`, `ActiveAuthoring`, `ConditionFeature`,
`Equality`, `ConditionEventObject` → `com.bovinelabs.reaction` (external, in `Library/PackageCache`).

## Anatomy (one component, two halves)

`ReactionAuthoring` **requires** `LifeCycleAuthoring` + `TargetsAuthoring` on the same object (it's `[RequireComponent]`
and `[DisallowMultipleComponent]`). It exposes two foldouts:

- **`Conditions`** — the IF half: a list of conditions (AND'd by default), plus chance / reset / composite logic.
- **`Active`** — the timing half: how long the "fired" state lasts and its cooldown.

The Action components (e.g. `ActionIntrinsicAuthoring`, `ActionCreateAuthoring`, `ActionTimelineAuthoring`,
`ActionStatAuthoring`) are added separately and run while the reaction is Active.

## The condition list — one row = one clause (max 8, AND'd)

Each row (`ConditionData`) the designer fills in:

| Field | What the designer is saying | Notes |
|---|---|---|
| **Condition** | "look at THIS named thing" | A schema ScriptableObject: an event (`OnInputDash`), a stat (`StaggerMeter`), or an intrinsic (`CurrentHealth`). Null → bake error "Null condition." |
| **Target** | "...on WHOM?" | `Target` enum: `None/Target/Owner/Source/Self/Custom` (home `com.bovinelabs.reaction`). Defaults to `Target` (= whom the effect lands on). Forced to `None` if the schema is global. Target semantics live in the sibling targets/augment skills — don't re-derive. |
| **Operation** | the comparison | `Equality` enum (see below). |
| **Value** | the number to compare against | Only used when Operation ≠ `Any`. |
| **ComparisonMode** | where Value comes from | `Constant` (the typed number) or `Custom` (a `[SerializeReference]` `ICustomComparison` — advanced; resolved at bake). Designers use `Constant`. |
| **Features** | event-presence vs numeric value | THE field designers get wrong — see below. |
| **Destroy if target destroyed** | self-cleanup | Default `true`: if the watched target dies the reaction can no longer evaluate, so it removes itself. Set `false` only if you'll re-point the target manually. |
| **Cancel Active** | "kill the effect early if this clause goes false" | Only valid when `Active.duration > 0`; OnValidate auto-clears it otherwise (logs a warning) and auto-sets `Active.cancellable = true` when used. |

All rows must pass for the reaction to fire (logical AND). For OR/NOT/XOR, see Composite below.

### `Operation` (the `Equality` enum — VERIFY display labels)

`Any` (event merely exists, ignores Value) · `Equal` · `NotEqual` · `GreaterThan` · `GreaterThanEqual` ·
`LessThan` · `LessThanEqual` · `Between` (uses Value**Min**/Value**Max**, inclusive — the only operation that
reads the Min/Max fields instead of Value). Inspector labels are often spaced ("Greater Than Equal").

## Features = Value vs Features = Condition — the #1 slip

`ConditionFeature` is a flags enum: `Invalid` (= error, never ship it), `Condition`, `Value`, `Accumulate`.

- **`Condition`** (the default): "use this clause to decide if the reaction is Active." For **event / binary**
  checks like `OnInputUnleash == 1` or `OnInputDash == 1`. The event's mere presence/value gates activation.
- **`Value`**: "record this number so the comparison actually reads it." Required for **numeric stat/intrinsic**
  comparisons like `CurrentHealth > 0` or `StaggerMeter >= 500`. If a numeric condition mysteriously never
  evaluates the way you expect, **this field is the first thing to check** — flip it to `Value`.
- **`Accumulate`** = `Condition | Value | (1<<2)`: each incoming event *adds* its value instead of replacing.
  Events only, not states. Niche.

Designer rule of thumb: **event/binary → `Condition`; comparing a stat or intrinsic against a number → `Value`.**
(Worked caveat: vex-ee's "Stagger at 5" reaction ships with `StaggerMeter >= 500`, `Features = Condition` and
works — so projects vary. Verify the live component rather than trusting the rule blindly; the brief's stance is
that `Value` is the *expected* setting for numeric comparisons and the first knob to try when one misbehaves.)

## `Active` — how long "fired" lasts, and its cooldown

`ActiveAuthoring` fields:

- **`duration`** (`Min(0)`): how long the reaction stays Active after firing. `0` = instantaneous fire. The
  duration **is** the effect window for while-active actions.
- **`cooldown`** (`Min(0)`): built-in re-fire lockout. **Starts the instant the reaction triggers**, even while a
  duration is still running — unless `cooldownAfterDuration` is set, which delays the cooldown start until the
  duration ends.
- **`trigger`**: requires external manual triggering (adds an `ActiveTrigger`, auto-resets). Most reactions leave
  this off and fire purely from conditions.
- **`cancellable`**: lets a `Cancel Active` condition end the effect early (auto-enabled by that condition).

**Worked example — On-Dash Damage + Double Cooldown (augment 011):** the dash *cooldown* is NOT a separate
intrinsic gate — it's literally `Player/Dash/Dash Force/Reaction → Active.cooldown = 2.0`, with condition
`OnInputDash == 1`. Designer lesson: **the reaction's own `Active.cooldown` IS the cooldown** — don't build a
counter for it. "Cooldown too long/short" → check this one field.

## `Conditions.chanceToTrigger` — randomness lives HERE, not in a script

A `[Range(0,1)]` float (default `1` = always). When all conditions pass, the reaction rolls this chance; on
success the Actions fire, on failure they don't. **This is how the project does ALL random/chance behavior** —
there is no custom random script. Baking warns if it's `0` (never fires) or `> 1`.

**Pairs with `ActionDestroyOnChanceFailAuthoring`** (addon package; `[RequireComponent(typeof(ReactionAuthoring))]`):
a `Target[]` list of whom to destroy when the conditions were all true **but the chance roll failed** (the system
fires on `ConditionActive.AllTrue` while `Active` is disabled, i.e. the fail branch). Use it on a transient
chance-gated payload so the spawned TRA cleans itself up whether the roll wins (destroy-on-activate) or loses
(destroy-on-chance-fail) — otherwise a failed-roll payload leaks.

**Worked example — Crit Chance 50% (augment 013):** "crit = double damage" is NOT a damage multiplier. It's modeled
as a **second, independent damage TRA** spawned alongside the normal one, whose `ReactionAuthoring` has
`chanceToTrigger = 0.5`. Its condition is `CurrentHealth > 0` (`Operation = GreaterThan`, `ComparisonMode = Constant`,
`Value = 0`, on `Target`). The crit prefab carries the full cleanup trio, all `Target = Self`:
`ActionDestroyOnActivate` (roll won, damage applied) + `ActionDestroyOnDeactivate` + `ActionDestroyOnChanceFail`
(roll lost). Designer translation: **"X% chance to do Y" = a separate Y-payload gated by a `chanceToTrigger = X`
reaction, with chance-fail cleanup.**

## `Conditions.doNotReset` — latch it permanently

Default `false`: each frame re-evaluates; when conditions stop being true the reaction goes inactive again. Set
`true` and **once all conditions have been met it stays true forever** — for one-shot / quest-style "it happened,
don't undo it" gates. (Crit TRA in augment 013 sets `doNotReset = true` so the single crit resolves cleanly rather
than flickering.)

## Composite logic — the OR / NOT escape hatch

The list is AND by default. For complex boolean logic, fill `Conditions.conditionLogic` with a string **expression**
over condition **indices** (0-based, the row order): operators `&` (AND), `|` (OR), `^` (XOR), `!` (NOT), and
parentheses. E.g. `"(0 | 1) & !2"`. Leave empty for plain AND. Note: `ActionDestroyOnChanceFail` is skipped on
entities that use composite conditions (the system has `WithNone<ConditionComposite>`), so don't combine chance-fail
cleanup with a composite expression.

## Designer-intent → reaction wiring (quick map)

| Designer says | Reaction wiring |
|---|---|
| "only hit things that are alive" | one row: `CurrentHealth`, `GreaterThan`, `Value = 0`, `Features = Value`, Target `Target` |
| "fire when I press X" | one row: the input event (`OnInputUnleash`), `Equal`, `Value = 1`, `Features = Condition`, Target `Owner` |
| "stagger when the meter hits 5" | one row: `StaggerMeter` stat, `GreaterThanEqual`, `Value = 500` (×100 fixed-point — see stat sibling), Target = the Essence; then an Action timeline |
| "2-second cooldown on dash" | `Active.cooldown = 2.0` on the dash reaction (NOT a counter) |
| "lasts 0.65s" | `Active.duration = 0.65` |
| "50% chance" | `chanceToTrigger = 0.5` + `ActionDestroyOnChanceFailAuthoring` for cleanup |
| "between 3 and 5 charges" | `Operation = Between`, ValueMin/ValueMax (NOT Value) |
| "do it once, then never again" | `doNotReset = true` |
| "either A or B, but not C" | `conditionLogic = "(0 | 1) & !2"` |

## Authoring & evidence notes

- Mutating a `ReactionAuthoring` is editing a SubScene/prefab GameObject's serialized component — operate inside the
  SubScene/prefab bracket and capture PRE-state per `unity-timeline-track-authoring`; record the inverse for undo.
- **Evidence to read back**: the condition rows (schema name, Operation, Value, Features, Target), `chanceToTrigger`,
  `doNotReset`, `Active.duration`, `Active.cooldown`, and the presence of any `ActionDestroy*` cleanup components.
- **Bake errors to surface honestly**: "Null condition." (empty Condition slot), "Condition set to invalid"
  (`Features = Invalid`), "The comparison mode has not been set when using custom data." (Custom mode, no
  `ICustomComparison`). Don't paper over these — report the offending row.
- Stat/intrinsic semantics (×100 fixed-point, which schema is the *Stat* vs the *Intrinsic* `StaggerMeter` — mixing
  them silently fails), event routing, and Target resolution are owned by the sibling skills
  (`unity-stats-intrinsics`, `unity-essence-actions`, `unity-targets`, `unity-augment-architecture`,
  `unity-tra-payloads`, `unity-object-definitions`). Cross-reference them; don't restate.
