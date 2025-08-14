// Copyright (c) 2022-2025, David Karnok & Contributors
// Licensed under the Apache License, Version 2.0

using BepInEx;
using SpaceCraft;
using HarmonyLib;
using System.Collections.Generic;
using BepInEx.Logging;
using BepInEx.Configuration;
using System.Linq;

namespace CheatMoreTrade
{
    [BepInPlugin("akarnokd.theplanetcraftermods.cheatmoretrade", "(Cheat) More Trade", PluginInfo.PLUGIN_VERSION)]
    [BepInDependency("akarnokd.theplanetcraftermods.itemrods", BepInDependency.DependencyFlags.SoftDependency)]
    public class Plugin : BaseUnityPlugin
    {
        // just some defaults
        static readonly Dictionary<string, int> tradeValues = new() 
        {
            { "Vegetable0Seed", 500 },
            { "Vegetable1Seed", 1000 },
            { "Vegetable2Seed", 1500 },
            { "Vegetable3Seed", 2000 },
            { "BlueprintT1", 3000 }
        };

        static ConfigEntry<bool> isEnabled;
        static ConfigEntry<string> customUnlocks;

        static ManualLogSource logger;

        public void Awake()
        {
            LibCommon.BepInExLoggerFix.ApplyFix();

            Logger.LogInfo($"Plugin is enabled.");
            
            logger = Logger;

            var str = string.Join(",", tradeValues.Select(kv => kv.Key + "=" + kv.Value));

            isEnabled = Config.Bind("General", "Enabled", true, "Is the mod enabled?");
            customUnlocks = Config.Bind("General", "Custom", str, "Comma separated list of id=value to modify to add to the tradeable list.");

            LibCommon.HarmonyIntegrityCheck.Check(typeof(Plugin));
            Harmony.CreateAndPatchAll(typeof(Plugin));
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(StaticDataHandler), "LoadStaticData")]
        static void StaticDataHandler_LoadStaticData(ref List<GroupData> ___groupsData)
        {
            if (!isEnabled.Value)
            {
                return;
            }
            tradeValues.Clear();
            foreach (var kv in customUnlocks.Value.Split(','))
            {
                var kv1 = kv.Split('=');
                if (kv1.Length == 2)
                {
                    if (int.TryParse(kv1[1].Trim(), out var n))
                    {
                        tradeValues[kv1[0].Trim()] = n;
                    }
                }
            }
            HashSet<string> found = [];

            foreach (var gr in ___groupsData)
            {
                if (gr != null && gr.associatedGameObject != null)
                {
                    if (tradeValues.TryGetValue(gr.id, out var value))
                    {
                        if (value < 0)
                        {
                            gr.tradeCategory = DataConfig.TradeCategory.Null;
                            gr.tradeValue = 0;
                            logger.LogInfo(gr.id + " is no longer tradeable");
                        }
                        else
                        {
                            gr.tradeCategory = DataConfig.TradeCategory.tier1;
                            gr.tradeValue = value;
                            logger.LogInfo(gr.id + " now tradeable at " + value + " tt");
                            found.Add(gr.id);
                        }
                    }
                }
            }

            foreach (var tv in tradeValues)
            {
                if (!found.Contains(tv.Key))
                {
                    logger.LogWarning(tv.Key + " (" + tv.Value + ") GroupData not found");
                }
            }
        }
    }
}
