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
    [BepInPlugin("akarnokd.theplanetcraftermods.cheatmachineremotedeposit", "(Cheat) Machines Deposit Into Remote Containers", PluginInfo.PLUGIN_VERSION)]
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

        static ConfigEntry<bool> modEnabled;
        static ConfigEntry<bool> debugMode;

        static readonly Dictionary<string, string> depositAliases = new();

        /// <summary>
        /// Set this function to override the last phase of generating and depositing the actual ore.
        /// </summary>
        public static Func<Inventory, string, bool> overrideDeposit;

        private void Awake()
        {
            // Plugin startup logic
            Logger.LogInfo($"Plugin is loaded!");

            logger = Logger;

            modEnabled = Config.Bind("General", "Enabled", true, "Is the mod enabled?");
            debugMode = Config.Bind("General", "DebugMode", false, "Produce detailed logs? (chatty)");

            ProcessAliases(Config.Bind("General", "Aliases", "", "A comma separated list of resourceId:aliasForId, for example, Iron:A,Cobalt:B,Uranim:C"));

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

            var harmony = Harmony.CreateAndPatchAll(typeof(Plugin));
            LibCommon.SaveModInfo.Patch(harmony);
        }

        void ProcessAliases(ConfigEntry<string> cfe)
        {
            var s = cfe.Value.Trim();
            if (s.Length != 0)
            {
                var i = 0;
                foreach (var str in s.Split(','))
                {
                    var idalias = str.Split(':');
                    if (idalias.Length != 2)
                    {
                        Logger.LogWarning("Wrong alias @ index " + i + " value " + str);
                    }
                    else
                    {
                        depositAliases[idalias[0]] = idalias[1].ToLower();
                        log("Alias " + idalias[0] + " -> " + idalias[1]);
                    }
                    i++;
                }
            }
        }

        static bool IsFull(Inventory inv, string gid)
        {
            if (isFullStacked != null)
            {
                // fine, machines always stack for now
                return isFullStacked.Invoke(inv.GetInsideWorldObjects(), inv.GetSize(), gid);
            }
            return inv.IsFull();
        }

        static void log(string s)
        {
            if (debugMode.Value)
            {
                logger.LogInfo(s);
            }
        }

        static string GenerateOre(
            List<GroupData> ___groupDatas,
            bool ___setGroupsDataViaLinkedGroup,
            WorldObject ___worldObject,
            List<GroupData> ___groupDatasTerraStage,
            WorldUnitsHandler ___worldUnitsHandler,
            TerraformStage ___terraStage)
        {
            // Since 0.6.001
            if (___setGroupsDataViaLinkedGroup)
            {
                var linkedGroups = ___worldObject.GetLinkedGroups();
                if (linkedGroups != null && linkedGroups.Count != 0)
                {
                    return linkedGroups[UnityEngine.Random.Range(0, linkedGroups.Count)].id;
                }
                return null;
            }
            if (___groupDatas.Count != 0)
            {
                // Since 0.7.001
                var groupDatasCopy = new List<GroupData>(___groupDatas);
                if (___groupDatasTerraStage.Count != 0
                    && ___worldUnitsHandler.IsWorldValuesAreBetweenStages(___terraStage, null))
                {
                    groupDatasCopy.AddRange(___groupDatasTerraStage);
                }

                return groupDatasCopy[UnityEngine.Random.Range(0, groupDatasCopy.Count)].id;
            }
            return null;
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(MachineGenerator), "GenerateAnObject")]
        static bool MachineGenerator_GenerateAnObject(
            Inventory ___inventory, List<GroupData> ___groupDatas,
            bool ___setGroupsDataViaLinkedGroup,
            WorldObject ___worldObject,
            List<GroupData> ___groupDatasTerraStage,
            WorldUnitsHandler ___worldUnitsHandler,
            TerraformStage ___terraStage
        )
        {
            if (!modEnabled.Value)
            {
                return true;
            }

            log("GenerateAnObject start");
            string oreId = GenerateOre(___groupDatas, ___setGroupsDataViaLinkedGroup, ___worldObject,
                    ___groupDatasTerraStage, ___worldUnitsHandler, ___terraStage);

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

            if (oreId == null)
            {
                return false;
            }

            // retarget inventory
            Inventory inventory = ___inventory;
            bool inventoryFound = false;
            string gid = "*" + oreId.ToLower();
            if (depositAliases.TryGetValue(oreId, out var alias))
            {
                gid = alias;
                log("  Deposit alias found: " + alias);
            }
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
