// Copyright (c) 2022-2024, David Karnok & Contributors
// Licensed under the Apache License, Version 2.0

using BepInEx;

namespace MiscDebug
{
    [BepInPlugin("akarnokd.theplanetcraftermods.miscdebug", "(Misc) Debug", PluginInfo.PLUGIN_VERSION)]
    public class Plugin : BaseUnityPlugin
    {

        public void Awake()
        {
            LibCommon.BepInExLoggerFix.ApplyFix();
        }
    }
}
