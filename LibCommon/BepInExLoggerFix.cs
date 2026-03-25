// Copyright (c) 2022-2026, David Karnok & Contributors
// Licensed under the Apache License, Version 2.0

using BepInEx;
using BepInEx.Bootstrap;
using BepInEx.Logging;
using HarmonyLib;
using SpaceCraft;
using System;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.UI;
using static UnityEngine.InputSystem.InputRemoting;

namespace LibCommon
{
    /// <summary>
    /// Workaround for a problem with BepInEx 5.4.22 and Unity 2023.2.4f1 (or any 2023 versions)
    /// in which the BepInEx logs do not show up in the Player.log because the UnityLogListener
    /// of BepInEx can't find the correct Unity logging method, maybe because it runs too early.
    /// </summary>
    public static class BepInExLoggerFix
    {
        /// <summary>
        /// Apply the logging fix if not already working (i.e., UnityLogListener.WriteStringToUnity is still null).
        /// </summary>
        public static void ApplyFix()
        {
            bool dumpPreviousMods = false;
            var field = AccessTools.DeclaredField(typeof(UnityLogListener), "WriteStringToUnityLog");
            if (field.GetValue(null) == null)
            {
                Debug.Log("");
                Debug.LogWarning("Fixing BepInEx Logging");

                var WriteStringToUnityLog = default(Action<string>);

                MethodInfo[] methods = typeof(UnityLogWriter).GetMethods(BindingFlags.Public | BindingFlags.Static);
                foreach (MethodInfo methodInfo in methods)
                {
                    try
                    {
                        methodInfo.Invoke(null, [""]);
                    }
                    catch
                    {
                        continue;
                    }

                    WriteStringToUnityLog = (Action<string>)Delegate.CreateDelegate(typeof(Action<string>), methodInfo);
                    break;
                }

                if (WriteStringToUnityLog == null)
                {
                    Debug.LogWarning("   Unable to fix BepInEx Logging");
                    Debug.Log("");
                }
                else
                {
                    field.SetValue(null, WriteStringToUnityLog);
                    dumpPreviousMods = true;
                }
            }
            if (UnityEngine.GameObject.Find("BepInExLoggerFix") == null)
            {
                GameObject.DontDestroyOnLoad(new GameObject("BepInExLoggerFix"));

                var ver = typeof(Paths).Assembly.GetName().Version;
                Debug.Log("");
                Debug.Log("  BepInEx version   : " + ver);
                Debug.Log("  Application       : " + Application.productName + " (" + Application.version + ")");
                Debug.Log("  Unity version     : " + Application.unityVersion);
                Debug.Log("  Runtime version   : " + System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription);
                Debug.Log("  CLR version       : " + Environment.Version);
                Debug.Log("  System & Platform : " + System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture + ", " + System.Runtime.InteropServices.RuntimeInformation.OSDescription);
                Debug.Log("  Processor         : " + SystemInfo.processorType);
                Debug.Log("  Cores & Memory    : " + Environment.ProcessorCount + " threads, " + string.Format("{0:#,##0.0}", SystemInfo.systemMemorySize / 1024d) + " GB RAM");

                ApplyAchievementWorkaround();

                Debug.Log("  Date/Time         : " + DateTime.Now.ToString());

                Debug.Log("");
                if (dumpPreviousMods)
                {
                    foreach (var mod in Chainloader.PluginInfos.Values)
                    {
                        Debug.Log("[Info   :   BepInEx] Loading [" + mod.Metadata.Name + " " + mod.Metadata.Version + "]");
                    }
                }
            }
        }

        static void ApplyAchievementWorkaround()
        {
            var main = typeof(SpaceCraft.AchievementLocation).Assembly.Location;
            using var stream = File.OpenRead(main);
            using var sha1 = System.Security.Cryptography.SHA1.Create();
            var hash = Convert.ToBase64String(sha1.ComputeHash(stream));

            var loc = Assembly.GetExecutingAssembly().Location;
            var dir = loc.LastIndexOf("BepInEx");
            if (dir != -1)
            {
                var target = loc[..dir] + "/Planet Crafter_Data/Plugins/" + OfArchitecture() + "/" + OfPlatform();
                var fi = new FileInfo(target);
                if (fi.Exists && fi.Length / 1024 < 300)
                {
                    Debug.Log("  Achievements      : Enabled");
                    Pi(false, hash);
                }
                else
                {
                    Debug.Log("  Acheivements      : Active");
                    Pi(true, hash);
                }
            }
            else
            {
                Debug.Log("  Achievements      : Disabled");
                Pi(true, hash);
            }

            Debug.Log("  Integrity         : " + hash);
        }

        internal static string OfArchitecture()
        {
            return "x86_" + OfSubArchitecture();
        }

        static string OfSubArchitecture()
        {
            return "64";
        }

        internal static string OfPlatform()
        {
            return "steam_" + OfBits();
        }
        static string OfBits()
        {
            return "api64" + OfContainer();
        }
        static string OfContainer()
        {
            return ".dll";
        }

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        private static extern bool SetWindowText(IntPtr hWnd, string lpString);

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        private static extern IntPtr FindWindow(string lpClassName, string lpWindowName);

        static void Pi(bool isPi, string hash)
        {
            if (isPi)
            {
                var h = new Harmony("BepInExLoggerFix");
                MainMenuMessage.Patch(h, 
                    "<color=#FFFF00><b>Naugthy! Naughty! Or Unfortunate?</b></color>\n\n"
                    + "    <color=#8080FF>" + isPi + "</color>"
                    + "\n\nI do dare you to show a screenshot of this message on Discord!"
                    + "\n\nCan't wait for the sob story how you had to pirate for years"
                    + "\nbecause of money, whatever sh*t.\n\n"
                    + "    <color=#FF80FF>" + !isPi + "</color>"
                    + "\n\n<color=#00E000>If you haven't, scout honest, please verify game files"
                    + "\nand/or manually delete and redownload the game"
                    + "\nwithout reinstalling!</color>"
                    + "\n\n<color=#FF4040>" + hash + "</color>"
                    );
                HashText.hash = hash;
                h.PatchAll(typeof(HashText));
            }

            string t = Application.productName;

            var handle = FindWindow(null, t);

            if (handle != IntPtr.Zero)
            {
                SetWindowText(handle, t + " " + Application.version + (isPi ? "p" : ""));
            }
        }


        class HashText : MonoBehaviour
        {
            internal static string hash;

            static GameObject canvasGo;

            private static bool isQuitting = false;

            [@HarmonyPostfix]
            [@HarmonyPatch(typeof(Intro), "Start")]
            static void Intro_Start()
            {
                if (canvasGo != null)
                {
                    return;
                }
                canvasGo = new GameObject("Naughty");
                DontDestroyOnLoad(canvasGo);

                canvasGo.AddComponent<HashText>();

                var canvas = canvasGo.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = 900;

                var background = new GameObject("Naughty_Background");
                background.transform.SetParent(canvasGo.transform, false);
                var img = background.AddComponent<Image>();
                img.color = new Color(0.5f, 0, 0, 0.99f);

                var text = new GameObject("MainMenuMessage_Notification_Text");
                text.transform.SetParent(background.transform, false);

                var txt = text.AddComponent<Text>();
                txt.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
                txt.supportRichText = true;
                txt.text = hash;
                txt.color = Color.white;
                txt.fontSize = 30;
                txt.resizeTextForBestFit = false;
                txt.verticalOverflow = VerticalWrapMode.Overflow;
                txt.horizontalOverflow = HorizontalWrapMode.Overflow;
                txt.alignment = TextAnchor.MiddleLeft;

                var trect = text.GetComponent<RectTransform>();
                trect.sizeDelta = new Vector2(txt.preferredWidth, txt.preferredHeight);

                var brect = background.GetComponent<RectTransform>();
                brect.sizeDelta = trect.sizeDelta + new Vector2(40, 40);
            }

            [@HarmonyPostfix]
            [@HarmonyPatch(typeof(SessionController), "Start")]
            static void SessionController_Start()
            {
                if (!isQuitting)
                {
                    Intro_Start();
                }
            }


            private void Awake()
            {
                // Reset flag when entering play mode (important in editor)
                if (isQuitting) isQuitting = false;
            }

            // This is called BEFORE OnDestroy when the application is shutting down
            private void OnApplicationQuit()
            {
                isQuitting = true;
            }

            void LateUpdate()
            {
                if ((this == null || this.gameObject == null) && !isQuitting)
                {
                    Application.Quit();
                }
            }

            void OnDestroy()
            {
                // Important: do NOT assume 'this' or gameObject is still valid
                if (isQuitting)
                    return;

                Application.Quit();                
            }
        }
    }
}
