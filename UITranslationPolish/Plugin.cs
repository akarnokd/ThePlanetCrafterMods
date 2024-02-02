// Copyright (c) 2022-2024, David Karnok & Contributors
// Licensed under the Apache License, Version 2.0

using BepInEx;

namespace UITranslationPolish
{
    [BepInPlugin("akarnokd.theplanetcraftermods.uitranslationpolish", "(UI) Polish Translation", PluginInfo.PLUGIN_VERSION)]
    public class Plugin : BaseUnityPlugin
    {
        public void Awake()
        {
            LibCommon.UITranslator.AddLanguage("polish", "labels-pl.txt", this, Logger, Config);
        }
    }
}
