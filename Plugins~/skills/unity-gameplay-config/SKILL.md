---
name: unity-gameplay-config
description: "Designer's map of WHERE core gameplay lives in a BovineLabs \"Arvex\" project and how to add to it: the Assets/Settings tree (Schemas + ObjectDefinitions), the three auto-maintained registries (EssenceSettings = stats+intrinsics, ReactionSettings = events, ObjectManagementSettings = objdefs), and the exact moves to add a new stat / intrinsic / event / object definition and have it wired in. Explains the auto-register + auto-ID mechanism (you create the asset in the right folder, the editor assigns the key and adds it to the registry — never hand-edit the array), the duplicate/wrong-key traps, and the two-StaggerMeter trap. Use when a designer asks \"where do I add a new number/counter/event/spawnable?\" or \"why isn't my new stat showing up in the dropdown?\" Portable to any project with the BovineLabs Core/Reaction/Essence/ObjectManagement packages — the folders and IDs here are a worked example from vex-ee; rediscover them in THIS project. Cross-references unity-stats-intrinsics, unity-reactions, unity-object-definitions, unity-targets, unity-essence-actions, unity-tra-payloads, unity-augment-architecture for the details of USING each thing once it exists."
---

# unity-gameplay-config — where gameplay lives, and how to add to it

Behave per `unity-agent-protocol`; use the editor per `unity-cli`. This skill is
**not a Timeline track**. It is the designer's answer to one recurring question:
*"I want a new number / counter / event / spawnable thing — where does it go, and
how does the game find it?"* For how to actually USE each piece once it exists, see
the sibling skills (cross-referenced below); this skill owns only the **filing
system and registration rules**.

## The one-sentence model

Gameplay is **data, not code**. A stat, an intrinsic, an event, an object
definition are each a small ScriptableObject `.asset` file living in a known
folder. Three **registry** assets list which ones the game loads. A designer's job
is to **create the asset in the right folder** — the editor then **assigns its
unique key and adds it to the registry for you**. You almost never touch the
registry by hand.

## The Assets/Settings tree (worked example — rediscover the paths)

The folders below are vex-ee's layout. They are **not hardcoded**: the project maps
each kind of asset to a folder in an editor settings asset (vex-ee:
`Assets/Settings/Settings/EditorSettings.asset`, a `BovineLabs.Core.Editor.Settings.EditorSettings`,
in its `paths` list of `Key → Path`). Read that `paths` list FIRST to learn where
THIS project keeps each kind. The keys are stable (`bl.*`); the paths are not.

| Designer wants…                  | Asset type (`m_EditorClassIdentifier`) | EditorSettings key            | vex-ee path |
|----------------------------------|----------------------------------------|-------------------------------|-------------|
| a **stat** (scaling number)      | `StatSchemaObject` (Essence)           | `bl.ar.statschemaobject`      | `Assets/Settings/Schemas/Stats/` |
| an **intrinsic** (live counter)  | `IntrinsicSchemaObject` (Essence)      | `bl.ar.intrinsicschemaobject` | `Assets/Settings/Schemas/Intrinsics/` |
| an **event** (transient signal)  | `ConditionEventObject` (Reaction)      | `bl.ar.conditioneventobject`  | `Assets/Settings/Schemas/Events/` |
| a **spawnable** (object def)     | `ObjectDefinition` (Core)              | `bl.ar.objectdefinition`      | `Assets/Settings/ObjectDefinitions/` |
| an **entity link** schema        | `EntityLinkSchema` (Timeline.EntityLinks) | `bl.ar.entitylinkschema`   | `Assets/Settings/Schemas/EntityLinks/` |

Stat vs intrinsic in one line (full treatment in `unity-stats-intrinsics`): a
**stat** is a modifiable/scaling number with no live runtime value of its own
(`MovementSpeed`, `Max Health`, `SlowMo`); an **intrinsic** is a live runtime
counter with a default and a range (`CurrentHealth`, `ActiveClones`, `DashCharges`).

## The three registries (worked example — `Assets/Settings/Settings/`)

Each registry is a ScriptableObject holding flat array(s) of references to the
schema/objdef assets. They live in the folder mapped by key `bl.settings`
(vex-ee: `Assets/Settings/Settings/`). Rediscover that path; don't assume it.

| Registry asset            | Type (assembly)                                     | Field(s) it lists |
|---------------------------|-----------------------------------------------------|-------------------|
| `EssenceSettings`         | `BovineLabs.Essence.Authoring.EssenceSettings`      | `statSchemas[]` **and** `intrinsicSchemas[]` |
| `ReactionSettings`        | `BovineLabs.Reaction.Authoring.Core.ReactionSettings` | `conditionEvents[]` |
| `ObjectManagementSettings`| `BovineLabs.Core.Authoring.ObjectManagement.ObjectManagementSettings` | `objectDefinitions[]` (+ `objectGroups[]`) |

So: **stats and intrinsics share ONE registry (`EssenceSettings`), in two separate
arrays.** Events live in `ReactionSettings`. Object definitions live in
`ObjectManagementSettings`. Each array entry is just `{fileID, guid}` pointing at
the asset file — there is no inline copy of the data.

## The mechanism that makes "just create the file" work

Every one of these asset types carries an `[AutoRef(...)]` attribute naming its
manager + field (e.g. `StatSchemaObject` → `("EssenceSettings","statSchemas",…)`;
`ConditionEventObject` → `("ReactionSettings","conditionEvents",…)`;
`ObjectDefinition` → `("ObjectManagementSettings","objectDefinitions",…)`), and
implements `IUID` (a unique integer key the **inspector shows read-only** — you do
not type it).

An editor asset post-processor (`ObjectManagementProcessor`) reacts to any
import/create of these types and does two things automatically:

1. **Auto-ID**: assigns a unique key, branch-merge-safe (it scans existing keys and
   picks an unused one) so two designers on two branches never collide. The key
   field is read-only in the inspector precisely because the editor owns it.
2. **Auto-register**: rebuilds the manager's array to contain **every asset of that
   type found in the project** (`UpdateAutoRefDirect` sets `arraySize` to the count
   and fills it). This is why the registry "just knows" about your new asset — and
   why **hand-editing the registry array is pointless and dangerous**: the
   post-processor overwrites it on the next import.

### How a designer actually adds one (two equivalent paths)

- **From the registry inspector** (intended path): select the manager asset
  (`EssenceSettings` / `ReactionSettings` / `ObjectManagementSettings`), find the
  array (`statSchemas` / `intrinsicSchemas` / `conditionEvents` /
  `objectDefinitions`), press its **`+`**. The `AssetCreator` UI creates the `.asset`
  in the correct folder (from the `[AutoRef]` path / EditorSettings key), names it,
  assigns the key, and registers it — one gesture.
- **By creating the asset in the mapped folder** (or via the create menu for that
  type). Drop a new `StatSchemaObject`/etc. into the folder the EditorSettings key
  points at; the post-processor assigns the key and adds it to the registry on
  import. (For object definitions there is also a **"Create Definitions from
  Assets"** tool menu that makes the objdef from a selected prefab AND adds the
  back-pointing `ObjectDefinitionAuthoring` to the prefab in one step — see below.)

Either way, the designer's only real inputs are **name** and the type-specific
fields. Via `unity-cli`, do it through the editor (create-asset + import) rather
than writing the registry YAML; let the post-processor own keys and the array.

## Type-specific fields a designer fills in

(Discover exact fields live; these are the verified vex-ee shapes. Details of
behavior live in the sibling skills — this is just what the create form asks for.)

- **Stat** (`StatSchemaObject`): just a name + `isGlobal` flag. The `key` is
  auto-assigned (worked example: `MovementSpeed` key 113, `Max Health` key 1).
  Stats are stored ×100 fixed-point at runtime — see `unity-stats-intrinsics`.
- **Intrinsic** (`IntrinsicSchemaObject`): name, `defaultValue`, `range` (min/max
  `Vector2Int`), optional `minStat`/`maxStat` (a stat that caps it, e.g.
  `CurrentHealth` clamped by a `Max Health` stat). `key` auto-assigned.
- **Event** (`ConditionEventObject`): name, optional `customDataType` (a payload
  type). `key` auto-assigned (worked example: `OnDashInitiated` key 26).
- **Object definition** (`ObjectDefinition`): `friendlyName`, `description`,
  `categories`, and a **`prefab`** reference. `id` auto-assigned (worked example:
  `TRA` id 1).

### The objdef two-way link (the #1 spawn fragility)

An `ObjectDefinition` points at a `prefab`; that prefab MUST carry an
`ObjectDefinitionAuthoring` whose `Definition` points **back at the same objdef
asset**. The "Create Definitions from Assets" menu wires both sides for you; if you
create the objdef by hand, you must add the authoring component to the prefab
yourself. Broken/missing back-pointer, or two objdefs sharing one id, → silent
wrong/null spawn. Full treatment in `unity-object-definitions`.

## Why these schemas double as Reaction conditions

Stats, intrinsics, and events all derive from the same `ConditionSchemaObject`
base, tagged by a `conditionType` byte resolved against the project's
`ConditionTypes` table (vex-ee: `event=0`, `stat=1`, `intrinsic=2`). That shared
base is exactly why a Reaction can test "intrinsic `CurrentHealth > 0`" or "event
`OnDashInitiated` present" using the same condition machinery — see
`unity-reactions`. Creating the schema here is what makes it selectable there.

## Traps (registry / ID specific — see Avoid Errors + Learned Feature List)

- **Don't hand-edit the registry array.** The auto-ref post-processor rebuilds it
  from "every asset of this type in the project" on the next import; your manual
  add/remove/reorder is discarded. Add/remove by creating/deleting the **asset**.
- **Two `StaggerMeter` schemas exist** — one `StatSchemaObject` (Stats folder) and
  one `IntrinsicSchemaObject` (Intrinsics folder). They have different keys in
  different registries; an Action or Reaction wired to the wrong one **silently
  does nothing**. Same display name ≠ same schema. Confirm which folder/type the
  rest of the mechanic uses before adding or referencing it.
- **Never type a key.** Keys are `[InspectorReadOnly]` and editor-owned. A
  hand-set or duplicated id collides another asset → wrong lookups. If a key looks
  wrong, re-import the asset and let the processor reassign, rather than editing
  YAML.
- **Folder placement matters.** An asset created outside the mapped folder may not
  be picked up where the designer expects; the create-from-registry `+` button
  guarantees the right folder. If a new stat "doesn't show in the dropdown", check
  (a) it's the right type, (b) it's under the mapped folder, (c) the project
  re-imported (the post-processor runs on import, debounced/delayed).
- **YAML-edit caution** (per Avoid Errors): treat `.asset` registry files as
  high-risk for hand edits. Prefer editor APIs via `unity-cli`; if you must text-
  edit, make the smallest change, then `unity-cli reserialize <path>` and
  `refresh_unity --compile request`, and check `console --type error`.

## Discovery preamble (do this, don't assume)

1. Read the project's editor settings `paths` map to learn the real folder for each
   `bl.*` key (stats/intrinsics/events/objdefs/entitylinks + the `bl.settings`
   registry folder). Names here are vex-ee examples.
2. Open the three registry assets; confirm the field names (`statSchemas`,
   `intrinsicSchemas`, `conditionEvents`, `objectDefinitions`) and that the asset
   you care about is (or isn't) listed.
3. To check whether a name already exists, search the mapped folder for the asset —
   and remember the StaggerMeter duplicate-name trap.
4. Add via the registry `+` button (or create-asset in the mapped folder) so the
   processor assigns the key and registers it; verify by re-reading the registry.
5. For object definitions, verify the prefab's `ObjectDefinitionAuthoring.Definition`
   points back at the new asset.

## Where to go next

- Using a stat/intrinsic (×100, ranges, caps, modify types) → `unity-stats-intrinsics`.
- Reactions/conditions that read these schemas → `unity-reactions`.
- Spawning via an objdef + the two-way prefab link → `unity-object-definitions`.
- Who an effect lands on (Owner/Source/Target/Self/Custom) → `unity-targets`.
- Actions that mutate stats/intrinsics/spawn → `unity-essence-actions`.
- The full TRA payload prefab (Targets+Reaction+Action+ObjectDefinition) →
  `unity-tra-payloads`.
- Composing all of the above into one mechanic → `unity-augment-architecture`.
