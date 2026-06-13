using System.IO;
using UnityEditor;
using UnityEngine;

namespace BovineLabs.Timeline.Core.Editor.CliTools.Shared
{
    /// <summary>Asset-side helpers: play-mode guard, folder creation, empty-folder check.</summary>
    internal static class AssetUtil
    {
        /// <summary>Returns a PLAY_MODE_BLOCKED error envelope when in/entering play mode, else null.</summary>
        public static object PlayModeBlocked()
        {
            if (Application.isPlaying || EditorApplication.isPlayingOrWillChangePlaymode)
                return ToolEnvelope.Error("PLAY_MODE_BLOCKED",
                    "Editor is in play mode; asset/scene authoring is blocked. Stop play mode first.");
            return null;
        }

        /// <summary>Create every missing folder segment of an asset path's directory.</summary>
        public static void EnsureFolders(string assetPath)
        {
            var dir = Path.GetDirectoryName(assetPath)?.Replace('\\', '/');
            if (string.IsNullOrEmpty(dir) || AssetDatabase.IsValidFolder(dir)) return;

            var parts = dir.Split('/');
            string cur = parts[0]; // "Assets"
            for (int i = 1; i < parts.Length; i++)
            {
                var next = cur + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(cur, parts[i]);
                cur = next;
            }
        }

        /// <summary>True when the folder exists and contains no assets or subfolders.</summary>
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
