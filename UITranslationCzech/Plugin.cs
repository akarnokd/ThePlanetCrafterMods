// Copyright (c) 2022-2025, David Karnok & Contributors
// Licensed under the Apache License, Version 2.0

using BepInEx;

namespace UITranslationCzech
{
    [BepInPlugin("akarnokd.theplanetcraftermods.uitranslationczech", "(UI) Czech Translation", PluginInfo.PLUGIN_VERSION)]
    public class Plugin : BaseUnityPlugin
    {
        public void Awake()
        {
            LibCommon.UITranslator.AddLanguage("czech", "labels-cz.txt", this, Logger, Config);
        }
    }
}
