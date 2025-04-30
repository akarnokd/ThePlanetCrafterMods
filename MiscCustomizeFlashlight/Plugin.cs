﻿// Copyright (c) 2022-2024, David Karnok & Contributors
// Licensed under the Apache License, Version 2.0

using BepInEx;
using SpaceCraft;
using HarmonyLib;
using BepInEx.Configuration;
using UnityEngine;

/// -------------------------------------------------------------------------------------------------------
/// Remake of https://github.com/aedenthorn/PlanetCrafterMods/blob/master/CustomFlashlight/BepInExPlugin.cs
/// -------------------------------------------------------------------------------------------------------

namespace MiscCustomizeFlashlight
{
    [BepInPlugin(modMiscCustomizeFlashlight, "(Misc) Customize Flashlight", PluginInfo.PLUGIN_VERSION)]
    public class Plugin : BaseUnityPlugin
    {
        const string modMiscCustomizeFlashlight = "akarnokd.theplanetcraftermods.misccustomizeflashlight";

        static ConfigEntry<bool> modEnabled;
        static ConfigEntry<string> color;
        static ConfigEntry<bool> useColorTemp;
        static ConfigEntry<int> colorTemp;
        static ConfigEntry<float> spotlightAngle;
        static ConfigEntry<float> spotlightInnerAngle;
        static ConfigEntry<float> intensity;
        static ConfigEntry<float> range;

        static AccessTools.FieldRef<MultiToolLight, float> fMultiToolLightBaseT1;
        static AccessTools.FieldRef<MultiToolLight, float> fMultiToolLightBaseT2;
        static AccessTools.FieldRef<MultiToolLight, float> fMultiToolLightBaseT3;

        public void Awake()
        {
            LibCommon.BepInExLoggerFix.ApplyFix();

            // Plugin startup logic
            Logger.LogInfo($"Plugin is loaded!");

            modEnabled = Config.Bind("General", "Enabled", true, "Enable this mod");
            useColorTemp = Config.Bind("General", "UseColorTemp", false, "Use color temperature.");
            color = Config.Bind("General", "Color", "FFFFF8E6", "Flashlight color in ARGB hex format, no hashmark. Example: FFFFCC00");
            colorTemp = Config.Bind("General", "ColorTemp", 6570, "Color temperature.");
            spotlightAngle = Config.Bind("General", "FlashlightAngle", 55.8698f, "Flashlight angle.");
            spotlightInnerAngle = Config.Bind("General", "FlashlightInnerAngle", 36.6912f, "Flashlight inner angle.");
            intensity = Config.Bind("General", "FlashlightIntensity", 40f, "Flashlight intensity.");
            range = Config.Bind("General", "FlashlightRange", 40f, "Flashlight range.");

            fMultiToolLightBaseT1 = AccessTools.FieldRefAccess<float>(typeof(MultiToolLight), "_baseT1");
            fMultiToolLightBaseT2 = AccessTools.FieldRefAccess<float>(typeof(MultiToolLight), "_baseT2");
            fMultiToolLightBaseT3 = AccessTools.FieldRefAccess<float>(typeof(MultiToolLight), "_baseT3");

            LibCommon.HarmonyIntegrityCheck.Check(typeof(Plugin));
            Harmony.CreateAndPatchAll(typeof(Plugin));
        }

        static void ApplyLightConfig(Light light, MultiToolLight multiToolLight, int index)
        {
            if (!modEnabled.Value || light == null)
            {
                return;
            }
            light.innerSpotAngle = spotlightInnerAngle.Value;
            light.spotAngle = spotlightAngle.Value;
            var colorStr = color.Value;
            if (colorStr.Length == 8)
            {
                try
                {
                    var a = int.Parse(colorStr[0..2], System.Globalization.NumberStyles.HexNumber) / 255f;
                    var r = int.Parse(colorStr[2..4], System.Globalization.NumberStyles.HexNumber) / 255f;
                    var g = int.Parse(colorStr[4..6], System.Globalization.NumberStyles.HexNumber) / 255f;
                    var b = int.Parse(colorStr[6..8], System.Globalization.NumberStyles.HexNumber) / 255f;
                    light.color = new Color(r, g, b, a);
                }
                catch
                {
                    // we ignore this
                }
            }
            light.useColorTemperature = useColorTemp.Value;
            light.colorTemperature = colorTemp.Value;
            light.intensity = intensity.Value;
            light.range = range.Value;

            switch (index)
            {
                case 2:
                    {
                        fMultiToolLightBaseT2(multiToolLight) = intensity.Value; 
                        break;
                    }
                case 3:
                    {
                        fMultiToolLightBaseT3(multiToolLight) = intensity.Value;
                        break;
                    }
                default:
                    {
                        fMultiToolLightBaseT1(multiToolLight) = intensity.Value;
                        break;
                    }
            }
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(PlayerMultitool), "Start")]
        static void PlayerMultitool_Start_Pre(
            MultiToolScreen ___multiToolScreen, ref bool __state)
        {
            __state = ___multiToolScreen == null;
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(PlayerMultitool), "Start")]
        static void PlayerMultitool_Start_Post(MultiToolLight ___multiToolLight, ref bool __state)
        {
            if (__state)
            {
                ApplyLightConfig(___multiToolLight.toolLightT1.GetComponent<Light>(), ___multiToolLight, 1);
                ApplyLightConfig(___multiToolLight.toolLightT2.GetComponent<Light>(), ___multiToolLight, 2);
                ApplyLightConfig(___multiToolLight.toolLightT3.GetComponent<Light>(), ___multiToolLight, 3);
            }
        }

        static void FindLights()
        {
            var pm = Managers.GetManager<PlayersManager>();
            if (pm == null)
            {
                return;
            }
            var ac = pm.GetActivePlayerController();
            if (ac == null)
            {
                return;
            }

            var pmt = ac.GetMultitool();
            if (pmt == null)
            {
                return;
            }

            var pmtl = pmt.GetMultiToolLight();
            if (pmtl == null)
            {
                return;
            }
            ApplyLightConfig(pmtl.toolLightT1.GetComponent<Light>(), pmtl, 1);
            ApplyLightConfig(pmtl.toolLightT2.GetComponent<Light>(), pmtl, 2);
            ApplyLightConfig(pmtl.toolLightT3.GetComponent<Light>(), pmtl, 3);
        }

        public static void OnModConfigChanged(ConfigEntryBase _)
        {
            FindLights();
        }
    }
}
