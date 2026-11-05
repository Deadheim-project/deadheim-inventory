using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using System;
using System.Linq;
using UnityEngine;

namespace Deadheim.InventoryUI
{
    [BepInPlugin(Guid, Name, Version)]
    [BepInDependency("Detalhes.Deadheim", BepInDependency.DependencyFlags.SoftDependency)]
    public sealed class Plugin : BaseUnityPlugin
    {
        public const string Guid = "Deadheim.InventoryUI";
        public const string Name = "Deadheim Inventory UI";
        public const string Version = "0.1.0";

        internal static ManualLogSource Log;
        internal static ConfigEntry<float> UiScale;
        internal static ConfigEntry<bool> ReduceMotion;
        internal static ConfigEntry<int> LowDurabilityPercent;
        internal static ConfigEntry<bool> ShowTooltips;

        private Harmony _harmony;

        private void Awake()
        {
            Log = Logger;
            UiScale = Config.Bind("Interface", "Scale", 1f,
                new ConfigDescription("Escala visual do painel de equipamento.", new AcceptableValueRange<float>(0.8f, 1.2f)));

            ReduceMotion = Config.Bind("Interface", "ReduceMotion", false,
                "Remove as transições suaves de cor dos botões do painel.");
            LowDurabilityPercent = Config.Bind("Interface", "LowDurabilityPercent", 25,
                new ConfigDescription("Abaixo desta durabilidade (%) o valor aparece em vermelho. 0 desliga o aviso.",
                    new AcceptableValueRange<int>(0, 50)));
            ShowTooltips = Config.Bind("Interface", "ShowTooltips", true,
                "Mostra o tooltip nativo do jogo ao passar o mouse sobre os itens do painel.");

            if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null)
            {
                Logger.LogInfo("Servidor dedicado detectado; a interface é somente do cliente e não será ativada aqui.");
                return;
            }

            if (BepInEx.Bootstrap.Chainloader.PluginInfos.Values.Any(info =>
                    info.Metadata.GUID.IndexOf("AzuExtendedPlayerInventory", StringComparison.OrdinalIgnoreCase) >= 0))
            {
                Logger.LogWarning("AzuExtendedPlayerInventory foi detectado; a interface Deadheim não será iniciada para evitar conflito no inventário.");
                return;
            }

            _harmony = new Harmony(Guid);
            _harmony.PatchAll(typeof(Plugin).Assembly);
            Logger.LogInfo($"{Name} v{Version} carregado para Valheim 1.0.21.");
        }

        private void OnDestroy()
        {
            _harmony?.UnpatchSelf();
            InventoryUi.Close();
        }
    }
}
