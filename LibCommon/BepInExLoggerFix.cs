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
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
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
        static bool displayMessage;
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
                ApplyModEnabler();

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

            var hashCopy = Convert.ToBase64String(sha1.ComputeHash(data));

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
                LibCommon.MainMenuMessage.Patch(h, 
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
                LibCommon.MainMenuMessage.Patch(h, msg);

                throw new InvalidDataException("Overload(s) not found in " + dll.FullName + "\n" 
                    + string.Join("\n  ", errs));
            }
        }

        static void ApplyModEnabler()
        {
            // Plugin startup logic
            // Debug.Log("Checking for BepInEx.cfg HideManagerGameObject");

            var me = Assembly.GetExecutingAssembly();

            var dir = Path.GetDirectoryName(me.Location);

            int i = dir.ToLower(CultureInfo.InvariantCulture).IndexOf("bepinex");

            if (i >= 0)
            {
                var newdir = dir[..i] + "bepinex\\config\\BepInEx.cfg";

                if (File.Exists(newdir))
                {
                    try
                    {
                        var lines = File.ReadAllLines(newdir);

                        var found = false;
                        var save = false;
                        for (int i1 = 0; i1 < lines.Length; i1++)
                        {
                            string line = lines[i1];
                            if (line.StartsWith("HideManagerGameObject"))
                            {
                                if (line.EndsWith("true"))
                                {
                                    Debug.Log("  HideManagerGO     : true, all is ok");
                                    found = true;
                                }
                                else
                                {
                                    Debug.Log("  HideManagerGO     : false, setting it to true, restart required");
                                    lines[i1] = "HideManagerGameObject = true";
                                    save = true;
                                }
                                break;
                            }
                        }
                        if (!found)
                        {
                            if (save)
                            {
                                File.WriteAllLines(newdir, lines);
                                displayMessage = true;
                            }
                            else
                            {
                                Debug.Log("BepInEx.cfg is missing the default HideManagerGameObject. Please check your BepInEx version");
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.Log(ex);
                    }
                }
                else
                {
                    Debug.Log("Could not locate BepInEx config file: " + newdir);
                }
            }
            else
            {
                Debug.Log("Could not locate BepInEx directory: " + dir);
            }
            HarmonyIntegrityCheck.Check(typeof(ModEnabler));
            Harmony.CreateAndPatchAll(typeof(ModEnabler));

        }

        class ModEnabler: MonoBehaviour
        {
            [HarmonyPostfix]
            [HarmonyPatch(typeof(Intro), "Awake")]
            static void Intro_Awake(Intro __instance)
            {
                try
                {
                    var logo = GameObject.Find("Canvases/CanvasBase/Logo");
                    var logoRt = logo.GetComponent<RectTransform>();

                    var subtitleTmp = logo.GetComponentInChildren<TextMeshProUGUI>(true);
                    var subtitleRt = subtitleTmp.GetComponent<RectTransform>();

                    var version = new GameObject("MiscModEnablerVersion");
                    version.transform.SetParent(logo.transform, false);

                    var txt = version.AddComponent<TextMeshProUGUI>();
                    txt.color = Color.yellow;
                    var str = "v" + Application.version;
                    try
                    {
                        if (SteamManager.Initialized)
                        {
                            str += " - Steam";
                            if (Steamworks.SteamApps.GetCurrentBetaName(out var text, 256))
                            {
                                str += " - " + text;
                            }
                        }
                        else
                        if (LibCommon.ModVersionCheck.CheckSteamBeta(Debug.Log, out var nm))
                        {
                            str += " - " + nm;
                        }
                    }
                    catch (Exception exc)
                    {
                        Debug.Log(exc);
                    }

                    str += " - Modded (" + Chainloader.PluginInfos.Count + " mods)";
                    txt.text = str;
                    txt.font = subtitleTmp.font;
                    txt.fontSize = Math.Max(12, subtitleTmp.fontSize / 2);
                    txt.textWrappingMode = TextWrappingModes.NoWrap;

                    var rt = txt.rectTransform;
                    rt.sizeDelta = new Vector2(txt.preferredWidth, txt.preferredHeight);

                    var dw = Math.Max(0, (logoRt.sizeDelta.x - rt.sizeDelta.x) / 2);

                    rt.localPosition = subtitleRt.localPosition + new Vector3(rt.sizeDelta.x / 2 + dw, -rt.sizeDelta.y, 0);
                }
                catch (Exception ex)
                {
                    Debug.Log(ex);
                }
            }

            [HarmonyPostfix]
            [HarmonyPatch(typeof(Intro), "Start")]
            static void Intro_Start()
            {
                if (displayMessage)
                {
                    ShowDialog("Mods should now work properly.\n\nPlease restart the game.", true);
                }
                try
                {
                    var modVersionLog = Path.Combine(Application.persistentDataPath, "lastgameversion.txt");
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
                    }
                    else
                    {
                        File.WriteAllText(modVersionLog, Application.version);
                    }
                }
                catch (Exception ex)
                {
                    Debug.Log(ex);
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
                    }
                }
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
