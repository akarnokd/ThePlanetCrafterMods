using BepInEx;
using HarmonyLib;
using SpaceCraft;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using FeatMultiplayer.MessageTypes;

namespace FeatMultiplayer
{
    public partial class Plugin : BaseUnityPlugin
    {
        /// <summary>
        /// The vanilla game uses the CraftManager::TryToCraftInInventory to create a new world object,
        /// put it into the player's inventory and remove the crafting ingredients for it.
        /// 
        /// As a host, we don't have to do anything as craft only affects the host's backpack.
        /// 
        /// As a client, we have to ask the host to do the crafting for us as the full sync
        /// would make its existence flicker. Still pretend the craft went through but
        /// don't create the object locally.
        /// </summary>
        /// <param name="__result">Would the crafting have been successful?</param>
        /// <param name="_sourceCrafter">The crafting screen.</param>
        /// <param name="_playerController">The player object to use its backpack inventory.</param>
        /// <param name="groupItem">The item to be crafted.</param>
        /// <param name="___totalCraft">The game tracks the crafting counts in the original method.</param>
        /// <returns>True for the host, false for the client.</returns>
        [HarmonyPrefix]
        [HarmonyPatch(typeof(CraftManager), nameof(CraftManager.TryToCraftInInventory))]
        static bool CraftManager_TryToCraftInInventory(
            ref bool __result,
            ActionCrafter _sourceCrafter, 
            PlayerMainController _playerController, 
            GroupItem groupItem,
            ref int ___totalCraft)
        {
            if (updateMode == MultiplayerMode.CoopClient)
            {
                List<Group> recipe = groupItem.GetRecipe().GetIngredientsGroupInRecipe();
                Inventory inventory = _playerController.GetPlayerBackpack().GetInventory();
                bool hasAllIngredients = inventory.ContainsItems(recipe);
                bool freeCraft = Managers.GetManager<GameSettingsHandler>().GetCurrentGameSettings().GetFreeCraft();

                if (hasAllIngredients || freeCraft)
                {
                    if ((recipe.Count == 0 || freeCraft) && inventory.IsFull())
                    {
                        Managers.GetManager<BaseHudHandler>().DisplayCursorText("UI_InventoryFull", 2f, "");
                        __result = false;
                    }
                    else
                    {
                        // Client prediction: the removal will succeed, no need to notify about the removal
                        suppressInventoryChange = true;
                        try
                        {
                            inventory.RemoveItems(recipe, false, true);
                        }
                        finally
                        {
                            suppressInventoryChange = false;
                        }

                        _sourceCrafter?.CraftAnimation(groupItem);
                        ___totalCraft++;
                        __result = true;

                        LogInfo("SendMessageCraft: " + groupItem.GetId());
                        SendHost(new MessageCraft() { groupId = groupItem.GetId() }, true);
                    }
                }
                else
                {
                    __result = false;
                }

                return false;
            }
            return true;
        }

        /// <summary>
        /// The vanilla game uses CraftManager::TryToCraftInWorld to create and place into the world.
        /// 
        /// On the host, we have to override and reproduce most of this behavior to get the 
        /// created WorldObject and send it to the client.
        /// 
        /// On the cient, we have to override and reproduce most of this behavior, then ask the
        /// host to craft the object for us without creating the object locally.
        /// </summary>
        /// <param name="__result">Would the crafting have been successful?</param>
        /// <param name="_sourceCrafter">The crafting screen.</param>
        /// <param name="_playerController">The player object to use its backpack inventory.</param>
        /// <param name="groupItem">The item to be crafted.</param>
        /// <param name="___totalCraft">The game tracks the crafting counts in the original method.</param>
        /// <returns>True on the host, false on the client</returns>
        [HarmonyPrefix]
        [HarmonyPatch(typeof(CraftManager), nameof(CraftManager.TryToCraftInWorld))]
        static bool CraftManager_TryCraftInWorld(
            ref bool __result,
            ActionCrafter _sourceCrafter, 
            PlayerMainController _playerController, 
            GroupItem groupItem,
            ref int ___totalCraft)
        {
            if (updateMode != MultiplayerMode.SinglePlayer)
            {
                List<Group> recipe = groupItem.GetRecipe().GetIngredientsGroupInRecipe();
                Inventory inventory = _playerController.GetPlayerBackpack().GetInventory();
                bool hasAllIngredients = inventory.ContainsItems(recipe);
                bool freeCraft = Managers.GetManager<GameSettingsHandler>().GetCurrentGameSettings().GetFreeCraft();

                if (hasAllIngredients || freeCraft)
                {
                    _sourceCrafter?.CraftAnimation(groupItem);
                    ___totalCraft++;
                    __result = true;

                    if (updateMode == MultiplayerMode.CoopClient)
                    {
                        // Client prediction: the removal will succeed, no need to notify about the removal
                        suppressInventoryChange = true;
                        try
                        {
                            inventory.RemoveItems(recipe, false, true);
                        }
                        finally
                        {
                            suppressInventoryChange = false;
                        }

                        SendHost(new MessageCraftWorld()
                        {
                            groupId = groupItem.GetId(),
                            position = _sourceCrafter.GetSpawnPosition(),
                            craftTime = _sourceCrafter.GetCraftTime()
                        }, true);
                    }
                    else
                    {
                        var wo = WorldObjectsHandler.CreateNewWorldObject(groupItem, 0);
                        inventory.RemoveItems(recipe, true, true);

                        wo.SetPositionAndRotation(_sourceCrafter.GetSpawnPosition(), new Quaternion(0f, 0f, 0f, 0f));
                        var go = WorldObjectsHandler.InstantiateWorldObject(wo, false);
                        go.AddComponent<ShowMeAfterDelay>().SetDelay(_sourceCrafter.GetCraftTime());

                        // FIXME this won't animate on the client
                        SendWorldObjectToClients(wo, false);
                    }
                }
                else
                {
                    __result = false;
                }

                return false;
            }
            return true;
        }

        static void ReceiveMessageCraft(MessageCraft mc)
        {
            if (updateMode == MultiplayerMode.CoopHost)
            {
                Inventory inv = mc.sender.shadowBackpack;
                GroupItem gri = GroupsHandler.GetGroupViaId(mc.groupId) as GroupItem;
                var recipe = gri.GetRecipe().GetIngredientsGroupInRecipe();
                if (gri != null)
                {
                    bool hasAllIngredients = inv.ContainsItems(recipe);
                    bool freeCraft = Managers.GetManager<GameSettingsHandler>().GetCurrentGameSettings().GetFreeCraft();

                    if (hasAllIngredients || freeCraft)
                    {
                        if (TryCreateInInventoryAndNotify(gri, inv, mc.sender, out _))
                        {
                            LogInfo("ReceiveMessageCraft: " + mc.groupId + ", success = true");
                            inv.RemoveItems(recipe, true, false);
                        }
                        else
                        {
                            LogWarning("ReceiveMessageCraft: " + mc.groupId + ", success = false, reason = inventory full");
                        }
                    }
                    else
                    {
                        LogWarning("ReceiveMessageCraft: " + mc.groupId + ", success = false, reason = missing ingredients");
                    }
                } 
                else
                {
                    LogWarning("ReceiveMessageCraft: Unknown groupId or not GroupItem: " + mc.groupId);
                }
            }
        }

        static void ReceiveMessageCraftWorld(MessageCraftWorld mcw)
        {
            if (updateMode == MultiplayerMode.CoopHost)
            {
                Inventory inv = mcw.sender.shadowBackpack;
                GroupItem gri = GroupsHandler.GetGroupViaId(mcw.groupId) as GroupItem;
                var recipe = gri.GetRecipe().GetIngredientsGroupInRecipe();
                if (gri != null)
                {
                    bool hasAllIngredients = inv.ContainsItems(recipe);
                    bool freeCraft = Managers.GetManager<GameSettingsHandler>().GetCurrentGameSettings().GetFreeCraft();

                    if (hasAllIngredients || freeCraft)
                    {
                        var wo = WorldObjectsHandler.CreateNewWorldObject(gri, 0);
                        inv.RemoveItems(recipe, true, false);

                        wo.SetPositionAndRotation(mcw.position, new Quaternion(0f, 0f, 0f, 0f));
                        var go = WorldObjectsHandler.InstantiateWorldObject(wo, false);
                        go.AddComponent<ShowMeAfterDelay>().SetDelay(mcw.craftTime);

                        LogInfo("ReceiveMessageCraftWorld: " + DebugWorldObject(wo) + ", success = true");
                        // FIXME this won't animate properly on the client
                        SendWorldObjectToClients(wo, false);
                    }
                    else
                    {
                        LogWarning("ReceiveMessageCraftWorld: " + mcw.groupId + ", success = false, reason = missing ingredients");
                    }
                }
                else
                {
                    LogWarning("ReceiveMessageCraftWorld: Unknown groupId or not GroupItem: " + mcw.groupId);
                }
            }
        }
    }
}
