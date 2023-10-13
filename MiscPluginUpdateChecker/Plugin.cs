using BepInEx;
using SpaceCraft;
using HarmonyLib;
using UnityEngine;
using BepInEx.Logging;
using BepInEx.Configuration;
using System.Runtime.CompilerServices;
using System.Collections.Generic;
using System;
using System.Threading;
using System.Threading.Tasks;
using BepInEx.Bootstrap;
using UnityEngine.UI;
using UnityEngine.InputSystem;

[assembly: InternalsVisibleTo("XTestPlugins")]
namespace MiscPluginUpdateChecker
{
    [BepInPlugin("akarnokd.theplanetcraftermods.miscpluginupdatechecker", "(Misc) Plugin Update Checker", PluginInfo.PLUGIN_VERSION)]
    [BepInDependency("akarnokd.theplanetcraftermods.featmultiplayer", BepInDependency.DependencyFlags.SoftDependency)]
    public class Plugin : BaseUnityPlugin
    {
        static ManualLogSource _logger;

        static ConfigEntry<bool> isEnabled;
        static ConfigEntry<string> versionInfoRepository;
        static ConfigEntry<bool> bypassCache;
        static ConfigEntry<int> fontSize;
        static ConfigEntry<bool> debugMode;

        private void Awake()
        {
            // Plugin startup logic
            Logger.LogInfo($"Plugin is loaded!");

            _logger = Logger;

            isEnabled = Config.Bind("General", "Enabled", true, "Is the mod enabled?");
            versionInfoRepository = Config.Bind("General", "VersionInfoRepository", Helpers.defaultVersionInfoRepository, "The URL from where to download an XML describing various known plugins and their latest versions.");
            bypassCache = Config.Bind("General", "BypassCache", false, "If true, this mod will try to bypass caching on the targeted URLs by appending an arbitrary query parameter");
            fontSize = Config.Bind("General", "FontSize", 16, "The font size");
            debugMode = Config.Bind("General", "DebugMode", false, "Enable detailed logging of the mod (chatty!)");

            Harmony.CreateAndPatchAll(typeof(Plugin));
        }

        static CancellationTokenSource cancelDownload;
        static volatile List<PluginVersionDiff> pluginInfos;

        static void LogInfo(object message)
        {
            if (debugMode.Value)
            {
                _logger.LogInfo(message);
            }
        }
        static void LogWarning(object message)
        {
            if (debugMode.Value)
            {
                _logger.LogWarning(message);
            }
        }
        static void LogError(object message)
        {
            if (debugMode.Value)
            {
                _logger.LogError(message);
            }
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(Intro), "Start")]
        static void Intro_Start(Intro __instance)
        {
            if (isEnabled.Value)
            {
                pluginInfos = null;

                var startUrl = versionInfoRepository.Value;
                var bypass = bypassCache.Value;

                var localPlugins = GetLocalPlugins();

                Destroy(aparent);
                aparent = new GameObject("PluginVersionDiff");
                aparent.SetActive(false);

                cancelDownload = new CancellationTokenSource();
                Task.Factory.StartNew(() => GetPluginInfos(startUrl, bypass, localPlugins),
                    cancelDownload.Token,
                    TaskCreationOptions.LongRunning,
                    TaskScheduler.Default);
            }
        }

        void Update()
        {
            var pis = pluginInfos;
            if (!cancelDownload.IsCancellationRequested)
            {
                if (pis != null && aparent != null && !aparent.activeSelf)
                {
                    DisplayPluginDiffs(pis);
                }
            }
            if (aparent != null && aparent.activeSelf && Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                Destroy(aparent);
                aparent = null;
                pluginInfos = null;
                foreach (GameObject go in toHide)
                {
                    go.SetActive(true);
                }
                toHide.Clear();
            }
            if (aparent != null && aparent.activeSelf && pluginInfos != null)
            {
                var mouse = Mouse.current.position.ReadValue();

                foreach (PluginVersionDiff diff in scrollEntries)
                {
                    if (IsWithin(diff.gameObject, mouse))
                    {
                        diff.gameObject.GetComponent<Text>().color = new Color(0.3f, 0.3f, 1f);
                        if (Mouse.current.leftButton.wasPressedThisFrame)
                        {
                            Application.OpenURL(diff.remote.link);
                        }
                    }
                    else
                    {
                        diff.gameObject.GetComponent<Text>().color = new Color(1, 1, 1);
                    }
                }
                var sy = Mouse.current.scroll.ReadValue();
                if (sy.y > 0 && scrollIndex > 0)
                {
                    scrollIndex = Math.Max(0, scrollIndex - 1);
                    RenderDiffList(pluginInfos);
                }
                if (sy.y < 0 && scrollDown != null)
                {
                    scrollIndex++;
                    RenderDiffList(pluginInfos);
                }
            }
        }

        static void Destroy()
        {
            cancelDownload?.Cancel();
            pluginInfos = null;
            scrollEntries.Clear();
            scrollIndex = 0;
        }

        static GameObject aparent;
        static GameObject scrollUp;
        static GameObject scrollDown;
        static int scrollIndex;
        static readonly List<PluginVersionDiff> scrollEntries = new();
        static readonly List<GameObject> toHide = new();

        static void DisplayPluginDiffs(List<PluginVersionDiff> diffs)
        {
            aparent.SetActive(true);
            Canvas canvas = aparent.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            scrollIndex = 0;
            RenderDiffList(diffs);

            if (diffs.Count != 0)
            {
                toHide.Clear();
                foreach (GameObject go in FindObjectsByType<GameObject>(FindObjectsSortMode.None))
                {
                    if (go != aparent && go.GetComponentInChildren<Canvas>() && go.activeSelf)
                    {
                        toHide.Add(go);
                        go.SetActive(false);
                    }
                }
            }
        }

        static GameObject CreateText(int x, int y, int w, string text, Color color, int fontSize)
        {
            var go = new GameObject("PluginVersionDiffText");
            go.transform.SetParent(aparent.transform);

            var txt = go.AddComponent<Text>();
            txt.text = text;
            txt.color = color;
            txt.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            txt.fontSize = fontSize;
            txt.resizeTextForBestFit = false;
            txt.verticalOverflow = VerticalWrapMode.Truncate;
            txt.horizontalOverflow = HorizontalWrapMode.Overflow;
            txt.alignment = TextAnchor.MiddleCenter;

            var rect = txt.GetComponent<RectTransform>();
            rect.localPosition = new Vector2(x, y);
            rect.sizeDelta = new Vector2(w, fontSize + 5);

            return go;
        }

        static void RenderDiffList(List<PluginVersionDiff> list)
        {
            foreach (Transform child in aparent.transform)
            {
                Destroy(child.gameObject);
            }

            int fs = fontSize.Value;


            int w = Screen.width * 3 / 4;
            int maxh = Screen.height * 3 / 4;

            int contentHeight = (list.Count * 3 + 2) * (fs + 5) + (fs + 10) + fs;

            int h = Screen.height * 3 / 4;
            if (contentHeight < h)
            {
                h = contentHeight;
            }
            int x = 0;
            int y = h / 2;
            int miny = -h / 2 + 4 * (fs + 5);

            scrollEntries.Clear();

            if (list.Count == 0)
            {
                Destroy(scrollUp);
                Destroy(scrollDown);
                scrollIndex = 0;

                CreateText(0, -Screen.height / 2 + fs * 2, w, "Plugins are up-to-date", new Color(1, 1, 1), fs);
                return;
            }

            var background = new GameObject("PluginVersionDiffBackground");
            background.transform.parent = aparent.transform;

            var img = background.AddComponent<Image>();
            img.color = new Color(0, 0, 0, 0.95f);
            var rect = img.GetComponent<RectTransform>();
            rect.localPosition = new Vector2(0, 0);
            rect.sizeDelta = new Vector2(w, h);

            y -= fs;

            CreateText(x, y, w, "Some plugins are out of date (ESC to close)", new Color(1, 0.75f, 0), fs + 5);
            
            y -= fs + 10;

            int index = scrollIndex;
            scrollUp = CreateText(x, y, w, "/\\", new Color(1, 0.75f, 0), fs);
            scrollUp.SetActive(index > 0);

            y -= fs + 5;

            int i = index;
            for (; i < list.Count; i++)
            {
                var de = list[i];

                var goGuid = CreateText(x, y, w, "--> " + de.local.guid + " <--", new Color(1, 1, 1), fs);
                scrollEntries.Add(new()
                {
                    local = de.local,
                    remote = de.remote,
                    gameObject = goGuid
                });

                y -= fs + 5;
                CreateText(x, y, w, de.local.description, new Color(0.8f, 0.8f, 0.8f), fs);
                y -= fs + 5;
                CreateText(x, y, w, de.local.explicitVersion + " -> " + de.remote.version, new Color(0.5f, 1, 0.5f), fs);
                y -= fs + 5;

                if (y < miny)
                {
                    break;
                }
            }

            if (i < list.Count - 1)
            {
                scrollDown = CreateText(x, y, w, "\\/", new Color(1, 0.75f, 0), fs);
            }
            else
            {
                scrollDown = null;
            }
        }

        static bool IsWithin(GameObject go, Vector2 mouse)
        {
            RectTransform rect = go.GetComponent<Text>().GetComponent<RectTransform>();

            var lp = rect.localPosition;
            lp.x += Screen.width / 2 - rect.sizeDelta.x / 2;
            lp.y += Screen.height / 2 - rect.sizeDelta.y / 2;

            return mouse.x >= lp.x && mouse.y >= lp.y
                && mouse.x <= lp.x + rect.sizeDelta.x && mouse.y <= lp.y + rect.sizeDelta.y;
        }

        class PluginVersionDiff
        {
            internal PluginEntry local;
            internal PluginEntry remote;
            internal GameObject gameObject;
        }

        static Dictionary<string, PluginEntry> GetLocalPlugins()
        {
            var result = new Dictionary<string, PluginEntry>();
            foreach (var pi in Chainloader.PluginInfos)
            {
                var pe = new PluginEntry();
                pe.guid = pi.Key;
                pe.description = pi.Value.Metadata.Name;
                pe.explicitVersion = pi.Value.Metadata.Version;

                result[pi.Key] = pe;
            }
            return result;
        }

        static void GetPluginInfos(string startUrl, bool bypassCache, Dictionary<string, PluginEntry> localPlugins)
        {
            try
            {
                var remotePluginInfos = Helpers.DownloadPluginInfos(
                    startUrl,
                    o => LogInfo(o),
                    o => LogWarning(o),
                    o => LogError(o),
                    bypassCache
                );
                LogInfo("Comparing local and remote plugins");

                List<PluginVersionDiff> diffs = new();

                foreach (var local in localPlugins.Values)
                {
                    if (remotePluginInfos.TryGetValue(local.guid, out var remote))
                    {
                        if (remote.CompareToVersion(local.explicitVersion) > 0)
                        {
                            var pd = new PluginVersionDiff()
                            {
                                local = local,
                                remote = remote
                            };
                            diffs.Add(pd);
                        }
                    }
                }
                LogInfo("Plugin differences found: " + diffs.Count);
                pluginInfos = diffs;
            }
            catch (Exception ex)
            {
                if (!cancelDownload.IsCancellationRequested)
                {
                    LogError(ex);
                }
            }
        }
    }
}
