// <copyright file="TimelineBinding.cs" company="BovineLabs">
//     Copyright (c) BovineLabs. All rights reserved.
// </copyright>

namespace BovineLabs.Timeline.Core.Editor
{
    using UnityEditor;
    using UnityEditor.Timeline;
    using UnityEngine;
    using UnityEngine.Playables;
    using UnityEngine.Timeline;

    /// <summary> Edit-time resolution of which object a Timeline clip's track is bound to in the inspected director. </summary>
    public static class TimelineBinding
    {
        /// <summary>
        /// The <see cref="Component" /> the clip property's track is bound to in the currently inspected Timeline,
        /// or false if the property isn't a clip / there's no inspected director / no binding. Lets clip drawers
        /// resolve roles and links the same way the baker does (via the Director binding).
        /// </summary>
        public static bool TryGetBoundComponent(SerializedProperty clipProperty, out Component bound)
        {
            bound = null;
            try
            {
                var targets = clipProperty.serializedObject.targetObjects;
                if (targets.Length != 1 || targets[0] is not PlayableAsset clipAsset)
                {
                    return false;
                }

                var director = TimelineEditor.inspectedDirector;
                var asset = TimelineEditor.inspectedAsset;
                if (director == null || asset == null)
                {
                    return false;
                }

                TrackAsset track = null;
                foreach (var t in asset.GetOutputTracks())
                {
                    foreach (var c in t.GetClips())
                    {
                        if (ReferenceEquals(c.asset, clipAsset))
                        {
                            track = t;
                            break;
                        }
                    }

                    if (track != null)
                    {
                        break;
                    }
                }

                if (track == null)
                {
                    return false;
                }

                var binding = director.GetGenericBinding(track);
                bound = binding as Component ?? (binding as GameObject)?.transform;
                return bound != null;
            }
            catch
            {
                // Timeline editor APIs unavailable (asset selected outside a Timeline window) — no binding.
                return false;
            }
        }
    }
}
