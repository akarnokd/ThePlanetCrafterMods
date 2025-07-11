﻿// Copyright (c) 2022-2025, David Karnok & Contributors
// Licensed under the Apache License, Version 2.0

using BepInEx;
using BepInEx.Configuration;
using SpaceCraft;
using UnityEngine.InputSystem;
using HarmonyLib;
using UnityEngine;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System;
using BepInEx.Logging;
using System.Linq;
using BepInEx.Bootstrap;
using System.Collections;
using LibCommon;
using Unity.Netcode;
using System.Diagnostics;
using System.Text;

namespace CheatInventoryStacking
{
    [BepInPlugin("akarnokd.theplanetcraftermods.cheatinventorystacking", "(Cheat) Inventory Stacking", PluginInfo.PLUGIN_VERSION)]
    [BepInDependency(modCheatCraftFromNearbyContainersGuid, BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency(modStorageBuffer, BepInDependency.DependencyFlags.SoftDependency)]
    public partial class Plugin : BaseUnityPlugin
    {
        const string modCheatCraftFromNearbyContainersGuid = "akarnokd.theplanetcraftermods.cheatcraftfromnearbycontainers";
        const string modStorageBuffer = "valriz.theplanetcrafter.storagebuffer";

        static ConfigEntry<int> stackSize;
        static ConfigEntry<int> fontSize;
        static ConfigEntry<bool> stackTradeRockets;
        static ConfigEntry<bool> stackShredder;
        static ConfigEntry<bool> stackOptimizer;
        static ConfigEntry<bool> stackBackpack;
        static ConfigEntry<bool> stackOreExtractors;
        static ConfigEntry<bool> stackWaterCollectors;
        static ConfigEntry<bool> stackGasExtractors;
        static ConfigEntry<bool> stackBeehives;
        static ConfigEntry<bool> stackBiodomes;
        static ConfigEntry<bool> stackHarvestingRobots;
        static ConfigEntry<bool> stackAutoCrafters;
        static ConfigEntry<bool> stackDroneStation;
        static ConfigEntry<bool> stackAnimalFeeder;
        static ConfigEntry<bool> stackOnlyBackpack;
        static ConfigEntry<bool> stackVehicle;
        static ConfigEntry<bool> stackOreCrusherIn;
        static ConfigEntry<bool> stackOreCrusherOut;
        static ConfigEntry<bool> stackInterplanetaryRockets;
        static ConfigEntry<bool> stackPlanetaryDepots;
        static ConfigEntry<bool> stackEcosystems;

        static ConfigEntry<bool> debugMode;
        static ConfigEntry<bool> debugModeLogMachineGenerator;
        static ConfigEntry<bool> debugModeOptimizations1;
        static ConfigEntry<bool> debugCoordinator;
        static ConfigEntry<int> networkBufferScaling;
        static ConfigEntry<int> logisticsTimeLimit;
        static ConfigEntry<bool> debugOverrideLogisticsAnyway;

        static ConfigEntry<float> offsetX;
        static ConfigEntry<float> offsetY;

        static ConfigEntry<bool> groupListEnabled;
        static ConfigEntry<int> groupListFontSize;
        static ConfigEntry<float> groupListOffsetX;
        static ConfigEntry<float> groupListOffsetY;

        static string expectedGroupIdToAdd;
        static bool isLastSlotOccupiedMode;

        static ManualLogSource logger;

        /// <summary>
        /// The list of pre-placed inventories that should not stack
        /// </summary>
        static readonly HashSet<int> defaultNoStackingInventories =
        [
            109441691, // altar at the door in the mushroom cave
            108035701, // altar in the super alloy cave
            109811866, // altar at the entrance to paradise canyon with 3 slots
            101767269, // altar at the entrance to the last building in the city with 5 slots
            109487734, // fusion generator in the hill wreck
            102606011, // fusion generator in the battleship
            101703877, // fusion generator in the stargate
            101484917, // fusion generator in the luxury cruiser with 3 slots
            107983344, // fusion generator in the lava ship with 2 slots
        ];

        /// <summary>
        /// The set of static and dynamic inventory ids that should not stack.
        /// </summary>
        static HashSet<int> noStackingInventories = [.. defaultNoStackingInventories];

        static PlayersManager playersManager;

        // These methods are not public and we need to relay to them in TrueRefreshContent.
        static MethodInfo mInventoryDisplayerOnImageClicked;
        static MethodInfo mInventoryDisplayerOnDropClicked;
        static MethodInfo mInventoryDisplayerOnActionViaGamepad;
        static MethodInfo mInventoryDisplayerOnConsumeViaGamepad;
        static MethodInfo mInventoryDisplayerOnDropViaGamepad;

        // This is needed by CraftManager.TryToCraftInInventory due to the need of access it from delegates
        static AccessTools.FieldRef<object, bool> fCraftManagerCrafting;
        
        // These are needed by MachineAutoCrafter.TryCraft overrides
        static AccessTools.FieldRef<MachineAutoCrafter, bool> fMachineAutoCrafterHasEnergy;
        static AccessTools.FieldRef<MachineAutoCrafter, Inventory> fMachineAutoCrafterInventory;
        static AccessTools.FieldRef<MachineAutoCrafter, float> fMachineAutoCrafterTimeHasCrafted;
        static AccessTools.FieldRef<MachineAutoCrafter, int> fMachineAutoCrafterInstancedAutocrafters;
        static MethodInfo mMachineAutoCrafterSetItemsInRange;
        static MethodInfo mMachineAutoCrafterCraftIfPossible;
        static MethodInfo mUiWindowTradeUpdateTokenUi;
        static MethodInfo mGroupListOnGroupClicked;

        static Font font;

        static Func<bool> apiTryToCraftInInventoryHandled;

        static AccessTools.FieldRef<LogisticManager, bool> fLogisticManagerUpdatingLogisticTasks;
        static AccessTools.FieldRef<Inventory, List<WorldObject>> fInventoryWorldObjectsInInventory;
        static AccessTools.FieldRef<MachineGrowerVegetationHarvestable, Inventory> fMachineGrowerVegetationHarvestableSecondInventory;
        static Plugin me;

        static readonly Version requiredCFNC = new(1, 0, 0, 14);
        static readonly Version requiredStorageBuffer = new(1, 0, 1);

        static Func<Inventory, WorldObject, Inventory, WorldObject, WorldObject, bool> Api_1_AllowBufferLogisticsTaskEx;

        static AccessTools.FieldRef<MachineDisintegrator, Inventory> fMachineDisintegratorSecondInventory;
        static AccessTools.FieldRef<MachineAutoCrafter, HashSet<WorldObject>> fMachineAutoCrafterWorldObjectsInRange;
        static AccessTools.FieldRef<object, List<(GameObject, Group)>> fMachineAutoCrafterGosInRangeForListing;

        void Awake()
        {
            me = this;

            LibCommon.BepInExLoggerFix.ApplyFix();

            // Plugin startup logic
            Logger.LogInfo($"Plugin is loaded!");
            logger = Logger;

            debugMode = Config.Bind("General", "DebugMode", false, "Produce detailed logs? (chatty)");
            debugOverrideLogisticsAnyway = Config.Bind("General", "DebugOverrideLogisticsAnyway", false, "If true, the custom logistics code still runs on StackSize <= 1");
            debugModeLogMachineGenerator = Config.Bind("General", "DebugModeMachineGenerator", false, "Produce detailed logs for the machine generators? (chatty)");
            debugModeOptimizations1 = Config.Bind("General", "DebugModeOptimizations1", false, "Enables certain type of optimizations (experimental)!");
            debugCoordinator = Config.Bind("General", "DebugCoordinator", false, "Produce coordinator logs (chatty)!");

            stackSize = Config.Bind("General", "StackSize", 10, "The stack size of all item types in the inventory");
            fontSize = Config.Bind("General", "FontSize", 25, "The font size for the stack amount");
            stackTradeRockets = Config.Bind("General", "StackTradeRockets", false, "Should the trade rockets' inventory stack?");
            stackInterplanetaryRockets = Config.Bind("General", "StackInterplanetaryRockets", false, "Should the interplanetary rockets' inventory stack?");
            stackPlanetaryDepots = Config.Bind("General", "StackPlanetaryDepots", false, "Stack the planetary depots?");
            stackShredder = Config.Bind("General", "StackShredder", false, "Should the shredder inventory stack?");
            stackOptimizer = Config.Bind("General", "StackOptimizer", false, "Should the Optimizer's inventory stack?");
            stackBackpack = Config.Bind("General", "StackBackpack", true, "Should the player backpack stack?");
            stackOreExtractors = Config.Bind("General", "StackOreExtractors", true, "Allow stacking in Ore Extractors.");
            stackWaterCollectors = Config.Bind("General", "StackWaterCollectors", true, "Allow stacking in Water Collectors.");
            stackGasExtractors = Config.Bind("General", "StackGasExtractors", true, "Allow stacking in Gas Extractors.");
            stackBeehives = Config.Bind("General", "StackBeehives", true, "Allow stacking in Beehives.");
            stackBiodomes = Config.Bind("General", "StackBiodomes", true, "Allow stacking in Biodomes.");
            stackEcosystems = Config.Bind("General", "StackEcosystems", true, "Allow stacking in Ecosystems.");
            stackHarvestingRobots = Config.Bind("General", "StackHarvestingRobots", true, "Allow stacking in Harvesting Robots");
            stackAutoCrafters = Config.Bind("General", "StackAutoCrafter", true, "Allow stacking in AutoCrafters.");
            stackDroneStation = Config.Bind("General", "StackDroneStation", true, "Allow stacking in Drone Stations.");
            stackAnimalFeeder = Config.Bind("General", "StackAnimalFeeder", false, "Allow stacking in Animal Feeders.");
            stackOnlyBackpack = Config.Bind("General", "StackOnlyBackpack", false, "If true, only the backpack will stack, nothing else no matter the other settings.");
            stackVehicle = Config.Bind("General", "StackVehicle", false, "If true, the inventory of vehicles stack.");
            stackOreCrusherIn = Config.Bind("General", "StackOreCrusherIn", true, "Humble DLC: Stack the input of the Ore Crusher?");
            stackOreCrusherOut = Config.Bind("General", "StackOreCrusherOut", true, "Humble DLC: Stack the output of the Ore Crusher?");

            networkBufferScaling = Config.Bind("General", "NetworkBufferScaling", 1024, "Workaround for the limited vanilla network buffers and too big stack sizes.");
            logisticsTimeLimit = Config.Bind("General", "LogisticsTimeLimit", 5000, "Maximum time allowed to run the logistics calculations per frame, approximately, in microseconds.");

            offsetX = Config.Bind("General", "OffsetX", 0.0f, "Move the stack count display horizontally (- left, + right)");
            offsetY = Config.Bind("General", "OffsetY", 0.0f, "Move the stack count display vertically (- down, + up)");

            groupListEnabled = Config.Bind("GroupList", "Enabled", true, "Enable stacking on the Interplanetary exchange screen's item preview list.");
            groupListFontSize = Config.Bind("GroupList", "FontSize", 15, "The font size for the stack amount on the Interplanetary exchange screen's item preview list.");
            groupListOffsetX = Config.Bind("GroupList", "OffsetX", 0.0f, "Move the stack count display horizontally (- left, + right) on the Interplanetary exchange screen's item preview list.");
            groupListOffsetY = Config.Bind("GroupList", "OffsetY", 0.0f, "Move the stack count display vertically (- down, + up) on the Interplanetary exchange screen's item preview list.");

            mInventoryDisplayerOnImageClicked = AccessTools.Method(typeof(InventoryDisplayer), "OnImageClicked", [typeof(EventTriggerCallbackData)]);
            mInventoryDisplayerOnDropClicked = AccessTools.Method(typeof(InventoryDisplayer), "OnDropClicked", [typeof(EventTriggerCallbackData)]);
            mInventoryDisplayerOnActionViaGamepad = AccessTools.Method(typeof(InventoryDisplayer), "OnActionViaGamepad", [typeof(EventTriggerCallbackData)]);
            mInventoryDisplayerOnConsumeViaGamepad = AccessTools.Method(typeof(InventoryDisplayer), "OnConsumeViaGamepad", [typeof(EventTriggerCallbackData)]);
            mInventoryDisplayerOnDropViaGamepad = AccessTools.Method(typeof(InventoryDisplayer), "OnDropViaGamepad", [typeof(EventTriggerCallbackData)]);
            
            fCraftManagerCrafting = AccessTools.FieldRefAccess<object, bool>(AccessTools.Field(typeof(CraftManager), "_crafting"));
            
            fMachineAutoCrafterHasEnergy = AccessTools.FieldRefAccess<MachineAutoCrafter, bool>("_hasEnergy");
            fMachineAutoCrafterInventory = AccessTools.FieldRefAccess<MachineAutoCrafter, Inventory>("_autoCrafterInventory");
            fMachineAutoCrafterTimeHasCrafted = AccessTools.FieldRefAccess<MachineAutoCrafter, float>("_timeHasCrafted");
            fMachineAutoCrafterInstancedAutocrafters = AccessTools.FieldRefAccess<int>(typeof(MachineAutoCrafter), "_instancedAutocrafters"); ;

            mMachineAutoCrafterSetItemsInRange = AccessTools.Method(typeof(MachineAutoCrafter), "SetItemsInRange");
            mMachineAutoCrafterCraftIfPossible = AccessTools.Method(typeof(MachineAutoCrafter), "CraftIfPossible");


            mUiWindowTradeUpdateTokenUi = AccessTools.Method(typeof(UiWindowTrade), "UpdateTokenUi");

            mGroupListOnGroupClicked = AccessTools.Method(typeof(GroupList), "OnGroupClicked");

            font = Resources.GetBuiltinResource<Font>("Arial.ttf");

            if (Chainloader.PluginInfos.TryGetValue(modCheatCraftFromNearbyContainersGuid, out var pi))
            {
                if (pi.Metadata.Version >= requiredCFNC)
                {
                    logger.LogInfo("Mod " + modCheatCraftFromNearbyContainersGuid + " found, TryToCraftInInventory will be handled by it.");
                    apiTryToCraftInInventoryHandled = (Func<bool>)AccessTools.Field(pi.Instance.GetType(), "apiTryToCraftInInventoryHandled").GetValue(null);
                    AccessTools.Field(pi.Instance.GetType(), "apiIsFullStackedWithRemoveInventory").SetValue(null, apiIsFullStackedWithRemoveInventory);
                }
                else
                {
                    logger.LogError("Mod " + modCheatCraftFromNearbyContainersGuid + " found but incompatible with Stacking: Actual: " + pi.Metadata.Version + ", Expected >= " + requiredCFNC);

                    LibCommon.MainMenuMessage.Patch(new Harmony("akarnokd.theplanetcraftermods.cheatinventorystacking"),
                        "!!! Error !!!\n\n"
                        + "The mod <color=#FFCC00>Inventory Stacking</color> v" + PluginInfo.PLUGIN_VERSION
                        + "\n\n        is incompatible with"
                        + "\n\nthe mod <color=#FFCC00>Craft From Nearby Containers</color> v" + pi.Metadata.Version
                        + "\n\nPlease make sure you have the latest version of both mods."
                        + "\nUntil then, Inventory Stacking will refuse to work."
                        );

                    return;
                }
            }
            else
            {
                logger.LogInfo("Mod " + modCheatCraftFromNearbyContainersGuid + " not found.");
                apiTryToCraftInInventoryHandled = () => false;
            }
            if (Chainloader.PluginInfos.TryGetValue(modStorageBuffer, out pi))
            {
                if (pi.Metadata.Version < requiredStorageBuffer) 
                {
                    logger.LogError("Mod " + modStorageBuffer + " found but incompatible with Stacking: Actual: " + pi.Metadata.Version + ", Expected >= " + requiredStorageBuffer);
                    LibCommon.MainMenuMessage.Patch(new Harmony("akarnokd.theplanetcraftermods.cheatinventorystacking"),
                            "!!! Error !!!\n\n"
                            + "The mod <color=#FFCC00>Inventory Stacking</color> v" + PluginInfo.PLUGIN_VERSION
                            + "\n\n        is incompatible with"
                            + "\n\nthe mod <color=#FFCC00>Storage Buffer</color> v" + pi.Metadata.Version
                            + "\n\nPlease make sure you have the latest version of both mods."
                            + "\nUntil then, Inventory Stacking will refuse to work."
                            );
                    return;
                }
                else
                {
                    logger.LogInfo("Mod " + modStorageBuffer + " found, using its Api_1_AllowBufferLogisticsTaskEx method");
                    Api_1_AllowBufferLogisticsTaskEx = (Func<Inventory, WorldObject, Inventory, WorldObject, WorldObject, bool>)AccessTools.Field(pi.Instance.GetType(), "Api_1_AllowBufferLogisticsTaskEx").GetValue(null);
                }
            }
            else
            {
                logger.LogInfo("Mod " + modStorageBuffer + " not found.");
            }

            fLogisticManagerUpdatingLogisticTasks = AccessTools.FieldRefAccess<LogisticManager, bool>("_updatingLogisticTasks");
            fInventoryWorldObjectsInInventory = AccessTools.FieldRefAccess<Inventory, List<WorldObject>>("_worldObjectsInInventory");

            fMachineGrowerVegetationHarvestableSecondInventory = AccessTools.FieldRefAccess<MachineGrowerVegetationHarvestable, Inventory>("_secondInventory");

            fMachineDisintegratorSecondInventory = AccessTools.FieldRefAccess<MachineDisintegrator, Inventory>("_secondInventory");

            fMachineAutoCrafterWorldObjectsInRange = AccessTools.FieldRefAccess<HashSet<WorldObject>>(typeof(MachineAutoCrafter), "_worldObjectsInRange");
            fMachineAutoCrafterGosInRangeForListing = AccessTools.FieldRefAccess<List<(GameObject, Group)>>(typeof(MachineAutoCrafter), "_gosInRangeForListing");

            CoroutineCoordinator.Init(LogCoord);

            LibCommon.HarmonyIntegrityCheck.Check(typeof(Plugin));
            var harmony = Harmony.CreateAndPatchAll(typeof(Plugin));
            LibCommon.GameVersionCheck.Patch(harmony, "(Cheat) Inventory Stacking - v" + PluginInfo.PLUGIN_VERSION);
        }

        // --------------------------------------------------------------------------------------------------------
        // Helper Methods
        // --------------------------------------------------------------------------------------------------------

        static void Log(string s)
        {
            if (debugMode.Value)
            {
                logger.LogInfo(s);
            }
        }

        static void LogMG(string s)
        {
            if (debugModeLogMachineGenerator.Value)
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

        static Action<EventTriggerCallbackData> CreateMouseCallback(MethodInfo mi, InventoryDisplayer __instance)
        {
            return AccessTools.MethodDelegate<Action<EventTriggerCallbackData>>(mi, __instance);
        }

        static Action<EventTriggerCallbackData> CreateGamepadCallback(MethodInfo mi, InventoryDisplayer __instance)
        {
            return AccessTools.MethodDelegate<Action<EventTriggerCallbackData>>(mi, __instance);
        }

        static List<List<WorldObject>> CreateInventorySlots(List<WorldObject> worldObjects, int n)
        {
            Dictionary<string, List<WorldObject>> currentSlot = [];
            List<List<WorldObject>> slots = [];

            foreach (WorldObject worldObject in worldObjects)
            {
                if (worldObject != null)
                {
                    string gid = GeneticsGrouping.GetStackId(worldObject);

                    if (currentSlot.TryGetValue(gid, out var slot))
                    {
                        slot.Add(worldObject);
                        if (slot.Count == n)
                        {
                            currentSlot.Remove(gid);
                        }
                    }
                    else
                    {
                        slot = [worldObject];
                        slots.Add(slot);
                        currentSlot[gid] = slot;
                    }
                }
            }

            return slots;
        }

        // --------------------------------------------------------------------------------------------------------
        // Main patches
        // --------------------------------------------------------------------------------------------------------

        [HarmonyPrefix]
        [HarmonyPatch(typeof(Inventory), nameof(Inventory.IsFull))]
        static bool Patch_Inventory_IsFull(
            ref bool __result, 
            List<WorldObject> ____worldObjectsInInventory, 
            int ____inventorySize, 
            int ____inventoryId)
        {
            var n = stackSize.Value;
            if (n > 1)
            {
                int count = ____worldObjectsInInventory.Count;
                // we have less than the capacity number of items
                // can't be full even with stacking
                if (count < ____inventorySize)
                {
                    __result = false;
                    return false;
                }
                // this inventory is not allowed to stack?
                // check if count is equal or exceeds the capacity
                if (!CanStack(____inventoryId))
                {
                    __result = count >= ____inventorySize;
                    return false;
                }
                // check if the inventory is fully-full
                // no need to calculate stacks
                if (count >= ____inventorySize * n)
                {
                    __result = true;
                    return false;
                }
                string gid = expectedGroupIdToAdd;
                expectedGroupIdToAdd = null;

                if (isLastSlotOccupiedMode && gid == null)
                {
                    __result = IsLastSlotOccupied(____worldObjectsInInventory, ____inventorySize, gid);
                }
                else
                {
                    __result = IsFullStackedList(____worldObjectsInInventory, ____inventorySize, gid);
                }
                return false;
            }
            return true;
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(Inventory), nameof(Inventory.DropObjectsIfNotEnoughSpace))]
        static bool Patch_Inventory_DropObjectsIfNotEnoughSpace(
            ref List<WorldObject> __result,
            int ____inventoryId,
            List<WorldObject> ____worldObjectsInInventory, 
            int ____inventorySize,
            Vector3 dropPosition,
            bool removeOnly
        )
        {
            if (stackSize.Value > 1 && CanStack(____inventoryId))
            {
                List<WorldObject> toDrop = [];

                while (GetStackCountList(____worldObjectsInInventory) > ____inventorySize)
                {
                    int lastIdx = ____worldObjectsInInventory.Count - 1;
                    WorldObject worldObject = ____worldObjectsInInventory[lastIdx];
                    toDrop.Add(worldObject);

                    if (!removeOnly)
                    {
                        WorldObjectsHandler.Instance.DropOnFloor(worldObject, dropPosition, 0f);
                    }
                    ____worldObjectsInInventory.RemoveAt(lastIdx);
                }

                __result = toDrop;
                return false;
            }
            return true;
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(Inventory), "AddItemInInventory")]
        static void Patch_Inventory_AddItemInInventory(WorldObject worldObject)
        {
            expectedGroupIdToAdd = GeneticsGrouping.GetStackId(worldObject);
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(InventoryDisplayer), "SetInventoryBlocks")]
        static bool Patch_InventoryDisplayer_SetInventoryBlocks(
            InventoryDisplayer __instance,
            LogisticManager ____logisticManager,
            Inventory ____inventory, 
            GridLayoutGroup ____grid, 
            ref int ____selectionIndex,
            ref Inventory ____inventoryInteracting,
            GroupInfosDisplayerBlocksSwitches infosDisplayerBlockSwitches, 
            bool shouldCheckItemsLogisticsStatus, 
            bool enabled,
            VisualsResourcesHandler ____visualResourcesHandler,
            WindowsGamepadHandler ____windowsHandlerControllers
        )
        {
            int n = stackSize.Value;
            if (n > 1 && CanStack(____inventory.GetId()))
            {
                /*
                var sw = new Stopwatch();
                sw.Start();
                */

                int fs = fontSize.Value;

                GameObjects.DestroyAllChildren(____grid.gameObject, false);

                GameObject inventoryBlock = ____visualResourcesHandler.GetInventoryBlock();

                bool showDropIconAtAll = Managers.GetManager<PlayersManager>().GetActivePlayerController().GetPlayerBackpack().GetInventory() == ____inventory;

                var authorizedGroups = ____inventory.GetAuthorizedGroups();
                Sprite authorizedGroupIcon = (authorizedGroups.Count > 0) ? ____visualResourcesHandler.GetGroupItemCategoriesSprite(authorizedGroups.First()) : null;

                __instance.groupSelector.gameObject.SetActive(Application.isEditor && Managers.GetManager<GameSettingsHandler>().GetCurrentGameSettings().GetFreeCraft());

                Action<EventTriggerCallbackData> onImageClickedDelegate = CreateMouseCallback(mInventoryDisplayerOnImageClicked, __instance);
                Action<EventTriggerCallbackData> onDropClickedDelegate = CreateMouseCallback(mInventoryDisplayerOnDropClicked, __instance);

                Action<EventTriggerCallbackData> onActionViaGamepadDelegate = CreateGamepadCallback(mInventoryDisplayerOnActionViaGamepad, __instance);
                Action<EventTriggerCallbackData> onConsumeViaGamepadDelegate = CreateGamepadCallback(mInventoryDisplayerOnConsumeViaGamepad, __instance);
                Action<EventTriggerCallbackData> onDropViaGamepadDelegate = CreateGamepadCallback(mInventoryDisplayerOnDropViaGamepad, __instance);

                var slots = CreateInventorySlots(fInventoryWorldObjectsInInventory(____inventory), n);

                var waitingSlots = ____inventory.GetLogisticEntity().waitingDemandSlots;

                for (int i = 0; i < ____inventory.GetSize(); i++)
                {
                    GameObject gameObject = Instantiate(inventoryBlock, ____grid.transform);
                    InventoryBlock component = gameObject.GetComponent<InventoryBlock>();
                    component.SetAuthorizedGroupIcon(authorizedGroupIcon);

                    if (i < slots.Count)
                    {
                        var slot = slots[i];
                        var worldObject = slot[^1];

                        var showDropIcon = showDropIconAtAll;
                        if (showDropIcon && !(worldObject.GetGroup() is GroupItem gi && gi.GetCantBeDestroyed()))
                        {
                            showDropIcon = worldObject.GetGroup() is not GroupItem gi1 || gi1.GetUsableType() != DataConfig.UsableType.Buildable;
                        }
                        else
                        {
                            showDropIcon = false;
                        }

                        component.SetDisplay(worldObject, infosDisplayerBlockSwitches, showDropIcon);

                        RectTransform rectTransform;

                        if (slot.Count > 1)
                        {
                            var countBackground = new GameObject();
                            countBackground.transform.SetParent(component.transform, false);

                            Image image = countBackground.AddComponent<Image>();
                            image.color = new Color(0.25f, 0.25f, 0.25f, 0.8f);

                            rectTransform = image.GetComponent<RectTransform>();
                            rectTransform.localPosition = new Vector3(offsetX.Value, offsetY.Value, 0);
                            rectTransform.sizeDelta = new Vector2(2 * fs, fs + 5);

                            var count = new GameObject();
                            count.transform.SetParent(component.transform, false);
                            Text text = count.AddComponent<Text>();
                            text.text = slot.Count.ToString();
                            text.font = font;
                            text.color = new Color(1f, 1f, 1f, 1f);
                            text.fontSize = fs;
                            text.resizeTextForBestFit = false;
                            text.verticalOverflow = VerticalWrapMode.Overflow;
                            text.horizontalOverflow = HorizontalWrapMode.Overflow;
                            text.alignment = TextAnchor.MiddleCenter;

                            rectTransform = text.GetComponent<RectTransform>();
                            rectTransform.localPosition = new Vector3(offsetX.Value, offsetY.Value, 0);
                            rectTransform.sizeDelta = new Vector2(2 * fs, fs + 5);
                        }

                        GameObject dropIcon = component.GetDropIcon();
                        if (!worldObject.GetIsLockedInInventory())
                        {
                            var eventTriggerCallbackData = new EventTriggerCallbackData(worldObject)
                            {
                                intValue = i
                            };

                            EventsHelpers.AddTriggerEvent(
                                gameObject, 
                                EventTriggerType.PointerClick,
                                onImageClickedDelegate,
                                eventTriggerCallbackData);
                            EventsHelpers.AddTriggerEvent(
                                dropIcon, 
                                EventTriggerType.PointerClick,
                                onDropClickedDelegate,
                                eventTriggerCallbackData);

                            gameObject.AddComponent<EventGamepadAction>()
                            .SetEventGamepadAction(
                                onActionViaGamepadDelegate,
                                eventTriggerCallbackData,
                                onConsumeViaGamepadDelegate,
                                showDropIconAtAll ? onDropViaGamepadDelegate : null,
                                null
                            );
                        }

                        if (shouldCheckItemsLogisticsStatus)
                        {
                            // Since any of the world objects in a slot could be part of a logistic task,
                            // we need to check all of them and mark the entire slot accordingly
                            var logisticStatusOfSlot = false;
                            foreach (var woInSlot in slot)
                            {
                                if (____logisticManager.WorldObjectIsInTasks(woInSlot))
                                {
                                    logisticStatusOfSlot = true;
                                    break;
                                }
                            }
                            component.SetLogisticStatus(logisticStatusOfSlot);
                        }
                    }
                    else
                    {
                        if (shouldCheckItemsLogisticsStatus)
                        {
                            component.SetLogisticStatus(waitingSlots > 0);
                            waitingSlots -= n;
                        }
                        gameObject.AddComponent<EventGamepadAction>()
                            .SetEventGamepadAction(
                                null, 
                                new EventTriggerCallbackData(i), 
                                null, 
                                null, 
                                null);
                    }
                    gameObject.SetActive(true);
                    if (enabled)
                    {
                        if (i == ____selectionIndex && (____inventoryInteracting == null || ____inventoryInteracting == ____inventory))
                        {
                            ____windowsHandlerControllers.SelectForController(gameObject, true, false, true, true, true);
                        }
                    }
                    else
                    {
                        gameObject.GetComponentInChildren<Selectable>().interactable = false;
                    }
                }

                /*
                invokeCount++;
                var t = Time.realtimeSinceStartup;
                if (t - invokeLast >= 10)
                {
                    invokeLast = t;

                    logger.LogInfo("InventoryDisplayer_SetInventoryBlocks. Count " + invokeCount + ", Time " + invokeSumTime + "ms, Avg " + invokeSumTime / invokeCount + " ms/call");

                    invokeSumTime = 0;
                    invokeCount = 0;
                }
                else
                {
                    invokeSumTime += sw.ElapsedTicks / 10000d;
                }
                */
                return false;
            }
            return true;
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(InventoryDisplayer), "OnImageClicked")]
        static bool Patch_InventoryDisplayer_OnImageClicked(
            EventTriggerCallbackData eventTriggerCallbackData, 
            Inventory ____inventory)
        {
            var n = stackSize.Value;
            if (n > 1 && CanStack(____inventory.GetId()))
            {
                if (eventTriggerCallbackData.pointerEventData.button == PointerEventData.InputButton.Left)
                {
                    if (Keyboard.current[Key.LeftShift].isPressed)
                    {
                        DataConfig.UiType openedUi = Managers.GetManager<WindowsHandler>().GetOpenedUi();
                        if (openedUi == DataConfig.UiType.Container || openedUi == DataConfig.UiType.Genetics || openedUi == DataConfig.UiType.DNAExtractor || openedUi == DataConfig.UiType.DNASynthetizer || openedUi == DataConfig.UiType.GroupSelector)
                        {
                            UiWindowContainer windowContainer = (UiWindowContainer)Managers.GetManager<WindowsHandler>().GetWindowViaUiId(openedUi);
                            Inventory otherInventory = windowContainer.GetOtherInventory(____inventory);
                            if (____inventory != null && otherInventory != null)
                            {
                                Log("Transfer from " + ____inventory.GetId() + " to " + otherInventory.GetId() + " start");
                                WorldObject wo = eventTriggerCallbackData.worldObject;
                                var slots = CreateInventorySlots(fInventoryWorldObjectsInInventory(____inventory), n);

                                foreach (var slot in slots)
                                {
                                    if (slot.Contains(wo))
                                    {

                                        me.StartCoroutine(TransferItems(____inventory, otherInventory, slot, wo, windowContainer));

                                        break;
                                    }
                                }
                            }
                            return false;
                        }
                    }
                }
            }
            return true;
        }

        static IEnumerator TransferItems(
            Inventory ____from, 
            Inventory to, 
            List<WorldObject> slot, 
            WorldObject wo, 
            UiWindowContainer windowContainer)
        {
            var n = 0;
            var m = 0;
            foreach (var item in slot)
            {
                Log("  Move " + item.GetId() + " (" + item.GetGroup().GetId() + ")");
                InventoriesHandler.Instance.TransferItem(____from, to, item, success =>
                {
                    if (success)
                    {
                        n++;
                    }
                    m++;
                });
            }

            while (m != slot.Count)
            {
                yield return null;
            }

            if (n != m && windowContainer.IsOpen)
            {
                InventoriesHandler.Instance.CheckInventoryWatchAndDirty(____from);
                InventoriesHandler.Instance.CheckInventoryWatchAndDirty(to);
            }

            Managers.GetManager<BaseHudHandler>().DisplayCursorText("", 3f,
                            "Transfer " + Readable.GetGroupName(wo.GetGroup()) + " x " + n + " / " + slot.Count + " complete");
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(InventoriesHandler), nameof(InventoriesHandler.DestroyInventory))]
        static void Patch_InventoriesHandler_DestroyInventory(int inventoryId)
        {
            if (!defaultNoStackingInventories.Contains(inventoryId))
            {
                noStackingInventories.Remove(inventoryId);
            }
        }


        /// <summary>
        /// When we quit, we clear the custom inventory ids that should not be stacked.
        /// </summary>
        [HarmonyPostfix]
        [HarmonyPatch(typeof(UiWindowPause), nameof(UiWindowPause.OnQuit))]
        static void Patch_UiWindowPause_OnQuit()
        {
            noStackingInventories = [.. defaultNoStackingInventories];
            stationDistancesCacheCurrentPlanet.Clear();
            nonAttributedTasksCacheAllPlanets.Clear();
            inventoryGroupIsFull.Clear();
            allTasksFrameCache.Clear();
            machineGetInRangeCache = [];
            placedWorldObjects.Clear();
            CoroutineCoordinator.Clear();
            /*
            optimizerLast = null;
            optimizerLastFrame = -1;
            optimizerWorldUnitCache.Clear();
            */
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(BlackScreen), nameof(BlackScreen.DisplayLogoStudio))]
        static void Patch_BlackScreen_DisplayLogoStudio()
        {
            Patch_UiWindowPause_OnQuit();
        }

        // Bypass the vanilla AddItem in the method to avoid restacking for thousands of items.
        [HarmonyPrefix]
        [HarmonyPatch(typeof(InventoriesHandler), "UpdateOrCreateInventoryFromMessage")]
        static bool Patch_InventoriesHandler_UpdateOrCreateInventoryFromMessage(
            int size, 
            int inventoryId, 
            int[] content, 
            int[] contentIds, 
            Inventory newInventory,
            ref Inventory __result
        )
        {
            if (stackSize.Value <= 1)
            {
                return true;
            }

            var timer = Stopwatch.StartNew();

            Inventory inventoryResult;
            if (newInventory == null)
            {
                inventoryResult = new Inventory(inventoryId, size, null, null, null, 0);
            }
            else
            {
                inventoryResult = newInventory;
                inventoryResult.ClearContent(size);
            }
            var list = fInventoryWorldObjectsInInventory(inventoryResult);
            for (int i = 0; i < content.Length; i++)
            {
                WorldObject worldObjectViaId = WorldObjectsHandler.Instance.GetWorldObjectViaId(contentIds[i]) 
                    ?? WorldObjectsHandler.Instance.CreateNewWorldObject(GroupsHandler.GetGroupFromHash(content[i]), contentIds[i], null, true);

                list.Add(worldObjectViaId);
            }
            __result = inventoryResult;

            if (!(NetworkManager.Singleton?.IsServer ?? true))
            {
                Log(string.Format("UpdateOrCreateInventoryFromMessage: {0:0.000} ms", timer.Elapsed.TotalMilliseconds));
            }

            return false;
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(JsonablesHelper), nameof(JsonablesHelper.JsonableToInventory))]
        static bool Patch_JsonablesHelper_JsonableToInventory(JsonableInventory jsonableInventory,
            Dictionary<int, WorldObject> objectMap,
            ref Inventory __result)
        {
            if (stackSize.Value <= 1)
            {
                return true;
            }
            List<WorldObject> list = [];
            var woIds = jsonableInventory.woIds ?? "";
            // Vanilla started limiting this list to 8000 entries, we need to free it
            foreach (string text in woIds.Split(',', StringSplitOptions.None))
            {
                if (!(text == ""))
                {
                    int.TryParse(text, out int num);
                    if (objectMap.ContainsKey(num))
                    {
                        WorldObject worldObject = objectMap[num];
                        list.Add(worldObject);
                    }
                }
            }
            __result = new Inventory(
                jsonableInventory.id, 
                jsonableInventory.size, 
                list, 
                GroupsHandler.GetGroupsViaString(jsonableInventory.supplyGrps, new HashSet<Group>()) as HashSet<Group>,
                GroupsHandler.GetGroupsViaString(jsonableInventory.demandGrps, new HashSet<Group>()) as HashSet<Group>, 
                jsonableInventory.priority
            );

            return false;
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(JsonablesHelper), nameof(JsonablesHelper.InventoryToJsonable))]
        static bool JsonablesHelper_InventoryToJsonable(Inventory inventory,
            ref JsonableInventory __result)
        {
            if (stackSize.Value <= 1)
            {
                return true;
            }
            var content = fInventoryWorldObjectsInInventory(inventory);
            var text = new StringBuilder(content.Count * 10 + 1);

            foreach (var wo in content)
            {
                text.Append(wo.GetId());
                text.Append(',');
            }
            if (content.Count != 0)
            {
                text.Remove(text.Length - 1, 1);
            }
            __result = new JsonableInventory(
                inventory.GetId(),
                text.ToString(),
                inventory.GetSize(),
                GroupsHandler.GetGroupsStringIds(inventory.GetLogisticEntity().GetDemandGroups()),
                GroupsHandler.GetGroupsStringIds(inventory.GetLogisticEntity().GetSupplyGroups()),
                inventory.GetLogisticEntity().GetPriority()
            );
            return false;
        }
    }
}
