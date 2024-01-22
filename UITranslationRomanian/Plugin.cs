using BepInEx;
using HarmonyLib;
using SpaceCraft;
using System.Collections.Generic;
using BepInEx.Logging;

namespace UITranslationRomanian
{
    [BepInPlugin("akarnokd.theplanetcraftermods.uitranslationromanian", "(UI) Romanian Translation", PluginInfo.PLUGIN_VERSION)]
    public class Plugin : BaseUnityPlugin
    {
        void Awake()
        {
            var h = LibCommon.UITranslator.AddLanguage("romanian", "labels-ro.txt", this, Logger, Config);
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
