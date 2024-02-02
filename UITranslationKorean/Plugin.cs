// Copyright (c) 2022-2024, David Karnok & Contributors
// Licensed under the Apache License, Version 2.0

using BepInEx;

namespace UITranslationKorean
{
    [BepInPlugin("akarnokd.theplanetcraftermods.uitranslationkorean", "(UI) Korean Translation", PluginInfo.PLUGIN_VERSION)]
    public class Plugin : BaseUnityPlugin
    {
        public void Awake()
        {
            LibCommon.UITranslator.AddLanguage("korean", "labels-ko.txt", this, Logger, Config);
        }
    }
}
