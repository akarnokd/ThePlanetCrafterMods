// Copyright (c) 2022-2024, David Karnok & Contributors
// Licensed under the Apache License, Version 2.0

using BepInEx;
using SpaceCraft;
using HarmonyLib;
using BepInEx.Configuration;
using UnityEngine.InputSystem;
using System.Collections.Generic;
using UnityEngine;
using System.Collections;
using BepInEx.Logging;
using System.Linq;
using System;

namespace CheatAutoStore
{
    [BepInPlugin("akarnokd.theplanetcraftermods.cheatautostore", "(Cheat) Auto Store", PluginInfo.PLUGIN_VERSION)]
    public class Plugin : BaseUnityPlugin
    {

        static ConfigEntry<bool> modEnabled;

        static ConfigEntry<bool> debugMode;

        static ConfigEntry<int> range;

        static ConfigEntry<string> includeList;

        static ConfigEntry<string> excludeList;

        static ConfigEntry<string> key;

        static InputAction storeAction;

        static bool inventoryStoringActive;

        static ManualLogSource logger;

        public void Awake()
        {
            LibCommon.BepInExLoggerFix.ApplyFix();

            // Plugin startup logic
            Logger.LogInfo($"Plugin is loaded!");

            logger = Logger;

            modEnabled = Config.Bind("General", "Enabled", true, "Is this mod enabled");
            debugMode = Config.Bind("General", "DebugMode", false, "Enable detailed logging? Chatty!");
            range = Config.Bind("General", "Range", 20, "The range to look for containers within.");
            includeList = Config.Bind("General", "IncludeList", "", "The comma separated list of case-sensitive item ids to include only. If empty, all items are considered except the those listed in ExcludeList.");
            excludeList = Config.Bind("General", "ExcludeList", "", "The comma separated list of case-sensitive item ids to exclude. Only considered if IncludeList is empty.");
            key = Config.Bind("General", "Key", "<Keyboard>/K", "The input action shortcut to trigger the storing of items.");

            if (!key.Value.Contains("<"))
            {
                key.Value = "<Keyboard>/" + key.Value;
            }
            storeAction = new InputAction(name: "Store Items", binding: key.Value);
            storeAction.Enable();

            Harmony.CreateAndPatchAll(typeof(Plugin));
        }

        static void Log(object message)
        {
            if (debugMode.Value)
            {
                logger.LogInfo(message);
            }
        }

        public void Update()
        {
            if (!modEnabled.Value)
            {
                return;
            }
            var pm = Managers.GetManager<PlayersManager>();
            if (pm == null)
            {
                return;
            }
            var ac = pm.GetActivePlayerController();
            if (ac == null)
            {
                return;
            }
            var wh = Managers.GetManager<WindowsHandler>();
            if (wh == null)
            {
                return;
            }
            if (wh.GetHasUiOpen())
            {
                return;
            }
            if (!storeAction.WasPressedThisFrame())
            {
                return;
            }

            if (inventoryStoringActive)
            {
                Log("Storing in progress");
                return;
            }

            Log("Begin storing");
            inventoryStoringActive = true;

            var ppos = ac.transform.position;

            List<int> candidateInventoryIds = [];
            foreach (var wo in WorldObjectsHandler.Instance.GetConstructedWorldObjects())
            {
                var grid = wo.GetGroup().GetId();

                if (grid.StartsWith("Container")
                    && wo.GetLinkedInventoryId() != 0
                    && Vector3.Distance(ppos, wo.GetPosition()) <= range.Value
                )
                {
                    candidateInventoryIds.Add(wo.GetLinkedInventoryId());
                }
            }

            Log("  Containers in range: " + candidateInventoryIds.Count);

            var backpackInv = ac.GetPlayerBackpack().GetInventory();

            var candidateInv = new List<Inventory>();

            foreach (var iid in candidateInventoryIds)
            {
                InventoriesHandler.Instance.GetInventoryById(iid, candidateInv.Add);
            }

            StartCoroutine(WaitForInventories(backpackInv, candidateInv, candidateInventoryIds.Count));
        }

        static IEnumerator WaitForInventories(Inventory backpackInv, List<Inventory> candidateInventory, int n)
        {
            Log("  Waiting for GetInventoryById callbacks: " + candidateInventory.Count + " of " + n);
            while (candidateInventory.Count != n)
            {
                yield return null;
            }

            var includeGroupIds = includeList.Value.Split(',').Select(v => v.Trim()).Where(v => v.Length != 0).ToHashSet();
            var excludeGroupIds = excludeList.Value.Split(',').Select(v => v.Trim()).Where(v => v.Length != 0).ToHashSet();

            List<WorldObject> backpackWos = [..backpackInv.GetInsideWorldObjects()];
            Log("  Begin enumerating the backpack: " + backpackWos.Count);

            var deposited = 0;
            var excluded = 0;

            foreach (var wo in backpackWos)
            {
                Log("  Begin for " + wo.GetId() + " (" + wo.GetGroup().id + ")");

                var group = wo.GetGroup();
                var gid = group.GetId();
                if (includeGroupIds.Count == 0)
                {
                    if (excludeGroupIds.Contains(gid))
                    {
                        Log("    Excluding via ExcludeList");
                        excluded++;
                        continue;
                    }
                }
                else if (!includeGroupIds.Contains(gid))
                {
                    Log("    Excluding via IncludeList");
                    excluded++;
                    continue;
                }

                var foundInventoryForWo = false;

                foreach (var inv in candidateInventory)
                {
                    if (inv != null)
                    {
                        var foundCandidate = false;
                        foreach (var wo2 in inv.GetInsideWorldObjects())
                        {
                            if (wo2.GetGroup().id == gid)
                            {
                                foundCandidate = true;
                                break;
                            }
                        }

                        if (foundCandidate)
                        {
                            var tch = new TransferCompletionHandler();

                            InventoriesHandler.Instance.TransferItem(backpackInv, inv, wo, success =>
                            {
                                tch.success = success;
                                tch.done = true;
                            });

                            while (!tch.done)
                            {
                                yield return null;
                            }

                            if (tch.success)
                            {
                                Log("    Deposited into " + inv.GetId());
                                Managers.GetManager<DisplayersHandler>()
                                    ?.GetInformationsDisplayer()
                                    ?.AddInformation(2f, Readable.GetGroupName(group), DataConfig.UiInformationsType.OutInventory, group.GetImage());

                                foundInventoryForWo = true;
                                deposited++;
                                break;
                            }
                        }
                    }
                }

                if (!foundInventoryForWo)
                {
                    Log("   Unable to find an inventory");
                }
            }
            Log("  Done.");
            inventoryStoringActive = false;
            Managers.GetManager<BaseHudHandler>()?.DisplayCursorText("", 5f, "Auto Store: " + deposited + " / " + backpackWos.Count + " deposited. " + excluded + " excluded.");
        }

        internal class TransferCompletionHandler
        {
            internal bool done;
            internal bool success;
        }
    }
}
