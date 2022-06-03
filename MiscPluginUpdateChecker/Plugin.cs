using BepInEx;
using SpaceCraft;
using HarmonyLib;
using UnityEngine;
using System.Globalization;
using BepInEx.Logging;
using BepInEx.Configuration;
using System.Runtime.CompilerServices;
using System.Collections.Generic;
using System;
using System.Net;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using BepInEx.Bootstrap;

[assembly: InternalsVisibleTo("XTestPlugins")]
namespace MiscPluginUpdateChecker
{
    [BepInPlugin("akarnokd.theplanetcraftermods.miscpluginupdatechecker", "(Misc) Plugin Update Checker", "1.0.0.0")]
    public class Plugin : BaseUnityPlugin
    {
        static ManualLogSource logger;

        static ConfigEntry<bool> isEnabled;
        static ConfigEntry<string> versionInfoRepository;
        static ConfigEntry<bool> bypassCache;

        private void Awake()
        {
            // Plugin startup logic
            Logger.LogInfo($"Plugin is loaded!");

            logger = Logger;

            isEnabled = Config.Bind("General", "Enabled", true, "Is the mod enabled?");
            versionInfoRepository = Config.Bind("General", "VersionInfoRepository", Helpers.defaultVersionInfoRepository, "The URL from where to download an XML describing various known plugins and their latest versions.");
            bypassCache = Config.Bind("General", "BypassCache", false, "If true, this mod will try to bypass caching on the targeted URLs by appending an arbitrary query parameter");

            Harmony.CreateAndPatchAll(typeof(Plugin));
        }

        static CancellationTokenSource cancelDownload;
        static volatile Dictionary<string, PluginEntry> pluginInfos;

        [HarmonyPostfix]
        [HarmonyPatch(typeof(Intro), "Start")]
        static void Intro_Start()
        {
            if (isEnabled.Value)
            {
                pluginInfos = null;

                var startUrl = versionInfoRepository.Value;
                var bypass = bypassCache.Value;

                var localPlugins = GetLocalPlugins();

                cancelDownload = new CancellationTokenSource();
                Task.Factory.StartNew(() => GetPluginInfos(startUrl, bypass, localPlugins),
                    cancelDownload.Token,
                    TaskCreationOptions.LongRunning,
                    TaskScheduler.Default);
            }
        }

        static void Update()
        {
            if (!cancelDownload.IsCancellationRequested)
            {
                var pis = pluginInfos;
                if (pis != null)
                {

                }
            }
        }

        static void Destroy()
        {
            cancelDownload?.Cancel();
            pluginInfos = null;
        }

        static Dictionary<string, PluginEntry> GetLocalPlugins()
        {
            var result = new Dictionary<string, PluginEntry>();
            foreach (var pi in Chainloader.PluginInfos)
            {
                var pe = new PluginEntry();
                pe.guid = pi.Key;
                pe.description = pi.Value.Metadata.Name;
                pe.explicitVersion = pi.Value.Metadata.Version.ToString();

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
                    o => logger.LogInfo(o),
                    o => logger.LogWarning(o),
                    o => logger.LogError(o),
                    bypassCache
                );
                logger.LogInfo("Comparing local and remote plugins");
            }
            catch (Exception ex)
            {
                if (!cancelDownload.IsCancellationRequested)
                {
                    logger.LogError(ex);
                }
            }
        }
    }
}
