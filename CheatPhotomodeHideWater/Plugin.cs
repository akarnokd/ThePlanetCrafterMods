// Copyright (c) 2022-2025, David Karnok & Contributors
// Licensed under the Apache License, Version 2.0

using BepInEx;
using SpaceCraft;
using HarmonyLib;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace CheatPhotomodeHideWater
{
    [BepInPlugin("akarnokd.theplanetcraftermods.uiphotomodehidewater", "(Cheat) Hide Water in Photomode", PluginInfo.PLUGIN_VERSION)]
    public class Plugin : BaseUnityPlugin
    {

        private void Awake()
        {
            LibCommon.BepInExLoggerFix.ApplyFix();

            // Plugin startup logic
            Logger.LogInfo($"Plugin is loaded!");

            LibCommon.HarmonyIntegrityCheck.Check(typeof(Plugin));
            Harmony.CreateAndPatchAll(typeof(Plugin));
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(VisualsToggler), nameof(VisualsToggler.ToggleUi))]
        static void VisualsToggler_ToggleUi(List<GameObject> ___uisToHide)
        {
            bool active = ___uisToHide[0].activeSelf;
            if (Keyboard.current[Key.LeftShift].isPressed)
            {
                foreach (var wv in FindObjectsByType<WaterVolume>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                {
                    wv.gameObject.SetActive(active);
                }

                foreach (GameObject gameObject2 in FindObjectsByType<GameObject>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                {
                    if (gameObject2.name == "Water")
                    {
                        gameObject2.SetActive(active);
                    }
                }
            }
        }
    }
}
