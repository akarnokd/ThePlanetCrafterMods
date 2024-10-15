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
using Unity.Netcode;

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

        static ConfigEntry<bool> storeBySame;

        static ConfigEntry<bool> storeByName;

        static ConfigEntry<string> storeByNameAliases;

        static ConfigEntry<string> storeByNameMarker;

        static ConfigEntry<bool> storeByDemand;

        static ConfigEntry<bool> storeBySupply;

        static ConfigEntry<string> keepList;

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
            storeBySame = Config.Bind("General", "StoreBySame", true, "Original behavior, store next to the same already stored items.");
            storeByName = Config.Bind("General", "StoreByName", false, "Store into containers whose naming matches the item id, such as !Iron for example. Use StoreByNameAliases to override individual items.");
            storeByDemand = Config.Bind("General", "StoreByDemand", false, "Store into containers whose logistic demand settings includes the item.");
            storeBySupply = Config.Bind("General", "StoreBySupply", false, "Store into containers whose logistic supply settings includes the item.");
            storeByNameAliases = Config.Bind("General", "StoreByNameAliases", "", "A comma separated list of itemId:name elements, denoting which item should find which container containing that name. The itemId is case sensitive, the name is case-insensitive. Example: Iron:A,Uranim:B,ice:C");
            storeByNameMarker = Config.Bind("General", "StoreByNameMarker", "!", "The prefix for when using default item ids for storage naming. To disambiguate with other remote deposit mods that use star.");
            keepList = Config.Bind("General", "KeepList", "", "A comma separated list of itemId:amount elements to keep a minimum amount of that item. itemId is case sensitive. Example: WaterBottle1:5,OxygenCapsule1:5 to keep 5 water bottles and oxygen capsules in the backpack");

            if (!key.Value.Contains("<"))
            {
                key.Value = "<Keyboard>/" + key.Value;
            }
            storeAction = new InputAction(name: "Store Items", binding: key.Value);
            storeAction.Enable();

            LibCommon.HarmonyIntegrityCheck.Check(typeof(Plugin));
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
            if (ac.GetPlayerBackpack() == null)
            {
                return;
            }
            if (ac.GetPlayerBackpack().GetInventory() == null)
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

            if (NetworkManager.Singleton?.IsServer ?? true)
            {
                InventorySearch(ac);
            }
            else
            {
                StartCoroutine(PrefetchInventoriesClient(ac));
            }
        }

        IEnumerator PrefetchInventoriesClient(PlayerMainController ac)
        {
            Log("  Prefetching Inventories on the client");
            var counter = new int[1] { 1 };
            var n = 0;
            foreach (var invp in FindObjectsByType<InventoryAssociatedProxy>(FindObjectsSortMode.None))
            {
                if (invp.GetComponent<InventoryAssociated>() == null
                    && invp.GetComponent<ActionOpenable>() != null
                    && invp.GetComponent<WorldObjectFromScene>() == null)
                {
                    counter[0]++;
                    n++;
                    invp.GetInventory((inv, wo) => counter[0]--);
                }
            }
            Log("    Waiting for " + n + " objects");
            counter[0]--;
            while (counter[0] > 0)
            {
                yield return null;
            }
            Log("    Continue with the inventory search");
            InventorySearch(ac);
        }

        void InventorySearch(PlayerMainController ac)
        {
            var playerPos = ac.transform.position;

            List<(int, string)> candidateInventoryIds = [];
            List<(WorldObject, string)> candidateGetInventoryOfWorldObject = [];

            List<WorldObject> wos = WorldObjectsHandler.Instance.GetConstructedWorldObjects();
            Log("  Constructed WorldObjects: " + wos.Count);

            foreach (var wo in wos)
            {
                var grid = wo.GetGroup().GetId();
                var woPos = Vector3.zero;
                var woTxt = wo.GetText();
                if (wo.GetIsPlaced())
                {
                    woPos = wo.GetPosition();
                }
                else
                {
                    var go = wo.GetGameObject();
                    if (go != null)
                    {
                        woPos = go.transform.position;

                        if (go.TryGetComponent<TextProxy>(out var tp))
                        {
                            woTxt = tp.GetText();
                        }
                    }
                }

                if (woPos != Vector3.zero)
                {
                    var dist = Vector3.Distance(playerPos, woPos);
                    Log("    WorldObject " + wo.GetId() + " (" + wo.GetGroup().GetId() + ") @ " + dist + " m");
                    if (grid.StartsWith("Container") && dist <= range.Value)
                    {
                        if (wo.GetLinkedInventoryId() != 0)
                        {
                            candidateInventoryIds.Add((
                                wo.GetLinkedInventoryId(),
                                woTxt
                            ));
                        }
                        else
                        {
                            Log("  WorldObject Container " + wo.GetId() + " missing local inventory");
                            candidateGetInventoryOfWorldObject.Add((wo, woTxt));
                        }
                    }
                }
            }
            Log("  Containers in range: "
                + candidateInventoryIds.Count + " + "
                + candidateGetInventoryOfWorldObject.Count
                + " = " + (candidateInventoryIds.Count + candidateGetInventoryOfWorldObject.Count));

            var backpackInv = ac.GetPlayerBackpack().GetInventory();

            var candidateInv = new List<(Inventory, string)>();

            foreach (var iid in candidateInventoryIds)
            {
                var fiid = iid;
                InventoriesHandler.Instance.GetInventoryById(iid.Item1, 
                    responseInv => candidateInv.Add((responseInv, fiid.Item2)));
            }
            foreach (var wo in candidateGetInventoryOfWorldObject)
            {
                var fwo = wo;
                InventoriesHandler.Instance.GetWorldObjectInventory(wo.Item1, 
                    responseInv => candidateInv.Add((responseInv, fwo.Item2))
                );
            }

            StartCoroutine(WaitForInventories(backpackInv, candidateInv, candidateInventoryIds.Count + candidateGetInventoryOfWorldObject.Count));

        }

        static IEnumerator WaitForInventories(Inventory backpackInv, List<(Inventory, string)> candidateInventory, int n)
        {
            Log("  Waiting for GetInventoryById callbacks: " + candidateInventory.Count + " of " + n);
            while (candidateInventory.Count != n)
            {
                yield return null;
            }

            var includeGroupIds = includeList.Value.Split(',').Select(v => v.Trim()).Where(v => v.Length != 0).ToHashSet();
            var excludeGroupIds = excludeList.Value.Split(',').Select(v => v.Trim()).Where(v => v.Length != 0).ToHashSet();

            Log("  Processed include/exclude lists");

            var modeStoreBySame = storeBySame.Value;
            var modeStoreByName = storeByName.Value;
            var marker = storeByNameMarker.Value;
            var modeStoreByDemand = storeByDemand.Value;
            var modeStoreBySupply = storeBySupply.Value;

            var aliases = new Dictionary<string, string>();
            foreach (var kv in storeByNameAliases.Value.Split(","))
            {
                var kvv = kv.Split(":");
                if (kvv.Length == 2)
                {
                    aliases[kvv[0].Trim()] = kvv[1].Trim().ToLowerInvariant();
                }
            }
            Log("  Processed aliases: " + aliases.Count);

            var keepCounter = new Dictionary<string, int>();
            var keepListItems = keepList.Value.Split(',');
            foreach (var kl in keepListItems)
            {
                var idAmount = kl.Split(':');
                if (idAmount.Length == 2)
                {
                    if (int.TryParse(idAmount[1], out var amount))
                    {
                        keepCounter[idAmount[0].Trim()] = amount;
                    }
                }
            }
            Log("  Processed keep list: " + keepCounter.Count);

            List<WorldObject> backpackWos = [..backpackInv.GetInsideWorldObjects()];
            int kept = 0;

            if (keepCounter.Count != 0)
            {
                Log("  Begin keep list pruning: " + backpackWos.Count);

                backpackWos.Reverse();

                for (int i = backpackWos.Count - 1; i >= 0; i--)
                {
                    var gid = backpackWos[i].GetGroup().GetId();

                    if (keepCounter.TryGetValue(gid, out var amount))
                    {
                        if (amount > 0)
                        {
                            keepCounter[gid] = amount - 1;
                            backpackWos.RemoveAt(i);
                            kept++;
                        }
                    }
                }

                backpackWos.Reverse();
            }

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
                    if (inv.Item1 != null)
                    {
                        var foundCandidate = false;
                        if (modeStoreBySame)
                        {
                            foreach (var wo2 in inv.Item1.GetInsideWorldObjects())
                            {
                                if (wo2.GetGroup().id == gid)
                                {
                                    foundCandidate = true;
                                    break;
                                }
                            }
                        }
                        if (!foundCandidate && modeStoreByName)
                        {
                            var nameToFind = marker + gid;
                            if (aliases.TryGetValue(gid, out var alias))
                            {
                                nameToFind = alias;
                            }
                            if (inv.Item2 != null && inv.Item2.Contains(nameToFind, System.StringComparison.InvariantCultureIgnoreCase))
                            {
                                foundCandidate = true;
                            }
                        }
                        if (!foundCandidate && modeStoreByDemand)
                        {
                            if (inv.Item1.GetLogisticEntity().GetDemandGroups()?.Contains(group) ?? false)
                            {
                                foundCandidate = true;
                            }
                        }
                        if (!foundCandidate && modeStoreBySupply)
                        {
                            if (inv.Item1.GetLogisticEntity().GetSupplyGroups()?.Contains(group) ?? false)
                            {
                                foundCandidate = true;
                            }
                        }

                        if (foundCandidate)
                        {
                            var tch = new TransferCompletionHandler();

                            InventoriesHandler.Instance.TransferItem(backpackInv, inv.Item1, wo, success =>
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
                                Log("    Deposited into " + inv.Item1.GetId() + " (" + inv.Item2 + ")");
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
            Managers.GetManager<BaseHudHandler>()?.DisplayCursorText("", 5f, "Auto Store: " + deposited + " / " + (backpackWos.Count + kept) + " deposited. " + excluded + " excluded. " + kept + " kept.");
        }

        /* Fixed in 1.002
        // Workaround for the method as it may crash if the woId no longer exists.
        // We temporarily restore an empty object for the duration of the method
        // so it can see no inventory and respond accordingly.
        [HarmonyPrefix]
        [HarmonyPatch(typeof(InventoriesHandler), "GetOrCreateNewInventoryServerRpc", [typeof(int), typeof(ServerRpcParams)])]
        static void InventoriesHandler_GetOrCreateNewInventoryServerRpc_Pre(int woId, ref bool __state)
        {
            if (!WorldObjectsHandler.Instance.GetAllWorldObjects().ContainsKey(woId))
            {
                WorldObjectsHandler.Instance.GetAllWorldObjects()[woId] = new WorldObject(woId, null);
                __state = true;
            }
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(InventoriesHandler), "GetOrCreateNewInventoryServerRpc", [typeof(int), typeof(ServerRpcParams)])]
        static void InventoriesHandler_GetOrCreateNewInventoryServerRpc_Post(int woId, ref bool __state)
        {
            if (__state)
            {
                WorldObjectsHandler.Instance.GetAllWorldObjects().Remove(woId);
            }
        }
        */

        internal class TransferCompletionHandler
        {
            internal bool done;
            internal bool success;
        }
    }
}
