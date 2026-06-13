---
name: unity-object-definitions
description: "The designer's mental model for \"what gets spawned\" in BovineLabs Arvex gameplay: the ObjectDefinition asset (stable id + prefab + categories), the ObjectManagementSettings registry that lists them, and the two-way prefab↔ definition link via ObjectDefinitionAuthoring — plus the spawn pose set by InitializeTransformAuthoring (From / To = Owner/Source/Target). Covers the two silent-failure traps a designer hits constantly: duplicate / zero IDs and a broken back-link (right prefab, wrong or null Definition → wrong or no spawn). Use whenever a designer asks \"make X spawn a Y\", \"my payload doesn't appear\", \"the wrong thing spawned\", or \"register this prefab so a clip/Action can spawn it\". Portable to any project with the BovineLabs Core ObjectManagement + Reaction packages; worked example from vex-ee. Behave per unity-agent-protocol, use the editor per unity-cli; the spawn pose ties into TRA payloads / Targets — cross-reference unity-augment-architecture rather than re-deriving."
---

# unity-object-definitions — what gets spawned, and how it's posed

A designer never wires a raw prefab into a spawning clip or Action. In Arvex,
**every spawnable is named by an `ObjectDefinition`** — a small asset that gives
the prefab a stable numeric **id**. Clips and Actions spawn by that id; the
runtime looks the id up in a registry and instantiates the matching prefab. This
indirection is what makes spawns branch-safe and moddable — and it's the source
of the two most common "nothing happened" bugs.

Behave per `unity-agent-protocol` (explore first, capture `PRE|` state before any
mutation, evidence every claim, record an inverse for every change, never leave
broken state). Operate the editor per `unity-cli`. This is a gameplay *concept*,
not a Timeline track — there is no SubScene open/save bracket here; you edit
project assets and prefabs. The `Targets`/`Target` model this skill leans on is
owned by `unity-targets`; the spawned effect object that carries these
components is `unity-tra-payloads`; the whole Input→Event→Reaction→Action→spawn
chain is `unity-augment-architecture`. Cross-reference them; don't re-derive.

## The three pieces (and the link between them)

```
ObjectDefinition.asset            ObjectManagementSettings.asset (the registry)
  id: <stable int>      ◄───────── objectDefinitions: [ …, this def, … ]
  prefab: ──────────┐
                    │   ┌────────► Payload.prefab
                    └───┤            └─ ObjectDefinitionAuthoring.Definition ──┐
                        │                                                       │
                        └───────────── must point at the SAME .asset ◄─────────┘
```

1. **`ObjectDefinition` asset** — the name tag. Fields (verify against the live
   type; vex-ee homes it at `BovineLabs.Core.Authoring.ObjectManagement.ObjectDefinition`,
   external package `com.bovinelabs.core`):
   - `id` — **read-only in the inspector, auto-assigned** (see "How the id is
     assigned"). This is the value clips/Actions actually store.
   - `prefab` — the GameObject to instantiate.
   - `categories` — a bit field (`ObjectCategories`); each set bit bakes an extra
     marker component onto the spawned entity so queries/groups can find it. Most
     payloads leave this `0`.
   - `friendlyName` / `description` — cosmetic; `FriendlyName` falls back to the
     asset name.
   In vex-ee these live under `Assets/Settings/ObjectDefinitions/` (e.g.
   `Fireball.asset` id 4, `Player.asset` id 2, `Player After Image.asset` id 3,
   `TRA.asset` id 1). **Rediscover the folder and ids in THIS project — never
   assume them.**

2. **`ObjectManagementSettings` asset** — the registry / phone book. One per
   project (the importer errors on more than one). Its `objectDefinitions` array
   lists every definition; only listed definitions exist at runtime. In vex-ee:
   `Assets/Settings/Settings/ObjectManagementSettings.asset` with four entries.
   **You almost never edit this array by hand** — it is auto-populated (see
   below). The same asset also holds `objectGroups` (sets of definitions queried
   together) and bakes the category→component map.

3. **`ObjectDefinitionAuthoring`** — the back-link, a `[DisallowMultipleComponent]`
   MonoBehaviour on the **prefab**. Its single field `Definition` must point at
   the SAME `ObjectDefinition` asset whose `prefab` points back here. At bake it
   stamps the entity with the definition's `ObjectId` and adds the category
   components. This is the two-way link — and the #1 fragility.

## How the id is assigned (you don't type it)

An editor `AssetPostprocessor` (`ObjectManagementProcessor`) watches asset
imports and **keeps every `ObjectDefinition` id unique automatically**:

- A new/duplicated definition with `id == 0`, **or** an id that collides with
  another definition, is reassigned to **the first free positive integer** (1,
  2, 3, … — ids start at 1; `0` means "unset / Null").
- It also keeps the registry array in sync: `ObjectDefinition` carries an
  `[AutoRef("ObjectManagementSettings", "objectDefinitions", …)]` attribute, so
  on import the processor **finds every definition asset in the project and
  rewrites `ObjectManagementSettings.objectDefinitions` to contain exactly them**.

What this means for a designer (this is the same auto-register + auto-ID
mechanism `unity-gameplay-config` describes for all the Settings registries —
see it for the folder map and the other registries):
- **To register a new spawnable, you don't touch the registry** — create the
  `ObjectDefinition` asset (Unity will assign an id and add it to the list on the
  next import/refresh). Conversely, deleting the asset drops it from the list.
- The id you see is the truth a clip/Action will store. Treat it as read-only.
- After creating/duplicating assets, force an asset refresh and **read back the
  id and the registry membership** before claiming the spawnable exists (the
  processor runs on a delayed call, not instantly).

## How something spawns by it (the consumer side)

Spawners reference the **definition asset**, never the prefab — at bake the
reference becomes the `ObjectId` (the int), a *weak reference* resolved at
runtime through the registry buffer. Two common consumers (verify the exact type
in THIS project):

- **`PhysicsTriggerInstantiateClip`** (trigger-spawn chain): a Timeline clip with
  an `ObjectDefinition` slot + `Trigger State` (e.g. `Enter`) — spawns the
  payload when a physics trigger fires.
- **`ActionCreateAuthoring`** (reaction chain): each entry is a `Definition` +
  `Target` (default `Target`) + `DestroyOnDisabled` flag — spawns when the
  Reaction fires. (See `unity-essence-actions` for the Action family.)

So "make a dash leave a clone" decomposes to: a prefab → its `ObjectDefinition`
→ referenced by the clip/Action that the dash's Reaction triggers. The
definition is the contract between "what" (the prefab) and "when/where" (the
clip/Action). See `unity-augment-architecture` for assembling that chain.

## Spawn pose — `InitializeTransformAuthoring` (From / To)

Where and how the spawned entity is placed comes from
`InitializeTransformAuthoring` on the **payload prefab** (vex-ee: external
package `com.bovinelabs.reaction`,
`BovineLabs.Reaction.Authoring.Core.InitializeTransformAuthoring`). It
`[RequireComponent]`s `ObjectDefinitionAuthoring` + `LifeCycleAuthoring` +
`TargetsAuthoring`, so it lives right alongside the back-link.

It picks **two reference entities** from the spawn's `Targets` and derives the
pose from them:

- **`From`** (default `Owner`) and **`To`** (default `Target`) — each is a
  `Target` enum: `None / Target / Owner / Source / Self / Custom`. (Owner = whom
  the effect belongs to, usually the player Essence; Source = the spawner;
  Target = whom it landed on — see `unity-augment-architecture` for the full
  `Targets`/`Target` model.)

- **`Position`** = `None` / `From` / `To` — place the new entity at the From or
  To entity (default `From`). "Spawn the projectile at the player" = `Position:
  From`, `From: Owner`. "Spawn it on the thing I touched" = often `From` set so
  it resolves the contacted entity (via an `Essence Link` target override on the
  spawning clip).
- **`Rotation`** = `None` / `From` / `To` / `Direction` / `DirectionInverse` —
  `Direction` orients forward along **To − From** (using From's up);
  `DirectionInverse` along **From − To**. Use `Direction` for "fire it toward the
  target."
- **`Scale`** = `None` / `From` / `To` / `Distance` — `Distance` sets scale to
  the From→To distance (e.g. a beam/tether stretched between two points).
- **`ApplyInitialTransform`** (default true) — keep the prefab's own
  LocalTransform as a local-space offset on top of the chosen pose; false
  discards it. Use this for "spawn 1m in front" offsets.

Defaults (`Position: From`, `Rotation/Scale: None`, `From: Owner`, `To: Target`)
= "appear exactly at the owner, no rotation/scale change."

## The two traps (both fail SILENTLY)

These are the bugs a designer reports as "my spawn doesn't work" with no error.

**Trap 1 — broken back-link.** The prefab's `ObjectDefinitionAuthoring.Definition`
is null or points at the *wrong* definition. Symptoms: wrong thing spawns, or the
entity spawns without its expected identity/category components and so its
Reaction never matches. Independently, if a registered `ObjectDefinition`'s
`prefab` field is **null**, the spawn just doesn't happen (the rest of the
mechanic still looks intact). The vex-ee wikis call this the single most common
broken state. **Always verify the link in BOTH directions:** def.prefab → prefab,
and prefab's `ObjectDefinitionAuthoring.Definition` → the same def.

**Trap 2 — duplicate or zero IDs.** Two definitions sharing an id, or an id left
at `0`, means the registry lookup resolves to the wrong prefab or to nothing. The
importer normally auto-heals this, but it bites when: assets are added outside
Unity (git merge, scripted copy) and not yet reimported; or you duplicated by
copying the `.asset` file rather than via Unity. The registry's
`ObjectDefinitionMap` additionally errors **"Non-unique object definitions"** if
two definitions point at the *same prefab* — to legitimately reuse a prefab, make
a **prefab Variant/instance**, not a second definition on the identical prefab.

**Diagnosis order** (smallest fix first):
1. Is the definition in `ObjectManagementSettings.objectDefinitions`? (If not,
   refresh assets so AutoRef re-adds it.)
2. Does the definition's `id` read as a unique positive integer?
3. Does the definition's `prefab` field point at a real prefab (not null)?
4. Does that prefab carry exactly one `ObjectDefinitionAuthoring` whose
   `Definition` points back at the same asset?
5. Only then look at the spawning clip/Action and the payload's
   `InitializeTransformAuthoring` / `Targets`.

## Verifying with unity-cli (read-back is evidence)

Discover, never assume — paths, ids, and folder names are vex-ee worked examples;
find the real ones in THIS project. Useful moves:

- Find the registry and definitions: search the project for the
  `ObjectManagementSettings` asset and the `ObjectDefinitions` folder; read each
  `.asset`'s `id` and `prefab` fields.
- Confirm the back-link: open the candidate prefab and read its
  `ObjectDefinitionAuthoring.Definition` reference; confirm it equals the def
  whose `prefab` points at this prefab.
- After any edit, trigger an asset refresh and **re-read** the id and registry
  membership (the auto-id/AutoRef processor runs on a delayed editor call) before
  claiming success.

## Undo

Mutations here are asset/prefab edits, so the inverse is the captured prior value
(per `unity-agent-protocol` — capture `PRE|` first):
- Created a definition → delete the asset (the importer removes it from the
  registry on refresh).
- Re-pointed `ObjectDefinitionAuthoring.Definition` or a def's `prefab` → restore
  the previously-recorded reference.
- Never hand-edit the auto-assigned `id` as an "undo"; restore by restoring the
  asset, and let the importer reconcile ids/registry.
