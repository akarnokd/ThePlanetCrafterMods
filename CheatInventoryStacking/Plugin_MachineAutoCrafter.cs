// Copyright (c) 2022-2025, David Karnok & Contributors
// Licensed under the Apache License, Version 2.0

using HarmonyLib;
using LibCommon;
using SpaceCraft;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SocialPlatforms;
using UnityEngine.UIElements;

namespace CheatInventoryStacking
{
    public partial class Plugin
    {
        /// <summary>
        /// Conditionally stack in AutoCrafters.
        /// </summary>
        /// <param name="_inventory">The inventory of the AutoCrafter.</param>
        [HarmonyPostfix]
        [HarmonyPatch(typeof(MachineAutoCrafter), nameof(MachineAutoCrafter.SetAutoCrafterInventory))]
        static void Patch_MachineAutoCrafter_SetAutoCrafterInventory(Inventory autoCrafterInventory)
        {
            if (!stackAutoCrafters.Value)
            {
                noStackingInventories.Add(autoCrafterInventory.GetId());
            }
        }

        /// <summary>
        /// The vanilla method uses isFull which might be unreliable with stacking enabled,
        /// thus we have to replace the coroutine with our fully rewritten one.
        /// 
        /// Consequently, the MachineAutoCrafter::CraftIfPossible consumes resources and
        /// does not verify the crafted item can be deposited into the local inventory, thus
        /// wasting the ingredients. Another reason to rewrite.
        /// </summary>
        /// <param name="__instance">The underlying MachineAutoCrafter instance to get public values from.</param>
        /// <param name="timeRepeat">How often to craft?</param>
        /// <param name="__result">The overridden coroutine</param>
        /// <returns>false when running with stack size > 1 and not multiplayer, true otherwise.</returns>
        [HarmonyPrefix]
        [HarmonyPatch(typeof(MachineAutoCrafter), "TryToCraft")]
        static bool Patch_MachineAutoCrafter_TryToCraft_Patch(
            MachineAutoCrafter __instance,
            float timeRepeat,
            ref IEnumerator __result)
        {
            if (stackSize.Value > 1)
            {
                __result = MachineAutoCrafterTryToCraftOverride(__instance, timeRepeat);
                return false;
            }
            return true;
        }

        static IEnumerator MachineAutoCrafterTryToCraftOverride(MachineAutoCrafter __instance, float timeRepeat)
        {
            yield return new WaitForSeconds((fMachineAutoCrafterInstancedAutocrafters() % 50) / 10f);

            var wait = new WaitForSeconds(timeRepeat);

            var mSetItemsInRange = AccessTools.MethodDelegate<Action>(mMachineAutoCrafterSetItemsInRange, __instance);
            var mCraftIfPossible = AccessTools.MethodDelegate<Action<Group>>(mMachineAutoCrafterCraftIfPossible, __instance);
            var worldObjectsInRange = fMachineAutoCrafterWorldObjectsInRange();

            for (; ; )
            {
                var sw = Stopwatch.StartNew();

                if (fMachineAutoCrafterHasEnergy(__instance))
                {
                    fMachineAutoCrafterTimeHasCrafted(__instance) = Time.time;
                    var inv = fMachineAutoCrafterInventory(__instance);
                    if (inv != null)
                    {
                        var machineWo = __instance.GetComponent<WorldObjectAssociated>()?.GetWorldObject();
                        if (machineWo != null)
                        {
                            var routineId = CreateRoutineId(inv.GetId());

                            if (!CoroutineCoordinator.CanRun(routineId))
                            {
                                yield return null;
                                continue;
                            }


                            var linkedGroups = machineWo.GetLinkedGroups();
                            if (linkedGroups != null && linkedGroups.Count != 0)
                            {

                                var gr = linkedGroups[0];

                                if (!IsFullStackedOfInventory(inv, gr.id))
                                {
                                    AutoCrafterTry(gr, worldObjectsInRange, 
                                        mSetItemsInRange, mCraftIfPossible);
                                }
                            }


                            var frameTimeLimit = logisticsTimeLimit.Value / 1000d;

                            var elaps0 = sw.Elapsed.TotalMilliseconds;
                            if (elaps0 > frameTimeLimit)
                            {
                                CoroutineCoordinator.Yield(routineId, elaps0);
                                do
                                {
                                    yield return null;
                                }
                                while (!CoroutineCoordinator.CanRun(routineId));
                            }
                        }
                    }
                }
                yield return wait;
            }
        }

        static string CreateRoutineId(int id)
        {
            return "CheatInventoryStacking::MachineAutoCrafterTryToCraft::" + id;
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(MachineAutoCrafter), "OnDestroy")]
        static void Patch_MachineAutoCrafter_OnDestroy(Inventory ____autoCrafterInventory)
        {
            if (____autoCrafterInventory != null)
            {
                CoroutineCoordinator.Remove(CreateRoutineId(____autoCrafterInventory.GetId()));
            }
        }

        static void AutoCrafterTry(Group gr, 
            HashSet<WorldObject> worldObjectsInRange, 
            Action mSetItemsInRange, 
            Action<Group> mCraftIfPossible)
        {
            ClearAutoCrafterWorldObjects(gr.GetRecipe().GetIngredientsGroupInRecipe());

            _gosInRangeForListingRef = fMachineAutoCrafterGosInRangeForListing();
            mSetItemsInRange();
            _gosInRangeForListingRef = null;

            JoinAutoCrafterWorldObjects(worldObjectsInRange);

            ClearAutoCrafterWorldObjects(null);

            mCraftIfPossible(gr);
        }

        static readonly Dictionary<Group, List<WorldObject>> autocrafterWorldObjects = [];

        static List<ValueTuple<GameObject, Group>> _gosInRangeForListingRef;

        [HarmonyPrefix]
        [HarmonyPatch(typeof(MachineAutoCrafter), "GetInRange")]
        static bool Patch_MachineAutoCrafter_GetInRange(
            List<ValueTuple<GameObject, Group>> ____gosInRangeForListing,
            List<InventoryAssociatedProxy> ____proxys,
            GameObject go, 
            Group group)
        {
            if (stackSize.Value <= 1)
            {
                return true;
            }
            if (autocrafterWorldObjects.Count == 0)
            {
                AutoCrafterGetInRangeOnlyListing(____gosInRangeForListing, go, group);
            }
            else
            {
                AutoCrafterGetInRangeFull(____gosInRangeForListing, ____proxys, go, group); ;
            }
            return false;
        }

        static bool AutoCrafterGetInRangeFull(
            List<ValueTuple<GameObject, Group>> ____gosInRangeForListing,
            List<InventoryAssociatedProxy> ____proxys,
            GameObject go,
            Group group)
        {
            if (group is GroupItem)
            {
                if (NetworkManager.Singleton.IsServer)
                {
                    var woa = go.GetComponentInChildren<WorldObjectAssociated>(true);
                    if (woa == null)
                    {
                        return false;
                    }
                    var wo = woa.GetWorldObject();
                    var growth = wo.GetGrowth();
                    if (growth > 0f && growth < 100f)
                    {
                        return false;
                    }
                    AddToAutoCrafterWorldObjects(wo, autocrafterWorldObjects);
                }
                ____gosInRangeForListing.Add(new(go, group));
                return false;
            }
            if (group.GetLogisticInterplanetaryType() != DataConfig.LogisticInterplanetaryType.Disabled
                && (
                    go.GetComponentInChildren<InventoryAssociated>(true) != null
                    || go.GetComponentInChildren<InventoryAssociatedProxy>(true) != null
                    )
                  )
            {
                ____gosInRangeForListing.Add(new(go, group));
                if (NetworkManager.Singleton.IsServer)
                {
                    ____proxys.Clear();
                    go.GetComponentsInChildren(true, ____proxys);
                    foreach (var inventoryAssociatedProxy in ____proxys)
                    {
                        var requestedInventoryData = inventoryAssociatedProxy.GetRequestedInventoryData();
                        if (group.GetLogisticInterplanetaryType() != DataConfig.LogisticInterplanetaryType.EnabledOnSecondaryInventories
                            || inventoryAssociatedProxy.GetSecondaryInventoryIndex() != -1)
                        {
                            AutoCrafterGetInRangeProcessInventory(requestedInventoryData.Item2, autocrafterWorldObjects);
                        }
                    }
                }
            }
            return false;
        }

        static void AutoCrafterGetInRangeOnlyListing(
            List<ValueTuple<GameObject, Group>> ____gosInRangeForListing,
            GameObject go,
            Group group)
        {
            if (group is GroupItem)
            {
                ____gosInRangeForListing.Add(new(go, group));
                return;
            }
            if (group.GetLogisticInterplanetaryType() != DataConfig.LogisticInterplanetaryType.Disabled
                && (
                    go.GetComponentInChildren<InventoryAssociated>(true) != null
                    || go.GetComponentInChildren<InventoryAssociatedProxy>(true) != null
                    )
                  )
            {
                ____gosInRangeForListing.Add(new(go, group));
            }
        }

        static void AddToAutoCrafterWorldObjects(WorldObject wo, Dictionary<Group, List<WorldObject>> dict)
        {
            var gr = wo.GetGroup();
            if (dict.TryGetValue(gr, out var list) 
                && list.Count < 9)
            {
                list.Add(wo);
            }
        }


        static void ClearAutoCrafterWorldObjects(List<Group> recipe)
        {
            autocrafterWorldObjects.Clear();
            if (recipe != null)
            {
                foreach (var g in recipe)
                {
                    autocrafterWorldObjects.TryAdd(g, new(10));
                }
            }
        }

        static void JoinAutoCrafterWorldObjects(HashSet<WorldObject> set)
        {
            set.Clear();
            foreach (var list in autocrafterWorldObjects.Values)
            {
                foreach (var worldObject in list)
                {
                    set.Add(worldObject);
                }
            }
        }

        static WorldObject[] machineGetInRangeCache = [];

        [HarmonyPrefix]
        [HarmonyPatch(typeof(MachineGetInRange), nameof(MachineGetInRange.GetGroupsInRange))]
        static bool Patch_MachineGetInRange_GetGroupsInRange(
            MachineGetInRange __instance,
            float range, 
            Action<GameObject, Group> result
        )
        {
            if (NetworkManager.Singleton.IsServer && !__instance.gameObject.activeInHierarchy)
            {
                MachineGetInRangeTryScan(__instance, range, result);
                return false;
            }
            if (!__instance.gameObject.activeInHierarchy)
            {
                return false;
            }

            return true;
        }

        private static void MachineGetInRangeTryScan(MachineGetInRange __instance, float range, Action<GameObject, Group> result)
        {
            var self = __instance.GetComponent<WorldObjectAssociated>().GetWorldObject();
            var position = __instance.transform.position;
            var h = self.GetPlanetHash();
            int n = Math.Min(4, Environment.ProcessorCount);

            var itemCount = WorldObjectsHandler.Instance.GetAllWorldObjects().Count;
            Log("MachineGetInRange: " + itemCount + " total, " + placedWorldObjects.Count + " placed, NCPU: " + n);
            if (itemCount < 10_000 || n <= 1 || _gosInRangeForListingRef == null)
            {
                MachineGetInRangeSequential(self, position, h, range, result);
            }
            else
            {
                MachineGetInRangeParallel(self, position, h, n, range, result);
            }
        }

        static void MachineGetInRangeSequential(WorldObject self, Vector3 pos, int h, float r, Action<GameObject, Group> result)
        {
            if (_gosInRangeForListingRef == null)
            {
                foreach (var value in placedWorldObjects)
                {
                    if (Vector3.Distance(pos, value.GetPosition()) < r
                        && value != self
                        && value.GetGameObject() != null
                        && value.GetPlanetHash() == h
                    )
                    {
                        result(value.GetGameObject(), value.GetGroup());
                    }
                }
            }
            else
            {
                foreach (var value in placedWorldObjects)
                {
                    if (Vector3.Distance(pos, value.GetPosition()) < r
                        && value != self
                        && value.GetGameObject() != null
                        && value.GetPlanetHash() == h
                    )
                    {
                        AutoCrafterGetInRangeWorldObject(value, autocrafterWorldObjects, _gosInRangeForListingRef);
                    }
                }
            }
        }

        static void MachineGetInRangeParallel(WorldObject self, 
            Vector3 position, int h, int n, float range,
            Action<GameObject, Group> result)
        {
            var workQueues = new Dictionary<Group, List<WorldObject>>[n];
            var listings = new List<(GameObject, Group)>[n];
            for (int i = 0; i < n; i++)
            {
                workQueues[i] = [];
                foreach (var k in autocrafterWorldObjects.Keys)
                {
                    workQueues[i].TryAdd(k, new(10));
                }
                listings[i] = [];
            }

            var count = MachineGetInRangeCreateArray(placedWorldObjects);

            MachineGetInRangeParallelScan(
                range,
                self,
                position,
                n,
                workQueues,
                count,
                result,
                listings);

            foreach (var wq in workQueues)
            {
                foreach (var e in wq)
                {
                    if (autocrafterWorldObjects.TryGetValue(e.Key, out var worldObjects))
                    {
                        worldObjects.AddRange(e.Value);
                    }
                }
            }
            foreach (var lst in listings)
            {
                _gosInRangeForListingRef.AddRange(lst);
            }
        }

        static int MachineGetInRangeCreateArray(HashSet<WorldObject> placed)
        {
            var n = placed.Count;
            if (machineGetInRangeCache.Length < n)
            {
                machineGetInRangeCache = new WorldObject[placed.Count * 11 / 10];
            }
            placed.CopyTo(machineGetInRangeCache, 0);
            return n;
        }

        static void MachineGetInRangeParallelScan(
            float _range, 
            WorldObject _worldObject, 
            Vector3 _position, 
            int _n,
            Dictionary<Group, List<WorldObject>>[] _workQueues,
            int inputCount,
            Action<GameObject, Group> result,
            List<(GameObject, Group)>[] listings)
        {
            Enumerable.Range(0, _n)
                .AsParallel()
                .ForAll(index =>
                {
                    var self = _worldObject;
                    var pos = _position;
                    var wos = machineGetInRangeCache;
                    var woq = _workQueues;
                    var count = inputCount / _n;
                    var start = count * index;
                    var end = start + count;
                    var r = _range;
                    var h = self.GetPlanetHash();
                    var q = _workQueues[index];
                    var lst = listings[index];
                    if (index == _n - 1)
                    {
                        end = inputCount;
                    }
                    for (int i = start; i < end; i++)
                    {
                        var value = wos[i];
                        if (Vector3.Distance(pos, value.GetPosition()) < r
                            && value != self
                            && value.GetGameObject() != null
                            && value.GetPlanetHash() == h
                        )
                        {
                            AutoCrafterGetInRangeWorldObject(value, q, lst);
                        }
                    }
                });
        }

        static void AutoCrafterGetInRangeWorldObject(WorldObject wo, 
            Dictionary<Group, List<WorldObject>> q,
            List<(GameObject, Group)> lst)
        {
            var group = wo.GetGroup();
            if (group is GroupItem)
            {
                var growth = wo.GetGrowth();
                if (growth > 0f && growth < 100f)
                {
                    return;
                }
                AddToAutoCrafterWorldObjects(wo, q);
                lst.Add(new(wo.GetGameObject(), group));
                return;
            }
            var lt = group.GetLogisticInterplanetaryType();
            if (lt != DataConfig.LogisticInterplanetaryType.Disabled)
            {
                var all = lt != DataConfig.LogisticInterplanetaryType.EnabledOnSecondaryInventories;
                var iid1 = wo.GetLinkedInventoryId();
                if (iid1 > 0)
                {
                    lst.Add(new(wo.GetGameObject(), group));
                    if (all)
                    {
                        AutoCrafterGetInRangeProcessInventory(iid1, q);
                    }
                }
                var secondIds = wo.GetSecondaryInventoriesId();
                if (secondIds != null && secondIds.Count != 0)
                {
                    foreach (var iid2 in secondIds)
                    {
                        AutoCrafterGetInRangeProcessInventory(iid2, q);
                    }
                }
            }
        }

        static void AutoCrafterGetInRangeProcessInventory(int iid, 
            Dictionary<Group, List<WorldObject>> q)
        {
            var items = fInventoryWorldObjectsInInventory(InventoriesHandler.Instance.GetInventoryById(iid));
            foreach (var worldObject in items)
            {
                if (worldObject.GetPosition() == Vector3.zero || worldObject.GetGrowth() == 100f)
                {
                    AddToAutoCrafterWorldObjects(worldObject, q);
                }
            }
        }

        static readonly HashSet<WorldObject> placedWorldObjects = [];

        [HarmonyPostfix]
        [HarmonyPatch(typeof(WorldObject), nameof(WorldObject.SetPositionAndRotation))]
        static void Patch_WorldObject_SetPositionAndRotation(
            WorldObject __instance, Vector3 position)
        {
            if (position == Vector3.zero)
            {
                placedWorldObjects.Remove(__instance);
            }
            else
            {
                placedWorldObjects.Add(__instance);
            }
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(WorldObject), nameof(WorldObject.ResetPositionAndRotation))]
        static void Patch_WorldObject_ResetPositionAndRotation(WorldObject __instance)
        {
            placedWorldObjects.Remove(__instance);
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(WorldObjectsHandler), "SetAllWorldObjects")]
        static void Patch_WorldObjectsHandler_SetAllWorldObjects()
        {
            placedWorldObjects.Clear();
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(WorldObjectsHandler), "AddToSpecificLists")]
        static void Patch_WorldObjectsHandler_AddToSpecificLists(WorldObject worldObject)
        {
            if (worldObject.GetPosition() != Vector3.zero)
            {
                placedWorldObjects.Add(worldObject);
            }
            else
            {
                placedWorldObjects.Remove(worldObject);
            }
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(WorldObjectsHandler), nameof(WorldObjectsHandler.DestroyWorldObject), [typeof(WorldObject), typeof(bool)])]
        static void Patch_WorldObjectsHandler_DestroyWorldObject(WorldObject worldObject)
        {
            placedWorldObjects.Remove(worldObject);
        }
    }
}
