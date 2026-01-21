// Copyright (c) 2022-2026, David Karnok & Contributors
// Licensed under the Apache License, Version 2.0

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
using System.IO;
using System.Net.Cache;
using System.Net;
using static System.Net.WebRequestMethods;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

[assembly: InternalsVisibleTo("XTestPlugins")]
namespace MiscPluginUpdateChecker
{
    [BepInPlugin("akarnokd.theplanetcraftermods.miscpluginupdatechecker", "(Misc) Plugin Update Checker", PluginInfo.PLUGIN_VERSION)]
    [BepInDependency("akarnokd.theplanetcraftermods.featmultiplayer", BepInDependency.DependencyFlags.SoftDependency)]
    public class Plugin : BaseUnityPlugin
    {
        static Plugin me;

        static ConfigEntry<bool> debugMode;
        static ConfigEntry<bool> testMode;

        public void Awake()
        {
            me = this;

            LibCommon.BepInExLoggerFix.ApplyFix();

            if (LibCommon.ModVersionCheck.Check(this, Logger.LogInfo))
            {
                LibCommon.ModVersionCheck.NotifyUser(this, Logger.LogInfo);
            }

            debugMode = Config.Bind("General", "DebugMode", false, "Enable detailed logging. Very chatty!");
            testMode = Config.Bind("General", "TestMode", false, "Enable to trigger the notifications even if there are no updates.");
            // Plugin startup logic
            Logger.LogInfo($"Plugin is loaded!");

            Harmony.CreateAndPatchAll(typeof(Plugin));
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(Intro), "Awake")]
        static void Intro_Awake()
        {

            Action<object> log = debugMode.Value ? me.Logger.LogInfo : _ => { };
            log("Begin checking other mods: " + Chainloader.PluginInfos.Count);

            foreach (var p in Chainloader.PluginInfos.Values)
            {
                log("  Mod: " + p.Metadata.GUID + " - " + p.Metadata.Name);
                if (LibCommon.ModVersionCheck.Check(p.Instance, log) || testMode.Value)
                {
                    LibCommon.ModVersionCheck.NotifyUser(p.Instance, log);
                }
            }

            log("DONE  checking other mods");
        }

    }
}
