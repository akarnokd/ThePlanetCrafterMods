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

        static readonly Dictionary<string, string> depositAliases = [];

        /// <summary>
        /// If the stacking mod is present, this will delegate to its apiIsFullStackedInventory call
        /// that considers the stackability of the target inventory with respect to the groupId to be added to i.
        /// </summary>
        static Func<Inventory, string, bool> InventoryCanAdd;

        static ConfigEntry<string> aliases;

        static ConfigEntry<string> dumpName;

        static ConfigEntry<int> period;

        static Plugin me;

        static readonly Dictionary<int, Coroutine> clearCoroutines = [];

        static AccessTools.FieldRef<MachineDisintegrator, Inventory> fMachineDisintegratorSecondInventory;

        static long coroutineIndex;

        void Awake()
        {
            me = this;

            LibCommon.BepInExLoggerFix.ApplyFix();

            // Plugin startup logic
            Logger.LogInfo($"Plugin is loaded!");

            logger = Logger;

            modEnabled = Config.Bind("General", "Enabled", true, "Is the mod enabled?");
            debugMode = Config.Bind("General", "DebugMode", false, "Produce detailed logs? (chatty)");

            aliases = Config.Bind("General", "Aliases", "", "A comma separated list of resourceId:aliasForId, for example, Iron:A,Cobalt:B,Uranim:C");

            dumpName = Config.Bind("General", "DumpName", "*Dump", "The name of the container to default deposit ores if no specific container was found.");

            period = Config.Bind("General", "Period", 5, "How often should the machines be emptied?");

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

        static IEnumerator ClearMachineInventory(Inventory _inventory, int planetHash)
        {
            long n = ++coroutineIndex % 60;
            while (--n > 0)
            {
                yield return null;
            }
            while (true)
            {
                // Server side is responsible for the transfer.
                if (modEnabled.Value && InventoriesHandler.Instance != null && InventoriesHandler.Instance.IsServer)
                {
                    var items = _inventory.GetInsideWorldObjects();
                    var originalCount = items.Count;
                    if (originalCount != 0)
                    {
                        var sw = Stopwatch.StartNew();
                        Log("ClearMachineInventory begin: " + _inventory.GetId() + " on " + planetHash + " count " + originalCount);

                        for (int i = items.Count - 1; i >= 0; i--)
                        {
                            if (i < items.Count)
                            {
                                var item = items[i];
                                var oreId = item.GetGroup().GetId();
                                var candidateInv = FindInventoryForOre(oreId, planetHash);
                                if (candidateInv != null)
                                {
                                    Log("    Transfer of " + item.GetId() + " (" + item.GetGroup().GetId() + ") from " + _inventory.GetId() + " to " + candidateInv.GetId());
                                    InventoriesHandler.Instance.TransferItem(_inventory, candidateInv, item);
                                }
                            }
                            if (i != 0)
                            {
                                yield return null;
                            }
                        }
                        Log("ClearMachineInventory end: " + _inventory.GetId() + " on " + planetHash + " count " + originalCount + ", elapsed " + sw.Elapsed.TotalMilliseconds);
                    }
                }
                yield return new WaitForSeconds(period.Value);
            }
        }

        static Inventory FindInventoryForOre(string oreId, int planetHash)
        {
            var containerNameFilter = "*" + oreId.ToLowerInvariant();
            if (depositAliases.TryGetValue(oreId, out var alias))
            {
                containerNameFilter = alias;
                Log("    Ore " + oreId + " -> Alias " + alias);
            }
            var dumpContainerName = dumpName.Value;
            var dumpInventory = default(Inventory);
            var dumpInventoryName = "";

            var sw = Stopwatch.StartNew();
            foreach (var constructs in WorldObjectsHandler.Instance.GetConstructedWorldObjects())
            {
                if (constructs != null 
                    && constructs.GetGroup().GetId().StartsWith("Container") 
                    && constructs.HasLinkedInventory()
                    && constructs.GetPlanetHash() == planetHash
                    )
                {
                    string txt = constructs.GetText();
                    if (txt != null)
                    {
                        if (txt.Contains(containerNameFilter, StringComparison.InvariantCultureIgnoreCase))
                        {
                            Inventory candidateInventory = InventoriesHandler.Instance.GetInventoryById(constructs.GetLinkedInventoryId());
                            if (candidateInventory != null && InventoryCanAdd(candidateInventory, oreId))
                            {
                                Log("    Found Inventory: " + candidateInventory.GetId() + " \"" + txt + "\" in " + sw.Elapsed.TotalMilliseconds);
                                return candidateInventory;
                            }
                        }
                        else if (dumpInventory == null && txt.Contains(dumpContainerName, StringComparison.InvariantCultureIgnoreCase))
                        {
                            var invDummy = InventoriesHandler.Instance.GetInventoryById(constructs.GetLinkedInventoryId());
                            if (invDummy != null && InventoryCanAdd(invDummy, oreId))
                            {
                                dumpInventory = invDummy;
                                dumpInventoryName = txt;
                            }
                        }
                    }
                }
            }
            // Log("    No suitable inventory found for " + oreId + " under " + sw.Elapsed.TotalMilliseconds);
            if (dumpInventory != null)
            {
                Log("    Found Dump Inventory: " + dumpInventory.GetId() + " \"" + dumpInventoryName + "\" in " + sw.Elapsed.TotalMilliseconds);
            }
            return dumpInventory;
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(MachineDisintegrator), nameof(MachineDisintegrator.SetDisintegratorInventory))]
        static void Patch_MachineDisintegrator_SetDisintegratorInventory(
            MachineDisintegrator __instance
        )
        {
            var wo = __instance.GetComponent<WorldObjectAssociated>().GetWorldObject();
            if (wo.GetGroup().GetId().StartsWith("OreBreaker", StringComparison.InvariantCulture))
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
            StopClearMachineInventory(inventory);
            var coroutine = me.StartCoroutine(ClearMachineInventory(inventory, planetHash));
            clearCoroutines[inventory.GetId()] = coroutine;
            Log("StartClearMachineInventory: " + inventory.GetId() + ", delay " + period.Value + ", planet " + planetHash);
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
    }
}
