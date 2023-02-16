using BepInEx;
using BepInEx.Bootstrap;
using HarmonyLib;
using SpaceCraft;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
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
        /// With 0.6.001, some machines can now generate items from the linked groups of the
        /// machine's world object.
        /// </summary>
        /// <param name="___groupDatas">The location's generator list</param>
        /// <param name="___setGroupsDataViaLinkedGroup">Should the linked groups used for generating ore?</param>
        /// <param name="___worldObject">The machine's world object</param>
        /// <returns>The generated ore id or null if nothing is generated</returns>
        static string GenerateOre(List<GroupData> ___groupDatas,
            bool ___setGroupsDataViaLinkedGroup,
            WorldObject ___worldObject)
        {
            // Since 0.6.001
            if (___setGroupsDataViaLinkedGroup)
            {
                var linkedGroups = ___worldObject.GetLinkedGroups();
                if (linkedGroups != null && linkedGroups.Count != 0)
                {
                    return linkedGroups[UnityEngine.Random.Range(0, linkedGroups.Count)].GetId();
                }
                return null;
            }
            if (___groupDatas.Count != 0)
            {
                return ___groupDatas[UnityEngine.Random.Range(0, ___groupDatas.Count)].id;
            }
            return null;
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
        static bool MachineGenerator_GenerateAnObject(
            List<GroupData> ___groupDatas, 
            Inventory ___inventory,
            bool ___setGroupsDataViaLinkedGroup,
            WorldObject ___worldObject)
        {
            if (!modMachineRemoteDeposit)
            {
                if (updateMode == MultiplayerMode.CoopHost)
                {
                    /*
                    LogInfo("MachineGenerator_GenerateAnObject: ___groupDatas.Count = " + ___groupDatas.Count);
                    foreach (GroupData gr in ___groupDatas)
                    {
                        LogInfo("MachineGenerator_GenerateAnObject:   " + gr.id);
                    }
                    */
                    string oreId = GenerateOre(___groupDatas, ___setGroupsDataViaLinkedGroup, ___worldObject);
                    if (oreId != null)
                    {
                        LogInfo("MachineGenerator_GenerateAnObject: Generated " + oreId);
                        GenerateAnObjectAndDepositInto(___inventory, oreId);
                    }
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
                TryCreateInInventoryAndNotify(GroupsHandler.GetGroupViaId(oreId), inv, null, out _);
                return true;
            }
            return false;
        }
    }
}
