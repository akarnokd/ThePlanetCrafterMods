using BepInEx.Bootstrap;
using BepInEx.Configuration;
using BepInEx.Logging;
using SpaceCraft;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace LibCommon
{
    /// <summary>
    /// Common routine to craft objects from backpack, equipment and/or nearby containers.
    /// </summary>
    internal class CraftHelper
    {
        /* If Craft From Containers is installed. */
        internal const string modCraftFromContainersGuid = "aedenthorn.CraftFromContainers";
        static ConfigEntry<bool> craftFromContainersEnabled;
        static ConfigEntry<bool> craftFromContainersPullFromChests;
        static ConfigEntry<float> craftFromContainersRange;

        /// <summary>
        /// Call this to initialize fields related to 3rd party mods.
        /// </summary>
        /// <param name="logger">The logger to use to report progress.</param>
        internal static void Init(ManualLogSource logger)
        {
            if (Chainloader.PluginInfos.TryGetValue(modCraftFromContainersGuid, out var pi))
            {
                logger.LogInfo(modCraftFromContainersGuid + " found, considering nearby containers");

                craftFromContainersEnabled = pi.Instance.Config["General", "Enabled"] as ConfigEntry<bool>;
                craftFromContainersPullFromChests = pi.Instance.Config["Options", "PullFromChests"] as ConfigEntry<bool>;
                craftFromContainersRange = pi.Instance.Config["Options", "Range"] as ConfigEntry<float>;
            }
            else
            {
                logger.LogInfo("[optional] " + modCraftFromContainersGuid + " not found.");
            }
        }

        /// <summary>
        /// Try crafting the specified item with the given context and call back on progress.
        /// </summary>
        /// <param name="item">The item to craft.</param>
        /// <param name="position">The position where the crafting takes place.</param>
        /// <param name="backpack">The inventory designated as the player's backpack.</param>
        /// <param name="equipment">The inventory designated as the player's equipment.</param>
        /// <param name="freeCraft">If true, not all ingredients have to be present.</param>
        /// <param name="create">If true, the item will be created and put into the backpack. If false, only the ingredients are consumed.</param>
        /// <param name="canAdd">Mandatory function to check if an inventory can hold the specified group item.</param>
        /// <param name="onWorldObjectCreated">Optional, called when the WorldObject was created but before deposited.</param>
        /// <param name="onAddEquipment">Optional, called when the item created would go into an equipment slot. 
        /// This happens if one of the ingredients was taken from the equipment inventory, indicating an in-place upgrade.
        /// Usually this will apply the equipment effect to the player.
        /// </param>
        /// <param name="onRemoveEquipment">Optional, called before the new item would go into an equipment slot, 
        /// with the list of items to be removed from the equipment.
        /// This happens if one of the ingredients was taken from the equipment inventory, indicating an in-place upgrade.
        /// Usually this will remove the equipment effects from the player.
        /// </param>
        /// <param name="displayInfo">If true, ingredients taken and inventory full messages will be displayed on the screen.</param>
        /// <returns>True if the crafting succeeded, false if no ingredients were found or the target inventory is full.</returns>
        internal static bool TryCraftInventory(
                GroupItem item,
                Vector3 position,
                Inventory backpack,
                Inventory equipment,
                bool freeCraft,
                bool create,
                Func<Inventory, Group, bool> canAdd,
                Action<WorldObject> onWorldObjectCreated,
                Action<WorldObject> onAddEquipment,
                Action<List<Group>> onRemoveEquipment,
                bool displayInfo
            )
        {

            var ingredients = item.GetRecipe().GetIngredientsGroupInRecipe();

            var inBackpack = new Dictionary<Group, int>();
            CountGroups(backpack, inBackpack);

            var inEquipment = new Dictionary<Group, int>();
            CountGroups(equipment, inEquipment);

            var inNearby = FindNearbyContainers(position);

            var fromBackpack = new List<Group>();
            var fromEquipment = new List<Group>();
            var fromNearby = new List<InventoryAndGroup>();

            var ingredientsFound = 0;

            for (int i = ingredients.Count - 1; i >= 0; i--)
            {
                var ingredient = ingredients[i];

                if (inBackpack.TryGetValue(ingredient, out var backpackCnt) && backpackCnt > 0) {
                    fromBackpack.Add(ingredient);
                    inBackpack[ingredient] = backpackCnt - 1;

                    ingredientsFound++;
                }
                else if (inEquipment.TryGetValue(ingredient, out var equipCnt) && equipCnt > 0)
                {
                    fromEquipment.Add(ingredient);
                    inEquipment[ingredient] = equipCnt - 1;

                    ingredientsFound++;
                }
                else
                {
                    foreach (var nearby in inNearby)
                    {
                        if (nearby.groupAndCount.TryGetValue(ingredient, out var cnt) && cnt > 0)
                        {
                            fromNearby.Add(new InventoryAndGroup()
                            {
                                inventory = nearby.inventory,
                                group = ingredient
                            });
                            nearby.groupAndCount[ingredient] = cnt - 1;

                            ingredientsFound++;

                            break;
                        }
                    }
                }
            }

            if (ingredients.Count == ingredientsFound || freeCraft)
            {
                // in freeCraft, we need to check if the target inventory has room for such an item
                // if we don't consume from either the backpack or equipment, there might be no room so check for room too
                if ((fromEquipment.Count == 0 && fromBackpack.Count == 0) || freeCraft)
                {
                    if (!canAdd(backpack, item))
                    {
                        if (displayInfo)
                        {
                            Managers.GetManager<BaseHudHandler>()?.DisplayCursorText("UI_InventoryFull", 2f, "");
                        }
                        return false;
                    }
                }

                backpack.RemoveItems(fromBackpack, true, displayInfo);
                foreach (var nb in fromNearby)
                {
                    nb.inventory.RemoveItems(new() { nb.group }, true, displayInfo);
                }

                if (create)
                {
                    var wo = WorldObjectsHandler.CreateNewWorldObject(item);
                    onWorldObjectCreated?.Invoke(wo);

                    if (fromEquipment.Count != 0)
                    {
                        onRemoveEquipment?.Invoke(fromEquipment);

                        onAddEquipment?.Invoke(wo);
                    }
                    else
                    {
                        backpack.AddItem(wo);
                    }
                }
                else
                {
                    equipment.RemoveItems(fromEquipment, true, displayInfo);
                }
                return true;
            }

            return false;
        }

        static void CountGroups(Inventory inv, Dictionary<Group, int> groupAndCount)
        {
            foreach (var wo in inv.GetInsideWorldObjects())
            {
                var id = wo.GetGroup();
                groupAndCount.TryGetValue(id, out var cnt);
                groupAndCount[id] = cnt + 1;
            }
        }

        static List<InventoryAndGroupAndCount> FindNearbyContainers(Vector3 position)
        {
            float range = -1f;
            bool includeChests = false;

            if (craftFromContainersEnabled?.Value ?? false)
            {
                range = craftFromContainersRange?.Value ?? -1f;
                includeChests = craftFromContainersPullFromChests?.Value ?? false;
            }

            List<InventoryAndGroupAndCount> result = new();
            if (range >= 0)
            {
                foreach (var wo in WorldObjectsHandler.GetConstructedWorldObjects())
                {
                    if (wo.GetGroup().GetId().StartsWith("Container") && wo.GetLinkedInventoryId() > 0)
                    {
                        var inv = InventoriesHandler.GetInventoryById(wo.GetLinkedInventoryId());
                        if (inv != null)
                        {
                            var dist = Vector3.Distance(wo.GetPosition(), position);
                            if (dist <= range)
                            {
                                var dict = new Dictionary<Group, int>();

                                CountGroups(inv, dict);

                                result.Add(new()
                                {
                                    inventory = inv,
                                    groupAndCount = dict
                                });
                            }
                        }
                    }
                }

                if (includeChests)
                {
                    foreach (var sceneInv in UnityEngine.Object.FindObjectsByType<InventoryFromScene>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
                    {
                        var inv = InventoriesHandler.GetInventoryById(sceneInv.GetInventoryGeneratedId());
                        if (inv != null)
                        {
                            var dist = Vector3.Distance(sceneInv.transform.position, position);
                            if (dist <= range)
                            {
                                var dict = new Dictionary<Group, int>();

                                CountGroups(inv, dict);

                                result.Add(new()
                                {
                                    inventory = inv,
                                    groupAndCount = dict
                                });
                            }
                        }
                    }
                }
            }
            return result;
        }

        class InventoryAndGroupAndCount
        {
            internal Inventory inventory;
            internal Dictionary<Group, int> groupAndCount;
        }

        class InventoryAndGroup
        {
            internal Inventory inventory;
            internal Group group;
        }
    }
}
