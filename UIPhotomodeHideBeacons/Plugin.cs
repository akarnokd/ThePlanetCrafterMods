using BepInEx;
using SpaceCraft;
using HarmonyLib;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace UIPhotomodeHideBeacons
{
    [BepInPlugin("akarnokd.theplanetcraftermods.uiphotomodehidebeacons", "(UI) Hide Beacons in Photomode", "1.0.0.0")]
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
            WorldObjectColor[] array = UnityEngine.Object.FindObjectsOfType<WorldObjectColor>();
            for (int idx = 0; idx < array.Length; idx++)
            {
                foreach (Image image in array[idx].images)
                {
                    image.enabled = active;
                }
            }
        }
    }
}
