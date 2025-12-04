// Copyright (c) 2022-2025, David Karnok & Contributors
// Licensed under the Apache License, Version 2.0

using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using SpaceCraft;
using System.Collections;
using UnityEngine;
using static UnityEngine.InputForUI.InputManagerProvider;

namespace UIShowGrabNMineCount
{
    [BepInPlugin("akarnokd.theplanetcraftermods.uishowgrabnminecount", "(UI) Show Grab N Mine Count", PluginInfo.PLUGIN_VERSION)]
    public class Plugin : BaseUnityPlugin
    {
        static ConfigEntry<bool> isEnabled;

        static AccessTools.FieldRef<ActionMinable, bool> fActionMinableMining;

        public void Awake()
        {
            LibCommon.BepInExLoggerFix.ApplyFix();

            // Plugin startup logic
            Logger.LogInfo($"Plugin is loaded!");

            isEnabled = Config.Bind("General", "Enabled", true, "Is the visual notification enabled?");

            fActionMinableMining = AccessTools.FieldRefAccess<ActionMinable, bool>("_mining");

            LibCommon.HarmonyIntegrityCheck.Check(typeof(Plugin));
            Harmony.CreateAndPatchAll(typeof(Plugin));
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(ActionMinable), "FinishMining")]
        static bool ActionMinable_FinishMining(float time,
            PlayerMainController ____playerSource,
            PlayerAnimations ____playerAnimations,
            ItemWorldDislpayer ____itemWorldDisplayer,
            ActionMinable __instance,
            ref IEnumerator __result)
        {
            __result = ActionMinable_FinishMining_Override(time, 
                ____playerSource, ____playerAnimations, 
                ____itemWorldDisplayer, __instance);
            return false;
        }

        static IEnumerator ActionMinable_FinishMining_Override(float time,
            PlayerMainController ____playerSource,
            PlayerAnimations ____playerAnimations,
            ItemWorldDislpayer ____itemWorldDisplayer,
            ActionMinable __instance)
        {
            yield return new WaitForSeconds(time);

            __instance.StopAllCoroutines();

            if (fActionMinableMining(__instance))
            {
                ____playerSource.GetMultitool().GetMultiToolMine().StopMining(true);
                ____playerSource.GetPlayerShareState().StopMining();
                ____itemWorldDisplayer.Hide();
                ____playerAnimations.AnimateRecolt(false, -1f);
                ____playerSource.GetMultitool().SetState(DataConfig.MultiToolState.Null);
                ____playerSource.GetMultitool().SetState(DataConfig.MultiToolState.Build);
            }
            fActionMinableMining(__instance) = false;

            // This also should fix two potential NPEs with quickly disappearing ores
            var woa = __instance.GetComponent<WorldObjectAssociated>();
            var wo = woa != null ? woa.GetWorldObject() : null;
            if (wo != null)
            {
                var inv = ____playerSource.GetPlayerBackpack().GetInventory();
                InventoriesHandler.Instance.AddWorldObjectToInventory(
                    wo,
                    inv,
                    grabbed: false,
                    success =>
                    {
                        if (success)
                        {
                            ShowInventoryAdded(wo, inv);
                        }
                        else
                        {
                            WorldObjectsHandler.Instance.DisableWorldObjectFromScene(wo, true, __instance.transform.position + new Vector3(0f, 1f, 0f));
                            WorldObjectsHandler.Instance.DestroyWorldObjectGOOnAllClients(wo.GetId(), 0f);
                            WorldObjectsHandler.Instance.DestroyWorldObject(wo.GetId(), false);

                        }
                    }
                );

            }
            else
            {
                Destroy(__instance.gameObject);
            }
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(ActionGrabable), "AddToInventory")]
        static bool ActionGrabable_AddToInventory(
            WorldObject worldObject, 
            PlayerMainController ____playerSource)
        {
            if (isEnabled.Value)
            {
                var inv = ____playerSource.GetPlayerBackpack().GetInventory();

                // FIXME: grabbed: true ???
                InventoriesHandler.Instance.AddWorldObjectToInventory(worldObject, inv, 
                    grabbed: true, success =>
                {
                    if (success)
                    {
                        ____playerSource.GetPlayerAudio().PlayGrab();
                        Managers.GetManager<DisplayersHandler>()?.GetItemWorldDisplayer()?.Hide();
                        
                        /*try
                        {
                            __instance.grabedEvent?.Invoke(worldObject);
                            __instance.grabedEvent = null;
                        }
                        finally
                        {*/
                            ShowInventoryAdded(worldObject, inv);
                        // }
                    }
                });


                return false;
            }
            return true;
        }

        static void ShowInventoryAdded(WorldObject worldObject, Inventory inventory)
        {
            int c = 0;
            Group group = worldObject.GetGroup();
            string gid = group.GetId();
            foreach (WorldObject wo in inventory.GetInsideWorldObjects())
            {
                if (wo.GetGroup().GetId() == gid)
                {
                    c++;
                }
            }

            string text = Readable.GetGroupName(group) + " + 1  (  " + c + "  )";
            InformationsDisplayer informationsDisplayer = Managers.GetManager<DisplayersHandler>().GetInformationsDisplayer();
            informationsDisplayer.AddInformation(2f, text, DataConfig.UiInformationsType.InInventory, group.GetImage());
        }

    }
}
