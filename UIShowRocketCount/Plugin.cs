using BepInEx;
using MijuTools;
using SpaceCraft;
using HarmonyLib;
using TMPro;
using UnityEngine;
using System.Collections;
using System.Collections.Generic;

namespace UIShowRocketCount
{
    [BepInPlugin("akarnokd.theplanetcraftermods.uishowrocketcount", "(UI) Show Rocket Counts", "1.0.0.0")]
    public class Plugin : BaseUnityPlugin
    {

        private void Awake()
        {
            // Plugin startup logic
            Logger.LogInfo($"Plugin is loaded!");

            Harmony.CreateAndPatchAll(typeof(Plugin));
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(WorldUnitsGenerationDisplayer), "RefreshDisplay")]

        static bool WorldUnitsGenerationDisplayer_RefreshDisplay(ref IEnumerator __result, float timeRepeat,
            WorldUnitsHandler ___worldUnitsHandler, List<TextMeshProUGUI> ___textFields, List<DataConfig.WorldUnitType> ___correspondingUnit)
        {
            __result = RefreshDisplayEnumerator(timeRepeat, ___worldUnitsHandler, ___textFields, ___correspondingUnit);
            return false;
        }

        static IEnumerator RefreshDisplayEnumerator(float timeRepeat, WorldUnitsHandler worldUnitsHandler, 
            List<TextMeshProUGUI> textFields, List<DataConfig.WorldUnitType> correspondingUnit)
        {
            for (; ; )
            {
                if (worldUnitsHandler != null)
                {
                    Dictionary<DataConfig.WorldUnitType, int> rockets = new Dictionary<DataConfig.WorldUnitType, int>();
                    foreach (WorldObject worldObject in WorldObjectsHandler.GetAllWorldObjects())
                    {
                        if (worldObject.GetGroup().GetId().StartsWith("RocketOxygen"))
                        {
                            rockets.TryGetValue(DataConfig.WorldUnitType.Oxygen, out int v);
                            rockets[DataConfig.WorldUnitType.Oxygen] = v + 1;
                        }
                        if (worldObject.GetGroup().GetId().StartsWith("RocketHeat"))
                        {
                            rockets.TryGetValue(DataConfig.WorldUnitType.Heat, out int v2);
                            rockets[DataConfig.WorldUnitType.Heat] = v2 + 1;
                        }
                        if (worldObject.GetGroup().GetId().StartsWith("RocketPressure"))
                        {
                            rockets.TryGetValue(DataConfig.WorldUnitType.Pressure, out int v3);
                            rockets[DataConfig.WorldUnitType.Pressure] = v3 + 1;
                        }
                        if (worldObject.GetGroup().GetId().StartsWith("RocketBiomass"))
                        {
                            rockets.TryGetValue(DataConfig.WorldUnitType.Biomass, out int v4);
                            rockets[DataConfig.WorldUnitType.Biomass] = v4 + 1;
                        }
                    }
                    for (int idx = 0; idx < textFields.Count; idx++)
                    {
                        WorldUnit unit = worldUnitsHandler.GetUnit(correspondingUnit[idx]);
                        if (unit != null)
                        {
                            string s = unit.GetDisplayStringForValue(unit.GetCurrentValuePersSec(), false, 0) + "/s";
                            rockets.TryGetValue(unit.GetUnitType(), out int c);
                            if (c > 0)
                            {
                                s = c + " x -----    " + s;
                            }
                            textFields[idx].text = s;
                        }
                    }
                }
                yield return new WaitForSeconds(timeRepeat);
            }
        }
    }
}
