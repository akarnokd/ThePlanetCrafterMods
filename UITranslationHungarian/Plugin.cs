// Copyright (c) 2022-2024, David Karnok & Contributors
// Licensed under the Apache License, Version 2.0

using BepInEx;

namespace UITranslationHungarian
{
    [BepInPlugin("akarnokd.theplanetcraftermods.uitranslationhungarian", "(UI) Hungarian Translation", PluginInfo.PLUGIN_VERSION)]
    public class Plugin : BaseUnityPlugin
    {
        public void Awake()
        {
            LibCommon.UITranslator.AddLanguage("hungarian", "labels-hu.txt", this, Logger, Config);
        }
    }
}
