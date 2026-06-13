---
name: unity-cli
description: |
  Unity DOTS/ECS development with BovineLabs Core + full ecosystem. Enforces strict Data-Oriented Design,
  6-assembly architecture. Deep SubScene navigation, runtime entity verification, and game creation recipes.
  Optimized for safe Unity CLI operation: inspect first, mutate second, verify before claiming success.
---
# SYSTEM CAPABILITY: `unity-cli`

## UNIVERSAL OPERATING PROCEDURE

This section is intentionally explicit and procedural. Use it for every Unity scene task,
especially when the request is vague, the editor state is unknown, or previous attempts
looped without progress.

### The Only Safe Loop
For every Unity task, do exactly one pass through this loop. Do not skip steps.

1. **Inspect**: find the active scene, root objects, target objects, and whether a SubScene exists.
2. **Decide**: choose parent scene or subscene. If unsure, use parent scene for normal GameObjects and subscene only for ECS authoring objects.
3. **Mutate once**: run one `unity-cli exec` block that creates/edits the requested objects.
4. **Save**: save the changed scene or asset inside the same exec block.
5. **Verify**: query the exact objects/components/materials that should now exist.
6. **Restore editor**: reopen the original parent scene with `OpenSceneMode.Single`.
7. **Check errors**: run `unity-cli console --filter error`.
8. **Report only verified facts**: never say done unless verification, restore, and console check passed.

If a step fails, stop creating new things. Run a focused inspection command and fix the
first concrete error.

### Connectivity Smoke Test
Before doing real work, prove the editor connection works:

```bash
unity-cli exec "return UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene().path;"
```

Expected result: a scene path like `Assets/Scenes/Scene.unity`.

If the command hangs or prints nothing after about 10 seconds:
- Do not assume Unity is broken.
- The shell sandbox may be blocking the local editor connection.
- Ask for permission/escalation to run `unity-cli exec` outside the sandbox, then retry the same command.
- Do not start editing files or writing workaround scripts until this simple command works.

### Transient Listener Timeouts (Domain Reloads)
`Error: timed out waiting for Unity listener: cannot reach Unity health endpoint` is
usually TRANSIENT, not a crash. It happens whenever the editor is mid domain-reload
(script recompile, another agent editing code, entering play mode). Protocol:
- Retry the smoke test up to 3 times, waiting ~20s between attempts, before declaring
  the editor down. A command that worked 30 seconds ago proves the connection is fine.
- Never run two `unity-cli exec` invocations concurrently; queue your own commands.
- NEVER redirect `unity-cli exec > file` as the only copy of important output. On a
  listener timeout the file is silently EMPTY and the data is lost. Print to stdout
  first, confirm it, then persist (or `tee` and check the exit code / file size).

### Verify the Project Before Trusting Paths
Multiple editors/projects may exist on this machine and the project named in docs can
be stale. Before relying on any absolute path, asset path, or assembly list, run:

```bash
unity-cli exec "return UnityEngine.Application.dataPath;"
```

and treat THAT project root as ground truth. To know which DOTS tracks/clips actually
exist, reflection-enumerate `DOTSTrack` subclasses in the live editor instead of
trusting a written catalog (assemblies and packages drift).

### First Command For Any Scene Task
Always run this before creating, moving, deleting, or modifying scene objects:

```bash
cat << 'CSHARP' | unity-cli exec
var scene = UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene();
var sb = new System.Text.StringBuilder();
sb.AppendLine("ACTIVE_SCENE|" + scene.name + "|" + scene.path + "|roots=" + scene.rootCount);
var roots = scene.GetRootGameObjects();
for (int i = 0; i < roots.Length; i++) {
    var go = roots[i];
    sb.AppendLine("ROOT|" + i + "|" + go.name + "|children=" + go.transform.childCount);
    var sub = go.GetComponent<Unity.Scenes.SubScene>();
    if (sub != null) {
        var p = sub.SceneAsset != null ? UnityEditor.AssetDatabase.GetAssetPath(sub.SceneAsset) : "null";
        sb.AppendLine("SUBSCENE|" + go.name + "|" + p);
    }
}
return sb.ToString();
CSHARP
```

Read the output literally:
- `ACTIVE_SCENE` tells you where normal `new GameObject()` calls will go.
- `ROOT` tells you what already exists. Reuse matching objects instead of duplicating.
- `SUBSCENE` means ECS authoring may belong in that referenced `.unity` file.

### Where To Put Things
- If the parent scene has a `Unity.Scenes.SubScene`, put world content inside the referenced subscene by default: props, markers, environment art, gameplay objects, ECS authoring objects, and smoke-test objects.
- Keep parent-scene objects only for bootstrap/scene-level objects: `Main Camera`, global lights/volumes, UI, inputs, audio, managers, and the SubScene GameObject itself.
- If there is no SubScene, create normal GameObjects in the active parent scene.
- User asks for ECS entities, bakers, authoring for runtime systems, or DOTS conversion: **subscene asset** if one exists.
- Never create objects in both parent scene and subscene unless the user explicitly asks for both.
- Before editing a subscene, capture `parentScene.path`, open the subscene additively, set it active, save it, close it, then reopen the parent scene with `OpenSceneMode.Single`.

### Final Editor State Is Part Of The Task
Never leave the editor showing only a subscene or an additive scene setup after an automated
edit. At the end of every SubScene inspection, edit, verification, or recovery command, run:

```csharp
UnityEditor.SceneManagement.EditorSceneManager.OpenScene(parentScenePath, UnityEditor.SceneManagement.OpenSceneMode.Single);
```

Rules:
- Capture `var parentScenePath = parentScene.path;` before opening any subscene.
- After saving and closing the subscene, reopen `parentScenePath` with `OpenSceneMode.Single`.
- Do this even for read-only verification snippets that temporarily open a subscene.
- If the final visible editor scene is not the parent scene, the task is not done.

## DEEP SUBSCENE NAVIGATION

### Full Hierarchy Dump (Recursive)
Dumps the complete scene hierarchy including all nested children, components, and SubScene references.
Use this when you need to understand the entire scene structure before making changes.

```bash
cat << 'CSHARP' | unity-cli exec
var scene = UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene();
var sb = new System.Text.StringBuilder();

void DumpTransform(UnityEngine.Transform t, string indent) {
    var go = t.gameObject;
    var comps = go.GetComponents<UnityEngine.Component>();
    var ct = new System.Text.StringBuilder();
    for (int j = 0; j < comps.Length; j++) {
        if (comps[j] != null) {
            if (ct.Length > 0) ct.Append(", ");
            ct.Append(comps[j].GetType().Name);
        }
    }
    sb.AppendLine(indent + go.name + " [" + ct + "] pos=" + t.position.ToString("F2"));
    for (int i = 0; i < t.childCount; i++)
        DumpTransform(t.GetChild(i), indent + "  ");
}

var roots = scene.GetRootGameObjects();
sb.AppendLine("SCENE|" + scene.name + "|" + scene.path + "|roots=" + roots.Length);
for (int i = 0; i < roots.Length; i++) {
    var go = roots[i];
    var sub = go.GetComponent<Unity.Scenes.SubScene>();
    if (sub != null) {
        var p = sub.SceneAsset != null ? UnityEditor.AssetDatabase.GetAssetPath(sub.SceneAsset) : "null";
        sb.AppendLine("SUBSCENE|" + go.name + "|" + p);
    }
    DumpTransform(go.transform, "  ");
}
return sb.ToString();
CSHARP
```

### Full SubScene Content Dump (All Components + Serialized Fields)
Opens the subscene additively and dumps every object with its authoring component fields.

```bash
cat << 'CSHARP' | unity-cli exec
var parentScene = UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene();
var parentScenePath = parentScene.path;
var subScenePath = "REPLACE_WITH_SUBSCENE_PATH";
var subScene = UnityEditor.SceneManagement.EditorSceneManager.OpenScene(subScenePath, UnityEditor.SceneManagement.OpenSceneMode.Additive);
var sb = new System.Text.StringBuilder();
sb.AppendLine("SUBSCENE|" + subScene.name + "|roots=" + subScene.rootCount);
var roots = subScene.GetRootGameObjects();
for (int i = 0; i < roots.Length; i++) {
    var go = roots[i];
    var comps = go.GetComponents<UnityEngine.Component>();
    sb.AppendLine("  [" + i + "] " + go.name + " pos=" + go.transform.position.ToString("F2"));
    for (int j = 0; j < comps.Length; j++) {
        var c = comps[j];
        if (c != null && c.GetType().Name != "Transform") {
            var so = new UnityEditor.SerializedObject(c);
            so.Update();
            var sp = so.GetIterator();
            var fields = new System.Text.StringBuilder();
            while (sp.NextVisible(true)) {
                if (sp.name == "m_Script") continue;
                if (fields.Length > 0) fields.Append(", ");
                var val = sp.propertyType == UnityEditor.SerializedPropertyType.ObjectReference
                    ? (sp.objectReferenceValue != null ? sp.objectReferenceValue.name : "null")
                    : sp.stringValue.Length > 0 ? sp.stringValue : sp.propertyType.ToString();
                fields.Append(sp.name + "=" + val);
            }
            sb.AppendLine("    ." + c.GetType().Name + ": " + fields);
        }
    }
}
UnityEditor.SceneManagement.EditorSceneManager.CloseScene(subScene, false);
UnityEditor.SceneManagement.EditorSceneManager.OpenScene(parentScenePath, UnityEditor.SceneManagement.OpenSceneMode.Single);
return sb.ToString();
CSHARP
```

### Find All SubScenes in Project
```bash
cat << 'CSHARP' | unity-cli exec
var guids = UnityEditor.AssetDatabase.FindAssets("t:SceneAsset");
var sb = new System.Text.StringBuilder();
for (int i = 0; i < guids.Length; i++) {
    var path = UnityEditor.AssetDatabase.GUIDToAssetPath(guids[i]);
    sb.AppendLine("SCENE|" + path);
}
return sb.ToString();
CSHARP
```

### Enumerate All Authoring Components on a Specific Object
```bash
cat << 'CSHARP' | unity-cli exec --usings BovineLabs.Reaction.Authoring.Core,BovineLabs.Essence.Authoring,BovineLabs.Core.Authoring.LifeCycle
var go = UnityEngine.GameObject.Find("TARGET_NAME");
if (go == null) return "NOT FOUND";
var comps = go.GetComponents<UnityEngine.Component>();
var sb = new System.Text.StringBuilder();
for (int i = 0; i < comps.Length; i++) {
    var c = comps[i];
    if (c == null) continue;
    sb.AppendLine(c.GetType().FullName);
    var so = new UnityEditor.SerializedObject(c);
    so.Update();
    var sp = so.GetIterator();
    while (sp.NextVisible(true)) {
        if (sp.name == "m_Script") continue;
        string val;
        switch (sp.propertyType) {
            case UnityEditor.SerializedPropertyType.Boolean: val = sp.boolValue.ToString(); break;
            case UnityEditor.SerializedPropertyType.Float: val = sp.floatValue.ToString("F3"); break;
            case UnityEditor.SerializedPropertyType.Integer: val = sp.intValue.ToString(); break;
            case UnityEditor.SerializedPropertyType.Enum: val = sp.enumNames[sp.enumValueIndex]; break;
            case UnityEditor.SerializedPropertyType.ObjectReference: val = sp.objectReferenceValue != null ? sp.objectReferenceValue.name : "null"; break;
            case UnityEditor.SerializedPropertyType.Vector3: val = sp.vector3Value.ToString("F2"); break;
            default: val = "[" + sp.propertyType + "]"; break;
        }
        sb.AppendLine("  " + sp.name + " = " + val);
    }
}
return sb.ToString();
CSHARP
```

## BOVINELABS TOOLING CATALOG

The BovineLabs ecosystem provides a complete game framework built on DOTS/ECS. Below is every
authoring component, timeline clip, and data type available, organized by package.

### Package Layout Convention (6-Assembly Architecture)
Every BovineLabs package follows this structure:
```
PackageName/
├── PackageName/                    # Runtime (ISystems, IJobEntity, runtime IComponentData)
├── PackageName.Data/               # Data (IComponentData, IBufferElementData, SharedData, blobs)
├── PackageName.Authoring/          # Authoring (MonoBehaviours + Bakers, UNITY_EDITOR only)
├── PackageName.Editor/             # Editor (CustomInspectors, PropertyDrawers)
├── PackageName.Debug/              # Debug (Debug systems, telemetry, UNITY_EDITOR || BL_DEBUG)
└── PackageName.Tests/              # Tests
```

**Assembly dependency chain**: `Runtime → Data`, `Authoring → Data + Runtime`, `Editor → Authoring`, `Debug → Data`.
The `Scripts` project in `Assets/Scripts/` mirrors this with: `Scripts`, `Scripts.Data`, `Scripts.Authoring`, `Scripts.Editor`, `Scripts.Debug`, `Scripts.Tests`.

### Core: LifeCycle System (`BovineLabs.Core`)
- **LifeCycleAuthoring** — Adds `InitializeEntity` (prefabs) or `InitializeSubSceneEntity` (scene objects) + `DestroyEntity` (disabled). Required by ReactionAuthoring.
- **ObjectDefinition** — ScriptableObject that maps an auto-incremented ID to a prefab. Used by spawn actions.
- **ObjectCategories** — Bitmask categorization for ObjectDefinitions.

### Core: Reaction System (`BovineLabs.Reaction`)
The reaction system is the backbone of gameplay logic. It provides an activation-based event system.

- **ReactionAuthoring** — Core authoring. Requires `LifeCycleAuthoring` + `TargetsAuthoring`. Fields: `Active` (ActiveAuthoring), `Conditions` (ConditionAuthoring).
- **TargetsAuthoring** — Defines Owner, Source, Target, Custom entity references. Required by ReactionAuthoring. Fields: `Owner` (GameObject), `Source` (GameObject), `Target` (GameObject), `Custom` (GameObject), `Initialize.Target` (Target enum).
- **Target enum** (`BovineLabs.Reaction.Data.Core.Target`, byte-backed, VERIFIED
  vex-ee 2026-06): `None=0, Target=1, Owner=2, Source=3, Self=4, Custom=6` —
  note the gap at 5 and that None exists. Never trust remembered member order;
  dump `Enum.GetNames` live.

#### Reaction Addon Actions (`com.bovinelabs.reaction.addon`)
All action authoring components require `[RequireComponent(typeof(ReactionAuthoring))]`.

| Authoring Component | When It Fires | What It Does | Key Fields |
|---|---|---|---|
| `ActionCreateOnActivateAuthoring` | Reaction activates | Spawns objects | `Spawns[]` (ObjectDefinition + Target) |
| `ActionCreateOnDeactivateAuthoring` | Reaction deactivates | Spawns objects | `Spawns[]` (ObjectDefinition + Target) |
| `ActionDestroyOnActivateAuthoring` | Reaction activates | Destroys target entity | `Target` (Target enum) |
| `ActionDestroyOnDeactivateAuthoring` | Reaction deactivates | Destroys target entity | `Target` (Target enum) |
| `ActionDestroyOnChanceFailAuthoring` | Chance roll fails | Destroys target entity | `Target` (Target enum) |

### Core: Essence System (`BovineLabs.Essence`)
Provides stats and intrinsics for RPG/fighter/character systems.

- **StatAuthoring** — Adds stats + intrinsics to entity. Fields: `AddStats`, `StatDefaults[]` (StatModifierAuthoring), `StatDefaultGroups[]`, `StatsCanBeModified`, `AddIntrinsics`, `IntrinsicDefaults[]`, `IntrinsicDefaultGroups[]`, `Initialize.CopyFrom`.
- **TransformAuthoring** — Sets `TransformUsageFlags` for the baked entity. Controls whether `LocalTransform` is preserved at runtime. NOTE: lives in `BovineLabs.Core.Authoring` (assembly `BovineLabs.Core.Authoring`), NOT in Essence — verified in vex-ee 2026-06-11.

### Timeline: Core (`BovineLabs.Timeline.Core`)
- **TimelineReferenceAuthoring** — Links a PlayableDirector to an ECS entity via `TimelineReference` component. Required for all Timeline ECS tracks.

### Timeline: Physics (`BovineLabs.Timeline.Physics`)
Timeline clips that drive physics simulation:

| Clip | Track | Purpose |
|---|---|---|
| `PhysicsForceClip` | `PhysicsForceTrack` | Apply forces to physics bodies |
| `PhysicsVelocityClip` | `PhysicsVelocityTrack` | Override velocity directly |
| `PhysicsDragClip` | `PhysicsDragTrack` | Apply drag over time |
| `PhysicsTriggerForceClip` | `StatefulTriggerTrack` | Apply force on trigger events |
| `PhysicsTriggerInstantiateClip` | `StatefulTriggerTrack` | Spawn objects on trigger |
| `PhysicsTriggerConditionClip` | `StatefulTriggerTrack` | Conditional logic on triggers |
| `PhysicsLinearPIDClip` | `PhysicsLinearPIDTrack` | PID controller for position |
| `PhysicsAngularPIDClip` | `PhysicsAngularPIDTrack` | PID controller for rotation |
| `PhysicsGravityOverrideClip` | `PhysicsGravityOverrideTrack` | Override gravity per-entity |
| `PhysicsKinematicOverrideClip` | `PhysicsKinematicOverrideTrack` | Force kinematic mode |
| `PhysicsFilterOverrideClip` | `PhysicsFilterOverrideTrack` | Change collision filter at runtime |
| `PhysicsVelocityClampClip` | `PhysicsVelocityClampTrack` | Clamp max velocity |
| `PhysicsRicochetClip` | `PhysicsRicochetTrack` | Ricochet off surfaces |
| `Physicsteleportclip` | `Physicsteleporttrack` | Teleport physics body |

### Timeline: Animation (`BovineLabs.Timeline.Animation`)
- **RukhankaAnimationClip** / **RukhankaAnimationTrack** — Play animation clips via Rukhanka
- **BlendTree2DClip** / **BlendTree2DTrack** — 2D blend tree (movement blending)
- **AfterImageClip** / **AfterImageTrack** — Spawn after-image effects
- **FollowPositionOnlyAuthoring** — Follow another entity's position only (no rotation)
- **TimelineAnimationStateAuthoring** — State machine for animation transitions

### Timeline: PlayerInputs (`BovineLabs.Timeline.PlayerInputs`)
- **InputConsumerAuthoring** — Marks entity as input consumer
- **AxisTransformClip** / **AxisTransformTrack** — Map input axis to transform
- **CommandSequenceClip** / **CommandSequenceTrack** — Fighting game combo sequences
- **InputEventsClip** / **InputEventsTrack** — Fire events on input conditions
- **InputBufferWindowClip** / **InputBufferTrack** — Input buffering window
- **InputBufferClearClip** / **InputBufferTrack** — Clear input buffer

### Timeline: EntityLinks (`BovineLabs.Timeline.EntityLinks`)
Entity reference system for Timeline clips:
- **EntityLinkSourceAuthoring** — Root entity with link buffer
- **EntityLinkRootAuthoring** — Entity that owns the link map
- **EntityLinkSchema** — ScriptableObject defining link ID → entity mapping
- **EntityLinkMutateClip** / **EntityLinkMutateTrack** — Modify entity links at runtime
- **EntityLinkParentClip** / **EntityLinkParentTrack** — Parent entities together
- **EntityLinkTargetPatchClip** / **EntityLinkTargetPatchTrack** — Patch target references

### Timeline: Transform (`com.bovinelabs.timeline.transform`)
- **PositionClip** / **PositionTrack** — Animate position (with PositionStartClip)
- **RotationTrack** + `RotationLookAtStartClip` / `RotationLookAtTargetClip` — Animate rotation
- **ScaleClip** / **ScaleTrack** — Animate scale (with ScaleStartClip)

### Timeline: Time (`BovineLabs.Timeline.Time`)
- **TimelineTimeScaleClip** / **TimelineTimeScaleTrack** — Per-timeline time scaling
- **WorldTimeScaleClip** / **WorldTimeScaleTrack** — Global time scaling (slow-mo, bullet time)

### Timeline: Parenting (`com.bovinelabs.timeline.parenting`)
- **TemporaryDetachClip** / **TemporaryDetachTrack** — Temporarily detach child from parent during clip

### Timeline: Essence (`BovineLabs.Timeline.Essence`)
- **TimelineEssenceStatClip** / **TimelineEssenceStatTrack** — Modify stats from timeline
- **TimelineEssenceIntrinsicClip** / **TimelineEssenceIntrinsicTrack** — Modify intrinsics from timeline
- **TimelineEssenceEventClip** — Fire essence events from timeline
- **ActionTickDistributionAuthoring** — Distribute ticks across time curves

### HitStop (`BovineLabs.HitStop`)
- **HitStopAuthoring** — Triggers hit-stop (frame freeze) effect on hit. Used in fighting games.

### Physics Extras
- **PhysicsForceAccumulatorAuthoring** — Accumulate multiple forces on one body per frame
- **PhysicsDebugDisplayAuthoring** — Visual debug for physics colliders, contacts, events
- **SmearVelocityAuthoring** — Motion smear effect (stretch mesh along velocity)

## PROJECT ASSEMBLY MAP

STALENESS WARNING (2026-06-11): the live editor reached by unity-cli is currently the
project at `/home/i/GitHub/vex-ee` (verify with `Application.dataPath` — see
"Verify the Project Before Trusting Paths"). vex-ee additionally contains
BovineLabs.Vibe (~70 cosmetic tracks: Volume, Cinemachine, Audio, Light…), plus
Timeline.Grid.Influence, Timeline.UI, and Timeline.Distance. A ground-truth track
inventory lives at `/home/i/GitHub/marimo-unity-cli/training/data/live-tracks.txt`.
The map below describes `/home/i/GitHub/BovineLabs` and still applies to the shared
packages:

### Project Scripts (`Assets/Scripts/`)
| Assembly | Purpose | Key References |
|---|---|---|
| `Scripts` | Runtime systems & logic | BovineLabs.Core, Unity.Entities |
| `Scripts.Data` | IComponentData, buffers | BovineLabs.Core |
| `Scripts.Authoring` | MonoBehaviours + Bakers (UNITY_EDITOR) | Scripts.Data, BovineLabs.Core.Authoring |
| `Scripts.Editor` | Custom editors | Scripts.Authoring |
| `Scripts.Debug` | Debug systems | Scripts.Data |
| `Scripts.Tests` | Tests | Scripts, Scripts.Data |

### Packages (in `Packages/`)
| Package | Sub-packages |
|---|---|
| `BovineLabs.Timeline.Core` | Core, Core.Authoring, Core.Data, Core.Debug, Core.Tests |
| `BovineLabs.Timeline.Physics` | Physics, Physics.Authoring, Physics.Data, Physics.Debug, Physics.Editor, Physics.Rendering, Physics.Rendering.Authoring, Physics.Tests |
| `BovineLabs.Timeline.Animation` | Animation, Animation.Authoring, Animation.Data, Animation.Editor, Animation.Tests |
| `BovineLabs.Timeline.PlayerInputs` | PlayerInputs, PlayerInputs.Authoring, PlayerInputs.Data, PlayerInputs.Debug, PlayerInputs.Editor, PlayerInputs.Tests |
| `BovineLabs.Timeline.EntityLinks` | EntityLinks, EntityLinks.Authoring, EntityLinks.Data, EntityLinks.Debug, EntityLinks.Editor, EntityLinks.Tests |
| `BovineLabs.Timeline.Essence` | Essence, Essence.Authoring, Essence.Data, Essence.Debug, Essence.Editor, Essence.Tests |
| `BovineLabs.Timeline.Time` | Time, Time.Authoring, Time.Data, Time.Tests |
| `BovineLabs.Timeline.Distance` | Distance, Distance.Authoring, Distance.Data, Distance.Debug, Distance.Editor, Distance.Tests |
| `BovineLabs.HitStop` | HitStop, HitStop.Authoring, HitStop.Data, HitStop.Tests |
| `com.bovinelabs.reaction.addon` | Reaction.Addon, Reaction.Addon.Authoring, Reaction.Addon.Data, Reaction.Addon.Debug, Reaction.Addon.Editor, Reaction.Addon.Tests |
| `com.bovinelabs.timeline.parenting` | Parenting, Parenting.Authoring, Parenting.Debug |
| `com.bovinelabs.timeline.transform` | Transform, Transform.Authoring |

### Monorepo Packages (tertle-monorepo at `/home/i/GitHub/tertle-monorepo/`)
The full BovineLabs ecosystem includes additional packages not in every project:
- `com.bovinelabs.core` — Core framework (LifeCycle, ObjectManagement, Extensions, Iterators)
- `com.bovinelabs.reaction` — Reaction system (Active states, Conditions, Targets)
- `com.bovinelabs.essence` — Stats, Intrinsics, Buffers
- `com.bovinelabs.bridge` — Unity bridge (Audio, Camera, Spline, VFX bakers)
- `com.bovinelabs.canopy` — State machine system
- `com.bovinelabs.nerve` — Networking (Netcode for Entities)
- `com.bovinelabs.grove` — Scene management
- `com.bovinelabs.vibe` — Audio
- `com.bovinelabs.quill` — Debug drawing
- `com.bovinelabs.recast` — NavMesh/AI
- `com.bovinelabs.traverse` — Traversal/movement
- `com.bovinelabs.saving.free` — Save system
- `com.bovinelabs.anchor` — Anchor/tethering
- `com.bovinelabs.timeline` — Timeline base package

## GAME CREATION RECIPES

These recipes create complete game elements using BovineLabs authoring components. All objects
go into the SubScene by default (open additively, set active, save, close, restore parent).

### Recipe: Player Entity (Physics Character)
Creates a player with physics body, collider, target system, and reaction system.

```bash
cat << 'CSHARP' | unity-cli exec
var parentScene = UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene();
var parentScenePath = parentScene.path;

// Find subscene path
var subScenePath = "";
var parentRoots = parentScene.GetRootGameObjects();
for (int i = 0; i < parentRoots.Length; i++) {
    var sub = parentRoots[i].GetComponent<Unity.Scenes.SubScene>();
    if (sub != null && sub.SceneAsset != null) {
        subScenePath = UnityEditor.AssetDatabase.GetAssetPath(sub.SceneAsset);
        break;
    }
}
if (string.IsNullOrEmpty(subScenePath)) return "ERROR|No SubScene found";

var subScene = UnityEditor.SceneManagement.EditorSceneManager.OpenScene(subScenePath, UnityEditor.SceneManagement.OpenSceneMode.Additive);
UnityEditor.SceneManagement.EditorSceneManager.SetActiveScene(subScene);

// Create player GameObject with primitives
var old = UnityEngine.GameObject.Find("Player");
if (old != null && old.scene.path == subScene.path) UnityEngine.Object.DestroyImmediate(old);

var player = UnityEngine.GameObject.CreatePrimitive(UnityEngine.PrimitiveType.Capsule);
player.name = "Player";
player.transform.position = new UnityEngine.Vector3(0f, 2f, 0f);
player.transform.localScale = UnityEngine.Vector3.one;

// Add Physics components (Unity Physics authoring)
var body = player.AddComponent<Unity.Physics.Authoring.PhysicsBodyAuthoring>();
// CORRECTED (verified vex-ee 2026-06): MotionType is a PROPERTY; SetMotionType() does not exist here.
body.MotionType = Unity.Physics.Authoring.BodyMotionType.Dynamic;
body.Mass = 1f;
body.LinearDamping = 0.1f;
body.AngularDamping = 0.05f;
body.GravityFactor = 1f;
// Shapes: use PhysicsShapeAuthoring.SetSphere(new SphereGeometry{...}, quaternion.identity)
// / SetBox(new BoxGeometry{...}). STATIC colliders = PhysicsShapeAuthoring ONLY (no body).
// Trigger volumes: shape.OverrideCollisionResponse = true; shape.CollisionResponse =
// Unity.Physics.CollisionResponsePolicy.RaiseTriggerEvents; plus
// BovineLabs.Core.Authoring.PhysicsStates.StatefulTriggerEventAuthoring (zero-field marker;
// asm BovineLabs.Core.Extensions.Authoring — NOT Unity.Physics.Stateful, see rule 6).
// CreatePrimitive adds a CLASSIC collider — DestroyImmediate it (ECS-pure rule).

// Add TargetsAuthoring (required by Reaction system)
var targets = player.AddComponent<BovineLabs.Reaction.Authoring.Core.TargetsAuthoring>();

// Add LifeCycleAuthoring (required by ReactionAuthoring)
player.AddComponent<BovineLabs.Core.Authoring.LifeCycle.LifeCycleAuthoring>();

// Add ReactionAuthoring (activation/condition system)
player.AddComponent<BovineLabs.Reaction.Authoring.Core.ReactionAuthoring>();

// Add StatAuthoring for health/stats
player.AddComponent<BovineLabs.Essence.Authoring.StatAuthoring>();

// Add TransformAuthoring to ensure LocalTransform is preserved at runtime
var ta = player.AddComponent<BovineLabs.Core.Authoring.TransformAuthoring>();
ta.TransformUsageFlags = Unity.Entities.TransformUsageFlags.Dynamic;

// Add EntityLinkSourceAuthoring for timeline entity linking
player.AddComponent<BovineLabs.Timeline.EntityLinks.Authoring.EntityLinkSourceAuthoring>();

UnityEditor.SceneManagement.EditorSceneManager.SaveScene(subScene);
UnityEditor.SceneManagement.EditorSceneManager.SetActiveScene(parentScene);
UnityEditor.SceneManagement.EditorSceneManager.CloseScene(subScene, false);
UnityEditor.SceneManagement.EditorSceneManager.SaveScene(parentScene);
UnityEditor.SceneManagement.EditorSceneManager.OpenScene(parentScenePath, UnityEditor.SceneManagement.OpenSceneMode.Single);
return "CREATED|Player in " + subScenePath;
CSHARP
```

### Recipe: Physics Prop (Destructible Crate)
Creates a physics-enabled prop with reaction system that destroys itself on deactivate.

```bash
cat << 'CSHARP' | unity-cli exec
var parentScene = UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene();
var parentScenePath = parentScene.path;
var subScenePath = "";
var parentRoots = parentScene.GetRootGameObjects();
for (int i = 0; i < parentRoots.Length; i++) {
    var sub = parentRoots[i].GetComponent<Unity.Scenes.SubScene>();
    if (sub != null && sub.SceneAsset != null) { subScenePath = UnityEditor.AssetDatabase.GetAssetPath(sub.SceneAsset); break; }
}
if (string.IsNullOrEmpty(subScenePath)) return "ERROR|No SubScene";

var subScene = UnityEditor.SceneManagement.EditorSceneManager.OpenScene(subScenePath, UnityEditor.SceneManagement.OpenSceneMode.Additive);
UnityEditor.SceneManagement.EditorSceneManager.SetActiveScene(subScene);

var old = UnityEngine.GameObject.Find("Crate");
if (old != null && old.scene.path == subScene.path) UnityEngine.Object.DestroyImmediate(old);

var crate = UnityEngine.GameObject.CreatePrimitive(UnityEngine.PrimitiveType.Cube);
crate.name = "Crate";
crate.transform.position = new UnityEngine.Vector3(3f, 0.5f, 0f);
crate.transform.localScale = UnityEngine.Vector3.one;

var body = crate.AddComponent<Unity.Physics.Authoring.PhysicsBodyAuthoring>();
body.SetMotionType(Unity.Physics.Authoring.PhysicsBodyAuthoring.MotionType.Dynamic);
body.Mass = 5f;
body.LinearDamping = 0.5f;

crate.AddComponent<BovineLabs.Core.Authoring.LifeCycle.LifeCycleAuthoring>();
var targets = crate.AddComponent<BovineLabs.Reaction.Authoring.Core.TargetsAuthoring>();
var reaction = crate.AddComponent<BovineLabs.Reaction.Authoring.Core.ReactionAuthoring>();
var destroyAction = crate.AddComponent<BovineLabs.Reaction.Addon.Authoring.ActionDestroyOnDeactivateAuthoring>();
// destroyAction.Target defaults to Target.Self — destroys this crate when reaction deactivates

UnityEditor.SceneManagement.EditorSceneManager.SaveScene(subScene);
UnityEditor.SceneManagement.EditorSceneManager.SetActiveScene(parentScene);
UnityEditor.SceneManagement.EditorSceneManager.CloseScene(subScene, false);
UnityEditor.SceneManagement.EditorSceneManager.SaveScene(parentScene);
UnityEditor.SceneManagement.EditorSceneManager.OpenScene(parentScenePath, UnityEditor.SceneManagement.OpenSceneMode.Single);
return "CREATED|Crate in " + subScenePath;
CSHARP
```

## ECS TIMELINE & ARCHITECTURE EDGE CASES

When automating ECS generation, modifying DOTS Timeline tracks, or resolving compilation/runtime issues, adhere to these generalized rules:

### 1. Robust Scene Generation (Avoid CLI limits)
When tasked with creating complex ECS setups (many GameObjects, nested SubScenes, multiple DOTS authoring components), **do not** write massive `unity-cli exec` blocks. The CLI dynamic compiler struggles to resolve assemblies if the project has existing compile errors, creating circular blockers.
**Best Practice**: Create a standard Editor script (e.g., `Assets/Editor/RebuildShowcasesEditor.cs`) with a `[MenuItem("Tools/Rebuild")]`, and execute it. This leverages Unity's robust internal compiler and handles edge cases gracefully.

### 2. Timeline to ECS Bridging (The ECS-Pure Rule)
Timeline tracks in DOTS do not work like classic Unity components. If you are animating or driving an entity via Timeline (Physics, Animation, Transform):
- **Never** mix classic Unity components (like `BoxCollider`, `Rigidbody`, `Animator`) with DOTS Timeline tracks on the same object. Use pure DOTS authoring equivalents (`PhysicsBodyAuthoring`, `PhysicsShapeAuthoring`).
- **Always** include `TimelineReferenceAuthoring` on directors that should PLAY.
  CORRECTED MECHANISM (source-verified vex-ee 2026-06): `PlayableDirectorBaker`
  bakes EVERY PlayableDirector with a TimelineAsset unconditionally — the marker is
  an ACTIVATION gate, not a bake gate. Its consumer enables `TimelineActive` on the
  baked timeline; without the marker the baked entities sit inert, so clips still
  "silently do nothing". Corollary: a sub-director nested via SubDirectorClip must
  NOT carry TimelineReferenceAuthoring, or its independently-baked copy activates in
  parallel and double-drives the bound objects.

### 3. Component Dependencies in ECS Authoring
ECS authoring is highly interdependent. Adding a single component is rarely enough. In the BovineLabs ecosystem:
- Any interaction or reaction requires **`TargetsAuthoring`** (defines who acts on whom) and **`LifeCycleAuthoring`** (initializes the entity).
- Missing dependencies won't crash the editor, but the systems will silently ignore the entity at runtime. Always check the Authoring Component documentation and add the full stack.

### 4. WorldSystemFilter Mismatches
If the Unity console throws errors about systems failing to inject into groups (e.g., *"...could not be added to group X, because the group was not created in the world Editor World"*):
- This means an `[ISystem]` has `WorldSystemFilterFlags.Editor` (trying to run in the editor), but its designated `[UpdateInGroup]` is restricted to runtime worlds (like `ServerLocal`).
- **Fix**: Either remove `WorldSystemFilterFlags.Editor` from the system, or change its update group to one that exists in the Editor world.

### 5a. OnValidate-Managed Arrays (EntityLinkRootAuthoring.Links and friends)
Some authoring arrays are REBUILT by `OnValidate()` on every load/import and will
silently discard manual assignments. Known case: `EntityLinkRootAuthoring.Links` is
rebuilt as `GetComponentsInChildren<EntityLinkSourceAuthoring>(true)`. The design
intent is hierarchy: parent the link-source object UNDER the root object instead of
assigning the array. After any such mutation, reload the scene from disk and read the
array back — if your value vanished, the field is auto-managed.

### 5b. Calling OnValidate From Automation
`SendMessage("OnValidate")` logs a `ShouldRunBehaviour()` editor assertion. If you must
trigger a private `OnValidate`, reflection-invoke it:
`comp.GetType().GetMethod("OnValidate", BindingFlags.Instance|BindingFlags.NonPublic).Invoke(comp, null);`

### 5c. Auto-ID ScriptableObjects (EntityLinkSchema etc.)
`[AutoRef]`/IUID assets get their id assigned at IMPORT time, not at
`AssetDatabase.CreateAsset` time. Create the asset, then re-read its id in a LATER
exec block (after an import pass) — reading immediately returns the default (0).

### 5d. Asset→Scene References Null Out SILENTLY
A project asset (.playable clip, ScriptableObject, prefab) can NEVER hold a direct
reference to a scene object. Assigning one in memory appears to work, but on save the
YAML serializes `{fileID: 0}` and a fresh load returns null — with NO console warning.
The in-memory object keeps the stale reference until domain reload, so reading it back
without a reload proves nothing. Always verify by reading the saved YAML
(`File.ReadAllText(assetPath)`) or a fresh `AssetDatabase.LoadAssetAtPath` +
`SerializedObject`. This gap is exactly what EntityLinks (schema IDs resolved at
runtime) exists to fill — use it instead of direct refs from timeline clips to scene
objects. Same applies to cross-scene refs (SubScene object → parent-scene object).

### 5e. Timeline Clip Inspection Specifics (verified in vex-ee)
- Many BovineLabs enums are byte-backed (`PositionType : byte`); `(int)` casts on
  boxed values throw — use `System.Convert.ToInt64(value)`.
- This editor build hard-errors on obsolete APIs inside exec snippets: use
  `objectReferenceEntityIdValue` (not `objectReferenceInstanceIDValue`) and
  `GetEntityId()` (not `GetInstanceID()`).
- `DOTSTrack` serializes its own `resetOnDeactivate` AND concrete tracks may add
  their own flag (e.g. `TransformPositionTrack.ResetPositionOnDeactivate`) — they
  are different fields; set the one you mean.
- Finding 0 baked entities in the Editor World for a closed SubScene is NORMAL
  (only a `SceneReference` entity may exist). It is not proof the bake failed; do
  not force imports or enter play mode just to "fix" it.

### 5f. A Field Can Bake Fine and Still Be Discarded at Runtime
Serialization + bake success does not mean the value takes effect. Example
(verified): `ScaleClip.Scale` is a Vector3, but if the BINDING entity has no
`PostTransformMatrix`, the write job does `LocalTransform.Scale = blend.x` — Y/Z
silently dropped. Whether `PostTransformMatrix` exists is decided at BAKE time by
whether the authoring GameObject's localScale was non-uniform. Lessons:
- Trace the write job's branch for the binding's actual baked shape before
  promising a designer an effect.
- Clip field defaults define the "no-op direction" and differ per type
  (Scale default = one, Position default = zero) — a fresh clip is not neutral
  in every track type.
- Runtime clamps usually protect blend INPUTS (e.g. current zero scale → 1), not
  designer-set targets; a target of (0,0,0) is applied verbatim. Read the branch.
- Namespaces can contain duplicated segments (real case:
  `BovineLabs.Timeline.Transform.Authoring.Authoring.Scale.TransformScaleTrack`).
  Reflect over assemblies for the real FullName; never guess from convention.

### 5g. ExposedReference Is the Asset→Scene Bridge — and It's Two-Sided
`ExposedReference<T>` fields (e.g. `RotationLookAtTargetClip.Target`) are the correct
way for a .playable clip to reference a scene object (contrast 5d). The link has TWO
halves needing TWO saves:
- Asset side: the clip stores only a GUID `exposedName`. Mint one if empty
  (`new PropertyName(Guid.NewGuid().ToString())`), then `AssetDatabase.SaveAssets()`.
- Scene side: `director.SetReferenceValue(name, obj)` writes the director's
  `m_ExposedReferences` table, serialized in the SCENE file — then save the scene.
Forgetting the scene save silently loses the object half while the GUID half looks
healthy. Verified to survive fresh reloads (vex-ee 2026-06). Bake resolves via
`director.GetReferenceValue(exposedName)`; unset/missing → Entity.Null → silent
per-frame skip at runtime. Director binding + exposed-reference tables are keyed by
track/GUID, so they survive `playableAsset` swaps — swap-and-restore is safe.

### 5h. Short Type Names Collide Across Timeline Assemblies
The same clip class name can exist in multiple assemblies with different fields
(real case: `RotationLookAtTargetClip` in `BovineLabs.Timeline.Transform.Authoring`
uses `ExposedReference<Transform>`, while `BovineLabs.Vibe.Authoring.LocalTransform`
has its own). Always fully-qualify type names in exec snippets and when reflecting.

### 5i. quaternion.LookRotation NaN Poisoning
`LookAtTargetClipJob` computes `LookRotation(targetPos - bindingPos, math.up())` with
NO guard: zero distance OR a look direction parallel to up produces NaN quaternions
that poison `LocalTransform.Rotation` (verified numerically). Designer rule: keep
look-at targets laterally offset from the bound object; never parked inside/directly
above it.

### 5j. Stat-Driven Tracks: Buffer-Missing ≠ Key-Missing
Tracks that read Essence stats (TimelineTimeScale, Distance, Essence*, EssenceUI)
resolve per frame with this guard: `StatKey != 0 && StatEntity != Null &&
TryGetBuffer(entity)`. Guard FAILS → graceful fallback to the authored value. Guard
PASSES but the key is absent from the buffer → `GetValueFloat` returns its
defaultValue of 0 → silent zero (for a time-scale track: a frozen timeline) — worse
than fallback and logged nowhere. Vaccine: ensure the schema is in the bound
entity's `StatAuthoring.StatDefaults` (append a `StatModifierAuthoring
{Stat, ModifyType=Added, Value}` entry via SerializedObject). REUSE the project's
existing schema assets (vex-ee: ~114 under `Assets/Settings/Schemas/Stats/`); never
create new auto-ID schema assets for tests. Clip→schema refs are asset→asset and
serialize fine (contrast 5d).

### 5k. SetGenericBinding Never Coerces; duration Overrides Only Seed
- `director.SetGenericBinding(track, obj)` stores exactly what you pass (GameObject
  stays GameObject, Component stays Component). Baking coerces either to the same
  entity, and `TrackBindingType` is only editor-UI guidance — so don't infer binding
  health from types; read `GetGenericBinding` back and check what's actually there.
- A clip class overriding `duration => X` only seeds the length at `CreateClip` time;
  `clip.duration` remains freely settable afterwards. Don't read an override as a
  fixed-duration constraint.
- Time-scale values are UNCLAMPED in BovineLabs Timeline: 0 freezes the clock,
  negative runs it backwards (`clock.DeltaTime *= value`). Guard designer inputs.

### 5l. Timeline Core Lives in PackageCache; Settings Assets Serialize Subsets
- The BovineLabs timeline CORE sources are in
  `Library/PackageCache/com.bovinelabs.timeline@<hash>/`, NOT under `Packages/`
  (unlike the Time/Transform/etc. packages) — and the scheduler folder/namespace is
  misspelled `Schedular`. Filename searches under `Packages/` alone come back empty.
- A SettingsBase .asset may serialize only a SUBSET of its fields (absent YAML keys
  mean the C# field initializers govern). Read the class defaults before concluding
  a setting's value from the asset file.
- Authoring an easeOut on a clip that is then overlapped converts silently to a
  blendOut — overlap blending supersedes the authored ease on that edge.

### 5m. Director timeUpdateMode Is Load-Bearing for Time-Scale Semantics
World time scale loops back through the engine: `Time.timeScale` → `Time.deltaTime`
→ ClockUpdateSystem → every GameTime timeline's own clock. Consequences (source-
proven, vex-ee 2026-06):
- A WorldTimeScaleClip with timeScale 0 on a GameTime director DEADLOCKS: the
  frozen clock can never advance past the clip's end, so 0 is re-asserted forever.
  Freeze-frame recipe: author the director `UnscaledGameTime`, or stop the timeline
  entity externally.
- World scale and TimelineTimeScale COMPOUND multiplicatively (one frame latent).
- `PlayableDirectorBaker` maps DSPClock→UnscaledGameTime (with a warning) and
  Manual→Constant — check the director's mode before reasoning about any
  freeze/slow-mo behavior.
- The MixData 4-slot weight shift-register is shared by ALL BovineLabs track
  blending: more than 4 simultaneous clips on one blend target silently drops the
  lowest-weighted (ties lose to incumbents).

### 5n. Timeline Nesting (SubDirectorClip / SubTimelineClip)
Verified vex-ee 2026-06:
- `SubTimelineClip.TrackBindings` (TrackKeyPair.Target) is a direct Object ref in
  an asset → SCENE targets null silently per 5d; ASSET targets survive. So
  SubTimelineClip is asset-target-only; for scene bindings use SubDirectorClip —
  the nested scene director owns real binding/exposed tables
  (SubTimelineClip's bake even sets `Director = null`, making TrackBindings its
  only binding source).
- `SubDirectorClip.DefaultClipDuration` defaults to 5 (kDefaultClipDurationInSeconds),
  not 1. Both clip types SILENTLY skip baking when their reference is unset.
- Composite timer: nested clock = `hostTime × clip.timeScale + (clipIn − start×timeScale)`,
  chained/anchored to the ROOT timer. No recursion depth guard — keep nesting a DAG;
  self-nesting would recurse unboundedly at bake. Depth ≥2 has a source-derived
  double-transform caveat (parent scale/offset applied twice unless identity) — treat
  deep nesting as untested.

### 5o. EntityLinks Family: Loud at Bake, Silent at Runtime
Verified vex-ee 2026-06 (package at `Packages/BovineLabs.Timeline.EntityLinks/`):
- BAKE failures are LOUD: a null/id-0 schema on an EntityLinks clip logs
  `Debug.LogError` and skips adding the component. RUNTIME resolution failures are
  ALL silent per-frame skips (null binding, Targets slot None/unset, root without
  the EntityLinkEntry buffer, key absent). Triage rule: console error → authoring
  problem; nothing happens with clean console → runtime resolution problem.
- Resolution chain (the family pattern): bound entity's `Targets` →
  `targets.Get(ReadRootFrom)` → that entity's `EntityLinkSource.Root` (or itself)
  → linear search of root's `EntityLinkEntry{Key,Target}` buffer for the schema's
  ushort key. The default `ReadRootFrom` differs per clip (CopyTransform=Owner,
  Mutate=Source) — an unset Targets.Owner means the DEFAULT silently never
  resolves; set ReadRootFrom=Self when the bound entity itself carries the link.
- KNOWN PACKAGE BUG (2026-06): `EntityLinkCopyTransformSystem.OnUpdate` creates
  its BeginSimulation ECB but never assigns the job's `ECB` field — the write path
  runs on a default ParallelWriter and should throw on the first resolved active
  frame. Any "CopyTransform does nothing / throws" triage starts there. Mirror
  finding: TargetPatch's ECB is assigned but UNUSED (dead code; its real write is
  same-frame in-place). Parent's ECB is correct.
- TargetPatch specifics: one-shot at activation, permanent, writes ONE Targets
  slot in place; `if (resolved == Entity.Null) return;` means failure NEVER
  nulls a slot — and TargetPatch cannot express "clear a slot" at all.
  Fallback=None = safe no-touch-on-failure default; Fallback=Self writes the
  binding entity (legal, though WriteTo=Self is a loud bake error). It stores no
  snapshot and has no self-inverse: the pre-patch value is unrecoverable
  on-timeline except via fragile park-then-restore slot copies (contrast Mutate's
  one-clip Swap undo).
- Ordering guarantees: ONLY Mutate is UpdateBefore TargetPatch/Parent.
  TargetPatch↔Parent↔CopyTransform are mutually UNORDERED in the group — never
  chain cross-track effects same-frame; stagger clips by at least one frame.
- Triage details: bake LogErrors print the clip SUB-ASSET name (not the
  TimelineClip displayName) — grep worker logs accordingly. EntityLinkSchema's
  implicit ushort conversion makes some Object-typed API calls ambiguous in the
  exec compiler — cast to `(UnityEngine.Object)` first.

### 5p. Capturing Bake-Time Errors (Closed SubScenes Bake on Import Workers)
Saving a scene or calling ImportAsset does NOT trigger an entity bake, and bake-time
`Debug.LogError`s from closed-SubScene bakes run on AssetImportWorker processes —
they never reach `unity-cli console` promptly. To actually run a bake and capture
its errors (verified recipe, vex-ee 2026-06):
1. Reflection-invoke `Unity.Scenes.Editor.SubSceneInspectorUtility.ForceReimport`.
2. `AssetDatabaseExperimental.ProduceArtifact(new ArtifactKey(
   AssetDatabase.GUIDFromAssetPath("Assets/SceneDependencyCache/<guid>.sceneWithBuildSettings"),
   typeof(SubSceneImporter)))` to force synchronous artifact production.
3. Read `Logs/AssetImportWorkerHW*.log` via File.ReadAllText INSIDE exec and grep
   for the error text (it later mirrors to console as `[Worker#] ...`).
CACHED-ARTIFACT TRAP (verified): `Assets/SceneDependencyCache/` can hold MULTIPLE
`.sceneWithBuildSettings` entries; producing the wrong one returns a cached
artifact silently and NO bake runs — which looks exactly like "the recipe is
broken". Produce artifacts for ALL entries, or match the right GUID via Editor.log.
Also: the type name `GUID` may not resolve in the exec compiler — obtain GUID
values via `AssetDatabase.GUIDFromAssetPath(...)` instead of constructing them.
`SubSceneImporter` is INTERNAL — `typeof` fails in exec; use
`Type.GetType("Unity.Scenes.Editor.SubSceneImporter, Unity.Scenes.Editor")`.
`IPlayableAsset` lives in `UnityEngine.Playables`, not UnityEngine.Timeline.

### 5q. EntityLink Mutations Persist (No Revert Mechanism)
`EntityLinkMutateSystem` is edge-triggered (fires once at clip activation; clip
length irrelevant), mutates the root's EntityLinkEntry buffer in place under
EntityLock, and has NO deactivation path — mutations survive clip end, timeline
end, everything. Temporary link changes require a compensating clip (Swap is
self-inverse — the cleanest undo). Runtime Assign/Swap can append duplicate keys
that bypass bake-time validation; Remove clears ALL entries for a key. Mutate runs
UpdateBefore TargetPatch/Parent with no ECB → same-frame visibility downstream.

### 5r. EntityLinkParent: Restore Means the POINTER, Never the POSE
`restoreOnEnd` (the EntityLinks family's only revert mechanism) restores the
PARENT relationship, not placement — `EntityLinkParentState` stores no pose
snapshot (verified from TransformUtility.SetupParent in BovineLabs.Core):
- Had a previous parent → at clip end the object keeps its LOCAL coordinates but
  swaps frames: it teleports into the old parent's frame at the clip's local
  offset. It does NOT return to its pre-clip placement.
- Had no parent → Parent removed, LocalTransform untouched → the clip's local
  pose is promoted to absolute WORLD pose.
- Parent applies via the EndFixedStepSimulation ECB (one fixed-step latent;
  contrast Mutate's same-frame in-place writes). The clip-pose LocalTransform
  write sits OUTSIDE the reparent guard: a resolved-but-LTW-less parent still
  teleports the object with ParentApplied=false, and exit never restores it.
- The unassigned-ECB bug (5o) is CopyTransform-ONLY; Parent's ECB is correctly
  assigned (verified).

### 5s. Essence Timeline Family: Silent EVERYWHERE (Unlike EntityLinks)
Verified vex-ee 2026-06 (package `Packages/BovineLabs.Timeline.Essence/`):
- Bake guards are ALL SILENT: Event/Intrinsic null schema bakes through with a
  Null/default key (filtered at runtime); Stat null schema silently ABORTS bake
  (`if (stat == null) return;` — no LogError). Null routeLink is also silent.
  Triage rule: a clean console proves nothing in this family — verify the baked
  YAML and the schema fields directly. (Contrast 5o: EntityLinks is loud at bake.)
- Routing: `routeTo` resolves FIRST and is mandatory (dead routeTo → effect lost
  even with a valid link); `routeLink` then hunts from the routeTo entity and WINS
  if it resolves, else graceful fallback to routeTo (still fires). Quirk: the
  Essence resolver treats Target.None like Self (resolves to the binding) —
  unlike Targets.Get(None)=Null.
- PACKAGE QUIRK: `TimelineEssenceStatData.RouteLinkKey` is baked but DEAD — the
  Stat system resolves via routeTo only; link routing works only for
  Event/Intrinsic clips.
- Events are TRANSIENT: edge-trigger per activation (duration irrelevant),
  same-frame (entity,key) amounts are pre-summed before a single
  `ConditionEventWriter.Trigger` (duplicate TryAdd would error), and the Reaction
  consumer clears buffers the same frame. Nothing persists; nothing to undo.
  value=0 trips a dev assert — use nonzero amounts (Intrinsic clips have NO such
  assert: zero/coalesced-to-zero amounts are quiet no-ops).
- INTRINSICS contrast with stats (verified): `IntrinsicWriter.Add` is LOUD at
  runtime on a key missing from EssenceConfig (schema unregistered from
  EssenceSettings), but SELF-HEALS a key missing from the entity's buffer
  (GetOrAddRefUnsafe at schema default) — no lesson-04-style silent-zero trap.
  Clamping: static schema range unless minStat/maxStat are set AND present in
  the entity's stat buffer (then floor(stat.Value) is the dynamic bound; missing
  stat falls back to static). Clamp absorbs over/underflow — clip TIME ORDER
  changes final counter values. Additions are permanent and re-fire per
  activation edge. The intrinsic-change→event bridge keys off a
  ConditionEventObject SUB-ASSET nested in the intrinsic schema — currently
  dormant in vex-ee (0 of 78 schemas have one).

### 5t. Essence Stats Are ×100 Fixed-Point with an Integer Added Sum
Verified vex-ee 2026-06 (StatModifierCalculator / StatAuthoringUtil):
- Final stat = `Σadded × (1 + Σincreased) × Π(1 + more)`, where Added is an INT
  and `ValueFloat = result / 100`. Authoring a flat Added value of 0.25 truncates
  to 0 at bake (`(int)0.25`) — author 25 to mean 0.25. Fractional flat-adds
  floor at BOTH bake and runtime.
- Percent (Increased/Reduced) and multiplicative (More/Less) modifiers MULTIPLY
  the Added sum — with a zero base they are invisible. Subtracted/Reduced/Less
  are just negated values of the same three StatModifyTypes.
- TimelineEssenceStat modifiers are while-active by construction: add on
  activation edge, value-blind remove by SourceEntity (clip entity) match on
  deactivation — symmetric under identical overlaps; removes drain before adds;
  timeline stop force-clears ClipActive so removal always fires.
- `ClipCaps.Blending` on edge-triggered clips is COSMETIC: the editor still
  generates blend-curve YAML on overlap, but no weight is ever read. Overlaps
  stack modifiers; they never blend.
- `StatsCanBeModified=false` removes the StatModifiers buffer, StatChanged, AND
  the StatDefaults blob at bake — stats frozen at baked values, runtime writes
  silently skipped.

### 5u. DistanceToStat + Cross-Cutting Resolver/Stat Refinements
Verified vex-ee 2026-06 (Packages/BovineLabs.Timeline.Distance/):
- In clips where a Target slot carries an EntityLinkSchema override but NO
  separate readRootFrom field (DistanceToStat's from/to/statTarget), the slot's
  MODE doubles as the link hunt's root — the hunt starts at `targets.Get(mode)`.
  A link override on a slot whose mode-entity can't reach a link root silently
  falls back to the mode-entity itself. (CopyTransform-style clips with an
  explicit readRootFrom differ.)
- Distance writes one live-updating `StatModifier{Added}` keyed
  SourceEntity=clipEntity, REPLACED per update (remove+add), removed on clip
  end — while-active like 5t, but updating. Distance ROUNDS its int
  (`(int)math.round`) where EssenceStat TRUNCATES — the two conversions differ.
- multiplier=1 under the ×100 stat encoding destroys precision (5.099m → 5 →
  reads 0.05); multiplier=100 is the designer rule for metric distances.
- StatModifiers WRITE side self-heals a missing stat key
  (`GetOrAddRefUnsafe` in StatModifierCalculator) — the lesson-04 silent-zero
  trap is READER-only; the key disappears again when the last modifier is
  removed. Continuous mode flags StatChanged every frame (full stat refold) —
  prefer Interval/OnStart for slow-changing uses.

### 5v. Physics Timeline Family Patterns (verified vex-ee 2026-06)
- Architecture: a TrackSystem (TimelineComponentAnimationGroup) blends clip
  configs into an enabled `Active<X>{Config}` on the BINDING entity; an
  ApplySystem consumes it at fixed step. `PhysicsProducerGroup` runs before
  PhysicsSystemGroup (PID/Ricochet/Gravity/Triggers), `PhysicsModifierGroup`
  after (FilterOverride/Drag/Kinematic/Teleport/VelocityClamp/VelocityOverride).
  One central `PhysicsTimelineBakingSystem` pre-adds the disabled Active+State
  pair to every binding target.
- The Fired enter/stay/exit machine's real while-active unit is the TIMELINE
  activation, not the clip: nothing disables Active<X> between clips, so an
  override regime runs from the first clip to timeline end; gaps hold the last
  config and the LAST clip's restore flag decides for the whole run.
- Collider mutators (FilterOverride) write INSIDE the PhysicsCollider blob via
  unsafe ptr and require `IsUnique`: baked colliders are SHARED by default —
  set ForceUnique on PhysicsShapeAuthoring or the override is skipped (editor
  warning is [BurstDiscard] → SILENT in player builds).
- Overlap rules differ per track: ClipCaps.None tracks bake no ClipWeight →
  first-writer-wins races; Blending-caps tracks use the 4-slot weighted
  register (5m). Don't assume one rule family-wide.
- Capture poisoning: a restoreOnExit=false run ends → next run's enter captures
  the MUTATED state as "original"; restore=true then restores the mutated
  values forever. Pair permanent clips with explicit compensators.
- ADD-path latency: when a Fired-machine enter path ECB-AddComponents a missing
  physics component (e.g. PhysicsGravityFactor — baked only when authoring
  factor != 1), the EndFixedStep ECB plays back AFTER that tick's physics step:
  first application is one fixed step late vs same-tick in-place mutation.
  Duplicate ECB AddComponents are harmless (set-on-duplicate semantics).
- On BLENDING tracks (e.g. GravityOverride — real 4-slot lerp, unlike Filter),
  the gap between clips holds the last clip's FULL-WEIGHT value, not neutral —
  designers expect neutral gaps and don't get them (timeline-activation scope).
- ASYMMETRIC Active-bit edges: `WriteActiveJob` ENABLES Active<X> via the
  BeginSimulation ECB, but `DisableStaleTrackJob` DISABLES by direct write at
  timeline end — any cross-track guard reading a sibling's Active bit is blind
  at a shared timeline end. Verified bite (Kinematic×Gravity): producer runs
  before modifier, so kinematic's exit clobbers gravity's restore on
  mutate-path bodies. Designer rule: never put Kinematic and Gravity tracks on
  one body in one timeline.
- PID tracks (Angular/Linear share ONE PhysicsPidApplySystem — the family's
  shared-consumer exception): output is `force×dt` into the PendingForce buffer
  (a real physical motor through inverse mass/inertia — composes with gravity/
  collisions; exit leaves momentum, no restore exists). Component casing breaks
  convention: `ActiveAngularPid`/`ActiveLinearPid` (lowercase "Pid") — guessed
  reflection names miss them. Unresolvable trackingTarget silently falls back
  to SELF (mode-dependent effect: MatchTarget≈no-op, LookAt up-rights the
  body). strengthStat multiplies via the ×100-encoded float (authored 25 →
  ×0.25); buffer-present-key-absent → multiplier 0 → PID silently dead.
  Designer gold: PidEditorUtility ships six named tuning presets
  (Snappy/Balanced/Floaty/Heavy/Precise/Rigid); doctrine = P until it reaches,
  D until stable, I only on stall.
- CAPTURE-ON-FIRST-TICK features are per-ACTIVATION, not per-clip: the
  IsInitialized gate is set by ANY prior PID tick of the activation and resets
  only on the next activation edge — an InitialLocal clip placed AFTER another
  PID clip silently seeks the stale captured position (world origin). Put
  snapshot-mode clips FIRST or on separate timeline runs.
- Fallback-to-self severity is OFFSET-DEPENDENT: modes that add an offset to
  the substituted self (TargetLocal/LineOfSight with offset≠0) become a
  perpetual self-chase force, not a no-op. FleeFromTarget fallback is a true
  no-op; World is fallback-blind; InitialLocal freezes self+offset (least-bad).
- Mixer Add semantics can differ per data type: the Linear PID mixer elects a
  DOMINANT config by higher Strength (tie → lower mode byte) for enum fields
  while summing numeric fields — cross-mode overlaps teleport the goal at the
  s=0.5 enum snap. PhysicsDragMixer.Add sums drags but keeps the FIRST
  operand's stat-strength config.
- EDGE-TRIGGERED physics clips fire once per TIMELINE ACTIVATION per binding,
  not once per clip: Fired/latch/reset state lives on the BINDING and lazy-
  resets only while Active<X> is disabled — which never happens between clips
  on one track. Two Impulse clips on one track = only the first fires.
  Stationary/unresolvable defers (AlongVelocity speed≈0, Toward dist≈0) retry
  every step WITHOUT consuming Fired — a deferred impulse can fire AFTER its
  clip window, even in a gap. Random streams (PhysicsForceRandom) persist
  across activations by design — determinism holds per-session at the same
  activation index, not across in-session re-runs. `Target.None` means "world
  frame" in space-vector contexts but "null entity" in Targets lookups — same
  enum, opposite no-ops. Negative stat multipliers pass the <1e-5 gate and
  INVERT forces.
- Drag is the family's STATELESS exception: ActiveDrag only (no State, no
  Fired machine, no restore flag) — scrub-proof and poison-proof, but velocity
  loss is permanent and gaps keep braking (timeline-activation scope). Decay is
  `exp(-drag × statMult × dt)` written in-place; stat multiplier clamped ≥0 and
  ≤1e-5 SKIPS the body (stat-zero = intended brakes-off switch; the key-absent
  trap silently disables brakes — opposite symptom of the PID dead motor).
  Negative authored drag = unguarded exponential energy injection. Tooltip's
  "50 = instant stop" is really exp(-1)≈63% loss per 50Hz step. Independent of
  built-in PhysicsDamping — both compound.
- CONFIG-BLIND exits exist: KinematicOverride's exit restores the gravity piece
  even for zeroGravity=false regimes (restoring a reset-seeded 1.0 it never
  captured) — kinematic-only timelines still perturb gravity at exit on
  factor≠1 bodies. Kinematic has NO restoreOnExit field: exit always restores;
  it cannot be made permanent. zeroVelocityOnEnter fires once per TIMELINE
  activation, not per clip. With isKinematic=1 gravity is moot anyway
  (infinite mass skips gravity; body is velocity-driven).

### 5w. Exec C# Authoring Traps (observed AI mistakes, 2026-06)
- Generic type arguments are COMPILE-TIME names only. Never put an expression
  inside angle brackets — `GetComponent<System.Type.GetType("X")>()` is a
  compile error ("Invalid expression term ')'"). Resolve the type into a
  variable and use the non-generic overload:
  `var t = System.Type.GetType("X, Asm"); var c = t != null ? go.GetComponent(t) : null;`
  Same rule for `AddComponent` and every other generic API: reflection-resolved
  types go through the `System.Type`-taking overloads.
- When a needed component type may be absent from the project, branch on the
  null `System.Type` FIRST and report the missing prerequisite — never let a
  null type reach an API call.
- Guard scene/asset edits with a play-mode check first —
  `if (UnityEditor.EditorApplication.isPlaying) return "BLOCKED|editor in play mode";`
  — `EditorSceneManager.OpenScene` throws `InvalidOperationException` during
  play mode. Never exit play mode yourself: the designer may be testing.

### 6. Third-Party / Package Upgrades and Namespaces
When updating DOTS projects, be aware that structs and enums frequently shift between Unity's built-in packages and custom extension packages (e.g., `Unity.Physics.Stateful` migrating to `BovineLabs.Core.PhysicsStates`). If a type is "missing", don't assume the package is broken—use global searches (`grep_search`) across `Library/PackageCache` to find where the type was moved, then update the `using` statements and `.asmdef` references.