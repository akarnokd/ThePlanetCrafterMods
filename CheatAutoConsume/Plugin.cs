using BepInEx;
using MijuTools;
using SpaceCraft;
using HarmonyLib;
using TMPro;
using UnityEngine;
using System.Collections;
using System.Collections.Generic;

namespace PerfLoadInventoriesFaster
{
    [BepInPlugin("akarnokd.theplanetcraftermods.cheatautoconsume", "(Cheat) Auto Consume Oxygen-Water-Food", "1.0.0.0")]
    public class Plugin : BaseUnityPlugin
    {

        private void Awake()
        {
            // Plugin startup logic
            Logger.LogInfo($"Plugin is loaded!");

            Harmony.CreateAndPatchAll(typeof(Plugin));
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(PlayerGaugeOxygen), "GaugeVerifications")]
        static void PlayerGaugeOxygen_GaugeVerifications_Pre(bool ___hasAlertedCritical, ref bool __state)
        {
            __state = ___hasAlertedCritical;
        }
        [HarmonyPostfix]
        [HarmonyPatch(typeof(PlayerGaugeOxygen), "GaugeVerifications")]
        static void PlayerGaugeOxygen_GaugeVerifications_Post(bool ___hasAlertedCritical, ref bool __state)
        {
            if (!__state && ___hasAlertedCritical)
            {
                PlayerMainController activePlayerController = Managers.GetManager<PlayersManager>().GetActivePlayerController();
                Inventory inv = Managers.GetManager<PlayersManager>().GetActivePlayerController().GetPlayerBackpack().GetInventory();
                foreach (WorldObject _worldObject in inv.GetInsideWorldObjects())
                {
                    if (_worldObject.GetGroup() is GroupItem)
                    {
                        GroupItem groupItem = (GroupItem)_worldObject.GetGroup();
                        int groupValue = groupItem.GetGroupValue();
                        if (groupItem.GetUsableType() == DataConfig.UsableType.Breathable && activePlayerController.GetGaugesHandler().Breath(groupValue))
                        {
                            inv.RemoveItem(_worldObject, true);
                            break;
                        }
                    }
                }
            }
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(PlayerGaugeThirst), "GaugeVerifications")]
        static void PlayerGaugeThirst_GaugeVerifications_Pre(bool ___hasAlertedCritical, ref bool __state)
        {
            __state = ___hasAlertedCritical;
        }
        [HarmonyPostfix]
        [HarmonyPatch(typeof(PlayerGaugeOxygen), "GaugeVerifications")]
        static void PlayerGaugeThirst_GaugeVerifications_Post(bool ___hasAlertedCritical, ref bool __state)
        {
            if (!__state && ___hasAlertedCritical)
            {
                PlayerMainController activePlayerController = Managers.GetManager<PlayersManager>().GetActivePlayerController();
                Inventory inv = Managers.GetManager<PlayersManager>().GetActivePlayerController().GetPlayerBackpack().GetInventory();
                foreach (WorldObject _worldObject in inv.GetInsideWorldObjects())
                {
                    if (_worldObject.GetGroup() is GroupItem)
                    {
                        GroupItem groupItem = (GroupItem)_worldObject.GetGroup();
                        int groupValue = groupItem.GetGroupValue();
                        if (groupItem.GetUsableType() == DataConfig.UsableType.Drinkable && activePlayerController.GetGaugesHandler().Drink(groupValue))
                        {
                            inv.RemoveItem(_worldObject, true);
                            break;
                        }
                    }
                }
            }
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(PlayerGaugeHealth), "GaugeVerifications")]
        static void PlayerGaugeHealth_GaugeVerifications_Pre(bool ___hasAlertedCritical, ref bool __state)
        {
            __state = ___hasAlertedCritical;
        }
        [HarmonyPostfix]
        [HarmonyPatch(typeof(PlayerGaugeOxygen), "GaugeVerifications")]
        static void PlayerGaugeHealth_GaugeVerifications_Post(bool ___hasAlertedCritical, ref bool __state)
        {
            if (!__state && ___hasAlertedCritical)
            {
                PlayerMainController activePlayerController = Managers.GetManager<PlayersManager>().GetActivePlayerController();
                Inventory inv = Managers.GetManager<PlayersManager>().GetActivePlayerController().GetPlayerBackpack().GetInventory();
                foreach (WorldObject _worldObject in inv.GetInsideWorldObjects())
                {
                    if (_worldObject.GetGroup() is GroupItem)
                    {
                        GroupItem groupItem = (GroupItem)_worldObject.GetGroup();
                        int groupValue = groupItem.GetGroupValue();
                        if (groupItem.GetUsableType() == DataConfig.UsableType.Eatable && activePlayerController.GetGaugesHandler().Eat(groupValue))
                        {
                            inv.RemoveItem(_worldObject, true);
                            break;
                        }
                    }
                }
            }
        }
    }
}
