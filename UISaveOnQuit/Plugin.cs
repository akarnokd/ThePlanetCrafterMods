﻿using BepInEx;
using BepInEx.Configuration;
using SpaceCraft;
using HarmonyLib;
using Unity.Netcode;

namespace UISaveOnQuit
{
    [BepInPlugin("akarnokd.theplanetcraftermods.saveonquit", "(UI) Save When Quitting", PluginInfo.PLUGIN_VERSION)]
    public class Plugin : BaseUnityPlugin
    {
        static ConfigEntry<bool> modEnabled;

        private void Awake()
        {
            LibCommon.BepInExLoggerFix.ApplyFix();

            // Plugin startup logic
            Logger.LogInfo($"Plugin is loaded!");

            modEnabled = Config.Bind("General", "Enabled", true, "Is the mod enabled?");

            Harmony.CreateAndPatchAll(typeof(Plugin));
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(UiWindowPause), nameof(UiWindowPause.OnQuit))]
        static void UiWindowPause_OnQuit(UiWindowPause __instance)
        {
            if (modEnabled.Value && (NetworkManager.Singleton?.IsServer ?? true))
            {
                __instance.OnSave();
            }
        }
    }
}
