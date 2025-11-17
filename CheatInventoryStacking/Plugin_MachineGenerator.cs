// Copyright (c) 2022-2025, David Karnok & Contributors
// Licensed under the Apache License, Version 2.0

using HarmonyLib;
using SpaceCraft;
using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace CheatInventoryStacking
{
    public partial class Plugin
    {
        [HarmonyPrefix]
        [HarmonyPatch(typeof(MachineGenerator), "GenerateAnObject")]
        static bool Patch_MachineGenerator_GenerateAnObject(
            Inventory ____inventory,
            List<GroupData> ___groupDatas,
            bool ___setGroupsDataViaLinkedGroup,
            WorldObject ____worldObject,
            List<GroupData> ___groupDatasTerraStage,
            ref WorldUnitsHandler ____worldUnitsHandler,
            ref TerraformStage ____terraStage,
            string ___terraStageNameInPlanetData
        )
        {
            if (stackSize.Value > 1)
            {
                // TODO these below are mostly duplicated within (Cheat) Machine Deposit Into Remote Containers
                //      eventually it would be great to get it factored out in some fashion...
                var sw = Stopwatch.StartNew();
                LogMG("GenerateAnObject start " + ____worldObject.GetId() + " (" + ____worldObject.GetGroup().GetId() + ") " + ____worldObject.GetPlanetHash());

                if (____worldUnitsHandler == null)
                {
                    ____worldUnitsHandler = Managers.GetManager<WorldUnitsHandler>();
                }
                if (____worldUnitsHandler == null)
                {
                    return false;
                }
                if (____terraStage == null && !string.IsNullOrEmpty(___terraStageNameInPlanetData))
                {
                    ____terraStage = typeof(PlanetData).GetField(___terraStageNameInPlanetData).GetValue(Managers.GetManager<PlanetLoader>().GetCurrentPlanetData()) as TerraformStage;
                }


                LogMG("    begin ore search");

                Group group = null;
                if (___setGroupsDataViaLinkedGroup)
                {
                    List<Group> linkedGroups = ____worldObject.GetLinkedGroups();
                    if (linkedGroups != null && linkedGroups.Count != 0)
                    {
                        group = linkedGroups[UnityEngine.Random.Range(0, linkedGroups.Count)];
                    }
                }
                else if (___groupDatas.Count != 0 || ___groupDatasTerraStage.Count != 0)
                {
                    List<GroupData> list = [.. ___groupDatas];
                    List<Group> linkedGroups = ____worldObject.GetLinkedGroups();
                    if (linkedGroups != null)
                    {
                        foreach (var linkedGroup in linkedGroups)
                        {
                            list.Add(linkedGroup.GetGroupData());
                        }
                    }

                    var planetId = Managers.GetManager<PlanetLoader>()
                        .planetList
                        .GetPlanetFromIdHash(____worldObject.GetPlanetHash())
                        .GetPlanetId();

                    if (___groupDatasTerraStage.Count != 0 
                        && ____worldUnitsHandler.IsWorldValuesAreBetweenStages(____terraStage, null, planetId))
                    {
                        list.AddRange(___groupDatasTerraStage);
                    }
                    if (list.Count != 0)
                    {
                        group = GroupsHandler.GetGroupViaId(list[UnityEngine.Random.Range(0, list.Count)].id);
                    }
                }

                // deposit the ore

                if (group != null)
                {
                    string oreId = group.id;

                    LogMG("    ore: " + oreId);

                    var inventory = ____inventory;

                    if (inventory != null)
                    {
                        InventoriesHandler.Instance.AddItemToInventory(group, inventory, (success, id) =>
                        {
                            if (!success)
                            {
                                LogMG("GenerateAnObject: Machine " + ____worldObject.GetId() + " could not add " + oreId + " to inventory " + inventory.GetId());
                                if (id != 0)
                                {
                                    WorldObjectsHandler.Instance.DestroyWorldObject(id);
                                }
                            }
                        });
                    }
                    else
                    {
                        LogMG("    No suitable inventory found, ore ignored");
                    }
                }
                else
                {
                    LogMG("    ore: none");
                }

                LogMG("GenerateAnObject end. " + sw.Elapsed.TotalMilliseconds + " ms");
                return false;
            }
            return true;
        }

        /// <summary>
        /// Conditionally disallow stacking in Ore Extractors, Water and Atmosphere generators.
        /// </summary>
        /// <param name="__instance">The current component used to find the world object's group id</param>
        /// <param name="inventory">The inventory of the machine being set.</param>
        [HarmonyPostfix]
        [HarmonyPatch(typeof(MachineGenerator), nameof(MachineGenerator.SetGeneratorInventory))]
        static void Patch_MachineGenerator_SetGeneratorInventory(MachineGenerator __instance, Inventory inventory)
        {
            var wo = __instance.GetComponent<WorldObjectAssociated>()?.GetWorldObject();
            if (wo != null)
            {
                var gid = wo.GetGroup().id;
                if (gid.StartsWith("OreExtractor", StringComparison.Ordinal))
                {
                    if (!stackOreExtractors.Value)
                    {
                        noStackingInventories.Add(inventory.GetId());
                    }
                }
                else if (gid.StartsWith("WaterCollector", StringComparison.Ordinal))
                {
                    if (!stackWaterCollectors.Value)
                    {
                        noStackingInventories.Add(inventory.GetId());
                    }
                }
                else if (gid.StartsWith("GasExtractor", StringComparison.Ordinal))
                {
                    if (!stackGasExtractors.Value)
                    {
                        noStackingInventories.Add(inventory.GetId());
                    }
                }
                else if (gid.StartsWith("Beehive", StringComparison.Ordinal))
                {
                    if (!stackBeehives.Value)
                    {
                        noStackingInventories.Add(inventory.GetId());
                    }
                }
                else if (gid.StartsWith("Biodome", StringComparison.Ordinal))
                {
                    if (!stackBiodomes.Value)
                    {
                        noStackingInventories.Add(inventory.GetId());
                    }
                }
                else if (gid.StartsWith("HarvestingRobot", StringComparison.Ordinal))
                {
                    if (!stackHarvestingRobots.Value)
                    {
                        noStackingInventories.Add(inventory.GetId());
                    }
                }
                else if (gid.StartsWith("Ecosystem", StringComparison.Ordinal))
                {
                    if (!stackEcosystems.Value)
                    {
                        noStackingInventories.Add(inventory.GetId());
                    }
                }
            }
        }
    }
}
