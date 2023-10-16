using BepInEx;
using HarmonyLib;
using SpaceCraft;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using FeatMultiplayer.MessageTypes;

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
        static void EnvironmentDayNightCycle_Start(EnvironmentDayNightCycle __instance, ref float ___fullDayStayTime)
        {
            environmentDayNightCycleValue = AccessTools.Field(typeof(EnvironmentDayNightCycle), "dayNightLerpValue");
            if (updateMode == MultiplayerMode.CoopClient)
            {
                // don't let the client interpolation run at all
                LogInfo("EnvironmentDayNightCycle_Start: Stopping Day-night cycle on the client.");
                __instance.StopAllCoroutines();
            }
            __instance.StartCoroutine(DayNightCycle(__instance, 0.5f));
            // Speed up day-night for testing
            //___fullDayStayTime = 1;
        }

        static IEnumerator DayNightCycle(EnvironmentDayNightCycle __instance, float delay)
        {
            for (; ; )
            {
                if (updateMode == MultiplayerMode.CoopHost)
                {
                    var dn = __instance.GetDayNightLerpValue();
                    //LogInfo("DayNightCycle: " + dn);
                    /*
                    LogWarning("FullDayStayTime: " + __instance.fullDayStayTime);
                    LogWarning("Time: " + Time.time);
                    LogWarning("pongValue: " + AccessTools.Field(typeof(EnvironmentDayNightCycle), "pongValue").GetValue(__instance));
                    LogWarning("fullPhaseTimeReached: " + AccessTools.Field(typeof(EnvironmentDayNightCycle), "fullPhaseTimeReached").GetValue(__instance));
                    LogWarning("previousLerpValue: " + AccessTools.Field(typeof(EnvironmentDayNightCycle), "previousLerpValue").GetValue(__instance));
                    */
                    SendAllClients(new MessageTime()
                    {
                        time = dn
                    }, true);
                }
                else
                if (updateMode == MultiplayerMode.CoopClient)
                {
                    if (hostTime != null)
                    {
                        //LogInfo("DayNightCycle: " + hostTime.time);
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
            SendAllClients(mtl, true);
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
            var m = AccessTools.Method(typeof(TerrainVisualsHandler), "SetTerrainsColorsFromDb");
            m.Invoke(manager, new object[0]);
        }

        static void SendTerraformState()
        {
            MessageTerraformState mts = new();

            WorldUnitsHandler wuh = Managers.GetManager<WorldUnitsHandler>();
            mts.oxygen = wuh.GetUnit(DataConfig.WorldUnitType.Oxygen).GetValue();
            mts.heat = wuh.GetUnit(DataConfig.WorldUnitType.Heat).GetValue();
            mts.pressure = wuh.GetUnit(DataConfig.WorldUnitType.Pressure).GetValue();
            mts.plants = wuh.GetUnit(DataConfig.WorldUnitType.Plants).GetValue();
            mts.insects = wuh.GetUnit(DataConfig.WorldUnitType.Insects).GetValue();
            mts.animals = wuh.GetUnit(DataConfig.WorldUnitType.Animals).GetValue();
            mts.tokens = TokensHandler.GetTokensNumber();
            mts.tokensAllTime = TokensHandler.GetAllTimeTokensNumber();

            SendAllClients(mts);
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
                    if (wu.GetUnitType() == DataConfig.WorldUnitType.Plants)
                    {
                        worldUnitCurrentTotalValue.SetValue(wu, mts.plants);
                    }
                    if (wu.GetUnitType() == DataConfig.WorldUnitType.Insects)
                    {
                        worldUnitCurrentTotalValue.SetValue(wu, mts.insects);
                    }
                    if (wu.GetUnitType() == DataConfig.WorldUnitType.Animals)
                    {
                        worldUnitCurrentTotalValue.SetValue(wu, mts.animals);
                    }
                    if (wu.GetUnitType() == DataConfig.WorldUnitType.Biomass)
                    {
                        worldUnitCurrentTotalValue.SetValue(wu, mts.GetBiomass());
                    }
                    if (wu.GetUnitType() == DataConfig.WorldUnitType.Terraformation)
                    {
                        worldUnitCurrentTotalValue.SetValue(wu, mts.GetTi());
                    }
                }

                // Prevent pinging all unlocks after the join
                if (firstTerraformSync)
                {
                    foreach (var go in FindObjectsByType<AlertUnlockables>(FindObjectsSortMode.None))
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

                    ForceUpdateWorldUnitPositioning(wup, wuh);

                    //LogInfo("WorldUnitPositioning-After: " + wup.transform.position);
                }

                var wuph = Managers.GetManager<WorldUnitPositioningHandler>();
                List<WorldUnitPositioning> wups = (List<WorldUnitPositioning>)worldUnitsPositioningHandlerAllWorldUnitPositionings.GetValue(wuph);
                foreach (var wup in wups)
                {
                    ForceUpdateWorldUnitPositioning(wup, wuh);
                }

                var prevTokens = TokensHandler.GetTokensNumber();
                TokensHandler.SetTotalTokens(mts.tokens);
                if (prevTokens < mts.tokens && !firstTerraformSync)
                {
                    Managers.GetManager<PopupsHandler>().PopupNewTokens(mts.tokens - prevTokens);
                }
                TokensHandler.SetAllTimeTokensNumber(mts.tokensAllTime);

                firstTerraformSync = false;
            }

            static void ForceUpdateWorldUnitPositioning(WorldUnitPositioning wup, WorldUnitsHandler wuh)
            {
                worldUnitsPositioningWorldUnitsHandler.SetValue(wup, wuh);
                worldUnitsPositioningHasMadeFirstInit.SetValue(wup, false);
                wup.UpdateEvolutionPositioning();
            }
        }
    }
}
