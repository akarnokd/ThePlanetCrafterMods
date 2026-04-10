// Copyright (c) 2022-2026, David Karnok & Contributors
// Licensed under the Apache License, Version 2.0

using BepInEx;
using HarmonyLib;
using MonoMod.Utils;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SpaceCraft;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Cache;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.UI;
using OutOfShape = (string guid, string version, string description, bool hashError, string repo);
using RemoteInfo = (string description, string version, string hash, string directory, string repo);

namespace LibCommon
{
    /// <summary>
    /// Once per game process, downloads the latest version numbers from
    /// supported repositories and allows each calling mod to check
    /// for newer version as they are.
    /// 
    /// The code ensures if multiple mods use this utility, only one is checking
    /// and creating notifications about outdated mods.
    /// 
    /// Put these into your Awake() method.
    /// <code>
    /// if (LibCommon.ModVersionCheck.Check(this, Logger.LogInfo))
    /// {
    ///     LibCommon.ModVersionCheck.NotifyUser(this, Logger.LogInfo);
    /// }
    /// </code>
    /// </summary>
    public static class ModVersionCheck
    {
        /// <summary>
        /// The currently supported list of GitHub repositories.
        /// </summary>
        static readonly string[] REPOSITORIES = {
            "akarnokd/ThePlanetCrafterMods",
            "Tjatja0/ThePlanetCrafterMods",
            "mcnicki2002/PlanetCrafterMods",
        };

        static Dictionary<string, RemoteInfo> versionInfo = [];

        static GameObject versionCheckGameObject;

        static GameObject notificationGameObject;

        static List<OutOfShape> outOfDateOrShape = [];

        /// <summary>
        /// Checks if the given plugin has any newer version online.
        /// </summary>
        /// <param name="plugin">The current plugin to check.</param>
        /// <param name="logInfo">Where to log debug information.</param>
        /// <returns>True if a newer version is available, false if not or there was an error</returns>
        public static bool Check(BaseUnityPlugin plugin, Action<object> logInfo, out bool hashError, out string repoURL)
        {
            using var sha2 = SHA256.Create();
            var filename = plugin.GetType().Assembly.Location;
            var hash = sha2.ComputeHash(File.ReadAllBytes(plugin.GetType().Assembly.Location));
            var hashStr = Convert.ToBase64String(hash);

            if (File.Exists(filename.Replace(".dll", ".pdb", StringComparison.InvariantCultureIgnoreCase)))
            {
                hashStr = ""; // ignore dev deployments locally if we have PDBs along with the DLLs
            }

            return Check(plugin.Info.Metadata.GUID, plugin.Info.Metadata.Version.ToString(), hashStr, logInfo, out hashError, out repoURL);
        }

        public static bool Check(string localModGuid, string localModVersion, string localHash, 
            Action<object> logInfo, out bool hashError, out string repoURL)
        {
            var killswitch = Environment.GetEnvironmentVariable("TPC_MODVERSIONCHECK_OFF");
            if (!string.IsNullOrEmpty(killswitch))
            {
                hashError = false;
                repoURL = "";
                return false;
            }

            versionCheckGameObject = GameObject.Find("ModVersionCheck");
            if (versionCheckGameObject == null)
            {
                var sw = Stopwatch.StartNew();
                logInfo("[ModVersionCheck] Initializing");
                versionCheckGameObject = new GameObject("ModVersionCheck");
                GameObject.DontDestroyOnLoad(versionCheckGameObject);
                var m = versionCheckGameObject.AddComponent<ModVersionCheckBehaviour>();
                m.dictionary = versionInfo;
                m.list = outOfDateOrShape;
                var beta = CheckSteamBeta(logInfo, out _);

                List<Task<Dictionary<string, RemoteInfo>>> allTasks = [];
                foreach (var repo in REPOSITORIES)
                {
                    var frepo = repo;
                    allTasks.Add(Task.Run<Dictionary<string, RemoteInfo>>(() =>
                    {
                        try
                        {
                            return GetVersionTxt(frepo, beta, logInfo);
                        }
                        catch (Exception ex)
                        {
                            logInfo("[ModVersionCheck]   Repo download failed: " + frepo + " (" + ex.Message + ")");
                            logInfo(ex);
                        }
                        return [];
                    }));
                }

                Task.WaitAll(allTasks.ToArray());

                logInfo("[ModVersionCheck] Initializing Complete in " + sw.ElapsedMilliseconds + " ms");

                versionInfo.Clear();

                foreach (var t in allTasks)
                {
                    versionInfo.AddRange(t.Result);
                }
            } 
            else
            {
                logInfo("[ModVersionCheck] Accessing existing data");

                foreach (var comp in versionCheckGameObject.GetComponents<MonoBehaviour>())
                {
                    if (comp.GetType().Name == "ModVersionCheckBehaviour")
                    {
                        versionInfo = AccessTools.FieldRefAccess<Dictionary<string, RemoteInfo>>(comp.GetType(), "dictionary")(comp);
                        // logInfo("[ModVersionCheck]   Found versionInfo with count " + versionInfo.Count);
                        outOfDateOrShape = AccessTools.FieldRefAccess<List<OutOfShape>>(comp.GetType(), "list")(comp);
                        // logInfo("[ModVersionCheck]   Found outOfDate with count " + outOfDateOrShape.Count);
                        break;
                    }
                }
            }
            if (versionInfo.TryGetValue(localModGuid, out var version))
            {
                var localVer = new Version(localModVersion);
                var remoteVer = new Version(version.version);
                var remoteHash = version.hash;
                var hasNewer = remoteVer > localVer;
                var sameHash = remoteHash == localHash || "" == remoteHash || "" == localHash;
                logInfo("[ModVersionCheck] " + localVer + " <-> " + remoteVer + (hasNewer ? " | Update Available" : ""));
                logInfo("[ModHashingCheck] " + localHash + " <-> " + remoteHash + (sameHash ? "" : " | CORRUPTED?"));
                hashError = !sameHash;
                repoURL = version.repo;
                return hasNewer || !sameHash;
            }
            logInfo("[ModVersionCheck] " + localModGuid + " not found [" + versionInfo.Count + "]" + string.Join("\n", versionInfo));

            hashError = false;
            repoURL = "";
            return false;
        }

        /// <summary>
        /// Indicate that the given mod needs to notify the user about an update
        /// once the game reaches the main menu.
        /// If multiple mods indicate, they are combined into a list.
        /// </summary>
        /// <param name="plugin">The current plugin to check.</param>
        /// <param name="logInfo">Where to log debug information.</param>
        public static void NotifyUser(BaseUnityPlugin plugin, bool hashError, string repo, Action<object> logInfo)
        {
            NotifyUser(plugin.Info.Metadata.GUID, plugin.Info.Metadata.Version.ToString(), 
                plugin.Info.Metadata.Name, hashError, repo, logInfo);
        }

        public static void NotifyUser(string localModGuid, string localVersion,
            string localDescription, bool hashError, string repo, Action<object> logInfo)
        {
            if (outOfDateOrShape.Any(e => e.Item1 == localModGuid))
            {
                return;
            }
            logInfo((localModGuid, localVersion, localDescription, hashError));
            outOfDateOrShape.Add((localModGuid, localVersion, localDescription, hashError, repo));

            notificationGameObject = GameObject.Find("ModVersionNotify");
            if (notificationGameObject == null)
            {
                UnityEngine.Debug.Log("Creating ModVersionNotify");
                notificationGameObject = new GameObject("ModVersionNotify");
                GameObject.DontDestroyOnLoad(notificationGameObject);
                var canvas = notificationGameObject.AddComponent<Canvas>();
                canvas.sortingOrder = 1100;
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;

                Harmony.CreateAndPatchAll(typeof(DialogCloser));
            }
            else
            {
                UnityEngine.Debug.Log("Found ModVersionNotify");
            }
        }

        public class ModVersionCheckBehaviour : MonoBehaviour
        {
            public Dictionary<string, RemoteInfo> dictionary;

            public List<OutOfShape> list;
        }

        class DialogCloser : MonoBehaviour
        {
            public static RectTransform textRect;        // Assign the Text's RectTransform
            public static RectTransform maskRect;        // Optional: the Panel's RectTransform for bounds checking
            public static Text txt;
            public static float scrollSpeed = 3f;       // Pixels per second

            private float maxScroll;         // how far we can scroll down

            [HarmonyPostfix]
            [HarmonyPatch(typeof(Intro), "Start")]
            static void Intro_Start()
            {
                // notificationGameObject.SetActive(true);
                for (int i = notificationGameObject.transform.childCount - 1; i >= 0; i--)
                {
                    UnityEngine.Object.Destroy(notificationGameObject.transform.GetChild(i).gameObject);
                }

                var background = new GameObject("ModVersionNotifyBackground");
                background.transform.SetParent(notificationGameObject.transform, false);
                background.AddComponent<DialogCloser>();

                var outline = background.AddComponent<Outline>();
                outline.effectDistance = new Vector2(2, 2);
                outline.effectColor = new Color(1f, 0.75f, 0, 1f);

                var img = background.AddComponent<Image>();
                img.color = new Color(0, 0, 0, 0.995f);

                var rect = background.GetComponent<RectTransform>();
                rect.sizeDelta = new Vector2(Screen.width * 3 / 4, Screen.height * 3 / 4);

                maskRect = background.AddComponent<RectMask2D>().GetComponent<RectTransform>();

                var textGo = new GameObject("ModVersionNotifyText");
                textGo.transform.SetParent(background.transform, false);
                txt = textGo.AddComponent<Text>();

                txt.fontSize = 25;
                txt.supportRichText = true;
                txt.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
                txt.color = Color.white;
                txt.alignment = TextAnchor.UpperCenter;
                txt.horizontalOverflow = HorizontalWrapMode.Overflow;
                txt.verticalOverflow = VerticalWrapMode.Overflow;

                textRect = textGo.GetComponent<RectTransform>();
                textRect.pivot = new(textRect.pivot.x, 1); 

                txt.text = "<color=#FFCC00><b><size=40>/!\\/!\\ " + outOfDateOrShape.Count + " mod(s) need attention /!\\/!\\</size></b></color>";
                txt.text += "\nPlease update them as soon as possible.";
                txt.text += "\n<color=#CCFFCC>[= Press Ctrl+Enter to upgrade/fix mods. =]</color>";
                txt.text += "\n<color=#CCCCFF>[=    Up/Down arrow or Mouse to scroll.  =]</color>";
                txt.text += "\n<color=#CCFF00>[=  Press ESC or Controller B to close.  =]</color>";

                var noDevChecks = !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("TPC_NODEVCHECKS_ON"));

                var idx = 1;
                foreach (var md in outOfDateOrShape)
                {
                    versionInfo.TryGetValue(md.guid, out var v2);
                    UnityEngine.Debug.Log(md);
                    UnityEngine.Debug.Log(v2);
                    if (md.version != v2.version)
                    {
                        txt.text += "\n\n<color=#C0C0C0>" + idx + ")</color> " + md.description + "  ~~~~  v" + md.version + " :::: <color=#80FF80>v" + (v2.version ?? "??") + "</color>";
                    }
                    else if (md.hashError && !noDevChecks)
                    {
                        txt.text += "\n\n<color=#C0C0C0>" + idx + ")</color> " + md.description + "  ~~~~    <color=#80FF80>v" + md.version + "</color> :::: <color=#FF8080>CORRUPTION?</color>";
                    }

                    idx++;
                }

                textRect.sizeDelta = new Vector2(txt.preferredWidth, txt.preferredHeight);
            }

            void Start()
            {
                // Force layout so the text calculates its full preferred height
                LayoutRebuilder.ForceRebuildLayoutImmediate(textRect);

                // Calculate how much content is overflowing
                float visibleHeight = maskRect != null ? maskRect.rect.height : textRect.rect.height;
                float contentHeight = txt.preferredHeight;   // full height after layout

                maxScroll = Mathf.Max(0f, contentHeight - visibleHeight);

                // === THIS IS THE KEY LINE TO START AT THE TOP ===
                textRect.anchoredPosition = new Vector2(textRect.anchoredPosition.x, maskRect.rect.height / 2);
            }

            public void ShiftText(float deltaY)
            {
                Vector2 pos = textRect.anchoredPosition;
                pos.y += deltaY;                          // positive delta = scroll up (reveal earlier text)

                // Clamp so we never go above the header (y=0) or below the bottom
                pos.y = Mathf.Clamp(pos.y, maskRect.rect.height / 2, maskRect.rect.height / 2 + maxScroll);

                textRect.anchoredPosition = pos;
            }

            // Optional: continuous auto-scroll example
            void Update()
            {
                if (Keyboard.current[Key.DownArrow].wasPressedThisFrame
                    || Mouse.current.scroll.y.value < 0)
                {
                    ShiftText(scrollSpeed  * txt.fontSize);
                }

                if (Keyboard.current[Key.UpArrow].wasPressedThisFrame
                    || Mouse.current.scroll.y.value > 0)
                {
                    ShiftText(-scrollSpeed * txt.fontSize);
                }

                var gamepad = Gamepad.current;
                if (Keyboard.current[Key.Escape].wasPressedThisFrame
                    || (gamepad != null && (gamepad[GamepadButton.B]?.wasPressedThisFrame ?? false)))
                {
                    Destroy(gameObject);
                }
                if ((Keyboard.current[Key.Enter].wasPressedThisFrame || Keyboard.current[Key.NumpadEnter].wasPressedThisFrame)
                    && (Keyboard.current[Key.LeftCtrl].isPressed || Keyboard.current[Key.RightCtrl].isPressed))
                {
                    // TODO
                }
            }

        }

        public static bool CheckSteamBeta(Action<object> logInfo, out string betaName)
        {
            var loc = typeof(Intro).Assembly.Location;
            var steamapps = loc.IndexOf("steamapps", StringComparison.InvariantCultureIgnoreCase);
            if (steamapps > 0)
            {
                var manifest = loc.Substring(0, steamapps + 10) + "\\appmanifest_1284190.acf";
                logInfo("[ModVersionCheck]   Checking Steam Beta branch: " + manifest);
                try
                {
                    var text = File.ReadAllText(manifest);

                    var regexp = new Regex("\"BetaKey\"\\s+\"(.+?)\"");
                    var match = regexp.Match(text);
                    if (match.Groups[1].Success && match.Groups[1].Value != "public")
                    {
                        logInfo("[ModVersionCheck]   Beta name: " + match.Groups[1].Value);
                        betaName = match.Groups[1].Value;
                        return true;
                    }
                }
                catch (Exception e)
                {
                    logInfo("[ModVersionCheck]   Unable to read game manifest: \r\n" + e);
                }
            }
            betaName = null;
            return false;
        }

        static Dictionary<string, RemoteInfo> GetVersionTxt(string repository, bool beta, Action<object> logInfo)
        {
            var startUrl = "https://raw.githubusercontent.com/" + repository + "/main/version_info.txt";

            if (beta) {
                var branches = GetBranches(repository, logInfo);
                branches.Sort();
                branches.Reverse();

                foreach (var b in branches)
                {
                    if (b != "main")
                    {
                        startUrl = "https://raw.githubusercontent.com/" + repository + "/" + b + "/version_info.txt";
                        break;
                    }
                }
            }

            logInfo("[ModVersionCheck] Download " + startUrl);

            Dictionary<string, RemoteInfo> plugins = [];

            var request = WebRequest.Create(MaybeRandom(startUrl)).NoCache();

            using var response = request.GetResponse();
            using var stream = response.GetResponseStream();
            using var reader = new StreamReader(stream);
            string desc = "";

            logInfo("[ModVersionCheck]   Parsing " + repository + " version_info.txt");
            for (; ; )
            {
                var line = reader.ReadLine();
                if (line == null)
                {
                    break;
                }
                if (line.StartsWith("#"))
                {
                    desc = line[2..];
                    continue;
                }
                var kvIndex = line.IndexOf("=");
                if (kvIndex < 0)
                {
                    continue;
                }
                string[] kv = [line[..kvIndex], line[(kvIndex + 1)..]];

                // logInfo("  -> " + kv[0] + " @ " + kv[1]);

                var subsections = kv[1].Split('|');
                if (subsections.Length == 1)
                {
                    subsections = [subsections[0], "", "", "", startUrl];
                }

                plugins[kv[0]] = (desc, subsections[0], subsections[1], subsections[2], startUrl);
            }

            logInfo("[ModVersionCheck]   Parsing " + repository + " version_info.txt DONE. " + plugins.Count + " entries found");

            return plugins;
        }

        internal static List<string> GetBranches(string repository, Action<object> logInfo)
        {
            List<string> result = [];
            logInfo("[ModVersionCheck]   GetBranches of " + repository);
            var api = "https://api.github.com/repos/" + repository + "/branches";
            var request = (HttpWebRequest)WebRequest.Create(MaybeRandom(api)).NoCache();
            request.UserAgent = "akarnokd-ThePlanetCrafterMods-ModVersionCheck";
            request.Accept = "application/json";

            using var response = request.GetResponse();
            using var stream = response.GetResponseStream();
            using var reader = new StreamReader(stream);

            logInfo("[ModVersionCheck]   Parsing Branches of " + repository);

            var json = JArray.Parse(reader.ReadToEnd());

            foreach (var item in json)
            {
                var jo = item as JObject;
                if (jo != null && jo.ContainsKey("name"))
                {
                    var nm = jo["name"];
                    result.Add(nm.ToString());
                    // LogInfo("  " + nm);
                }
            }
            logInfo("[ModVersionCheck]   Branches Done of " + repository + ": " + string.Join(", ", result));
            return result;
        }

        static string MaybeRandom(string url)
        {
            return url + "?v=" + DateTime.UtcNow.Ticks;
        }
    }

    static class HelpersExt
    {
        internal static WebRequest NoCache(this WebRequest request)
        {
            request.CachePolicy = new HttpRequestCachePolicy(HttpRequestCacheLevel.NoCacheNoStore);
            request.Headers[HttpRequestHeader.CacheControl] = "no-cache";
            return request;
        }
    }
}
