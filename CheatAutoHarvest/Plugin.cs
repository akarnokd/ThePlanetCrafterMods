// Copyright (c) 2022-2024, David Karnok & Contributors
// Licensed under the Apache License, Version 2.0

using BepInEx;
using SpaceCraft;
using HarmonyLib;
using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using BepInEx.Configuration;
using System;
using BepInEx.Logging;
using Unity.Netcode;
using LibCommon;

namespace CheatAutoHarvest
{
    [BepInPlugin(modCheatAutoHarvest, "(Cheat) Automatically Harvest Food n Algae", PluginInfo.PLUGIN_VERSION)]
    public class Plugin : BaseUnityPlugin
    {
        const string modCheatAutoHarvest = "akarnokd.theplanetcraftermods.cheatautoharvest";

        static Plugin me;

        static ManualLogSource logger;
        static ConfigEntry<bool> debugAlgae;
        static ConfigEntry<bool> debugFood;

        static ConfigEntry<bool> harvestAlgae;
        static ConfigEntry<bool> harvestFood;

        static readonly Dictionary<string, ConfigEntry<string>> depositAliases = [];

        static Coroutine machineGrowerRoutine;

        private void Awake()
        {
            LibCommon.BepInExLoggerFix.ApplyFix();

            // Plugin startup logic
            Logger.LogInfo($"Plugin is loaded!");

            me = this;

            logger = Logger;
            harvestAlgae = Config.Bind("General", "HarvestAlgae", true, "Enable auto harvesting for algae.");
            harvestFood = Config.Bind("General", "HarvestFood", true, "Enable auto harvesting for food.");
            debugAlgae = Config.Bind("General", "DebugAlgae", false, "Enable debug log for algae (chatty!)");
            debugFood = Config.Bind("General", "DebugFood", false, "Enable debug log for food (chatty!)");

            depositAliases["Algae1Seed"] = Config.Bind("General", "AliasAlgae", "*Algae1Seed", "The container name to put algae into.");
            depositAliases["Vegetable0Growable"] = Config.Bind("General", "AliasEggplant", "*Vegetable0Growable", "The container name to put eggplant into.");
            depositAliases["Vegetable1Growable"] = Config.Bind("General", "AliasSquash", "*Vegetable1Growable", "The container name to put squash into.");
            depositAliases["Vegetable2Growable"] = Config.Bind("General", "AliasBeans", "*Vegetable2Growable", "The container name to put beans into.");
            depositAliases["Vegetable3Growable"] = Config.Bind("General", "AliasMushroom", "*Vegetable3Growable", "The container name to put mushroom into.");
            depositAliases["CookCocoaGrowable"] = Config.Bind("General", "AliasCocoa", "*CookCocoaGrowable", "The container name to put cocoa into.");
            depositAliases["CookWheatGrowable"] = Config.Bind("General", "AliasWheat", "*CookWheatGrowable", "The container name to put wheat into.");

            if (debugAlgae.Value || debugFood.Value)
            {
                foreach (var kv in depositAliases) 
                {
                    Logger.LogInfo("  Alias " + kv.Key + " -> " + kv.Value.Value);
                }
            }

            LibCommon.HarmonyIntegrityCheck.Check(typeof(Plugin));
            var harmony = Harmony.CreateAndPatchAll(typeof(Plugin));
            LibCommon.ModPlanetLoaded.Patch(harmony, modCheatAutoHarvest, _ => PlanetLoader_HandleDataAfterLoad());
        }

        static void LogAlgae(string s)
        {
            if (debugAlgae.Value)
            {
                logger.LogInfo(s);
            }
        }
        static void LogFood(string s)
        {
            if (debugFood.Value)
            {
                logger.LogInfo(s);
            }
        }

        static void PlanetLoader_HandleDataAfterLoad()
        {
            if (machineGrowerRoutine != null)
            {
                me.StopCoroutine(machineGrowerRoutine);
                machineGrowerRoutine = null;
            }
            machineGrowerRoutine = me.StartCoroutine(HarvestLoop());
            LibCommon.SaveModInfo.Save();
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(UiWindowPause), nameof(UiWindowPause.OnQuit))]
        static void UiWindowPause_OnQuit()
        {
            if (machineGrowerRoutine != null)
            {
                me.StopCoroutine(machineGrowerRoutine);
                machineGrowerRoutine = null;
            }
            // So they don't have stale items between reloads
            WorldObjectsHandler.Instance.GetPickablesByDronesWorldObjects().Clear();
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(BlackScreen), nameof(BlackScreen.DisplayLogoStudio))]
        static void BlackScreen_DisplayLogoStudio()
        {
            UiWindowPause_OnQuit();
        }

        static IEnumerator HarvestLoop()
        {
            var wait = new WaitForSeconds(2.5f);

            for (; ; )
            {
                if ((harvestFood.Value || harvestAlgae.Value)
                    && (NetworkManager.Singleton?.IsServer ?? false)
                    && Managers.GetManager<PlayersManager>()?.GetActivePlayerController() != null
                )
                {
                    DoHarvest();
                }
                yield return wait;
            }
        }

        static void DoHarvest()
        {
            var pickables = WorldObjectsHandler.Instance.GetPickablesByDronesWorldObjects();
            foreach (var wo in new List<WorldObject>(pickables))
            {
                if (wo.GetIsPlaced())
                {
                    var gid = wo.GetGroup().GetId();

                    Action<string> log = gid.StartsWith("Algae") ? LogAlgae : LogFood;

                    if ((gid.StartsWith("Algae") && gid.EndsWith("Seed") && harvestAlgae.Value)
                        || (gid.StartsWith("Vegetable") && gid.EndsWith("Growable") && harvestFood.Value)
                        || (gid.StartsWith("Cook") && gid.EndsWith("Growable") && harvestFood.Value)
                    )
                    {
                        var ag = wo.GetGameObject().AsNullable()?.GetComponentInChildren<ActionGrabable>();

                        if (ag != null && !GrabChecker.IsOnDisplay(ag) && ag.GetCanGrab())
                        {
                            var wo1 = wo;
                            new DeferredDepositor()
                            {
                                inventory = FindInventoryFor(gid),
                                worldObject = wo,
                                logger = log,
                                OnDepositSuccess = () =>
                                {
                                    var call = ag.grabedEvent;
                                    ag.grabedEvent = null;
                                    call?.Invoke(wo1, false);
                                }
                            }.Drain();
                        }
                    }
                    else
                    {
                        log("Not grabbable: " + DebugWorldObject(wo));
                    }
                }
            }
        }

        static IEnumerator<Inventory> FindInventoryFor(string gid)
        {
            var containerName = gid;
            if (depositAliases.TryGetValue(gid, out var alias))
            {
                containerName = alias.Value;
            }
            foreach (var candidate in WorldObjectsHandler.Instance.GetConstructedWorldObjects())
            {
                var txt = candidate.GetText();
                if (txt != null && txt.Contains(containerName, StringComparison.InvariantCultureIgnoreCase))
                {
                    var iid = candidate.GetLinkedInventoryId();
                    if (iid != 0)
                    {
                        var inv = InventoriesHandler.Instance.GetInventoryById(iid);
                        if (inv != null)
                        {
                            yield return inv;
                        }
                    }
                }
            }
        }

        static string DebugWorldObject(WorldObject wo)
        {
            var str = wo.GetId() + ", " + wo.GetGroup().GetId();
            var txt = wo.GetText();
            if (!string.IsNullOrEmpty(txt))
            {
                str += ", \"" + txt + "\"";
            }
            str += ", " + (wo.GetIsPlaced() ? wo.GetPosition() : "");
            return str;
        }

        internal class DeferredDepositor
        {
            internal IEnumerator<Inventory> inventory;
            internal WorldObject worldObject;
            internal Action<string> logger;
            internal Action OnDepositSuccess;

            Inventory current;
            int wip;

            internal void Drain()
            {
                if (wip++ != 0)
                {
                    return;
                }

                for (; ; )
                {
                    if (current == null)
                    {
                        if (inventory.MoveNext())
                        {
                            current = inventory.Current;
                        }
                        else
                        {
                            logger?.Invoke("No suitable non-full inventory found for " + DebugWorldObject(worldObject));
                            break;
                        }
                        // FIXME grabbed: true ???
                        InventoriesHandler.Instance.AddWorldObjectToInventory(worldObject, inventory.Current, grabbed: false, OnInventoryCallback);
                    }

                    if (--wip == 0)
                    {
                        break;
                    }
                }
            }

            void OnInventoryCallback(bool success)
            {
                if (success)
                {
                    logger?.Invoke("Inventory " + current.GetId() + " <- " + DebugWorldObject(worldObject));
                    current = null;
                    OnDepositSuccess?.Invoke();
                }
                else
                {
                    current = null;
                    Drain();
                }
            }
        }
    }
}
