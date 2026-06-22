using System;
using System.Collections.Generic;
using UnityCliConnector;

namespace BovineLabs.Timeline.Core.Editor.CliTools.Shared
{
    internal static class ToolEnvelope
    {
        public static object Ok(string message, object result = null, object pre = null, object undo = null,
            object verify = null)
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

        public static object FromException(ToolException e)
        {
            return Error(e.Code, e.Message, e.Detail);
        }
    }

    internal sealed class ToolException : Exception
    {
        public ToolException(string code, string message, object detail = null) : base(message)
        {
            Code = code;
            Detail = detail;
        }

        public string Code { get; }
        public object Detail { get; }
    }
}