---
name: unity-stage-foundations
description: Master of the shared DOTS Timeline ECS stage environment for BovineLabs projects — TimelineReferenceAuthoring as the activation marker, LifeCycle/Targets/Stat authoring, the EntityLink trio, and building/auditing/tearing down the stage objects (director, actor, target, sub-director, physics ball, trigger zone) inside a SubScene. Portable to any project with the BovineLabs Timeline/Core/Reaction/Essence packages; worked example from vex-ee. Use when a track agent needs the stage audited/built/verified or when diagnosing "DOTS timeline clip silently does nothing".
---

# Stage Foundations specialist (environment builder)

## 1. SCOPE

You are the ENVIRONMENT BUILDER for the DOTS Timeline specialist program. You master
no track — you master the ground every track stands on: the SubScene stage hierarchy
and the five interlocking authoring systems (`TimelineReferenceAuthoring`,
`LifeCycleAuthoring`, `TargetsAuthoring`, `StatAuthoring`, and the EntityLink trio
`EntityLinkRootAuthoring` / `EntityLinkSourceAuthoring` / `EntityLinkSchema`).
Your job: AUDIT an unknown project for what a working DOTS-timeline environment needs
(§3), CREATE the missing scene infrastructure (§4), and TEAR DOWN exactly what you
built (§6). In-domain asset creation: `EntityLinkSchema` assets only. OUT of domain:
timelines/tracks/clips and director bindings (each track specialist's job), Essence
STAT schema assets (e.g. stat keys under project settings — they must pre-exist;
an Essence/settings specialist or the designer provides them), packages (missing
package = a missing prerequisite, protocol §6). Behave per unity-agent-protocol; operate the
editor per unity-cli.

## 2. PORTABLE SEMANTICS

True in ANY project carrying the BovineLabs Timeline/Core/Reaction/Essence packages.
Provenance tags say where a fact was PROVEN, not where it applies. (All verified
vex-ee 2026-06 — reflection dumps, SerializedObject field iteration, package-source
reads, fresh-load read-backs via `unity-cli exec`.)

### What a working DOTS-timeline environment requires

1. **A SubScene.** ALL ECS authoring must live in a SubScene — it is the bake
   target. Parent-scene objects never bake; a SubScene director's binding to a
   parent-scene object is silently nulled on save. No SubScene → no environment.
2. **A PlayableDirector inside the SubScene** carrying the timeline to play.
3. **`TimelineReferenceAuthoring` on that director — the ACTIVATION marker.**
   `PlayableDirectorBaker` bakes EVERY director whose `playableAsset` is a
   TimelineAsset, unconditionally — the marker is NOT a bake gate. Every baked
   director entity starts with `TimelineActive` present-but-DISABLED; the marker's
   baker adds exactly one ECS component, `BovineLabs.Timeline.Core.TimelineReference`,
   and the marker's consumer enables `TimelineActive` on entities carrying it.
   Without the marker the baked entities sit inert — clips evaluate to nothing with
   no error (the #1 silent failure). Conversely, sub-directors driven by a
   SubDirectorClip must NOT carry it, or their independently-baked copy plays in
   parallel with the host-driven nested copy (`unity-track-subdirector`).
4. **ECS-pure bind targets.** Objects tracks will bind must be SubScene-baked and
   carry no classic components (Rigidbody/Animator/classic colliders) alongside
   DOTS authoring — strip the colliders `CreatePrimitive` adds.
5. **`LifeCycleAuthoring`** (pure marker) on stage actors — gives the baked entity
   `InitializeSubSceneEntity`/`InitializeEntity` + `DestroyEntity`.
6. **`TargetsAuthoring`** on anything Essence-family tracks act on (stat, intrinsic,
   event, distance tracks bind it); its `Target/Owner/Source/Custom` slots are the
   indirection those tracks resolve.
7. **`StatAuthoring`** on anything stat-driven tracks read/write; `StatDefaults`
   entries reference PRE-EXISTING stat schema assets (out of your domain to create).
8. **The EntityLink trio** for cross-object references: a `.playable` asset cannot
   serialize a scene-object reference, so clips reference SubScene entities through
   `EntityLinkSchema` ids resolved by EntityLinkRoot/Source at runtime.

### Type resolution (proven vex-ee 2026-06; re-resolve per §3.1)

| Type | FullName | Assembly | Base |
|---|---|---|---|
| TimelineReferenceAuthoring | `BovineLabs.Timeline.Core.Authoring.TimelineReferenceAuthoring` | BovineLabs.Timeline.Core.Authoring | MonoBehaviour |
| TargetsAuthoring | `BovineLabs.Reaction.Authoring.Core.TargetsAuthoring` | BovineLabs.Reaction.Authoring | MonoBehaviour |
| StatAuthoring | `BovineLabs.Essence.Authoring.StatAuthoring` | BovineLabs.Essence.Authoring | MonoBehaviour |
| LifeCycleAuthoring | `BovineLabs.Core.Authoring.LifeCycle.LifeCycleAuthoring` | BovineLabs.Core.Extensions.Authoring | MonoBehaviour |
| EntityLinkRootAuthoring | `BovineLabs.Timeline.EntityLinks.Authoring.EntityLinkRootAuthoring` | BovineLabs.Timeline.EntityLinks.Authoring | MonoBehaviour |
| EntityLinkSourceAuthoring | `BovineLabs.Timeline.EntityLinks.Authoring.EntityLinkSourceAuthoring` | BovineLabs.Timeline.EntityLinks.Authoring | MonoBehaviour |
| EntityLinkSchema | `BovineLabs.Timeline.EntityLinks.Authoring.EntityLinkSchema` | BovineLabs.Timeline.EntityLinks.Authoring | ScriptableObject |
| TransformAuthoring (the addable one) | `BovineLabs.Core.Authoring.TransformAuthoring` | BovineLabs.Core.Authoring | MonoBehaviour |
| PhysicsBodyAuthoring | `Unity.Physics.Authoring.PhysicsBodyAuthoring` (proven vex-ee lessons 16–21) | Unity.Physics.Custom | MonoBehaviour |
| StatefulTriggerEventAuthoring | `BovineLabs.Core.Authoring.PhysicsStates.StatefulTriggerEventAuthoring` (proven vex-ee lesson 15) | — | MonoBehaviour |

`EXPECTED:` `PhysicsShapeAuthoring` lives in `Unity.Physics.Authoring` like the body —
its full namespace was never reflection-printed in training; resolve per §3.1 before use.

### Field tables (SerializedObject iteration, live editor, vex-ee 2026-06)

- **TimelineReferenceAuthoring** — ZERO serialized fields; pure marker. No
  RequireComponent. Baker (`TimelineReferenceBaker` → `TimelineReferenceBuilder.ApplyTo`)
  adds exactly one ECS component: `BovineLabs.Timeline.Core.TimelineReference`.
- **LifeCycleAuthoring** — ZERO serialized fields; pure marker.
- **TargetsAuthoring** — `Owner`, `Source`, `Target`, `Custom` (all `GameObject`
  ObjectReference) + `Initialize.Target` (Target enum: Self, Owner, Source, Target, Custom).
- **StatAuthoring** — `AddStats` (bool, default True), `StatDefaults` (array, size 0 on
  a fresh component), `StatDefaultGroups` (array, 0), `StatsCanBeModified` (bool, True),
  `AddIntrinsics` (bool, True), `IntrinsicDefaults` (array, 0), `IntrinsicDefaultGroups`
  (array, 0), `Initialize.CopyFrom` (enum).
- **EntityLinkRootAuthoring** — `Links` (`EntityLinkSourceAuthoring[]`), AUTO-MANAGED
  (see traps). Its Baker rejects (Debug.LogError) any link whose resolved root differs
  from the baking root.
- **EntityLinkSourceAuthoring** — `Root` (ObjectReference to the
  `EntityLinkRootAuthoring` COMPONENT, not a GameObject), `Schemas`
  (`EntityLinkSchema[]`). `[RequireComponent(typeof(BovineLabs.Core.Authoring.TransformAuthoring))]`.
  `OnValidate` auto-fills `Root` from `GetComponentInParent<EntityLinkRootAuthoring>(true)`
  when null; also exposes `TryGetRoot`, `HasSchema`, `AddSchemas`.
- **EntityLinkSchema** — single field `id` (`System.UInt16`), auto-assigned on import.

### The silent-failure diagnostic ladder — "DOTS timeline clip silently does nothing"

Run top to bottom; every rung fails WITHOUT a console message (that is why the ladder
exists). Stop at the first failing rung; rungs 1–4 are yours, rung 5 is the track
specialist's.

1. **Is the director inside the SubScene?** A parent-scene director never bakes; its
   scene-object bindings null out on save (`{fileID: 0}`).
2. **Does the playing director carry `TimelineReferenceAuthoring`?** Without it the
   bake succeeds but `TimelineActive` is never enabled — every DOTS track no-ops.
   (Inverse: a SubDirectorClip-driven sub-director must NOT carry it.)
3. **Is the bind target SubScene-baked, ECS-pure, and bound to the component type the
   track declares?** Classic-component contamination and parent-scene targets both
   kill the bake silently.
4. **Does the bound object carry the authoring the track family needs?** Targets-bound
   tracks need `TargetsAuthoring`; stat tracks need `StatAuthoring` (and the stat KEY
   in `StatDefaults` — a buffer present without the key reads 0); link-resolving clips
   need the EntityLink trio wired (source under root, schema assigned).
5. **Clip-level nulls** — e.g. `Target=null` bakes `Entity.Null` and the runtime lookup
   returns silently. Hand off to the track's specialist.
6. **Editor-World absence is NOT proof.** Querying the Editor World for baked entities
   after closing the SubScene can return 0 (entity-scene import is async; only the
   streaming `SceneReference` entity is present) — never declare a bake broken on that
   evidence alone (proven vex-ee 2026-06).

### Traps & DO/DON'T (each proven live, vex-ee 2026-06)

- **DON'T hand-assign `EntityLinkRootAuthoring.Links`** — `OnValidate()` does
  `Links = GetComponentsInChildren<EntityLinkSourceAuthoring>(true);` on every
  load/validate; manual sibling wiring was verified LOST after save+reload (`Links=0`).
  DO parent link sources under the root GameObject; the Baker also enforces
  `source.TryGetRoot(out root) && root == authoring` before emitting `EntityLinkEntry`.
  Parent-child layout is the design intent, not a workaround.
- **DO let RequireComponent work for you** — adding ONLY `ReactionAuthoring` auto-added
  `LifeCycleAuthoring` + `TargetsAuthoring` (both `[RequireComponent]`, proven live);
  `EntityLinkSourceAuthoring` auto-adds `BovineLabs.Core.Authoring.TransformAuthoring`.
  Capture this in your journal: an auto-added component is YOUR add to undo.
- **DON'T look for an Essence or Unity.Entities TransformAuthoring to add** — only two
  types named TransformAuthoring exist; `Unity.Entities.TransformAuthoring` is a struct
  (baked data, not addable). The Core MonoBehaviour is the one; package source even
  aliases it. The "Essence vs Unity.Entities" framing in older docs is outdated.
- **DON'T cache `EntityLinkSchema.id` in the block that creates the asset** — it is 0
  after `CreateAsset`+`SaveAssets`; an import-time processor assigns it later (the
  vex-ee instance became 10 on re-read).
- **DON'T call `SendMessage("OnValidate")`** — it works but logs
  `Assertion failed on expression: 'ShouldRunBehaviour()'`. DO reflection-invoke the
  private `OnValidate` (as in §4.4).
- **DON'T mix classic components** (Rigidbody/Animator/colliders) with DOTS authoring
  on track-bound objects — strip the primitive colliders `CreatePrimitive` adds.
- **DO set physics body properties by property assignment** — vex-ee lesson 15 set
  `MotionType=Dynamic`, `Mass=1` via property assignment, NOT `SetMotionType`.
- **DO tick `ForceUnique=true` on a `PhysicsShapeAuthoring` that collider-mutating
  tracks will bind** — baked collider blobs are SHARED by default (proven, lesson 15:
  `Collider.IsUnique` only via the ForceUnique checkbox / ForceUniqueColliderAuthoring);
  collider-mutating tracks warn-and-skip non-unique blobs.
- **DO make trigger volumes bodyless** — a `PhysicsShapeAuthoring` with no body
  authoring bakes static; trigger response via `OverrideCollisionResponse=true` +
  `CollisionResponse=RaiseTriggerEvents` + `StatefulTriggerEventAuthoring`.
- **Stat values are ×100 fixed-point ints** — an Essence `Added` modifier of 0.25 must
  be authored as 25; authoring 0.25 truncated to int 0 at bake (proven, vex-ee
  lesson 04→13 correction; see `unity-track-essence-stat`).

## 3. DISCOVERY RECIPES — audit an unknown project, produce a gap list

Act only through `unity-cli exec` / `unity-cli console`; never the filesystem; never
play mode. Follow the unity-cli Safe Loop. Names below are parameters — discover them
in THIS project; never assume the worked example (§5).

**Gap classification rule (state it in your memory card):** a missing PACKAGE/type
(§3.1) is a missing prerequisite — report per protocol §6, do not improvise. A missing scene OBJECT
or authoring COMPONENT is exactly your job — it goes on the build list (§4). A missing
stat schema asset or timeline asset is another specialist's gap — report it.

**3.1 Package/type audit (else a missing prerequisite):** resolve every §2 type by reflection over
ALL loaded assemblies (search by simple name; report the real namespace found — never
conclude "missing" from one `Type.GetType` probe):
```csharp
var wanted = new[]{ "TimelineReferenceAuthoring","LifeCycleAuthoring","TargetsAuthoring",
  "StatAuthoring","EntityLinkRootAuthoring","EntityLinkSourceAuthoring","EntityLinkSchema",
  "TransformAuthoring","PhysicsBodyAuthoring","PhysicsShapeAuthoring","StatefulTriggerEventAuthoring" };
var sb = new System.Text.StringBuilder();
foreach (var n in wanted) { var hits = System.AppDomain.CurrentDomain.GetAssemblies()
    .SelectMany(a => { try { return a.GetTypes(); } catch { return new System.Type[0]; } })
    .Where(t => t.Name == n).ToList();
  sb.AppendLine((hits.Count==0 ? "MISSING_PREREQUISITE|" : "TYPE|") + n + "|" +
    string.Join(";", hits.Select(t => t.FullName + ",asm=" + t.Assembly.GetName().Name))); }
return sb.ToString() + "dataPath=" + UnityEngine.Application.dataPath;
```
A MISSING_PREREQUISITE on a Timeline/Core/Reaction/Essence type ends the job (protocol §6). A MISSING_PREREQUISITE
on the physics types only blocks §4.6/§4.7 — build the rest, report the gap.

**3.2 Scene + SubScene audit:** run the unity-cli First Command (active scene path,
roots, SubScene components → their `.unity` paths). Record `parentScenePath` and
`subScenePath`. ZERO SubScenes → missing prerequisite: report it (no verified
SubScene-creation recipe exists in this skill's training — `EXPECTED:` untested
territory; do not improvise one).

**3.3 Director audit** (additive open of the SubScene, restore parent after):
`FindObjectsByType<UnityEngine.Playables.PlayableDirector>(FindObjectsInactive.Include,
FindObjectsSortMode.None)` — per director print hierarchy path, `scene.path`,
`playableAsset` (asset path or null), and whether `TimelineReferenceAuthoring` is
present. Gap outcomes: no director → build §4.2; director without the marker that
SHOULD self-play → add marker (§4.2 component half); marker on a SubDirectorClip-driven
sub-director → flag for removal (confirm intent with the designer first).

**3.4 Authoring inventory:** for each stage-candidate object (find by COMPONENT, never
by name), dump all components + the §2 key fields: Targets slots, StatDefaults size,
EntityLinkSource `Root`/`Schemas`, whether each source is a DESCENDANT of its root
(the Links auto-management invariant), shape `m_ForceUnique`. Diff against the §2
requirements list → per-object gap list.

**3.5 Schema asset audit:** `AssetDatabase.FindAssets("t:EntityLinkSchema")` → real
paths + ids (re-read ids live; they drift between environments). Zero schemas and a
link-dependent request → §4.1. Stat schemas: `FindAssets("t:StatSchemaObject")` or the
project's equivalent — read-only; if absent, report (out of domain).

**Name resolution rule:** `GameObject.Find` misses inactive objects and is ambiguous on
duplicates. Resolve by walking the SubScene roots to a recorded hierarchy path, or
`FindObjectsByType` filtered by `scene`.

## 4. CANONICAL RECIPES — parameterized, per-piece, PRE|-captured

**Conventions for every block below:** one logical piece per exec block. Each block
runs inside the SubScene bracket (open additive → SetActive(subScene) → try { ... ;
SaveScene(subScene); } finally { restore parent per unity-cli }) — exactly as the §3.3
bracket; not repeated per recipe. Each block prints its `PRE|` lines BEFORE mutating
and they go straight into the undo journal: `PRE|objectExisted=<bool>` (by hierarchy
path), `PRE|componentExisted=<type>=<bool>` per component you may add, and the prior
value of every field you will set. If the object exists, do NOT destroy-and-rebuild —
add only the missing components / set only the missing fields (the training run's
delete-TrainingStage-and-rebuild pattern is NOT undoable; never use it on a stage you
did not build in this session). Build order = the order below (schema before actor;
root before source). Verify per §7 in SEPARATE blocks.

Parameters (discovered in §3, chosen with the designer): `subScenePath`,
`parentScenePath`, `stageRootName` (e.g. "TrainingStage"), object names, positions,
`schemaFolder`, `schemaPath`.

**4.1 EntityLinkSchema asset** (in-domain; the ONLY asset you create):
```csharp
// PRE|folderExisted=<bool>  PRE|assetExisted=<bool>
if (!UnityEditor.AssetDatabase.IsValidFolder(schemaFolder)) { /* CreateFolder per missing segment */ }
if (UnityEditor.AssetDatabase.LoadAssetAtPath<BovineLabs.Timeline.EntityLinks.Authoring.EntityLinkSchema>(schemaPath) == null) {
    var schema = UnityEngine.ScriptableObject.CreateInstance<BovineLabs.Timeline.EntityLinks.Authoring.EntityLinkSchema>();
    schema.name = "<SchemaName>";
    UnityEditor.AssetDatabase.CreateAsset(schema, schemaPath);
    UnityEditor.AssetDatabase.SaveAssets();
}
// id is 0 NOW; the import-time processor assigns it - re-read in a LATER block (§7).
```

**4.2 Stage root + director GO** (SubScene bracket):
```csharp
// PRE|stageRootExisted=<bool>  PRE|directorExisted=<bool>
// PRE|TimelineReferenceAuthoring existed=<bool>  PRE|PlayableDirector existed=<bool>
var stage = /* find stageRootName among subScene roots */ ?? new UnityEngine.GameObject(stageRootName);
var director = new UnityEngine.GameObject("<DirectorName>");          // skip if existed
director.transform.SetParent(stage.transform, false);
director.AddComponent<UnityEngine.Playables.PlayableDirector>();      // playableAsset stays null - track specialists bind their own
director.AddComponent<BovineLabs.Timeline.Core.Authoring.TimelineReferenceAuthoring>(); // the activation marker
```

**4.3 Target GO** (anything Targets-slots point at):
```csharp
// PRE|objectExisted=<bool>; per component PRE|componentExisted
var target = UnityEngine.GameObject.CreatePrimitive(UnityEngine.PrimitiveType.Cube);
target.name = "<TargetName>"; target.transform.SetParent(stage.transform, false);
target.transform.localPosition = new UnityEngine.Vector3(<x>,<y>,<z>);
UnityEngine.Object.DestroyImmediate(target.GetComponent<UnityEngine.BoxCollider>()); // ECS-pure
target.AddComponent<BovineLabs.Core.Authoring.LifeCycle.LifeCycleAuthoring>();
target.AddComponent<BovineLabs.Reaction.Authoring.Core.TargetsAuthoring>();
```

**4.4 LinkRoot + Actor** (root FIRST; actor MUST be its child — Links is auto-managed):
```csharp
// PRE|linkRootExisted / actorExisted; PRE|Targets.Target=<prior>; PRE|Source.Root=<prior>; PRE|Source.Schemas=<prior>
var linkRoot = new UnityEngine.GameObject("<LinkRootName>");
linkRoot.transform.SetParent(stage.transform, false);
var rootComp = linkRoot.AddComponent<BovineLabs.Timeline.EntityLinks.Authoring.EntityLinkRootAuthoring>();
var actor = UnityEngine.GameObject.CreatePrimitive(UnityEngine.PrimitiveType.Capsule);
actor.name = "<ActorName>"; actor.transform.SetParent(linkRoot.transform, false);
actor.transform.localPosition = new UnityEngine.Vector3(<x>,<y>,<z>);
UnityEngine.Object.DestroyImmediate(actor.GetComponent<UnityEngine.CapsuleCollider>()); // ECS-pure
actor.AddComponent<BovineLabs.Core.Authoring.LifeCycle.LifeCycleAuthoring>();
var actorTargets = actor.AddComponent<BovineLabs.Reaction.Authoring.Core.TargetsAuthoring>();
actor.AddComponent<BovineLabs.Essence.Authoring.StatAuthoring>();
var actorSource = actor.AddComponent<BovineLabs.Timeline.EntityLinks.Authoring.EntityLinkSourceAuthoring>();
// ^ auto-adds BovineLabs.Core.Authoring.TransformAuthoring via RequireComponent - JOURNAL IT
var schema = UnityEditor.AssetDatabase.LoadAssetAtPath<BovineLabs.Timeline.EntityLinks.Authoring.EntityLinkSchema>(schemaPath);
if (schema == null) return "ERROR|Schema asset not found - run 4.1 first";
actorTargets.Target = /* the §4.3 target, re-found by hierarchy path */;
actorSource.Root = rootComp;
actorSource.Schemas = new[] { schema };
// Populate Links via the component's own OnValidate (reflection avoids the
// SendMessage 'ShouldRunBehaviour' editor assertion):
rootComp.GetType().GetMethod("OnValidate",
    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
    .Invoke(rootComp, null);
foreach (var c in new UnityEngine.Object[]{ actorTargets, actorSource, rootComp })
    UnityEditor.EditorUtility.SetDirty(c);
```

**4.5 Inert sub-director** (only when timeline-nesting work is planned): empty GO under
the stage root, exactly Transform + PlayableDirector (`playOnAwake=false`), and
deliberately **NO TimelineReferenceAuthoring** (§2 rung 2 inverse). Its playableAsset
and bindings belong to `unity-track-subdirector`, not you.
`PRE|objectExisted`, `PRE|componentExisted` as above.

**4.6 Physics ball** (dynamic body for the physics track family):
```csharp
// PRE|objectExisted; per component PRE|componentExisted; PRE|<field>=<prior> if mutating
var ball = UnityEngine.GameObject.CreatePrimitive(UnityEngine.PrimitiveType.Sphere);
ball.name = "<BallName>"; ball.transform.SetParent(stage.transform, false);
ball.transform.localPosition = new UnityEngine.Vector3(<x>,<y>,<z>);
UnityEngine.Object.DestroyImmediate(ball.GetComponent<UnityEngine.SphereCollider>()); // ECS-pure
var shape = ball.AddComponent<Unity.Physics.Authoring.PhysicsShapeAuthoring>(); // namespace per §3.1
// sphere r=0.5; ForceUnique=true (collider-mutating tracks warn-and-skip shared blobs)
shape.ForceUnique = true;
var body = ball.AddComponent<Unity.Physics.Authoring.PhysicsBodyAuthoring>();
body.MotionType = Unity.Physics.Authoring.BodyMotionType.Dynamic;  // property assignment, NOT SetMotionType
body.Mass = 1f;
ball.AddComponent<BovineLabs.Core.Authoring.LifeCycle.LifeCycleAuthoring>();
var ballTargets = ball.AddComponent<BovineLabs.Reaction.Authoring.Core.TargetsAuthoring>();
ballTargets.Target = /* stage target, re-found */;
```

**4.7 Trigger zone** (static, bodyless — a bodyless shape bakes static):
```csharp
// PRE|objectExisted; per component PRE|componentExisted
var zone = UnityEngine.GameObject.CreatePrimitive(UnityEngine.PrimitiveType.Cube);
zone.name = "<ZoneName>"; zone.transform.SetParent(stage.transform, false);
zone.transform.localPosition = new UnityEngine.Vector3(<x>,<y>,<z>);
zone.transform.localScale = new UnityEngine.Vector3(<sx>,<sy>,<sz>);
UnityEngine.Object.DestroyImmediate(zone.GetComponent<UnityEngine.BoxCollider>()); // ECS-pure
var zshape = zone.AddComponent<Unity.Physics.Authoring.PhysicsShapeAuthoring>();   // box
// OverrideCollisionResponse=true; CollisionResponse=RaiseTriggerEvents
zone.AddComponent<BovineLabs.Core.Authoring.PhysicsStates.StatefulTriggerEventAuthoring>();
// deliberately NO PhysicsBodyAuthoring
```

## 5. WORKED EXAMPLE (vex-ee training stage) — example environment; rediscover, never assume

- Project: `/home/i/GitHub/vex-ee` (`dataPath=/home/i/GitHub/vex-ee/Assets`). Parent
  scene `Assets/Scenes/Main Scene.unity` (root `Sub Scene` holds the SubScene
  component); SubScene `Assets/Scenes/Main Sub Scene.unity`. Foundations assets under
  `Assets/Training/00-foundations/`.
- Canonical stage (verified by fresh-load read-back, lessons 00/06/15):

```
TrainingStage                                                                       @ (0,0,0)
  Stage_Director   [PlayableDirector, TimelineReferenceAuthoring]                   @ (0,0,0)
  Stage_Target     [MeshFilter, MeshRenderer, LifeCycleAuthoring, TargetsAuthoring] @ (5,0,0)
  Stage_LinkRoot   [EntityLinkRootAuthoring]                                        @ (0,0,0)
    Stage_Actor    [MeshFilter, MeshRenderer, LifeCycleAuthoring, TargetsAuthoring,
                    StatAuthoring, TransformAuthoring, EntityLinkSourceAuthoring]   @ world (0,1,0)
  Stage_SubDirector [PlayableDirector ONLY - deliberately NO TimelineReferenceAuthoring]
  Stage_PhysicsBall [MeshFilter, MeshRenderer, PhysicsShapeAuthoring, PhysicsBodyAuthoring,
                    LifeCycleAuthoring, TargetsAuthoring]                           @ (0,1,5)
  Stage_TriggerZone [MeshFilter, MeshRenderer, PhysicsShapeAuthoring,
                    StatefulTriggerEventAuthoring]                                  @ (3,1,5) scale (2,2,2)
```

- Wiring: `Stage_Actor.Targets.Target = Stage_Target`; `Stage_Actor.Source.Root =
  Stage_LinkRoot`; `Stage_Actor.Source.Schemas[0] = Schema_Actor`;
  `Stage_LinkRoot.Links = [Stage_Actor]` (auto-populated, survives reload).
- Schema asset: `Assets/Training/00-foundations/Schema_Actor.asset` (EntityLinkSchema,
  auto-id resolved to 10 on re-read; 0 in the creation block).
- `Stage_Director.playableAsset` intentionally null at the foundations level — track
  lessons bind their own TimelineAssets. As of lesson 15 the director's binding table
  held THIRTEEN track bindings (Position/Scale/Rotation/TimeScale from lessons 01–04,
  the EntityLink quartet, Event, Intrinsic, TimelineEssenceStatTrack →
  Stage_Actor's TargetsAuthoring (lesson 13), DistanceToStatTrack → same (lesson 14),
  PhysicsFilterOverrideTrack → the Stage_PhysicsBall GameObject (lesson 15); TimeScale
  binds the StatAuthoring component). Tables are keyed by track asset and survive
  playableAsset swaps. Lessons 16–21 (physics family) appended more — counts drift;
  always re-dump, never trust the thirteen.
- Permanent stage state (lesson 04, corrected after lesson 13): `Stage_Actor`'s
  StatAuthoring carries `StatDefaults[0] = {Stat: SlowMo
  (Assets/Settings/Schemas/Stats/SlowMo.asset, key=94), ModifyType: Added, Value: 25}`
  — 25 in ×100 fixed-point = 0.25 factor; the original 0.25 truncated to int 0 at bake.
  Added via the SerializedObject append recipe in `unity-track-timeline-timescale`
  (the universal pattern for stat-driven tracks).
- Permanent stage state (lesson 06): `Stage_SubDirector` — exactly Transform +
  PlayableDirector (`playableAsset =
  Assets/Training/02-transform-scale-track/ScaleMastery.playable`, `playOnAwake=false`),
  ITS OWN binding ScaleTrack → `Stage_Actor.transform`, NO TimelineReferenceAuthoring
  (see `unity-track-subdirector` for the mechanism).
- Permanent stage state (lesson 15): `Stage_PhysicsBall` — PhysicsBodyAuthoring
  MotionType=Dynamic, Mass=1 (property assignment); PhysicsShapeAuthoring sphere r=0.5
  with ForceUnique=true (corrected post-lesson-15: authored False, and lesson 15 proved
  baked collider blobs are SHARED by default — see `unity-track-physics-filter-override`);
  classic SphereCollider removed. `Stage_TriggerZone` — static box shape,
  OverrideCollisionResponse=true + CollisionResponse=RaiseTriggerEvents,
  StatefulTriggerEventAuthoring, NO body authoring; classic BoxCollider removed.
- Known pre-existing vex-ee console background entries (don't claim them):
  UnityCliConnector HTTP server start, PerformanceTesting IPrebuildSetup/
  IPostBuildCleanup, TestResults.xml save.
- Historical note: the lesson-00 build used an idempotent delete-TrainingStage-and-
  rebuild block with NO pre-state capture. That pattern is retired by §4 — `EXPECTED:`
  any pre-lesson-00 TrainingStage contents were never captured and are unrecoverable.

## 6. UNDO APPENDIX

Artifact inventory for one full §4 run (vex-ee instance shown in §5):
1. Created asset `<schemaPath>` (+ folder segments, only if `PRE|folderExisted=false`).
2. Created GOs by recorded hierarchy path: stage root, director, target,
   linkRoot/actor, sub-director, ball, zone — each flagged `PRE|objectExisted`.
3. Added components on any PRE-EXISTING objects (the add-vs-mutate flag) — including
   the auto-added `TransformAuthoring` from EntityLinkSource's RequireComponent.
4. Mutated fields on pre-existing components (Targets slots, Source.Root/Schemas) —
   captured prior values.
5. NOT yours to undo unless your journal recorded adding them: director bindings,
   playableAssets, StatDefaults entries (in vex-ee those came from later track
   lessons — their journals own them). `EXPECTED:` the foundations training run
   captured none of the binding-table accretion; if asked to restore a pristine
   director table, the captures live in each track lesson's report, not here.

ORDER = REVERSE CREATION: zone → ball → sub-director → linkRoot (destroying it
destroys the child actor — correct only if BOTH were yours; if the actor pre-existed,
first remove your added components and restore captured fields, then leave it) →
target → director → stage root (only if `PRE|stageRootExisted=false` AND it is now
empty) → schema asset LAST. Justification: the actor's `Schemas[0]` references the
schema asset, so every scene reference must be destroyed/restored and SAVED before
`DeleteAsset`, or the scene file keeps a dangling `{fileID: 0}` reference; children
go before parents so "now empty" checks are truthful; the marker/components on
pre-existing objects are removed rather than their hosts destroyed.

Per-piece inverse ops (run inside the SubScene bracket; SaveScene before restoring
the parent; one logical undo per block):

```csharp
// UNDO-GO: a GameObject I created (PRE|objectExisted=false)
var go = /* walk subScene roots to "<CAPTURED hierarchy path>" */;
if (go != null) UnityEngine.Object.DestroyImmediate(go);
// UNDO-COMPONENT: a component I added to a pre-existing object (PRE|componentExisted=false)
var c = go.GetComponent<T>(); if (c != null) UnityEngine.Object.DestroyImmediate(c);
// Remove EntityLinkSourceAuthoring BEFORE its RequireComponent'd TransformAuthoring,
// and only remove TransformAuthoring if YOUR journal added it.
// UNDO-FIELD: a field I mutated on a pre-existing component
targets.Target = <CAPTURED prior value, never "null because that's the default">;
UnityEditor.EditorUtility.SetDirty(targets);
// After link-source removal, reflection-invoke the root's OnValidate so Links
// re-syncs (same recipe as §4.4), then SetDirty + SaveScene.
```

```csharp
// UNDO-SCHEMA (LAST, after the scene save above is verified):
var ok = UnityEditor.AssetDatabase.DeleteAsset("<CAPTURED schemaPath>");
if (!folderExisted && UnityEditor.AssetDatabase.FindAssets("", new[]{ "<CAPTURED schemaFolder>" }).Length == 0)
    UnityEditor.AssetDatabase.DeleteAsset("<CAPTURED schemaFolder>");
return "UNDONE|deleted=" + ok;
```

FINAL: fresh-load verification (protocol §7): re-open the SubScene additively; confirm
every journal path resolves to nothing (or to its restored pre-state values, quoted
against the `PRE|` lines); confirm `LoadAssetAtPath(schemaPath)==null`; restore the
parent scene single; `unity-cli console --filter error` clean against the project
baseline. A destroyed-object check that merely fails `GameObject.Find` is
insufficient — walk the recorded hierarchy paths.

## 7. VERIFICATION PROTOCOL

1. **Fresh-load hierarchy dump**: after building, re-open the SubScene additively in a
   NEW exec block and dump the stage hierarchy + key serialized fields per object;
   expect your §4 parameter values, including `WIRE|<root>.Links=<n>|[i]=<actor>`
   (Links populated and SURVIVING reload — the auto-management proof).
2. **Schema id re-read**: read `<schemaPath>` in a LATER exec block; expect a NONZERO
   auto-assigned id. Quote it — track specialists need the live value.
3. **Wiring read-back**: `Targets.Target`, `Source.Root`, `Source.Schemas[0]` quoted
   from the fresh load, not from the creating block (in-memory state lies).
4. **Parent-scene restore**: end with `sceneCount=1`,
   `scene[0]=<parentScenePath>|loaded=True|active=True|dirty=False`.
5. **Console**: `unity-cli console --filter error` must show nothing new beyond the
   project's known pre-existing background entries (vex-ee baseline in §5).
6. **Type-resolution failure protocol**: reflection-search all loaded assemblies for
   the simple name before concluding a type is missing; report the real namespace
   found (§3.1).
7. **Environment hand-off check**: before declaring the stage ready for a track
   specialist, walk the §2 silent-failure ladder rungs 1–4 against the built stage
   and quote the evidence per rung in your memory card.
