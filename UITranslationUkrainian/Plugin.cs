// Copyright (c) 2022-2025, David Karnok & Contributors
// Licensed under the Apache License, Version 2.0

using BepInEx;

namespace UITranslationUkrainian
{
    [BepInPlugin("akarnokd.theplanetcraftermods.uitranslationukrainian", "(UI) Ukrainian Translation", PluginInfo.PLUGIN_VERSION)]
    public class Plugin : BaseUnityPlugin
    {
        public void Awake()
        {
            LibCommon.UITranslator.AddLanguage("ukrainian", "labels-ua.txt", this, Logger, Config);
        }
    }
}
