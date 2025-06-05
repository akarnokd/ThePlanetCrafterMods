// Copyright (c) 2022-2025, David Karnok & Contributors
// Licensed under the Apache License, Version 2.0

using BepInEx;
using SpaceCraft;
using HarmonyLib;
using TMPro;
using System.Collections.Generic;
using System;
using BepInEx.Bootstrap;

namespace UIShowPlayerTooltipItemCount
{
    [BepInPlugin("akarnokd.theplanetcraftermods.uishowplayertooltipitemcount", "(UI) Show Player Tooltip Item Count", PluginInfo.PLUGIN_VERSION)]
    [BepInDependency(modCheatCraftFromNearbyContainersGuid, BepInDependency.DependencyFlags.SoftDependency)]
    public class Plugin : BaseUnityPlugin
    {
        const string modCheatCraftFromNearbyContainersGuid = "akarnokd.theplanetcraftermods.cheatcraftfromnearbycontainers";

        static AccessTools.FieldRef<EventHoverShowGroup, Group> fEventHoverShowGroupAssociatedGroup;

        static AccessTools.FieldRef<object, List<Inventory>> fCandidateInventories;

        void Awake()
        {
            LibCommon.BepInExLoggerFix.ApplyFix();

            // Plugin startup logic
            Logger.LogInfo($"Plugin is loaded!");

            fEventHoverShowGroupAssociatedGroup = AccessTools.FieldRefAccess<EventHoverShowGroup, Group>("_associatedGroup");

            if (Chainloader.PluginInfos.TryGetValue(modCheatCraftFromNearbyContainersGuid, out var pi))
            {
                Logger.LogInfo("Mod " + modCheatCraftFromNearbyContainersGuid + " found. Considering nearby containers");

                fCandidateInventories = AccessTools.FieldRefAccess<List<Inventory>>(pi.Instance.GetType(), "candidateInventories");
            }
            else
            {
                Logger.LogInfo("Mod " + modCheatCraftFromNearbyContainersGuid + " not found.");
            }


            LibCommon.HarmonyIntegrityCheck.Check(typeof(Plugin));
            Harmony.CreateAndPatchAll(typeof(Plugin));
        }

        static readonly Dictionary<string, int> inventoryCountsCache = [];
        static readonly Dictionary<string, int> recipeCountsCache = [];

        [HarmonyPostfix]
        [HarmonyPatch(typeof(GroupInfosDisplayer), nameof(GroupInfosDisplayer.Show))]
        static void GroupInfosDisplayer_Show(
            Group group, 
            TextMeshProUGUI ___nameText,
            GroupInfosDisplayerBlocksSwitches blocksSwitches
            )
        {
            if (blocksSwitches.showDescription)
            {
                string gname = ___nameText.text;
                /*
                    Readable.GetGroupName(group);
                if (group is GroupItem gi && gi.GetUnlocksGroup() != null)
                {
                    var unlocksGroup = gi.GetUnlocksGroup();
                    if (unlocksGroup == null 
                        && unlocksGroup == ___blueprintChipGroup 
                        && worldObject != null 
                        && worldObject.GetLinkedGroups() != null 
                        && worldObject.GetLinkedGroups().Count > 0)
                    {
                        unlocksGroup = worldObject.GetLinkedGroups()[0];
                    }
                    if (unlocksGroup != null)
                    {
                        gname = Readable.GetGroupName(unlocksGroup);
                    }
                }
                */
                Inventory inventory = Managers.GetManager<PlayersManager>().GetActivePlayerController().GetPlayerBackpack().GetInventory();
                int cnt = 0;

                inventoryCountsCache.Clear();
                recipeCountsCache.Clear();

                foreach (WorldObject wo in inventory.GetInsideWorldObjects())
                {
                    var gid = wo.GetGroup().GetId();
                    if (gid == group.GetId())
                    {
                        cnt++;
                    }

                    inventoryCountsCache.TryGetValue(gid, out var itemCount);
                    inventoryCountsCache[gid] = itemCount + 1;
                }
                if (cnt > 0)
                {
                    gname += "    x " + cnt;
                }

                var ingredients = group.GetRecipe().GetIngredientsGroupInRecipe();
                if (ingredients.Count != 0)
                {
                    if (fCandidateInventories != null)
                    {
                        var candidateInvs = fCandidateInventories();
                        if (candidateInvs != null)
                        {
                            foreach (var inv in candidateInvs)
                            {
                                if (inv != null)
                                {
                                    foreach (WorldObject wo in inv.GetInsideWorldObjects())
                                    {
                                        var gid = wo.GetGroup().GetId();

                                        inventoryCountsCache.TryGetValue(gid, out var itemCount);
                                        inventoryCountsCache[gid] = itemCount + 1;
                                    }
                                }
                            }
                        }
                    }

                    foreach (var gr in ingredients)
                    {
                        recipeCountsCache.TryGetValue(gr.id, out var ingredientCount);
                        recipeCountsCache[gr.id] = ingredientCount + 1;
                    }

                    int buildableCount = int.MaxValue;
                    foreach (var entry in recipeCountsCache)
                    {
                        inventoryCountsCache.TryGetValue(entry.Key, out var haveCount);
                        buildableCount = Math.Min(buildableCount, haveCount / entry.Value);
                    }
                    if (buildableCount != int.MaxValue)
                    {
                        gname += "    < " + buildableCount + " >";
                    }
                }

                ___nameText.text = gname;
            }
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(UiWindowCraft), "Craft")]
        static void UiWindowCraft_Craft(GroupItem _group)
        {
            if (Managers.GetManager<PlayersManager>().GetActivePlayerController().GetPlayerInputDispatcher().IsPressingAccessibilityKey())
            {
                foreach (EventHoverShowGroup e in FindObjectsByType<EventHoverShowGroup>(UnityEngine.FindObjectsSortMode.None))
                {
                    Group g = fEventHoverShowGroupAssociatedGroup(e);
                    if (g != null && g.GetId() == _group.GetId())
                    {
                        e.OnImageHovered();
                    }
                }

            }
        }
    }
}
