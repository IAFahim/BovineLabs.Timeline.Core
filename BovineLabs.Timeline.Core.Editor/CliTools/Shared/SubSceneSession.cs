using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace BovineLabs.Timeline.Core.Editor.CliTools.Shared
{
    internal sealed class SubSceneSession : IDisposable
    {
        private bool disposed;

        private bool openedByUs;

        private SubSceneSession()
        {
        }

        public string SubscenePath { get; private set; }
        public string ParentPath { get; private set; }
        public Scene Subscene { get; private set; }

        public object Error { get; private set; }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;

            if (openedByUs)
            {
                if (Subscene.IsValid() && Subscene.isLoaded)
                    EditorSceneManager.CloseScene(Subscene, true);

                var current = EditorSceneManager.GetActiveScene();
                if (!string.IsNullOrEmpty(ParentPath) && current.path != ParentPath)
                    EditorSceneManager.OpenScene(ParentPath, OpenSceneMode.Single);
            }
            else
            {
                var parent = EditorSceneManager.GetSceneByPath(ParentPath);
                if (parent.IsValid() && parent.isLoaded)
                    EditorSceneManager.SetActiveScene(parent);
            }
        }

        public static SubSceneSession Open(string subscenePathOrNull)
        {
            var s = new SubSceneSession();
            try
            {
                s.OpenInternal(subscenePathOrNull);
            }
            catch (ToolException e)
            {
                s.Error = ToolEnvelope.FromException(e);
            }
            catch (Exception ex)
            {
                if (s.openedByUs && s.Subscene.IsValid() && s.Subscene.isLoaded)
                    EditorSceneManager.CloseScene(s.Subscene, true);
                if (!string.IsNullOrEmpty(s.ParentPath))
                {
                    var current = EditorSceneManager.GetActiveScene();
                    if (current.path != s.ParentPath)
                        EditorSceneManager.OpenScene(s.ParentPath, OpenSceneMode.Single);
                }

                s.openedByUs = false;
                s.Error = ToolEnvelope.Error("BAD_VALUE", ex.Message);
            }

            return s;
        }

        private void OpenInternal(string subscenePath)
        {
            if (Application.isPlaying || EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Error = ToolEnvelope.Error("PLAY_MODE_BLOCKED",
                    "Editor is in play mode; scene/asset authoring is blocked. Stop play mode first.");
                return;
            }

            var active = EditorSceneManager.GetActiveScene();
            if (!active.IsValid())
            {
                Error = ToolEnvelope.Error("MISSING_PREREQUISITE", "No active scene.");
                return;
            }

            ParentPath = active.path;

            if (string.IsNullOrEmpty(subscenePath))
                subscenePath = DetectSubscenePath(active);
            if (string.IsNullOrEmpty(subscenePath))
            {
                Error = ToolEnvelope.Error("MISSING_PREREQUISITE",
                    "No SubScene found in the active scene (and none specified).");
                return;
            }

            SubscenePath = subscenePath;

            var existing = EditorSceneManager.GetSceneByPath(subscenePath);
            if (existing.IsValid() && existing.isLoaded)
            {
                Subscene = existing;
                openedByUs = false;
            }
            else
            {
                Subscene = EditorSceneManager.OpenScene(subscenePath, OpenSceneMode.Additive);
                openedByUs = true;
            }

            if (!Subscene.IsValid())
            {
                Error = ToolEnvelope.Error("NOT_FOUND", $"Could not open subscene '{subscenePath}'.");
                return;
            }

            EditorSceneManager.SetActiveScene(Subscene);
        }

        public GameObject Find(string hierarchyPath)
        {
            if (string.IsNullOrEmpty(hierarchyPath) || !Subscene.IsValid()) return null;
            var parts = hierarchyPath.Split('/');
            foreach (var root in Subscene.GetRootGameObjects())
            {
                if (root.name != parts[0]) continue;
                if (parts.Length == 1) return root;
                var rest = string.Join("/", parts, 1, parts.Length - 1);
                var child = root.transform.Find(rest);
                if (child != null) return child.gameObject;
            }

            return null;
        }

        public void Save()
        {
            if (Subscene.IsValid())
                EditorSceneManager.SaveScene(Subscene);

            RebakeUtil.ReimportOpenSubScenes();
        }

        public static string DetectSubscenePath(Scene scene)
        {
            foreach (var root in scene.GetRootGameObjects())
            {
                var found = DetectInTransform(root.transform);
                if (!string.IsNullOrEmpty(found)) return found;
            }

            return null;
        }

        private static string DetectInTransform(Transform t)
        {
            foreach (var c in t.GetComponents<Component>())
            {
                if (c == null) continue;
                if (c.GetType().Name == "SubScene")
                {
                    var path = ResolveSceneAssetPath(c);
                    if (!string.IsNullOrEmpty(path)) return path;
                }
            }

            for (var i = 0; i < t.childCount; i++)
            {
                var found = DetectInTransform(t.GetChild(i));
                if (!string.IsNullOrEmpty(found)) return found;
            }

            return null;
        }

        private static string ResolveSceneAssetPath(Component subSceneComponent)
        {
            var so = new SerializedObject(subSceneComponent);
            var it = so.GetIterator();
            while (it.NextVisible(true))
                if (it.propertyType == SerializedPropertyType.ObjectReference &&
                    it.objectReferenceValue != null &&
                    it.objectReferenceValue.GetType().Name == "SceneAsset")
                    return AssetDatabase.GetAssetPath(it.objectReferenceValue);

            return null;
        }
    }
}