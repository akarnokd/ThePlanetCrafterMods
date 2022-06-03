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

        [HarmonyPostfix]
        [HarmonyPatch(typeof(Intro), "Start")]
        static void Intro_Start()
        {

        }

    }
}
