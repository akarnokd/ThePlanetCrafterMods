using BepInEx;
using SpaceCraft;
using HarmonyLib;
using System.Collections.Generic;
using BepInEx.Logging;
using BepInEx.Configuration;
using System.Linq;
using UnityEngine.UIElements;

namespace CheatMoreTrade
{
    [BepInPlugin("akarnokd.theplanetcraftermods.cheatmoretrade", "(Cheat) More Trade", PluginInfo.PLUGIN_VERSION)]
    public class Plugin : BaseUnityPlugin
    {
        // just some defaults
        static Dictionary<string, int> tradeValues = new() 
        {
            { "Vegetable0Seed", 500 },
            { "Vegetable1Seed", 1000 },
            { "Vegetable2Seed", 1500 },
            { "Vegetable3Seed", 2000 },
            { "BlueprintT1", 3000 }
        };

        static ConfigEntry<string> customUnlocks;

        static ManualLogSource logger;

        void Awake()
        {
            Logger.LogInfo($"Plugin is enabled.");
            
            logger = Logger;

            var str = string.Join(",", tradeValues.Select(kv => kv.Key + "=" + kv.Value));

            customUnlocks = Config.Bind("General", "Custom", str, "Comma separated list of id=value to modify to add to the tradeable list.");

            tradeValues.Clear();
            foreach (var kv in customUnlocks.Value.Split(','))
            {
                var kv1 = kv.Split('=');
                if (kv1.Length == 2)
                {
                    tradeValues[kv1[0].Trim()] = int.Parse(kv1[1].Trim());
                }
            }

            Harmony.CreateAndPatchAll(typeof(Plugin));
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(StaticDataHandler), "LoadStaticData")]
        static void StaticDataHandler_LoadStaticData(ref List<GroupData> ___groupsData)
        {
            foreach (var gr in ___groupsData)
            {
                if (gr.associatedGameObject != null)
                {
                    if (tradeValues.TryGetValue(gr.id, out var value))
                    {
                        gr.tradeCategory = DataConfig.TradeCategory.tier1;
                        gr.tradeValue = value;
                        logger.LogInfo(gr.id + " now tradeable at " + value + " tt");
                    }
                }
            }
        }
    }
}
