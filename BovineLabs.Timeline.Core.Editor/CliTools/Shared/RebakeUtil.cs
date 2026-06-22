using System;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace BovineLabs.Timeline.Core.Editor.CliTools.Shared
{
    /// <summary>
    /// Forces any OPEN SubScene to re-bake after a programmatic .playable / clip / timeline ASSET edit.
    ///
    /// THE GAP (proven 2026-06): editing a Timeline clip asset via SerializedObject + AssetDatabase.SaveAssets()
    /// writes the asset on disk but does NOT re-bake an open SubScene, so the live ECS world silently keeps the
    /// OLD baked data (the edit looks like it "did nothing" until the user touches the clip in the inspector).
    /// The baker is NOT at fault — PlayableDirectorBaker correctly registers DependsOn(timeline/track/clip.asset).
    /// The gap is the live-conversion change tracker: for an OPEN subscene it only watches changes to objects
    /// INSIDE the open authoring scene; AssetDatabase.SaveAssets raises no event it consumes, so the registered
    /// dependency is never marked dirty. SubSceneInspectorUtility.ForceReimport re-converts the open subscene and
    /// is the cheapest trigger that actually works (plain AssetDatabase.ImportAsset does NOT). The internal API is
    /// reached by reflection so this assembly keeps no hard dependency on Unity.Scenes(.Editor).
    ///
    /// Call this right after AssetDatabase.SaveAssets() in any tool that mutates a timeline/clip asset.
    /// Scene-object edits do NOT need it — those go through the change tracker and re-bake on their own.
    /// </summary>
    internal static class RebakeUtil
    {
        /// <summary>
        /// Re-converts every currently-loaded SubScene. Returns the number reimported (0 if none are open or the
        /// reflection path is unavailable). Best-effort: never throws — a re-bake is a convenience, not correctness.
        /// </summary>
        public static int ReimportOpenSubScenes()
        {
            try
            {
                var subSceneType = Type.GetType("Unity.Scenes.SubScene, Unity.Scenes");
                var utilType = Type.GetType("Unity.Scenes.Editor.SubSceneInspectorUtility, Unity.Scenes.Editor");
                if (subSceneType == null || utilType == null)
                {
                    return 0;
                }

                // Pick the collection overload (param assignable from SubScene[]), never the single-SubScene one.
                var arrayType = subSceneType.MakeArrayType();
                var method = utilType.GetMethods(BindingFlags.Public | BindingFlags.Static)
                    .FirstOrDefault(m => m.Name == "ForceReimport"
                                         && m.GetParameters().Length == 1
                                         && m.GetParameters()[0].ParameterType.IsAssignableFrom(arrayType));
                if (method == null)
                {
                    return 0;
                }

                var loaded = Resources.FindObjectsOfTypeAll(subSceneType)
                    .OfType<Component>()
                    .Where(c => c != null && c.gameObject.scene.IsValid() && c.gameObject.scene.isLoaded)
                    .ToArray();
                if (loaded.Length == 0)
                {
                    return 0;
                }

                var arr = Array.CreateInstance(subSceneType, loaded.Length);
                for (var i = 0; i < loaded.Length; i++)
                {
                    arr.SetValue(loaded[i], i);
                }

                method.Invoke(null, new object[] { arr });
                return loaded.Length;
            }
            catch
            {
                return 0;
            }
        }
    }
}
