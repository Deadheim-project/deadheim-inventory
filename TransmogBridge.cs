using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace Deadheim.InventoryUI
{
    internal static class TransmogBridge
    {
        private const string ApiTypeName = "Deadheim.TransmogApi";
        private const int ApiVersion = 1;
        private static Type _api;
        private static bool _lookedUp;
        private static float _retryAt;
        private static readonly Dictionary<string, MethodInfo> Methods = new Dictionary<string, MethodInfo>();

        internal static bool Available
        {
            get
            {
                Resolve();
                return _api != null;
            }
        }

        internal static int Revision() => Value("StateRevision", 0);
        internal static bool Enabled() => Value("IsEnabled", false);
        internal static string ChangeRestriction()
        {
            MethodInfo method = Method("InventoryUiChangeRestriction");
            if (method == null) return "Atualize o mod principal Deadheim para usar esta ação.";
            try { return method.Invoke(null, null) as string; }
            catch (Exception exception)
            {
                string reason = exception.InnerException?.Message ?? exception.Message;
                Plugin.Log.LogWarning("API de aparência InventoryUiChangeRestriction: " + reason);
                return "Não foi possível confirmar as regras do servidor.";
            }
        }
        internal static bool IsSuppressedHere() => Value("IsSuppressedHere", false);
        internal static string SlotOf(string prefab) => Text("SlotOf", prefab) ?? string.Empty;
        internal static string WhyIncompatible(string real, string appearance) => Text("WhyIncompatible", real, appearance);
        internal static void Preview(string pair) => Invoke("SetPreview", pair ?? string.Empty);
        internal static void ClearPreview() => Invoke("ClearPreview");

        internal static List<string> Unlocked() => (Text("LocalUnlocked") ?? string.Empty)
            .Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries).ToList();

        internal static Dictionary<string, string> Chosen()
        {
            var result = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (string pair in (Text("LocalChosen") ?? string.Empty).Split(';'))
            {
                int separator = pair.IndexOf('>');
                if (separator > 0 && separator < pair.Length - 1)
                    result[pair.Substring(0, separator)] = pair.Substring(separator + 1);
            }
            return result;
        }

        internal static string Apply(string real, string appearance) =>
            CallRequest("InventoryUiApply", real, appearance);

        internal static string Revert(string real) =>
            CallRequest("InventoryUiRevert", real);

        private static void Resolve()
        {
            if (_api != null) return;
            if (_lookedUp && Time.realtimeSinceStartup < _retryAt) return;
            _lookedUp = true;
            _retryAt = Time.realtimeSinceStartup + 5f;
            try
            {
                _api = AppDomain.CurrentDomain.GetAssemblies()
                    .Select(assembly => assembly.GetType(ApiTypeName, false))
                    .FirstOrDefault(type => type != null);
                if (_api == null) return;
                object version = _api.GetField("Version", BindingFlags.Public | BindingFlags.Static)?.GetValue(null);
                if (!(version is int current) || current != ApiVersion)
                {
                    Plugin.Log.LogWarning($"API de transmog incompatível ({version ?? "ausente"}); aparência indisponível.");
                    _api = null;
                    return;
                }
                string[] required = { "IsEnabled", "IsSuppressedHere", "LocalUnlocked", "LocalChosen", "SlotOf",
                    "WhyIncompatible", "SetPreview", "ClearPreview", "InventoryUiChangeRestriction",
                    "InventoryUiApply", "InventoryUiRevert" };
                string missing = required.FirstOrDefault(name =>
                    _api.GetMethod(name, BindingFlags.Public | BindingFlags.Static) == null);
                if (missing != null)
                {
                    Plugin.Log.LogWarning("API de transmog do Deadheim está desatualizada; falta " + missing + ".");
                    _api = null;
                    return;
                }
                Plugin.Log.LogInfo("Integração de aparência com o Deadheim disponível.");
            }
            catch (Exception exception)
            {
                Plugin.Log.LogWarning("Não foi possível carregar a API de aparência: " + exception.Message);
                _api = null;
            }
        }

        private static MethodInfo Method(string name)
        {
            Resolve();
            if (_api == null) return null;
            if (Methods.TryGetValue(name, out MethodInfo cached)) return cached;
            MethodInfo method = _api.GetMethod(name, BindingFlags.Public | BindingFlags.Static);
            Methods[name] = method;
            if (method == null) Plugin.Log.LogWarning("A API de aparência não oferece " + name + ".");
            return method;
        }

        private static void Invoke(string name, params object[] args)
        {
            try { Method(name)?.Invoke(null, args); }
            catch (Exception exception)
            {
                Plugin.Log.LogWarning($"API de aparência {name}: {exception.InnerException?.Message ?? exception.Message}");
            }
        }

        private static string Text(string name, params object[] args)
        {
            try { return Method(name)?.Invoke(null, args) as string; }
            catch (Exception exception)
            {
                Plugin.Log.LogWarning($"API de aparência {name}: {exception.InnerException?.Message ?? exception.Message}");
                return null;
            }
        }

        private static string CallRequest(string name, params object[] args)
        {
            MethodInfo method = Method(name);
            if (method == null) return "Atualize o mod principal Deadheim para usar esta ação.";
            try { return method.Invoke(null, args) as string; }
            catch (Exception exception)
            {
                string reason = exception.InnerException?.Message ?? exception.Message;
                Plugin.Log.LogWarning($"API de aparência {name}: {reason}");
                return "O Deadheim não conseguiu enviar a solicitação.";
            }
        }

        private static T Value<T>(string name, T fallback) where T : struct
        {
            try { return Method(name)?.Invoke(null, null) is T value ? value : fallback; }
            catch (Exception exception)
            {
                Plugin.Log.LogWarning($"API de aparência {name}: {exception.InnerException?.Message ?? exception.Message}");
                return fallback;
            }
        }
    }
}
