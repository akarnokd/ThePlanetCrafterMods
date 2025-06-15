// Copyright (c) 2022-2025, David Karnok & Contributors
// Licensed under the Apache License, Version 2.0

using BepInEx;
using BepInEx.Configuration;
using SpaceCraft;
using HarmonyLib;
using System.Collections.Generic;
using BepInEx.Logging;
using UnityEngine;
using System.Collections;
using Unity.Netcode;
using LibCommon;
using System;
using System.Diagnostics;
using System.Linq;

namespace CheatRecyclerRemoteDeposit
{
    [BepInPlugin(modGuid, "(Cheat) Recyclers Deposit Into Remote Containers", PluginInfo.PLUGIN_VERSION)]
    public class Plugin : BaseUnityPlugin
    {
        const string modGuid = "akarnokd.theplanetcraftermods.cheatrecyclerremotedeposit";
        const string FunctionRequestRecycle = "RequestRecycle";

        static ManualLogSource logger;

        static ConfigEntry<bool> modEnabled;

        static ConfigEntry<bool> debugMode;

        static ConfigEntry<bool> debugCoordinator;

        static ConfigEntry<string> defaultDepositAlias;

        static ConfigEntry<string> customDepositAliases;

        static ConfigEntry<int> autoRecyclePeriod;

        static ConfigEntry<int> maxRange;

        static ConfigEntry<int> frameTimeLimit;

        static readonly Dictionary<string, string> aliasMap = [];

        static bool isRunning;

        static Plugin me;

        static readonly Dictionary<int, Coroutine> clearCoroutines = [];

        static readonly Dictionary<ActionRecycle, Coroutine> autoRecycleCoroutines = [];

        static bool calledFromOnHoverOut;

        static AccessTools.FieldRef<MachineDisintegrator, Inventory> fMachineDisintegratorSecondInventory;

        static AccessTools.FieldRef<Inventory, List<WorldObject>> fInventoryWorldObjectsInInventory;

        private void Awake()
        {
            me = this;

            LibCommon.BepInExLoggerFix.ApplyFix();

            // Plugin startup logic
            Logger.LogInfo($"Plugin is loading!");

            logger = Logger;

            modEnabled = Config.Bind("General", "Enabled", true, "Is the mod enabled?");
            debugMode = Config.Bind("General", "DebugMode", false, "Enable debug mode with detailed logging (chatty!)");
            debugCoordinator = Config.Bind("General", "DebugCoordinator", false, "Enable coordinator logging (chatty!)");
            defaultDepositAlias = Config.Bind("General", "DefaultDepositAlias", "*Recycled", "The name of the container to deposit resources not explicity mentioned in CustomDepositAliases.");
            customDepositAliases = Config.Bind("General", "CustomDepositAliases", "", "Comma separated list of resource_id:alias to deposit into such named containers");
            autoRecyclePeriod = Config.Bind("General", "AutoRecyclePeriod", 5, "How often to auto-recycle, seconds. Zero means no auto-recycle.");
            maxRange = Config.Bind("General", "MaxRange", 20, "The maximum range to look for containers within. Zero means unlimited range.");
            frameTimeLimit = Config.Bind("General", "FrameTimeLimit", 5000, "How much time is allowed to inspect the output inventories to avoid stutter, in microseconds");

            fMachineDisintegratorSecondInventory = AccessTools.FieldRefAccess<MachineDisintegrator, Inventory>("_secondInventory");

            fInventoryWorldObjectsInInventory = AccessTools.FieldRefAccess<Inventory, List<WorldObject>>("_worldObjectsInInventory");

            ParseAliasConfig();

            CoroutineCoordinator.Init(LogCoord);

            LibCommon.HarmonyIntegrityCheck.Check(typeof(Plugin));
            var h = Harmony.CreateAndPatchAll(typeof(Plugin));

            ModNetworking.Init(modGuid, Logger);
            ModNetworking.Patch(h);
            ModNetworking.RegisterFunction(FunctionRequestRecycle, OnClientAction);

            LibCommon.ModPlanetLoaded.Patch(h, modGuid, _ => PlanetLoader_HandleDataAfterLoad());

            Logger.LogInfo($"Plugin is loaded!");
        }

        static void PlanetLoader_HandleDataAfterLoad()
        {
            foreach (var ar in FindObjectsByType<ActionRecycle>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (ar != null && ar.gameObject != null 
                    && !ar.gameObject.activeInHierarchy
                    && IsClone(ar.gameObject))
                {
                    Log("Manually Starting ActionRecycle looper for " + ar.GetInstanceID());
                    ActionRecylce_Start(ar);
                }
            }
        }

        static bool IsClone(GameObject go)
        {
            while (go != null)
            {
                if (go.name.EndsWith("(Clone)"))
                {
                    return true;
                }
                go = go.transform.parent.gameObject;
            }
            return false;
        }

        static void ParseAliasConfig()
        {
            aliasMap.Clear();
            var aliasesStr = customDepositAliases.Value.Trim();
            if (aliasesStr.Length > 0)
            {
                var parts = aliasesStr.Split(',');
                foreach (var alias in parts )
                {
                    var idname = alias.Split(':');
                    if (idname.Length == 2)
                    {
                        aliasMap[idname[0]] = idname[1].ToLowerInvariant();
                    }
                }
            }
        }

        public static void OnModConfigChanged(ConfigEntryBase _)
        {
            ParseAliasConfig();
        }

        static IEnumerable<Inventory> FindInventoryFor(string gid, Vector3 center, int planetHash)
        {
            if (!aliasMap.TryGetValue(gid, out var name))
            {
                name = defaultDepositAlias.Value.ToLowerInvariant();
            }

            var range = maxRange.Value;

            foreach (var wo in WorldObjectsHandler.Instance.GetConstructedWorldObjects())
            {
                if (wo.GetPlanetHash() == planetHash)
                {
                    var wot = wo.GetText();
                    if (wot != null && wot.ToLowerInvariant().Contains(name))
                    {
                        var xyz = wo.GetPosition();
                        if (Vector3.Distance(xyz, center) < range || range == 0)
                        {
                            var inv = InventoriesHandler.Instance.GetInventoryById(wo.GetLinkedInventoryId());
                            if (inv != null)
                            {
                                yield return inv;
                            }
                        }
                    }
                }
            }
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(ActionRecycle), nameof(ActionRecycle.OnAction))]
        static bool ActionRecycle_OnAction(
            ActionRecycle __instance, 
            Collider ___craftSpawn,
            GameObject ___tempCage)
        {
            if (modEnabled.Value && __instance.GetComponentInParent<GhostFx>(true) == null)
            {
                Log("ActionRecycle.OnAction called");
                if (!isRunning)
                {
                    var iap = __instance.GetComponentInParent<InventoryAssociatedProxy>(true);
                    if (iap != null && iap.IsSpawned)
                    {
                        // Log("  via InventoryAssociatedProxy");
                        isRunning = true;
                        ___tempCage.SetActive(true);

                        var depositor = new Depositor(__instance, ___craftSpawn);
                        iap.GetInventory(depositor.OnMachineReceived);
                    }
                    else
                    {
                        Log("  IsSpawned = false");
                    }
                    /*
                    else
                    {
                        var woa = __instance.GetComponent<WorldObjectAssociated>();
                        if (woa != null)
                        {
                            var wo = woa.GetWorldObject();
                            var inv = InventoriesHandler.Instance.GetInventoryById(wo.GetLinkedInventoryId());

                            Log("  via WorldObjectAssociated");

                            isRunning = true;
                            ___tempCage.SetActive(true);

                            var depositor = new Depositor(__instance, ___craftSpawn);
                            depositor.OnMachineReceived(inv, wo);
                        }
                        else
                        {
                            Log("  IsSpawned = false");
                        }
                    }
                    */
                }
                else
                {
                    Log("ActionRecycle.OnAction -> recycling is still going on");
                }
                return false;
            }
            return true;
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(ActionRecycle), "Start")]
        static void ActionRecylce_Start(ActionRecycle __instance)
        {
            ActionRecycle_OnDestroy(__instance);
            autoRecycleCoroutines[__instance] = me.StartCoroutine(AutoRecycle_Loop(__instance));
            Log("ActionRecycle::Start " + __instance.GetInstanceID());
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(ActionRecycle), nameof(ActionRecycle.OnHoverOut))]
        static void ActionRecycle_OnHoverOut()
        {
            calledFromOnHoverOut = true;
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(ActionRecycle), "OnDestroy")]
        static void ActionRecycle_OnDestroy(ActionRecycle __instance)
        {
            var b = calledFromOnHoverOut;
            calledFromOnHoverOut = false;
            if (!b)
            {
                if (autoRecycleCoroutines.Remove(__instance, out var coroutine))
                {
                    me.StopCoroutine(coroutine);
                    Log("ActionRecycle::OnDestroy " + __instance.GetInstanceID());
                }
            }
        }

        static IEnumerator AutoRecycle_Loop(ActionRecycle __instance)
        {
            for (; ; )
            {
                // Log("ActionRecycle_Loop:1");
                var ar = autoRecyclePeriod.Value;
                yield return new WaitForSeconds(ar == 0 ? 5f : ar);
                if (modEnabled.Value && ar > 0 && (NetworkManager.Singleton?.IsServer ?? true))
                {
                    // Log("ActionRecycle_Loop:2");
                    if (__instance.GetComponentInParent<GhostFx>(true) == null)
                    {
                        // Log("ActionRecycle_Loop:3");
                        __instance.OnAction();
                        // Log("ActionRecycle_Loop:4");
                    }
                }
            }
        }

        static void OnClientAction(ulong senderId, string arguments)
        {
            if (NetworkManager.Singleton.IsServer)
            {
                int id = int.Parse(arguments);

                var wo = WorldObjectsHandler.Instance.GetWorldObjectViaId(id);
                if (wo != null)
                {
                    var go = wo.GetGameObject();
                    if (go != null)
                    {
                        var action = go.GetComponentInChildren<ActionRecycle>();
                        if (action != null)
                        {
                            action.OnAction();
                        }
                        else
                        {
                            Log("ActionRecycle not found on " + arguments);
                        }
                    }
                    else
                    {
                        Log("GameObject not found on " + arguments);
                    }
                }
                else
                {
                    Log("WorldObject not found on " + arguments);
                }
            }
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(UiWindowPause), nameof(UiWindowPause.OnQuit))]
        static void UiWindowPause_OnQuit()
        {
            isRunning = false;
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(BlackScreen), nameof(BlackScreen.DisplayLogoStudio))]
        static void BlackScreen_DisplayLogoStudio()
        {
            UiWindowPause_OnQuit();
        }


        static void Log(object message)
        {
            if (debugMode.Value)
            {
                logger.LogInfo(message);
            }
        }

        static void LogCoord(object message)
        {
            if (debugCoordinator.Value)
            {
                logger.LogInfo(message);
            }
        }

        internal class Depositor
        {
            readonly ActionRecycle instance;
            readonly Collider craftSpawn;

            Inventory machineInventory;
            WorldObject machineWorldObject;
            WorldObject toRecycleWo;

            Vector3 machinePosition;

            IEnumerator<Inventory> currentInventoryCandidates;
            Inventory currentCandidate;

            int queueWip;

            readonly Queue<object> queue = [];

            internal Depositor(ActionRecycle instance, Collider craftSpawn)
            {
                this.instance = instance;
                this.craftSpawn = craftSpawn;
            }

            internal void OnMachineReceived(
                Inventory machineInventory,
                WorldObject machineWorldObject
            )
            {
                this.machineInventory = machineInventory;
                this.machineWorldObject = machineWorldObject;

                bool isServer = NetworkManager.Singleton?.IsServer ?? true;
                if (!isServer)
                {
                    isRunning = false;
                    Log("Request Recycle " + machineWorldObject.GetId() + " on " + machineWorldObject.GetPlanetHash());
                    ModNetworking.SendHost(FunctionRequestRecycle, machineWorldObject.GetId().ToString());
                    return;
                }


                var n = fInventoryWorldObjectsInInventory(machineInventory).Count;
                Log("Begin " + machineWorldObject.GetId() + " (" + n + ") on planet " + machineWorldObject.GetPlanetHash());

                if (n == 0)
                {
                    isRunning = false;
                    Log("    Nothing to recycle");
                    return;
                }

                machinePosition = machineWorldObject.GetPosition();

                toRecycleWo = fInventoryWorldObjectsInInventory(machineInventory)[0];
                var gi = toRecycleWo.GetGroup() as GroupItem;
                var recipe = gi.GetRecipe().GetIngredientsGroupInRecipe();

                if (recipe.Count == 0)
                {
                    Log("    Missing recipe: " + toRecycleWo.GetId() + " - " + gi.id);
                    queue.Enqueue(toRecycleWo);
                }
                else if (gi.GetCantBeRecycled())
                {
                    Log("    Can't be recycled: " + toRecycleWo.GetId() + " - " + gi.id);
                    queue.Enqueue(toRecycleWo);
                }
                else
                {
                    foreach (var ingredient in recipe)
                    {
                        queue.Enqueue(ingredient);
                    }
                }

                Drain();
            }

            internal void Drain()
            {
                if (queueWip++ != 0)
                {
                    return;
                }

                for (; ; )
                {
                    if (!queue.TryPeek(out var item))
                    {
                        OnComplete();
                    }
                    else
                    {
                        if (currentCandidate == null)
                        {
                            string gid = "";
                            if (item is WorldObject itemWo)
                            {
                                gid = itemWo.GetGroup().GetId();
                            }
                            else
                            {
                                gid = ((Group)item).id;
                            }

                            currentInventoryCandidates ??= FindInventoryFor(gid, machinePosition, machineWorldObject.GetPlanetHash()).GetEnumerator();

                            if (currentInventoryCandidates.MoveNext())
                            {
                                currentCandidate = currentInventoryCandidates.Current;
                                if (item is Group groupItem)
                                {
                                    InventoriesHandler.Instance.AddItemToInventory(groupItem, currentCandidate, OnAdded);
                                }
                                else
                                {
                                    InventoriesHandler.Instance.TransferItem(machineInventory, currentCandidate, (WorldObject)item, OnTransferred);
                                }
                            }
                            else
                            {
                                currentCandidate = null;
                                currentInventoryCandidates = null;
                                queue.Dequeue();

                                var position = new Vector3(
                                    UnityEngine.Random.Range(craftSpawn.bounds.min.x, craftSpawn.bounds.max.x),
                                    UnityEngine.Random.Range(craftSpawn.bounds.min.y, craftSpawn.bounds.max.y),
                                    UnityEngine.Random.Range(craftSpawn.bounds.min.z, craftSpawn.bounds.max.z)
                                );

                                if (item is GroupItem groupItem)
                                {
                                    Log("    Can't be deposited: " + toRecycleWo.GetId() + " - " + groupItem.id);
                                    WorldObjectsHandler.Instance.CreateAndDropOnFloor(groupItem, position, 0.6f);
                                }
                                else
                                {
                                    var wo = (WorldObject)item;
                                    Log("    Can't be deposited: " + toRecycleWo.GetId() + " - " + wo.GetId());

                                    WorldObjectsHandler.Instance.DropOnFloor(wo, position, 0.6f, dropSound: false);
                                }

                                continue;
                            }
                        }
                    }
                    if (--queueWip == 0)
                    {
                        break;
                    }
                }
            }

            internal void OnAdded(bool success, int id)
            {

                if (success)
                {
                    currentInventoryCandidates = null;
                    var g = (Group)queue.Dequeue();
                    Log("    " + toRecycleWo.GetId() + " - " + g.id + " deposited into " + currentCandidate.GetId());
                }
                else
                {
                    if (id != 0)
                    {
                        WorldObjectsHandler.Instance.DestroyWorldObject(id, false);
                    }
                }

                currentCandidate = null;
                Drain();
            }

            internal void OnTransferred(bool success)
            {
                if (success)
                {
                    currentInventoryCandidates = null;
                    var wo = (WorldObject)queue.Dequeue();
                    Log("    " + toRecycleWo.GetId() + " - " + wo.GetId() + " deposited into " + currentCandidate.GetId());
                }

                currentCandidate = null;
                Drain();
            }

            internal void OnComplete()
            {
                isRunning = false;
                InventoriesHandler.Instance.RemoveItemFromInventory(toRecycleWo, machineInventory, destroy: true);
                instance.Invoke("DisableCage", 1.5f);
                instance.GetComponent<ActionnableInteractive>()?.OnActionInteractive();
                Log("Done " + machineWorldObject.GetId());
            }
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(MachineDisintegrator), nameof(MachineDisintegrator.SetDisintegratorInventory))]
        static void Patch_MachineDisintegrator_SetDisintegratorInventory(
            MachineDisintegrator __instance
        )
        {
            var wo = __instance.GetComponent<WorldObjectAssociated>().GetWorldObject();
            if (wo.GetGroup().GetId().StartsWith("RecyclingMachine", StringComparison.Ordinal))
            {
                var planetHash = wo.GetPlanetHash();
                me.StartCoroutine(MachineDisintegrator_WaitForSecondInventory(__instance, planetHash, wo.GetPosition(), wo));
            }
        }

        static IEnumerator MachineDisintegrator_WaitForSecondInventory(
            MachineDisintegrator __instance, int planetHash, Vector3 pos, WorldObject machine)
        {
            bool requesting = false;
            while (fMachineDisintegratorSecondInventory(__instance) == null)
            {
                if (__instance.secondInventoryAssociatedProxy.IsSpawned)
                {
                    if (!requesting)
                    {
                        requesting = true;
                        __instance.secondInventoryAssociatedProxy.GetInventory((inv, _) =>
                        {
                            fMachineDisintegratorSecondInventory(__instance) = inv;
                        });
                    }
                }
                yield return null;
            }

            var inv = fMachineDisintegratorSecondInventory(__instance);
            StartClearMachineInventory(inv, planetHash, pos, machine);
        }

        static void StartClearMachineInventory(Inventory inventory, int planetHash, Vector3 center, WorldObject machine)
        {
            StopClearMachineInventory(inventory);
            var coroutine = me.StartCoroutine(ClearMachineInventory(inventory, planetHash, center, machine));
            clearCoroutines[inventory.GetId()] = coroutine;
            Log("StartClearMachineInventory: " + inventory.GetId() + ", planet " + planetHash);
        }

        static void StopClearMachineInventory(Inventory inventory)
        {
            if (inventory != null && clearCoroutines.Remove(inventory.GetId(), out var coroutine))
            {
                me.StopCoroutine(coroutine);
                Log("StopClearMachineInventory: " + inventory.GetId() + " (remaining: " + clearCoroutines.Count + ")");
            }
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(MachineDisintegrator), "OnDestroy")]
        static void MachineDisintegrator_OnDestroy(Inventory ____secondInventory)
        {
            StopClearMachineInventory(____secondInventory);
            if (____secondInventory != null)
            {
                CoroutineCoordinator.Remove(CreateRoutineId(____secondInventory.GetId()));
            }
        }

        static string CreateRoutineId(int id)
        {
            return "CheatRecyclerRemoteDeposit::ClearMachineInventory::" + id;
        }

        static IEnumerator ClearMachineInventory(Inventory _inventory, int planetHash, Vector3 center, WorldObject machine)
        {
            while (true)
            {
                // Server side is responsible for the transfer.
                if (modEnabled.Value && InventoriesHandler.Instance != null && InventoriesHandler.Instance.IsServer)
                {
                    var timeLimit = frameTimeLimit.Value / 1000d;
                    var items = fInventoryWorldObjectsInInventory(_inventory);

                    var routineId = CreateRoutineId(_inventory.GetId());
                    if (!CoroutineCoordinator.CanRun(routineId))
                    {
                        yield return null;
                        continue;
                    }

                    var sw = Stopwatch.StartNew();

                    Log("ClearMachineInventory begin: " + _inventory.GetId() + " on " + planetHash);

                    for (int i = items.Count - 1; i >= 0; i--)
                    {
                        if (i < items.Count)
                        {
                            var item = items[i];
                            var oreId = item.GetGroup().GetId();
                            var candidateInv = FindInventoryFor(oreId, center, planetHash).FirstOrDefault();
                            if (candidateInv != null)
                            {
                                Log("    Transfer of " + item.GetId() + " (" + item.GetGroup().GetId() + ") from " + _inventory.GetId() + " to " + candidateInv.GetId());
                                InventoriesHandler.Instance.TransferItem(_inventory, candidateInv, item);
                            }
                        }
                        // last yield check
                        var elaps2 = sw.Elapsed.TotalMilliseconds;
                        if (elaps2 > timeLimit)
                        {
                            CoroutineCoordinator.Yield(routineId, elaps2);
                            do
                            {
                                yield return null;
                            }
                            while (!CoroutineCoordinator.CanRun(routineId));
                            sw.Restart();
                        }
                    }
                    Log("ClearMachineInventory end: " + _inventory.GetId() + " on " + planetHash + ", elapsed " + sw.Elapsed.TotalMilliseconds);

                    // last yield check
                    var elaps3 = sw.Elapsed.TotalMilliseconds;
                    if (elaps3 > timeLimit)
                    {
                        CoroutineCoordinator.Yield(routineId, elaps3);
                        do
                        {
                            yield return null;
                        }
                        while (!CoroutineCoordinator.CanRun(routineId));
                    }

                }
                yield return new WaitForSeconds(autoRecyclePeriod.Value);
            }
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(UiWindowPause), nameof(UiWindowPause.OnQuit))]
        static void Patch_UiWindowPause_OnQuit()
        {
            CoroutineCoordinator.Clear();
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(BlackScreen), nameof(BlackScreen.DisplayLogoStudio))]
        static void Patch_BlackScreen_DisplayLogoStudio()
        {
            Patch_UiWindowPause_OnQuit();
        }
    }
}
