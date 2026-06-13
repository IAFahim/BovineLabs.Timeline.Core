using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

namespace BovineLabs.Timeline.Core.Editor.CliTools.Shared
{
    /// <summary>
    /// Reads real PRE-state from the editor: the exact PRE|playableAsset / PRE|binding| /
    /// PRE|exposedRef| content the skills capture by hand, plus folder/asset existence — the
    /// source every mutator's deterministic undo journal is built from.
    /// </summary>
    internal static class Capture
    {
        public const string MarkerComponent = "TimelineReferenceAuthoring";

        public sealed class BindingPre
        {
            public int index;
            public string trackName;
            public string trackType;
            public string boundPath;
            public string boundComponentType;
        }

        public sealed class ExposedRefPre
        {
            public string targetPath;
            public string targetName;
            public bool idValid;
        }

        public sealed class DirectorPre
        {
            public string path;
            public string scene;
            public string playableAsset;
            public bool hasTimelineReferenceAuthoring;
            public List<BindingPre> bindings = new List<BindingPre>();
            public List<ExposedRefPre> exposedRefs = new List<ExposedRefPre>();
        }

        public static DirectorPre Director(PlayableDirector d)
        {
            var pre = new DirectorPre
            {
                path = Hierarchy.PathOf(d.gameObject),
                scene = d.gameObject.scene.path,
                playableAsset = d.playableAsset != null ? AssetDatabase.GetAssetPath(d.playableAsset) : null,
                hasTimelineReferenceAuthoring = HasMarker(d.gameObject),
            };

            if (d.playableAsset is TimelineAsset timeline)
            {
                int idx = 0;
                foreach (var track in timeline.GetOutputTracks())
                {
                    var bound = d.GetGenericBinding(track);
                    string boundPath = null, comp = null;
                    if (bound is Component bc) { boundPath = Hierarchy.PathOf(bc.gameObject); comp = bc.GetType().Name; }
                    else if (bound is GameObject bg) { boundPath = Hierarchy.PathOf(bg); comp = "GameObject"; }

                    pre.bindings.Add(new BindingPre
                    {
                        index = idx++,
                        trackName = track.name,
                        trackType = track.GetType().Name,
                        boundPath = boundPath,
                        boundComponentType = comp,
                    });
                }
            }

            pre.exposedRefs = ReadExposedRefs(d);
            return pre;
        }

        public static bool HasMarker(GameObject go)
        {
            foreach (var c in go.GetComponents<Component>())
                if (c != null && c.GetType().Name == MarkerComponent)
                    return true;
            return false;
        }

        private static List<ExposedRefPre> ReadExposedRefs(PlayableDirector d)
        {
            var list = new List<ExposedRefPre>();
            var so = new SerializedObject(d);
            var arr = so.FindProperty("m_ExposedReferences.m_References");
            if (arr == null || !arr.isArray) return list;

            for (int i = 0; i < arr.arraySize; i++)
            {
                var el = arr.GetArrayElementAtIndex(i);
                UnityEngine.Object target = null;

                var end = el.GetEndProperty();
                var it = el.Copy();
                bool enter = true;
                while (it.NextVisible(enter) && !SerializedProperty.EqualContents(it, end))
                {
                    enter = false;
                    if (it.propertyType == SerializedPropertyType.ObjectReference)
                    {
                        target = it.objectReferenceValue;
                        break;
                    }
                }

                string path = null;
                if (target is Component c) path = Hierarchy.PathOf(c.gameObject);
                else if (target is GameObject g) path = Hierarchy.PathOf(g);

                list.Add(new ExposedRefPre
                {
                    targetPath = path,
                    targetName = target != null ? target.name : null,
                    idValid = target != null,
                });
            }
            return list;
        }

        public sealed class AssetExistencePre
        {
            public string folder;
            public bool folderExisted;
            public bool assetExisted;
        }

        public static AssetExistencePre AssetExistence(string assetPath)
        {
            var folder = Path.GetDirectoryName(assetPath)?.Replace('\\', '/');
            return new AssetExistencePre
            {
                folder = folder,
                folderExisted = !string.IsNullOrEmpty(folder) && AssetDatabase.IsValidFolder(folder),
                assetExisted = AssetDatabase.LoadMainAssetAtPath(assetPath) != null,
            };
        }
    }
}
