using BepInEx;
using BepInEx.Bootstrap;
using HarmonyLib;
using SpaceCraft;
using System;
using System.Collections;
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
        /// If true, the <c>akarnokd.theplanetcraftermods.cheatmachineremotedeposit</c>
        /// mod has been detected and its generation routine overridden.
        /// </summary>
        static bool modMachineRemoteDeposit;

        /// <summary>
        /// If the mod <c>akarnokd.theplanetcraftermods.cheatmachineremotedeposit</c>
        /// is found, install the deposit override callback.
        /// 
        /// If the mod <c>akarnokd.theplanetcraftermods.cheatautoharvest</c>
        /// is found, install a callback to disable it on the client.
        /// </summary>
        static void TryInstallMachineModOverrides()
        {
            if (Chainloader.PluginInfos.TryGetValue(modCheatMachineRemoteDepositGuid, out var pi))
            {
                AccessTools.Field(pi.Instance.GetType(), "overrideDeposit")
                    .SetValue(null, new Func<Inventory, string, bool>(GenerateAnObjectAndDepositInto));
                modMachineRemoteDeposit = true;
                LogInfo("Mod " + modCheatMachineRemoteDepositGuid + " Found, Overriding its deposit logic");
            }
            else
            {
                LogInfo("Mod " + modCheatMachineRemoteDepositGuid + " Not found");
            }

            if (Chainloader.PluginInfos.TryGetValue(modCheatAutoHarvestGuid, out pi))
            {
                AccessTools.Field(pi.Instance.GetType(), "canExecute")
                    .SetValue(null, new Func<bool>(AllowAutoHarvest));
                modMachineRemoteDeposit = true;
                LogInfo("Mod " + modCheatAutoHarvestGuid + " Found, Overriding its deposit logic");

                // I think nothing else needs overridden because the grower's algae already have
                // a WorldObject, and the depositer only moves them to inventory - all standard messaging
            }
            else
            {
                LogInfo("Mod " + modCheatAutoHarvestGuid + " Not found");
            }
        }

        /// <summary>
        /// The vanilla game's machines, such as the miners, the water collectors and the gas
        /// extractors use MachineGenerator::GenerateAnObject to generate an object and add it
        /// to the machine's own inventory.
        /// 
        /// On the host, we override and reproduce the behavior to get to the world object and
        /// send it to the client.
        /// 
        /// On the client, we don't let it generate any object so the host can generate it.
        /// 
        /// If the mod <c>akarnokd.theplanetcraftermods.cheatmachineremotedeposit</c> is
        /// detected, the deposit is overridden and the method
        /// <see cref="GenerateAnObjectAndDepositInto(Inventory, string)"/> will handle
        /// the dispatching of the WorldObject and inventory change.
        /// </summary>
        /// <returns>True in singleplayer and if the mod is installed so it can run, false otherwise.</returns>
        [HarmonyPrefix]
        [HarmonyPatch(typeof(MachineGenerator), "GenerateAnObject")]
        static bool MachineGenerator_GenerateAnObject(List<Group> ___groupDatas, Inventory ___inventory)
        {
            if (!modMachineRemoteDeposit)
            {
                if (updateMode == MultiplayerMode.CoopHost)
                {
                    string oreId = ___groupDatas[UnityEngine.Random.Range(0, ___groupDatas.Count)].id;
                    GenerateAnObjectAndDepositInto(___inventory, oreId);
                    return false;
                }
                else
                if (updateMode == MultiplayerMode.CoopClient)
                {
                    return false;
                }
            }
            return true;
        }

        /// <summary>
        /// Called by the mod <c>akarnokd.theplanetcraftermods.cheatmachineremotedeposit</c>
        /// in its <c>MachineGenerator_GenerateAnObject</c> patched method when
        /// it was determined what to deposit to which inventory.
        /// </summary>
        /// <param name="inv">The target inventory.</param>
        /// <param name="oreId">The ore to create and deposit.</param>
        /// <returns>True if the deposit was handled, false if the original mod should handle it</returns>
        static bool GenerateAnObjectAndDepositInto(Inventory inv, string oreId)
        {
            if (updateMode == MultiplayerMode.CoopClient)
            {
                return true;
            }
            if (updateMode == MultiplayerMode.CoopHost)
            {
                var wo = WorldObjectsHandler.CreateNewWorldObject(GroupsHandler.GetGroupViaId(oreId));
                suppressInventoryChange = true;
                bool added;
                try
                {
                    added = inv.AddItem(wo);
                }
                finally
                {
                    suppressInventoryChange = false;
                }

                if (added)
                {
                    // We need to send the object first, then send the instruction that it has been
                    // Added to the target inventory.
                    SendWorldObject(wo, false);
                    Send(new MessageInventoryAdded()
                    {
                        inventoryId = inv.GetId(),
                        itemId = wo.GetId(),
                        groupId = oreId
                    });
                    Signal();
                }
                else
                {
                    WorldObjectsHandler.DestroyWorldObject(wo);
                }
                return true;
            }
            return false;
        }

        /// <summary>
        /// Called by the mod <c>akarnokd.theplanetcraftermods.cheatautoharvest</c> to
        /// check if its deposit routine should run.
        /// For clients, it should not.
        /// </summary>
        /// <returns>False for clients, true otherwise</returns>
        static bool AllowAutoHarvest()
        {
            return updateMode != MultiplayerMode.CoopClient;
        }

        // FIXME this grower stuff

        [HarmonyPostfix]
        [HarmonyPatch(typeof(MachineGrower), nameof(MachineGrower.SetGrowerInventory))]
        static void MachineGrower_SetGrowerInventory(
            MachineGrower __instance,
            WorldObject ___worldObjectGrower,
            float ___updateSizeInterval)
        {
            __instance.StartCoroutine(MachineGrower_GrowthTracker(___worldObjectGrower, ___updateSizeInterval));
        }

        static IEnumerator MachineGrower_GrowthTracker(WorldObject growerMachine, float updateSizeInterval)
        {
            for (; ; )
            {
                SendWorldObject(growerMachine, false);
                yield return new WaitForSeconds(updateSizeInterval);
            }
        }
        
    }
}
