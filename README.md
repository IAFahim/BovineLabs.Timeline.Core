# BovineLabs Timeline Core

Core utilities and shared components for BovineLabs Timeline tracks. This package serves as the foundational dependency for all specialized track packages (GameObject, Transform, Physics, Animation, Parenting, Instantiate, etc.) to ensure a modular and decoupled architecture.

> **Note**: This is an extension package maintained by [IAFahim](https://github.com/IAFahim) that builds upon the core [com.bovinelabs.timeline](https://gitlab.com/tertle/com.bovinelabs.timeline) implementation by [tertle](https://gitlab.com/tertle).

## Installation

Add this package to your Unity project using the Package Manager:
1. Open the Package Manager (`Window > Package Manager`).
2. Click the `+` button and select `Add package from git URL...`.
3. Enter the URL of this repository.

## Requirements

- **Unity**: 2022.3 or newer
- **BovineLabs Core**: 1.3.6 or newer ([gitlab.com/tertle/com.bovinelabs.core](https://gitlab.com/tertle/com.bovinelabs.core) or via [OpenUPM](https://openupm.com/packages/com.bovinelabs.core/))
- **com.unity.entities**: 1.3.0+
- **com.unity.collections**: 1.5.1+
- **com.unity.burst**: 1.8.11+
- **com.unity.mathematics**: 1.3.2+

**Dependencies**: This package extends [com.bovinelabs.timeline](https://gitlab.com/tertle/com.bovinelabs.timeline) and requires [com.bovinelabs.timeline.data](https://gitlab.com/tertle/com.bovinelabs.timeline.data).

## Overview

`BovineLabs.Timeline.Core` provides shared utilities that are used across all BovineLabs Timeline track packages. By centralizing these utilities, each track package remains lightweight and avoids redundant dependencies.

### Components

| Component | Description |
|-----------|-------------|
| `TimelineReference` | Tag component that identifies entities driven by a Timeline Director |
| `StartUI` | MonoBehaviour helper that activates timeline-referenced entities on first Update |

### Utilities

| Utility | Description |
|---------|-------------|
| `Float4x4Ext.ExtractLocalTransform()` | Extracts a `LocalTransform` from a `float4x4` matrix |

## API Reference

### TimelineReference

A tag component used to identify entities that are driven by a Timeline Director. This component enables systems like `StartUI` to locate and activate timeline-bound entities at runtime.

```csharp
using BovineLabs.Timeline.Core;
using Unity.Entities;

public struct TimelineReference : IComponentData
{
    // Tag component - no fields
}
```

### StartUI

A MonoBehaviour helper that activates all entities with `TimelineReference` on the first Update frame. This is useful for UI scenarios where you want timeline-driven elements to start only after the scene is fully initialized.

**Usage:**

1. Add the `StartUI` component to any GameObject in your scene
2. On the first `Update` call, it will:
   - Find all entities with `TimelineReference` and `TimelineActive` (disabled) components
   - Enable `TimelineActive` on each entity, starting their timeline playback
   - Disable itself to prevent re-triggering

**Manual Trigger:**

```csharp
// You can also trigger manually from code
var startUI = GetComponent<StartUI>();
startUI.TriggerTimeline();
```

### TimelineReferenceAuthoring

An authoring component that adds the `TimelineReference` tag component to baked entities. Add this MonoBehaviour to any GameObject that should be tracked by timeline utility systems.

**Usage:**

```csharp
using BovineLabs.Timeline.Core.Authoring;
using UnityEngine;

[DisallowMultipleComponent]
public class TimelineReferenceAuthoring : MonoBehaviour
{
    // Simply add to a GameObject - Baker handles entity creation
    private class Baker : Baker<TimelineReferenceAuthoring>
    {
        public override void Bake(TimelineReferenceAuthoring authoring)
        {
            var entity = GetEntity(TransformUsageFlags.None);
            AddComponent<TimelineReference>(entity);
        }
    }
}
```

### Float4x4Ext

Provides extension methods for Unity's `float4x4` matrix type.

#### ExtractLocalTransform

Extracts position, rotation, and uniform scale from a `float4x4` matrix and returns a `LocalTransform`. This is useful when working with world-space matrices and converting them to ECS `LocalTransform` format.

**Signature:**

```csharp
[MethodImpl(MethodImplOptions.AggressiveInlining)]
public static void ExtractLocalTransform(this float4x4 m, out LocalTransform localTransform)
```

**Parameters:**
- `m`: The input `float4x4` matrix
- `localTransform`: Output parameter containing the extracted `LocalTransform`

**Behavior:**
- Extracts position from the translation column (c3)
- Calculates uniform scale from column vector lengths
- Extracts rotation by normalizing the rotation columns
- Assumes uniform scaling (uses X scale for all axes)

**Example:**

```csharp
using BovineLabs.Timeline.Core;
using Unity.Transforms;

// Convert a world-space matrix to LocalTransform
float4x4 worldMatrix = GetWorldMatrix(); // Your matrix source
worldMatrix.ExtractLocalTransform(out LocalTransform localTransform);

// Use the extracted transform
EntityManager.SetComponentData(entity, localTransform);
```

**Important Notes:**
- This method assumes uniform scaling. Non-uniform scale will be approximated using the X-axis scale.
- The matrix is modified in-place during extraction (rotation columns are normalized).
- Uses aggressive inlining for Burst-optimized performance.

## Usage in Custom Track Packages

When creating a custom timeline track package, reference `BovineLabs.Timeline.Core` to access shared utilities without pulling in dependencies on other track packages.

### Example: Using Float4x4Ext in a Transform Track

```csharp
using BovineLabs.Timeline.Core;
using Unity.Transforms;

[UpdateInGroup(typeof(TimelineComponentAnimationGroup))]
public partial struct MyTransformTrackSystem : ISystem
{
    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        foreach (var (localTransform, clipData) in 
            SystemAPI.Query<RefRW<LocalTransform>>()
                .WithAll<MyTrackComponent>())
        {
            // Extract LocalTransform from a matrix
            clipData.WorldMatrix.ExtractLocalTransform(out var result);
            localTransform.ValueRW = result;
        }
    }
}
```

### Example: Using StartUI for UI Timelines

```csharp
// Scene setup:
// 1. Create a PlayableDirector with your Timeline asset
// 2. Add TimelineReferenceAuthoring to the director GameObject
// 3. Add StartUI component to any GameObject in the scene
// 4. On first Update, all timeline references will be activated
```

## Architecture

The package is organized into three assemblies:

| Assembly | Purpose |
|----------|---------|
| `BovineLabs.Timeline.Core` | Runtime components and utilities |
| `BovineLabs.Timeline.Core.Authoring` | Authoring components and bakers (Editor-only) |
| `BovineLabs.Timeline.Core.Editor` | Editor tools and inspectors (Editor-only) |

## Related Packages

**Core Packages (by tertle)**:
- [com.bovinelabs.timeline](https://gitlab.com/tertle/com.bovinelabs.timeline) - Core Timeline DOTS implementation
- [com.bovinelabs.core](https://gitlab.com/tertle/com.bovinelabs.core) - Core ECS utilities and patterns

**Extension Packages (by IAFahim)**:
- [com.bovinelabs.timeline.core](https://github.com/IAFahim/com.bovinelabs.timeline.core) - Shared utilities and components (this package)
- [com.bovinelabs.timeline.gameobjects](https://github.com/IAFahim/com.bovinelabs.timeline.gameobjects) - GameObject activation track
- [com.bovinelabs.timeline.transform](https://github.com/IAFahim/com.bovinelabs.timeline.transform) - Transform animation track
- [com.bovinelabs.timeline.physics](https://github.com/IAFahim/com.bovinelabs.timeline.physics) - Physics body animation track
- [com.bovinelabs.timeline.animation](https://github.com/IAFahim/com.bovinelabs.timeline.animation) - Animation clip animation track
- [com.bovinelabs.timeline.instantiate](https://github.com/IAFahim/com.bovinelabs.timeline.instantiate) - Entity instantiation track
- [com.bovinelabs.timeline.parenting](https://github.com/IAFahim/com.bovinelabs.timeline.parenting) - Parent/child relationship track
- [com.bovinelabs.timeline.playerinput](https://github.com/IAFahim/com.bovinelabs.timeline.playerinput) - Player input control track
- [com.bovinelabs.timeline.targets](https://github.com/IAFahim/com.bovinelabs.timeline.targets) - Target selection track

## License

Copyright (c) BovineLabs. All rights reserved.

## Support

For issues, feature requests, or questions, please use the issue tracker at the source repository.
