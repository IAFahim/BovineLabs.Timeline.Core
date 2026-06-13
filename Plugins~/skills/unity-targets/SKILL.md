---
name: unity-targets
description: The Owner/Source/Target model a designer reasons about in BovineLabs "Arvex" reactions — the Target enum (None/Target/Owner/Source/Self/Custom), the Targets component every reaction/payload carries, how Initialize.Target re-routes a spawned payload's victim, the one-field flip that turns "damage the enemy" into "damage myself", and the Essence Link "act on whatever I touched" override. NOT a Timeline track — a gameplay concept. Portable to any project with com.bovinelabs.reaction + the EntityLinks package; worked example from vex-ee.
---

# unity-targets — who an effect lands on

Every reaction and every spawned payload carries a small address book: **Owner,
Source, Target, Custom**. Each condition and action picks ONE of those slots to
read from or write to. Get the slot wrong and the right effect lands on the
wrong body — heal flows to the enemy, damage flows to you. This skill is the
designer's mental model for that address book and the three ways its entries get
filled.

Behave per `unity-agent-protocol`; use the editor per `unity-cli`. This is a
*concept*, not a track — there is no SubScene bracket here. When you actually
build the payload that uses these targets, that ceremony lives in
`unity-augment-architecture` (whole-mechanic) or a `unity-track-*` skill.

**Discover, never assume.** Type/field/asset names below are the vex-ee worked
example. In THIS project, confirm them: the enum and component live in
`com.bovinelabs.reaction` (external, in `Library/PackageCache`); the "Essence
Link" link schema lives under `Assets/Settings/Schemas/EntityLinks/`. Grep
before you rely on a name.

## The four slots (the address book)

The runtime component is `Targets` (`BovineLabs.Reaction.Data.Core`): four
`Entity` fields — `Owner`, `Source`, `Target`, `Custom`. Authored via
`TargetsAuthoring` (added automatically by `ReactionAuthoring`). In plain terms:

| Slot   | Designer meaning                                    | Authoring default |
|--------|-----------------------------------------------------|-------------------|
| Owner  | Whose effect this is — usually the player Essence   | empty → prefab root |
| Source | Who/what set it off — the attacker / spawner / clip | empty → prefab root |
| Target | Who it lands on — the thing hit / affected          | the bound GameObject |
| Custom | One extra slot for a secondary relationship          | none |

In `TargetsAuthoring` you drag a GameObject into Owner/Source/Target/Custom.
Leaving Owner or Source empty defaults it to the prefab root (so a payload
"belongs to" itself unless told otherwise).

## The Target enum — the dropdown a designer actually touches

Conditions and actions don't store an entity; they store a **`Target` enum value**
naming which slot to resolve. Verified in `Target.cs` (note: Custom is 6, not 5):

```
None = 0   → nothing (Entity.Null)
Target = 1 → the Target slot   ← the default everywhere
Owner = 2  → the Owner slot
Source = 3 → the Source slot
Self = 4   → THIS reaction/payload entity itself
Custom = 6 → the Custom slot
```

Resolution is `Targets.Get(target, self)`: a plain switch. `Self` is the only
value that ignores the address book and returns the entity the component is on.
`Custom` returns `Entity.Null` when the Custom slot was never filled — a silent
no-op, not an error.

Where the enum appears (all default to `Target.Target` in code):
- **Conditions** — `ConditionAuthoring.Target`: *whose* number do I check?
- **Actions** — every Action*Authoring has a Target: *whom* do I affect?
  (`ActionIntrinsicAuthoring`/`ActionStatAuthoring` show it as the **Target**
  field, serialized `1`=Target, `2`=Owner …; `ActionCreateAuthoring.Target`
  decides which slot the *spawned* payload inherits as its Target.)

## The flip: "damage the enemy" → "damage myself"

This is the #1 designer slip and the headline use of this skill. A damage payload
is `ActionIntrinsicAuthoring` doing `CurrentHealth -30` with Target = **Target**.
Change that one dropdown to **Owner** and the same `-30` now hits the player
Essence instead of the contacted enemy. Nothing else changes; no error fires.

- Self-buff / lifesteal / recoil → set the action's Target to **Owner** (or
  **Self**) on purpose.
- "20% chance the *player* explodes" (Wiki 007) → the explosion's damage action
  targets Owner, not Target.
- A heal that should hit the player but is left on Target silently heals whatever
  you touched. Always read the Target dropdown when an effect lands on the wrong
  body.

Mirror for **conditions**: a reaction that should fire on the *enemy's* health
must check `CurrentHealth` with condition Target = **Target**; flip it to Owner
and it gates on the player's health instead.

## How the slots get filled — three mechanisms

A payload's address book is rarely set by hand at runtime; it is *inherited and
re-routed* at spawn. Three layers, in order:

### 1. Inheritance on spawn (ActionCreate)
When a reaction spawns a payload (`ActionCreateAuthoring` → an ObjectDefinition),
the new entity's `Targets` is copied from the spawner via `targets.Copy(...)`:
- **Source** = the spawner entity (the reaction that created it).
- **Owner**  = inherited unchanged from the spawner.
- **Target** = `targets.Get(create.Target, spawner)` — i.e. the spawner's
  `ActionCreate.Target` dropdown picks which of the spawner's slots becomes the
  child's Target. (Default Target → the child keeps aiming at what the parent
  was aiming at.)

So "who the projectile hits" is decided by the spawner *before* the payload even
exists. See `unity-augment-architecture` for the full spawn chain.

### 2. Initialize.Target — re-route the victim at instantiation
`TargetsAuthoring.Initialize.Target` (a `Target` enum, default `Target`) is the
designer-facing knob for "a buff should target the Owner, not whoever spawned
me." At spawn, `InitializeTargetsSystem` does, per object id:

```
targets.Target = targets.Get(Initialize.Target, thisEntity)   // re-point Target
if (result == Entity.Null) keep the previous Target            // safe fallback
```

So a payload baked with `Initialize.Target = Owner` rewrites its own Target slot
to its Owner the instant it's created — turning an inherited "hit the enemy"
payload into a "buff myself" payload, with a built-in guard: an unresolvable
choice leaves the old Target intact rather than nulling it. This only touches the
**Target** slot; Owner/Source/Custom are untouched.

### 3. Essence Link override — "act on whatever I touched"
The first two mechanisms route among *already-known* entities. The Essence Link
override answers a different question: when a trigger fires on contact, *who did
I even touch?* The contacted object isn't in any slot yet.

A trigger-spawn clip (`PhysicsTriggerInstantiateClip`) has a **Target Link
Override** set to **Essence Link**. On contact it walks the collided object's
root, resolves *its* Essence entity through the Essence Link schema, and assigns
that as the spawned payload's **Target**. That is how a falling weapon damages
"the enemy I landed on" without anyone wiring the enemy in advance.

- "Essence Link" is an `EntityLinkSchema` asset (vex-ee: `id: 8` under
  `Assets/Settings/Schemas/EntityLinks/Essence Link.asset`,
  `BovineLabs.Timeline.EntityLinks`). Confirm its presence/id in THIS project.
- For the resolution to work the contacted root must publish its Essence link
  (an `EntityLinkSource`) and the collision filter must let the trigger see it.
- Permanently re-pointing a payload's Target slot through a link *after* the fact
  is a sibling concern — see `unity-track-entitylink-targetpatch`. The full link
  family + Essence Link wiring lives in `unity-stage-foundations` and the
  `unity-track-entitylink-*` skills; don't re-derive it here.

## Designer reasoning checklist

When an effect lands on the wrong thing, walk the address book:
1. Which slot does the action's **Target** dropdown name? Wrong slot = the flip
   bug (most often Target↔Owner).
2. Was the payload **spawned**? Then its Target was inherited from the spawner's
   `ActionCreate.Target`, possibly re-routed by `Initialize.Target`. Check both.
3. Is the victim "whatever I touched"? Then the trigger clip needs **Essence
   Link** as its Target Link Override, and the touched root must publish an
   Essence link the trigger's collision filter can reach.
4. Is the condition reading the *same* slot the action writes? An on-hit reaction
   that checks Owner's health but damages Target (or vice versa) gates on the
   wrong body.
5. **Custom** unset resolves to null → a silent no-op, not an error.

## Cross-references (don't re-derive)
- Stats vs intrinsics that actions read/write → `unity-stats-intrinsics`.
- Reaction conditions/Active/cooldown semantics → `unity-reactions`.
- ObjectDefinition spawn + ActionCreate chain → `unity-object-definitions`,
  `unity-augment-architecture`.
- Essence event routing (routeTo/routeLink) → `unity-essence-actions` /
  `unity-track-essence-event`.
- The TRA payload's four components, end-to-end → `unity-tra-payloads`.
- Permanent link-based Target re-point → `unity-track-entitylink-targetpatch`.
