using UnityEditor;
using UnityEditor.Timeline;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

namespace BovineLabs.Timeline.Core.Editor
{
    public static class TimelineBinding
    {
        public static bool TryGetBoundComponent(SerializedProperty clipProperty, out Component bound)
        {
            bound = null;
            try
            {
                var targets = clipProperty.serializedObject.targetObjects;
                if (targets.Length != 1 || targets[0] is not PlayableAsset clipAsset) return false;

                var director = TimelineEditor.inspectedDirector;
                var asset = TimelineEditor.inspectedAsset;
                if (director == null || asset == null) return false;

                TrackAsset track = null;
                foreach (var t in asset.GetOutputTracks())
                {
                    foreach (var c in t.GetClips())
                        if (ReferenceEquals(c.asset, clipAsset))
                        {
                            track = t;
                            break;
                        }

                    if (track != null) break;
                }

                if (track == null) return false;

                var binding = director.GetGenericBinding(track);
                bound = binding as Component ?? (binding as GameObject)?.transform;
                return bound != null;
            }
            catch
            {
                return false;
            }
        }
    }
}