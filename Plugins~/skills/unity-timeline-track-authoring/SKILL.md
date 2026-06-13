---
name: unity-timeline-track-authoring
description: The shared ceremony every BovineLabs DOTS Timeline track skill repeats — the SubScene open/SetActive/save/try-finally-restore bracket, the unity-cli-only discovery preamble, the PRE| pre-state capture convention, the undo-appendix STRUCTURE (artifact inventory, restore-director-first ORDER, UNDO-1/2/3/4 templates), and the fresh-load verification protocol — as one canonical home with copy-paste templates using <TRACK_TYPE>/<CLIP_TYPE>/<BIND_TARGET> placeholders. A track skill says "operate per unity-timeline-track-authoring" and omits all of it. Portable to any project with the BovineLabs Timeline packages; worked example from vex-ee.
---

# Timeline track authoring — the shared ceremony

This is the one canonical home for the ~40% of boilerplate that EVERY BovineLabs DOTS
Timeline track skill repeats: how a track agent opens the SubScene, discovers names,
captures pre-state, mutates a `.playable` + the director, verifies from a fresh load,
and undoes itself. A track skill says **"Operate per `unity-timeline-track-authoring`;
behave per `unity-agent-protocol`; use the editor per `unity-cli`"** and keeps ONLY its
unique content (its track/clip type facts, field tables, runtime semantics, traps, and
2–4 track-specific clip patterns).

This skill owns the CEREMONY, not the editor mechanics and not the behavioral contract.
It does NOT duplicate:
- **`unity-cli`** — the Safe Loop, the First Command, the SubScene open/save/close
  mechanics, and the edge-case rule book (5a–5q). When a step here cites a rule (e.g.
  "5d: asset→scene refs null out silently", "5k: `SetGenericBinding` never coerces"),
  go read that rule there; this skill assumes it.
- **`unity-agent-protocol`** — §1 explore-first, §2 transactional capture/act/verify,
  §3 cleanup-before-retry, §5 the memory-card format + undo journal, §6 the
  missing-prerequisite rule, §7 undo-on-request. This skill is the Timeline-track
  *instance* of §2 and §5; the protocol is the law.

What is portable here is the SHAPE. Every name, path, id, count below is a placeholder
or a worked example from vex-ee (§ at the end) — **rediscover them in THIS project**
(protocol §1). Discovery over assumption.

## 0. The mental model — what a track agent actually changes

A DOTS Timeline track lives in TWO files, and the whole ceremony exists to mutate both
safely:

1. **A `.playable` TimelineAsset** (a project asset): holds the **track** (a
   `<TRACK_TYPE>` deriving `DOTSTrack`) and its **clip(s)** (`<CLIP_TYPE>` deriving
   `DOTSClip`) as sub-assets. Clip fields and asset→asset refs (a `StatSchemaObject`,
   an `EntityLinkSchema`) serialize fine here. A direct reference to a **scene object**
   does NOT (unity-cli 5d — it silently becomes `{fileID: 0}`); that is what
   `ExposedReference` (5g) and EntityLinks (5o) exist for.
2. **The PlayableDirector's binding table** (lives in the SUBSCENE `.unity` file): maps
   each track → the `<BIND_TARGET>` it drives, plus `director.playableAsset`. This table
   is keyed by track asset and **survives `playableAsset` swaps** (5g/5k), so capture the
   WHOLE table, not just the asset.

The bind target is a **COMPONENT** on a SubScene-baked object — which component is the
track's `[TrackBindingType]` (a `UnityEngine.Transform`, a `PhysicsBodyAuthoring`, a
`BovineLabs.Reaction.Authoring.Core.TargetsAuthoring`, …). `SetGenericBinding` stores
exactly what you pass and never coerces (5k); the baker coerces component→entity. DOTS
tracks can only animate **SubScene-baked** objects — never bind a parent-scene object
(silently nulled on save, and the parent scene is never baked).

## 1. DISCOVERY PREAMBLE (read-only — copy verbatim into a track skill's §3)

> Act only through `unity-cli exec` / `unity-cli console`; never the filesystem; never
> play mode. Follow the unity-cli Safe Loop on every mutation. Names below are
> parameters — discover them in THIS project; never assume the worked example.

The five openers every track skill runs, in order. A track skill overrides only the
TYPE NAMES and the bind-target COMPONENT; the shape is fixed.

**D1 — Confirm the package exists** (else report a missing prerequisite, protocol §6).
The robust form also sweeps loaded assemblies, for packages whose assembly-qualified
name varies:
```csharp
var t = System.Type.GetType("<TRACK_FULLNAME>, <TRACK_ASSEMBLY>");
if (t == null) foreach (var asm in System.AppDomain.CurrentDomain.GetAssemblies())
    { t = asm.GetType("<TRACK_FULLNAME>"); if (t != null) break; }
return t == null
    ? "MISSING_PREREQUISITE|<TRACK_TYPE> not found - package <PACKAGE> is absent in this project"
    : "OK|" + t.AssemblyQualifiedName + "|dataPath=" + UnityEngine.Application.dataPath;
```

**D2 — Find the active scene + SubScene(s):** run the unity-cli **First Command**.
Record `parentScenePath` and each candidate `subScenePath`.

**D3 — Find PlayableDirector(s) inside the SubScene** (read-only additive open, restore
parent after): `FindObjectsByType<UnityEngine.Playables.PlayableDirector>(FindObjectsInactive.Include,
FindObjectsSortMode.None)`. Print per director: hierarchy path, `scene.path`,
`playableAsset` path-or-null, sibling components. **Selection rule (STATE it in your
memory card):** the single director in the chosen SubScene; if several, prefer the one
carrying the project's timeline-reference authoring component (DOTS timelines need it as
an ACTIVATION gate — unity-cli rule 2); if still ambiguous, ask the designer. Zero
directors → protocol §6.

**D4 — Find/confirm the bind target by COMPONENT, never by name**, plus this track's
prerequisites:
```csharp
var holders = UnityEngine.Object.FindObjectsByType<<BIND_TARGET>>(
    UnityEngine.FindObjectsInactive.Include, UnityEngine.FindObjectsSortMode.None);
// print per holder: hierarchy path, scene.path, and the fields this track depends on
// (e.g. TargetsAuthoring slots, MotionType/Mass, StatAuthoring flags). Confirm with the
// designer if more than one plausible candidate exists.
```
ZERO holders in the SubScene → a missing prerequisite (protocol §6): you BIND the target,
you don't create it — a stage specialist must add it. Resolve any track-specific
prerequisite the SAME read-only way and report (never improvise) what is missing:
- **Schemas (stat / link):** `AssetDatabase.FindAssets("t:StatSchemaObject")` /
  `"t:EntityLinkSchema"`, then read each match's real path + live id/key. **Ids and keys
  DRIFT between projects — never trust a remembered one; NEVER create schema assets**
  (out of domain). A stat receiver needs `StatAuthoring` with `AddStats=True` AND
  `StatsCanBeModified=True` (unity-cli 5j); a `Target`-slot endpoint must be ASSIGNED
  (an unset Owner/Source slot = silent never-resolve).

**D5 — Capture the chosen director's existing state — this IS pre-state (`PRE|`)**:
```csharp
// PRE|playableAsset=<asset PATH or null>          via AssetDatabase.GetAssetPath(director.playableAsset)
// PRE|binding|<i>|<track name>|<track type>|<bound object hierarchy path + component type, or null>
//   one line per GetOutputTracks() of the CURRENT asset, via director.GetGenericBinding(track).
```
Capture the asset PATH and each track's NAME/index **even when the table looks empty** —
they are what make the undo journal replayable (UNDO-1 reloads the old asset by path and
re-binds by name/index). Binding tables survive `playableAsset` swaps (5g/5k) — capture
the WHOLE table. Record all `PRE|` lines in the undo journal (§3) BEFORE any mutation.

**Name-resolution rule (applies everywhere below):** `GameObject.Find` misses inactive
objects and is ambiguous on duplicate names. Confirm the chosen name is active and
unique in the SubScene; otherwise walk the SubScene roots to the recorded hierarchy path
(or `FindObjectsByType` filtered by `scene`) instead of bare `Find`.

## 2. THE SUBSCENE BRACKET (the create-and-wire template)

One logical change per exec block; print the `PRE|` capture before mutating (protocol §2);
save inside the same block; verify from a fresh load (§4) in a SEPARATE block. The bracket
below is identical across every track family — a track skill fills the placeholders, swaps
the clip-authoring middle for its own patterns, and changes nothing else. Set byte-backed
enum fields via `SerializedObject` using the YAML field names (unity-cli 5e: `(int)` casts
on boxed byte enums throw — use the property's `intValue`, or `System.Convert.ToInt64`).

```csharp
// ---- parameters (discovered in §1 / chosen with designer) ----
var parentScenePath = "<DISCOVERED>";  var subScenePath = "<DISCOVERED>";   // D2
var directorGoName  = "<DISCOVERED>";  var bindTargetPath = "<DISCOVERED>"; // D3 / D4 (carries <BIND_TARGET>)
var assetFolder = "<CHOSEN>";  var assetPath = assetFolder + "/<Name>.playable";  var trackName = "<CHOSEN>";

var parentScene = UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene();
var subScene = UnityEditor.SceneManagement.EditorSceneManager.OpenScene(
    subScenePath, UnityEditor.SceneManagement.OpenSceneMode.Additive);
UnityEditor.SceneManagement.EditorSceneManager.SetActiveScene(subScene);
try {
    // CAPTURE (print + journal): PRE|folderExisted=<bool> PRE|assetExisted=<bool>
    var folderExisted = UnityEditor.AssetDatabase.IsValidFolder(assetFolder);
    var assetExisted  = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(assetPath) != null;
    if (!folderExisted) { /* CreateFolder for each missing segment of assetFolder */ }

    var timeline = UnityEngine.ScriptableObject.CreateInstance<UnityEngine.Timeline.TimelineAsset>();
    UnityEditor.AssetDatabase.CreateAsset(timeline, assetPath);
    var track = timeline.CreateTrack<<TRACK_TYPE>>(null, trackName);

    // ---- TRACK-SPECIFIC MIDDLE (the only part a track skill replaces) ----
    var clip = track.CreateClip<<CLIP_TYPE>>();
    clip.start = 0; clip.duration = 1; clip.displayName = "<clipName>"; // duration override only SEEDS length (5k)
    var so = new UnityEditor.SerializedObject((UnityEngine.Object)clip.asset);
    // so.FindProperty("<field>").intValue/floatValue/objectReferenceValue = ...;  // camelCase YAML names
    so.ApplyModifiedPropertiesWithoutUndo();
    // ---------------------------------------------------------------------
    UnityEditor.AssetDatabase.SaveAssets();

    // Wire the director (binding table lives in the SCENE file -> persists)
    var director = UnityEngine.GameObject.Find(directorGoName).GetComponent<UnityEngine.Playables.PlayableDirector>();
    // CAPTURE (print + journal) BEFORE mutating: PRE|playableAsset=... and one PRE|binding| line
    // per GetOutputTracks() of the CURRENT asset (D5).
    var bindComp = UnityEngine.GameObject.Find(bindTargetPath).GetComponent<<BIND_TARGET>>(); // the COMPONENT (5k), not necessarily a Transform
    director.playableAsset = timeline;
    director.SetGenericBinding(track, bindComp);
    UnityEditor.EditorUtility.SetDirty(director);
    UnityEditor.SceneManagement.EditorSceneManager.SaveScene(subScene);
    return "OK|" + assetPath;
} finally {
    UnityEditor.SceneManagement.EditorSceneManager.SetActiveScene(parentScene);
    UnityEditor.SceneManagement.EditorSceneManager.CloseScene(subScene, false);
    UnityEditor.SceneManagement.EditorSceneManager.OpenScene(parentScenePath, UnityEditor.SceneManagement.OpenSceneMode.Single);
}
```

Bracket invariants (true for every track family):
- **Open additive → SetActiveScene → SaveScene → restore parent Single in `finally`.**
  The SubScene is the active scene only so scene-object lookups resolve for the binding;
  the `.playable` itself is a project asset (created/saved with `AssetDatabase`, not the
  scene). The `finally` ALWAYS restores the parent scene Single — even if the body throws
  (protocol §3 / unity-cli "final editor state is part of the task").
- **`CreateAsset` + `SaveAssets` for the asset; `SaveScene(subScene)` for the binding.**
  Two files, two saves. Forgetting the scene save loses the binding half silently.
- Clip starts/durations/values shown are example choices, NOT package constants.
- If direct field assignment fails to compile in the exec sandbox, set every field via
  `SerializedObject` + the YAML property names; fully-qualify all type names (5h).

## 3. UNDO APPENDIX STRUCTURE (copy this shape into a track skill's §6)

The runtime effect of a Timeline track exists only in play mode and never writes back to
authoring data — so for the authoring workflow there is **nothing runtime to undo**; undo
is purely the authoring artifacts. (Track skills whose RUNTIME effect persists within a
play session — e.g. EntityLinks parenting/mutation with no package revert — must ADD a
runtime note saying the package's own revert is the only in-session undo and the journal
below reverses only authoring artifacts.)

**Artifact inventory** for one run of §2 (state the vex-ee instance under "worked example"):
1. Created asset `<assetPath>` (.playable: TimelineAsset + track + clip sub-assets —
   `DeleteAsset` removes ALL sub-assets with the file).
2. Possibly-created folder(s) `<assetFolder>` (only if `PRE|folderExisted=false`).
3. Mutated `director.playableAsset` (restore the CAPTURED value, never "default").
4. Added/changed generic-binding entry for the new track (table lives in the SubScene
   file; survives `playableAsset` swaps — full undo must `ClearGenericBinding` it).
5. Any track-specific extras (e.g. a deliberate bake-error demo's temp clip + console
   line + new bake artifact hashes — console history and derived caches are not undoable
   state; RECORD them in the card). Most track families: **none** beyond 1–4 — the track
   never mutates editor objects, schemas, or stage state.

**ORDER (non-negotiable): restore the director FIRST, THEN delete the asset, THEN restore
any other captured scene values.** Deleting the asset while the director still points at
it leaves a dangling `{fileID: 0}` reference in the scene file instead of the captured
pre-state, and destroys the track sub-assets that `ClearGenericBinding` needs to match.

Journal entry templates (protocol §5 — fill from YOUR captures, reverse-ordered):

```csharp
// UNDO-1: restore director's captured playableAsset + binding table (SubScene bracket).
// Runs inside the SAME bracket as §2 (open <CAPTURED subScenePath> additive, SetActiveScene,
// try { body } finally { restore <CAPTURED parentScenePath> Single }).
var directorGoName = "<CAPTURED>"; var assetPath = "<CAPTURED>";
var director = UnityEngine.GameObject.Find(directorGoName).GetComponent<UnityEngine.Playables.PlayableDirector>();
var myAsset  = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.Timeline.TimelineAsset>(assetPath);
foreach (var tr in myAsset.GetOutputTracks()) director.ClearGenericBinding(tr); // entries I added for MY tracks
// restore each CAPTURED PRE|binding| line: reload the PREVIOUS playable asset by captured path,
// match tracks by name/index, re-find each bound object by captured hierarchy path, then
// director.SetGenericBinding(prevTrack, <re-found CAPTURED component>).
director.playableAsset =                                  // CAPTURED value (or null if captured null), never "default"
    null /* or AssetDatabase.LoadAssetAtPath<UnityEngine.Playables.PlayableAsset>("<CAPTURED pre path>") */;
UnityEditor.EditorUtility.SetDirty(director);
UnityEditor.SceneManagement.EditorSceneManager.SaveScene(subScene);
return "UNDONE|director restored";
```

```csharp
// UNDO-2: delete the created .playable (+ folder, only if PRE|folderExisted=false and now empty)
var assetPath = "<CAPTURED>"; var assetFolder = "<CAPTURED>"; var folderExisted = false; // <CAPTURED>
var ok = UnityEditor.AssetDatabase.DeleteAsset(assetPath);
if (!folderExisted && UnityEditor.AssetDatabase.FindAssets("", new[]{ assetFolder }).Length == 0)
    UnityEditor.AssetDatabase.DeleteAsset(assetFolder);
return "UNDONE|deleted=" + ok + "|" + assetPath;
```

```csharp
// UNDO-3: restore any other captured scene values — for MOST track families there are NONE
// beyond UNDO-1 (the track never moves editor objects, schemas, or stage state). Include
// only entries your own journal recorded; restore each to its CAPTURED value, never a default.
```

**UNDO-4 (fresh-load verification — protocol §7):** reload the SubScene additively; print
`director.playableAsset` (must equal the CAPTURED pre value) and the binding table (must
equal the captured `PRE|binding|` lines); confirm
`AssetDatabase.LoadAssetAtPath<UnityEngine.Object>("<CAPTURED assetPath>") == null`;
restore the parent scene Single; `unity-cli console --filter error` clean against the
project's known baseline.

## 4. VERIFICATION PROTOCOL (copy this shape into a track skill's §7)

Run each step in a SEPARATE exec block. In-memory state after a save is NOT evidence
(unity-cli 5d: refs null silently on save; the in-memory object lies until domain reload).
A clean console is NOT evidence either — DOTS track bakes are frequently silent even when
misconfigured (null-schema silent abort, unbound-track no-op, runtime resolution skips).
Silence is expected, not proof.

1. **Fresh-load asset dump.** `AssetDatabase.LoadAssetAtPath` the `.playable` at
   `<assetPath>`; dump every track/clip — name, start/duration, blendIn/Out, `clipCaps`,
   and ALL the fields in this track's §2 field table (the track skill names them).
2. **Raw YAML check.** `File.ReadAllText(assetPath)`: byte-backed enums as ints; numeric
   fields verbatim; asset refs present (`{fileID: 11400000, guid: …}`) vs `{fileID: 0}`
   where a ref was intended (a dropped scene ref = the 5d trap); any bake-vs-YAML unit
   quirk the track documents (e.g. degrees in YAML, radians only in baked data).
3. **Prerequisite re-check, live.** Re-dump the bound object's relevant fields and any
   schema's live id/key (they DRIFT — re-read, never trust a remembered value): the
   `[TrackBindingType]` match, `Target` slots used, `StatAuthoring` flags / key presence.
4. **Binding from a RELOADED SubScene.** Expect
   `BINDING|<trackName> (<TRACK_TYPE>) -> <bindTargetName> (<BIND_TARGET>)` —
   `GetGenericBinding` returns the COMPONENT verbatim (5k); all prior entries intact.
5. **Parent-scene restore.** End with `sceneCount=1`,
   `scene[0]=<parentScenePath>|loaded=True|active=True|dirty=False`.
6. **Console.** `unity-cli console --filter error` shows nothing new beyond the project's
   known baseline. (If a track skill deliberately demos a bake error, remove the temp clip
   and prove the clean rebake — a changed artifact hash, no new error lines, and produce
   ALL `.sceneWithBuildSettings` artifacts per unity-cli 5p.)

## 5. WORKED EXAMPLE (the shared vex-ee stage) — rediscover, never assume

Every track skill rediscovered THIS same environment; it is the canonical worked example
for the ceremony's placeholders (it is an EXAMPLE — verify each fact in YOUR project per
§1):
- Project `/home/i/GitHub/vex-ee` (`dataPath=/home/i/GitHub/vex-ee/Assets`); parent scene
  `Assets/Scenes/Main Scene.unity`; SubScene `Assets/Scenes/Main Sub Scene.unity`.
- Stage (built by `unity-stage-foundations`): `Stage_Director` — the ONLY director,
  carries `TimelineReferenceAuthoring` (the activation gate). Bind targets on
  `Stage_Actor` (capsule, ~(0,1,0); Transform / TargetsAuthoring / StatAuthoring /
  EntityLinkSource) and `Stage_PhysicsBall` (`PhysicsBodyAuthoring`, Dynamic, Mass=1).
  `Stage_Target` (cube ~(5,0,0)) is the worked-example `Targets.Target` slot.
- Each track lesson created one `.playable` under
  `Assets/Training/<NN>-<track>-track/<Name>Mastery.playable`, wired it onto
  `Stage_Director`, then **restored the director to its captured pre value** (usually
  `Assets/Training/01-transform-position-track/PositionMastery.playable`) — the binding
  table grew by one entry per lesson and prior entries stayed intact (tables survive
  `playableAsset` swaps).
- Known pre-existing console baseline (NOT your errors): UnityCliConnector HTTP server
  start, PerformanceTesting IPrebuildSetup/IPostBuildCleanup, TestResults.xml save, and
  lessons 08–10 `[Worker2]` EntityLinks bake-error demo lines.

## 6. HOW A TRACK SKILL USES THIS (the contract)

A track skill's §1 SCOPE ends with: **"Operate per `unity-timeline-track-authoring`;
behave per `unity-agent-protocol`; use the editor per `unity-cli`."** It then OMITS the
discovery preamble, the SubScene bracket, the undo-appendix structure, and the
verification protocol — citing the section here instead — and keeps ONLY:
- its TYPE FACTS (the `<TRACK_TYPE>`/`<CLIP_TYPE>` FullNames + assembly, base class,
  `[TrackBindingType]` → the `<BIND_TARGET>`, `[TrackClipType]`, `clipCaps`, attributes);
- its FIELD TABLE (every clip field, type, default, meaning — the §2 placeholders made
  concrete);
- its RUNTIME SEMANTICS + TRAPS (what the system does per frame, the silence profile,
  the DO/DON'T list — all grounded in source it actually read);
- its 2–4 TRACK-SPECIFIC CLIP PATTERNS (the §2 "track-specific middle"), as designer
  intents → wiring;
- a one-line worked example delta vs §5 (its asset path, clip names, the specific
  schema/slot it used) — anything the ceremony's §5 does not already cover.

If a fact here conflicts with what a track skill proves in source for ITS package, the
track skill's source-grounded fact wins for that package — note the divergence there.
