using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace Deadheim.InventoryUI
{
    // Leitura somente: descobre qual aparência o Deadheim aplicou em cada peça, para o painel
    // mostrar o ícone de como ela aparece. Este mod nunca pede para mudar nada.
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

        // peça real (prefab) -> prefab da aparência aplicada. Vazio se o servidor desligou o recurso.
        internal static Dictionary<string, string> Chosen()
        {
            var result = new Dictionary<string, string>(StringComparer.Ordinal);
            if (!Value("IsEnabled", false)) return result;
            foreach (string pair in (Text("LocalChosen") ?? string.Empty).Split(';'))
            {
                int separator = pair.IndexOf('>');
                if (separator > 0 && separator < pair.Length - 1)
                    result[pair.Substring(0, separator)] = pair.Substring(separator + 1);
            }
            return result;
        }

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
                    Plugin.Log.LogWarning($"API de aparência incompatível ({version ?? "ausente"}); ícones mostram a peça real.");
                    _api = null;
                    return;
                }
                string[] required = { "IsEnabled", "LocalChosen" };
                string missing = required.FirstOrDefault(name =>
                    _api.GetMethod(name, BindingFlags.Public | BindingFlags.Static) == null);
                if (missing != null)
                {
                    Plugin.Log.LogWarning("API de aparência do Deadheim está desatualizada; falta " + missing + ".");
                    _api = null;
                }
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
            return method;
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
