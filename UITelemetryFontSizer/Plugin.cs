using BepInEx;
using SpaceCraft;
using HarmonyLib;
using TMPro;
using UnityEngine;
using System;
using System.Collections.Generic;
using BepInEx.Bootstrap;
using System.Reflection;
using BepInEx.Configuration;

namespace UITelemetryFontSizer
{
    [BepInPlugin("akarnokd.theplanetcraftermods.uitelemetryfontsizer", "(UI) Telemetry Font Sizer", PluginInfo.PLUGIN_VERSION)]
    public class Plugin : BaseUnityPlugin
    {

        static ConfigEntry<int> leftTelemetryFontSize;

        static ConfigEntry<int> rightTelemetryFontSize;

        private void Awake()
        {
            // Plugin startup logic
            Logger.LogInfo($"Plugin is loaded!");

            leftTelemetryFontSize = Config.Bind("General", "LeftFontSize", -1, "The font size of the left side text block (coordinates). -1 to use the default.");
            rightTelemetryFontSize = Config.Bind("General", "RightFontSize", -1, "The font size of the right side text block (version + framerate). -1 to use the default.");

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
                ___textPositionDecoration.enableWordWrapping = false;
                ___textPositionDecoration.overflowMode = TextOverflowModes.Overflow;
            }

            fs = rightTelemetryFontSize.Value;
            if (fs > 0)
            {
                ___textBottomRight.fontSize = fs;
                ___textBottomRight.enableWordWrapping = false;
                ___textBottomRight.overflowMode = TextOverflowModes.Overflow;
            }
        }
    }
}
