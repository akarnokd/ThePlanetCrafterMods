// Copyright (c) 2022-2025, David Karnok & Contributors
// Licensed under the Apache License, Version 2.0

using BepInEx;

namespace UITranslationHungarian
{
    [BepInPlugin("akarnokd.theplanetcraftermods.uitranslationhungarian", "(UI) Hungarian Translation", PluginInfo.PLUGIN_VERSION)]
    public class Plugin : BaseUnityPlugin
    {
        public void Awake()
        {
            LibCommon.UITranslator.AddLanguage("hungarian", "labels-hu.txt", this, Logger, Config, labels =>
            {
                labels["GROUP_NAME_TrashAluminiumScraps1"] = "Aluminium hulladék";
                labels["GROUP_NAME_Poster13"] = labels["GROUP_NAME_poster13"];
                labels["Ui_no"] = labels["UI_no"];
            });
        }
    }
}
