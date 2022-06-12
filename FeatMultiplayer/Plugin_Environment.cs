using BepInEx;
using HarmonyLib;
using MijuTools;
using SpaceCraft;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace FeatMultiplayer
{
    public partial class Plugin : BaseUnityPlugin
    {
        static FieldInfo environmentDayNightCycleValue;

        static MessageTime hostTime;

        /// <summary>
        /// The vanilla game calls EnvironmentDayNightCycle::Start to start
        /// a coroutine that updates the environmental parameters as time progresses.
        /// 
        /// We start our own coroutine and send the current GetDayNightLerpValue from the host
        /// to the client or update the client's value from the host's message.
        /// </summary>
        [HarmonyPostfix]
        [HarmonyPatch(typeof(EnvironmentDayNightCycle), "Start")]
        static void EnvironmentDayNightCycle_Start(EnvironmentDayNightCycle __instance)
        {
            environmentDayNightCycleValue = AccessTools.Field(typeof(EnvironmentDayNightCycle), "dayNightLerpValue");
            if (updateMode == MultiplayerMode.CoopClient)
            {
                // don't let the client interpolation run at all
                __instance.StopAllCoroutines();
                LogInfo("EnvironmentDayNightCycle_Start: Stopping Day-night cycle on the client.");
            }
            __instance.StartCoroutine(DayNightCycle(__instance, 0.5f));
        }

        static IEnumerator DayNightCycle(EnvironmentDayNightCycle __instance, float delay)
        {
            for (; ; )
            {
                if (updateMode == MultiplayerMode.CoopHost)
                {
                    Send(new MessageTime()
                    {
                        time = __instance.GetDayNightLerpValue()
                    });
                    Signal();
                }
                else
                if (updateMode == MultiplayerMode.CoopClient)
                {
                    if (hostTime != null)
                    {
                        environmentDayNightCycleValue.SetValue(__instance, hostTime.time);
                        hostTime = null;
                    }
                }
                yield return new WaitForSeconds(delay);
            }
        }

        static void ReceiveMessageTime(MessageTime mt)
        {
            if (updateMode == MultiplayerMode.CoopClient)
            {
                hostTime = mt;
            }
        }

        static void SendTerrainLayers()
        {
            List<TerrainLayerData> terrainLayersData = Managers.GetManager<TerrainVisualsHandler>().GetTerrainLayersData();

            var mtl = new MessageTerrainLayers();
            foreach (var data in terrainLayersData)
            {
                var layer = new MessageTerrainLayer();

                layer.layerId = data.name;
                layer.colorBase = data.colorBase;
                layer.colorCustom = data.colorCustom;
                layer.colorBaseLerp = data.GetColorBaseLerp();
                layer.colorCustomLerp = data.GetColorCustomLerp();

                mtl.layers.Add(layer);
            }
            LogInfo("SendTerrainLayers: " + mtl.layers.Count);
            Send(mtl);
            Signal();
        }

        static void ReceiveMessageTerrainLayers(MessageTerrainLayers mtl)
        {
            if (updateMode != MultiplayerMode.CoopClient)
            {
                return;
            }

            LogInfo("ReceiveMessageTerrainLayers: " + mtl.layers.Count);
            TerrainVisualsHandler manager = Managers.GetManager<TerrainVisualsHandler>();
            foreach (var layer in mtl.layers)
            {
                manager.SetTerrainLayerData(
                    layer.layerId, 
                    layer.colorBase, 
                    layer.colorCustom, 
                    layer.colorBaseLerp, 
                    layer.colorCustomLerp);
            }
            manager.FinishTerrainLayersSetup();
        }

        static void SendTerraformState()
        {
            MessageTerraformState mts = new MessageTerraformState();

            WorldUnitsHandler wuh = Managers.GetManager<WorldUnitsHandler>();
            mts.oxygen = wuh.GetUnit(DataConfig.WorldUnitType.Oxygen).GetValue();
            mts.heat = wuh.GetUnit(DataConfig.WorldUnitType.Heat).GetValue();
            mts.pressure = wuh.GetUnit(DataConfig.WorldUnitType.Pressure).GetValue();
            mts.biomass = wuh.GetUnit(DataConfig.WorldUnitType.Biomass).GetValue();

            _sendQueue.Enqueue(mts);
        }

        /// <summary>
        /// The vanilla game uses WorldUnit::Compute to periodically add to
        /// the total value, based on the delta time and the speed of change
        /// for the unit.
        /// 
        /// On the host, we let it happen.
        /// 
        /// On the client, we don't let it happen as it is force-synced from the host.
        /// </summary>
        /// <param name="__instance">The unit so we still can call SetCurrentLabelIndex.</param>
        /// <returns>False if on the client, true otherwise</returns>
        [HarmonyPrefix]
        [HarmonyPatch(typeof(WorldUnit), nameof(WorldUnit.Compute))]
        static bool WorldUnit_Compute(WorldUnit __instance)
        {
            if (updateMode == MultiplayerMode.CoopClient)
            {
                __instance.SetCurrentLabelIndex();
                return false;
            }
            return true;
        }

        /// <summary>
        /// Set to true in <see cref="ReceiveWelcome"/> so the first terraform
        /// sync doesn't ping over all the unlocks.
        /// </summary>
        static bool firstTerraformSync;

        static void ReceiveTerraformState(MessageTerraformState mts)
        {
            if (updateMode == MultiplayerMode.CoopClient)
            {
                WorldUnitsHandler wuh = Managers.GetManager<WorldUnitsHandler>();
                foreach (WorldUnit wu in wuh.GetAllWorldUnits())
                {
                    if (wu.GetUnitType() == DataConfig.WorldUnitType.Oxygen)
                    {
                        worldUnitCurrentTotalValue.SetValue(wu, mts.oxygen);
                    }
                    if (wu.GetUnitType() == DataConfig.WorldUnitType.Heat)
                    {
                        worldUnitCurrentTotalValue.SetValue(wu, mts.heat);
                    }
                    if (wu.GetUnitType() == DataConfig.WorldUnitType.Pressure)
                    {
                        worldUnitCurrentTotalValue.SetValue(wu, mts.pressure);
                    }
                    if (wu.GetUnitType() == DataConfig.WorldUnitType.Biomass)
                    {
                        worldUnitCurrentTotalValue.SetValue(wu, mts.biomass);
                    }
                    if (wu.GetUnitType() == DataConfig.WorldUnitType.Terraformation)
                    {
                        worldUnitCurrentTotalValue.SetValue(wu, mts.oxygen + mts.heat + mts.pressure + mts.biomass);
                    }
                }

                // Prevent pinging all unlocks after the join
                if (firstTerraformSync)
                {
                    firstTerraformSync = false;
                    var go = FindObjectOfType<AlertUnlockables>();
                    if (go != null)
                    {
                        AccessTools.Field(typeof(AlertUnlockables), "hasInited").SetValue(go, false);
                    }
                }

                List<GameObject> allWaterVolumes = Managers.GetManager<WaterHandler>().GetAllWaterVolumes();
                //LogInfo("allWaterVolumes.Count = " + allWaterVolumes.Count);
                foreach (GameObject go in allWaterVolumes)
                {
                    var wup = go.GetComponent<WorldUnitPositioning>();

                    //LogInfo("WorldUnitPositioning-Before: " + wup.transform.position);

                    worldUnitsPositioningWorldUnitsHandler.SetValue(wup, wuh);
                    worldUnitsPositioningHasMadeFirstInit.SetValue(wup, false);
                    wup.UpdateEvolutionPositioning();

                    //LogInfo("WorldUnitPositioning-After: " + wup.transform.position);
                }
            }
        }
    }
}
