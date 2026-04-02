# Changelog
All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](http://keepachangelog.com/en/1.0.0/)
and this project adheres to [Semantic Versioning](http://semver.org/spec/v2.0.0.html).

## [1.0.0] - 2026-04-02
### Added
- Initial release of `com.bovinelabs.timeline.core`.
- Base ASMDEFs for Runtime, Authoring, and Editor.
- `Float4x4Ext.ExtractLocalTransform()` - extracts LocalTransform from float4x4 matrix.
- `TimelineReference` component - tag component for timeline-driven entities.
- `StartUI` MonoBehaviour - activates all Timeline-referenced entities on first Update.
- `TimelineReferenceAuthoring` - authoring component for TimelineReference.
