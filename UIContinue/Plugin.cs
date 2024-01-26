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
    public class Plugin : BaseUnityPlugin
    {
        static GameObject continueButton;
        static GameObject lastSaveInfo;
        static GameObject lastSaveDate;

        static string lastSave;
        static string lastSaveInfoText;
        static string lastSaveDateText;

        private void Awake()
        {
            LibCommon.BepInExLoggerFix.ApplyFix();

            // Plugin startup logic
            Logger.LogInfo($"Plugin is loaded!");

            Harmony.CreateAndPatchAll(typeof(Plugin));
        }

        static IEnumerator DeferredBuildButtons()
        {
            var playButton = GameObject.Find("ButtonPlay");

            continueButton = Instantiate(playButton);
            continueButton.name = "ButtonContinue";
            continueButton.transform.SetParent(playButton.transform.parent, false);
            continueButton.transform.SetAsFirstSibling();

            var lt = continueButton.GetComponentInChildren<LocalizedText>();

            lt.textId = "MainMenu_Button_Continue";
            lt.UpdateTranslation();

            var btn = continueButton.GetComponent<Button>();
            var bc = new Button.ButtonClickedEvent();
            btn.onClick = bc;

            var imgIn = continueButton.GetComponentInChildren<Image>();
            var imgColorSaved = imgIn.color;
            var faded = new Color(1, 1, 1, 0.2f);
            imgIn.color = faded;
            var txtIn = continueButton.GetComponentInChildren<TextMeshProUGUI>();
            var txtColorSaved = txtIn.color;
            txtIn.color = faded;

            yield return new WaitForSeconds(0.1f);

            lastSave = null;
            lastSaveDateText = "";
            lastSaveInfoText = "";

            string[] files = Directory.GetFiles(Application.persistentDataPath, "*.json");

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
                        lastSaveInfoText = Path.GetFileNameWithoutExtension(lastSave);
                        lastSaveDateText = File.GetLastWriteTime(lastSave).ToString();

                        var tis = File.ReadLines(lastSave).Skip(1).Take(4).ToList();
                        if (tis.Count != 0)
                        {
                            JsonableWorldState ws = new();
                            JsonUtility.FromJsonOverwrite(tis[0].Replace("unitBiomassLevel", "unitPlantsLevel"), ws);

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


                            if (tis.Count >= 4)
                            {
                                if (tis[3].StartsWith("@"))
                                {
                                    lastSaveInfoText += "     <i><color=#FFFF00>[Single]</color></i>";
                                }
                                else
                                {
                                    lastSaveInfoText += "     <i><color=#00FF00>[Multi]</color></i>";
                                }
                            }

                            lastSaveInfoText += "    " + tiAndUnit;

                            imgIn.color = imgColorSaved;
                            txtIn.color = txtColorSaved;
                            bc.AddListener(OnContinueClick);
                        }
                        break;
                    }
                }
            }

            if (lastSaveInfo == null)
            {
                lastSaveInfo = Instantiate(continueButton);
                lastSaveInfo.name = "ContinueLastSaveInfo";
                lastSaveInfo.transform.SetParent(continueButton.transform.parent.parent, false);
                DestroyImmediate(lastSaveInfo.GetComponentInChildren<Image>());
                Destroy(lastSaveInfo.GetComponentInChildren<Button>());
                Destroy(lastSaveInfo.GetComponentInChildren<LocalizedText>());

                var lastSaveInfoBackground = new GameObject("ContinueLastSaveInfoBackground");
                lastSaveInfoBackground.transform.SetParent(lastSaveInfo.transform, false);
                lastSaveInfoBackground.transform.SetAsFirstSibling();
                var img = lastSaveInfoBackground.AddComponent<Image>();
                img.color = Color.black;
                var lastSaveInfoBackgroundRT = lastSaveInfoBackground.GetComponent<RectTransform>();

                var txtInfo = lastSaveInfo.GetComponentInChildren<TextMeshProUGUI>();
                txtInfo.text = lastSaveInfoText;
                txtInfo.overflowMode = TextOverflowModes.Overflow;
                txtInfo.textWrappingMode = TextWrappingModes.NoWrap;

                lastSaveInfoBackgroundRT.sizeDelta = new Vector2(txtInfo.preferredWidth + 20, txtInfo.preferredHeight);

                lastSaveDate = Instantiate(continueButton);
                lastSaveDate.name = "ContinueLastSaveDate";
                lastSaveDate.transform.SetParent(continueButton.transform.parent.parent, false);
                Destroy(lastSaveDate.GetComponentInChildren<Image>());
                Destroy(lastSaveDate.GetComponentInChildren<Button>());
                Destroy(lastSaveDate.GetComponentInChildren<LocalizedText>());

                var txtDate = lastSaveDate.GetComponentInChildren<TextMeshProUGUI>();
                txtDate.text = lastSaveDateText;
                txtDate.overflowMode = TextOverflowModes.Overflow;
                txtDate.textWrappingMode = TextWrappingModes.NoWrap;
            }

            {
                var txtInfo = lastSaveInfo.GetComponentInChildren<TextMeshProUGUI>();
                var txtDate = lastSaveDate.GetComponentInChildren<TextMeshProUGUI>();
                var rect = continueButton.GetComponent<RectTransform>();

                lastSaveInfo.transform.position = continueButton.transform.position
                    + new Vector3(rect.sizeDelta.x * rect.localScale.x / 1.75f + txtInfo.preferredWidth / 2, txtInfo.preferredHeight / 2, 0)
                    ;

                lastSaveDate.transform.position = continueButton.transform.position
                    + new Vector3(rect.sizeDelta.x * rect.localScale.x / 1.75f + txtDate.preferredWidth / 2, -txtInfo.preferredHeight, 0)
                    ;
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
                SceneManager.LoadScene(GameConfig.mainSceneName);
                foreach (var go in FindObjectsByType<GameObject>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                {
                    if (go.name == "CanvasLoading")
                    {
                        go.SetActive(true);
                    }
                    if (go.name == "CanvasBase")
                    {
                        go.SetActive(false);
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
                dict["MainMenu_Button_Continue"] = "Folytat";
            }
            if (___localizationDictionary.TryGetValue("english", out dict))
            {
                dict["MainMenu_Button_Continue"] = "Continue";
            }
        }
    }
}
