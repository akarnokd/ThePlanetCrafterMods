using BepInEx;
using SpaceCraft;
using HarmonyLib;
using UnityEngine;
using System.Collections.Generic;
using BepInEx.Logging;

namespace FixUnbrickSave
{
    [BepInPlugin("akarnokd.theplanetcraftermods.fixunbricksave", "(Fix) Unbrick Save", "1.0.0.1")]
    public class Plugin : BaseUnityPlugin
    {

        static ManualLogSource logger;

        private void Awake()
        {
            // Plugin startup logic
            Logger.LogInfo($"Plugin is loaded!");

            logger = Logger;

            Harmony.CreateAndPatchAll(typeof(Plugin));
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(WorldObjectsHandler), nameof(WorldObjectsHandler.InstantiateWorldObject))]
        static bool WorldObjectsHandler_InstantiateWorldObject(WorldObject _worldObject, ref GameObject __result)
        {
            var group = _worldObject.GetGroup();
            if (group == null)
            {
                __result = null;
                return false;
            }
            var gameObject = group.GetAssociatedGameObject();
            if (gameObject == null)
            {
                __result = null;
                logger.LogInfo("WorldObject " + _worldObject.GetId() + " of type " + group.GetId() + " at position " + _worldObject.GetPosition() + " can't be placed as it lacks a template");
                _worldObject.SetPositionAndRotation(new Vector3(0, 0, 0), new Quaternion(0, 0, 0, 0));
                return false;
            }
            return true;
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(SaveFileDisplayer), "SetData")]
        private static void SaveFileDisplayer_SetData(ref WorldUnit _terraformationUnit)
        {
            bool flag = _terraformationUnit == null;
            if (flag)
            {
                _terraformationUnit = new WorldUnit(0f, new List<string>
                {
                    "Ti",
                    "kTi",
                    "MTi",
                    "GTi",
                    "TTi",
                    "PTi",
                    "ETi",
                    "ZTi",
                    "YTi"
                }, DataConfig.WorldUnitType.Terraformation);
            }
        }
    }
}
