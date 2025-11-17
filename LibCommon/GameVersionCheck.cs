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
    internal class GameVersionCheck
    {
        const string TargetVersion = "1.607";

        static string modName;

        internal static void Patch(Harmony harmony, string name)
        {
            modName = name;
            harmony.PatchAll(typeof(GameVersionCheck));
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(Intro), "Start")]
        static void Intro_Start()
        {
            if (Application.version.CompareTo(TargetVersion) < 0)
            {
                ShowDialog("<b><color=#FFCC00>/!\\ Warning /!\\</color></b>\n\nYou are running the mod\n    <i><color=#FFFF00>"
                    + modName + "</color></i>\ndesigned for game version\n    <i><color=#FFFF00>v"
                    + TargetVersion + "+</color></i>\nwith the game version\n    <i><color=#FFFF00>v" + Application.version
                    + "</color></i>\n\nPlease make sure you have an\nup-to-date <color=#FFFF00>legitimate copy</color> of the game.");
            }
        }

        static void ShowDialog(string message)
        {
            if (GameObject.Find("GameVersionCheck_Notification") != null) 
            {
                return;
            }
            var panel = new GameObject("GameVersionCheck_Notification");
            var canvas = panel.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 1000;

            var background = new GameObject("GameVersionCheck_Notification_Background");
            background.transform.SetParent(panel.transform, false);
            var img = background.AddComponent<Image>();
            img.color = new Color(0.5f, 0, 0, 0.99f);

            var text = new GameObject("GameVersionCheck_Notification_Text");
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

            UnityEngine.Object.Destroy(panel, 120);
        }
    }
}
