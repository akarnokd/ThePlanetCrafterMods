// Copyright (c) 2022-2025, David Karnok & Contributors
// Licensed under the Apache License, Version 2.0

using BepInEx;
using HarmonyLib;
using SpaceCraft;

namespace UITranslationItalian
{
    [BepInPlugin("akarnokd.theplanetcraftermods.uitranslationitalian", "(UI) Italian Translation", PluginInfo.PLUGIN_VERSION)]
    public class Plugin : BaseUnityPlugin
    {
        void Awake()
        {
            var h = LibCommon.UITranslator.AddLanguage("italian", "labels-it.txt", this, Logger, Config);

            LibCommon.HarmonyIntegrityCheck.Check(typeof(Plugin));
            h.PatchAll(typeof(Plugin));
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(UiGroupLine), nameof(UiGroupLine.SetValues))]
        static void UiGroupLine_SetValues(ref string replaceInLabel)
        {
            if (replaceInLabel == "t1")
            {
                replaceInLabel = "T1";
            }
        }
    }
}
