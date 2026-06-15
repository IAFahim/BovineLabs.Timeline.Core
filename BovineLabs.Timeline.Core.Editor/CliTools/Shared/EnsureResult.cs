namespace BovineLabs.Timeline.Core.Editor.CliTools.Shared
{
    /// <summary>
    /// The uniform result shape every ensure_* tool returns, so the L2 engine and the L3 doctor can
    /// read one shape regardless of which requirement was checked:
    /// <c>result.{ satisfied, action, target, before?, after? }</c> where action is one of
    /// already-ok / fixed / would-fix.
    ///
    /// An ensure_* is IDEMPOTENT: it first checks, then either no-ops (already-ok, no undo) or fixes
    /// (fixed, with a replayable undo). In <c>dry_run</c> it never mutates — it reports already-ok or
    /// would-fix and emits no undo. That single flag is what lets the doctor reuse the very same tools
    /// in report-only mode.
    /// </summary>
    internal static class EnsureResult
    {
        public const string AlreadyOk = "already-ok";
        public const string Fixed = "fixed";
        public const string WouldFix = "would-fix";

        /// <summary>The requirement was already met — nothing changed, no undo.</summary>
        public static object Satisfied(string message, object target, object before = null)
            => ToolEnvelope.Ok(message, result: new { satisfied = true, action = AlreadyOk, target, before });

        /// <summary>dry_run and the requirement is NOT met — report only, no mutation, no undo.</summary>
        public static object WouldFixResult(string message, object target, object before = null)
            => ToolEnvelope.Ok(message, result: new { satisfied = false, action = WouldFix, target, before });

        /// <summary>The requirement was unmet and has been fixed — carries the replayable undo.</summary>
        public static object Applied(string message, object target, object before, object after, object[] undo)
            => ToolEnvelope.Ok(message, result: new { satisfied = false, action = Fixed, target, before, after }, undo: undo);
    }
}
