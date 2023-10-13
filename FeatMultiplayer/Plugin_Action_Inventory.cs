using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using SpaceCraft;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;
using FeatMultiplayer.MessageTypes;

namespace FeatMultiplayer
{
    public partial class Plugin : BaseUnityPlugin
    {
        /// <summary>
        /// If true, operations affecting the inventory won't send out messages,
        /// avoiding message ping-pong between the parties.
        /// </summary>
        static bool suppressInventoryChange;
        /// <summary>
        /// If the CheatInventoryStacking is installed, consider the stack counts when displaying information.
        /// </summary>
        static Func<List<WorldObject>, int> getStackCount;

        static ConfigEntry<int> stackSize;

        /// <summary>
        /// The vanilla game calls Inventory::AddItem all over the place to try to add to the player's backpack,
        /// equipment, chests, machines, etc. The backpack is at id 1, the equipment is at id 2.
        /// 
        /// On the host, updates to anything but its backpack and inventory is synced back to the client.
        /// The shadow backpack and shadow equipment is renamed to 1 and 2.
        /// </summary>
        /// <param name="__result">Was the original addition successful?</param>
        /// <param name="___worldObjectsInInventory">The full inventory list</param>
        /// <param name="___inventoryId">The id of the inventory being manipulated</param>
        /// <param name="_worldObject">What was added</param>
        [HarmonyPostfix]
        [HarmonyPatch(typeof(Inventory), nameof(Inventory.AddItem))]
        static void Inventory_AddItem_Post(bool __result, List<WorldObject> ___worldObjectsInInventory,
            int ___inventoryId, WorldObject _worldObject)
        {
            //LogInfo("InventoryAddItem: " + ___inventoryId + ", " + _worldObject.GetGroup().GetId() + ", Count = " + ___worldObjectsInInventory.Count + ", result = " + __result);
            if (__result && updateMode != MultiplayerMode.SinglePlayer && !suppressInventoryChange)
            {
                int iid = ___inventoryId;

                // except the host's own inventory
                if (updateMode == MultiplayerMode.CoopHost
                    && (iid == 1 || iid == 2))
                {
                    return;
                }

                var mia = new MessageInventoryAdded()
                {
                    inventoryId = iid, // we will override this below if necessary
                    itemId = _worldObject.GetId(),
                    groupId = _worldObject.GetGroup().GetId()
                };

                if (updateMode == MultiplayerMode.CoopHost)
                {
                    // is the target one of the shadow inventories?
                    foreach (var cc in _clientConnections.Values)
                    {
                        if (cc.shadowBackpack != null && cc.shadowBackpack.GetId() == iid)
                        {
                            mia.inventoryId = 1;

                            LogInfo("InventoryAddItem: Send [" + cc.clientName + "] = " + iid + " (shadow backpack), " + _worldObject.GetId() + ", " + _worldObject.GetGroup().GetId());
                            cc.Send(mia);
                            cc.Signal();
                            return;
                        }
                        else if (cc.shadowEquipment != null && cc.shadowEquipment.GetId() == iid)
                        {
                            mia.inventoryId = 2;

                            LogInfo("InventoryAddItem: Send [" + cc.clientName + "] = " + iid + " (shadow equipment), " + _worldObject.GetId() + ", " + _worldObject.GetGroup().GetId());
                            cc.Send(mia);
                            cc.Signal();
                            return;
                        }
                    }
                    // looks like a regular world inventory
                    // let all of them known
                    LogInfo("InventoryAddItem: Send [*] = " + iid + ", " + _worldObject.GetId() + ", " + _worldObject.GetGroup().GetId());
                    SendAllClients(mia, true);
                } 
                else
                {
                    LogInfo("InventoryAddItem: Send [host] = " + iid + ", " + _worldObject.GetId() + ", " + _worldObject.GetGroup().GetId());
                    SendHost(mia, true);
                }
            }
        }

        /// <summary>
        /// The vanilla game calls Inventory::RemoveItem all over the place to remove items from inventories.
        /// 
        /// On the host, we let the original method run and then notify the cleint about the inventory change.
        /// The shadow backpack and shadow equipment is renamed to 1 and 2.
        /// 
        /// On the client, we do nothing additional to what <see cref="Inventory_RemoveItem(int, WorldObject, bool)"/> does.
        /// </summary>
        /// <param name="___inventoryId">The inventory id where the object would be removed</param>
        /// <param name="_worldObject">The world object to remove</param>
        /// <param name="_destroyWorldObject">If true, the world object is destroyed, such as when it was consumed or used for building.</param>
        [HarmonyPostfix]
        [HarmonyPatch(typeof(Inventory), nameof(Inventory.RemoveItem))]
        static void Inventory_RemoveItem(int ___inventoryId, WorldObject _worldObject, bool _destroyWorldObject)
        {
            if (updateMode != MultiplayerMode.SinglePlayer && !suppressInventoryChange)
            {
                int iid = ___inventoryId;

                // except the host's own inventory
                if (updateMode == MultiplayerMode.CoopHost
                    && (iid == 1 || iid == 2))
                {
                    return;
                }

                var mir = new MessageInventoryRemoved()
                {
                    inventoryId = iid, // we will override this below
                    itemId = _worldObject.GetId(),
                    destroy = _destroyWorldObject
                };

                if (updateMode == MultiplayerMode.CoopHost)
                {
                    // is the target one of the shadow inventories?
                    foreach (var cc in _clientConnections.Values)
                    {
                        if (cc.shadowBackpack.GetId() == iid)
                        {
                            mir.inventoryId = 1;

                            LogInfo("InventoryRemoveItem: Send [" + cc.clientName + "] = " + iid + " (shadow backpack), " + _worldObject.GetId() + ", " + _worldObject.GetGroup().GetId());
                            cc.Send(mir);
                            cc.Signal();
                            return;
                        }
                        else if (cc.shadowEquipment.GetId() == iid)
                        {
                            mir.inventoryId = 2;

                            LogInfo("InventoryRemoveItem: Send [" + cc.clientName + "] = " + iid + " (shadow equipment), " + _worldObject.GetId() + ", " + _worldObject.GetGroup().GetId());
                            cc.Send(mir);
                            cc.Signal();
                            return;
                        }
                    }
                    // looks like a regular world inventory
                    // let all of them known
                    LogInfo("InventoryRemoveItem: Send [*] = " + iid + ", " + _worldObject.GetId() + ", " + _worldObject.GetGroup().GetId());
                    SendAllClients(mir, true);
                }
                else
                {
                    LogInfo("InventoryRemoveItem: Send [host] = " + iid + ", " + _worldObject.GetId() + ", " + _worldObject.GetGroup().GetId());
                    SendHost(mir, true);
                }
            }
        }

        /// <summary>
        /// Called by the Inventory_AutoSort method to allow custom sorting options.
        /// </summary>
        public static Action<List<WorldObject>> inventoryAutoSortOverride;

        /// <summary>
        /// The vanilla game uses Inventory::AutoSort when the user clicks on the sort button in the inventory screen.
        /// 
        /// On the host, the shadow backpack and shadow equipment is renamed to 1 and 2.
        ///
        /// On both sides, we have to sort ourselves because the underlying inventoryDisplayer is
        /// null for inventories not shown on the other side.
        /// 
        /// This avoids forgetting the sort if the client rejoins or the host changes something in the inventory.
        /// </summary>
        /// <param name="___inventoryId">The target inventory id</param>
        [HarmonyPrefix]
        [HarmonyPatch(typeof(Inventory), nameof(Inventory.AutoSort))]
        static bool Inventory_AutoSort(int ___inventoryId, 
            List<WorldObject> ___worldObjectsInInventory,
            InventoryDisplayer ___inventoryDisplayer)
        {
            if (updateMode == MultiplayerMode.SinglePlayer)
            {
                return true;
            }
            if (inventoryAutoSortOverride != null)
            {
                inventoryAutoSortOverride(___worldObjectsInInventory);
            }
            else
            {
                ___worldObjectsInInventory.Sort((a, b) => a.GetGroup().GetId().CompareTo(b.GetGroup().GetId()));
            }
            ___inventoryDisplayer?.RefreshContent();
            if (!suppressInventoryChange)
            {

                int iid = ___inventoryId;

                // except the host's own inventory
                if (updateMode == MultiplayerMode.CoopHost &&
                    (iid == 1 || iid == 2))
                {
                    return false;
                }
                var msi = new MessageSortInventory()
                {
                    inventoryId = iid
                };

                if (updateMode == MultiplayerMode.CoopHost) 
                {
                    // is the target one of the shadow inventories?
                    foreach (var cc in _clientConnections.Values)
                    {
                        if (cc.shadowBackpack.GetId() == iid)
                        {
                            msi.inventoryId = 1;

                            cc.Send(msi);
                            cc.Signal();
                            return false;
                        }
                        else if (cc.shadowEquipment.GetId() == iid)
                        {
                            msi.inventoryId = 2;

                            cc.Send(msi);
                            cc.Signal();
                            return false;
                        }
                    }
                    // looks like a regular world inventory
                    // let all of them known
                    SendAllClients(msi, true);
                }
                else
                {
                    SendHost(msi, true);
                }
            }

            return false;
        }

        /// <summary>
        /// The vanilla game calls PlayerEquipment::UpdateAfterEquipmentChange to apply
        /// the equipment effect added/removed by the player.
        /// 
        /// On the host, we let it happen.
        /// 
        /// On the client, we prevent it from running so the host-sync messages about
        /// adding and removing can apply the item.
        /// </summary>
        /// <returns></returns>
        [HarmonyPrefix]
        [HarmonyPatch(typeof(PlayerEquipment), "UpdateAfterEquipmentChange")]
        static bool PlayerEquipment_UpdateAfterEquipmentChange()
        {
            return updateMode != MultiplayerMode.CoopClient;
        }

        static void ClientConsumeRecipe(GroupConstructible gc, Inventory inv)
        {
            var toRemove = new List<Group>() { gc };
            if (inv.ContainsItems(toRemove))
            {
                inv.RemoveItems(toRemove, true, false);
            }
            else
            {
                toRemove.Clear();
                toRemove.AddRange(gc.GetRecipe().GetIngredientsGroupInRecipe());
                inv.RemoveItems(toRemove, true, false);
            }
        }
        
        static void DispatchInventoryChange<T>(T mc, Action<T, Inventory> ifEquipment, Action<T, Inventory> otherwise) where T : MessageInventoryChanged
        {
            Inventory targetInventory = null;
            if (updateMode == MultiplayerMode.CoopHost)
            {
                if (mc.inventoryId == 1)
                {
                    targetInventory = mc.sender.shadowBackpack;
                }
                else if (mc.inventoryId == 2)
                {
                    targetInventory = mc.sender.shadowEquipment;
                }
                else
                {
                    foreach (var cc in _clientConnections.Values)
                    {
                        var inv = cc.shadowBackpack;
                        if (inv != null && inv.GetId() == mc.inventoryId)
                        {
                            LogWarning("Trying to access the shadow backpack of " + cc.clientName + " - " + mc.inventoryId);
                            return;
                        }
                        inv = cc.shadowEquipment;
                        if (inv != null && inv.GetId() == mc.inventoryId)
                        {
                            LogWarning("Trying to access a shadow equipment of " + cc.clientName + " - " + mc.inventoryId);
                            return;
                        }
                    }
                    targetInventory = InventoriesHandler.GetInventoryById(mc.inventoryId);
                }
            }
            else
            {
                if (mc.inventoryId == 1)
                {
                    targetInventory = GetPlayerMainController().GetPlayerBackpack().GetInventory();
                }
                else if (mc.inventoryId == 2)
                {
                    targetInventory = GetPlayerMainController().GetPlayerEquipment().GetInventory();
                    // on the client, apply the equipment and quit
                    ifEquipment(mc, targetInventory);
                    return;
                }
                else
                {
                    targetInventory = InventoriesHandler.GetInventoryById(mc.inventoryId);
                }
            }

            otherwise(mc, targetInventory);
        }

        static void ReceiveMessageEquipmentAdded(MessageInventoryAdded mia, Inventory targetInventory)
        {
            // on the client, apply the equipment added and quit
            suppressInventoryChange = true;
            try
            {
                WorldObject wo = WorldObjectsHandler.GetWorldObjectViaId(mia.itemId);
                if (wo != null)
                {
                    if (!InventoryContainsId(targetInventory, wo.GetId()))
                    {
                        targetInventory.AddItem(wo);
                    }
                    TryApplyEquipment(wo, GetPlayerMainController(), null);

                    LogInfo("ReceiveMessageInventoryAdded: Add Equipment " + mia.itemId + ", " + mia.groupId);

                    targetInventory.RefreshDisplayerContent();
                }
                else
                {
                    LogError("ReceiveMessageInventoryAdded: Add Equipment missing WorldObject " + mia.itemId + ", " + mia.groupId);
                }
            }
            finally
            {
                suppressInventoryChange = false;
            }
        }

        static void ReceiveMessageOtherInventoryAdded(MessageInventoryAdded mia, Inventory targetInventory)
        {
            if (targetInventory != null)
            {
                WorldObject wo = WorldObjectsHandler.GetWorldObjectViaId(mia.itemId);
                if (wo == null && WorldObjectsIdHandler.IsWorldObjectFromScene(mia.itemId))
                {
                    wo = WorldObjectsHandler.CreateNewWorldObject(GroupsHandler.GetGroupViaId(mia.groupId), mia.itemId);
                }
                if (wo != null)
                {
                    suppressInventoryChange = updateMode == MultiplayerMode.CoopClient;
                    try
                    {
                        if (!InventoryContainsId(targetInventory, wo.GetId()))
                        {
                            targetInventory.AddItem(wo);
                            LogInfo("ReceiveMessageOtherInventoryAdded: " + mia.inventoryId + " <= " + mia.itemId + ", " + mia.groupId);
                            targetInventory.RefreshDisplayerContent();
                        }
                    }
                    finally
                    {
                        suppressInventoryChange = false;
                    }
                    if (inventorySpawning.Remove(targetInventory.GetId()))
                    {
                        LogInfo("ReceiveMessageOtherInventoryAdded: Clearing inventorySpawning marker " + mia.inventoryId);
                    }
                }
                else
                {
                    LogWarning("ReceiveMessageOtherInventoryAdded: Uknown WorldObject " + mia.inventoryId + " <= " + mia.itemId + ", " + mia.groupId);
                }
            }
            else
            {
                LogWarning("ReceiveMessageOtherInventoryAdded: Uknown inventory " + mia.inventoryId + " <= " + mia.itemId + ", " + mia.groupId);
            }
        }

        static void ReceiveMessageInventoryAdded(MessageInventoryAdded mia)
        {
            DispatchInventoryChange(mia, ReceiveMessageEquipmentAdded, ReceiveMessageOtherInventoryAdded);
        }

        static bool InventoryContainsId(Inventory inv, int id)
        {
            foreach (WorldObject wo in inv.GetInsideWorldObjects())
            {
                if (wo.GetId() == id)
                {
                    return true;
                }
            }
            return false;
        }

        static void ReceiveMessageEquipmentRemoved(MessageInventoryRemoved mir, Inventory targetInventory)
        {
            suppressInventoryChange = true;
            try
            {
                WorldObject wo = WorldObjectsHandler.GetWorldObjectViaId(mir.itemId);
                if (wo != null)
                {
                    targetInventory.RemoveItem(wo);

                    // if unequipping causes further inventory change, let it happen
                    suppressInventoryChange = false;

                    UnapplyUsingList(GetPlayerMainController(), targetInventory.GetInsideWorldObjects());

                    LogInfo("ReceiveMessageInventoryRemoved: Remove Equipment " + mir.itemId + ", " + wo.GetGroup().GetId());
                }
                else
                {
                    LogError("ReceiveMessageInventoryRemoved: Remove Equipment missing WorldObject " + mir.itemId);
                }
            }
            finally
            {
                suppressInventoryChange = false;
            }
        }

        static void ReceiveMessageOtherInventoryRemoved(MessageInventoryRemoved mir, Inventory targetInventory)
        {
            if (targetInventory != null)
            {
                WorldObject wo = WorldObjectsHandler.GetWorldObjectViaId(mir.itemId);
                if (wo != null)
                {
                    suppressInventoryChange = updateMode == MultiplayerMode.CoopClient;
                    try
                    {
                        targetInventory.RemoveItem(wo, mir.destroy);
                        LogInfo("ReceiveMessageOtherInventoryRemoved: " + mir.inventoryId + " <= " + mir.itemId + ", " + wo.GetGroup().GetId());
                    }
                    finally
                    {
                        suppressInventoryChange = false;
                    }
                }
                else
                {
                    LogWarning("ReceiveMessageOtherInventoryRemoved: Unknown item " + mir.inventoryId + " <= " + mir.itemId);
                }
            }
            else
            {
                LogWarning("ReceiveMessageOtherInventoryRemoved: Unknown inventory " + mir.inventoryId + " <= " + mir.itemId);
            }
        }

        static void ReceiveMessageInventoryRemoved(MessageInventoryRemoved mir)
        {
            DispatchInventoryChange(mir, ReceiveMessageEquipmentRemoved, ReceiveMessageOtherInventoryRemoved);
        }

        static void ReceiveMessageInventories(MessageInventories minv)
        {
            if (updateMode == MultiplayerMode.CoopClient)
            {
                //LogInfo("Received all inventories");

                HashSet<int> toDelete = new HashSet<int>();
                Dictionary<int, Inventory> localInventories = new Dictionary<int, Inventory>();
                foreach (Inventory inv in InventoriesHandler.GetAllInventories())
                {
                    int id = inv.GetId();
                    if (!localInventories.ContainsKey(id))
                    {
                        localInventories[id] = inv;
                        toDelete.Add(id);
                    }
                }

                suppressInventoryChange = true;
                try
                {
                    foreach (WorldInventory wi in minv.inventories)
                    {
                        toDelete.Remove(wi.id);
                        localInventories.TryGetValue(wi.id, out var inv);
                        if (inv == null)
                        {
                            //LogInfo("ReceiveMessageInventories: Creating new inventory " + wi.id + " of size " + wi.size);
                            inv = InventoriesHandler.CreateNewInventory(wi.size, wi.id);
                        }
                        else
                        {
                            // Don't resize the player's inventory/equipment
                            if (wi.id > 2)
                            {
                                inv.SetSize(wi.size);
                            }
                            //LogInfo("ReceiveMessageInventories: Updating inventory " + wi.id + ", " + wi.size + ", " + wi.itemIds.Count);
                        }

                        SyncInventory(inv, wi);
                    }
                }
                finally
                {
                    suppressInventoryChange = false;
                }

                List<Inventory> inventories = InventoriesHandler.GetAllInventories();
                for (int i = inventories.Count - 1; i >= 0; i--)
                {
                    Inventory inv = inventories[i];
                    if (toDelete.Contains(inv.GetId()))
                    {
                        //LogInfo("ReceiveMessageInventories: Removing inventory " + inv.GetId());
                        inventories.RemoveAt(i);
                    }
                }

                //LogInfo("Received all inventories - Done");
            }
        }

        static void SyncInventory(Inventory inv, WorldInventory wi)
        {
            List<WorldObject> worldObjects = inv.GetInsideWorldObjects();

            // see if there was an inventory composition change
            HashSet<int> currentIds = new HashSet<int>();
            foreach (WorldObject obj in worldObjects)
            {
                currentIds.Add(obj.GetId());
            }
            HashSet<int> newIds = new HashSet<int>();
            foreach (int id in wi.itemIds)
            {
                newIds.Add(id);
            }

            bool changed = false;
            bool once = true;
            for (int i = worldObjects.Count - 1; i >= 0; i--)
            {
                var woi = worldObjects[i];
                int id = woi.GetId();
                if (!newIds.Contains(id))
                {
                    worldObjects.RemoveAt(i);
                    inv.inventoryContentModified?.Invoke(woi, false);
                    currentIds.Remove(id);
                    if (once)
                    {
                        once = false;
                        LogInfo("ReceiveMessageInventories: " + inv.GetId() + " changed");
                    }
                    LogInfo("ReceiveMessageInventories:   Removed " + DebugWorldObject(woi));
                    changed = true;
                }
            }
            foreach (int id in wi.itemIds)
            {
                if (!currentIds.Contains(id))
                {
                    if (worldObjectById.TryGetValue(id, out var wo))
                    {
                        worldObjects.Add(wo);
                        inv.inventoryContentModified?.Invoke(wo, true);
                        if (once)
                        {
                            once = false;
                            LogInfo("ReceiveMessageInventories: " + inv.GetId() + " changed");
                        }
                        LogInfo("ReceiveMessageInventories:   Added " + DebugWorldObject(wo));
                        changed = true;
                    }
                }
            }

            UpdateLogisticEntityFromMessage(inv, wi.demandGroups, wi.supplyGroups, wi.priority);

            if (changed)
            {
                if (wi.id == 2)
                {
                    var _playerController = GetPlayerMainController();
                    // Reset some equipment stats:

                    // Apply equipment stats
                    HashSet<DataConfig.EquipableType> equipTypes = new HashSet<DataConfig.EquipableType>();
                    foreach (WorldObject wo in worldObjects)
                    {
                        TryApplyEquipment(wo, _playerController, equipTypes);
                    }

                    // re-enable inventory sync in case of backpack/equipment capacity change
                    bool suppr = suppressInventoryChange;
                    suppressInventoryChange = false;
                    try
                    {
                        UnapplyEquipment(equipTypes, _playerController);
                    }
                    finally
                    {
                        suppressInventoryChange = suppr;
                    }
                }
                inv.RefreshDisplayerContent();
            }
        }

        /// <summary>
        /// Since we don't go the usual way of equipping and unequipping equimpent when the client joins
        /// or manipulates it equipments, we need to apply the equipment effects manually.
        /// </summary>
        /// <param name="wo">The equipment object</param>
        /// <param name="_playerController">The player's object</param>
        /// <param name="equippables">Output as to what was equipped. Used for disabling certain equipment effects.</param>
        static void TryApplyEquipment(WorldObject wo,
            PlayerMainController _playerController,
            ICollection<DataConfig.EquipableType> equippables)
        {
            if (wo.GetGroup() is GroupItem groupItem)
            {
                var equipType = groupItem.GetEquipableType();
                equippables?.Add(equipType);
                switch (equipType)
                {
                    case DataConfig.EquipableType.BackpackIncrease:
                        {
                            _playerController.GetPlayerBackpack()
                                .GetInventory()
                                .SetSize(12 + groupItem.GetGroupValue());
                            break;
                        }
                    case DataConfig.EquipableType.EquipmentIncrease:
                        {
                            _playerController.GetPlayerEquipment()
                                .GetInventory()
                                .SetSize(4 + groupItem.GetGroupValue());
                            break;
                        }
                    case DataConfig.EquipableType.OxygenTank:
                        {
                            _playerController.GetPlayerGaugesHandler()
                                .UpdateGaugesDependingOnEquipment(
                                    _playerController.GetPlayerEquipment()
                                    .GetInventory()
                                );
                            break;
                        }
                    case DataConfig.EquipableType.MultiToolMineSpeed:
                        {
                            _playerController.GetMultitool()
                                .GetMultiToolMine()
                                .SetMineTimeReducer(groupItem.GetGroupValue());
                            break;
                        }
                    case DataConfig.EquipableType.BootsSpeed:
                        {
                            _playerController.GetPlayerMovable()
                                .SetMoveSpeedChangePercentage((float)groupItem.GetGroupValue());
                            break;
                        }
                    case DataConfig.EquipableType.Jetpack:
                        {
                            _playerController.GetPlayerMovable()
                                .SetJetpackFactor((float)groupItem.GetGroupValue() / 100f);
                            break;
                        }
                    case DataConfig.EquipableType.MultiToolLight:
                        {
                            _playerController.GetMultitool()
                                .SetCanUseLight(true, groupItem.GetGroupValue());
                            break;
                        }

                    case DataConfig.EquipableType.MultiToolDeconstruct:
                        {
                            _playerController.GetMultitool().AddEnabledState(DataConfig.MultiToolState.Deconstruct);

                            if (groupItem.GetGroupValue() == 2)
                            {
                                playerEquipmentHasDeconstructT2.SetValue(_playerController.GetPlayerEquipment(), true);
                            }

                            break;
                        }
                    case DataConfig.EquipableType.MultiToolBuild:
                        {
                            _playerController.GetMultitool().AddEnabledState(DataConfig.MultiToolState.Build);
                            break;
                        }
                    case DataConfig.EquipableType.CompassHUD:
                        {
                            playerEquipmentHasCompassChip.SetValue(_playerController.GetPlayerEquipment(), true);
                            Managers.GetManager<CanvasCompass>().SetStatus(true);
                            break;
                        }
                    case DataConfig.EquipableType.AirFilter:
                        {
                            _playerController.GetGaugesHandler().SetHasRebreather(_playerController.GetPlayerEquipment()
                                    .GetInventory());
                            break;
                        }
                    case DataConfig.EquipableType.MultiToolCleanConstruction:
                        {
                            playerEquipmentHasCleanConstructionChip.SetValue(
                                _playerController.GetPlayerEquipment(), true);
                            break;
                        }
                    case DataConfig.EquipableType.MapChip:
                        {
                            // Since 0.9.002: the field is not public
                            playerEquipmentHasMapChip.SetValue(_playerController.GetPlayerEquipment(), true);
                            break;
                        }
                }
            }
        }

        static void UnapplyEquipment(HashSet<DataConfig.EquipableType> equipTypes, PlayerMainController player)
        {
            var mt = player.GetMultitool();
            // unequip
            if (!equipTypes.Contains(DataConfig.EquipableType.MultiToolBuild))
            {
                if (mt.HasEnabledState(DataConfig.MultiToolState.Build))
                {
                    mt.RemoveEnabledState(DataConfig.MultiToolState.Build);
                }
            }
            if (!equipTypes.Contains(DataConfig.EquipableType.MultiToolDeconstruct))
            {
                if (mt.HasEnabledState(DataConfig.MultiToolState.Deconstruct))
                {
                    mt.RemoveEnabledState(DataConfig.MultiToolState.Deconstruct);
                }
                // FIXME may need to be conditional???
                playerEquipmentHasDeconstructT2.SetValue(player.GetPlayerEquipment(), false);
            }
            if (!equipTypes.Contains(DataConfig.EquipableType.MultiToolLight))
            {
                if ((bool)playerMultitoolCanUseLight.GetValue(mt))
                {
                    mt.SetCanUseLight(false, 1);
                }
            }
            if (!equipTypes.Contains(DataConfig.EquipableType.CompassHUD))
            {
                playerEquipmentHasCompassChip.SetValue(player.GetPlayerEquipment(), false);
                var cc = Managers.GetManager<CanvasCompass>();
                cc.SetStatus(false);
            }
            if (!equipTypes.Contains(DataConfig.EquipableType.OxygenTank))
            {
                player.GetPlayerGaugesHandler()
                                .UpdateGaugesDependingOnEquipment(
                                    player.GetPlayerEquipment()
                                    .GetInventory()
                                );
            }
            if (!equipTypes.Contains(DataConfig.EquipableType.BootsSpeed))
            {
                player.GetPlayerMovable()
                                .SetMoveSpeedChangePercentage(0f);
            }
            if (!equipTypes.Contains(DataConfig.EquipableType.Jetpack))
            {
                player.GetPlayerMovable()
                                .SetJetpackFactor(0f);
            }
            if (!equipTypes.Contains(DataConfig.EquipableType.MultiToolMineSpeed))
            {
                player.GetMultitool()
                                .GetMultiToolMine()
                                .SetMineTimeReducer(0);
            }
            if (!equipTypes.Contains(DataConfig.EquipableType.AirFilter))
            {
                // Since 0.6.001: There is no simple boolean toggle;
                // the method checks for the existence of the rebreather in the inventory
                // thus we fake an empty inventory
                player.GetGaugesHandler().SetHasRebreather(new Inventory(-1, 1));
            }
            if (!equipTypes.Contains(DataConfig.EquipableType.MultiToolCleanConstruction))
            {
                // Since 0.6.006: there is no publicly accessible field to set or method to call
                playerEquipmentHasCleanConstructionChip.SetValue(
                    player.GetPlayerEquipment(), false);
            }
            if (!equipTypes.Contains(DataConfig.EquipableType.MapChip))
            {
                // Since 0.9.002: the field is not public
                playerEquipmentHasMapChip.SetValue(player.GetPlayerEquipment(), false);
            }
            // FIXME backpack and equipment mod unequipping
            float dropDistance = 0.7f;
            if (!equipTypes.Contains(DataConfig.EquipableType.BackpackIncrease))
            {
                Inventory inv = player.GetPlayerBackpack().GetInventory();
                var list = inv.GetInsideWorldObjects();
                inv.SetSize(12);
                var point = player.GetAimController().GetAimRay().GetPoint(dropDistance);
                
                for (int i = list.Count - 1; i >= 0; i--)
                {
                    if (!IsFull(inv))
                    {
                        break;
                    }
                    var item = list[i];
                    inv.RemoveItem(item, false);
                    LogInfo("UnapplyEquipment: Dropping From Backpack " + DebugWorldObject(item));
                    WorldObjectsHandler.DropOnFloor(item, point);
                    LogInfo("UnapplyEquipment: " + inv.GetInsideWorldObjects().Count + "; " + inv.IsFull());
                }
            }
            if (!equipTypes.Contains(DataConfig.EquipableType.EquipmentIncrease))
            {
                Inventory inv = player.GetPlayerBackpack().GetInventory();
                Inventory equip = player.GetPlayerEquipment().GetInventory();
                var point = player.GetAimController().GetAimRay().GetPoint(dropDistance);

                var list = equip.GetInsideWorldObjects();
                equip.SetSize(4);
                bool equipmentRemoved = false;

                for (int i = list.Count - 1; i >= 0; i--)
                {
                    if (!equip.IsFull())
                    {
                        break;
                    }
                    equipmentRemoved = true;
                    var eq = list[i];
                    equip.RemoveItem(eq, false);
                    if (!inv.AddItem(eq))
                    {
                        LogInfo("UnapplyEquipment: Dropping From Equipment " + DebugWorldObject(eq));
                        WorldObjectsHandler.DropOnFloor(eq, point);
                    }
                    else
                    {
                        LogInfo("UnapplyEquipment: Move to backpack " + DebugWorldObject(eq));
                    }
                }

                if (equipmentRemoved)
                {
                    UnapplyUsingList(player, list);
                }
            }
        }

        static bool IsFull(Inventory inv)
        {
            if (getStackCount != null)
            {
                // fine, always run against the player's inventory which always stacks
                return inv.GetSize() < getStackCount(inv.GetInsideWorldObjects());
            }
            return inv.IsFull();
        }

        static void UnapplyUsingList(PlayerMainController player, List<WorldObject> list)
        {
            var equippables = new HashSet<DataConfig.EquipableType>();
            foreach (WorldObject wo in list)
            {
                if (wo.GetGroup() is GroupItem groupItem)
                {
                    var equipType = groupItem.GetEquipableType();
                    equippables.Add(equipType);
                }
            }
            UnapplyEquipment(equippables, player);
        }

        static void ReceiveMessageSortInventory(MessageSortInventory msi)
        {
            if (msi.inventoryId != 2)
            {
                Inventory targetInventory = null;
                // retarget shadow inventory
                if (msi.inventoryId == 1 && updateMode == MultiplayerMode.CoopHost)
                {
                    targetInventory = msi.sender.shadowBackpack;
                }
                else if (msi.inventoryId >= shadowInventoryWorldIdEnd)
                {
                    targetInventory = InventoriesHandler.GetInventoryById(msi.inventoryId);
                }
                if (targetInventory != null)
                {
                    suppressInventoryChange = true;
                    try
                    {
                        targetInventory.AutoSort();
                    }
                    finally
                    {
                        suppressInventoryChange = false;
                    }
                }
            }
        }

        /// <summary>
        /// Set of inventory ids that are being spawned by the host.
        /// </summary>
        static readonly HashSet<int> inventorySpawning = new();

        /// <summary>
        /// The vanilla game calls InventoryAssociated::GetInventory to generate the inventory contents on
        /// demand.
        /// 
        /// On the host, we will notify the client about the creation of the inventory and
        /// let the add messages propagate.
        /// 
        /// On the client, we send a inventory spawn request to the host. We return
        /// a temporary empty inventory, which must be locked out of deconstruction.
        /// </summary>
        /// <param name="__instance">The component instance to find the inventory id</param>
        /// <param name="___inventory">The actual inventory returned generated</param>
        /// <param name="__result">The inventory instance</param>
        /// <returns>false for the client, true otherwise</returns>
        [HarmonyPrefix]
        [HarmonyPatch(typeof(InventoryAssociated), nameof(InventoryAssociated.GetInventory))]
        static bool InventoryAssociated_GetInventory(InventoryAssociated __instance,
            ref Inventory ___inventory,
            ref Inventory __result
        )
        {
            if (updateMode != MultiplayerMode.SinglePlayer)
            {
                if (___inventory == null)
                {
                    InventoryFromScene component = __instance.GetComponent<InventoryFromScene>();
                    if (component != null)
                    {
                        int iid = component.GetInventoryGeneratedId();
                        Inventory inventoryById = InventoriesHandler.GetInventoryById(iid);
                        if (inventoryById != null)
                        {
                            __instance.SetInventory(inventoryById);
                            __result = ___inventory;
                        }
                        else
                        {
                            Inventory inventory = InventoriesHandler.CreateNewInventory(component.GetSize(), iid);
                            __instance.SetInventory(inventory);
                            __result = ___inventory;
                            if (updateMode == MultiplayerMode.CoopClient)
                            {
                                LogInfo("InventoryAssociated_GetInventory: Request host spawn " + iid + ", Size = " + component.GetSize());
                                SendHost(new MessageInventorySpawn() { inventoryId = iid }, true);
                                inventorySpawning.Add(iid);
                            }
                            else
                            {
                                LogInfo("InventoryAssociated_GetInventory: Generating host inventory: " + iid + ", Size = " + component.GetSize());
                                SendAllClients(new MessageInventorySize() { inventoryId = iid, size = component.GetSize() }, true);

                                List<Group> generatedGroups = component.GetGeneratedGroups();
                                foreach (Group item in generatedGroups)
                                {
                                    WorldObject worldObject = WorldObjectsHandler.CreateNewWorldObject(item);
                                    SendWorldObjectToClients(worldObject, false);
                                    inventory.AddItem(worldObject);
                                }
                            }
                        }
                    }
                    else
                    {
                        LogWarning("InventoryAssociated_GetInventory: No InventoryFromScene?!");
                    }
                }
                else
                {
                    __result = ___inventory;
                }

                return false;
            }
            return true;
        }

        static void FindAndGenerateInventoryFromScene(int iid)
        {
            foreach (var sceneGo in FindObjectsByType<GameObject>(FindObjectsSortMode.None))
            {
                var sceneInventory = sceneGo.GetComponentInChildren<InventoryFromScene>();
                if (sceneInventory != null)
                {
                    int sid = sceneInventory.GetInventoryGeneratedId();
                    //LogInfo("ReceiveMessageInventorySpawn: Found " + sid);
                    if (sid == iid)
                    {
                        Inventory inventoryById = InventoriesHandler.GetInventoryById(iid);
                        if (inventoryById == null)
                        {
                            List<Group> generatedGroups = sceneInventory.GetGeneratedGroups();
                            LogInfo("ReceiveMessageInventorySpawn: InventoryFromScene generated = " + iid + ", Count = " + generatedGroups.Count);
                            Inventory inventory = InventoriesHandler.CreateNewInventory(sceneInventory.GetSize(), iid);

                            SendAllClients(new MessageInventorySize() { inventoryId = iid, size = sceneInventory.GetSize() }, true);

                            foreach (Group group in generatedGroups)
                            {
                                WorldObject worldObject = WorldObjectsHandler.CreateNewWorldObject(group, 0);
                                SendWorldObjectToClients(worldObject, false);
                                inventory.AddItem(worldObject);
                            }
                        }
                        else
                        {
                            LogWarning("ReceiveMessageInventorySpawn: InventoryFromScene already instantiated = " + iid);
                        }
                        return;
                    }
                }
            }
            LogWarning("ReceiveMessageInventorySpawn: InventoryFromScene not found: " + iid + ", (sector not loaded?)");
        }

        static void OnSceneLoadedForSpawn(AsyncOperation op, Sector ___sector, string name)
        {
            try
            {
                sectorSceneLoaded.Invoke(___sector, new object[] { op });    
            } 
            finally
            {
                sceneLoadingForSpawn = false;
            }
            LogInfo("SectorEnter_TrackOtherPlayer: Loading Scene " + name + " done");
            foreach (int spawnId in sceneSpawnRequest)
            {
                FindAndGenerateInventoryFromScene(spawnId);
            }
            sceneSpawnRequest.Clear();
        }

        static void ReceiveMessageInventorySpawn(MessageInventorySpawn mis)
        {
            if (updateMode == MultiplayerMode.CoopHost)
            {
                int iid = mis.inventoryId;

                if (sceneLoadingForSpawn)
                {
                    LogInfo("ReceiveMessageInventorySpawn: Scene loading in progress, resubmit request");
                    //receiveQueue.Enqueue(mis);
                    sceneSpawnRequest.Add(mis.inventoryId);
                }
                else
                {
                    FindAndGenerateInventoryFromScene(iid);
                }
            }
        }

        static void ReceiveMessageInventorySize(MessageInventorySize mis)
        {
            if (updateMode == MultiplayerMode.CoopClient)
            {
                Inventory inv = InventoriesHandler.GetInventoryById(mis.inventoryId);
                if (inv == null)
                {
                    LogInfo("ReceiveMessageInventorySize: Create new inventory " + mis.inventoryId + ", size = " + mis.size);
                    InventoriesHandler.CreateNewInventory(mis.size, mis.inventoryId);
                }
                else
                {
                    LogInfo("ReceiveMessageInventorySize: Update " + mis.inventoryId + ", size = " + inv.GetSize() + " -> " + mis.size);
                    inv.SetSize(mis.size);
                }
            }
        }

        /// <summary>
        /// Tries to create an item in the target inventory and if successful, notifies
        /// the client about the new WorldObject and the inventory that got changed.
        /// </summary>
        /// <param name="group">The item type to create</param>
        /// <param name="inventory">The target inventory to create into</param>
        /// <param name="cc">The client whose inventory is targeted or null for all clients</param>
        /// <param name="worldObjectCreated">The object created</param>
        /// <returns>True if successful, false if the target inventory is full.</returns>
        static bool TryCreateInInventoryAndNotify(Group group, Inventory inventory, 
            ClientConnection cc,
            out WorldObject worldObjectCreated)
        {
            var craftedWo = WorldObjectsHandler.CreateNewWorldObject(group);
            suppressInventoryChange = true;
            bool added;
            try
            {
                added = inventory.AddItem(craftedWo);
            }
            finally
            {
                suppressInventoryChange = false;
            }

            if (added)
            {
                // We need to send the object first, then send the instruction that it has been
                // Added to the target inventory.
                SendWorldObjectToClients(craftedWo, false);

                var msg = new MessageInventoryAdded()
                {
                    inventoryId = inventory.GetId(),
                    itemId = craftedWo.GetId(),
                    groupId = craftedWo.GetGroup().GetId()
                };

                if (cc != null)
                {
                    cc.Send(msg);
                    cc.Signal();
                } 
                else
                {
                    SendAllClients(msg, true);
                }
                worldObjectCreated = craftedWo;
                return true;
            }
            WorldObjectsHandler.DestroyWorldObject(craftedWo);
            worldObjectCreated = null;
            return false;
        }

        /// <summary>
        /// Some machines can be set to clear inventory if they are full.
        /// 
        /// We don't do this on the client and let the host handle it.
        /// </summary>
        /// <returns></returns>
        [HarmonyPrefix]
        [HarmonyPatch(typeof(MachineDestructInventoryIfFull), "TryToCleanInventory")]
        static bool MachineDestructInventoryIfFull_TryToCleanInventory()
        {
            return updateMode != MultiplayerMode.CoopClient;
        }

        /// <summary>
        /// Vanilla calls this when the setting buttons, on the inventory screen, have been clicked.
        /// 
        /// We relay this to the other parties.
        /// </summary>
        /// <param name="___relatedWorldObject"></param>
        [HarmonyPostfix]
        [HarmonyPatch(typeof(UiContainerSettingOnOffButton), "OnClickOnOffButtons")]
        static void UiContainerSettingOnOffButton_OnClickOnOffButtons(WorldObject ___relatedWorldObject)
        {
            if (updateMode == MultiplayerMode.CoopHost)
            {
                SendWorldObjectToClients(___relatedWorldObject, false);
            }
            else
            {
                SendWorldObjectToHost(___relatedWorldObject, false);
            }
        }

        /// <summary>
        /// Vanilla calls it during the setting up of the settings button, on the inventory screen.
        /// 
        /// We install a tracker so we can update the visuals if the other side changes the settings
        /// </summary>
        /// <param name="___relatedWorldObject"></param>
        [HarmonyPostfix]
        [HarmonyPatch(typeof(UiContainerSettingOnOffButton), "SetUiSettingWorldObject")]
        static void UiContainerSettingOnOffButton_SetUiSettingWorldObject(UiContainerSettingOnOffButton __instance, 
            WorldObject ___relatedWorldObject, UiOnOffButtons ___uiOnOffButtons)
        {
            if (updateMode != MultiplayerMode.SinglePlayer)
            {
                __instance.StartCoroutine(UiContainerSettingOnOffButton_Tracker(___relatedWorldObject, ___uiOnOffButtons, 0.25f));
            }
        }

        static IEnumerator UiContainerSettingOnOffButton_Tracker(WorldObject ___relatedWorldObject, 
            UiOnOffButtons ___uiOnOffButtons, float delay)
        {
            for (; ; )
            {
                // do not trigger ping-pong updates
                var save = ___uiOnOffButtons.uiOnOffButtonsClick;
                ___uiOnOffButtons.uiOnOffButtonsClick = e => { };

                ___uiOnOffButtons.SetStatusOfOnOffButtons(___relatedWorldObject.GetSetting() != 0);

                ___uiOnOffButtons.uiOnOffButtonsClick = save;

                yield return new WaitForSeconds(delay);
            }
        }
    }
}
