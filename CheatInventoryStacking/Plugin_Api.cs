// Copyright (c) 2022-2025, David Karnok & Contributors
// Licensed under the Apache License, Version 2.0

using LibCommon;
using SpaceCraft;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace CheatInventoryStacking
{
    public partial class Plugin
    {
        // --------------------------------------------------------------------------------------------------------
        // API: delegates to call from other mods.
        // --------------------------------------------------------------------------------------------------------

        /// <summary>
        /// Given a sequence of WorldObjects, return the number stacks it would coalesce into.
        /// </summary>
        public static readonly Func<IEnumerable<WorldObject>, int> apiGetStackCount = GetStackCountEnum;

        /// <summary>
        /// Given an Inventory, return the number of stacks it's contents would coalesce into.
        /// The inventory is checked if it does allow stacking or not.
        /// </summary>
        public static readonly Func<Inventory, int> apiGetStackCountInventory = GetStackCountOfInventory;

        /// <summary>
        /// Given an Inventory and an optional item group id, return true if
        /// the number of stacks the contents would coalesce (including the item) would
        /// be more than the maximum stack count for that inventory.
        /// The inventory is checked if it does allows stacking or not.
        /// </summary>
        public static readonly Func<Inventory, string, bool> apiIsFullStackedInventory = IsFullStackedOfInventory;

        /// <summary>
        /// Given an Inventory, a list of world objects to be removed and an optional item group id, return true if
        /// the number of stacks the contents would coalesce (including the item) would
        /// be more than the maximum stack count for that inventory.
        /// The inventory is checked if it does allow stacking or not.
        /// </summary>
        public static readonly Func<Inventory, HashSet<int>, string, bool> apiIsFullStackedWithRemoveInventory = IsFullStackedWithRemoveOfInventory;

        /// <summary>
        /// Given an Inventory, return the maximum number of homogeneous items it can hold,
        /// subject to if said inventory is allowed to stack or not.
        /// </summary>
        public static readonly Func<Inventory, int> apiGetCapacityInventory = GetCapacityOfInventory;

        /// <summary>
        /// Checks if the given inventory, identified by its id, is allowed to stack items, depending
        /// on the mod's settings.
        /// It also checks if any player's backpack is allowed to stack or not.
        /// </summary>
        public static readonly Func<int, bool> apiCanStack = CanStack;

        // -------------------------------------------------------------------------------------------------------------
        // API: pointed to by the delegates. Please use the delegates instead of doing reflective method calls on these.
        // -------------------------------------------------------------------------------------------------------------

        /// <summary>
        /// The workspace for counting groups without reallocating the dictionary.
        /// </summary>
        static readonly Dictionary<string, int> groupCounts = [];

        static readonly DictionaryStackCounter groupCounts2 = new(1024);

        static void AddToStackDict(string gid, Dictionary<string, int> groupCounts, int n, ref int stacks)
        {
            groupCounts.TryGetValue(gid, out int count);

            if (++count == 1)
            {
                stacks++;
            }
            if (count > n)
            {
                stacks++;
                count = 1;
            }
            groupCounts[gid] = count;
        }


        static int GetStackCountEnum(IEnumerable<WorldObject> items)
        {
            int n = stackSize.Value;
            int stacks = 0;

            if (debugModeOptimizations1.Value)
            {
                groupCounts2.Clear();
                foreach (WorldObject worldObject in items)
                {
                    groupCounts2.Update(GeneticsGrouping.GetStackId(worldObject), n, ref stacks);
                }
            }
            else
            {
                groupCounts.Clear();

                foreach (WorldObject worldObject in items)
                {
                    AddToStackDict(GeneticsGrouping.GetStackId(worldObject), groupCounts, n, ref stacks);
                }
            }

            return stacks;
        }

        static int GetStackCountList(List<WorldObject> items)
        {
            int n = stackSize.Value;
            int stacks = 0;

            if (debugModeOptimizations1.Value)
            {
                groupCounts2.Clear();
                foreach (WorldObject worldObject in items)
                {
                    groupCounts2.Update(GeneticsGrouping.GetStackId(worldObject), n, ref stacks);
                }
            }
            else
            {
                groupCounts.Clear();

                foreach (WorldObject worldObject in items)
                {
                    AddToStackDict(GeneticsGrouping.GetStackId(worldObject), groupCounts, n, ref stacks);
                }
            }

            return stacks;
        }

        static bool IsFullStackedEnum(IEnumerable<WorldObject> worldObjectsInInventory, int inventorySize, string gid)
        {

            int n = stackSize.Value;
            int stacks = 0;

            if (debugModeOptimizations1.Value)
            {
                groupCounts2.Clear();

                foreach (WorldObject worldObject in worldObjectsInInventory)
                {
                    groupCounts2.Update(GeneticsGrouping.GetStackId(worldObject), n, ref stacks);
                }

                if (gid != null)
                {
                    groupCounts2.Update(gid, n, ref stacks);
                }
            }
            else
            {
                groupCounts.Clear();

                foreach (WorldObject worldObject in worldObjectsInInventory)
                {
                    AddToStackDict(GeneticsGrouping.GetStackId(worldObject), groupCounts, n, ref stacks);
                }

                if (gid != null)
                {
                    AddToStackDict(gid, groupCounts, n, ref stacks);
                }
            }

            return stacks > inventorySize;
        }

        static bool IsFullStackedList(List<WorldObject> worldObjectsInInventory, int inventorySize, string gid)
        {
            int n = stackSize.Value;
            int stacks = 0;

            if (debugModeOptimizations1.Value)
            {
                groupCounts2.Clear();

                foreach (WorldObject worldObject in worldObjectsInInventory)
                {
                    groupCounts2.Update(GeneticsGrouping.GetStackId(worldObject), n, ref stacks);
                }

                if (gid != null)
                {
                    groupCounts2.Update(gid, n, ref stacks);
                }
            }
            else
            {
                groupCounts.Clear();

                foreach (WorldObject worldObject in worldObjectsInInventory)
                {
                    AddToStackDict(GeneticsGrouping.GetStackId(worldObject), groupCounts, n, ref stacks);
                }

                if (gid != null)
                {
                    AddToStackDict(gid, groupCounts, n, ref stacks);
                }
            }

            return stacks > inventorySize;
        }

        static bool IsLastSlotOccupied(IEnumerable<WorldObject> worldObjectsInInventory, int inventorySize, string gid = null)
        {
            int n = stackSize.Value;
            int stacks = 0;

            if (debugModeOptimizations1.Value)
            {
                groupCounts2.Clear();

                foreach (WorldObject worldObject in worldObjectsInInventory)
                {
                    groupCounts2.Update(GeneticsGrouping.GetStackId(worldObject), n, ref stacks);
                }

                if (gid != null)
                {
                    groupCounts2.Update(gid, n, ref stacks);
                }
            }
            else
            {
                groupCounts.Clear();

                foreach (WorldObject worldObject in worldObjectsInInventory)
                {
                    AddToStackDict(GeneticsGrouping.GetStackId(worldObject), groupCounts, n, ref stacks);
                }

                if (gid != null)
                {
                    AddToStackDict(gid, groupCounts, n, ref stacks);
                }
            }

            return stacks >= inventorySize;
        }

        /// <summary>
        /// Returns the number of stacks in the given inventory.
        /// </summary>
        /// <param name="inventory">The target inventory</param>
        /// <returns>The number of stacks occupied by the list of items.</returns>
        static int GetStackCountOfInventory(Inventory inventory)
        {
            var content = fInventoryWorldObjectsInInventory(inventory);
            if (!CanStack(inventory.GetId()))
            {
                return content.Count;
            }
            return GetStackCountList(content);
        }

        /// <summary>
        /// Checks if the given inventory is full or not if one tries to add the optional item indicated by
        /// its group id.
        /// </summary>
        /// <param name="inventory">The target inventory.</param>
        /// <param name="gid">The optional item group id to check if it can be added or not.</param>
        /// <returns>True if the list is full.</returns>
        static bool IsFullStackedOfInventory(Inventory inventory, string gid)
        {
            var worldObjects = fInventoryWorldObjectsInInventory(inventory);
            int capacity = inventory.GetSize();
            int count = worldObjects.Count;
            // if less than capacity items, we can't be stacked fully
            if (count < capacity)
            {
                return false;
            }
            if (!CanStack(inventory.GetId()))
            {
                return count >= capacity;
            }
            // if more than the capacity times the stacksize, we are full
            if (count >= capacity * stackSize.Value)
            {
                return true;
            }

            return IsFullStackedList(worldObjects, capacity, gid);
        }

        /// <summary>
        /// Checks if the given inventory is full or not if one tries to first remove the list
        /// of world objects and then add the optional item indicated by its group id.
        /// </summary>
        /// <param name="inventory">The target inventory.</param>
        /// <param name="gid">The optional item group id to check if it can be added or not.</param>
        /// <returns>True if the list is full.</returns>
        static bool IsFullStackedWithRemoveOfInventory(Inventory inventory, HashSet<int> toremove, string gid = null)
        {
            var content = fInventoryWorldObjectsInInventory(inventory);
            if (!CanStack(inventory.GetId()))
            {
                int count = content.Count;
                return count - toremove.Count >= inventory.GetSize();
            }
            if (toremove.Count != 0)
            {
                return IsFullStackedEnum(
                    content
                    .Where(wo => !toremove.Contains(wo.GetId())), 
                    inventory.GetSize(), gid);
            }

            return IsFullStackedList(content, inventory.GetSize(), gid);
        }

        /// <summary>
        /// Returns the total item capacity of the given inventory considering the stacking settings.
        /// </summary>
        /// <param name="inventory">The inventory to get the capacity of.</param>
        /// <returns>The capacity.</returns>
        static int GetCapacityOfInventory(Inventory inventory)
        {
            if (!CanStack(inventory.GetId()) || stackSize.Value <= 1)
            {
                return inventory.GetSize();
            }
            return inventory.GetSize() * stackSize.Value;
        }

        /// <summary>
        /// Checks if the given inventory (identified by its id) is allowed to stack items.
        /// Does not check if stackSize > 1 on its own!
        /// </summary>
        /// <param name="inventoryId">The target inventory to check.</param>
        /// <returns>True if the target inventory can stack.</returns>
        static bool CanStack(int inventoryId)
        {
            if (noStackingInventories.Contains(inventoryId))
            {
                return false;
            }
            // In multiplayer, players can come and go, so we must check their dynamic backpack ids.
            // Similarly, the host's backpack id is no longer constant (1).
            // Not as snappy as checking the hashset from before, but we do this only if
            // backpack stacking was explicitly disabled. Usually it won't be for most players.

            bool isBackpack = IsPlayerBackpack(inventoryId);

            if (!stackBackpack.Value && isBackpack)
            {
                return false;
            }
            if (stackOnlyBackpack.Value && !isBackpack)
            {
                return false;
            }
            return true;
        }

        static bool IsPlayerBackpack(int inventoryId)
        {
            // FIXME So if the playersManager is not available, does it mean stacking is not really relevant
            // because we are outside a world?

            // We cache the PlayersManager here.
            if (playersManager == null)
            {
                playersManager = Managers.GetManager<PlayersManager>();
            }
            if (playersManager != null)
            {
                foreach (var player in playersManager.playersControllers)
                {
                    if (player != null)
                    {
                        var pinv = player.GetPlayerBackpack().GetInventory();
                        if (pinv != null && pinv.GetId() == inventoryId)
                        {
                            return true;
                        }
                    }
                }

                // FIXME I don't know if playersControllers does include the active controller or not
                var apc = playersManager.GetActivePlayerController();
                if (apc != null)
                {
                    var pinv = apc.GetPlayerBackpack().GetInventory();
                    if (pinv != null && pinv.GetId() == inventoryId)
                    {
                        return true;
                    }
                }
            }
            return false;
        }
    }
}
