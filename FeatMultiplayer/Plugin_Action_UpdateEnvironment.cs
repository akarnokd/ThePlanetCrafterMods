using BepInEx;
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
