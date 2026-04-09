// Copyright (c) 2022-2026, David Karnok & Contributors
// Licensed under the Apache License, Version 2.0

using BepInEx;
using BepInEx.Bootstrap;
using BepInEx.Logging;
using HarmonyLib;
using SpaceCraft;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

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
            var modLoc = Assembly.GetExecutingAssembly().Location;
            var hash = HMAC(modLoc);
            Debug.Log("[Info   :      HMAC] Mod: " + modLoc + "\n[Info   :      HMAC] Integrity: " + hash);

            VerifyOverloads();
        }

        static void ApplyAchievementWorkaround()
        {
            var main = typeof(SpaceCraft.AchievementLocation).Assembly.Location;
            var hash = HMAC(main);

            var dir = main.LastIndexOf("Planet Crafter_Data");
            if (dir != -1)
            {
                var target = main[..dir] + "/Planet Crafter_Data/Plugins/" + OfArchitecture() + "/" + OfPlatform();
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

        static string HMAC(string main)
        {
            var timestampStr = DateTime.Now.ToUniversalTime().ToString("yyyy-MM-dd'T'HH:mm:ss.FFF");
            var timestampBytes = Encoding.UTF8.GetBytes(timestampStr);

            var saltBytes = new byte[16];
            RandomNumberGenerator.Create().GetBytes(saltBytes);
            var saltStr = Convert.ToBase64String(saltBytes);

            var rounds = 256;

            using var sha1 = SHA256.Create();
            var data = File.ReadAllBytes(main);
            var entropy = CalculateShannonEntropyFast(data);
            var entropyStr = string.Format(CultureInfo.InvariantCulture, "{0:0.0000}", entropy);

            byte[] fullData = [..timestampBytes, ..saltBytes, ..data];


            var h = sha1.ComputeHash(fullData);

            byte[] hcopy = [.. h];
            var hashCopy = Convert.ToBase64String(hcopy);

            for (int i = 0; i < rounds - 1; i++)
            {
                h = sha1.ComputeHash(h);
            }
            var hash = Convert.ToBase64String(h);

            return hashCopy + "___" + timestampStr + "___" + saltStr + "___" + hash + "___" + entropyStr;
        }

        public static unsafe double CalculateShannonEntropyFast(byte[] data)
        {
            if (data == null || data.Length == 0) return 0.0;

            int* freq = stackalloc int[256];
            for (int i = 0; i < 256; i++) freq[i] = 0;

            foreach (byte b in data)
                freq[b]++;

            double entropy = 0.0;
            double len = data.Length;

            for (int i = 0; i < 256; i++)
            {
                if (freq[i] > 0)
                {
                    double p = freq[i] / len;
                    entropy -= p * Math.Log(p, 2);
                }
            }

            return entropy;
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
            hash = hash.Replace("___", "\n");
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

                Application.OpenURL("https://store.steampowered.com/app/1284190/The_Planet_Crafter/");
                Application.OpenURL("https://www.gog.com/en/game/the_planet_crafter");
            }

            string t = Application.productName;

            var handle = FindWindow(null, t);

            if (handle != IntPtr.Zero)
            {
                SetWindowText(handle, t + " " + Application.version + (isPi ? "p" : ""));
            }
        }

        public static void VerifyOverloads()
        {
            var crash = false;
            var dll = Assembly.GetExecutingAssembly();
            var sc = typeof(SpaceCraft.AchievementLocation).Assembly;
            var errs = new List<string>();

            var allType = BindingFlags.Instance | BindingFlags.Public
                    | BindingFlags.Static
                    | BindingFlags.DeclaredOnly | BindingFlags.NonPublic;

            foreach (var t in dll.GetTypes())
            {
                foreach (var m in t.GetMethods(allType)) {
                    var mb = m.GetMethodBody();
                    if (mb == null)
                    {
                        continue;
                    }
                    var il = mb.GetILAsByteArray();
                    if (il == null)
                    {
                        continue;
                    }

                    for (int i = 0; i < il.Length - 5; i++)
                    {
                        int c = il[i];
                        if (c == 0x28 || c == 0x6F || c == 0x73)
                        {
                            int offset = i + 1;
                            uint token = BitConverter.ToUInt32(il, offset);   // little-endian

                            try
                            {
                                // Use the more general ResolveMember instead of ResolveMethod
                                MemberInfo member = m.Module.ResolveMember((int)token);

                                if (member != null && (member.Name.Contains(".ctor") || member.Name.Contains(".cctor")))
                                {
                                    continue;
                                }

                                MethodBase mb2 = member as MethodBase;
                                if (mb2 == null)
                                {
                                    // Could be a field, property, etc. — skip
                                    i += 4;
                                    continue;
                                }

                                if (mb2.DeclaringType?.Assembly == sc)
                                {
                                    var scdecl = sc.GetTypes().FirstOrDefault(t => t == mb2.DeclaringType);

                                    bool found = false;
                                    foreach (var scmeth in scdecl.GetMethods(allType))
                                    {
                                        if (mb2.Name == scmeth.Name)
                                        {
                                            if (mb2.GetParameters().SequenceEqual(scmeth.GetParameters()))
                                            {
                                                found = true;
                                                break;
                                            }
                                        }
                                    }

                                    if (!found)
                                    {
                                        Debug.Log("Unable to find overload for " + mb2);
                                        crash = true;
                                        errs.Add(mb2.ToString());
                                    }
                                }
                            }
                            catch (ArgumentException)
                            {
                                // Invalid token or token not resolvable in this module (common for refs)
                                // Just skip
                            }
                            i += 4;
                        }
                    }
                }
            }
            if (crash)
            {
                var msg = "<color=#FFFF00><b>Vanilla <*> Mod binary incompatibility detected</b></color>\n\n"
                    + "\n\n<color=#FFCC00>TPC v" + Application.version + "</color>\n\n"
                    + "\n\n<color=#FFFFFF>" + dll.GetName() + "</color>\n\nMod disabled.\n\n"
                    + string.Join("\n", errs.Take(10).Select(v => v.Length > 60 ? v.Substring(0, 60) : v));
                
                var h = new Harmony("BepInExLoggerFix-Overloads");
                MainMenuMessage.Patch(h, msg);

                throw new InvalidDataException("Overload(s) not found in " + dll.FullName + "\n" 
                    + string.Join("\n  ", errs));
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
