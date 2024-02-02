// Copyright (c) 2022-2024, David Karnok & Contributors
// Licensed under the Apache License, Version 2.0

using BepInEx;

namespace ZipRest
{
    [BepInPlugin("akarnokd.theplanetcraftermods.ziprest", "Zip the other mods", "1.0.0.0")]
    public class Plugin : BaseUnityPlugin
    {
        public void Awake()
        {
            // Plugin startup logic
            Logger.LogInfo($"Plugin is loaded!");

            // Harmony.CreateAndPatchAll(typeof(Plugin));
        }
    }
}
