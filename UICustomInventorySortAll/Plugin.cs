using BepInEx;
using BepInEx.Configuration;
using SpaceCraft;
using HarmonyLib;
using System.Collections.Generic;
using System;
using BepInEx.Bootstrap;

namespace UICustomInventorySortAll
{
    [BepInPlugin("akarnokd.theplanetcraftermods.uicustominventorysortall", "(UI) Customize Inventory Sort Order", PluginInfo.PLUGIN_VERSION)]
    public class Plugin : BaseUnityPlugin
    {
        /// <summary>
        /// List of comma-separated resource ids to look for in order.
        /// </summary>
        static readonly Dictionary<string, int> preferenceMap = [];

        static readonly string defaultPreference = string.Join(",",
        [
            "OxygenCapsule1",
            "WaterBottle1",
            "astrofood" // no capitalization in the game
        ]);

        static ConfigEntry<string> preference;

        private void Awake()
        {
            LibCommon.BepInExLoggerFix.ApplyFix();

            // Plugin startup logic
            Logger.LogInfo($"Plugin is loaded!");


            preference = Config.Bind("General", "Preference", defaultPreference, "List of comma-separated resource ids to look for in order.");
            ParsePreferences();

            Harmony.CreateAndPatchAll(typeof(Plugin));
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(Inventory), nameof(Inventory.AutoSort))]
        static bool Inventory_AutoSort(List<WorldObject> ____worldObjectsInInventory, InventoryDisplayer ____inventoryDisplayer)
        {
            DoSort(____worldObjectsInInventory);
            ____inventoryDisplayer?.RefreshContent();
            
            return false;
        }

        static void DoSort(List<WorldObject> ____worldObjectsInInventory)
        {
            ____worldObjectsInInventory.Sort((a, b) =>
            {
                Group groupA = a.GetGroup();
                Group groupB = b.GetGroup();

                string ga = groupA.GetId();
                string gb = groupB.GetId();

                if (!preferenceMap.TryGetValue(ga, out var ia))
                {
                    ia = int.MaxValue;
                }
                if (!preferenceMap.TryGetValue(gb, out var ib))
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

                return groupA.stableHashCode.CompareTo(groupB.stableHashCode);
            });
        }

        static void ParsePreferences()
        {
            preferenceMap.Clear();

            string[] preferenceArray = preference.Value.Split(',');

            // associate an index with the preference resource ids
            for (int i = 0; i < preferenceArray.Length; i++)
            {
                preferenceMap[preferenceArray[i]] = i;
            }
        }

        static void OnModConfigChanged(ConfigEntryBase _)
        {
            ParsePreferences();
        }
    }
}
