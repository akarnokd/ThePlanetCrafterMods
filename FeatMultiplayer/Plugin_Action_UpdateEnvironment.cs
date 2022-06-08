using BepInEx;
using HarmonyLib;
using MijuTools;
using SpaceCraft;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace FeatMultiplayer
{
    public partial class Plugin : BaseUnityPlugin
    {

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
        static bool Compute(WorldUnit __instance)
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
                    if (go != null) {
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
