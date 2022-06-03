using BepInEx;
using SpaceCraft;
using HarmonyLib;
using UnityEngine;
using System.Globalization;
using BepInEx.Logging;
using BepInEx.Configuration;

namespace MiscPluginUpdateChecker
{
    [BepInPlugin("akarnokd.theplanetcraftermods.miscpluginupdatechecker", "(Misc) Plugin Update Checker", "1.0.0.0")]
    public class Plugin : BaseUnityPlugin
    {

        const string defaultVersionInfoRepository = "https://raw.githubusercontent.com/akarnokd/ThePlanetCrafterMods/main/version_info_repository.xml";

        static ManualLogSource logger;

        static ConfigEntry<bool> isEnabled;
        static ConfigEntry<string> versionInfoRepository;

        private void Awake()
        {
            // Plugin startup logic
            Logger.LogInfo($"Plugin is loaded!");

            logger = Logger;

            isEnabled = Config.Bind("General", "Enabled", true, "Is the mod enabled?");
            versionInfoRepository = Config.Bind("General", "VersionInfoRepository", defaultVersionInfoRepository, "The URL from where to download an XML describing various known plugins and their latest versions.");

            Harmony.CreateAndPatchAll(typeof(Plugin));
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(Intro), "Start")]
        static void Intro_Start()
        {

        }
    }
}
