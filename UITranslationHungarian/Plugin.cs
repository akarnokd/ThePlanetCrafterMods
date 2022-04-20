using BepInEx;
using UnityEngine;
using BepInEx.Bootstrap;
using System.Reflection;
using System;
using HarmonyLib;
using SpaceCraft;
using System.Collections.Generic;
using BepInEx.Logging;
using MijuTools;
using System.Text;
using System.IO;
using BepInEx.Configuration;

namespace UITranslationHungarian
{
    [BepInPlugin("akarnokd.theplanetcraftermods.uitranslationhungarian", "(UI) Hungarian Translation", "1.0.0.2")]
    public class Plugin : BaseUnityPlugin
    {
        const string languageKey = "hungarian";

        static ManualLogSource logger;

        static Dictionary<string, string> labels = new Dictionary<string, string>();

        static ConfigEntry<bool> checkMissing;

        static string currentLanguage;

        private void Awake()
        {
            // Plugin startup logic
            Logger.LogInfo($"Plugin is loaded!");

            logger = Logger;

            checkMissing = Config.Bind("General", "CheckMissing", false, "If enabled, the new language's keys are checked against the english keys to find missing translations. See the logs afterwards.");

            Assembly me = Assembly.GetExecutingAssembly();
            string dir = Path.GetDirectoryName(me.Location);

            labels.Clear();

            string[] text = File.ReadAllLines(dir + "\\labels-hu.txt", Encoding.UTF8);

            foreach (string row in text)
            {
                string line = row.Trim();
                if (line.Length != 0 && !line.StartsWith("#"))
                {
                    int idx = line.IndexOf('=');
                    if (idx >= 0) 
                    {
                        labels[line.Substring(0, idx)] = line.Substring(idx + 1);
                    }
                }
            }

            Harmony.CreateAndPatchAll(typeof(Plugin));
        }

        // There is no GetLanguage, need to save current language for later checks
        [HarmonyPrefix]
        [HarmonyPatch(typeof(Localization), nameof(Localization.SetLangage))]
        static void Localization_SetLanguage(string langage)
        {
            currentLanguage = langage;
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(Localization), "LoadLocalization")]
        static void Localization_LoadLocalization(
                Dictionary<string, Dictionary<string, string>> ___localizationDictionary,
                List<string> ___availableLanguages
        )
        {
            // add it to the available languages
            if (!___availableLanguages.Contains(languageKey))
            {
                ___availableLanguages.Add(languageKey);
            }
            if (!GameConfig.TranslatedLangages.Contains(languageKey))
            {
                GameConfig.TranslatedLangages.Add(languageKey);
            }

            ___localizationDictionary[languageKey] = labels;

            if (checkMissing.Value)
            {
                Dictionary<string, string> english = ___localizationDictionary["english"];
                foreach (string key in english.Keys)
                {
                    if (!labels.ContainsKey(key))
                    {
                        logger.LogWarning("Missing translation\r\n" + key + "=" + english[key]);
                    }
                    else if (labels[key] == english[key])
                    {
                        logger.LogWarning("Not translated\r\n" + key + "=" + english[key]);
                    }
                }
            }
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(UnlockablesGraph), "OnEnable")]
        static void UnlockablesGraph_OnEnable(UnlockablesGraph __instance)
        {
            if (languageKey == currentLanguage) {
                if (__instance.worldUnitType == DataConfig.WorldUnitType.Terraformation)
                {
                    __instance.unitLabel.enableAutoSizing = true;
                }
            }
        }
    }
}
