// Copyright (c) 2022-2024, David Karnok & Contributors
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

        static ConfigEntry<string> defaultDepositAlias;

        static ConfigEntry<string> customDepositAliases;

        static ConfigEntry<int> autoRecyclePeriod;

        static ConfigEntry<int> maxRange;

        static readonly Dictionary<string, string> aliasMap = [];

        static bool isRunning;

        private void Awake()
        {
            LibCommon.BepInExLoggerFix.ApplyFix();

            // Plugin startup logic
            Logger.LogInfo($"Plugin is loading!");

            logger = Logger;

            modEnabled = Config.Bind("General", "Enabled", true, "Is the mod enabled?");
            debugMode = Config.Bind("General", "DebugMode", false, "Enable debug mode with detailed logging (chatty!)");
            defaultDepositAlias = Config.Bind("General", "DefaultDepositAlias", "*Recycled", "The name of the container to deposit resources not explicity mentioned in CustomDepositAliases.");
            customDepositAliases = Config.Bind("General", "CustomDepositAliases", "", "Comma separated list of resource_id:alias to deposit into such named containers");
            autoRecyclePeriod = Config.Bind("General", "AutoRecyclePeriod", 5, "How often to auto-recycle, seconds. Zero means no auto-recycle.");
            maxRange = Config.Bind("General", "MaxRange", 20, "The maximum range to look for containers within. Zero means unlimited range.");

            ParseAliasConfig();

            LibCommon.HarmonyIntegrityCheck.Check(typeof(Plugin));
            var h = Harmony.CreateAndPatchAll(typeof(Plugin));

            ModNetworking.Init(modGuid, Logger);
            ModNetworking.Patch(h);
            ModNetworking.RegisterFunction(FunctionRequestRecycle, OnClientAction);

            Logger.LogInfo($"Plugin is loaded!");
        }

        static void ParseAliasConfig()
        {
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

        static IEnumerable<Inventory> FindInventoryFor(string gid, Vector3 center)
        {
            if (!aliasMap.TryGetValue(gid, out var name))
            {
                name = defaultDepositAlias.Value.ToLowerInvariant();
            }

            var range = maxRange.Value;

            foreach (var wo in WorldObjectsHandler.Instance.GetConstructedWorldObjects())
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

        [HarmonyPrefix]
        [HarmonyPatch(typeof(ActionRecycle), nameof(ActionRecycle.OnAction))]
        static bool ActionRecycle_OnAction(
            ActionRecycle __instance, 
            Collider ___craftSpawn,
            GameObject ___tempCage)
        {
            if (modEnabled.Value && __instance.GetComponentInParent<GhostFx>() == null)
            {
                Log("ActionRecycle.OnAction called");
                if (!isRunning)
                {
                    var iap = __instance.GetComponentInParent<InventoryAssociatedProxy>();
                    if (iap.IsSpawned)
                    {
                        isRunning = true;
                        ___tempCage.SetActive(true);

                        var depositor = new Depositor(__instance, ___craftSpawn);
                        iap.GetInventory(depositor.OnMachineReceived);
                    }
                    else
                    {
                        Log("  IsSpawned = false");
                    }
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
            if (modEnabled.Value)
            {
                var t = autoRecyclePeriod.Value;
                if (t > 0)
                {
                    __instance.StartCoroutine(AutoRecycle_Loop(__instance, t));
                }
            }
        }

        static IEnumerator AutoRecycle_Loop(ActionRecycle __instance, int t)
        {
            for (; ; )
            {
                yield return new WaitForSeconds(t);
                if (NetworkManager.Singleton?.IsServer ?? true)
                {
                    if (__instance.GetComponentInParent<GhostFx>() == null)
                    {
                        __instance.OnAction();
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
                    Log("Request Recycle " + machineWorldObject.GetId());
                    ModNetworking.SendHost(FunctionRequestRecycle, machineWorldObject.GetId().ToString());
                    return;
                }


                Log("Begin " + machineWorldObject.GetId());

                this.machinePosition = machineWorldObject.GetPosition();

                if (machineInventory.GetInsideWorldObjects().Count == 0)
                {
                    isRunning = false;
                    Log("    Nothing to recycle");
                    return;
                }

                toRecycleWo = machineInventory.GetInsideWorldObjects()[0];
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

                            currentInventoryCandidates ??= FindInventoryFor(gid, machinePosition).GetEnumerator();

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
    }
}
