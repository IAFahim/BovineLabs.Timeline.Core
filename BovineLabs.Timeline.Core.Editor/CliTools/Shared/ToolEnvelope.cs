using System;
using System.Collections.Generic;
using UnityCliConnector;

namespace BovineLabs.Timeline.Core.Editor.CliTools.Shared
{
    /// <summary>
    /// Uniform success/error envelope so every vex tool returns the same shape:
    /// success -> data.{result, pre?, undo?, verify?}; error -> data.{code, detail?}.
    /// Null sections are omitted so reads stay clean.
    /// </summary>
    internal static class ToolEnvelope
    {
        public static object Ok(string message, object result = null, object pre = null, object undo = null, object verify = null)
        {
            var data = new Dictionary<string, object>();
            if (result != null) data["result"] = result;
            if (pre != null) data["pre"] = pre;
            if (undo != null) data["undo"] = undo;
            if (verify != null) data["verify"] = verify;
            return new SuccessResponse(message, data);
        }

        public static object Error(string code, string message, object detail = null)
        {
            var data = new Dictionary<string, object> { ["code"] = code };
            if (detail != null) data["detail"] = detail;
            return new ErrorResponse(message, data);
        }

        /// <summary>Map a thrown <see cref="ToolException"/> to the standard error envelope.</summary>
        public static object FromException(ToolException e)
            => Error(e.Code, e.Message, e.Detail);
    }

    /// <summary>
    /// A handled, expected failure carrying a machine code (MISSING_PREREQUISITE / AMBIGUOUS /
    /// PLAY_MODE_BLOCKED / SCENE_REF_REFUSED / NOT_FOUND / BAD_VALUE) plus an optional detail.
    /// Tools catch this and return <see cref="ToolEnvelope.FromException"/> instead of letting it
    /// surface as a generic CommandRouter failure.
    /// </summary>
    internal sealed class ToolException : Exception
    {
        public string Code { get; }
        public object Detail { get; }

        public ToolException(string code, string message, object detail = null) : base(message)
        {
            Code = code;
            Detail = detail;
        }
    }
}
