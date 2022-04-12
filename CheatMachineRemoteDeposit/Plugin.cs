using BepInEx;
using MijuTools;
using BepInEx.Configuration;
using SpaceCraft;
using UnityEngine.InputSystem;
using HarmonyLib;
using UnityEngine;
using System.Collections.Generic;
using System.Reflection;

namespace CheatMachineRemoteDeposit
{
    [BepInPlugin("akarnokd.theplanetcraftermods.cheatmachineremotedeposit", "(Cheat) Machines Deposit Into Remote Containers", "1.0.0.2")]
    [BepInDependency("akarnokd.theplanetcraftermods.cheatinventorystacking", BepInDependency.DependencyFlags.SoftDependency)]
    public class Plugin : BaseUnityPlugin
    {

        private void Awake()
        {
            // Plugin startup logic
            Logger.LogInfo($"Plugin is loaded!");

            Harmony.CreateAndPatchAll(typeof(Plugin));
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(MachineGenerator), "GenerateAnObject")]
        static bool MachineGenerator_GenerateAnObject(Inventory ___inventory, List<GroupData> ___groupDatas)
        {
            WorldObject worldObject = WorldObjectsHandler.CreateNewWorldObject(
                GroupsHandler.GetGroupViaId(
                    ___groupDatas[UnityEngine.Random.Range(0, ___groupDatas.Count)].id), 0);
            Inventory inventory = ___inventory;
            string gid = "*" + worldObject.GetGroup().GetId().ToLower();
            foreach (WorldObject wo2 in WorldObjectsHandler.GetAllWorldObjects())
            {
                if (wo2 != null && wo2.HasLinkedInventory())
                {
                    Inventory inv2 = InventoriesHandler.GetInventoryById(wo2.GetLinkedInventoryId());
                    if (inv2 != null && !inv2.IsFull())
                    {
                        string txt = wo2.GetText();
                        if (txt != null && txt.ToLower().Contains(gid))
                        {
                            inventory = inv2;
                            break;
                        }
                    }
                }
            }
            if (!inventory.AddItem(worldObject))
            {
                WorldObjectsHandler.DestroyWorldObject(worldObject);
            }
            return false;
        }
    }
}
