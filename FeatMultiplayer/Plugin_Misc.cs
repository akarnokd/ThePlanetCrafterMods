using BepInEx;
using HarmonyLib;
using SpaceCraft;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

namespace FeatMultiplayer
{
    public partial class Plugin : BaseUnityPlugin
    {
        static void NotifyUser(string message, float duration = 5f)
        {
            // Managers.GetManager<BaseHudHandler>()?.DisplayCursorText("", duration, message);

            var panel = new GameObject("Multiplayer_Notification");
            var canvas = panel.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;

            var background = new GameObject("Multiplayer_Notification_Background");
            background.transform.SetParent(panel.transform, false);
            var img = background.AddComponent<Image>();
            img.color = new Color(0.5f, 0, 0, 0.95f);

            var text = new GameObject("Multiplayer_Notification_Text");
            text.transform.SetParent(background.transform, false);

            var txt = text.AddComponent<Text>();
            txt.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            txt.supportRichText = true;
            txt.text = message;
            txt.color = Color.white;
            txt.fontSize = (int)fontSize.Value * 2;
            txt.resizeTextForBestFit = false;
            txt.verticalOverflow = VerticalWrapMode.Overflow;
            txt.horizontalOverflow = HorizontalWrapMode.Overflow;
            txt.alignment = TextAnchor.MiddleLeft;

            var trect = text.GetComponent<RectTransform>();
            trect.sizeDelta = new Vector2(txt.preferredWidth, txt.preferredHeight);

            var brect = background.GetComponent<RectTransform>();
            brect.sizeDelta = trect.sizeDelta + new Vector2(10, 10);

            Destroy(panel, duration);
        }

        static void NotifyUserFromBackground(string message, float duration = 5f)
        {
            var msg = new NotifyUserMessage
            {
                message = message,
                duration = duration
            };
            _receiveQueue.Enqueue(msg);
        }

        static void ToggleConsumption()
        {
            slowdownConsumption.Value = !slowdownConsumption.Value;
            LogInfo("SlowdownConsumption: " + slowdownConsumption.Value);

            ResetGaugeConsumptions();
        }
    }
}
