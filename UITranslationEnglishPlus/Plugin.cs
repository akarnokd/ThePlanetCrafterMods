// Copyright (c) 2022-2026, David Karnok & Contributors
// Licensed under the Apache License, Version 2.0

using BepInEx;
using HarmonyLib;

namespace UITranslationEnglishPlus
{
    [BepInPlugin("akarnokd.theplanetcraftermods.uitranslationenglishplus", "(UI) English Plus Translation", PluginInfo.PLUGIN_VERSION)]
    public class Plugin : BaseUnityPlugin
    {
        public void Awake()
        {
            LibCommon.UITranslator.AddLanguage("english", "labels-engplus.txt", this, Logger, Config, labels =>
            {
            });
            LibCommon.HarmonyIntegrityCheck.Check(typeof(Plugin));
            LibCommon.GameVersionCheck.Patch(new Harmony(PluginInfo.PLUGIN_GUID + "_Ver"), PluginInfo.PLUGIN_NAME + " - v" + PluginInfo.PLUGIN_VERSION);
        }
    }
}
