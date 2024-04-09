// Copyright (c) 2022-2024, David Karnok & Contributors
// Licensed under the Apache License, Version 2.0

using BepInEx;
using SpaceCraft;
using HarmonyLib;
using TMPro;
using BepInEx.Configuration;

namespace UITelemetryFontSizer
{
    [BepInPlugin("akarnokd.theplanetcraftermods.uitelemetryfontsizer", "(UI) Telemetry Font Sizer", PluginInfo.PLUGIN_VERSION)]
    public class Plugin : BaseUnityPlugin
    {

        static ConfigEntry<int> leftTelemetryFontSize;

        static ConfigEntry<int> rightTelemetryFontSize;

        void Awake()
        {
            LibCommon.BepInExLoggerFix.ApplyFix();

            // Plugin startup logic
            Logger.LogInfo($"Plugin is loaded!");

            leftTelemetryFontSize = Config.Bind("General", "LeftFontSize", -1, "The font size of the left side text block (coordinates). -1 to use the default.");
            rightTelemetryFontSize = Config.Bind("General", "RightFontSize", -1, "The font size of the right side text block (version + framerate). -1 to use the default.");

            LibCommon.HarmonyIntegrityCheck.Check(typeof(Plugin));
            Harmony.CreateAndPatchAll(typeof(Plugin));
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(BaseHudHandler), nameof(BaseHudHandler.UpdateHud))]
        static void BaseHudHandler_UpdateHud(TextMeshProUGUI ___textPositionDecoration,
            TextMeshProUGUI ___textBottomRight)
        {
            int fs = leftTelemetryFontSize.Value;
            if (fs > 0)
            {
                ___textPositionDecoration.fontSize = fs;
                ___textPositionDecoration.textWrappingMode = TextWrappingModes.NoWrap;
                ___textPositionDecoration.overflowMode = TextOverflowModes.Overflow;
            }

            fs = rightTelemetryFontSize.Value;
            if (fs > 0)
            {
                ___textBottomRight.fontSize = fs;
                ___textBottomRight.textWrappingMode = TextWrappingModes.NoWrap;
                ___textBottomRight.overflowMode = TextOverflowModes.Overflow;
            }
        }
    }
}
