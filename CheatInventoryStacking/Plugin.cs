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

namespace CheatInventoryStacking
{
    [BepInPlugin("akarnokd.theplanetcraftermods.cheatinventorystacking", "(Cheat) Inventory Stacking", PluginInfo.PLUGIN_VERSION)]
    public partial class Plugin : BaseUnityPlugin
    {
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
        static ConfigEntry<bool> stackAutoCrafters;
        static ConfigEntry<bool> stackDroneStation;

        static ConfigEntry<bool> debugMode;

        static string expectedGroupIdToAdd;

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
        static HashSet<int> noStackingInventories = new(defaultNoStackingInventories);

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
        static MethodInfo mMachineAutoCrafterSetItemsInRange;
        static MethodInfo mMachineAutoCrafterCraftIfPossible;
        static MethodInfo mUiWindowTradeUpdateTokenUi;

        void Awake()
        {
            LibCommon.BepInExLoggerFix.ApplyFix();

            // Plugin startup logic
            Logger.LogInfo($"Plugin is loaded!");
            logger = Logger;

            debugMode = Config.Bind("General", "DebugMode", false, "Produce detailed logs? (chatty)");

            stackSize = Config.Bind("General", "StackSize", 10, "The stack size of all item types in the inventory");
            fontSize = Config.Bind("General", "FontSize", 25, "The font size for the stack amount");
            stackTradeRockets = Config.Bind("General", "StackTradeRockets", false, "Should the trade rockets' inventory stack?");
            stackShredder = Config.Bind("General", "StackShredder", false, "Should the shredder inventory stack?");
            stackOptimizer = Config.Bind("General", "StackOptimizer", false, "Should the Optimizer's inventory stack?");
            stackBackpack = Config.Bind("General", "StackBackpack", true, "Should the player backpack stack?");
            stackOreExtractors = Config.Bind("General", "StackOreExtractors", true, "Allow stacking in Ore Extractors.");
            stackWaterCollectors = Config.Bind("General", "StackWaterCollectors", true, "Allow stacking in Water Collectors.");
            stackGasExtractors = Config.Bind("General", "StackGasExtractors", true, "Allow stacking in Gas Extractors.");
            stackBeehives = Config.Bind("General", "StackBeehives", true, "Allow stacking in Beehives.");
            stackBiodomes = Config.Bind("General", "StackBiodomes", true, "Allow stacking in Biodomes.");
            stackAutoCrafters = Config.Bind("General", "StackAutoCrafter", true, "Allow stacking in AutoCrafters.");
            stackDroneStation = Config.Bind("General", "StackDroneStation", true, "Allow stacking in Drone Stations.");

            mInventoryDisplayerOnImageClicked = AccessTools.Method(typeof(InventoryDisplayer), "OnImageClicked", [typeof(EventTriggerCallbackData)]);
            mInventoryDisplayerOnDropClicked = AccessTools.Method(typeof(InventoryDisplayer), "OnDropClicked", [typeof(EventTriggerCallbackData)]);
            mInventoryDisplayerOnActionViaGamepad = AccessTools.Method(typeof(InventoryDisplayer), "OnActionViaGamepad", [typeof(WorldObject), typeof(Group), typeof(int)]);
            mInventoryDisplayerOnConsumeViaGamepad = AccessTools.Method(typeof(InventoryDisplayer), "OnConsumeViaGamepad", [typeof(WorldObject), typeof(Group), typeof(int)]);
            mInventoryDisplayerOnDropViaGamepad = AccessTools.Method(typeof(InventoryDisplayer), "OnConsumeViaGamepad", [typeof(WorldObject), typeof(Group), typeof(int)]);
            
            fCraftManagerCrafting = AccessTools.FieldRefAccess<object, bool>(AccessTools.Field(typeof(CraftManager), "_crafting"));
            
            fMachineAutoCrafterHasEnergy = AccessTools.FieldRefAccess<MachineAutoCrafter, bool>("_hasEnergy");
            fMachineAutoCrafterInventory = AccessTools.FieldRefAccess<MachineAutoCrafter, Inventory>("_autoCrafterInventory");
            mMachineAutoCrafterSetItemsInRange = AccessTools.Method(typeof(MachineAutoCrafter), "SetItemsInRange");
            mMachineAutoCrafterCraftIfPossible = AccessTools.Method(typeof(MachineAutoCrafter), "CraftIfPossible");

            mUiWindowTradeUpdateTokenUi = AccessTools.Method(typeof(UiWindowTrade), "UpdateTokenUi");

            var harmony = Harmony.CreateAndPatchAll(typeof(Plugin));
            LibCommon.GameVersionCheck.Patch(harmony, "(Cheat) Inventory Stacking - v" + PluginInfo.PLUGIN_VERSION);
        }

        // --------------------------------------------------------------------------------------------------------
        // Helper Methods
        // --------------------------------------------------------------------------------------------------------

        static void log(string s)
        {
            if (debugMode.Value)
            {
                logger.LogInfo(s);
            }
        }

        static void AddToStack(string gid, Dictionary<string, int> groupCounts, int n, ref int stacks)
        {
            groupCounts.TryGetValue(gid, out int count);

            if (++count == 1)
            {
                stacks++;
            }
            if (count > n)
            {
                stacks++;
                count = 1;
            }
            groupCounts[gid] = count;
        }

        static Action<EventTriggerCallbackData> CreateMouseCallback(MethodInfo mi, InventoryDisplayer __instance)
        {
            return AccessTools.MethodDelegate<Action<EventTriggerCallbackData>>(mi, __instance);
        }

        static Action<WorldObject, Group, int> CreateGamepadCallback(MethodInfo mi, InventoryDisplayer __instance)
        {
            return AccessTools.MethodDelegate<Action<WorldObject, Group, int>>(mi, __instance);
        }

        static List<List<WorldObject>> CreateInventorySlots(IEnumerable<WorldObject> worldObjects, int n)
        {
            Dictionary<string, List<WorldObject>> currentSlot = [];
            List<List<WorldObject>> slots = [];

            foreach (WorldObject worldObject in worldObjects)
            {
                if (worldObject != null)
                {
                    string gid = worldObject.GetGroup().GetId();

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
            if (stackSize.Value > 1 && CanStack(____inventoryId))
            {
                string gid = expectedGroupIdToAdd;
                expectedGroupIdToAdd = null;
                __result = IsFullStacked(____worldObjectsInInventory, ____inventorySize, gid);
                return false;
            }
            return true;
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(Inventory), nameof(Inventory.DropObjectsIfNotEnoughSpace))]
        static bool Patch_Inventory_DropObjectsIfNotEnoughSpace(
            Inventory __instance,
            ref List<WorldObject> __result, 
            List<WorldObject> ____worldObjectsInInventory, 
            int ____inventorySize,
            Vector3 dropPosition,
            bool removeOnly
        )
        {
            if (stackSize.Value > 1)
            {
                List<WorldObject> toDrop = [];

                while (IsFullStacked(____worldObjectsInInventory, ____inventorySize))
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
            expectedGroupIdToAdd = worldObject.GetGroup().GetId();
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(InventoryDisplayer), nameof(InventoryDisplayer.TrueRefreshContent))]
        static bool Patch_InventoryDisplayer_TrueRefreshContent(
            InventoryDisplayer __instance,
            LogisticManager ____logisticManager,
            Inventory ____inventory, 
            GridLayoutGroup ____grid, 
            ref int ____selectionIndex,
            ref Vector2 ____originalSizeDelta, 
            ref Inventory ____inventoryInteracting
        )
        {
            int n = stackSize.Value;
            if (n > 1 && CanStack(____inventory.GetId()))
            {
                /*
                var sw = new Stopwatch();
                sw.Start();
                */

                // Since 0.9.020 (vanilla gamepad improvements)
                GameObject currentSelectedGameObject = EventSystem.current.currentSelectedGameObject;
                if (currentSelectedGameObject != null
                    && currentSelectedGameObject.GetComponentInParent<InventoryDisplayer>() == __instance
                    && currentSelectedGameObject.GetComponent<EventGamepadAction>() != null)
                {
                    ____selectionIndex = currentSelectedGameObject.GetComponent<EventGamepadAction>().GetReferenceInt();
                    ____inventoryInteracting = ____inventory;
                }

                int fs = fontSize.Value;

                GameObjects.DestroyAllChildren(____grid.gameObject, false);

                WindowsGamepadHandler manager = Managers.GetManager<WindowsGamepadHandler>();

                var groupInfosDisplayerBlocksSwitches = new GroupInfosDisplayerBlocksSwitches 
                {
                    showActions = true,
                    showDescription = true,
                    showMultipliers = true,
                    showInfos = true
                };

                VisualsResourcesHandler manager2 = Managers.GetManager<VisualsResourcesHandler>();
                GameObject inventoryBlock = manager2.GetInventoryBlock();

                bool showDropIconAtAll = Managers.GetManager<PlayersManager>().GetActivePlayerController().GetPlayerBackpack().GetInventory() == ____inventory;

                var authorizedGroups = ____inventory.GetAuthorizedGroups();
                Sprite authorizedGroupIcon = (authorizedGroups.Count > 0) ? manager2.GetGroupItemCategoriesSprite(authorizedGroups.First()) : null;

                bool logisticFlag = ____logisticManager.GetGlobalLogisticsEnabled() && ____inventory.GetLogisticEntity().HasDemandOrSupplyGroups();

                __instance.groupSelector.gameObject.SetActive(Application.isEditor && Managers.GetManager<GameSettingsHandler>().GetCurrentGameSettings().GetFreeCraft());

                // Since 0.9.020 (vanilla gamepad improvements)
                var selectableEnablerComponent = __instance.GetComponentInParent<SelectableEnabler>();
                var selectablesEnabled = selectableEnablerComponent != null && selectableEnablerComponent.SelectablesEnabled;

                Action<EventTriggerCallbackData> onImageClickedDelegate = CreateMouseCallback(mInventoryDisplayerOnImageClicked, __instance);
                Action<EventTriggerCallbackData> onDropClickedDelegate = CreateMouseCallback(mInventoryDisplayerOnDropClicked, __instance);

                Action<WorldObject, Group, int> onActionViaGamepadDelegate = CreateGamepadCallback(mInventoryDisplayerOnActionViaGamepad, __instance);
                Action<WorldObject, Group, int> onConsumeViaGamepadDelegate = CreateGamepadCallback(mInventoryDisplayerOnConsumeViaGamepad, __instance);
                Action<WorldObject, Group, int> onDropViaGamepadDelegate = CreateGamepadCallback(mInventoryDisplayerOnDropViaGamepad, __instance);

                var slots = CreateInventorySlots(____inventory.GetInsideWorldObjects(), n);

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
                        if (worldObject.GetGroup() is GroupItem gi && gi.GetCantBeDestroyed())
                        {
                            showDropIcon = false;
                        }

                        component.SetDisplay(worldObject, groupInfosDisplayerBlocksSwitches, showDropIcon);

                        RectTransform rectTransform;

                        if (slot.Count > 1)
                        {
                            var countBackground = new GameObject();
                            countBackground.transform.SetParent(component.transform, false);

                            Image image = countBackground.AddComponent<Image>();
                            image.color = new Color(0.25f, 0.25f, 0.25f, 0.8f);

                            rectTransform = image.GetComponent<RectTransform>();
                            rectTransform.localPosition = new Vector3(0, 0, 0);
                            rectTransform.sizeDelta = new Vector2(2 * fs, fs + 5);

                            var count = new GameObject();
                            count.transform.SetParent(component.transform, false);
                            Text text = count.AddComponent<Text>();
                            text.text = slot.Count.ToString();
                            text.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
                            text.color = new Color(1f, 1f, 1f, 1f);
                            text.fontSize = fs;
                            text.resizeTextForBestFit = false;
                            text.verticalOverflow = VerticalWrapMode.Overflow;
                            text.horizontalOverflow = HorizontalWrapMode.Overflow;
                            text.alignment = TextAnchor.MiddleCenter;

                            rectTransform = text.GetComponent<RectTransform>();
                            rectTransform.localPosition = new Vector3(0, 0, 0);
                            rectTransform.sizeDelta = new Vector2(2 * fs, fs + 5);
                        }

                        GameObject dropIcon = component.GetDropIcon();
                        if (!worldObject.GetIsLockedInInventory())
                        {
                            EventsHelpers.AddTriggerEvent(gameObject, EventTriggerType.PointerClick,
                                onImageClickedDelegate, null, worldObject);
                            EventsHelpers.AddTriggerEvent(dropIcon, EventTriggerType.PointerClick,
                                onDropClickedDelegate, null, worldObject);

                            gameObject.AddComponent<EventGamepadAction>()
                            .SetEventGamepadAction(
                                onActionViaGamepadDelegate,
                                worldObject.GetGroup(), 
                                worldObject, 
                                i,
                                onConsumeViaGamepadDelegate,
                                showDropIconAtAll ? onDropViaGamepadDelegate : null,
                                null
                            );
                        }

                        if (logisticFlag)
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
                        if (logisticFlag)
                        {
                            component.SetLogisticStatus(waitingSlots > 0);
                            waitingSlots -= n;
                        }
                        gameObject.AddComponent<EventGamepadAction>()
                            .SetEventGamepadAction(null, null, null, i, null, null, null);
                    }
                    gameObject.SetActive(true);
                    if (selectablesEnabled)
                    {
                        if (i == ____selectionIndex && (____inventoryInteracting == null || ____inventoryInteracting == ____inventory))
                        {
                            manager.SelectForController(gameObject, true, false, true, true, true);
                        }
                    }
                    else
                    {
                        gameObject.GetComponentInChildren<Selectable>().interactable = false;
                    }
                }
                if (____originalSizeDelta == Vector2.zero)
                {
                    ____originalSizeDelta = __instance.GetComponent<RectTransform>().sizeDelta;
                }
                if (____inventory.GetSize() > 35)
                {
                    __instance.GetComponent<RectTransform>().sizeDelta = new Vector2(____originalSizeDelta.x + 70f, ____originalSizeDelta.y);
                    GridLayoutGroup componentInChildren = __instance.GetComponentInChildren<GridLayoutGroup>();
                    componentInChildren.cellSize = new Vector2(76f, 76f);
                    componentInChildren.spacing = new Vector2(3f, 3f);
                }
                else if (____inventory.GetSize() > 28)
                {
                    __instance.GetComponent<RectTransform>().sizeDelta = new Vector2(____originalSizeDelta.x + 50f, ____originalSizeDelta.y);
                }
                else
                {
                    __instance.GetComponent<RectTransform>().sizeDelta = ____originalSizeDelta;
                }
                __instance.SetIconsPositionRelativeToGrid();
                ____selectionIndex = -1;


                /*
                invokeCount++;
                var t = Time.realtimeSinceStartup;
                if (t - invokeLast >= 10)
                {
                    invokeLast = t;

                    logger.LogInfo("InventoryDisplayer_TrueRefreshContent. Count " + invokeCount + ", Time " + invokeSumTime + "ms, Avg " + invokeSumTime / invokeCount + " ms/call");

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
        static bool Patch_InventoryDisplayer_OnImageClicked(EventTriggerCallbackData eventTriggerCallbackData, Inventory ____inventory)
        {
            var n = stackSize.Value;
            if (n > 1 && CanStack(____inventory.GetId()))
            {
                if (eventTriggerCallbackData.pointerEventData.button == PointerEventData.InputButton.Left)
                {
                    if (Keyboard.current[Key.LeftShift].isPressed)
                    {
                        WorldObject wo = eventTriggerCallbackData.worldObject;
                        var slots = CreateInventorySlots(____inventory.GetInsideWorldObjects(), n);

                        foreach (var slot in slots)
                        {
                            if (slot.Contains(wo))
                            {
                                foreach (var toMove in slot)
                                {
                                    // I know it is inefficient but since it does a lot of network stuff,
                                    // I don't want to recreate all of those things.
                                    InventoriesHandler.Instance.AnInventoryHasBeenClicked(____inventory, toMove);
                                }

                                Managers.GetManager<BaseHudHandler>().DisplayCursorText("", 3f, 
                                    "Transfer " + Readable.GetGroupName(wo.GetGroup()) + " x " + slot.Count);

                                break;
                            }
                        }
                        return false;
                    }
                }
            }
            return true;
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
            noStackingInventories = new(defaultNoStackingInventories);
            inventoryOwnerCache.Clear();
            stationDistancesCache.Clear();
            nonAttributedTasksCache.Clear();
            inventoryGroupIsFull.Clear();
        }
    }
}
