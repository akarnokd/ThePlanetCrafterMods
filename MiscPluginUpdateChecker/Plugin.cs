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

        public void Awake()
        {
            LibCommon.BepInExLoggerFix.ApplyFix();


            if (LibCommon.ModVersionCheck.Check(this, Logger.LogInfo))
            {
                LibCommon.ModVersionCheck.NotifyUser(this, Logger.LogInfo);
            }
            // Plugin startup logic
            Logger.LogInfo($"Plugin is loaded!");
        }

    }
}
