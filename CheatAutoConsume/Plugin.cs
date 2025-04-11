﻿// Copyright (c) 2022-2024, David Karnok & Contributors
// Licensed under the Apache License, Version 2.0

using BepInEx;
using SpaceCraft;
using HarmonyLib;
using BepInEx.Configuration;
using System.Linq;
using System;
using System.Collections.Generic;

namespace CheatAutoConsume
{
    [BepInPlugin("akarnokd.theplanetcraftermods.cheatautoconsume", "(Cheat) Auto Consume Oxygen-Water-Food", PluginInfo.PLUGIN_VERSION)]
    [BepInDependency("akarnokd.theplanetcraftermods.uitranslationhungarian", BepInDependency.DependencyFlags.SoftDependency)]
    public class Plugin : BaseUnityPlugin
    {

        static ConfigEntry<int> threshold;
        static ConfigEntry<bool> oxygenEnabled;
        static ConfigEntry<bool> waterEnabled;
        static ConfigEntry<bool> foodEnabled;
        static ConfigEntry<string> includeList;
        static ConfigEntry<string> excludeList;

        static bool oxygenWarning;
        static bool waterWarning;
        static bool foodWarning;

        public void Awake()
        {
            LibCommon.BepInExLoggerFix.ApplyFix();

            // Plugin startup logic
            Logger.LogInfo($"Plugin is loaded!");

            threshold = Config.Bind("General", "Threshold", 9, "The percentage for which below food/water/oxygen is consumed.");

            oxygenEnabled = Config.Bind("General", "Oxygen", true, "If true, oxygen is automatically consumed.");
            waterEnabled = Config.Bind("General", "Water", true, "If true, water is automatically consumed.");
            foodEnabled = Config.Bind("General", "Food", true, "If true, food is automatically consumed.");

            includeList = Config.Bind("General", "IncludeList", "", "Comma separated list of case-insensitive item identifiers that can be auto-consumed. If empty, all eligible items can be consumed. Example: astrofood1, vegetable1growable");
            excludeList = Config.Bind("General", "ExcludeList", "", "Comma separated list of case-insensitive item identifiers that should not be auto-consumed. Example: astrofood1, vegetable1growable");

            LibCommon.HarmonyIntegrityCheck.Check(typeof(Plugin));
            Harmony.CreateAndPatchAll(typeof(Plugin));
        }

        static bool FindAndConsume(DataConfig.UsableType type)
        {
            var includeSet = includeList.Value.Split(",", StringSplitOptions.RemoveEmptyEntries).Select(v => v.Trim().ToLowerInvariant()).ToHashSet();
            var excludeSet = excludeList.Value.Split(",", StringSplitOptions.RemoveEmptyEntries).Select(v => v.Trim().ToLowerInvariant()).ToHashSet();

            var activePlayerController = Managers.GetManager<PlayersManager>().GetActivePlayerController();
            var inv = Managers.GetManager<PlayersManager>().GetActivePlayerController().GetPlayerBackpack().GetInventory();
            var gh = activePlayerController.GetGaugesHandler();
            foreach (WorldObject _worldObject in inv.GetInsideWorldObjects())
            {
                if (_worldObject.GetGroup() is GroupItem groupItem)
                {
                    var gn = groupItem.id.ToLowerInvariant();
                    int groupValue = groupItem.GetGroupValue();
                    if (groupItem.GetUsableType() == type)
                    {
                        if (includeSet.Count != 0 && !includeSet.Contains(gn))
                        {
                            continue;
                        }
                        if (excludeSet.Count != 0 && excludeSet.Contains(gn))
                        {
                            continue;
                        }
                        if ((type == DataConfig.UsableType.Eatable && gh.Eat(groupValue))
                                || (type == DataConfig.UsableType.Breathable && gh.Breath(groupValue, true))
                                || (type == DataConfig.UsableType.Drinkable && gh.Drink(groupValue))
                                )
                        {

                            if (groupItem.GetEffectOnPlayer() != null)
                            {
                                activePlayerController.GetPlayerEffects().ActivateEffect(groupItem.GetEffectOnPlayer());
                            }

                            InventoriesHandler.Instance.RemoveItemFromInventory(_worldObject, inv, true, null);

                            return true;
                        }
                    }
                }
            }
            return false;
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(PlayerGaugeOxygen), "GaugeVerifications")]
        static void PlayerGaugeOxygen_GaugeVerifications(float ___gaugeValue, bool ___isInited)
        {
            if (!___isInited)
            {
                return;
            }
            if (___gaugeValue >= threshold.Value)
            {
                oxygenWarning = false;
            }
            if (___gaugeValue < threshold.Value && !oxygenWarning && oxygenEnabled.Value)
            {
                if (!FindAndConsume(DataConfig.UsableType.Breathable))
                {
                    Managers.GetManager<BaseHudHandler>().DisplayCursorText("CheatAutoConsume_No_Oxygen", 3f);
                    oxygenWarning = true;
                }
            }
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(PlayerGaugeThirst), "GaugeVerifications")]
        static void PlayerGaugeThirst_GaugeVerifications_Pre(float ___gaugeValue, bool ___isInited)
        {
            if (!___isInited)
            {
                return;
            }
            if (___gaugeValue >= threshold.Value)
            {
                waterWarning = false;
            }
            if (___gaugeValue < threshold.Value && !waterWarning && waterEnabled.Value)
            {
                if (!FindAndConsume(DataConfig.UsableType.Drinkable))
                {
                    Managers.GetManager<BaseHudHandler>().DisplayCursorText("CheatAutoConsume_No_Water", 3f);
                    waterWarning = true;
                }
            }
        }


        [HarmonyPrefix]
        [HarmonyPatch(typeof(PlayerGaugeHealth), "GaugeVerifications")]
        static void PlayerGaugeHealth_GaugeVerifications_Pre(float ___gaugeValue, bool ___isInited)
        {
            if (!___isInited)
            {
                return;
            }
            if (___gaugeValue >= threshold.Value)
            {
                foodWarning = false;
            }
            if (___gaugeValue < threshold.Value && !foodWarning && foodEnabled.Value)
            {
                if (!FindAndConsume(DataConfig.UsableType.Eatable))
                {
                    Managers.GetManager<BaseHudHandler>().DisplayCursorText("No Food In Inventory!", 3f);
                    foodWarning = true;
                }
            }
        }
        [HarmonyPostfix]
        [HarmonyPatch(typeof(Localization), "LoadLocalization")]
        static void Localization_LoadLocalization(
        Dictionary<string, Dictionary<string, string>> ___localizationDictionary)
        {
            if (___localizationDictionary.TryGetValue("hungarian", out var dict))
            {
                dict["CheatAutoConsume_No_Oxygen"] = "Nincs Oxigén a Hátizsákban!";
                dict["CheatAutoConsume_No_Water"] = "Nincs Víz a Hátizsákban!";
                dict["CheatAutoConsume_No_Food"] = "Nincs Étel a Hátizsákban!";
            }
            if (___localizationDictionary.TryGetValue("english", out dict))
            {
                dict["CheatAutoConsume_No_Oxygen"] = "No Oxygen In Inventory!";
                dict["CheatAutoConsume_No_Water"] = "No Water In Inventory!";
                dict["CheatAutoConsume_No_Food"] = "No Food In Inventory!";
            }
        }

    }
}
