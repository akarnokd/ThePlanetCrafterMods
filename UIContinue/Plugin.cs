// Copyright (c) 2022-2025, David Karnok & Contributors
// Licensed under the Apache License, Version 2.0

using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using SpaceCraft;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace UIContinue
{
    [BepInPlugin("akarnokd.theplanetcraftermods.uicontinue", "(UI) Continue", PluginInfo.PLUGIN_VERSION)]
    [BepInDependency("akarnokd.theplanetcraftermods.uitranslationhungarian", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency("akarnokd.theplanetcraftermods.uitranslationpolish", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency("akarnokd.theplanetcraftermods.uitranslationestonian", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency("akarnokd.theplanetcraftermods.uitranslationukrainian", BepInDependency.DependencyFlags.SoftDependency)]
    public class Plugin : BaseUnityPlugin
    {
        static GameObject continueButton;
        static GameObject lastSaveInfo;
        static GameObject lastSaveDate;

        static string lastSave;
        static string lastSaveInfoText;
        static string lastSaveDateText;

        static ConfigEntry<float> fontSize;

        internal static ManualLogSource logger;

        public void Awake()
        {
            LibCommon.BepInExLoggerFix.ApplyFix();

            // Plugin startup logic
            Logger.LogInfo($"Plugin is loaded!");

            logger = Logger;

            fontSize = Config.Bind("General", "FontSize", 32f, "The font size for the latest save labels");

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
                            var playerCount = 0;

                            var sf = File.ReadAllLines(lastSave);

                            if (sf.Length > 2)
                            {
                                var worldStateLine = sf[1];

                                JsonablePlanetState ws = new();

                                bool isOldSaveFormat = worldStateLine.Contains("unitOxygenLevel");
                                if (isOldSaveFormat)
                                {
                                    JsonUtility.FromJsonOverwrite(worldStateLine
                                        .Replace("unitBiomassLevel", "unitPlantsLevel")
                                        , ws);
                                }

                                bool nameSectionFound = false;
                                int section = 1;
                                for (int j = 2; j < sf.Length; j++)
                                {
                                    if (sf[j].StartsWith("@", StringComparison.Ordinal))
                                    {
                                        section++;
                                    }
                                    else
                                    {
                                        if ((section == 2 && isOldSaveFormat) || (section == 3 && !isOldSaveFormat))
                                        {
                                            playerCount++;
                                        }
                                        else if (section == 2 && !isOldSaveFormat)
                                        {
                                            JsonablePlanetState wsTemp = new();
                                            JsonUtility.FromJsonOverwrite(
                                                sf[j].Replace("unitBiomassLevel", "unitPlantsLevel")
                                                .Replace("}|", "}"), 
                                                wsTemp);

                                            AddPlanetStates(ws, wsTemp);
                                        }
                                        if (section == (isOldSaveFormat ? 8 : 9))
                                        {
                                            nameSectionFound = true;
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
                                if (!nameSectionFound)
                                {
                                    lastSaveInfoText += Path.GetFileNameWithoutExtension(lastSave);
                                }
                                lastSaveInfoText += "  @  " + CreateTiAndUnit(ws);
                                if (playerCount <= 1)
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

        static void AddPlanetStates(JsonablePlanetState dest, JsonablePlanetState src)
        {
            dest.unitOxygenLevel += src.unitOxygenLevel;
            dest.unitHeatLevel += src.unitHeatLevel;
            dest.unitPressureLevel += src.unitPressureLevel;
            dest.unitPlantsLevel += src.unitPlantsLevel;
            dest.unitInsectsLevel += src.unitInsectsLevel;
            dest.unitAnimalsLevel += src.unitAnimalsLevel;
        }

        static string CreateTiAndUnit(JsonablePlanetState ws)
        {
            var ti = ws.unitHeatLevel + ws.unitPressureLevel + ws.unitOxygenLevel + ws.unitPlantsLevel + ws.unitInsectsLevel + ws.unitAnimalsLevel;

            string tiAndUnit;
            if (ti >= 1E24)
            {
                tiAndUnit = string.Format("{0:#,##0.0} YTi", ti / 1E24);
            }
            else if (ti >= 1E21)
            {
                tiAndUnit = string.Format("{0:#,##0.0} ZTi", ti / 1E21);
            }
            else if (ti >= 1E18)
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
            if (lastSaveInfo == null || lastSaveDate == null)
            {
                return;
            }
            var txtInfo = lastSaveInfo.GetComponentInChildren<TextMeshProUGUI>();
            var txtDate = lastSaveDate.GetComponentInChildren<TextMeshProUGUI>();
            var buttonsRect = continueButton.transform.parent.gameObject.GetComponent<RectTransform>();
            var lastSaveInfoRect = lastSaveInfo.GetComponent<RectTransform>();
            var lastSaveDateRect = lastSaveDate.GetComponent<RectTransform>();

            txtInfo.fontSize = fontSize.Value;
            txtDate.fontSize = fontSize.Value;

            lastSaveInfoRect.localPosition = buttonsRect.localPosition 
                + new Vector3(txtInfo.preferredWidth / 2 + 40, 0, 0);
            lastSaveDateRect.localPosition = buttonsRect.localPosition 
                + new Vector3(txtDate.preferredWidth / 2 + 40, -fontSize.Value - 16, 0);
            lastSaveDateRect.localPosition = new Vector3(lastSaveInfoRect.localPosition.x - txtInfo.preferredWidth / 2 + txtDate.preferredWidth / 2, 
                lastSaveDateRect.localPosition.y, 0);

            var rt1 = lastSaveInfo.transform.Find("ContinueLastSaveInfoBackground").GetComponent<RectTransform>();
            rt1.sizeDelta = new Vector2(txtInfo.preferredWidth + 20, txtInfo.preferredHeight);

            var rt2 = lastSaveDate.transform.Find("ContinueLastSaveDateBackground").GetComponent<RectTransform>();
            rt2.sizeDelta = new Vector2(txtDate.preferredWidth + 20, txtDate.preferredHeight);
        }

        public static void OnModConfigChanged(ConfigEntryBase _)
        {
            RepositionInfoAndDate();
        }


        [HarmonyPostfix]
        [HarmonyPatch(typeof(Intro), "Start")]
        static void Intro_Start(Intro __instance)
        {
            __instance.StartCoroutine(DeferredBuildButtons());
        }

        static void OnContinueClick()
        {
            continueButton.GetComponent<Button>().enabled = false;
            try
            {
                var modVersionLog = Path.Combine(Application.persistentDataPath, "lastgameversion2.txt");
                if (File.Exists(modVersionLog))
                {
                    var log = File.ReadAllText(modVersionLog).Trim();
                    if (log != Application.version)
                    {
                        ShowDialog("The game just updated from <color=#FFCC00>v"
                            + log + "</color> to <color=#FFCC00>v" + Application.version + "</color> !"
                            + "\n"
                            + "\nIf you are experiencing problems,"
                            + "\nplease update your mods as soon as possible."
                            + "\n"
                            + "\n=[ Press ESC / Gamepad-B to continue ]="
                            , false);
                        File.WriteAllText(modVersionLog, Application.version);
                    }
                    else
                    {
                        DoLoad();
                    }
                }
                else
                {
                    ShowDialog("Welcome to <color=#FFCC00>v" + Application.version + "</color> !"
                            + "\n"
                            + "\nIf you are experiencing problems,"
                            + "\nplease update your mods as soon as possible."
                            + "\n"
                            + "\n=[ Press ESC / Gamepad-B to continue ]="
                            , false);
                    File.WriteAllText(modVersionLog, Application.version);
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex);
                DoLoad();
            }
        }

        static void ShowDialog(string message, bool warning)
        {
            var panel = new GameObject("MiscModEnabler_Notification");
            var canvas = panel.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;

            var background = new GameObject("MiscModEnabler_Notification_Background");
            background.transform.SetParent(panel.transform, false);
            var img = background.AddComponent<Image>();
            if (warning)
            {
                img.color = new Color(0.5f, 0, 0, 0.99f);
            }
            else
            {
                img.color = new Color(0, 0.4f, 0, 0.99f);
            }

            var text = new GameObject("MiscModEnabler_Notification_Text");
            text.transform.SetParent(background.transform, false);

            var txt = text.AddComponent<Text>();
            txt.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            txt.supportRichText = true;
            txt.text = message;
            txt.color = Color.white;
            txt.fontSize = 30;
            txt.resizeTextForBestFit = false;
            txt.verticalOverflow = VerticalWrapMode.Overflow;
            txt.horizontalOverflow = HorizontalWrapMode.Overflow;
            txt.alignment = TextAnchor.MiddleLeft;

            var trect = text.GetComponent<RectTransform>();
            trect.sizeDelta = new Vector2(txt.preferredWidth, txt.preferredHeight);

            var brect = background.GetComponent<RectTransform>();
            brect.sizeDelta = trect.sizeDelta + new Vector2(30, 30);

            panel.AddComponent<DialogCloser>();
        }

        class DialogCloser : MonoBehaviour
        {
            void Update()
            {
                var gamepad = Gamepad.current;
                if (Keyboard.current[Key.Escape].wasPressedThisFrame
                    || (gamepad != null && (gamepad[GamepadButton.B]?.wasPressedThisFrame ?? false)))
                {
                    Destroy(gameObject);
                    DoLoad();
                }
            }
        }

        static void DoLoad()
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
            if (___localizationDictionary.TryGetValue("russian", out dict))
            {
                dict["MainMenu_Button_Continue"] = "Продолжить";
            }
            if (___localizationDictionary.TryGetValue("french", out dict))
            {
                dict["MainMenu_Button_Continue"] = "Continuer";
            }
            if (___localizationDictionary.TryGetValue("german", out dict))
            {
                dict["MainMenu_Button_Continue"] = "Weiterspielen";
            }
            if (___localizationDictionary.TryGetValue("japanese", out dict))
            {
                dict["MainMenu_Button_Continue"] = "プレイを続ける";
            }
            if (___localizationDictionary.TryGetValue("spanish", out dict))
            {
                dict["MainMenu_Button_Continue"] = "Continuar";
            }
            if (___localizationDictionary.TryGetValue("schinese", out dict))
            {
                dict["MainMenu_Button_Continue"] = "继续玩";
            }
            if (___localizationDictionary.TryGetValue("tchinese", out dict))
            {
                dict["MainMenu_Button_Continue"] = "繼續玩";
            }
            if (___localizationDictionary.TryGetValue("polish", out dict))
            {
                dict["MainMenu_Button_Continue"] = "Kontynuować";
            }
            if (___localizationDictionary.TryGetValue("portuguese", out dict))
            {
                dict["MainMenu_Button_Continue"] = "Continuar";
            }
            if (___localizationDictionary.TryGetValue("korean", out dict))
            {
                dict["MainMenu_Button_Continue"] = "계속하다";
            }
            if (___localizationDictionary.TryGetValue("turk", out dict))
            {
                dict["MainMenu_Button_Continue"] = "Devam etmek";
            }
            if (___localizationDictionary.TryGetValue("ukrainian", out dict))
            {
                dict["MainMenu_Button_Continue"] = "Продовжити";
            }
            if (___localizationDictionary.TryGetValue("estonian", out dict))
            {
                dict["MainMenu_Button_Continue"] = "Jätka";
            }
        }

    }
}
