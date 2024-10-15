// Copyright (c) 2022-2024, David Karnok & Contributors
// Licensed under the Apache License, Version 2.0

using BepInEx;
using BepInEx.Configuration;
using SpaceCraft;
using HarmonyLib;
using UnityEngine;
using System.Collections.Generic;
using UnityEngine.UI;
using System;
using System.Linq;
using System.Collections;
using BepInEx.Bootstrap;
using System.Reflection;
using BepInEx.Logging;
using UnityEngine.EventSystems;
using UnityEngine.Events;
using System.Text;

namespace UIModConfigMenu
{
    [BepInPlugin("akarnokd.theplanetcraftermods.uimodconfigmenu", "(UI) Mod Config Menu", PluginInfo.PLUGIN_VERSION)]
    [BepInDependency("akarnokd.theplanetcraftermods.uitranslationhungarian", BepInDependency.DependencyFlags.SoftDependency)]
    public class Plugin : BaseUnityPlugin
    {
        static ManualLogSource logger;

        static string filterValue = "";

        static Texture2D resetButtonImage;

        public void Awake()
        {
            LibCommon.BepInExLoggerFix.ApplyFix();

            // Plugin startup logic
            Logger.LogInfo($"Plugin is loaded!");

            logger = Logger;

            CreateResetButtonImage();

            LibCommon.HarmonyIntegrityCheck.Check(typeof(Plugin));
            Harmony.CreateAndPatchAll(typeof(Plugin));
        }

        void CreateResetButtonImage()
        {
            resetButtonImage = new Texture2D(24, 24);

            byte[] bytes =
            [
                0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x00, 0x00, 0x0D, 0x49, 0x48, 0x44, 0x52,
                0x00, 0x00, 0x00, 0x18, 0x00, 0x00, 0x00, 0x18, 0x08, 0x06, 0x00, 0x00, 0x00, 0xE0, 0x77, 0x3D,
                0xF8, 0x00, 0x00, 0x00, 0x01, 0x73, 0x52, 0x47, 0x42, 0x00, 0xAE, 0xCE, 0x1C, 0xE9, 0x00, 0x00,
                0x00, 0x04, 0x67, 0x41, 0x4D, 0x41, 0x00, 0x00, 0xB1, 0x8F, 0x0B, 0xFC, 0x61, 0x05, 0x00, 0x00,
                0x00, 0x09, 0x70, 0x48, 0x59, 0x73, 0x00, 0x00, 0x0E, 0xC3, 0x00, 0x00, 0x0E, 0xC3, 0x01, 0xC7,
                0x6F, 0xA8, 0x64, 0x00, 0x00, 0x01, 0xE7, 0x49, 0x44, 0x41, 0x54, 0x48, 0x4B, 0xDD, 0x95, 0xCB,
                0x4A, 0x1C, 0x41, 0x14, 0x86, 0xA7, 0x75, 0xBC, 0xA0, 0x88, 0x91, 0x09, 0xA2, 0x42, 0xBC, 0xAE,
                0x74, 0x23, 0xA2, 0x11, 0x54, 0x14, 0x14, 0x22, 0x83, 0xB8, 0xD1, 0x07, 0x10, 0x71, 0xE1, 0x42,
                0x74, 0xE1, 0xC2, 0x47, 0xC8, 0x2B, 0xF8, 0x00, 0xBE, 0x80, 0xB8, 0x31, 0x04, 0x1F, 0xC0, 0xA5,
                0x20, 0x8A, 0x2E, 0x94, 0x24, 0x10, 0x6F, 0xE0, 0x42, 0x63, 0x42, 0xD4, 0x85, 0xE3, 0xF7, 0x57,
                0x9F, 0xD6, 0xE9, 0x76, 0x46, 0x06, 0x6C, 0x11, 0xFC, 0xE0, 0xE7, 0xAF, 0x3A, 0x53, 0x75, 0x4E,
                0x57, 0x4D, 0x57, 0x75, 0xE2, 0x7D, 0x91, 0xC9, 0x64, 0x06, 0xAC, 0x19, 0x1B, 0x45, 0xE6, 0x01,
                0xC3, 0x14, 0xF9, 0x6C, 0xED, 0x10, 0xC4, 0x93, 0x28, 0x85, 0x2A, 0x2C, 0x54, 0x10, 0x9E, 0xB9,
                0x83, 0xC9, 0x4B, 0x58, 0xA9, 0xE7, 0x79, 0x5F, 0xAD, 0x9F, 0xC2, 0xFA, 0x51, 0x13, 0xFA, 0xA8,
                0x98, 0x71, 0x83, 0x0E, 0xD1, 0x3E, 0x63, 0xB7, 0x5D, 0x24, 0x0F, 0xD1, 0x15, 0x5C, 0xA3, 0x12,
                0x35, 0x48, 0xDE, 0x87, 0xCD, 0xA3, 0x6E, 0x14, 0x24, 0xBF, 0x32, 0x2F, 0x43, 0x1D, 0x68, 0x92,
                0x71, 0x13, 0xCF, 0xAD, 0x2A, 0xBA, 0x82, 0x59, 0xAC, 0x01, 0xFD, 0x44, 0xCD, 0x8A, 0xC1, 0x16,
                0xDA, 0x41, 0x67, 0x3C, 0xED, 0x5F, 0xC6, 0x94, 0xD2, 0xFE, 0x84, 0x06, 0x51, 0x30, 0xE6, 0x1F,
                0x5A, 0xE5, 0xF7, 0x03, 0xBF, 0xFB, 0x48, 0xB4, 0xC0, 0x14, 0xD6, 0xEA, 0xF7, 0x1C, 0x6B, 0x4C,
                0x52, 0x81, 0x9C, 0x30, 0xBE, 0x1E, 0xFB, 0x82, 0x34, 0xE7, 0x1C, 0x2D, 0x33, 0xFE, 0x0E, 0x7F,
                0x20, 0xBA, 0x45, 0xDA, 0xDB, 0x6C, 0xD2, 0x24, 0x99, 0x43, 0x43, 0xD6, 0x0F, 0x41, 0xB2, 0x13,
                0xB4, 0x42, 0xF3, 0x08, 0x69, 0x1B, 0xD3, 0x8A, 0x67, 0x13, 0x2D, 0xF0, 0x1F, 0xFD, 0x40, 0xBF,
                0x90, 0x9E, 0x48, 0x4F, 0x53, 0x8B, 0x46, 0x28, 0x32, 0x86, 0xE7, 0x63, 0xC3, 0xBC, 0x97, 0x71,
                0x6D, 0xD6, 0x76, 0x84, 0xB6, 0xE8, 0x25, 0x90, 0x78, 0x02, 0xEB, 0x44, 0x9B, 0xAC, 0xEA, 0xBB,
                0x0B, 0x42, 0x74, 0x05, 0x2F, 0xE1, 0xD4, 0xBC, 0xC5, 0xDC, 0xF1, 0x1A, 0x05, 0xAA, 0xCD, 0x1D,
                0x71, 0x16, 0xB8, 0x35, 0x2F, 0x36, 0x77, 0xC4, 0x59, 0xA0, 0xCE, 0xFC, 0xD2, 0xDC, 0x11, 0x67,
                0x01, 0x9D, 0x09, 0xF1, 0xDB, 0xDC, 0x11, 0x4B, 0x01, 0xDE, 0xA0, 0x0F, 0x58, 0x97, 0xDF, 0x4B,
                0x84, 0x4E, 0x73, 0xDE, 0x02, 0x4C, 0xAA, 0xB4, 0x66, 0x21, 0x8C, 0x22, 0xED, 0xFD, 0x1E, 0xAF,
                0xE8, 0xAE, 0x8B, 0x18, 0x39, 0x0B, 0x90, 0x7C, 0x06, 0x5B, 0xC4, 0x83, 0xBB, 0x26, 0x27, 0xFC,
                0xEE, 0x21, 0x25, 0xD7, 0xC5, 0xA7, 0x43, 0xFA, 0x4D, 0xF1, 0x6C, 0x9E, 0x14, 0x60, 0x82, 0x6E,
                0x46, 0x5D, 0x68, 0x49, 0x34, 0x4D, 0x5F, 0xA7, 0xB3, 0x5C, 0xBF, 0x05, 0xA8, 0x8F, 0xDA, 0x69,
                0x2E, 0x20, 0x5D, 0xE7, 0x62, 0x9D, 0xA7, 0xFF, 0x63, 0xED, 0x07, 0xF2, 0x9E, 0x64, 0x12, 0x8C,
                0x63, 0x3D, 0x7E, 0xCF, 0xA1, 0xFB, 0xE6, 0x02, 0xE9, 0x1B, 0x11, 0xBC, 0x31, 0x42, 0x7F, 0xAA,
                0x92, 0x1F, 0xFB, 0xDD, 0x30, 0xCF, 0x5E, 0x15, 0x14, 0xD1, 0xD1, 0xD7, 0xF7, 0xA0, 0xD1, 0x05,
                0xC2, 0xE8, 0x75, 0xDC, 0x21, 0x71, 0x70, 0x0F, 0xE5, 0xA4, 0xA0, 0xBB, 0x88, 0x42, 0x35, 0x98,
                0x54, 0x85, 0xF4, 0x51, 0xBA, 0x24, 0x71, 0x70, 0x72, 0xDF, 0x92, 0x44, 0xE2, 0x1E, 0x3F, 0xE3,
                0x9F, 0xE3, 0x6F, 0x89, 0x26, 0x93, 0x00, 0x00, 0x00, 0x00, 0x49, 0x45, 0x4E, 0x44, 0xAE, 0x42,
                0x60, 0x82
            ];

            resetButtonImage.LoadImage(bytes);
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(Intro), "Start")]
        static void Intro_Start(Intro __instance)
        {
            __instance.StartCoroutine(DeferredStart());
        }

        static IEnumerator DeferredStart()
        {
            yield return new WaitForSeconds(0.1f);

            enterMainMenu = true;
            try
            {
                CreateModPanel();
            }
            finally
            {
                enterMainMenu = false;
            }
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(WindowsHandler), "Start")]
        static void WindowsHandler_Start(WindowsHandler __instance)
        {
            __instance.StartCoroutine(DeferredStart());
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(UiWindowOptions), nameof(UiWindowOptions.OnClose))]
        static void UiWindowOptions_OnClose()
        {
            ClearTooltips();
        }

        static void ClearTooltips()
        {
            foreach (var tooltip in FindObjectsByType<ModConfigEntryHover>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                tooltip.OnPointerExit(default);
            }
        }

        static List<BepInEx.PluginInfo> pluginList;
        static GameObject modScrollContent;
        static Transform otherOptions;
        static float otherOptionsX;
        static GameObject buttonMods;
        static GameObject modScroll;
        static GameObject filterGo;
        static GameObject filterCount;
        static bool enterMainMenu;
        static Coroutine coroutineRenderPluginListDelayed;

        static void CreateModPanel()
        {
            GameObject buttonControls = default;
            GameObject buttonGraphics = default;
            GameObject scrollViewControls = default;
            OptionsPanel optionsPanel = default;
            GameObject applyBtn = default;

            foreach (var go in FindObjectsByType<GameObject>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (go.name == "ButtonControls")
                {
                    buttonControls = go;
                }
                if (go.name == "ButtonGraphics")
                {
                    buttonGraphics = go;
                }
                if (go.name == "Scroll View Controls")
                {
                    scrollViewControls = go;
                }
                if (go.name == "Game Options Panel")
                {
                    optionsPanel = go.GetComponent<OptionsPanel>();
                }
                if (go.name == "ApplyButton")
                {
                    applyBtn = go;
                }
            }

            if (buttonControls != null && buttonGraphics != null && scrollViewControls != null
                && optionsPanel != null && applyBtn != null)
            {
                buttonMods = Instantiate(buttonGraphics);
                buttonMods.name = "ButtonMods";
                buttonMods.transform.SetParent(buttonGraphics.transform.parent, false);

                /*
                buttonMods.transform.localPosition = buttonGraphics.transform.localPosition
                    + new Vector3(buttonGraphics.transform.localPosition.x - buttonControls.transform.localPosition.x, 0, 0);
                */
                buttonMods.transform.SetSiblingIndex(buttonGraphics.transform.parent.childCount - 2);

                var lt = buttonMods.GetComponentInChildren<LocalizedText>();
                lt.textId = "ModConfigMenu_Button";
                lt.UpdateTranslation();

                var btn = buttonMods.GetComponent<Button>();
                var bc = new Button.ButtonClickedEvent();
                btn.onClick = bc;

                optionsPanel.tabsButtons.Add(buttonMods);

                var buttonsRt = buttonGraphics.transform.parent.GetComponent<RectTransform>();
                var buttonGraphicsRT = buttonGraphics.GetComponent<RectTransform>();
                buttonsRt.localPosition -= new Vector3(buttonGraphicsRT.sizeDelta.x / 2, 0, 0);

                otherOptions = scrollViewControls.transform.Find("Viewport/Content/Other Options");
                otherOptionsX = otherOptions.GetComponent<RectTransform>().localPosition.x;

                modScroll = Instantiate(scrollViewControls);
                modScroll.name = "Scroll View Mods";
                modScroll.transform.SetParent(scrollViewControls.transform.parent, false);
                modScroll.SetActive(false);

                optionsPanel.tabsContent.Add(modScroll);
                var tabIndex = optionsPanel.tabsContent.Count - 1;

                modScrollContent = modScroll.transform.Find("Viewport/Content").gameObject;

                pluginList = new(Chainloader.PluginInfos.Values);
                pluginList.Sort((a, b) =>
                {
                    return a.Metadata.Name.CompareTo(b.Metadata.Name);
                });

                CreateFilterInput();

                bc.AddListener(() => {
                    optionsPanel.OnClickTab(tabIndex);
                    filterGo.SetActive(true);
                });

                if (applyBtn != null)
                {
                    var applyRt = applyBtn.GetComponent<RectTransform>();
                    applyRt.localPosition -= new Vector3(0, applyRt.sizeDelta.y * 1.5f, 0);
                }

                RenderPluginList();
            }
            else
            {
                logger.LogInfo("ButtonControls: " + (buttonControls != null));
                logger.LogInfo("ButtonGraphics: " + (buttonGraphics != null));
                logger.LogInfo("Scroll View Control: " + (scrollViewControls != null));
                logger.LogInfo("OptionsPanel: " + (optionsPanel != null));
                logger.LogInfo("ApplyBtn: " + (optionsPanel != null));
            }
        }

        public void Update()
        {
            if (modScroll != null && filterGo != null && !modScroll.activeSelf && filterGo.activeSelf)
            {
                filterGo.SetActive(false);
            }
        }


        static void CreateFilterInput() {

            filterGo = new GameObject("ModConfigMenu_Filter");
            filterGo.transform.SetParent(modScroll.transform.parent, false);

            var filterImg = filterGo.AddComponent<Image>();
            filterImg.color = new Color(0.4f, 0.4f, 0.4f);

            var filterRt = filterGo.GetComponent<RectTransform>();
            filterRt.localPosition = new Vector3(-900, -810, 0);

            var filterInput = filterGo.AddComponent<InputField>();

            var filterTextGo = new GameObject("ModConfigMenu_Filter_Text");
            filterTextGo.transform.SetParent(filterGo.transform, false);

            var buttonTxtRef = buttonMods.GetComponentInChildren<Text>();

            var filterTextPlaceholderGo = new GameObject("ModConfigMenu_Filter_Text_PlaceHolder");
            filterTextPlaceholderGo.transform.SetParent(filterGo.transform, false);

            var filterPlaceholderText = filterTextPlaceholderGo.AddComponent<Text>();
            filterPlaceholderText.font = buttonTxtRef.font;
            filterPlaceholderText.fontSize = buttonTxtRef.fontSize;
            filterPlaceholderText.fontStyle = FontStyle.Italic;
            filterPlaceholderText.text = "Filter by mod name parts or #parameter name";
            filterPlaceholderText.color = new Color(0.7f, 0.7f, 0.7f);

            var filterText = filterTextGo.AddComponent<Text>();
            filterText.font = buttonTxtRef.font;
            filterText.fontSize = buttonTxtRef.fontSize;
            filterText.fontStyle = buttonTxtRef.fontStyle;

            filterRt.sizeDelta = new Vector2(1000, buttonTxtRef.fontSize + 20);
            RectTransform filterTextPlaceholderRt = filterTextPlaceholderGo.GetComponent<RectTransform>();
            filterTextPlaceholderRt.localPosition = new Vector3(5, 0, 0);
            filterTextPlaceholderRt.sizeDelta = filterRt.sizeDelta;
            RectTransform filterTxtRt = filterTextGo.GetComponent<RectTransform>();
            filterTxtRt.sizeDelta = filterRt.sizeDelta;
            filterTxtRt.localPosition = new Vector3(5, 0, 0);

            filterInput.textComponent = filterText;
            filterInput.text = filterValue;
            filterInput.targetGraphic = filterImg;
            filterInput.placeholder = filterPlaceholderText;
            filterInput.caretWidth = 3;
            filterInput.caretColor = Color.white;

            filterInput.onValueChanged.AddListener(new UnityAction<string>(s =>
            {
                if (filterValue != s)
                {
                    filterValue = s ?? "";
                    RenderPluginListDebounce();
                }
            }));

            filterInput.enabled = false;
            filterInput.enabled = true;


            filterCount = new GameObject("ModConfigMenu_Filter_Count");
            filterCount.transform.SetParent(filterGo.transform, false);

            var filterCountTxt = filterCount.AddComponent<Text>();
            filterCountTxt.font = buttonTxtRef.font;
            filterCountTxt.fontSize = buttonTxtRef.fontSize;
            filterCountTxt.fontStyle = buttonTxtRef.fontStyle;
            filterCountTxt.alignment = TextAnchor.MiddleCenter;
            filterCountTxt.horizontalOverflow = HorizontalWrapMode.Overflow;
            filterCountTxt.text = "        ";

            var filterCountRt = filterCount.GetComponent<RectTransform>();
            filterCountRt.localPosition = new Vector3(filterRt.sizeDelta.x / 2 + filterCountTxt.preferredWidth / 2 + 30, 0, 0);

            filterGo.SetActive(false);
        }


        static void RenderPluginListDebounce()
        {
            var img = filterGo.GetComponent<Image>();
            if (coroutineRenderPluginListDelayed != null)
            {
                img.StopCoroutine(coroutineRenderPluginListDelayed);
                coroutineRenderPluginListDelayed = null;
            }
            coroutineRenderPluginListDelayed = img.StartCoroutine(RenderPluginListDelayed());
        }
        
        static IEnumerator RenderPluginListDelayed()
        {
            yield return new WaitForSecondsRealtime(0.25f);
            RenderPluginList();
        }

        static void RenderPluginList()
        {
            if (!enterMainMenu)
            {
                // after the first create, the offsets go way off for some reason, fix it here
                otherOptionsX = 1370;
            }
            for (int i = modScrollContent.transform.childCount - 1; i >= 0; i--)
            {
                Destroy(modScrollContent.transform.GetChild(i).gameObject);
            }

            ClearTooltips();

            var filterParts = filterValue.Split(' ').Where(s => s.Length != 0);

            var j = 0f;
            var cnt = 0;
            foreach (var pi in pluginList)
            {
                if (filterValue != "")
                {
                    bool found = true;
                    foreach (var ft in filterParts)
                    {
                        if (ft.StartsWith("#"))
                        {
                            bool foundKey = false;
                            foreach (var cef in pi.Instance.Config.Keys)
                            {
                                if (cef.Key.Contains(ft[1..], StringComparison.InvariantCultureIgnoreCase))
                                {
                                    foundKey = true;
                                    break;
                                }
                            }
                            if (!foundKey)
                            {
                                found = false;
                            }
                        }
                        else
                        if (!pi.Metadata.GUID.Contains(ft, StringComparison.InvariantCultureIgnoreCase) 
                            && !pi.Metadata.Name.Contains(ft, StringComparison.InvariantCultureIgnoreCase))
                        {
                            found = false;
                            break;
                        }
                    }
                    if (!found)
                    {
                        continue;
                    }
                }

                cnt++;

                // [Optionally] call a method on the mod if the config is changed live
                MethodInfo mOnModConfigChanged = pi.Instance.GetType().GetMethod(
                    "OnModConfigChanged", 
                    AccessTools.all, 
                    null, 
                    [typeof(ConfigEntryBase)], 
                    []
                );

                // Create the mods header row with version
                // ---------------------------------------

                var amod = Instantiate(otherOptions);
                amod.name = pi.Metadata.GUID;
                Destroy(amod.GetComponentInChildren<LocalizedText>());
                var amodImg = amod.GetComponentInChildren<Image>();
                amodImg.enabled = true;

                var txt = amod.transform.Find("Label/Text").GetComponent<Text>();

                txt.supportRichText = true;
                txt.text = pi.Metadata.Name + " <color=#FFFF00>v" + pi.Metadata.Version + "</color>";

                amod.transform.SetParent(modScrollContent.transform, false);

                var rtLine = amod.GetComponent<RectTransform>();

                rtLine.localPosition = new Vector3(otherOptionsX, j, 0);

                // Create the open config file button
                // ----------------------------------

                var amodOpenCfg = new GameObject(amod.name + "-openconfig");
                amodOpenCfg.transform.SetParent(amod.transform, false);

                var amodOpenCfgImg = amodOpenCfg.AddComponent<Image>();
                amodOpenCfgImg.color = new Color(0.5f, 0, 0.5f);

                var amodOpenCfgTextGo = new GameObject(amod.name + "-openconfig-button");
                amodOpenCfgTextGo.transform.SetParent(amodOpenCfgImg.transform, false);

                var amodOpenCfgTxt = amodOpenCfgTextGo.AddComponent<Text>();
                amodOpenCfgTxt.text = " Open .cfg ";
                amodOpenCfgTxt.font = txt.font;
                amodOpenCfgTxt.fontSize = txt.fontSize;
                amodOpenCfgTxt.fontStyle = txt.fontStyle;
                amodOpenCfgTxt.color = Color.white;
                amodOpenCfgTxt.horizontalOverflow = HorizontalWrapMode.Overflow;
                amodOpenCfgTxt.alignment = TextAnchor.MiddleCenter;

                var amodOpenCfgRt = amodOpenCfg.GetComponent<RectTransform>();
                amodOpenCfgRt.localPosition = new Vector3(1200, -rtLine.sizeDelta.y / 2, 0);
                amodOpenCfgRt.sizeDelta = new Vector2(amodOpenCfgTxt.preferredWidth + 20, amodOpenCfgTxt.preferredHeight + 20);

                var amodOpenCfgBtn = amodOpenCfg.AddComponent<Button>();
                var amodOpenCfgRef = pi.Instance.Config;
                amodOpenCfgBtn.onClick.AddListener(new UnityAction(() =>
                {
                    Application.OpenURL("file:///" + amodOpenCfgRef.ConfigFilePath);
                }));


                // Create a sorted set of per mod options
                // --------------------------------------

                j -= rtLine.sizeDelta.y;

                List<string> sections = new(pi.Instance.Config.Keys.Select(e => e.Section).Distinct());
                sections.Sort(StringComparer.OrdinalIgnoreCase);

                if (sections.Count == 0)
                {
                    var amodCfg = Instantiate(otherOptions);
                    amodCfg.name = "NoConfig";
                    amodCfg.transform.SetParent(modScrollContent.transform, false);

                    amodCfg.GetComponentInChildren<Image>().enabled = false;

                    Destroy(amodCfg.GetComponentInChildren<LocalizedText>());

                    txt = amodCfg.transform.Find("Label/Text").GetComponent<Text>();

                    txt.supportRichText = true;
                    txt.horizontalOverflow = HorizontalWrapMode.Overflow;
                    txt.fontStyle = FontStyle.BoldAndItalic;

                    var rt = amodCfg.GetComponent<RectTransform>();

                    rt.localPosition = new Vector3(otherOptionsX, j, 0);

                    txt.text = "( No configuration options declared )";

                    j -= rt.sizeDelta.y;
                }
                else
                {

                    foreach (var sec in sections)
                    {
                        foreach (var ce in pi.Instance.Config)
                        {
                            if (sec == ce.Key.Section)
                            {
                                var amodCfg = Instantiate(otherOptions);
                                amodCfg.name = ce.Key.Section + "-" + ce.Key.Key;
                                amodCfg.transform.SetParent(modScrollContent.transform, false);

                                amodCfg.GetComponentInChildren<Image>().enabled = false;

                                Destroy(amodCfg.GetComponentInChildren<LocalizedText>());

                                txt = amodCfg.transform.Find("Label/Text").GetComponent<Text>();

                                txt.supportRichText = true;
                                txt.horizontalOverflow = HorizontalWrapMode.Overflow;

                                var rt = amodCfg.GetComponent<RectTransform>();

                                rt.localPosition = new Vector3(otherOptionsX, j, 0);

                                txt.text = "[" + ce.Key.Section + "] " + ce.Key.Key;

                                if (ce.Value.SettingType == typeof(bool))
                                {

                                    var editorGo = new GameObject(ce.Key.Section + "-" + ce.Key.Key + "-editor");
                                    editorGo.transform.SetParent(txt.transform, false);

                                    editorGo.AddComponent<GraphicRaycaster>();

                                    var editorRt = editorGo.GetComponent<RectTransform>();

                                    var img = editorGo.AddComponent<Image>();
                                    var colorOff = new Color(0.1f, 0.1f, 0.1f);
                                    var colorOn = new Color(0f, 1f, 0f);
                                    img.color = ce.Value.GetSerializedValue() == "true" ? colorOn : colorOff;

                                    var bw = txt.fontSize + 20;
                                    editorRt.localPosition = new Vector2(bw / 2, editorRt.localPosition.y);
                                    editorRt.sizeDelta = new Vector2(bw, bw);

                                    var btnComp = editorGo.AddComponent<Button>();
                                    var ceb = ce.Value;
                                    var txt2 = txt;

                                    var pcfg = pi.Instance.Config;

                                    btnComp.onClick.AddListener(new UnityAction(() =>
                                    {
                                        ceb.SetSerializedValue(ceb.GetSerializedValue() == "true" ? "false" : "true");
                                        pcfg.Save();
                                        img.color = ceb.GetSerializedValue() == "true" ? colorOn : colorOff;

                                        mOnModConfigChanged?.Invoke(null, [ceb]);
                                    }));
                                }
                                else
                                {
                                    var editorGo = new GameObject(ce.Key.Section + "-" + ce.Key.Key + "-editor");
                                    editorGo.transform.SetParent(txt.transform, false);

                                    editorGo.AddComponent<GraphicRaycaster>();

                                    var editorRt = editorGo.GetComponent<RectTransform>();

                                    var img = editorGo.AddComponent<Image>();
                                    img.color = new Color(0.1f, 0.1f, 0.1f);

                                    var editor = editorGo.AddComponent<InputField>();

                                    var txt3Go = new GameObject(editorGo.name + "-text");
                                    var txt3 = txt3Go.AddComponent<Text>();
                                    txt3.transform.SetParent(editorGo.transform, false);
                                    var rt3 = txt3.GetComponent<RectTransform>();
                                    rt3.localPosition = new Vector3(5, -5, 0);
                                    txt3.font = txt.font;
                                    txt3.fontSize = txt.fontSize;
                                    txt3.fontStyle = txt.fontStyle;
                                    txt3.horizontalOverflow = HorizontalWrapMode.Overflow;
                                    txt3.verticalOverflow = VerticalWrapMode.Overflow;

                                    editor.textComponent = txt3;
                                    editor.targetGraphic = img;
                                    editor.caretWidth = 3;

                                    editor.text = ce.Value.GetSerializedValue();
                                    var ew = 1300;
                                    editorRt.localPosition = new Vector2(ew / 2, editorRt.localPosition.y);
                                    editorRt.sizeDelta = new Vector2(ew, txt.fontSize + 20);
                                    rt3.sizeDelta = editorRt.sizeDelta;

                                    editor.caretColor = Color.white;
                                    editor.enabled = false;
                                    editor.enabled = true;

                                    var resetBtn = new GameObject(editorGo.name + "-reset");

                                    resetBtn.transform.SetParent(txt.transform, false);

                                    resetBtn.AddComponent<GraphicRaycaster>();

                                    var resetRt = resetBtn.GetComponent<RectTransform>();

                                    var resetImg = resetBtn.AddComponent<Image>();
                                    resetImg.color = Color.white;
                                    resetImg.sprite = Sprite.Create(resetButtonImage, new Rect(0, 0, 24, 24), new Vector2(0.5f, 0.5f));

                                    var bw = txt.fontSize + 20;
                                    resetRt.localPosition = new Vector2(ew + bw / 2 + 5, editorRt.localPosition.y);
                                    resetRt.sizeDelta = new Vector2(bw, bw);

                                    var resetBtnBtn = resetBtn.AddComponent<Button>();

                                    var ceb = ce.Value;
                                    var pcfg = pi.Instance.Config;

                                    resetBtnBtn.onClick.AddListener(new UnityAction(() =>
                                    {
                                        ceb.BoxedValue = ceb.DefaultValue;
                                        pcfg.Save();

                                        mOnModConfigChanged?.Invoke(null, [ceb]);

                                        editor.text = ceb.GetSerializedValue();
                                    }));

                                    editor.onValueChanged.AddListener(new UnityAction<string>(e =>
                                    {
                                        ceb.SetSerializedValue(e);
                                        pcfg.Save();
                                        mOnModConfigChanged?.Invoke(null, [ceb]);
                                    }));

                                    var rbc = editorGo.AddComponent<ResetButtonChecker>();
                                    rbc.resetImage = resetImg;
                                    rbc.ceb = ceb;
                                }

                                j -= rt.sizeDelta.y;


                                var hover = amodCfg.gameObject.AddComponent<ModConfigEntryHover>();
                                hover.cfg = ce.Value;
                            }
                        }
                    }
                }
            }

            var contentRect = modScrollContent.GetComponent<RectTransform>();
            contentRect.sizeDelta = new Vector2(contentRect.sizeDelta.x, Math.Abs(j));

            var cntStr = cnt.ToString();
            if (cnt != pluginList.Count)
            {
                cntStr += " / " + pluginList.Count;
            }

            var filterCountTxt = filterCount.GetComponent<Text>();
            filterCountTxt.text = cntStr;
            var filterRt = filterGo.GetComponent<RectTransform>();
            filterCount.GetComponent<RectTransform>().localPosition = new Vector3(filterRt.sizeDelta.x / 2 + filterCountTxt.preferredWidth / 2 + 30, 0, 0);
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(Localization), "LoadLocalization")]
        static void Localization_LoadLocalization(
                Dictionary<string, Dictionary<string, string>> ___localizationDictionary
        )
        {
            if (___localizationDictionary.TryGetValue("hungarian", out var dict))
            {
                dict["ModConfigMenu_Button"] = "Modok";
            }
            if (___localizationDictionary.TryGetValue("english", out dict))
            {
                dict["ModConfigMenu_Button"] = "Mods";
            }
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(PlayerInputDispatcher), nameof(PlayerInputDispatcher.OnOpenConstructionDispatcher))]
        static bool PlayerInputDispatcher_OnOpenConstructionDispatcher()
        {
            var wh = Managers.GetManager<WindowsHandler>();
            return wh == null || wh.GetOpenedUi() != DataConfig.UiType.Options;
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(PlayerInputDispatcher), nameof(PlayerInputDispatcher.OnOpenInventoryDispatcher))]
        static bool PlayerInputDispatcher_OnOpenInventoryDispatcher()
        {
            var wh = Managers.GetManager<WindowsHandler>();
            return wh == null || wh.GetOpenedUi() != DataConfig.UiType.Options;
        }

        class ModConfigEntryHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
        {

            GameObject tooltipGo;

            internal ConfigEntryBase cfg;

            public void OnPointerEnter(PointerEventData eventData)
            {
                tooltipGo = new GameObject(gameObject.name + "-tooltip");
                tooltipGo.transform.SetParent(modScroll.transform.parent, false);

                var img = tooltipGo.AddComponent<Image>();
                img.color = new Color(0.2f, 0.2f, 0.3f);

                var outl = tooltipGo.AddComponent<Outline>();
                outl.effectColor = new Color(0.8f, 0.8f, 0.8f);
                outl.effectDistance = new Vector2(4, 4);

                var tooltipTxt = new GameObject(gameObject.name + "-tooltip-text");
                tooltipTxt.transform.SetParent(tooltipGo.transform, false);

                var txtExample = gameObject.GetComponentInChildren<Text>();
                var txt = tooltipTxt.AddComponent<Text>();

                txt.supportRichText = true;
                txt.font = txtExample.font;
                txt.fontSize = txtExample.fontSize;
                txt.fontStyle = FontStyle.Normal;
                txt.horizontalOverflow = HorizontalWrapMode.Overflow;
                txt.verticalOverflow = VerticalWrapMode.Overflow;
                txt.alignment = TextAnchor.MiddleCenter;

                txt.text = "<color=#FFFF00>" + cfg.Description.Description.Replace(". ", "\r\n") + "</color>\n\n"
                    + "Type: <color=#FF8080>" + cfg.SettingType + "</color>\r\nDefault: <color=#00FF00>" + LimitWidth(cfg.DefaultValue, 80) + "</color>";
                
                var rtTxt = tooltipTxt.GetComponent<RectTransform>();

                rtTxt.localPosition = new Vector3(0, 0, 0);
                rtTxt.sizeDelta = new Vector3(txt.preferredWidth, txt.preferredHeight, 0);

                var pos2 = tooltipGo.transform.InverseTransformPoint(gameObject.transform.position);

                var rtImg = tooltipGo.GetComponent<RectTransform>();
                rtImg.localPosition = pos2 + new Vector3(-1350 + txt.preferredWidth / 2, -txt.preferredHeight - 40); // new Vector3(-1000, -1000, 0);
                rtImg.sizeDelta = rtTxt.sizeDelta + new Vector2(20, 20);
            }

            static string LimitWidth(object obj, int maxChars)
            {
                if (obj is string txt)
                {
                    var sb = new StringBuilder(txt.Length + 32);

                    var split = txt.Split(',');
                    var lengthSoFar = 0;

                    foreach (var s in split)
                    {
                        if (sb.Length != 0)
                        {
                            sb.Append(',');
                        }
                        if (lengthSoFar + s.Length > maxChars)
                        {
                            sb.Append('\r').Append('\n');
                            lengthSoFar = 0;
                        }
                        sb.Append(s);
                        lengthSoFar += s.Length;
                    }


                    return sb.ToString();
                }
                if (obj == null)
                {
                    return "";
                }
                return obj.ToString();
            }

            public void OnPointerExit(PointerEventData eventData)
            {
                Destroy(tooltipGo);
            }
        }

        class ResetButtonChecker : MonoBehaviour
        {
            internal Image resetImage;
            internal ConfigEntryBase ceb;

            public void Update()
            {
                if (ceb.BoxedValue?.ToString() != ceb.DefaultValue?.ToString())
                {
                    resetImage.color = Color.green;
                }
                else 
                {
                    resetImage.color = Color.gray;
                }
            }
        }
    }
}
