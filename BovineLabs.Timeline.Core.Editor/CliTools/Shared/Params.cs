using Newtonsoft.Json.Linq;

namespace BovineLabs.Timeline.Core.Editor.CliTools.Shared
{
    internal sealed class Params
    {
        private readonly JObject p;

        public Params(JObject @params)
        {
            p = @params ?? new JObject();
        }

        private JToken Tok(string key)
        {
            return p.TryGetValue(key, out var t) ? t : null;
        }

        private static bool IsNull(JToken t)
        {
            return t == null || t.Type == JTokenType.Null;
        }

        public bool Has(string key)
        {
            return !IsNull(Tok(key));
        }

        public bool IsExplicitNull(string key)
        {
            return p.TryGetValue(key, out var t) && t.Type == JTokenType.Null;
        }

        public string OptString(string key, string def = null)
        {
            var t = Tok(key);
            if (IsNull(t)) return def;
            if (t.Type == JTokenType.Object || t.Type == JTokenType.Array)
                throw new ToolException("BAD_VALUE", $"Param '{key}' must be a string, got a JSON {t.Type}.");
            return t.Type == JTokenType.String ? t.Value<string>() : t.ToString();
        }

        public string RequireString(string key)
        {
            var v = OptString(key);
            if (string.IsNullOrEmpty(v))
                throw new ToolException("MISSING_PREREQUISITE", $"Required param '{key}' is missing or empty.");
            return v;
        }

        public float OptFloat(string key, float def)
        {
            var t = Tok(key);
            if (IsNull(t)) return def;
            try
            {
                return t.Value<float>();
            }
            catch
            {
                throw new ToolException("BAD_VALUE", $"Param '{key}' must be a number, got '{t}'.");
            }
        }

        public int OptInt(string key, int def)
        {
            var t = Tok(key);
            if (IsNull(t)) return def;
            if (t.Type == JTokenType.Float)
                throw new ToolException("BAD_VALUE", $"Param '{key}' must be an integer, got '{t}'.");
            try
            {
                return t.Value<int>();
            }
            catch
            {
                throw new ToolException("BAD_VALUE", $"Param '{key}' must be an integer, got '{t}'.");
            }
        }

        public bool OptBool(string key, bool def)
        {
            var t = Tok(key);
            if (IsNull(t)) return def;
            try
            {
                return t.Value<bool>();
            }
            catch
            {
                throw new ToolException("BAD_VALUE", $"Param '{key}' must be a boolean, got '{t}'.");
            }
        }

        public JArray OptArray(string key)
        {
            var t = Tok(key);
            if (IsNull(t)) return null;
            if (t is JArray a) return a;
            throw new ToolException("BAD_VALUE", $"Param '{key}' must be a JSON array.");
        }

        public JArray RequireArray(string key)
        {
            var a = OptArray(key);
            if (a == null)
                throw new ToolException("MISSING_PREREQUISITE", $"Required array param '{key}' is missing.");
            return a;
        }

        public JObject OptObject(string key)
        {
            var t = Tok(key);
            if (IsNull(t)) return null;
            if (t is JObject o) return o;
            throw new ToolException("BAD_VALUE", $"Param '{key}' must be a JSON object.");
        }

        public JObject RequireObject(string key)
        {
            var o = OptObject(key);
            if (o == null)
                throw new ToolException("MISSING_PREREQUISITE", $"Required object param '{key}' is missing.");
            return o;
        }
    }
}