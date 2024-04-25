// Copyright (c) 2022-2024, David Karnok & Contributors
// Licensed under the Apache License, Version 2.0

using HarmonyLib;
using SpaceCraft;
using System.Collections.Generic;
using UnityEngine;

namespace CheatInventoryStacking
{
    public partial class Plugin
    {
        /// <summary>
        /// Vanilla creates the object in the player's backpack
        /// or replaces the equipment inplace.
        /// It calls Inventory::IsFull 0 to 2 times so we can't sneak in the groupId.
        /// </summary>
        [HarmonyPrefix]
        [HarmonyPatch(typeof(CraftManager), nameof(CraftManager.TryToCraftInInventory))]
        static bool Patch_CraftManager_TryToCraftInInventory(
            GroupItem groupItem,
            PlayerMainController playerController,
            ActionCrafter sourceCrafter,
            ref bool ____crafting,
            int ____tempSpaceInInventory,
            ref bool __result
        )
        {
            if (stackSize.Value > 1 && !apiTryToCraftInInventoryHandled())
            {
                var ingredients = groupItem.GetRecipe().GetIngredientsGroupInRecipe();
                var backpack = playerController.GetPlayerBackpack();
                var backpackInventory = backpack.GetInventory();
                var equipment = playerController.GetPlayerEquipment();
                var equipmentInventory = equipment.GetInventory();

                var isFreeCraft = Managers.GetManager<GameSettingsHandler>().GetCurrentGameSettings().GetFreeCraft();

                var useFromEquipment = new List<Group>();

                // Should allow Craft From Containers to work on its own by overriding ItemsContainsStatus.
                var availableBackpack = backpackInventory.ItemsContainsStatus(ingredients);

                // due to Craft From Containers tricking ItemsContainsStatus
                // we need to double check which ingredient would be taken from the backpack
                // so that the fullness check can work correctly.
                var toRemoveFromBackpack = new HashSet<int>();
                var ingredientsCopy = new List<Group>(ingredients);

                foreach (var wo in backpackInventory.GetInsideWorldObjects())
                {
                    for (int i = 0; i < ingredientsCopy.Count; i++)
                    {
                        Group ingr = ingredientsCopy[i];
                        if (wo.GetGroup() == ingr)
                        {
                            toRemoveFromBackpack.Add(wo.GetId());
                            ingredientsCopy.RemoveAt(i);
                            break;
                        }
                    }
                    if (ingredientsCopy.Count == 0)
                    {
                        break;
                    }
                }

                if (availableBackpack.Contains(item: false))
                {
                    var availableEquipment = equipmentInventory.ItemsContainsStatus(ingredients);

                    for (int i = 0; i < availableEquipment.Count; i++)
                    {
                        if (!availableBackpack[i] && availableEquipment[i])
                        {
                            availableBackpack[i] = true;
                            useFromEquipment.Add(ingredients[i]);
                        }
                    }
                }
                if (!availableBackpack.Contains(item: false) || isFreeCraft)
                {

                    if (IsFullStackedWithRemoveOfInventory(backpackInventory, toRemoveFromBackpack, groupItem.id))
                    {
                        Managers.GetManager<BaseHudHandler>().DisplayCursorText("UI_InventoryFull", 2f);
                        __result = false;
                        return false;
                    }

                    ____crafting = true;

                    sourceCrafter?.CraftAnimation(groupItem);

                    if (useFromEquipment.Count != 0)
                    {
                        InventoriesHandler.Instance.SetInventorySize(equipmentInventory, ____tempSpaceInInventory, Vector3.zero);
                        InventoriesHandler.Instance.SetInventorySize(backpackInventory, ____tempSpaceInInventory, Vector3.zero);
                    }

                    InventoriesHandler.Instance.RemoveItemsFromInventory(ingredients, backpackInventory, true, true);

                    equipment.DestroyItemsFromEquipment(useFromEquipment, success =>
                    {
                        InventoriesHandler.Instance.AddItemToInventory(groupItem, backpackInventory, (success, id) =>
                        {
                            fCraftManagerCrafting(null) = false;

                            if (!success)
                            {
                                if (id > 0)
                                {
                                    WorldObjectsHandler.Instance.DestroyWorldObject(id);
                                }
                                RestoreInventorySizes(backpackInventory, equipment,
                                    -____tempSpaceInInventory, useFromEquipment.Count != 0);
                            }
                            else
                            {
                                if (useFromEquipment.Count != 0 && groupItem.GetEquipableType() != DataConfig.EquipableType.Null)
                                {
                                    equipment.WatchForModifications(enabled: true);
                                    var wo = WorldObjectsHandler.Instance.GetWorldObjectViaId(id);

                                    InventoriesHandler.Instance.TransferItem(backpackInventory, equipmentInventory,
                                        wo, _ => RestoreInventorySizes(backpackInventory, equipment,
                                                    -____tempSpaceInInventory, useFromEquipment.Count != 0)
                                    );
                                }
                                else
                                {
                                    RestoreInventorySizes(backpackInventory, equipment,
                                        -____tempSpaceInInventory, useFromEquipment.Count != 0);
                                }
                            }

                        });
                    });

                    CraftManager.AddOneToTotalCraft();
                    __result = true;
                    return false;
                }
                __result = false;
                return false;
            }
            return true;
        }

        static void RestoreInventorySizes(Inventory backpack, PlayerEquipment equipment, int delta, bool equipmentUsed)
        {
            if (equipmentUsed)
            {
                equipment.WatchForModifications(false);
                InventoriesHandler.Instance.SetInventorySize(equipment.GetInventory(), delta, Vector3.zero);
                InventoriesHandler.Instance.SetInventorySize(backpack, delta, Vector3.zero);
            }
        }
    }
}
