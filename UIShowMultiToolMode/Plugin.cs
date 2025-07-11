﻿// Copyright (c) 2022-2025, David Karnok & Contributors
// Licensed under the Apache License, Version 2.0

using BepInEx;
using SpaceCraft;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using BepInEx.Configuration;

namespace UIShowMultiToolMode
{
    [BepInPlugin("akarnokd.theplanetcraftermods.uishowmultitoolmode", "(UI) Show MultiTool Mode", PluginInfo.PLUGIN_VERSION)]
    [BepInDependency(uiCraftEquipmentInPlaceGuid, BepInDependency.DependencyFlags.SoftDependency)]
    public class Plugin : BaseUnityPlugin
    {
        const string uiCraftEquipmentInPlaceGuid = "akarnokd.theplanetcraftermods.uicraftequipmentinplace";

        static ConfigEntry<bool> showText;
        static ConfigEntry<bool> showIcon;
        static ConfigEntry<int> fontSize;
        static ConfigEntry<int> iconSize;
        static ConfigEntry<int> textWidth;
        static ConfigEntry<int> transparencyPercent;
        static ConfigEntry<int> bottom;
        static ConfigEntry<int> right;

        static GameObject parent;
        static GameObject textBackground;
        static GameObject iconBackground;
        static GameObject textObject;
        static GameObject iconObject;

        static GameObject hud;
        static bool visualsToggleState;

        private void Awake()
        {
            LibCommon.BepInExLoggerFix.ApplyFix();

            // Plugin startup logic
            Logger.LogInfo($"Plugin is loaded!");

            showText = Config.Bind("General", "ShowText", true, "Show the current mode as text?");
            showIcon = Config.Bind("General", "ShowIcon", true, "Show the current mode as icon?");
            fontSize = Config.Bind("General", "FontSize", 15, "The size of the font used");
            iconSize = Config.Bind("General", "IconSize", 100, "The icon size");
            textWidth = Config.Bind("General", "TextWidth", 200, "The width of the text background");
            transparencyPercent = Config.Bind("General", "TransparencyPercent", 80, "How transparent the text/icon background should be.");
            bottom = Config.Bind("General", "Bottom", 30, "Position of the text from the bottom of the screen");
            right = Config.Bind("General", "Right", 10, "Position of the text from the right of the screen");

            LibCommon.HarmonyIntegrityCheck.Check(typeof(Plugin));
            Harmony.CreateAndPatchAll(typeof(Plugin));
        }

        void Update()
        {
            PlayersManager playersManager = Managers.GetManager<PlayersManager>();
            if (playersManager != null)
            {
                PlayerMainController player = playersManager.GetActivePlayerController();
                if (player != null)
                {
                    Setup();
                    Show(player.GetMultitool());
                    return;
                }
            }
            Teardown();
        }

        void Setup()
        {
            if (parent == null)
            {
                parent = new GameObject("MultiToolModeCanvas");
                Canvas canvas = parent.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;

                RectTransform rectTransform;
                Image image;
                Text text;
                Color bgColor = new(0, 0, 0, transparencyPercent.Value / 100f);

                int s = fontSize.Value;
                int pad = 10;
                int tw = textWidth.Value;
                int x = Screen.width / 2 - right.Value - tw / 2;
                int y = -Screen.height / 2 + bottom.Value + (s + pad) / 2;
                int ics = iconSize.Value;
                int xi = Screen.width / 2 - right.Value - ics / 2;
                int yi = y + (s + pad) / 2 + ics / 2;

                // ----------------------------

                textBackground = new GameObject();
                textBackground.transform.parent = parent.transform;

                image = textBackground.AddComponent<Image>();
                image.color = bgColor;

                rectTransform = image.GetComponent<RectTransform>();
                rectTransform.localPosition = new Vector3(x, y, 0);
                rectTransform.sizeDelta = new Vector2(tw, s + pad);

                textObject = new GameObject();
                textObject.transform.parent = parent.transform;

                text = textObject.AddComponent<Text>();
                text.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
                text.text = "";
                text.color = Color.white;
                text.fontSize = s;
                text.resizeTextForBestFit = false;
                text.verticalOverflow = VerticalWrapMode.Overflow;
                text.horizontalOverflow = HorizontalWrapMode.Overflow;
                text.alignment = TextAnchor.MiddleCenter;
                rectTransform = text.GetComponent<RectTransform>();
                rectTransform.localPosition = new Vector3(x, y, 0);
                rectTransform.sizeDelta = new Vector2(tw, s + pad);

                // -----------------------------

                iconBackground = new GameObject();
                iconBackground.transform.parent = parent.transform;

                image = iconBackground.AddComponent<Image>();
                image.color = bgColor;

                rectTransform = image.GetComponent<RectTransform>();
                rectTransform.localPosition = new Vector3(xi, yi, 0);
                rectTransform.sizeDelta = new Vector2(ics, ics);

                iconObject = new GameObject();
                iconObject.transform.parent = parent.transform;

                image = iconObject.AddComponent<Image>();
                image.color = Color.white;
                image.transform.localScale = new Vector3(1f, 1f, 1f);

                rectTransform = image.GetComponent<RectTransform>();
                rectTransform.localPosition = new Vector3(xi, yi, 0);
                rectTransform.sizeDelta = new Vector2(ics, ics);

                if (hud == null)
                {
                    hud = GameObject.Find("MainScene/BaseStack/UI/HUD");
                }
                visualsToggleState = true;
            }
        }
        void Show(PlayerMultitool multitool)
        {
            var state = multitool.GetState();
            var screen = multitool.GetComponentInChildren<MultiToolScreen>();

            iconBackground.SetActive(showIcon.Value && screen != null);
            iconObject.SetActive(showIcon.Value && screen != null);

            textBackground.SetActive(showText.Value && screen != null);
            textObject.SetActive(showText.Value && screen != null);

            parent.SetActive((hud == null
                || !hud.TryGetComponent<CanvasGroup>(out var cg)
                || cg.alpha > 0.99) && visualsToggleState);

            if (screen != null)
            {
                switch (state)
                {
                    case DataConfig.MultiToolState.Build:
                        {
                            textObject.GetComponent<Text>().text = Localization.GetLocalizedString("GROUP_NAME_MultiBuild");
                            iconObject.GetComponent<Image>().sprite = screen.illustrationBuild;
                            break;
                        }
                    case DataConfig.MultiToolState.Deconstruct:
                        {
                            textObject.GetComponent<Text>().text = Localization.GetLocalizedString("GROUP_NAME_MultiDeconstruct");
                            iconObject.GetComponent<Image>().sprite = screen.illustrationDeconstruct;
                            break;
                        }
                    default:
                        {
                            textObject.GetComponent<Text>().text = "< none >";
                            iconObject.GetComponent<Image>().sprite = screen.illustrationDefault;
                            break;
                        }
                }
            }
        }

        static void Teardown()
        {
            Destroy(textBackground);
            Destroy(textObject);
            Destroy(iconBackground);
            Destroy(iconObject);
            Destroy(parent);
            parent = null;
            textBackground = null;
            textObject = null;
            iconBackground = null;
            iconObject = null;
            visualsToggleState = true;
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(VisualsToggler), nameof(VisualsToggler.ToggleUi))]
        static void VisualsToggler_ToggleUi(List<GameObject> ___uisToHide)
        {
            bool active = ___uisToHide[0].activeSelf;
            parent?.SetActive(active);
            visualsToggleState = active;
        }

        static void OnModConfigChanged(ConfigEntryBase _)
        {
            Teardown();
        }
    }
}
