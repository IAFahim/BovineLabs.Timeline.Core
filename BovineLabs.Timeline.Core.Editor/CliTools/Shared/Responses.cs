using System.Collections.Generic;
using UnityCliConnector;

namespace BovineLabs.Timeline.Core.Editor.CliTools.Shared
{
    internal static class Responses
    {
        public static bool IsError(object resp)
        {
            return resp is ErrorResponse;
        }

        public static string Message(object resp)
        {
            return resp is SuccessResponse sr ? sr.message : resp is ErrorResponse er ? er.message : null;
        }

        public static object[] Undo(object resp)
        {
            return Section(resp, "undo") as object[];
        }

        public static object Section(object resp, string key)
        {
            if (resp is SuccessResponse sr && sr.data is IDictionary<string, object> d
                                           && d.TryGetValue(key, out var v))
                return v;
            return null;
        }
    }
}