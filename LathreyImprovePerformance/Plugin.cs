using BepInEx;
using SpaceCraft;
using HarmonyLib;
using BepInEx.Configuration;
using UnityEngine.InputSystem;
using UnityEngine;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using static UnityEngine.ParticleSystem.PlaybackState;
using BepInEx.Logging;

// Reimplemented with permission
// https://github.com/TysonCodes/PlanetCrafterPlugins/blob/master/ImprovePerformance_Plugin
// >>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>
namespace LathreyImprovePerformance
{
    [BepInPlugin("akarnokd.theplanetcraftermods.lathreyimproveperformance", "(Lathrey) Improve Performance", PluginInfo.PLUGIN_VERSION)]
    public class Plugin : BaseUnityPlugin
    {
        static ConfigEntry<string> disableLightsConfig;

        static ConfigEntry<string> disableParticlesConfig;

        static readonly List<string> buildingsToDisableLightsOn = new()
        {
            "VegetableGrower1",
            "VegetableGrower2",
            "Heater1",
            "Heater2",
            "Heater3",
            "Heater4",
            "EnergyGenerator4",
            "EnergyGenerator5",
            "EnergyGenerator6",
            "CraftStation1",
            "CraftStation2"
        };

        static readonly List<string> buildingsToDisableParticlesOn = new()
        {
            "AlgaeSpreader1",
            "AlgaeSpreader2",
            "Heater1",
            "Heater2",
            "Heater3",
            "Heater4",
            "EnergyGenerator4",
            "EnergyGenerator5",
            "EnergyGenerator6",
            "CraftStation1",
            "CraftStation2",
            "Vegetube1",
            "VegeTube2",
            "VegetubeOutside1",
            "Drill0",
            "Drill1",
            "Drill2",
            "Drill3",
            "Beacon1",
            "GasExtractor",
            "Biodome1",
            "Wall_Door"
        };

        static ManualLogSource logger;

        void Awake()
        {
            disableLightsConfig = Config.Bind("General", "DisableLights",
                string.Join(",", buildingsToDisableLightsOn),
                "List of comma separated building group ids to disable lights of.");

            disableParticlesConfig = Config.Bind("General", "DisableParticles",
                string.Join(",", buildingsToDisableParticlesOn),
                "List of comma separated building group ids to disable particle effects of.");

            logger = Logger;

            if (Config.Bind("General", "Enabled", true, "Is this mod enabled?").Value)
            {
                Harmony.CreateAndPatchAll(typeof(Plugin));

                Logger.LogInfo($"Plugin is enabled.");
            }
            else
            {
                Logger.LogInfo($"Plugin is disabled.");
            }

        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(StaticDataHandler), "LoadStaticData")]
        static void StaticDataHandler_LoadStaticData(ref List<GroupData> ___groupsData)
        {
            var lightsSet = new HashSet<string>(disableLightsConfig.Value.Split(',').Select(v => v.Trim()));
            var particleSet = new HashSet<string>(disableParticlesConfig.Value.Split(',').Select(v => v.Trim()));

            foreach (var gr in ___groupsData)
            {
                if (gr.associatedGameObject != null)
                {
                    if (lightsSet.Contains(gr.id))
                    {
                        Light[] lights = gr.associatedGameObject.GetComponentsInChildren<Light>();
                        foreach (Light light in lights)
                        {
                            light.gameObject.SetActive(false);
                        }
                        logger.LogInfo("Disabling lights    on " + gr.id + " (" + lights.Length + " lights)");
                    }
                    if (particleSet.Contains(gr.id))
                    {
                        ParticleSystem[] particleSystems = gr.associatedGameObject.GetComponentsInChildren<ParticleSystem>();
                        foreach (ParticleSystem particleSystem in particleSystems)
                        {
                            particleSystem.gameObject.SetActive(false);
                        }
                        logger.LogInfo("Disabling particles on " + gr.id + " (" + particleSystems.Length + " particles)");
                    }
                }
            }
        }
    }
}
