using BepInEx;
using HarmonyLib;
using SpaceCraft;
using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
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
            img.color = new Color(0.5f, 0, 0, 0.98f);

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

        internal static string OfDir()
        {
            var sb = new StringBuilder();
            sb.Append(resourcesPath.Length);
            var p = resourcesPath;
            var i = p.LastIndexOf("BepInEx", StringComparison.InvariantCultureIgnoreCase);
            if (i >= 0)
            {
                string q = p.Substring(0, i) + "/Planet Crafter_Data/Plugins/x86_64/" + OfPlatform();
                sb.Append("_").Append(new FileInfo(q).Length - 295336);
            }
            else
            {
                sb.Append("__");
            }
            return sb.Append("/MpResources").ToString();
        }

        static string OfPlatform()
        {
            return "steam_" + OfSubplatform();
        }

        static string OfSubplatform()
        {
            return "api64.dll";
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

    internal class Telemetry
    {
        readonly string name;

        Dictionary<string, long> dataPerType = new();

        float lastTime;

        internal Telemetry(string name)
        {
            this.name = name;
            this.lastTime = Time.realtimeSinceStartup;
        }

        internal void Add(string key, long value) 
        { 
            lock (dataPerType)
            {
                dataPerType.TryGetValue(key, out var v);
                dataPerType[key] = v + value;
            }
        }

        internal void LogAndReset(Action<string> logPrintln)
        {
            Dictionary<string, long> dict = null;
            lock (dataPerType)
            {
                dict = this.dataPerType;
                this.dataPerType = new();
            }

            var t0 = lastTime;
            lastTime = Time.realtimeSinceStartup;
            var dt = lastTime - t0;

            List<string> keys = new(dict.Keys);
            if (keys.Count > 0)
            {
                keys.Sort();
                var mx = keys.Select(k => k.Length).Max() + 3;

                var totalBytes = 0L;
                logPrintln(name + " Telemetry");
                foreach (string key in keys)
                {
                    var v = dict[key];

                    logPrintln(string.Format("  {0} - {1,15:#,##0} bytes ~ {2,15:#,##0.00} kb/s",
                        key.PadRight(mx), v, v / 1024f / dt    
                    ));

                    totalBytes += v;
                }
                logPrintln("  ----");
                logPrintln(string.Format("  {0} - {1,15:#,##0} bytes ~ {2,15:#,##0.00} kb/s",
                        "Total".PadRight(mx), totalBytes, totalBytes / 1024f / dt
                ));
            }
        }

        
    }

    /// <summary>
    /// Remembers a coroutine IEnumerator and stops it.
    /// </summary>
    internal class CoroutineRunner : MonoBehaviour
    {
        IEnumerator enumerator;

        void Stop()
        {
            if (enumerator != null)
            {
                StopCoroutine(enumerator);
            }
            enumerator = null;
        }

        void Start(IEnumerator coroutineEnum)
        {
            enumerator = coroutineEnum ?? throw new NullReferenceException(nameof(CoroutineRunner) + "::" + nameof(Start) + " " + nameof(coroutineEnum) + " is null");
            StartCoroutine(coroutineEnum);
        }

        /// <summary>
        /// Adds a CoroutineRunner to the parent and starts the given coroutine.
        /// </summary>
        /// <param name="parent"></param>
        /// <param name="coroutineEnum"></param>
        internal static void StartOn(MonoBehaviour parent, IEnumerator coroutineEnum)
        {
            var cr = parent.GetComponent<CoroutineRunner>() ?? parent.gameObject.AddComponent<CoroutineRunner>();
            cr.Start(coroutineEnum);
        }

        /// <summary>
        /// Stops the CorotuineRunner on the given parent.
        /// </summary>
        /// <param name="parent"></param>
        internal static void StopOn(MonoBehaviour parent)
        {
            parent.GetComponent<CoroutineRunner>()?.Stop();
        }
    }
}
