using System;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace BovineLabs.Timeline.Core.Editor.CliTools.Shared
{
    internal static class RebakeUtil
    {
        public static int ReimportOpenSubScenes()
        {
            try
            {
                var subSceneType = Type.GetType("Unity.Scenes.SubScene, Unity.Scenes");
                var utilType = Type.GetType("Unity.Scenes.Editor.SubSceneInspectorUtility, Unity.Scenes.Editor");
                if (subSceneType == null || utilType == null) return 0;

                var arrayType = subSceneType.MakeArrayType();
                var method = utilType.GetMethods(BindingFlags.Public | BindingFlags.Static)
                    .FirstOrDefault(m => m.Name == "ForceReimport"
                                         && m.GetParameters().Length == 1
                                         && m.GetParameters()[0].ParameterType.IsAssignableFrom(arrayType));
                if (method == null) return 0;

                var loaded = Resources.FindObjectsOfTypeAll(subSceneType)
                    .OfType<Component>()
                    .Where(c => c != null && c.gameObject.scene.IsValid() && c.gameObject.scene.isLoaded)
                    .ToArray();
                if (loaded.Length == 0) return 0;

                var arr = Array.CreateInstance(subSceneType, loaded.Length);
                for (var i = 0; i < loaded.Length; i++) arr.SetValue(loaded[i], i);

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