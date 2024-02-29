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
using UnityEngine.UIElements;

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
        public static Func<Inventory, string, bool> apiIsFullStackedInventory = (inv, grid) => inv.IsFull();

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

            if (!key.Value.Contains("<"))
            {
                key.Value = "<Keyboard>/" + key.Value;
            }
            toggleAction = new InputAction(name: "Toggle the ranged crafting", binding: key.Value);
            toggleAction.Enable();

            fCraftManagerCrafting = AccessTools.FieldRefAccess<bool>(typeof(CraftManager), "_crafting");
            mPlayerInputDispatcherIsTyping = AccessTools.Method(typeof(PlayerInputDispatcher), "IsTyping")
                ?? throw new InvalidOperationException("PlayerInputDispatcher::IsTyping not found");
            
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

            if (ac.GetMultitool().GetState() == DataConfig.MultiToolState.Deconstruct)
            {
                return false;
            }

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

            candidateInventories = [];

            foreach (var iid in candidateInventoryIds)
            {
                InventoriesHandler.Instance.GetInventoryById(iid, candidateInventories.Add);
            }

            __instance.StartCoroutine(WaitForInventories(candidateInventoryIds.Count, __instance));
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
                            Managers.GetManager<WindowsHandler>().OpenAndReturnUi(DataConfig.UiType.Construction);
                            ____playerMultitool.SetState(DataConfig.MultiToolState.Null);
                            ____playerMultitool.SetState(DataConfig.MultiToolState.Build);
                        }
                    );
                }
                if (!Managers.GetManager<WindowsHandler>().GetHasUiOpen())
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

            List<int> candidateInventoryIds = [];
            foreach (var wo in WorldObjectsHandler.Instance.GetConstructedWorldObjects())
            {
                var grid = wo.GetGroup().GetId();

                if (grid.StartsWith("Container")
                    && wo.GetLinkedInventoryId() != 0
                    && Vector3.Distance(pos, wo.GetPosition()) <= range.Value
                )
                {
                    candidateInventoryIds.Add(wo.GetLinkedInventoryId());
                }
            }

            Log("  Containers in range: " + candidateInventoryIds.Count);

            var inventoryList = new List<Inventory>();
            foreach (var iid in candidateInventoryIds)
            {
                InventoriesHandler.Instance.GetInventoryById(iid, inventoryList.Add);
            }
            parent.StartCoroutine(GetInventoriesInRangeWait(candidateInventoryIds.Count, inventoryList, onComplete));
        }

        static IEnumerator GetInventoriesInRangeWait(int n, List<Inventory> inventoryList, Action<List<Inventory>> onComplete)
        {
            Log("  Containers in discovered so far: " + inventoryList.Count + " / " + n);
            while (inventoryList.Count != n)
            {
                yield return null;
            }
            Log("  Containers in discovered: " + inventoryList.Count);

            onComplete.Invoke(inventoryList);
            
            Log("Done GetInventoriesInRange");
        }

        static IEnumerator WaitForInventories(int n, ActionCrafter __instance)
        {
            Log("  Waiting for GetInventoryById callbacks: " + candidateInventories.Count + " of " + n);
            while (candidateInventories.Count != n)
            {
                yield return null;
            }
            Log("  GetInventoryById callbacks done: " + candidateInventories.Count);


            UiWindowCraft uiWindowCraft = (UiWindowCraft)Managers.GetManager<WindowsHandler>()
                .OpenAndReturnUi(DataConfig.UiType.Craft);
            uiWindowCraft.SetCrafter(__instance, !__instance.cantCraft);
            uiWindowCraft.ChangeTitle(Localization.GetLocalizedString(__instance.titleLocalizationId));

            inventoryLookupInProgress = false;
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
            ref int ____totalCraft,
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

            DiscoverAvailability(discovery, ingredients, backpackInv, null, null, null);

            bool available = discovery.Count == ingredients.Count;

            __result = available || freeCraft;

            if (__result && !____crafting)
            {
                ____crafting = true;

                WorldObjectsHandler.Instance.CreateAndInstantiateWorldObject(groupItem, sourceCrafter.GetSpawnPosition(), 
                    Quaternion.identity, true, true, true, newSpawnedObject =>
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
                ____totalCraft++;
            }

            return false;
        }

        static void DiscoverAvailability(
            Dictionary<int, (Inventory, WorldObject)> discovery,
            List<Group> ingredients,
            Inventory backpackInv,
            Inventory equipmentInv,
            List<Group> useFromEquipment,
            List<bool> isAvailableList
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
            ref int ____totalCraft,
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

            DiscoverAvailability(discovery, ingredients, backpackInv, equipmentInv, useFromEquipment, null);

            bool available = discovery.Count == ingredients.Count;

            __result = available || freeCraft;

            if (__result && !____crafting)
            {
                if (ingredients.Count == 0 && apiIsFullStackedInventory(backpackInv, groupItem.id))
                {
                    Managers.GetManager<BaseHudHandler>().DisplayCursorText("UI_InventoryFull", 2f);
                    __result = false;
                    return false;
                }
                if (freeCraft && apiIsFullStackedInventory(backpackInv, groupItem.id))
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

                ____totalCraft++;
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
            var ac = Managers.GetManager<PlayersManager>()
                .GetActivePlayerController();
            var backpackInv = ac
                .GetPlayerBackpack()
                .GetInventory();

            if (__instance != backpackInv)
            {
                return true;
            }

            var equipmentInv = ac.GetPlayerEquipment().GetInventory();

            __result = [];

            var discovery = new Dictionary<int, (Inventory, WorldObject)>();

            DiscoverAvailability(discovery, groups, backpackInv, equipmentInv, [], __result);

            return false;
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(PlayerBuilder), "OnConstructed")]
        static bool PlayerBuilder_OnConstructed(PlayerBuilder __instance, 
            ref ConstructibleGhost ___ghost,
            GroupConstructible ___ghostGroupConstructible)
        {
            if (!modEnabled.Value)
            {
                return true;
            }

            ___ghost = null;
            __instance.GetComponent<PlayerAudio>().PlayBuildGhost();
            __instance.GetComponent<PlayerAnimations>().AnimateConstruct(true, -1f);
            __instance.GetComponent<PlayerShareState>().StartConstructing();
            __instance.Invoke("StopAnimation", 0.5f);
            __instance.StartCoroutine(Build_Deduce(__instance, ___ghostGroupConstructible));

            return false;
        }

        static IEnumerator Build_Deduce(PlayerBuilder __instance, GroupConstructible ___ghostGroupConstructible)
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

            if (CheckInventoryForDirectBuild(backpackInv, __instance, ___ghostGroupConstructible))
            {
                yield break;
            }

            foreach (var inv in candidateInventories)
            {
                if (inv != null)
                {
                    if (CheckInventoryForDirectBuild(inv, __instance, ___ghostGroupConstructible))
                    {
                        yield break;
                    }
                }
            }

            var recipe = ___ghostGroupConstructible.GetRecipe().GetIngredientsGroupInRecipe();

            var discovery = new Dictionary<int, (Inventory, WorldObject)>();

            DiscoverAvailability(discovery, recipe, backpackInv, null, null, null);

            yield return RemoveFromInventories(discovery, () =>
            {
                Build_CheckChain(__instance, true, ___ghostGroupConstructible);
            });
        }

        static bool CheckInventoryForDirectBuild(Inventory inv, PlayerBuilder __instance, GroupConstructible ___ghostGroupConstructible)
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
    }
}
