namespace BovineLabs.Timeline.Core.Editor.CliTools.Shared
{
    internal static class EnsureResult
    {
        public const string AlreadyOk = "already-ok";
        public const string Fixed = "fixed";
        public const string WouldFix = "would-fix";

        public static object Satisfied(string message, object target, object before = null)
        {
            return ToolEnvelope.Ok(message, new { satisfied = true, action = AlreadyOk, target, before });
        }

        public static object WouldFixResult(string message, object target, object before = null)
        {
            return ToolEnvelope.Ok(message, new { satisfied = false, action = WouldFix, target, before });
        }

        public static object Applied(string message, object target, object before, object after, object[] undo)
        {
            return ToolEnvelope.Ok(message, new { satisfied = false, action = Fixed, target, before, after },
                undo: undo);
        }
    }
}