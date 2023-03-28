using BepInEx;
using UnityEngine;
using System.Reflection;
using HarmonyLib;
using SpaceCraft;
using System.Collections.Generic;
using BepInEx.Logging;
using System.Text;
using System.IO;
using BepInEx.Configuration;
using System.Collections;

namespace UITranslationHungarian
{
    [BepInPlugin("akarnokd.theplanetcraftermods.uitranslationhungarian", "(UI) Hungarian Translation", PluginInfo.PLUGIN_VERSION)]
    public class Plugin : BaseUnityPlugin
    {
        const string languageKey = "hungarian";

        static ManualLogSource logger;

        static Dictionary<string, string> labels = new Dictionary<string, string>();

        static ConfigEntry<bool> checkMissing;
        static ConfigEntry<bool> dumpLabels;

        static string currentLanguage;

        static bool loadSuccess;

        private void Awake()
        {
            // Plugin startup logic
            Logger.LogInfo($"Plugin is loading!");

            logger = Logger;

            checkMissing = Config.Bind("General", "CheckMissing", false, "If enabled, the new language's keys are checked against the english keys to find missing translations. See the logs afterwards.");
            dumpLabels = Config.Bind("General", "DumpLabels", false, "Dump all labels for all languages in the game?");

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

            StartCoroutine(WaitForLocalizationLoad());

            Logger.LogInfo($"Plugin loaded!");
        }

        IEnumerator WaitForLocalizationLoad()
        {
            logger.LogInfo("Waiting for loadSuccess");
            for (; ; )
            {
                // yield return new WaitForSeconds(0.1f);
                /*
                for (int i = 0; i < 6; i++)
                {
                    yield return null;
                }
                */
                yield return new WaitForSecondsRealtime(0.1f);
                logger.LogInfo("Waiting for loadSuccess...");
                if (loadSuccess)
                {
                    foreach (LocalizedText ltext in UnityEngine.Object.FindObjectsOfType<LocalizedText>(true))
                    {
                        ltext.UpdateTranslation();

                        if (ltext.textId == "Newsletter_Button")
                        {
                            var rt = ltext.GetComponent<RectTransform>();
                            if (rt != null)
                            {
                                rt.sizeDelta = new Vector2(rt.sizeDelta.x + 20, rt.sizeDelta.y);
                                rt.localScale = new Vector2(rt.localScale.x * 0.85f, rt.localScale.y * 0.85f);
                            }
                        }
                    }
                    ExportLocalization();
                    yield break;
                }
                logger.LogInfo("Waiting for loadSuccess");
            }
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
                List<string> ___availableLanguages,
                bool ___hasLoadedSuccesfully
        )
        {
            loadSuccess = ___hasLoadedSuccesfully;
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

        static void ExportLocalization()
        {
            if (dumpLabels.Value)
            {
                Localization.GetLocalizedString("");
                FieldInfo fi = AccessTools.Field(typeof(Localization), "localizationDictionary");
                Dictionary<string, Dictionary<string, string>> dic = (Dictionary<string, Dictionary<string, string>>)fi.GetValue(null);
                if (dic != null)
                {
                    logger.LogInfo("Found localizationDictionary");

                    foreach (KeyValuePair<string, Dictionary<string, string>> kvp in dic)
                    {
                        Dictionary<string, string> dic2 = kvp.Value;
                        if (dic2 != null)
                        {
                            logger.LogInfo("Found " + kvp.Key + " labels");
                            StringBuilder sb = new StringBuilder();
                            foreach (KeyValuePair<string, string> kv in dic2)
                            {

                                sb.Append(kv.Key).Append("=").Append(kv.Value);
                                sb.AppendLine();
                            }

                            Assembly me = Assembly.GetExecutingAssembly();
                            string dir = Path.GetDirectoryName(me.Location);
                            File.WriteAllText(dir + "\\labels." + kvp.Key + ".txt", sb.ToString());
                        }
                    }
                }
            }
        }

    }
}
