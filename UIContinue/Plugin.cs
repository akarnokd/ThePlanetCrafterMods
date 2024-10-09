// Copyright (c) 2022-2024, David Karnok & Contributors
// Licensed under the Apache License, Version 2.0

using BepInEx;
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
using BepInEx.Logging;

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

        internal static ManualLogSource logger;

        public void Awake()
        {
            LibCommon.BepInExLoggerFix.ApplyFix();

            // Plugin startup logic
            Logger.LogInfo($"Plugin is loaded!");

            logger = Logger;

            LibCommon.HarmonyIntegrityCheck.Check(typeof(Plugin));
            Harmony.CreateAndPatchAll(typeof(Plugin));
        }

        static IEnumerator DeferredBuildButtons()
        {
            var playButton = GameObject.Find("ButtonIntroPlay");

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
                        try
                        {
                            lastSaveInfoText = "";
                            lastSaveDateText = File.GetLastWriteTime(lastSave).ToString();
                            var isSingle = false;

                            var sf = File.ReadAllLines(lastSave);

                            if (sf.Length > 2)
                            {
                                JsonableWorldState ws = new();
                                JsonUtility.FromJsonOverwrite(sf[1].Replace("unitBiomassLevel", "unitPlantsLevel"), ws);

                                if (sf.Length > 4)
                                {
                                    if (sf[4].StartsWith("@"))
                                    {
                                        isSingle = true;
                                    }
                                    else
                                    {
                                        isSingle = false;
                                    }
                                }

                                int section = 1;
                                for (int j = 4; j < sf.Length; j++)
                                {
                                    if (sf[j].StartsWith("@"))
                                    {
                                        section++;
                                    }
                                    else
                                    {
                                        if (section == 7)
                                        {
                                            var state = ScriptableObject.CreateInstance<JsonableGameState>();
                                            JsonUtility.FromJsonOverwrite(sf[j], state);

                                            if (!string.IsNullOrWhiteSpace(state.saveDisplayName))
                                            {
                                                lastSaveInfoText += "\"" + state.saveDisplayName + "\"";
                                                lastSaveDateText = Path.GetFileNameWithoutExtension(lastSave) + "     " + lastSaveDateText;
                                            }
                                            else
                                            {
                                                lastSaveInfoText += Path.GetFileNameWithoutExtension(lastSave);
                                            }

                                            if (!string.IsNullOrWhiteSpace(state.planetId) && state.planetId != "Prime")
                                            {
                                                lastSaveInfoText += " (<color=#FFCC00>" + state.planetId + "</color>)";
                                            }
                                        }
                                    }
                                }

                                lastSaveInfoText += "  @  " + CreateTiAndUnit(ws);
                                if (isSingle)
                                {
                                    lastSaveInfoText += "     <i><color=#FFFF00>[Single]</color></i>";
                                }
                                else
                                {
                                    lastSaveInfoText += "     <i><color=#00FF00>[Multi]</color></i>";
                                }

                                imgIn.color = imgColorSaved;
                                txtIn.color = txtColorSaved;
                                bc.AddListener(OnContinueClick);

                            }
                        }
                        catch (Exception ex)
                        {
                            logger.LogError(ex);

                            lastSaveInfoText = Path.GetFileNameWithoutExtension(lastSave) + "    (error reading details)";
                            lastSaveDateText = File.GetLastWriteTime(lastSave).ToString();
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
                lastSaveInfo.GetComponentsInChildren<Image>().Do(DestroyImmediate);
                Destroy(lastSaveInfo.GetComponentInChildren<Button>());
                Destroy(lastSaveInfo.GetComponentInChildren<LocalizedText>());

                {
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
                }

                lastSaveDate = Instantiate(continueButton);
                lastSaveDate.name = "ContinueLastSaveDate";
                lastSaveDate.transform.SetParent(continueButton.transform.parent.parent, false);
                lastSaveDate.GetComponentsInChildren<Image>().Do(DestroyImmediate);
                Destroy(lastSaveDate.GetComponentInChildren<Button>());
                Destroy(lastSaveDate.GetComponentInChildren<LocalizedText>());

                {
                    var lastSaveDateBackground = new GameObject("ContinueLastSaveDateBackground");
                    lastSaveDateBackground.transform.SetParent(lastSaveDate.transform, false);
                    lastSaveDateBackground.transform.SetAsFirstSibling();
                    var img = lastSaveDateBackground.AddComponent<Image>();
                    img.color = Color.black;
                    var lastSaveDateBackgroundRT = lastSaveDateBackground.GetComponent<RectTransform>();

                    var txtDate = lastSaveDate.GetComponentInChildren<TextMeshProUGUI>();
                    txtDate.text = lastSaveDateText;
                    txtDate.overflowMode = TextOverflowModes.Overflow;
                    txtDate.textWrappingMode = TextWrappingModes.NoWrap;

                    lastSaveDateBackgroundRT.sizeDelta = new Vector2(txtDate.preferredWidth + 20, txtDate.preferredHeight);

                }
            }

            var screenWidth = 0;
            var screenHeight = 0;

            while (lastSaveInfo != null)
            {
                if (screenWidth != Screen.width || screenHeight != Screen.height)
                {
                    screenWidth = Screen.width;
                    screenHeight = Screen.height;
                    RepositionInfoAndDate();
                }
                yield return new WaitForSecondsRealtime(1);
            }
        }

        static string CreateTiAndUnit(JsonableWorldState ws)
        {
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
            return tiAndUnit;
        }

        static void RepositionInfoAndDate()
        {
            var txtInfo = lastSaveInfo.GetComponentInChildren<TextMeshProUGUI>();
            var txtDate = lastSaveDate.GetComponentInChildren<TextMeshProUGUI>();
            var buttonsRect = continueButton.transform.parent.gameObject.GetComponent<RectTransform>();
            var lastSaveInfoRect = lastSaveInfo.GetComponent<RectTransform>();
            var lastSaveDateRect = lastSaveDate.GetComponent<RectTransform>();

            lastSaveInfoRect.localPosition = buttonsRect.localPosition + new Vector3(0 * Mathf.Abs(buttonsRect.sizeDelta.x) + 1 * txtInfo.preferredWidth / 2 + 40, 0, 0);
            lastSaveDateRect.localPosition = buttonsRect.localPosition + new Vector3(0 * Mathf.Abs(buttonsRect.sizeDelta.x) + 1 * txtDate.preferredWidth / 2 + 40, -50, 0);
            /*
            var i = 100;
            do
            {
                lastSaveInfoRect.localPosition += new Vector3(20f, 0, 0);
            }
            while (lastSaveInfoRect.Overlaps(buttonsRect, true) && (--i) > 0)
            ;
            lastSaveInfoRect.localPosition += new Vector3(40f, 0, 0);
            */
            lastSaveDateRect.localPosition = new Vector3(lastSaveInfoRect.localPosition.x - txtInfo.preferredWidth / 2 + txtDate.preferredWidth / 2, lastSaveDateRect.localPosition.y, 0);
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
                    if (go.name == "GamepadButtons")
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

    public static class RectTransformExtensions
    {

        public static bool Overlaps(this RectTransform a, RectTransform b)
        {
            return a.WorldRect().Overlaps(b.WorldRect());
        }
        public static bool Overlaps(this RectTransform a, RectTransform b, bool allowInverse)
        {
            return a.WorldRect().Overlaps(b.WorldRect(), allowInverse);
        }

        public static Rect WorldRect(this RectTransform rectTransform)
        {
            Vector3[] v = new Vector3[4];
            rectTransform.GetWorldCorners(v);

            float x0 = Mathf.Min(v[0].x, v[1].x, v[2].x, v[3].x);
            float y0 = Mathf.Min(v[0].y, v[1].y, v[2].y, v[3].y);

            float x1 = Mathf.Max(v[0].x, v[1].x, v[2].x, v[3].x);
            float y1 = Mathf.Max(v[0].y, v[1].y, v[2].y, v[3].y);

            var r = new Rect(x0, y0, x1 - x0, y1 - y0);
            Plugin.logger.LogInfo(r);
            return r;

            /*
            Vector2 sizeDelta = rectTransform.sizeDelta;

            float rectTransformWidth = sizeDelta.x * rectTransform.lossyScale.x;
            float rectTransformHeight = sizeDelta.y * rectTransform.lossyScale.y;

            //With this it works even if the pivot is not at the center
            Vector3 position = rectTransform.TransformPoint(rectTransform.rect.center);
            float x = position.x - rectTransformWidth * 0.5f;
            float y = position.y - rectTransformHeight * 0.5f;

            return new Rect(x, y, rectTransformWidth, rectTransformHeight);
            */
        }
    }
}
