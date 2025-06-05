// Copyright (c) 2022-2025, David Karnok & Contributors
// Licensed under the Apache License, Version 2.0

using BepInEx;
using HarmonyLib;

namespace LibCommon
{
    [BepInPlugin("akarnokd.theplanetcraftermods.libcommon", "(Lib) Common tools for other mods", "1.0.0.0")]
    public class Plugin : BaseUnityPlugin
    {

        public void Awake()
        {
            // Plugin startup logic
            Logger.LogInfo($"Plugin is loaded!");

            Harmony.CreateAndPatchAll(typeof(Plugin));
        }

        public void OnDestroy()
        {
            Logger.LogInfo($"Plugin destroyed!");
        }

    }
}
