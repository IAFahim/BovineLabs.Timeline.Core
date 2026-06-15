using System.Collections.Generic;
using UnityCliConnector;

namespace BovineLabs.Timeline.Core.Editor.CliTools.Shared
{
    /// <summary>
    /// Read the standard envelope sections off a tool response returned by an in-process
    /// <c>HandleCommand</c> call — used by ensure_* wrappers (which delegate to an underlying L0 tool
    /// and pass its undo through) and by the L2 engine (which collects each step's undo and verify).
    /// </summary>
    internal static class Responses
    {
        public static bool IsError(object resp) => resp is ErrorResponse;

        public static string Message(object resp)
            => resp is SuccessResponse sr ? sr.message : resp is ErrorResponse er ? er.message : null;

        /// <summary>The <c>data.undo</c> array, or null when the response carried none.</summary>
        public static object[] Undo(object resp) => Section(resp, "undo") as object[];

        /// <summary>One named section of <c>data</c> (result / pre / undo / verify), or null.</summary>
        public static object Section(object resp, string key)
        {
            if (resp is SuccessResponse sr && sr.data is IDictionary<string, object> d
                && d.TryGetValue(key, out var v))
                return v;
            return null;
        }
    }
}
