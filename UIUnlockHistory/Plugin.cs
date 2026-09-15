// Copyright (c) 2022-2026, David Karnok & Contributors
// Licensed under the Apache License, Version 2.0

using BepInEx;
using SpaceCraft;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using BepInEx.Configuration;

namespace UIUnlockHistory
{
    [BepInPlugin("akarnokd.theplanetcraftermods.uiunlockhistory", "(UI) Unlock History", PluginInfo.PLUGIN_VERSION)]
    public class Plugin : BaseUnityPlugin
    {
        private void Awake()
        {
            LibCommon.BepInExLoggerFix.ApplyFix();
            LibCommon.HarmonyIntegrityCheck.Check(typeof(Plugin));
            LibCommon.GameVersionCheck.Patch(new Harmony(PluginInfo.PLUGIN_GUID + "_Ver"), PluginInfo.PLUGIN_NAME + " - v" + PluginInfo.PLUGIN_VERSION);
            if (LibCommon.ModVersionCheck.Check(this, Logger.LogInfo, out var hashError, out var repoURL))
            {
                LibCommon.ModVersionCheck.NotifyUser(this, hashError, repoURL, Logger.LogInfo);
            }

            // Plugin startup logic
            Logger.LogInfo($"Plugin is loaded!");


            Harmony.CreateAndPatchAll(typeof(Plugin));
        }

        void Update()
        {
        }


        static void OnModConfigChanged(ConfigEntryBase _)
        {
        }
    }
}
