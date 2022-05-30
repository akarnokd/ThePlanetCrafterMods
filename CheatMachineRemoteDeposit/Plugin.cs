using BepInEx;
using BepInEx.Configuration;
using SpaceCraft;
using HarmonyLib;
using System.Collections.Generic;
using System.Reflection;
using System;
using BepInEx.Bootstrap;
using BepInEx.Logging;

namespace CheatMachineRemoteDeposit
{
    [BepInPlugin("akarnokd.theplanetcraftermods.cheatmachineremotedeposit", "(Cheat) Machines Deposit Into Remote Containers", "1.0.0.6")]
    [BepInDependency(cheatInventoryStackingGuid, BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency(oreExtractorTweaksGuid, BepInDependency.DependencyFlags.SoftDependency)]
    public class Plugin : BaseUnityPlugin
    {
        const string cheatInventoryStackingGuid = "akarnokd.theplanetcraftermods.cheatinventorystacking";
        static Func<List<WorldObject>, int, string, bool> isFullStacked;

        const string oreExtractorTweaksGuid = "Lathrey-OreExtractorTweaks";
        static ConfigEntry<bool> configOnlyExtractDetectedOre;
        static ConfigEntry<bool> configDetectedOreEveryTick;

        static ManualLogSource logger;

        static bool debugMode;

        /// <summary>
        /// Set this function to override the last phase of generating and depositing the actual ore.
        /// </summary>
        public static Func<Inventory, string, bool> overrideDeposit;

        private void Awake()
        {
            // Plugin startup logic
            Logger.LogInfo($"Plugin is loaded!");

            logger = Logger;

            if (Chainloader.PluginInfos.TryGetValue(cheatInventoryStackingGuid, out BepInEx.PluginInfo pi))
            {
                Logger.LogInfo(cheatInventoryStackingGuid + " detected, getting IsFullStacked");
                MethodInfo mi = AccessTools.Method(pi.Instance.GetType(), "IsFullStacked", new Type[] { typeof(List<WorldObject>), typeof(int), typeof(string) });
                isFullStacked = AccessTools.MethodDelegate<Func<List<WorldObject>, int, string, bool>>(mi, pi.Instance);
            }

            if (Chainloader.PluginInfos.TryGetValue(oreExtractorTweaksGuid, out BepInEx.PluginInfo ei))
            {
                Logger.LogInfo(oreExtractorTweaksGuid + " detected, overriding configOnlyExtractDetectedOre");
                FieldInfo fieldInfo = AccessTools.Field(ei.Instance.GetType(), "configOnlyExtractDetectedOre");
                configOnlyExtractDetectedOre = (ConfigEntry<bool>)fieldInfo.GetValue(null);

                ConfigEntry<bool> overrideConfigOnlyExtractDetectedOre = Config.Bind("General", "overrideConfigOnlyExtractDetectedOre", false, "Override overrideConfigOnlyExtractDetectedOre, always false.");
                overrideConfigOnlyExtractDetectedOre.Value = false;
                fieldInfo.SetValue(null, overrideConfigOnlyExtractDetectedOre);

                configDetectedOreEveryTick = (ConfigEntry<bool>)AccessTools.Field(ei.Instance.GetType(), "configDetectedOreEveryTick").GetValue(null);
            }

            Harmony.CreateAndPatchAll(typeof(Plugin));
        }
        static bool IsFull(Inventory inv, string gid)
        {
            if (isFullStacked != null)
            {
                return isFullStacked.Invoke(inv.GetInsideWorldObjects(), inv.GetSize(), gid);
            }
            return inv.IsFull();
        }

        static void log(string s)
        {
            if (debugMode)
            {
                logger.LogInfo(s);
            }
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(MachineGenerator), "GenerateAnObject")]
        static bool MachineGenerator_GenerateAnObject(Inventory ___inventory, List<GroupData> ___groupDatas)
        {
            log("GenerateAnObject start");
            int index = UnityEngine.Random.Range(0, ___groupDatas.Count);
            string oreId = ___groupDatas[index].id;

            log("  Ore detected: " + oreId);
            // If Lathrey's OreExtractorTweaks are installed, intertwine its logic

            bool dropIfNoTarget = false;
            if (configOnlyExtractDetectedOre != null && configOnlyExtractDetectedOre.Value) 
            {
                string detectedOreId = ___groupDatas[___groupDatas.Count - 1].id;
                if (configDetectedOreEveryTick.Value || oreId == detectedOreId)
                {
                    oreId = detectedOreId;
                    log("    Ore overridden: " + oreId);
                }
                else
                {
                    dropIfNoTarget = true;
                }
            }


            // retarget inventory
            Inventory inventory = ___inventory;
            bool inventoryFound = false;
            string gid = "*" + oreId.ToLower();
            foreach (WorldObject wo2 in WorldObjectsHandler.GetAllWorldObjects())
            {
                if (wo2 != null && wo2.HasLinkedInventory())
                {
                    string txt = wo2.GetText();
                    if (txt != null && txt.ToLower().Contains(gid))
                    {
                        Inventory inv2 = InventoriesHandler.GetInventoryById(wo2.GetLinkedInventoryId());
                        if (inv2 != null && !IsFull(inv2, oreId))
                        {
                            inventory = inv2;
                            inventoryFound = true;
                            break;
                        }
                        else
                        {
                            log("This inventory is full: " + txt);
                        }
                    }
                }
            }

            log("  Inventory found? " + inventoryFound);
            log("  Drop If No Target? " + dropIfNoTarget);

            // inventoryFound -> add
            // !inventoryFound && !dropIfNoTarget -> add
            // !inventoryFound && dropIfNoTarget -> skip

            if (inventoryFound || !dropIfNoTarget)
            {
                if (overrideDeposit == null || !overrideDeposit.Invoke(inventory, oreId))
                {
                    // instantiate and add the ore to the target inventory
                    WorldObject worldObject = WorldObjectsHandler.CreateNewWorldObject(GroupsHandler.GetGroupViaId(oreId), 0);
                    if (!inventory.AddItem(worldObject))
                    {
                        WorldObjectsHandler.DestroyWorldObject(worldObject);
                    }
                    else
                    {
                        log("  Added to inventory");
                    }
                }
            }
            return false;
        }
    }
}
