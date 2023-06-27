using BepInEx;
using BepInEx.Configuration;
using SpaceCraft;
using HarmonyLib;
using UnityEngine;
using System.Collections.Generic;
using UnityEngine.UI;
using TMPro;
using System.IO;
using System;
using System.Linq;
using UnityEngine.SceneManagement;
using System.Collections;
using BepInEx.Bootstrap;
using System.Reflection;

namespace UIContinue
{
    [BepInPlugin("akarnokd.theplanetcraftermods.uicontinue", "(UI) Continue", PluginInfo.PLUGIN_VERSION)]
    [BepInDependency("akarnokd.theplanetcraftermods.uitranslationhungarian", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency("akarnokd.theplanetcraftermods.uitranslationitalian", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency(modFeatMultiplayerGuid, BepInDependency.DependencyFlags.SoftDependency)]
    public class Plugin : BaseUnityPlugin
    {
        const string modFeatMultiplayerGuid = "akarnokd.theplanetcraftermods.featmultiplayer";

        static GameObject continueButton;
        static GameObject lastSaveInfo;
        static GameObject lastSaveDate;

        static string lastSave;
        static string lastSaveInfoText;
        static string lastSaveDateText;

        static MethodInfo multiplayerContinue;

        private void Awake()
        {
            // Plugin startup logic
            Logger.LogInfo($"Plugin is loaded!");

            if (Chainloader.PluginInfos.TryGetValue(modFeatMultiplayerGuid, out var pi))
            {
                multiplayerContinue = AccessTools.Method(pi.Instance.GetType(), "MainMenuContinue");
            }

            Harmony.CreateAndPatchAll(typeof(Plugin));
        }

        static IEnumerator DeferredBuildButtons()
        {
            yield return new WaitForSeconds(0.1f);

            var playButton = GameObject.Find("ButtonPlay");

            var optionsButton = GameObject.Find("ButtonOptions");

            continueButton = Instantiate(playButton);
            continueButton.name = "ButtonContinue";
            continueButton.transform.SetParent(playButton.transform.parent, false);

            var lt = continueButton.GetComponentInChildren<LocalizedText>();

            lt.textId = "MainMenu_Button_Continue";
            lt.UpdateTranslation();

            var btn = continueButton.GetComponent<Button>();
            var bc = new Button.ButtonClickedEvent();
            btn.onClick = bc;
            bc.AddListener(() => OnContinueClick());

            lastSave = null;
            lastSaveDateText = "";
            lastSaveInfoText = "";

            string[] files = Directory.GetFiles(Application.persistentDataPath, "*.json");

            continueButton.SetActive(false);

            if (files.Length != 0)
            {
                Array.Sort(files, (a, b) =>
                {
                    return File.GetLastWriteTime(b).CompareTo(File.GetLastWriteTime(a));
                });

                for (int i = 0; i < files.Length; i++)
                {
                    lastSave = files[i];
                    if (!lastSave.ToLower().EndsWith("backup.json"))
                    {
                        lastSaveInfoText = Path.GetFileName(lastSave);
                        lastSaveDateText = File.GetLastWriteTime(lastSave).ToString();

                        var tis = File.ReadLines(lastSave).Skip(1).Take(1).SingleOrDefault();
                        if (!string.IsNullOrEmpty(tis))
                        {
                            JsonableWorldState ws = new();
                            JsonUtility.FromJsonOverwrite(tis.Replace("unitBiomassLevel", "unitPlantsLevel"), ws);

                            var ti = ws.unitHeatLevel + ws.unitPressureLevel + ws.unitOxygenLevel + ws.unitPlantsLevel + ws.unitInsectsLevel + ws.unitAnimalsLevel;

                            var tiAndUnit = "";
                            if (ti >= 1E24)
                            {
                                tiAndUnit = string.Format("{0:#,##0.0} YTi", ti / 1E24);
                            }
                            if (ti >= 1E21)
                            {
                                tiAndUnit = string.Format("{0:#,##0.0} ZTi", ti / 1E21);
                            }
                            if (ti >= 1E18)
                            {
                                tiAndUnit = string.Format("{0:#,##0.0} ETi", ti / 1E18);
                            }
                            else if (ti >= 1E15)
                            {
                                tiAndUnit = string.Format("{0:#,##0.0} PTi", ti / 1E15);
                            }
                            else if (ti >= 1E12)
                            {
                                tiAndUnit = string.Format("{0:0.0} TTi", ti / 1E12);
                            }
                            else if (ti >= 1E9)
                            {
                                tiAndUnit = string.Format("{0:0.0} GTi", ti / 1E9);
                            }
                            else if (ti >= 1E6)
                            {
                                tiAndUnit = string.Format("{0:0.0} MTi", ti / 1E6);
                            }
                            else if (ti >= 1E3)
                            {
                                tiAndUnit = string.Format("{0:0.0} kTi", ti / 1E3);
                            }
                            else
                            {
                                tiAndUnit = string.Format("{0:0.0} Ti", ti);
                            }


                            lastSaveInfoText += "    " + tiAndUnit;
                            continueButton.SetActive(true);
                        }
                        break;
                    }
                }
            }

            continueButton.transform.localPosition = playButton.transform.localPosition + new Vector3(0, Mathf.Abs(playButton.transform.localPosition.y - optionsButton.transform.localPosition.y));

            if (lastSaveInfo == null)
            {
                lastSaveInfo = Instantiate(continueButton);
                lastSaveInfo.name = "ContinueLastSaveInfo";
                lastSaveInfo.transform.SetParent(continueButton.transform.parent, false);
                Destroy(lastSaveInfo.GetComponentInChildren<Image>());
                Destroy(lastSaveInfo.GetComponentInChildren<Button>());
                Destroy(lastSaveInfo.GetComponentInChildren<LocalizedText>());

                var txtInfo = lastSaveInfo.GetComponentInChildren<TextMeshProUGUI>();
                txtInfo.text = lastSaveInfoText;
                txtInfo.overflowMode = TextOverflowModes.Overflow;
                txtInfo.enableWordWrapping = false;

                lastSaveDate = Instantiate(continueButton);
                lastSaveDate.name = "ContinueLastSaveDate";
                lastSaveDate.transform.SetParent(continueButton.transform.parent, false);
                Destroy(lastSaveDate.GetComponentInChildren<Image>());
                Destroy(lastSaveDate.GetComponentInChildren<Button>());
                Destroy(lastSaveDate.GetComponentInChildren<LocalizedText>());

                var txtDate = lastSaveDate.GetComponentInChildren<TextMeshProUGUI>();
                txtDate.text = lastSaveDateText;
                txtDate.overflowMode = TextOverflowModes.Overflow;
                txtDate.enableWordWrapping = false;
            }

            {
                var txtInfo = lastSaveInfo.GetComponentInChildren<TextMeshProUGUI>();
                var txtDate = lastSaveDate.GetComponentInChildren<TextMeshProUGUI>();
                var rect = continueButton.GetComponent<RectTransform>();

                lastSaveInfo.transform.localPosition = continueButton.transform.localPosition
                    + new Vector3(rect.sizeDelta.x * rect.localScale.x + txtInfo.preferredWidth / 2, txtInfo.preferredHeight / 2, 0);

                lastSaveDate.transform.localPosition = continueButton.transform.localPosition
                    + new Vector3(rect.sizeDelta.x * rect.localScale.x + txtDate.preferredWidth / 2, -txtInfo.preferredHeight, 0);
            }
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(Intro), "Start")]
        static void Intro_Start(Intro __instance)
        {
            __instance.StartCoroutine(DeferredBuildButtons());
        }

        static void OnContinueClick()
        {
            if (lastSave != null)
            {
                Managers.GetManager<SavedDataHandler>().SetSaveFileName(Path.GetFileNameWithoutExtension(lastSave));
                lastSave = null;
                if (multiplayerContinue != null)
                {
                    multiplayerContinue.Invoke(null, new object[0]);
                }
                SceneManager.LoadScene(GameConfig.mainSceneName);
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
                dict["MainMenu_Button_Continue"] = "Folytat";
            }
            if (___localizationDictionary.TryGetValue("english", out dict))
            {
                dict["MainMenu_Button_Continue"] = "Continue";
            }
        }
    }
}
