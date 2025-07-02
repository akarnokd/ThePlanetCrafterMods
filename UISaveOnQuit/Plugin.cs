// Copyright (c) 2022-2025, David Karnok & Contributors
// Licensed under the Apache License, Version 2.0

using BepInEx;
using BepInEx.Configuration;
using SpaceCraft;
using HarmonyLib;
using Unity.Netcode;
using UnityEngine.UI;
using UnityEngine.Events;
using System;
using BepInEx.Logging;
using System.Net.NetworkInformation;

namespace UISaveOnQuit
{
    [BepInPlugin("akarnokd.theplanetcraftermods.saveonquit", "(UI) Save When Quitting", PluginInfo.PLUGIN_VERSION)]
    public class Plugin : BaseUnityPlugin
    {
        static ConfigEntry<bool> modEnabled;

        static Action OnContinueQuitting;

        private void Awake()
        {
            LibCommon.BepInExLoggerFix.ApplyFix();

            // Plugin startup logic
            Logger.LogInfo($"Plugin is loaded!");

            modEnabled = Config.Bind("General", "Enabled", true, "Is the mod enabled?");

            LibCommon.HarmonyIntegrityCheck.Check(typeof(Plugin));
            Harmony.CreateAndPatchAll(typeof(Plugin));
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(UiWindowPause), "Start")]
        static void UiWindowPause_Start(UiWindowPause __instance)
        {
            var quitBtn = __instance.transform.Find("Buttons/ButtonQuit").gameObject;

            var btn = quitBtn.GetComponent<Button>();

            var pc = btn.onClick.m_PersistentCalls;

            for (var i = pc.Count - 1; i >= 0; i--) {
                pc.RemoveListener(i);
            }

            btn.onClick.RemoveAllListeners();
            btn.onClick.AddListener(new UnityAction(() => {
                if (modEnabled.Value && (NetworkManager.Singleton?.IsServer ?? true))
                {
                    var sdh = Managers.GetManager<SavedDataHandler>();

                    OnContinueQuitting = () =>
                    {
                        sdh.OnSaved -= OnContinueQuitting;
                        __instance.OnQuit();
                    };

                    sdh.OnSaved += OnContinueQuitting;
                    __instance.OnSave();
                }
                else
                {
                    __instance.OnQuit();
                }
            }));
        }
    }
}
