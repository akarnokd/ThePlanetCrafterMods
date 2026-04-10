// Copyright (c) 2022-2026, David Karnok & Contributors
// Licensed under the Apache License, Version 2.0

using BepInEx;
using BepInEx.Bootstrap;
using Galaxy.Api;
using HarmonyLib;
using SpaceCraft;
using System;
using System.Collections;
using System.Globalization;
using System.IO;
using System.Net.Http.Headers;
using System.Reflection;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.UI;

namespace MiscModEnabler
{
    [BepInPlugin("akarnokd.theplanetcraftermods.miscmodenabler", "(Misc) Mod Enabler", PluginInfo.PLUGIN_VERSION)]
    public class Plugin : BaseUnityPlugin
    {
        public void Awake()
        {
            LibCommon.BepInExLoggerFix.ApplyFix();

            if (LibCommon.ModVersionCheck.Check(this, Logger.LogInfo, out var hashError, out var repoURL))
            {
                LibCommon.ModVersionCheck.NotifyUser(this, hashError, repoURL, Logger.LogInfo);
            }
        }

    }
}
