using BepInEx;
using MijuTools;
using BepInEx.Configuration;
using SpaceCraft;
using UnityEngine.InputSystem;
using HarmonyLib;
using UnityEngine;
using System.Collections.Generic;
using System.Reflection;
using System;
using BepInEx.Bootstrap;

namespace CheatMachineRemoteDeposit
{
    [BepInPlugin("akarnokd.theplanetcraftermods.cheatmachineremotedeposit", "(Cheat) Machines Deposit Into Remote Containers", "1.0.0.3")]
    [BepInDependency(cheatInventoryStackingGuid, BepInDependency.DependencyFlags.SoftDependency)]
    public class Plugin : BaseUnityPlugin
    {
        const string cheatInventoryStackingGuid = "akarnokd.theplanetcraftermods.cheatinventorystacking";
        static Func<List<WorldObject>, int, string, bool> isFullStacked;

        private void Awake()
        {
            // Plugin startup logic
            Logger.LogInfo($"Plugin is loaded!");

            if (Chainloader.PluginInfos.TryGetValue(cheatInventoryStackingGuid, out BepInEx.PluginInfo pi))
            {
                MethodInfo mi = AccessTools.Method(pi.Instance.GetType(), "IsFullStacked", new Type[] { typeof(List<WorldObject>), typeof(int), typeof(string) });
                isFullStacked = AccessTools.MethodDelegate<Func<List<WorldObject>, int, string, bool>>(mi, pi.Instance);
            }

            Harmony.CreateAndPatchAll(typeof(Plugin));
        }
        static bool IsFull(Inventory inv, WorldObject wo)
        {
            if (isFullStacked != null)
            {
                return isFullStacked.Invoke(inv.GetInsideWorldObjects(), inv.GetSize(), wo.GetGroup().GetId());
            }
            return inv.IsFull();
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
                    if (inv2 != null && !IsFull(inv2, worldObject))
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
