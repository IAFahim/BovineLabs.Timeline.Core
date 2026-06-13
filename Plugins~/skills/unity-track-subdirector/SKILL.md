---
name: unity-track-subdirector
description: Master of SubDirectorTrack + SubDirectorClip/SubTimelineClip (BovineLabs core timeline package com.bovinelabs.timeline) — timeline nesting via composite timers, scene-vs-asset binding rules, and the no-TimelineReferenceAuthoring-on-sub-directors rule. Portable to any project containing the package; worked example from vex-ee. Use when a designer asks to "play a cutscene inside a cutscene" or "reuse a timeline as a building block".
---

# SubDirectorTrack specialist

## 1. SCOPE

You are the specialist for **`SubDirectorTrack`** and its two clip types **`SubDirectorClip`** and
**`SubTimelineClip`** from the CORE BovineLabs timeline package `com.bovinelabs.timeline` (namespace
`BovineLabs.Timeline.Authoring`; runtime scheduling folder/namespace misspelled `Schedular`). Scope:
exactly this track family — nesting one timeline inside another via bake-time **composite timers**
(affine clocks `subTime = hostTime × Scale + Offset`), the two binding models (scene sub-director with
its OWN tables vs asset-side `TrackBindings`), and the activation rule for sub-directors (deliberately
NO TimelineReferenceAuthoring). Stage construction belongs to `unity-stage-foundations`; nested
timelines' CONTENT to the respective track specialists.

**Operate per `unity-timeline-track-authoring`; behave per `unity-agent-protocol`; use the editor per
`unity-cli`.** The shared skill owns discovery (§1), the SubScene bracket (§2), the undo structure
(§3), and verification (§4); this skill keeps ONLY the SubDirector-unique facts below.

Shape twist vs the generic bracket: the track takes **NO `[TrackBindingType]` / no generic binding**
(bindings belong to the nested content), and `SubDirectorClip` needs a **second director** (the
scene-side sub-director, recipe 4.1) plus **two-sided ExposedReference** wiring (asset GUID + the
HOST director's scene table). Carry those deltas into the shared bracket.

## 2. PORTABLE SEMANTICS

True in ANY project containing the core timeline package. Provenance tags say where a fact was PROVEN,
not where it applies. (All verified vex-ee 2026-06: reflection dumps, package-source quotes, raw YAML
reads, fresh-load read-backs via `unity-cli exec`. No play mode: runtime claims are source-derived;
the depth≥2 caveat is explicitly a source-reading observation, NOT runtime-tested.)

Types (assembly `BovineLabs.Timeline.Authoring` — ships in PackageCache, not necessarily under `Packages/`; vex-ee location in §5):

| Type | Base | Facts |
|---|---|---|
| `SubDirectorTrack` | `DOTSTrack` | `[TrackColor(0.5,0.1,0.5)]`, `[TrackClipType(SubDirectorClip)]`, `[TrackClipType(SubTimelineClip)]`, `[DisplayName("DOTS/Sub Director Track")]`. **NO `TrackBindingType`**, no own fields — pure container (live: `outputTargetType=null`, `GetGenericBinding=null`, correct and permanent — don't "fix" it). Bindings belong to the nested content, never to this track. |
| `SubDirectorClip` | `DOTSClip` | `ClipCaps.ClipIn \| SpeedMultiplier` (live: `caps=ClipIn, SpeedMultiplier`; no Blending). The scene-friendly clip. |
| `SubTimelineClip` | `DOTSClip` | Same caps. Private `OnValidate()` → `TrackBindings.SyncToTimeline(Timeline)`. The asset-only clip. |
| `TrackKeyBindings` | struct | `List<TrackKeyPair> Bindings`; `FindObject(track)` keys by TrackAsset identity (`FindIndex(x => x.Track == asset)`); `SyncToTimeline` scaffolds missing pairs / prunes dead ones, PRESERVING `Target` on survivors. |
| `TrackKeyBindings.TrackKeyPair` | struct | `TrackAsset Track; UnityEngine.Object Target` — `Target` is a **plain direct Object reference** (the scene-vs-asset boundary, see traps). |

### Clip fields
| Member | Type | Default | Meaning |
|---|---|---|---|
| `SubDirectorClip.SubDirector` | `ExposedReference<PlayableDirector>` | unset (`exposedName=':0'`) | Resolved through the HOST director's scene table at bake; null → SILENT skip |
| `SubDirectorClip.DefaultClipDuration` | `[HideInInspector] double` | **5** (`TimelineClip.kDefaultClipDurationInSeconds`) | **ERRATUM: an earlier curriculum draft said 1; verified 5** (fresh clip seeded `duration=5`, YAML `DefaultClipDuration: 5`). Seeds UI clip length only; the editor updates it to the referenced timeline's duration |
| `SubTimelineClip.Timeline` | `TimelineAsset` | null | The embedded asset; null → SILENT skip. Asset→asset ref, persists fine |
| `SubTimelineClip.TrackBindings` | `TrackKeyBindings` | empty | Per-track targets for the embedded asset's DOTS tracks — ASSET targets only |
| `SubTimelineClip.duration` (override) | `double` | `Timeline != null ? Timeline.duration : base.duration` | Property-level inheritance ONLY; the TimelineClip is seeded 5s at `CreateClip`, BEFORE you can assign `Timeline` from code — set `clip.duration` yourself |

Package sample reference shape (`Sample~/Timelines/Timeline1.playable`):
`m_Name: SubDirectorClip` / `SubDirector: {exposedName: 3db83e7dce6d507429222c35393a05e3,
defaultValue: {fileID: 0}}` / `DefaultClipDuration: 5`.

### Bake path (source-quoted)
```csharp
// SubDirectorClip.Bake — silent-skip guard + director switch:
var player = this.SubDirector.Resolve(context.Director);   // host director's scene table
if (player != null) { ...                                  // unset/missing -> SILENT skip
    context = context.CreateCompositeTimer();
    context.Director = player;                             // nested timeline resolves against SUB-director
    PlayableDirectorBaker.ConvertPlayableDirector(context, context.Clip!.GetSubTimelineRange());
// SubTimelineClip.Bake — silent-skip guard + NO director:
if (this.Timeline != null) { ...                           // null -> SILENT skip
    var newContext = context.CreateCompositeTimer();
    newContext.Director = null;                            // bindings come ONLY from TrackBindings
    foreach (var track in this.Timeline.GetDOTSTracks(context.Baker)) { ...
        newContext.Binding = context.GetBinding(track, this.TrackBindings.FindObject(track));
        PlayableDirectorBaker.ConvertTrack(newContext, range);
```

`GetBinding`: `trackBinding != null ? Baker.GetEntity(...) : Entity.Null` — a null target means the
embedded track bakes with `Binding = Entity.Null`, a silent no-op.

### CompositeTimer — fields and the affine math
`CreateCompositeTimer` (ConversionContext.cs) bakes, per nesting clip: `offset = clipIn −
start·timeScale`, `scale = timeScale`, composed with any ancestor composite and re-anchored to the
ROOT timer; runtime is TimerUpdateSystem.cs (`Schedular` namespace):

```csharp
if (context.SharedContextValues.CompositeTimers.TryGetValue(context.Timer, out var parent))
{   parentScale = parent.Scale; parentOffset = parent.Offset;
    masterTimer = parent.SourceTimer; }        // <- chains to the ROOT timer, not the immediate parent
var composite = new CompositeTimer {
    SourceTimer = masterTimer, Offset = offset + (parentOffset * scale), Scale = scale * parentScale,
    ActiveRange = { Start = (range.Start / parentScale) - parentOffset,
                    End   = (range.End   / parentScale) - parentOffset } };
// runtime, every frame:
timer.Time = (source.Time * composite.Scale) + composite.Offset;
timer.DeltaTime = source.DeltaTime * composite.Scale;
var active = source.Time >= composite.ActiveRange.Start && source.Time < composite.ActiveRange.End;
```

Root timers tick by clock (`[WithNone(typeof(CompositeTimer))]` — composites never tick themselves);
composites are recomputed every frame by recursive descent through `CompositeTimerLink` buffers,
flipping `TimelineActive` on the composite and all linked track/clip entities as the parent clock
enters/leaves ActiveRange. Generic deep-nesting algebra (clip at start s, clipIn c, timeScale k under
a parent of Scale P, Offset Q): `SourceTimer=ROOT`, `Offset=(c − s·k) + Q·k`, `Scale=k·P` — bake-time
composition is exact root-relative math. Worked numbers: §5.

### Traps & DO/DON'T (each proven live or source-quoted, vex-ee 2026-06)

- **DON'T put scene objects in `TrackKeyPair.Target` — they die `{fileID: 0}`** — a scene Transform
  assigned to `Target` read back fine in memory (the lie), but after `SaveAssets()` the YAML held
  `Target: {fileID: 0}` and a fresh load read `Target=NULL`, no console warning. (The unity-cli 5d
  asset→scene-ref trap; the shared skill's bracket exists to guard exactly this.)
- **DO use asset targets in TrackBindings — they survive** — a TimelineAsset target persisted as
  `{fileID: 11400000, guid: …, type: 2}` and fresh-loaded intact (§5).
- **DO treat SubDirectorClip as THE scene-friendly nesting clip** — its scene-side sub-director owns
  real scene binding tables + `m_ExposedReferences`, reached via ExposedReference, the one legal
  asset→scene bridge; SubTimelineClip is asset-only.
- **DON'T put TimelineReferenceAuthoring on sub-directors** — `PlayableDirectorBaker` bakes EVERY
  director with a TimelineAsset unconditionally (only gate: `if (director.playableAsset is not
  TimelineAsset) return;`), so the sub-timeline bakes twice regardless (independently + nested); the
  marker's only consumer is ACTIVATION logic (`WithAll<TimelineReference>` + `WithDisabled<TimelineActive>`
  → `SetComponentEnabled<TimelineActive>(true)`). With the marker, the independent root-timer copy
  plays IN PARALLEL with the host-driven nested copy, double-driving the same target; without it, it
  sits inert (`TimelineActive` present-but-disabled from bake). The HOST keeps its marker — that
  starts the nested tree. `playOnAwake` is never read by the baker. (vex-ee caveat: the one consumer
  found was StartUI.cs, absent from its scenes — the inert-by-default argument holds regardless.)
- **DON'T expect warnings on unset references — silent skip** — `if (player != null)` /
  `if (this.Timeline != null)` are the only guards; temp clips with unset refs saved, reloaded,
  baked nothing, console clean.
- **DON'T create nesting cycles — there is NO recursion guard** — `SubDirectorClip.Bake →
  ConvertPlayableDirector → ConvertTracks → SubDirectorClip.Bake → …` has no depth counter or
  visited set (all three files quoted); self-(transitive-)nesting means unbounded bake recursion —
  keep nesting a DAG, never point a clip at an ancestor timeline.
- **DON'T trust depth≥2 with non-identity transforms (source-derived, NOT runtime-tested)** —
  `TimerUpdateSystem` passes the PARENT's already-transformed time to the child while child
  Offset/Scale are baked root-relative (`SourceTimer` is never read in the update), double-applying
  the parent transform when parent Offset≠0 or Scale≠1; at depth 1 parent time == root time so all is
  exact — keep clips at start=0/clipIn=0/timeScale=1 beyond one level until proven otherwise.
- **DO set `clip.duration` yourself, and remember DefaultClipDuration = 5, not 1** — both are
  CreateClip-time seeds only (clip-fields table; vex-ee numbers in §5).
- **DO rely on director tables surviving playableAsset swaps** — binding and exposed-reference tables
  are keyed by track/GUID; all pre-existing entries read back intact after swap-and-restore
  (re-confirmed across three lessons, §5).

## 3. DISCOVERY

Run the shared discovery preamble (`unity-timeline-track-authoring` §1) with these substitutions:
- **D1 package check**: type `BovineLabs.Timeline.Authoring.SubDirectorTrack`, assembly
  `BovineLabs.Timeline.Authoring`; absent → `MISSING_PREREQUISITE|SubDirectorTrack not found -
  BovineLabs core timeline package absent here`.
- **D3 director CLASSIFICATION (track-unique)**: print per director whether its GameObject carries
  `TimelineReferenceAuthoring`. The **HOST** carries the marker (the activation root); **SUB-DIRECTOR**
  candidates are directors WITHOUT it. Zero hosts → missing prerequisite (protocol §6).
- **D4 bind target**: NONE for this track (it takes no binding). Instead discover the **nested
  content** — the timeline to nest must ALREADY exist: `AssetDatabase.FindAssets("t:TimelineAsset")`,
  read real paths, choose with the designer. Building nested CONTENT is the content specialist's job
  (missing-prerequisite boundary); creating an empty host timeline + this track is yours. If recipe
  4.1 will run, confirm the chosen sub-director name is unused and record `PRE|subDirectorExisted=<bool>`.
- **D5 pre-state (track-unique addition)**: capture the HOST director's `PRE|playableAsset` and
  `PRE|binding|` lines AS USUAL, PLUS one `PRE|exposedRef|<guid>: <fileID>` line per EXISTING
  `m_ExposedReferences` entry — read the SubScene file text inside exec (`File.ReadAllText`) and quote
  the `m_References` block. Your minted GUID must be the ONLY entry added vs this capture.

## 4. CANONICAL RECIPES (the bracket's track-specific middle)

Drive each through the shared SubScene bracket (`unity-timeline-track-authoring` §2): one logical
change per exec block, print `PRE|` before mutating, save inside the block, verify from a fresh load
(§7). The SubDirector deltas vs the generic bracket: the track takes NO `SetGenericBinding`;
SubDirectorClip needs recipe 4.1's second director AND a SECOND scene-side save for the ExposedReference.

**4.1 The scene-side sub-director** (needed for SubDirectorClip; inside the bracket):

```csharp
var subDirectorName = "<CHOSEN>"; var nestedAssetPath = "<DISCOVERED>";  // D4
// CAPTURE (print + journal): PRE|subDirectorExisted=<bool>
var go = new UnityEngine.GameObject(subDirectorName);   // Transform + PlayableDirector ONLY;
                                                         // optionally parent under a discovered stage root
var sub = go.AddComponent<UnityEngine.Playables.PlayableDirector>();
sub.playableAsset = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.Playables.PlayableAsset>(nestedAssetPath);
sub.playOnAwake = false;
// the sub-director owns ITS OWN bindings — one per DOTS track of the nested asset:
//   sub.SetGenericBinding(<track>, <DISCOVERED SubScene object/component>);
// NO TimelineReferenceAuthoring on this object (trap, §2).
UnityEditor.EditorUtility.SetDirty(sub); UnityEditor.SceneManagement.EditorSceneManager.SaveScene(subScene);
```

**4.2 "Play that scene cutscene inside this one" (SubDirectorClip, scene-friendly).** Two-sided
ExposedReference wiring = TWO saves (asset side, then scene side):

```csharp
var assetFolder = "<CHOSEN>"; var assetPath = assetFolder + "/<Name>.playable";
// ---- asset side (no SubScene needed). CAPTURE: PRE|folderExisted=<bool> PRE|assetExisted=<bool>
var timeline = UnityEngine.ScriptableObject.CreateInstance<UnityEngine.Timeline.TimelineAsset>();
UnityEditor.AssetDatabase.CreateAsset(timeline, assetPath);
var track = timeline.CreateTrack<BovineLabs.Timeline.Authoring.SubDirectorTrack>(null, "<trackName>");
var clip = track.CreateClip<BovineLabs.Timeline.Authoring.SubDirectorClip>();
clip.start = 0; clip.displayName = "<clipName>";       // seeded duration=5 (DefaultClipDuration)
clip.clipIn = 0.5; clip.timeScale = 2;                 // <CHOSEN> — caps: ClipIn|SpeedMultiplier, no blending
var ca = (BovineLabs.Timeline.Authoring.SubDirectorClip)clip.asset;
var er = ca.SubDirector; er.exposedName = System.Guid.NewGuid().ToString(); ca.SubDirector = er; // mint GUID
UnityEditor.EditorUtility.SetDirty(ca); UnityEditor.AssetDatabase.SaveAssets();  // save #1 (asset side)
// ---- scene side (SubScene bracket). CAPTURE first: the full §3 D5 PRE| set.
var host = /* resolve host director per Name resolution rule */;
host.SetReferenceValue(er.exposedName, subDirector);   // scene table gains <guid>: <fileID>
host.playableAsset = timeline;                         // if this is to be the host's main asset
UnityEditor.EditorUtility.SetDirty(host);
UnityEditor.SceneManagement.EditorSceneManager.SaveScene(subScene);   // save #2 (scene side)
```

**4.3 "Embed a timeline asset as a building block" (SubTimelineClip, asset-only):**

```csharp
var clip2 = track.CreateClip<BovineLabs.Timeline.Authoring.SubTimelineClip>();
clip2.start = 5; clip2.displayName = "<clipName>";
var st = (BovineLabs.Timeline.Authoring.SubTimelineClip)clip2.asset;
st.Timeline = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.Timeline.TimelineAsset>("<DISCOVERED>");
clip2.duration = st.Timeline.duration;   // set yourself — TimelineClip was seeded 5 before Timeline existed
// TrackBindings: one TrackKeyPair per DOTS track of the embedded asset (OnValidate's SyncToTimeline
// scaffolds the list in the editor). Target may ONLY be an ASSET Object — scene objects null silently
// to {fileID: 0} on save (trap, §2). For scene targets use 4.1/4.2, or an EntityLinks track inside
// the embedded timeline.
UnityEditor.AssetDatabase.SaveAssets();
```

Timings/values above are example choices, not package constants; verify per the shared verification
protocol (§4) in SEPARATE blocks before claiming success.

## 5. UNDO + VERIFICATION DELTA

Undo per `unity-timeline-track-authoring` §3, verification per §4. The SubDirector inventory adds these
artifacts beyond the standard created-asset/folder/director-playableAsset set, and they reorder the
restore:
- a possibly-created **second director object** `<subDirectorName>` (recipe 4.1): GameObject +
  PlayableDirector; its `playableAsset` pointer + OWN bindings die with the object, but it MUST be
  inventoried — a forgotten sub-director keeps the nested asset referenced and, if anyone later adds
  the activation marker, double-drives the nested timeline (trap, §2). Destroy via `DestroyImmediate`
  ONLY if `PRE|subDirectorExisted=false`.
- an added **exposed-reference entry** `<minted GUID> → <sub-director fileID>` in the HOST director's
  scene-side `m_ExposedReferences` — it SURVIVES deleting the asset; remove it explicitly with
  `host.ClearReferenceValue(new UnityEngine.PropertyName("<minted GUID>"))` (inverse of
  `SetReferenceValue`). (vex-ee: left as permanent stage state, so `ClearReferenceValue` was untested
  there — verify removal via raw YAML afterward; all `PRE|exposedRef|` entries must remain.)
- a possibly-created **sub-timeline asset** — only if the job CREATED the nested TimelineAsset instead
  of reusing one; journal it as a separate created asset.
- **NO generic-binding entry for the track** (it takes no binding); pre-existing entries untouched.

**ORDER (extends the shared restore-director-first rule):** restore the HOST director FIRST
(playableAsset + remove the minted exposed-ref entry) → THEN destroy the created sub-director (it
references the nested asset; remove referencers before deleting assets) → THEN delete the created host
`.playable` → THEN any job-created sub-timeline asset (its only referencer, the host clip, is now gone).

**Verification additions** beyond the shared §4 dump: confirm `exposedName:` non-empty on every
SubDirectorClip; `TrackKeyPair.Target` entries are `{fileID: 11400000, guid: …}` (asset) not
`{fileID: 0}` (dead); the minted GUID appears in the HOST's `m_ExposedReferences` with
`GetReferenceValue → idValid=True`; the sub-director shows `playOnAwake=False` and
`TimelineReferenceAuthoring=False`; `GetGenericBinding(subDirectorTrack) == null`.

## 6. WORKED EXAMPLE (vex-ee training stage) — example environment; rediscover, never assume

Delta vs the shared stage (`unity-timeline-track-authoring` §5; project `/home/i/GitHub/vex-ee`,
host `Stage_Director` carrying the activation marker). Core sources:
`Library/PackageCache/com.bovinelabs.timeline@4331b95d072a/`.

- Permanent lesson-06 stage addition, the scene-side sub-director `Stage_SubDirector`:
  ```
  FRESH|Stage_SubDirector|components=UnityEngine.Transform,UnityEngine.Playables.PlayableDirector
  FRESH|playableAsset=Assets/Training/02-transform-scale-track/ScaleMastery.playable|playOnAwake=False
  FRESH|binding[ScaleTrack]=Stage_Actor (UnityEngine.Transform)
  FRESH|TimelineReferenceAuthoring=False
  ```
- Asset built: `Assets/Training/06-subdirector-track/NestingMastery.playable` — track `NestingTrack`;
  clip A_SubDirector 0–5s clipIn=0.5 timeScale=2 →
  `exposedName=6902b47b-85fa-401f-bf2c-9cb9dd947e28` resolved to Stage_SubDirector's PlayableDirector
  (scene side: `6902b47b-…: {fileID: 808433437}` in Stage_Director's `m_ExposedReferences`, alongside
  lesson-03's pre-existing rotation entry `cca01140-…`); clip B_SubTimeline 5–11s →
  `Timeline=PositionMastery.playable`, TrackBindings `PositionTrack→null` on disk — by design dead
  (scene-target trap demo).
- Duration inheritance: after `Timeline=PositionMastery`, the clip ASSET's `duration` property read
  `5.999999999999` (= PositionMastery's 6s); the TimelineClip kept the 5s seed until set 5–11.
- Asset-target proof used `ScaleMastery` as `TrackKeyPair.Target`: persisted as
  `{fileID: 11400000, guid: 68555533298d86b528c4c1436f8f5ed2, type: 2}`, fresh-loaded intact, then
  reverted to null.
- Composite-timer worked numbers (Clip A: clipIn=0.5, start=0, timeScale=2, window 0–5s):
  `offset = 0.5 + (−0 × 2) = 0.5`; parent is the host ROOT timer (`parentScale=1, parentOffset=0`) →
  **`CompositeTimer { SourceTimer=root, Offset=0.5, Scale=2, ActiveRange=[0,5) }`**;
  `subTime = hostTime × 2 + 0.5` (host t=0 → 0.5s; t=1 → 2.5s; t=2.25 → 5.0s, ScaleMastery's 5s content
  exhausted; timer runs on to 10.5 at host t=5, matching `GetSubTimelineRange = [0.5, 10.5]`).
- Post-training restore: Stage_Director back on PositionMastery; all 4 scene bindings AND both
  exposed-ref entries intact. Console baseline per shared §5.
