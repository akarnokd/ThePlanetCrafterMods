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
    [BepInDependency(modFeatMultiplayerGuid, BepInDependency.DependencyFlags.SoftDependency)]
    public class Plugin : BaseUnityPlugin
    {
        const string modFeatMultiplayerGuid = "akarnokd.theplanetcraftermods.featmultiplayer";

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

        static Func<string> apiGetMultiplayerMode;

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

            if (Chainloader.PluginInfos.TryGetValue(modFeatMultiplayerGuid, out var pi))
            {
                apiGetMultiplayerMode = (Func<string>)AccessTools.Field(pi.Instance.GetType(), "apiGetMultiplayerMode").GetValue(null);

                AccessTools.Field(pi.Instance.GetType(), "inventoryAutoSortOverride").SetValue(null, new Action<List<WorldObject>>(DoSort));

                Logger.LogInfo(modFeatMultiplayerGuid + " found, installing AutoSort override");
            } 
            else
            {
                Logger.LogInfo(modFeatMultiplayerGuid + " not found");
            }

            Harmony.CreateAndPatchAll(typeof(Plugin));
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(Inventory), nameof(Inventory.AutoSort))]
        static bool Inventory_AutoSort(List<WorldObject> ___worldObjectsInInventory, InventoryDisplayer ___inventoryDisplayer)
        {
            if (apiGetMultiplayerMode == null || apiGetMultiplayerMode() == "SinglePlayer")
            {
                DoSort(___worldObjectsInInventory);
                ___inventoryDisplayer?.RefreshContent();
                return false;
            }
            return true;
        }

        static void DoSort(List<WorldObject> ___worldObjectsInInventory)
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
        }
    }
}
