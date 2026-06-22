using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

namespace BovineLabs.Timeline.Core.Editor.CliTools.Shared
{
    internal static class Capture
    {
        public const string MarkerComponent = "TimelineBeginAuthoring";

        public static DirectorPre Director(PlayableDirector d)
        {
            var pre = new DirectorPre
            {
                path = Hierarchy.PathOf(d.gameObject),
                scene = d.gameObject.scene.path,
                playableAsset = d.playableAsset != null ? AssetDatabase.GetAssetPath(d.playableAsset) : null,
                hasActivationMarker = HasMarker(d.gameObject)
            };

            if (d.playableAsset is TimelineAsset timeline)
            {
                var idx = 0;
                foreach (var track in timeline.GetOutputTracks())
                {
                    var bound = d.GetGenericBinding(track);
                    string boundPath = null, comp = null;
                    if (bound is Component bc)
                    {
                        boundPath = Hierarchy.PathOf(bc.gameObject);
                        comp = bc.GetType().Name;
                    }
                    else if (bound is GameObject bg)
                    {
                        boundPath = Hierarchy.PathOf(bg);
                        comp = "GameObject";
                    }

                    pre.bindings.Add(new BindingPre
                    {
                        index = idx++,
                        trackName = track.name,
                        trackType = track.GetType().Name,
                        boundPath = boundPath,
                        boundComponentType = comp
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

            for (var i = 0; i < arr.arraySize; i++)
            {
                var el = arr.GetArrayElementAtIndex(i);
                Object target = null;

                var end = el.GetEndProperty();
                var it = el.Copy();
                var enter = true;
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
                    idValid = target != null
                });
            }

            return list;
        }

        public static AssetExistencePre AssetExistence(string assetPath)
        {
            var folder = Path.GetDirectoryName(assetPath)?.Replace('\\', '/');
            return new AssetExistencePre
            {
                folder = folder,
                folderExisted = !string.IsNullOrEmpty(folder) && AssetDatabase.IsValidFolder(folder),
                assetExisted = AssetDatabase.LoadMainAssetAtPath(assetPath) != null
            };
        }

        public sealed class BindingPre
        {
            public string boundComponentType;
            public string boundPath;
            public int index;
            public string trackName;
            public string trackType;
        }

        public sealed class ExposedRefPre
        {
            public bool idValid;
            public string targetName;
            public string targetPath;
        }

        public sealed class DirectorPre
        {
            public List<BindingPre> bindings = new();
            public List<ExposedRefPre> exposedRefs = new();
            public bool hasActivationMarker;
            public string path;
            public string playableAsset;
            public string scene;
        }

        public sealed class AssetExistencePre
        {
            public bool assetExisted;
            public string folder;
            public bool folderExisted;
        }
    }
}