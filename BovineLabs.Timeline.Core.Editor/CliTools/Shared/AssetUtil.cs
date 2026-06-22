using System.IO;
using UnityEditor;
using UnityEngine;

namespace BovineLabs.Timeline.Core.Editor.CliTools.Shared
{
    internal static class AssetUtil
    {
        public static object PlayModeBlocked()
        {
            if (Application.isPlaying || EditorApplication.isPlayingOrWillChangePlaymode)
                return ToolEnvelope.Error("PLAY_MODE_BLOCKED",
                    "Editor is in play mode; asset/scene authoring is blocked. Stop play mode first.");
            return null;
        }

        public static void EnsureFolders(string assetPath)
        {
            var dir = Path.GetDirectoryName(assetPath)?.Replace('\\', '/');
            if (string.IsNullOrEmpty(dir) || AssetDatabase.IsValidFolder(dir)) return;

            var parts = dir.Split('/');
            var cur = parts[0];
            for (var i = 1; i < parts.Length; i++)
            {
                var next = cur + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(cur, parts[i]);
                cur = next;
            }
        }

        public static bool IsFolderEmpty(string folder)
        {
            if (string.IsNullOrEmpty(folder) || !AssetDatabase.IsValidFolder(folder)) return false;
            var subs = AssetDatabase.GetSubFolders(folder);
            if (subs != null && subs.Length > 0) return false;
            var assets = AssetDatabase.FindAssets(string.Empty, new[] { folder });
            return assets == null || assets.Length == 0;
        }
    }
}