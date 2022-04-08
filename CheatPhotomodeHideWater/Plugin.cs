using BepInEx;
using SpaceCraft;
using HarmonyLib;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using MijuTools;

namespace UIPhotomodeHideWater
{
    [BepInPlugin("akarnokd.theplanetcraftermods.uiphotomodehidewater", "(UI) Hide Water in Photomode", "1.0.0.0")]
    public class Plugin : BaseUnityPlugin
    {

        private void Awake()
        {
            // Plugin startup logic
            Logger.LogInfo($"Plugin is loaded!");

            Harmony.CreateAndPatchAll(typeof(Plugin));
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(LiveDevTools), nameof(LiveDevTools.ToggleUi))]
        static void LiveDevTools_ToggleUi(List<GameObject> ___handObjectsToHide)
        {
            bool active = !___handObjectsToHide[0].activeSelf;
            if (Keyboard.current[Key.LeftShift].isPressed)
            {
                foreach (GameObject gameObject2 in Managers.GetManager<WaterHandler>().waterVolumes)
                {
                    gameObject2.SetActive(active);
                }
            }
        }
    }
}
