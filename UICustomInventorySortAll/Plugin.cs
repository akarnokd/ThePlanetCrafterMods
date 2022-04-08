using BepInEx;
using MijuTools;
using BepInEx.Configuration;
using SpaceCraft;
using UnityEngine.InputSystem;
using HarmonyLib;
using UnityEngine;
using System.Collections.Generic;

namespace UICustomInventorySortAll
{
    [BepInPlugin("akarnokd.theplanetcraftermods.uicustominventorysortall", "(UI) Customize Inventory Sort Order", "1.0.0.0")]
    public class Plugin : BaseUnityPlugin
    {
        /// <summary>
        /// List of comma-separated resource ids to look for in order.
        /// </summary>
        private static Dictionary<string, int> preferenceMap;

        static readonly string defaultPreference = string.Join(",", new string[]
        {
            "OxygenCapsule1",
            "WaterBottle1",
            "astrofood" // no capitalization in the game
        });

        private void Awake()
        {
            // Plugin startup logic
            Logger.LogInfo($"Plugin is loaded!");


            ConfigEntry<string> preference = Config.Bind("General", "Preference", defaultPreference, "List of comma-separated resource ids to look for in order.");
            string[] preferenceArray = preference.Value.Split(',');

            // associate an index with the preference resource ids
            preferenceMap = new Dictionary<string, int>();
            for (int i = 0; i < preferenceArray.Length; i++)
            {
                preferenceMap[preferenceArray[i]] = i;
            }

            Harmony.CreateAndPatchAll(typeof(Plugin));
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(Inventory), nameof(Inventory.AutoSort))]
        static bool Inventory_AutoSort(List<WorldObject> ___worldObjectsInInventory, InventoryDisplayer ___inventoryDisplayer)
        {
            ___worldObjectsInInventory.Sort((a, b) =>
            {
                string ga = a.GetGroup().GetId();
                string gb = b.GetGroup().GetId();

                int ia;
                if (!preferenceMap.TryGetValue(ga, out ia))
                {
                    ia = int.MaxValue;
                }
                int ib;
                if (!preferenceMap.TryGetValue(gb, out ib))
                {
                    ib = int.MaxValue;
                }

                if (ia < ib)
                {
                    return -1;
                }
                if (ia > ib)
                {
                    return 1;
                }

                return ga.CompareTo(gb);
            });
            ___inventoryDisplayer.RefreshContent();
            return false;
        }
    }
}
