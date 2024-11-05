// Copyright (c) 2022-2024, David Karnok & Contributors
// Licensed under the Apache License, Version 2.0

using BepInEx;
using SpaceCraft;
using HarmonyLib;
using BepInEx.Configuration;
using System.Collections.Generic;
using UnityEngine;
using System.Collections;
using BepInEx.Logging;
using System;
using UnityEngine.InputSystem;
using LibCommon;
using System.Reflection;
using Unity.Netcode;
using System.Diagnostics;

namespace CheatCraftFromNearbyContainers
{
    [BepInPlugin(modCheatCraftFromNearbyContainersGuid, "(Cheat) Craft From Nearby Containers", PluginInfo.PLUGIN_VERSION)]
    public class Plugin : BaseUnityPlugin
    {
        const string modCheatCraftFromNearbyContainersGuid = "akarnokd.theplanetcraftermods.cheatcraftfromnearbycontainers";

        static ConfigEntry<bool> modEnabled;

        static ConfigEntry<bool> debugMode;

        static ConfigEntry<int> range;

        static ConfigEntry<string> key;

        static ConfigEntry<string> includeFilter;

        static ManualLogSource logger;

        static bool inventoryLookupInProgress;

        static InputAction toggleAction;

        /// <summary>
        ///  Check the value of this field to know if the mod is active and the player
        ///  is looking at a crafting screen.
        /// </summary>
        public static List<Inventory> candidateInventories;

        /// <summary>
        /// Check this function to know if CraftManager.TryToCraftInInventory will be handled by this mod.
        /// </summary>
        public static Func<bool> apiTryToCraftInInventoryHandled = () => modEnabled.Value;

        /// <summary>
        /// Call this function to get the nearby inventories based on this mod's settings via a callback.
        /// </summary>
        public static Action<MonoBehaviour, Vector3, Action<List<Inventory>>> apiGetInventoriesInRange = GetInventoriesInRange;

        static AccessTools.FieldRef<object, bool> fCraftManagerCrafting;

        // Set from Inventory Stacking if present
        public static Func<Inventory, HashSet<int>, string, bool> apiIsFullStackedWithRemoveInventory = 
            (inv, toremove, grid) => inv.GetInsideWorldObjects().Count - toremove.Count >= inv.GetSize();

        /// <summary>
        /// Call this method to make inventory preparations before setting a construction ghost.
        /// Otherwise the vanilla logic may not pick up the nearby inventories.
        /// </summary>
        public static Action<MonoBehaviour, Vector3, Action> apiPrepareSetNewGhost = PrepareSetNewGhost;

        static MethodInfo mPlayerInputDispatcherIsTyping;

        static Coroutine vanillaPinUpdaterCoroutine;

        public void Awake()
        {
            LibCommon.BepInExLoggerFix.ApplyFix();

            // Plugin startup logic
            Logger.LogInfo($"Plugin is loaded!");

            logger = Logger;

            modEnabled = Config.Bind("General", "Enabled", true, "Is this mod enabled");
            debugMode = Config.Bind("General", "DebugMode", false, "Enable detailed logging? Chatty!");
            range = Config.Bind("General", "Range", 20, "The range to look for containers within.");
            key = Config.Bind("General", "Key", "<Keyboard>/Home", "The input action shortcut toggle this mod on or off.");
            includeFilter = Config.Bind("General", "IncludeFilter", "", "Comma-separated list of item id prefixes whose inventory should be included. Example: OreExtractor,OreBreaker");

            if (!key.Value.Contains("<"))
            {
                key.Value = "<Keyboard>/" + key.Value;
            }
            toggleAction = new InputAction(name: "Toggle the ranged crafting", binding: key.Value);
            toggleAction.Enable();

            fCraftManagerCrafting = AccessTools.FieldRefAccess<bool>(typeof(CraftManager), "_crafting");
            mPlayerInputDispatcherIsTyping = AccessTools.Method(typeof(PlayerInputDispatcher), "IsTyping")
                ?? throw new InvalidOperationException("PlayerInputDispatcher::IsTyping not found");

            LibCommon.HarmonyIntegrityCheck.Check(typeof(Plugin));
            var harmony = Harmony.CreateAndPatchAll(typeof(Plugin));
            /*
            LibCommon.GameVersionCheck.Patch(harmony, "(Cheat) Craft From Nearby Containers - v" + PluginInfo.PLUGIN_VERSION);
            */
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
            if (!toggleAction.WasPressedThisFrame())
            {
                return;
            }
            modEnabled.Value = !modEnabled.Value;

            Managers.GetManager<BaseHudHandler>()
                ?.DisplayCursorText("", 3f, "Craft From Nearby Containers: " + (modEnabled.Value ? "Activated" : "Deactivated"));

            // cancel building if the player has a ghost while toggling off of the mod and getting out of range
            if (!modEnabled.Value)
            {
                ac.GetPlayerBuilder().InputOnCancelAction();
            }
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(ActionCrafter), nameof(ActionCrafter.OnAction))]
        static bool ActionCrafter_OnAction(ActionCrafter __instance)
        {
            candidateInventories = null;

            if (!modEnabled.Value)
            {
                return true;
            }

            var pm = Managers.GetManager<PlayersManager>();
            if (pm == null)
            {
                return false;
            }
            var ac = pm.GetActivePlayerController();
            if (ac == null)
            {
                return false;
            }

            if (__instance is MachineAutoCrafter)
            {
                return false;
            }

            if (ac.GetMultitool().GetState() == DataConfig.MultiToolState.Deconstruct)
            {
                return false;
            }
            Log("ActionCrafter::OnAction");
            if (__instance.cantCraftIfSpawnContainsObject)
            {
                WorldObjectsHandler.Instance.HasWorldObjectAtPosition(__instance.craftSpawn.transform.position, result =>
                {
                    if (result)
                    {
                        Managers.GetManager<BaseHudHandler>()?.DisplayCursorText("UI_craft_contains_object", 0f, "", "");
                        return;
                    }
                    PrepareInventories(__instance, ac);
                });
                return false;
            }

            PrepareInventories(__instance, ac);

            return false;
        }

        static void PrepareInventories(ActionCrafter __instance, PlayerMainController ac)
        {
            if (inventoryLookupInProgress)
            {
                Log("Inventory Lookup In Progress");
                return;
            }

            Log("Begin Inventory Lookup");

            inventoryLookupInProgress = true;

            GetInventoriesInRange(__instance, ac.transform.position, list =>
            {
                candidateInventories = list;

                UiWindowCraft uiWindowCraft = (UiWindowCraft)Managers.GetManager<WindowsHandler>()
                    .OpenAndReturnUi(DataConfig.UiType.Craft);
                uiWindowCraft.SetCrafter(__instance, !__instance.cantCraft);
                uiWindowCraft.ChangeTitle(Localization.GetLocalizedString(__instance.titleLocalizationId));

                inventoryLookupInProgress = false;
            });
        }


        [HarmonyPrefix]
        [HarmonyPatch(typeof(PlayerInputDispatcher), nameof(PlayerInputDispatcher.OnOpenConstructionDispatcher))]
        static bool PlayerInputDispatcher_OnOpenConstructionDispatcher(
            PlayerInputDispatcher __instance,
            PlayerMultitool ____playerMultitool)
        {
            candidateInventories = null;

            if (!modEnabled.Value)
            {
                return true;
            }

            // Workaround for opening the construction menu from the pause menu.
            var wh = Managers.GetManager<WindowsHandler>();
            if (wh != null && wh.GetOpenedUi() == DataConfig.UiType.Options)
            {
                return false;
            }

            Log("PlayerInputDispatcher::OnOpenConstructionDispatcher");


            if ((bool)mPlayerInputDispatcherIsTyping.Invoke(__instance, []))
            {
                return false;
            }
            if (____playerMultitool.HasEnabledState(DataConfig.MultiToolState.Build) 
                || Managers.GetManager<GameSettingsHandler>().GetCurrentGameSettings().GetFreeCraft())
            {
                if (!GamepadConfig.Instance.GetIsUsingController())
                {
                    GetInventoriesInRange(__instance,
                        Managers.GetManager<PlayersManager>().GetActivePlayerController().transform.position,
                        list =>
                        {
                            candidateInventories = list;
                            Managers.GetManager<WindowsHandler>().ToggleUi(DataConfig.UiType.Construction);
                            ____playerMultitool.SetState(DataConfig.MultiToolState.Null);
                            ____playerMultitool.SetState(DataConfig.MultiToolState.Build);
                        }
                    );
                } else if (!Managers.GetManager<WindowsHandler>().GetHasUiOpen())
                {
                    GetInventoriesInRange(__instance,
                        Managers.GetManager<PlayersManager>().GetActivePlayerController().transform.position,
                        list =>
                        {
                            candidateInventories = list;
                            Managers.GetManager<WindowsHandler>().OpenAndReturnUi(DataConfig.UiType.Construction);
                            ____playerMultitool.SetState(DataConfig.MultiToolState.Null);
                            ____playerMultitool.SetState(DataConfig.MultiToolState.Build);
                        }
                    );
                }
            }
            else
            {
                Managers.GetManager<BaseHudHandler>().DisplayCursorText("UI_warn_no_constr_chip", 2f, "", "");
            }

            return false;
        }

        public static void GetInventoriesInRange(MonoBehaviour parent, Vector3 pos, Action<List<Inventory>> onComplete)
        {
            if (onComplete == null)
            {
                throw new ArgumentNullException(nameof(onComplete));
            }

            if (!modEnabled.Value)
            {
                onComplete([]);
                return;
            }

            Log("Begin GetInventoriesInRange");

            if (NetworkManager.Singleton?.IsServer ?? true)
            {
                GetInventoriesInRangeSearch(parent, pos, onComplete);
            }
            else
            {
                parent.StartCoroutine(PrefetchInventoriesInRangeClient(parent, pos, onComplete));
            }
        }

        static IEnumerator PrefetchInventoriesInRangeClient(MonoBehaviour parent, Vector3 pos, Action<List<Inventory>> onComplete)
        {
            Log("  Prefetching Inventories on the client");
            var sw = Stopwatch.StartNew();
            var counter = new int[1] { 1 };
            var n = 0;
            foreach (var invp in FindObjectsByType<InventoryAssociatedProxy>(FindObjectsSortMode.None))
            {
                if (invp.GetComponent<InventoryAssociated>() == null
                    && invp.GetComponent<ActionOpenable>() != null
                    && invp.GetComponent<WorldObjectFromScene>() == null
                    && Vector3.Distance(invp.transform.position, pos) <= range.Value
                    && invp.IsSpawned
                )
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
            Log("      Prefetch inventory time: " + sw.Elapsed.TotalMilliseconds + " ms");
            Log("    Continue with the inventory search");
            GetInventoriesInRangeSearch(parent, pos, onComplete);
        }

        static List<string> GetPrefixes()
        {
            var v = includeFilter.Value;
            if (v.IsNullOrWhiteSpace())
            {
                return [];
            }
            return [..v.Split(',')];
        }

        static void GetInventoriesInRangeSearch(MonoBehaviour parent, Vector3 pos, Action<List<Inventory>> onComplete)
        {
            List<int> candidateInventoryIds = [];
            List<WorldObject> candidateGetInventoryOfWorldObject = [];
            HashSet<int> seen = [];
            List<string> prefixes = GetPrefixes();
            foreach (var wo in WorldObjectsHandler.Instance.GetConstructedWorldObjects())
            {
                var grid = wo.GetGroup().GetId();
                var wpos = Vector3.zero;
                if (wo.GetIsPlaced())
                {
                    wpos = wo.GetPosition();
                }
                else
                {
                    var go = wo.GetGameObject();
                    if (go != null)
                    {
                        wpos = go.transform.position;
                    }
                }
                var dist = Vector3.Distance(pos, wpos);

                if ((grid.StartsWith("Container") || CheckGIDList(grid, prefixes))
                    && dist <= range.Value
                )
                {
                    if (seen.Add(wo.GetId()))
                    {
                        Log("  Found Container " + wo.GetId() + " (" + wo.GetGroup().GetId() + ") @ " + dist + " m");
                        if (wo.GetLinkedInventoryId() != 0)
                        {
                            candidateInventoryIds.Add(wo.GetLinkedInventoryId());
                            if (wo.GetSecondaryInventoriesId().Count != 0)
                            {
                                candidateInventoryIds.AddRange(wo.GetSecondaryInventoriesId());
                            }
                        }
                        else
                        {
                            candidateGetInventoryOfWorldObject.Add(wo);
                        }
                    }
                }
            }

            Log("  Containers in range: "
                + candidateInventoryIds.Count + " + "
                + candidateGetInventoryOfWorldObject.Count
                + " = " + (candidateInventoryIds.Count + candidateGetInventoryOfWorldObject.Count));

            var inventoryList = new List<Inventory>();
            foreach (var iid in candidateInventoryIds)
            {
                InventoriesHandler.Instance.GetInventoryById(iid, inventoryList.Add);
            }
            foreach (var wo in candidateGetInventoryOfWorldObject)
            {
                InventoriesHandler.Instance.GetWorldObjectInventory(wo, inventoryList.Add);
            }
            parent.StartCoroutine(GetInventoriesInRangeWait(candidateInventoryIds.Count + candidateGetInventoryOfWorldObject.Count, inventoryList, onComplete));
        }

        static bool CheckGIDList(string grid, List<string> prefixes)
        {
            foreach (var prefix in prefixes)
            {
                if (grid.StartsWith(prefix))
                {
                    return true;
                }
            }
            return false;
        }

        static IEnumerator GetInventoriesInRangeWait(int n, List<Inventory> inventoryList, Action<List<Inventory>> onComplete)
        {
            Log("  Containers in discovered so far: " + inventoryList.Count + " / " + n);
            while (inventoryList.Count != n)
            {
                yield return null;
            }
            Log("  Containers in discovered: " + inventoryList.Count);
            /*
            foreach (var inv in inventoryList)
            {
                Log("    " + (inv != null ? inv.GetId() : "null"));
            }
            */

            onComplete.Invoke(inventoryList);
            
            Log("Done GetInventoriesInRange");
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(UiWindow), nameof(UiWindow.OnClose))]
        static void UiWindow_OnClose(UiWindow __instance)
        {
            if (__instance is UiWindowCraft)
            {
                candidateInventories = null;
            }
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(CraftManager), nameof(CraftManager.TryToCraftInWorld))]
        static bool CraftManager_TryToCraftInWorld(
            ActionCrafter sourceCrafter,
            PlayerMainController playerController,
            GroupItem groupItem,
            ref bool ____crafting,
            ref bool __result)
        {
            if (candidateInventories == null)
            {
                return true;
            }

            var ingredients = groupItem.GetRecipe().GetIngredientsGroupInRecipe();
            var backpackInv = playerController.GetPlayerBackpack().GetInventory();
            var freeCraft = Managers.GetManager<GameSettingsHandler>().GetCurrentGameSettings().GetFreeCraft();

            var discovery = new Dictionary<int, (Inventory, WorldObject)>();

            DiscoverAvailability(discovery, ingredients, backpackInv, null, null, null, null);

            bool available = discovery.Count == ingredients.Count;

            __result = available || freeCraft;

            if (__result && !____crafting)
            {
                ____crafting = true;

                WorldObjectsHandler.Instance.CreateAndInstantiateWorldObject(groupItem, sourceCrafter.GetSpawnPosition(),
                    sourceCrafter.GetSpawnRotation(), true, true, true, newSpawnedObject =>
                {
                    if (newSpawnedObject != null)
                    {
                        sourceCrafter.PlayCraftSound();
                        sourceCrafter.StartCoroutine(RemoveFromInventories(discovery, () =>
                        {
                            fCraftManagerCrafting() = false;
                        }));
                        return;
                    }
                    fCraftManagerCrafting() = false;
                });
                CraftManager.AddOneToTotalCraft();
            }

            return false;
        }

        static void DiscoverAvailability(
            Dictionary<int, (Inventory, WorldObject)> discovery,
            List<Group> ingredients,
            Inventory backpackInv,
            Inventory equipmentInv,
            List<Group> useFromEquipment,
            List<bool> isAvailableList,
            HashSet<int> foundInBackpack
        )
        {
            foreach (var gr in ingredients)
            {
                bool found = false;
                foreach (var wo in backpackInv.GetInsideWorldObjects())
                {
                    if (wo.GetGroup() == gr)
                    {
                        if (!discovery.ContainsKey(wo.GetId()))
                        {
                            discovery[wo.GetId()] = (backpackInv, wo);
                            found = true;
                            foundInBackpack?.Add(wo.GetId());
                            break;
                        }
                    }
                }
                if (!found && equipmentInv != null)
                {
                    foreach (var wo in equipmentInv.GetInsideWorldObjects())
                    {
                        if (wo.GetGroup() == gr)
                        {
                            if (!discovery.ContainsKey(wo.GetId()))
                            {
                                discovery[wo.GetId()] = (equipmentInv, wo);
                                found = true;
                                useFromEquipment.Add(gr);
                                break;
                            }
                        }
                    }
                }
                if (!found)
                {
                    foreach (var inv in candidateInventories)
                    {
                        if (inv != null)
                        {
                            bool found2 = false;
                            foreach (var wo in inv.GetInsideWorldObjects())
                            {
                                if (wo.GetGroup() == gr)
                                {
                                    if (!discovery.ContainsKey(wo.GetId()))
                                    {
                                        discovery[wo.GetId()] = (inv, wo);
                                        found2 = true;
                                        found = true;
                                        break;
                                    }
                                }
                            }
                            if (found2)
                            {
                                break;
                            }
                        }
                    }
                }
                isAvailableList?.Add(found);
            }
        }
        
        static IEnumerator RemoveFromInventories(Dictionary<int, (Inventory, WorldObject)> discovery, Action onComplete)
        {
            var callbackWaiter = new CallbackWaiter();
            foreach (var invWo in discovery.Values)
            {
                callbackWaiter.Reset();

                InventoriesHandler.Instance.RemoveItemFromInventory(invWo.Item2, invWo.Item1, true, callbackWaiter.Done);

                while (!callbackWaiter.IsDone)
                {
                    yield return null;
                }

                if (callbackWaiter.IsSuccess)
                {
                    var gr = invWo.Item2.GetGroup();

                    var informationsDisplayer = Managers.GetManager<DisplayersHandler>()?.GetInformationsDisplayer();
                    informationsDisplayer?.AddInformation(2f, Readable.GetGroupName(gr), DataConfig.UiInformationsType.OutInventory, gr.GetImage());
                }
            }
            onComplete?.Invoke();
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(CraftManager), nameof(CraftManager.TryToCraftInInventory))]
        static bool Patch_CraftManager_TryToCraftInInventory(
            GroupItem groupItem,
            PlayerMainController playerController,
            ActionCrafter sourceCrafter,
            ref bool ____crafting,
            int ____tempSpaceInInventory,
            ref bool __result
        )
        {
            if (candidateInventories == null)
            {
                return true;
            }

            var ingredients = groupItem.GetRecipe().GetIngredientsGroupInRecipe();
            var backpack = playerController.GetPlayerBackpack();
            var backpackInv = backpack.GetInventory();
            var equipment = playerController.GetPlayerEquipment();
            var equipmentInv = equipment.GetInventory();

            var freeCraft = Managers.GetManager<GameSettingsHandler>().GetCurrentGameSettings().GetFreeCraft();

            var useFromEquipment = new List<Group>();
            var discovery = new Dictionary<int, (Inventory, WorldObject)>();
            var foundInBackpack = new HashSet<int>();

            DiscoverAvailability(discovery, ingredients, backpackInv, equipmentInv, useFromEquipment, null, foundInBackpack);

            bool available = discovery.Count == ingredients.Count;

            __result = available || freeCraft;

            if (__result && !____crafting)
            {
                if (apiIsFullStackedWithRemoveInventory(backpackInv, foundInBackpack, groupItem.id))
                {
                    Managers.GetManager<BaseHudHandler>().DisplayCursorText("UI_InventoryFull", 2f);
                    __result = false;
                    return false;
                }

                ____crafting = true;

                sourceCrafter.CraftAnimation(groupItem);

                if (useFromEquipment.Count != 0)
                {
                    InventoriesHandler.Instance.SetInventorySize(equipmentInv, ____tempSpaceInInventory, Vector3.zero);
                    InventoriesHandler.Instance.SetInventorySize(backpackInv, ____tempSpaceInInventory, Vector3.zero);
                }

                sourceCrafter.StartCoroutine(RemoveItemsFromInventoriesAndCraft(
                    groupItem, discovery, useFromEquipment, 
                    backpackInv, equipmentInv, equipment, ____tempSpaceInInventory));

                CraftManager.AddOneToTotalCraft();
            }
            return false;
        }

        static IEnumerator RemoveItemsFromInventoriesAndCraft(GroupItem groupItem, 
            Dictionary<int, (Inventory, WorldObject)> discovery,
            List<Group> useFromEquipment,
            Inventory backpackInv, 
            Inventory equipmentInv, 
            PlayerEquipment playerEquipment,
            int ____tempSpaceInInventory
        )
        {
            var callbackWaiter = new CallbackWaiter();

            foreach (var invWo in discovery.Values)
            {
                if (invWo.Item1 != equipmentInv)
                {
                    callbackWaiter.Reset();

                    InventoriesHandler.Instance.RemoveItemFromInventory(invWo.Item2, invWo.Item1, true, callbackWaiter.Done);

                    while (!callbackWaiter.IsDone)
                    {
                        yield return null;
                    }

                    if (callbackWaiter.IsSuccess)
                    {
                        var gr = invWo.Item2.GetGroup();

                        var informationsDisplayer = Managers.GetManager<DisplayersHandler>()?.GetInformationsDisplayer();
                        informationsDisplayer?.AddInformation(2f, Readable.GetGroupName(gr), DataConfig.UiInformationsType.OutInventory, gr.GetImage());
                    }
                }
            }

            callbackWaiter.Reset();

            playerEquipment.DestroyItemsFromEquipment(useFromEquipment, callbackWaiter.Done);

            while (!callbackWaiter.IsDone)
            {
                yield return null;
            }

            InventoriesHandler.Instance.AddItemToInventory(groupItem, backpackInv, (success, id) =>
            {
                fCraftManagerCrafting() = false;

                if (!success)
                {
                    if (id > 0)
                    {
                        WorldObjectsHandler.Instance.DestroyWorldObject(id);
                    }
                    RestoreInventorySizes(backpackInv, playerEquipment,
                        -____tempSpaceInInventory, useFromEquipment.Count != 0);
                }
                else
                {
                    if (useFromEquipment.Count != 0 && groupItem.GetEquipableType() != DataConfig.EquipableType.Null)
                    {
                        playerEquipment.WatchForModifications(enabled: true);
                        var wo = WorldObjectsHandler.Instance.GetWorldObjectViaId(id);

                        InventoriesHandler.Instance.TransferItem(backpackInv, equipmentInv,
                            wo, _ => RestoreInventorySizes(backpackInv, playerEquipment,
                                        -____tempSpaceInInventory, useFromEquipment.Count != 0)
                        );
                    }
                    else
                    {
                        RestoreInventorySizes(backpackInv, playerEquipment,
                            -____tempSpaceInInventory, useFromEquipment.Count != 0);
                    }
                }

            });
        }

        static void RestoreInventorySizes(Inventory backpack, PlayerEquipment equipment, int delta, bool equipmentUsed)
        {
            if (equipmentUsed)
            {
                equipment.WatchForModifications(false);
                InventoriesHandler.Instance.SetInventorySize(equipment.GetInventory(), delta, Vector3.zero);
                InventoriesHandler.Instance.SetInventorySize(backpack, delta, Vector3.zero);
            }
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(Inventory), nameof(Inventory.ItemsContainsStatus))]
        static bool Inventory_ItemsContainsStatus(Inventory __instance, List<Group> groups, ref List<bool> __result)
        {
            if (candidateInventories == null)
            {
                return true;
            }
            var pm = Managers.GetManager<PlayersManager>();
            if (pm == null)
            {
                return true;
            }
            var ac = pm.GetActivePlayerController();
            if (ac == null)
            {
                return true;
            }
            var backpack = ac.GetPlayerBackpack();
            if (backpack == null)
            {
                return true;
            }
            var backpackInv = backpack.GetInventory();

            if (__instance != backpackInv || backpackInv == null)
            {
                return true;
            }

            /*
            var equipment = ac.GetPlayerEquipment();
            if (equipment == null)
            {
                return true;
            }

            var equipmentInv = equipment.GetInventory();

            if (equipmentInv == null)
            {
                return true;
            }
            */
            __result = [];

            var discovery = new Dictionary<int, (Inventory, WorldObject)>();

            DiscoverAvailability(discovery, groups, backpackInv, null /*equipmentInv*/, [], __result, null);

            return false;
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(PlayerBuilder), nameof(PlayerBuilder.InputOnAction))]
        static bool PlayerBuilder_InputOnAction(
            PlayerBuilder __instance,
            ConstructibleGhost ____ghost,
            GroupConstructible ____ghostGroupConstructible,
            float ____timeCreatedGhost,
            float ____timeCantBuildInterval,
            WorldObject ____sourceWorldObject
        )
        {
            if (!modEnabled.Value)
            {
                return true;
            }

            if (____ghost != null)
            {
                if (Time.time < ____timeCreatedGhost + ____timeCantBuildInterval && !Managers.GetManager<GameSettingsHandler>().GetCurrentGameSettings().GetFreeCraft())
                {
                    return false;
                }

                // Get all currently known inventores, hopefully relatively up-to-date from the prefetch.
                var pos = Managers.GetManager<PlayersManager>().GetActivePlayerController().transform.position;
                candidateInventories = [];
                HashSet<int> seen = [];
                List<string> prefixes = GetPrefixes();

                foreach (var wo in WorldObjectsHandler.Instance.GetConstructedWorldObjects())
                {
                    var grid = wo.GetGroup().GetId();
                    var wpos = Vector3.zero;
                    if (wo.GetIsPlaced())
                    {
                        wpos = wo.GetPosition();
                    }
                    else
                    {
                        var go = wo.GetGameObject();
                        if (go != null)
                        {
                            wpos = go.transform.position;
                        }
                    }
                    var dist = Vector3.Distance(pos, wpos);

                    if ((grid.StartsWith("Container") || CheckGIDList(grid, prefixes))
                        && dist <= range.Value
                    )
                    {
                        if (seen.Add(wo.GetId()))
                        {
                            var iid = wo.GetLinkedInventoryId();
                            Log("  Found Container " + wo.GetId() + " (" + wo.GetGroup().GetId() + ") @ " + dist + " m, IID: " + iid);
                            if (iid != 0)
                            {
                                candidateInventories.Add(InventoriesHandler.Instance.GetInventoryById(iid));
                                
                                foreach (var iid2 in wo.GetSecondaryInventoriesId())
                                {
                                    candidateInventories.Add(InventoriesHandler.Instance.GetInventoryById(iid2));
                                }
                            }
                        }
                    }
                }

                // double check if we are still in range for building

                List<Group> ingredientsGroupInRecipe = ____ghostGroupConstructible.GetRecipe().GetIngredientsGroupInRecipe();
                bool available = __instance.GetComponent<PlayerMainController>().GetPlayerBackpack().GetInventory()
                    .ContainsItems(ingredientsGroupInRecipe);
                bool freeCraft = Managers.GetManager<GameSettingsHandler>().GetCurrentGameSettings().GetFreeCraft();
                if (available || freeCraft || ____sourceWorldObject != null)
                {
                    var onConstructed = AccessTools.MethodDelegate<Action<GameObject>>(AccessTools.Method(typeof(PlayerBuilder), "OnConstructed"), __instance);
                    ____ghost.Place(onConstructed);
                }
                else
                {
                    Managers.GetManager<BaseHudHandler>().DisplayCursorText("", 3f, "Craft From Nearby Containers: Ingredients no longer available!");
                }
            }

            return false;
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(PlayerBuilder), "OnConstructed")]
        static bool PlayerBuilder_OnConstructed(PlayerBuilder __instance, 
            ref ConstructibleGhost ____ghost,
            GroupConstructible ____ghostGroupConstructible,
            WorldObject ____sourceWorldObject)
        {
            if (!modEnabled.Value)
            {
                return true;
            }

            ____ghost = null;
            __instance.GetComponent<PlayerAudio>().PlayBuildGhost();
            __instance.GetComponent<PlayerAnimations>().AnimateConstruct(true, -1f);
            __instance.GetComponent<PlayerShareState>().StartConstructing();
            __instance.Invoke("StopAnimation", 0.5f);

            if (____sourceWorldObject != null)
            {
                InventoriesHandler.Instance.RemoveItemFromInventory(____sourceWorldObject, __instance.GetComponent<PlayerBackpack>().GetInventory(), false, null);
            }
            else
            {
                __instance.StartCoroutine(Build_Deduce(__instance, ____ghostGroupConstructible));
            }
            return false;
        }

        static IEnumerator Build_Deduce(PlayerBuilder __instance, 
            GroupConstructible ____ghostGroupConstructible)
        {
            var cw = new CallbackWaiter();
            GetInventoriesInRange(__instance, __instance.transform.position, list =>
            {
                candidateInventories = list;
                cw.Done();
            });

            while (!cw.IsDone)
            {
                yield return null;
            }

            var backpackInv = __instance.GetComponent<PlayerBackpack>().GetInventory();

            if (CheckInventoryForDirectBuild(backpackInv, __instance, ____ghostGroupConstructible))
            {
                yield break;
            }

            foreach (var inv in candidateInventories)
            {
                if (inv != null)
                {
                    if (CheckInventoryForDirectBuild(inv, __instance, ____ghostGroupConstructible))
                    {
                        yield break;
                    }
                }
            }

            var recipe = ____ghostGroupConstructible.GetRecipe().GetIngredientsGroupInRecipe();

            var discovery = new Dictionary<int, (Inventory, WorldObject)>();

            DiscoverAvailability(discovery, recipe, backpackInv, null, null, null, null);

            yield return RemoveFromInventories(discovery, () =>
            {
                Build_CheckChain(__instance, true, ____ghostGroupConstructible);
            });
        }

        static bool CheckInventoryForDirectBuild(Inventory inv, 
            PlayerBuilder __instance, 
            GroupConstructible ___ghostGroupConstructible)
        {
            foreach (var wo in inv.GetInsideWorldObjects())
            {
                if (wo.GetGroup() == ___ghostGroupConstructible)
                {
                    InventoriesHandler.Instance.RemoveItemFromInventory(wo, inv, true, success =>
                    {
                        var informationsDisplayer = Managers.GetManager<DisplayersHandler>()?.GetInformationsDisplayer();
                        informationsDisplayer?.AddInformation(2f, Readable.GetGroupName(___ghostGroupConstructible), DataConfig.UiInformationsType.OutInventory, ___ghostGroupConstructible.GetImage());

                        Build_CheckChain(__instance, success, ___ghostGroupConstructible);
                    });
                    return true;
                }
            }
            return false;
        }

        static void Build_CheckChain(PlayerBuilder __instance, bool success, GroupConstructible ___ghostGroupConstructible)
        {
            if (success && __instance.GetComponent<PlayerInputDispatcher>().IsPressingAccessibilityKey() 
                && ___ghostGroupConstructible.GetRecipe().GetIngredientsGroupInRecipe().Count > 0)
            {
                __instance.SetNewGhost(___ghostGroupConstructible);
            }
        }

        static void PrepareSetNewGhost(MonoBehaviour parent, Vector3 position, Action onReady)
        {
            GetInventoriesInRange(parent, position, list =>
            {
                candidateInventories = list;
                onReady();
            });
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(CanvasPinedRecipes), "SetPlayerInventory")]
        static void CanvasPinnedRecipes_SetPlayerInventory(CanvasPinedRecipes __instance, 
            Inventory _inventory,
            List<InformationDisplayer> ___informationDisplayers,
            List<Group> ___groupsAdded)
        {
            if (vanillaPinUpdaterCoroutine != null)
            {
                __instance.StopCoroutine(vanillaPinUpdaterCoroutine);
                vanillaPinUpdaterCoroutine = null;
            }
            vanillaPinUpdaterCoroutine = __instance.StartCoroutine(UpdateVanillaPinsCoroutine(__instance,
                _inventory, 
                ___informationDisplayers, 
                ___groupsAdded));
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(CanvasPinedRecipes), "OnUiWindowEvent")]
        static void CanvasPinnedRecipes_OnUiWindowEvent(CanvasPinedRecipes __instance,
            Inventory ___playerInventory,
            List<InformationDisplayer> ___informationDisplayers,
            List<Group> ___groupsAdded)
        {
            CanvasPinnedRecipes_SetPlayerInventory(__instance, ___playerInventory, ___informationDisplayers, ___groupsAdded);
        }

        static IEnumerator UpdateVanillaPinsCoroutine(CanvasPinedRecipes __instance, 
            Inventory _inventory,
            List<InformationDisplayer> ___informationDisplayers, 
            List<Group> ___groupsAdded)
        {
            var wait = new WaitForSeconds(0.25f);
            var cw = new CallbackWaiter();
            for (; ; )
            {
                var pm = Managers.GetManager<PlayersManager>();
                if (pm != null)
                {
                    var ac = pm.GetActivePlayerController();
                    if (ac != null)
                    {
                        var pos = ac.transform.position;

                        cw.Reset();
                        GetInventoriesInRange(__instance, pos, list =>
                        {
                            try
                            {
                                candidateInventories = list;

                                for (int i = 0; i < ___groupsAdded.Count; i++)
                                {
                                    var gr = ___groupsAdded[i];
                                    var id = ___informationDisplayers[i];

                                    id.SetGroupListGroupsAvailability(
                                        _inventory.ItemsContainsStatus(gr.GetRecipe().GetIngredientsGroupInRecipe())
                                    );
                                }
                            }
                            catch (Exception ex)
                            {
                                logger.LogError(ex);
                            }
                            cw.Done();
                        });

                        while (!cw.IsDone)
                        {
                            yield return null;
                        }
                    }
                }

                yield return wait;
            }

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

        [HarmonyPrefix]
        [HarmonyPatch(typeof(UiWorldInstanceSelector), nameof(UiWorldInstanceSelector.SetValues))]
        static void UiWorldInstanceSelector_SetValue(
            List<Group> groups, 
            ref List<bool> groupsAvailabilty)
        {
            candidateInventories = null;
            var inv = Managers.GetManager<PlayersManager>()
                ?.GetActivePlayerController()
                ?.GetPlayerBackpack()
                ?.GetInventory();
            if (inv != null) 
            {
                if (groups != null)
                {
                    groupsAvailabilty = inv.ItemsContainsStatus(groups);
                }
            }
            else
            {
                if (groupsAvailabilty != null)
                {
                    for (int i = 0; i < groupsAvailabilty.Count; i++)
                    {
                        groupsAvailabilty[i] = false;
                    }
                }
            }
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(UiWindowPause), nameof(UiWindowPause.OnQuit))]
        static void Patch_UiWindowPause_OnQuit()
        {
            candidateInventories = null;
        }


        [HarmonyPrefix]
        [HarmonyPatch(typeof(BlackScreen), nameof(BlackScreen.DisplayLogoStudio))]
        static void BlackScreen_DisplayLogoStudio()
        {
            Patch_UiWindowPause_OnQuit();
        }
    }
}
