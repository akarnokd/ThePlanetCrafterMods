// Copyright (c) 2022-2025, David Karnok & Contributors
// Licensed under the Apache License, Version 2.0

using BepInEx;
using SpaceCraft;
using HarmonyLib;
using TMPro;
using System;
using System.Collections.Generic;

namespace UIShowETA
{
    [BepInPlugin("akarnokd.theplanetcraftermods.uishoweta", "(UI) Show ETA", PluginInfo.PLUGIN_VERSION)]
    [BepInDependency("akarnokd.theplanetcraftermods.fixunofficialpatches", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency("akarnokd.theplanetcraftermods.uitranslationhungarian", BepInDependency.DependencyFlags.SoftDependency)]
    public class Plugin : BaseUnityPlugin
    {
        public void Awake()
        {
            LibCommon.BepInExLoggerFix.ApplyFix();

            // Plugin startup logic
            Logger.LogInfo($"Plugin is loaded!");


            LibCommon.HarmonyIntegrityCheck.Check(typeof(Plugin));
            Harmony.CreateAndPatchAll(typeof(Plugin));
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(ScreenTerraStage), "RefreshDisplay", [])]
        static void ScreenTerraStage_RefreshDisplay(
            TextMeshProUGUI ___percentageProcess, 
            TerraformStagesHandler ___terraformStagesHandler)
        {
            var currentStage = ___terraformStagesHandler.GetCurrentGlobalStage();
            TerraformStage nextGlobalStage = ___terraformStagesHandler.GetNextGlobalStage();
            if (nextGlobalStage == null || currentStage == nextGlobalStage)
            {
                ___percentageProcess.text = "<br><color=#FFFF00>" 
                    + Localization.GetLocalizedString("ShowETA_ETA") 
                    + "</color><br>"
                    + Localization.GetLocalizedString("ShowETA_Done");
            }
            else
            {
                var wuh = Managers.GetManager<WorldUnitsHandler>();
                var nextGlobalStageUnit = wuh.GetUnit(nextGlobalStage.GetWorldUnitType());
                var speed = 0f;
                var remaining = nextGlobalStage.GetStageStartValue();
                if (nextGlobalStageUnit != null)
                {
                    speed = nextGlobalStageUnit.GetCurrentValuePersSec();
                    remaining = nextGlobalStage.GetStageStartValue() - nextGlobalStageUnit.GetValue();
                }

                var gameSettings = Managers.GetManager<GameSettingsHandler>();
                if (gameSettings != null)
                {
                    speed *= gameSettings.GetComputedTerraformationMultiplayerFactor(nextGlobalStage.GetWorldUnitType());
                }

                if (speed <= 0)
                {
                    ___percentageProcess.text += "<br><color=#FFFF00>"
                        + Localization.GetLocalizedString("ShowETA_ETA")
                        + "</color><br>"
                        + Localization.GetLocalizedString("ShowETA_Infinite");
                    ;
                }
                else
                {
                    var time = (long)(remaining / speed);
                    if (time > 0)
                    {
                        if (time < 366L * 24 * 60 * 60)
                        {
                            var ts = TimeSpan.FromSeconds(time);

                            if (ts.Days > 1)
                            {
                                ___percentageProcess.text += string.Format("<br><color=#FFFF00>"
                                    + Localization.GetLocalizedString("ShowETA_ETA")
                                    + "</color><br>{0:#} "
                                    + Localization.GetLocalizedString("ShowETA_Days")
                                    + "<br>{1}:{2:00}:{3:00}", ts.Days, ts.Hours, ts.Minutes, ts.Seconds);
                            }
                            else
                            if (ts.Days > 0)
                            {
                                ___percentageProcess.text += string.Format("<br><color=#FFFF00>"
                                    + Localization.GetLocalizedString("ShowETA_ETA")
                                    + "</color><br>{0:#} "
                                    + Localization.GetLocalizedString("ShowETA_Day")
                                    + "<br>{1}:{2:00}:{3:00}", ts.Days, ts.Hours, ts.Minutes, ts.Seconds);
                            }
                            else
                            {
                                ___percentageProcess.text += string.Format("<br><color=#FFFF00>"
                                    + Localization.GetLocalizedString("ShowETA_ETA")
                                    + "</color><br>{1}:{2:00}:{3:00}", ts.Days, ts.Hours, ts.Minutes, ts.Seconds);
                            }
                        }
                        else
                        {
                            ___percentageProcess.text += "<br><color=#FFFF00>"
                                + Localization.GetLocalizedString("ShowETA_ETA")
                                + "</color><br>"
                                + Localization.GetLocalizedString("ShowETA_Year");
                        }
                    }
                    else
                    {
                        ___percentageProcess.text += "<br><color=#FFFF00>"
                            + Localization.GetLocalizedString("ShowETA_ETA")
                            + "</color><br>"
                            + Localization.GetLocalizedString("ShowETA_Now");
                    }
                }
            }
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(Localization), "LoadLocalization")]
        static void Localization_LoadLocalization(
            Dictionary<string, Dictionary<string, string>> ___localizationDictionary
        )
        {
            if (___localizationDictionary.TryGetValue("hungarian", out var dict))
            {
                dict["ShowETA_ETA"] = "Idő";
                dict["ShowETA_Infinite"] = "Végtelen";
                dict["ShowETA_Year"] = "Év+";
                dict["ShowETA_Now"] = "Most";
                dict["ShowETA_Done"] = "Kész";
                dict["ShowETA_Days"] = "nap";
                dict["ShowETA_Day"] = "nap";
            }
            if (___localizationDictionary.TryGetValue("english", out dict))
            {
                dict["ShowETA_ETA"] = "ETA";
                dict["ShowETA_Infinite"] = "Infinite";
                dict["ShowETA_Year"] = "Year+";
                dict["ShowETA_Now"] = "Now";
                dict["ShowETA_Done"] = "Done";
                dict["ShowETA_Days"] = "days";
                dict["ShowETA_Day"] = "day";
            }
            if (___localizationDictionary.TryGetValue("russian", out dict))
            {
                dict["ShowETA_ETA"] = "осталось";
                dict["ShowETA_Infinite"] = "Бесконечность";
                dict["ShowETA_Year"] = "года+";
                dict["ShowETA_Now"] = "Сейчас";
                dict["ShowETA_Done"] = "Готово";
                dict["ShowETA_Days"] = "дн.";
                dict["ShowETA_Day"] = "д.";
            }
        }

    }
}
