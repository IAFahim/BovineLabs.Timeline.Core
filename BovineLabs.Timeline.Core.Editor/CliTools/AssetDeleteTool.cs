using BovineLabs.Timeline.Core.Editor.CliTools.Shared;
using Newtonsoft.Json.Linq;
using UnityCliConnector;
using UnityEditor;

namespace BovineLabs.Timeline.Core.Editor.CliTools
{
    [UnityCliTool(
        Name = "asset_delete",
        Group = "vex",
        Description =
            "Delete an asset (and optionally its folder if it becomes empty). The inverse of timeline_create; an undo primitive. Idempotent: succeeds if already absent.")]
    public static class AssetDeleteTool
    {
        public static object HandleCommand(JObject @params)
        {
            var p = new Params(@params);
            try
            {
                var guard = AssetUtil.PlayModeBlocked();
                if (guard != null) return guard;

                var assetPath = p.RequireString("asset");
                var folderIfEmpty = p.OptString("folder_if_empty");

                var wasPresent = AssetDatabase.LoadMainAssetAtPath(assetPath) != null;
                if (wasPresent && !AssetDatabase.DeleteAsset(assetPath))
                    return ToolEnvelope.Error("BAD_VALUE", $"Failed to delete '{assetPath}'.");

                var folderDeleted = false;
                if (!string.IsNullOrEmpty(folderIfEmpty) && AssetUtil.IsFolderEmpty(folderIfEmpty))
                    folderDeleted = AssetDatabase.DeleteAsset(folderIfEmpty);

                return ToolEnvelope.Ok(
                    wasPresent ? $"Deleted {assetPath}." : $"{assetPath} already absent.",
                    new { asset = assetPath, wasPresent, folderDeleted });
            }
            catch (ToolException e)
            {
                return ToolEnvelope.FromException(e);
            }
        }

        public class Parameters
        {
            [ToolParameter("The asset path to delete.", Required = true)]
            public string Asset { get; set; }

            [ToolParameter("Also delete this folder if it becomes empty after the asset is removed.")]
            public string FolderIfEmpty { get; set; }
        }
    }
}