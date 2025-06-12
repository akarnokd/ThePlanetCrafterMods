// Copyright (c) 2022-2025, David Karnok & Contributors
// Licensed under the Apache License, Version 2.0

using HarmonyLib;
using SpaceCraft;
using UnityEngine;
using UnityEngine.UI;

namespace LibCommon
{
    /// <summary>
    /// Displays a warning dialog if the target version differs from the game version.
    /// </summary>
    internal class MainMenuMessage
    {
        static string text = "";

        internal static void Patch(Harmony harmony, string text)
        {
            MainMenuMessage.text = text;
            harmony.PatchAll(typeof(MainMenuMessage));
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(Intro), "Start")]
        static void Intro_Start()
        {
            ShowDialog(text);
        }

        internal static void ShowDialog(string message)
        {
            if (GameObject.Find("MainMenuMessage_Notification") != null) 
            {
                return;
            }
            var panel = new GameObject("MainMenuMessage_Notification");
            var canvas = panel.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 1000;

            var background = new GameObject("MainMenuMessage_Notification_Background");
            background.transform.SetParent(panel.transform, false);
            var img = background.AddComponent<Image>();
            img.color = new Color(0.5f, 0, 0, 0.99f);

            var text = new GameObject("MainMenuMessage_Notification_Text");
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
            brect.sizeDelta = trect.sizeDelta + new Vector2(10, 10);

            UnityEngine.Object.Destroy(panel, 120);
        }
    }
}
