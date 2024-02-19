// Copyright (c) 2022-2024, David Karnok & Contributors
// Licensed under the Apache License, Version 2.0

using BepInEx;

namespace UITranslationEstonian
{
    [BepInPlugin("akarnokd.theplanetcraftermods.uitranslationestonian", "(UI) Estonian Translation", PluginInfo.PLUGIN_VERSION)]
    public class Plugin : BaseUnityPlugin
    {
        public void Awake()
        {
            LibCommon.UITranslator.AddLanguage("estonian", "labels-et.txt", this, Logger, Config);
        }
    }
}
