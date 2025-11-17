// Copyright (c) 2022-2025, David Karnok & Contributors
// Licensed under the Apache License, Version 2.0

using BepInEx;
using BepInEx.Configuration;
using SpaceCraft;
using HarmonyLib;
using System.Collections.Generic;
using System;
using BepInEx.Bootstrap;
using BepInEx.Logging;
using System.Collections;
using UnityEngine;
using System.Diagnostics;
using LibCommon;
using System.Linq;

namespace CheatMachineRemoteDeposit
{
    [BepInPlugin(modCheatMachineRemoteDepositGuid, "(Cheat) Machines Deposit Into Remote Containers", PluginInfo.PLUGIN_VERSION)]
    [BepInDependency(modCheatInventoryStackingGuid, BepInDependency.DependencyFlags.SoftDependency)]
    public class Plugin : BaseUnityPlugin
    {
        const string modCheatInventoryStackingGuid = "akarnokd.theplanetcraftermods.cheatinventorystacking";
        const string modCheatMachineRemoteDepositGuid = "akarnokd.theplanetcraftermods.cheatmachineremotedeposit";

        static readonly Version stackingMinVersion = new(1, 0, 1, 92);

        static ManualLogSource logger;

        static ConfigEntry<bool> modEnabled;
        static ConfigEntry<bool> debugMode;
        static ConfigEntry<bool> debugCoordinator;

        static readonly Dictionary<string, string> depositAliases = [];

        /// <summary>
        /// If the stacking mod is present, this will delegate to its apiIsFullStackedInventory call
        /// that considers the stackability of the target inventory with respect to the groupId to be added to i.
        /// </summary>
        static Func<Inventory, string, bool> InventoryCanAdd;

        static ConfigEntry<string> aliases;

        static ConfigEntry<string> dumpName;

        static ConfigEntry<int> period;

        static ConfigEntry<int> frameTimeLimit;

        static ConfigEntry<bool> balance;

        static ConfigEntry<bool> sortSources;

        static ConfigEntry<int> maxItems;

        static Plugin me;

        static AccessTools.FieldRef<MachineDisintegrator, Inventory> fMachineDisintegratorSecondInventory;

        static readonly Dictionary<Inventory, int> clearInventoryAndPlanetHash = [];

        static Coroutine clearCoroutine;

        static AccessTools.FieldRef<Inventory, List<WorldObject>> fInventoryWorldObjectsInInventory;

        void Awake()
        {
            me = this;

            LibCommon.BepInExLoggerFix.ApplyFix();

            // Plugin startup logic
            Logger.LogInfo($"Plugin is loaded!");

            logger = Logger;

            modEnabled = Config.Bind("General", "Enabled", true, "Is the mod enabled?");
            debugMode = Config.Bind("General", "DebugMode", false, "Produce detailed logs? (chatty)");
            debugCoordinator = Config.Bind("General", "DebugCoordinator", false, "Produce coordinator logs? (chatty)");

            aliases = Config.Bind("General", "Aliases", "", "A comma separated list of resourceId:aliasForId, for example, Iron:A,Cobalt:B,Uranim:C");

            dumpName = Config.Bind("General", "DumpName", "*Dump", "The name of the container to default deposit ores if no specific container was found.");

            period = Config.Bind("General", "Period", 5, "How often should the machines be emptied?");

            frameTimeLimit = Config.Bind("General", "FrameTimeLimit", 5000, "How much time is allowed to inspect the output inventories to avoid stutter, in microseconds");

            balance = Config.Bind("General", "Balance", false, "If true, the inventory with the least items in it will receive the produce, resulting in somewhat uniform filling of multiple inventories.");

            sortSources = Config.Bind("General", "SortSources", true, "If true, the machine's own inventory gets sorted before the deposition commences");

            maxItems = Config.Bind("General", "MaxItems", 30, "The maximum number of items per source inventory to consider.");

            ProcessAliases(aliases);

            InventoryCanAdd = (inv, gid) => !inv.IsFull();

            // If present, we interoperate with the stacking mod.
            // It has places to override the inventory into which items should be added in some machines.
            // However, when this mod looks for suitable inventories, we need to use stacking to correctly
            // determine if a stackable inventory can take an item.
            if (Chainloader.PluginInfos.TryGetValue(modCheatInventoryStackingGuid, out var info))
            {
                if (info.Metadata.Version < stackingMinVersion)
                {
                    Logger.LogError("Inventory Stacking v" + info.Metadata.Version + " is incompatible with Machine Remote Deposit v" + PluginInfo.PLUGIN_VERSION);
                    LibCommon.MainMenuMessage.Patch(new Harmony(modCheatMachineRemoteDepositGuid),
                            "!!! Error !!!\n\n"
                            + "The mod <color=#FFCC00>Machines Deposit Into Remote Containers</color> v" + PluginInfo.PLUGIN_VERSION
                            + "\n\n        is incompatible with"
                            + "\n\nthe mod <color=#FFCC00>Inventory Stacking</color> v" + info.Metadata.Version
                            + "\n\nPlease make sure you have the latest version of both mods."
                            );
                    return;
                }

                Logger.LogInfo("Mod " + modCheatInventoryStackingGuid + " found, overriding FindInventoryForGroupID.");

                var modType = info.Instance.GetType();

                // get the function that can tell if an inventory can't take one item of a provided group id
                var apiIsFullStackedInventory = (Func<Inventory, string, bool>)AccessTools.Field(modType, "apiIsFullStackedInventory").GetValue(null);
                // we need to logically invert it as we need it as "can-do"
                InventoryCanAdd = (inv, gid) => !apiIsFullStackedInventory(inv, gid);
            }
            else
            {
                Logger.LogInfo("Mod " + modCheatInventoryStackingGuid + " not found.");
            }

            fMachineDisintegratorSecondInventory = AccessTools.FieldRefAccess<MachineDisintegrator, Inventory>("_secondInventory");

            fInventoryWorldObjectsInInventory = AccessTools.FieldRefAccess<Inventory, List<WorldObject>>("_worldObjectsInInventory");

            CoroutineCoordinator.Init(LogCoord);

            LibCommon.HarmonyIntegrityCheck.Check(typeof(Plugin));
            Harmony.CreateAndPatchAll(typeof(Plugin));
        }

        static void ProcessAliases(ConfigEntry<string> cfe)
        {
            depositAliases.Clear();
            var s = cfe.Value.Trim();
            if (s.Length != 0)
            {
                var i = 0;
                foreach (var str in s.Split(','))
                {
                    var idalias = str.Split(':');
                    if (idalias.Length != 2)
                    {
                        logger.LogWarning("Wrong alias @ index " + i + " value " + str);
                    }
                    else
                    {
                        depositAliases[idalias[0].Trim()] = idalias[1].ToLowerInvariant();
                        Log("Alias " + idalias[0] + " -> " + idalias[1]);
                    }
                    i++;
                }
            }
        }

        public static void OnModConfigChanged(ConfigEntryBase _)
        {
            ProcessAliases(aliases);
        }

        static void Log(string s)
        {
            if (debugMode.Value)
            {
                logger.LogInfo(s);
            }
        }
        static void LogCoord(string s)
        {
            if (debugCoordinator.Value)
            {
                logger.LogInfo(s);
            }
        }

        /// <summary>
        /// When the vanilla sets up a Machine Generator, we have to launch
        /// the inventory cleaning routine to unclog it.
        /// Otherwise a full inventory will stop the ore generation in the
        /// MachineGenerator.TryToGenerate method.
        /// </summary>
        /// <param name="__instance"></param>
        /// <param name="inventory"></param>
        [HarmonyPostfix]
        [HarmonyPatch(typeof(MachineGenerator), nameof(MachineGenerator.SetGeneratorInventory))]
        static void MachineGenerator_SetGeneratorInventory(
            Inventory inventory,
            WorldObject ____worldObject)
        {
            StartClearMachineInventory(inventory, ____worldObject.GetPlanetHash());
        }

        /// <summary>
        /// When the speed multiplier is changed, the vanilla code
        /// cancels all coroutines and starts a new generation coroutine.
        /// We have to also restart our inventory cleaning routine.
        /// </summary>
        /// <param name="__instance"></param>
        /// <param name="____inventory"></param>
        [HarmonyPostfix]
        [HarmonyPatch(typeof(MachineGenerator), nameof(MachineGenerator.AddToGenerationSpeedMultiplier))]
        static void MachineGenerator_AddToGenerationSpeedMultiplier(
            Inventory ____inventory,
            WorldObject ____worldObject
        )
        {
            StartClearMachineInventory(
                ____inventory, ____worldObject.GetPlanetHash());
        }

        static IEnumerator ClearMachineInventory()
        {
            const string routineId = "CheatMachineRemoteDeposit::ClearMachineInventory";
            while (true)
            {
                // Server side is responsible for the transfer.
                var invh = InventoriesHandler.Instance;
                if (modEnabled.Value && invh != null && invh.IsServer)
                {
                    var timeLimit = frameTimeLimit.Value / 1000d;
                    while (!CoroutineCoordinator.CanRun(routineId))
                    {
                        yield return null;
                    }

                    var sw0 = Stopwatch.StartNew();
                    var skips = 0;
                    Log("ClearMachineInventory begin - " + clearInventoryAndPlanetHash.Count);

                    var sw = Stopwatch.StartNew();
                    var callback = new CallbackWaiter();
                    Action<bool> callbackDone = callback.Done;

                    var transferFailures = new Dictionary<Inventory, HashSet<string>>();

                    var clearCopy = new List<Inventory>(clearInventoryAndPlanetHash.Keys);
                    clearCopy.Sort(balancedSorter);
                    foreach (var _inventory in clearCopy)
                    {
                        if (invh == null || invh.GetInventoryById(_inventory.GetId()) == null)
                        {
                            continue;
                        }
                        if (!clearInventoryAndPlanetHash.TryGetValue(_inventory, out var planetHash))
                        {
                            continue;
                        }
                        var items = fInventoryWorldObjectsInInventory(_inventory);
                        var originalCount = items.Count;
                        if (originalCount != 0)
                        {
                            if (sortSources.Value)
                            {
                                _inventory.AutoSort();
                            }
                            var elaps0 = sw.Elapsed.TotalMilliseconds;
                            if (elaps0 > timeLimit)
                            {
                                skips++;
                                Log("      --- yield (inv) --- " + elaps0);
                                CoroutineCoordinator.Yield(routineId, elaps0);
                                do
                                {
                                    yield return null;
                                }
                                while (!CoroutineCoordinator.CanRun(routineId));
                                transferFailures.Clear();
                                sw.Restart();
                            }

                            Log("  Begin: " + _inventory.GetId() + " on " + planetHash + " count " + originalCount);

                            var lastOreId = "";
                            var lastPlanetHash = 0;
                            var mainList = default(List<Inventory>);
                            var dumpList = default(List<Inventory>);

                            int j = maxItems.Value;
                            for (int i = items.Count - 1; i >= 0 && j >= 0; i--, j--)
                            {
                                if (i < items.Count)
                                {
                                    var elaps1 = sw.Elapsed.TotalMilliseconds;
                                    if (elaps1 > timeLimit)
                                    {
                                        skips++;
                                        Log("      --- yield (item) --- " + elaps1);
                                        CoroutineCoordinator.Yield(routineId, elaps1);
                                        do
                                        {
                                            yield return null;
                                        }
                                        while (!CoroutineCoordinator.CanRun(routineId));
                                        transferFailures.Clear();
                                        sw.Restart();
                                    }

                                    var item = items[i];
                                    var oreId = item.GetGroup().GetId();
                                    if (lastOreId != oreId || lastPlanetHash != planetHash)
                                    {
                                        lastOreId = oreId;
                                        lastPlanetHash = planetHash;
                                        (mainList, dumpList) = FindInventories(oreId, planetHash);
                                    }
                                    if (balance.Value)
                                    {
                                        mainList.Sort(balancedSorter);
                                        dumpList.Sort(balancedSorter);
                                    }

                                    bool found = false;
                                    foreach (var candidate in mainList.Concat(dumpList))
                                    {
                                        if (invh != null && invh.GetInventoryById(candidate.GetId()) != null)
                                        {
                                            var elaps2 = sw.Elapsed.TotalMilliseconds;
                                            if (elaps2 > timeLimit)
                                            {
                                                skips++;
                                                Log("      --- yield (candidate) --- " + elaps2);
                                                CoroutineCoordinator.Yield(routineId, elaps2);
                                                do
                                                {
                                                    yield return null;
                                                }
                                                while (!CoroutineCoordinator.CanRun(routineId));
                                                transferFailures.Clear();
                                                sw.Restart();
                                            }

                                            transferFailures.TryGetValue(candidate, out var set);

                                            if (set == null || !set.Contains(oreId))
                                            {
                                                callback.Reset();
                                                InventoriesHandler.Instance.TransferItem(_inventory, candidate, item, callbackDone);
                                                while (!callback.IsDone)
                                                {
                                                    skips++;
                                                    Log("      --- yield (transfer) ---");
                                                    CoroutineCoordinator.Yield(routineId, 0);
                                                    do
                                                    {
                                                        yield return null;
                                                    }
                                                    while (!CoroutineCoordinator.CanRun(routineId));
                                                    transferFailures.Clear();
                                                }
                                                if (callback.IsSuccess)
                                                {
                                                    Log("    Transfer of " + item.GetId() + " (" + item.GetGroup().GetId() + ") from " + _inventory.GetId() + " to " + candidate.GetId());
                                                    found = true;
                                                    break;
                                                }
                                                else
                                                {
                                                    if (set == null)
                                                    {
                                                        set = [];
                                                        transferFailures[candidate] = set;
                                                    }
                                                    set.Add(oreId);
                                                }
                                            }
                                        }
                                    }

                                    if (!found)
                                    {
                                        Log("    Transfer of " + item.GetId() + " (" + item.GetGroup().GetId() + ") from " + _inventory.GetId() + " failed due to lack of inventory space");
                                    }
                                }
                            }
                            Log("  End: " + _inventory.GetId() + " on " + planetHash + " count " + originalCount + ", elapsed " + sw.Elapsed.TotalMilliseconds + ", skips " + skips);
                        }
                    }
                    Log("ClearMachineInventory end - " + clearInventoryAndPlanetHash.Count + " in " + sw0.Elapsed.TotalMilliseconds + " skips " + skips);

                    // last yield check
                    var elaps3 = sw.Elapsed.TotalMilliseconds;
                    if (elaps3 > timeLimit)
                    {
                        Log("      --- yield (final) --- " + elaps3);
                        CoroutineCoordinator.Yield(routineId, elaps3);
                        do
                        {
                            yield return null;
                        }
                        while (!CoroutineCoordinator.CanRun(routineId));
                    }
                }
                yield return new WaitForSeconds(period.Value);
            }
        }


        static (List<Inventory> main, List<Inventory> dump) FindInventories(string oreId, int planetHash)
        {
            var mainList = new List<Inventory>();
            var dumpList = new List<Inventory>();

            var invh = InventoriesHandler.Instance;
            if (invh != null)
            {
                var sw = Stopwatch.StartNew();


                var containerNameFilter = "*" + oreId;
                if (depositAliases.TryGetValue(oreId, out var alias))
                {
                    containerNameFilter = alias;
                    Log("    Ore " + oreId + " -> Alias " + alias);
                }
                var dumpContainerName = dumpName.Value;
                var worldObjects = WorldObjectsHandler.Instance.GetConstructedWorldObjects();

                // Log("  Begin FindInventories - " + worldObjects.Count + " constructs");


                foreach (var constructs in worldObjects)
                {
                    if (constructs != null
                        && constructs.GetGroup().GetId().StartsWith("Container", StringComparison.Ordinal)
                        && constructs.HasLinkedInventory()
                        && constructs.GetPlanetHash() == planetHash
                        )
                    {
                        var txt = constructs.GetText();
                        if (txt != null)
                        {
                            if (txt.Contains(containerNameFilter, StringComparison.InvariantCultureIgnoreCase))
                            {
                                var inv = invh.GetInventoryById(constructs.GetLinkedInventoryId());
                                mainList.Add(inv);
                            }
                            if (txt.Contains(dumpContainerName, StringComparison.InvariantCultureIgnoreCase))
                            {
                                var inv = invh.GetInventoryById(constructs.GetLinkedInventoryId());
                                dumpList.Add(inv);
                            }
                        }
                    }
                }
                Log("    FindInventories: main = " + mainList.Count + ", dump = " + dumpList.Count + " in " + sw.Elapsed.TotalMilliseconds);
            }
            return (mainList, dumpList);
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(MachineDisintegrator), nameof(MachineDisintegrator.SetDisintegratorInventory))]
        static void Patch_MachineDisintegrator_SetDisintegratorInventory(
            MachineDisintegrator __instance
        )
        {
            var wo = __instance.GetComponent<WorldObjectAssociated>().GetWorldObject();
            var gid = wo.GetGroup().GetId();
            if (gid.StartsWith("OreBreaker", StringComparison.Ordinal)
                || gid.StartsWith("DetoxificationMachine", StringComparison.Ordinal))
            {
                var planetHash = wo.GetPlanetHash();

                me.StartCoroutine(MachineDisintegrator_WaitForSecondInventory(__instance, planetHash));
            }
        }

        static IEnumerator MachineDisintegrator_WaitForSecondInventory(
            MachineDisintegrator __instance, int planetHash)
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
            StartClearMachineInventory(inv, planetHash);
        }

        static void StartClearMachineInventory(Inventory inventory, int planetHash)
        {
            if (inventory == null)
            {
                return;
            }
            clearInventoryAndPlanetHash[inventory] = planetHash;

            if (clearInventoryAndPlanetHash.Count == 1)
            {
                clearCoroutine = me.StartCoroutine(ClearMachineInventory());
            }
            Log("StartClearMachineInventory: " + inventory.GetId() + ", delay " + period.Value + ", planet " + planetHash);
        }

        static void StopClearMachineInventory(Inventory inventory) 
        {
            if (inventory == null)
            {
                return;
            }
            if (clearInventoryAndPlanetHash.Remove(inventory))
            {
                Log("StopClearMachineInventory: " + inventory.GetId() + " (remaining: " + clearInventoryAndPlanetHash.Count + ")");
            }
            if (clearInventoryAndPlanetHash.Count == 0)
            {
                me.StopCoroutine(clearCoroutine);
            }
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(MachineGenerator), "OnDestroy")]
        static void MachineGenerator_OnDestroy(Inventory ____inventory)
        {
            StopClearMachineInventory(____inventory);
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(MachineDisintegrator), "OnDestroy")]
        static void MachineDisintegrator_OnDestroy(Inventory ____secondInventory)
        {
            StopClearMachineInventory(____secondInventory);
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

        static readonly Comparison<Inventory> balancedSorter = (a, b) => a.GetInsideWorldObjects().Count.CompareTo(b.GetInsideWorldObjects().Count);
    }
}
