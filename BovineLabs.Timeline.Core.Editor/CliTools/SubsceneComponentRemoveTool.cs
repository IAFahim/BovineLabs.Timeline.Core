using BovineLabs.Timeline.Core.Editor.CliTools.Shared;
using Newtonsoft.Json.Linq;
using UnityCliConnector;
using UnityEngine;

namespace BovineLabs.Timeline.Core.Editor.CliTools
{
    [UnityCliTool(
        Name = "subscene_component_remove",
        Group = "vex",
        Description =
            "Remove a component (by type name) from a SubScene object. The replayable inverse of an ensure_component add; idempotent (no-op when the component is already absent).")]
    public static class SubsceneComponentRemoveTool
    {
        public static object HandleCommand(JObject @params)
        {
            var p = new Params(@params);
            try
            {
                var guard = AssetUtil.PlayModeBlocked();
                if (guard != null) return guard;

                var objPath = p.RequireString("object");
                var compName = p.RequireString("component");
                var compType = SceneObjectUtil.ResolveComponentType(compName);

                using (var session = SubSceneSession.Open(p.OptString("subscene")))
                {
                    if (session.Error != null) return session.Error;

                    var go = session.Find(objPath);
                    if (go == null)
                        return ToolEnvelope.Error("NOT_FOUND", $"No object '{objPath}' in {session.SubscenePath}.");

                    var comp = go.GetComponent(compType);
                    var undo = new object[]
                    {
                        new
                        {
                            tool = "ensure_component",
                            @params = new
                                { subscene = session.SubscenePath, @object = objPath, component = compType.Name }
                        }
                    };

                    if (comp == null)
                        return ToolEnvelope.Ok($"'{objPath}' has no {compType.Name} (already removed).",
                            new { removed = false }, undo: undo);

                    Object.DestroyImmediate(comp, true);
                    session.Save();

                    return ToolEnvelope.Ok($"Removed {compType.Name} from '{objPath}'.",
                        new { removed = true }, undo: undo);
                }
            }
            catch (ToolException e)
            {
                return ToolEnvelope.FromException(e);
            }
        }

        public class Parameters
        {
            [ToolParameter("Subscene .unity path. Default: auto-detected.")]
            public string Subscene { get; set; }

            [ToolParameter("SubScene object hierarchy path.", Required = true)]
            public string Object { get; set; }

            [ToolParameter("Component type name, simple or full.", Required = true)]
            public string Component { get; set; }
        }
    }
}