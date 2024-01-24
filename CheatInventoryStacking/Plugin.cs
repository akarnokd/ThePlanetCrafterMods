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
using BepInEx.Bootstrap;
using BepInEx.Logging;
using System.Collections;
using System.Linq;
using System.Diagnostics;
using System.Data;
using static UnityEngine.InputSystem.InputSettings;

namespace CheatInventoryStacking
{
    [BepInPlugin("akarnokd.theplanetcraftermods.cheatinventorystacking", "(Cheat) Inventory Stacking", PluginInfo.PLUGIN_VERSION)]
    public class Plugin : BaseUnityPlugin
    {
        static ConfigEntry<int> stackSize;
        static ConfigEntry<int> fontSize;
        static ConfigEntry<bool> stackTradeRockets;
        static ConfigEntry<bool> stackShredder;
        static ConfigEntry<bool> stackOptimizer;
        static ConfigEntry<bool> _stackBackpack;
        static ConfigEntry<bool> stackOreExtractors;
        static ConfigEntry<bool> stackWaterCollectors;
        static ConfigEntry<bool> stackGasExtractors;
        static ConfigEntry<bool> stackBeehives;
        static ConfigEntry<bool> stackBiodomes;
        static ConfigEntry<bool> stackAutoCrafters;

        static ConfigEntry<bool> debugMode;

        static string expectedGroupIdToAdd;

        static ManualLogSource logger;

        /// <summary>
        /// The list of pre-placed inventories that should not stack
        /// </summary>
        static readonly HashSet<int> defaultNoStackingInventories = new()
        {
            109441691, // altar at the door in the mushroom cave
            108035701, // altar in the super alloy cave
            109811866, // altar at the entrance to paradise canyon with 3 slots
            101767269, // altar at the entrance to the last building in the city with 5 slots
            109487734, // fusion generator in the hill wreck
            102606011, // fusion generator in the battleship
            101703877, // fusion generator in the stargate
            101484917, // fusion generator in the luxury cruiser with 3 slots
            107983344, // fusion generator in the lava ship with 2 slots
        };

        /// <summary>
        /// The set of static and dynamic inventory ids that should not stack.
        /// </summary>
        static readonly HashSet<int> _noStackingInventories = new(defaultNoStackingInventories);

        static PlayersManager playersManager;

        // These methods are not public and we need to relay to them in TrueRefreshContent.
        static MethodInfo mInventoryDisplayerOnImageClicked;
        static MethodInfo mInventoryDisplayerOnDropClicked;
        static MethodInfo mInventoryDisplayerOnActionViaGamepad;
        static MethodInfo mInventoryDisplayerOnConsumeViaGamepad;
        static MethodInfo mInventoryDisplayerOnDropViaGamepad;
        static AccessTools.FieldRef<object, bool> fCraftManagerCrafting;

        private void Awake()
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
            _stackBackpack = Config.Bind("General", "StackBackpack", true, "Should the player backpack stack?");
            stackOreExtractors = Config.Bind("General", "StackOreExtractors", true, "Allow stacking in Ore Extractors.");
            stackWaterCollectors = Config.Bind("General", "StackWaterCollectors", true, "Allow stacking in Water Collectors.");
            stackGasExtractors = Config.Bind("General", "StackGasExtractors", true, "Allow stacking in Gas Extractors.");
            stackBeehives = Config.Bind("General", "StackBeehives", true, "Allow stacking in Beehives.");
            stackBiodomes = Config.Bind("General", "StackBiodomes", true, "Allow stacking in Biodomes.");
            stackAutoCrafters = Config.Bind("General", "StackAutoCrafter", true, "Allow stacking in AutoCrafters.");

            mInventoryDisplayerOnImageClicked = AccessTools.Method(typeof(InventoryDisplayer), "OnImageClicked", [typeof(EventTriggerCallbackData)]);
            mInventoryDisplayerOnDropClicked = AccessTools.Method(typeof(InventoryDisplayer), "OnDropClicked", [typeof(EventTriggerCallbackData)]);
            mInventoryDisplayerOnActionViaGamepad = AccessTools.Method(typeof(InventoryDisplayer), "OnActionViaGamepad", [typeof(WorldObject), typeof(Group), typeof(int)]);
            mInventoryDisplayerOnConsumeViaGamepad = AccessTools.Method(typeof(InventoryDisplayer), "OnConsumeViaGamepad", [typeof(WorldObject), typeof(Group), typeof(int)]);
            mInventoryDisplayerOnDropViaGamepad = AccessTools.Method(typeof(InventoryDisplayer), "OnConsumeViaGamepad", [typeof(WorldObject), typeof(Group), typeof(int)]);
            fCraftManagerCrafting = AccessTools.FieldRefAccess<object, bool>(AccessTools.Field(typeof(CraftManager), "_crafting"));

            var harmony = Harmony.CreateAndPatchAll(typeof(Plugin));
            LibCommon.GameVersionCheck.Patch(harmony, "(Cheat) Inventory Stacking - v" + PluginInfo.PLUGIN_VERSION);
        }

        // --------------------------------------------------------------------------------------------------------
        // API: delegates to call from other mods.
        // --------------------------------------------------------------------------------------------------------

        /// <summary>
        /// Given a sequence of WorldObjects, return the number stacks it would coalesce into.
        /// </summary>
        public static readonly Func<IEnumerable<WorldObject>, int> apiGetStackCount = GetStackCount;

        /// <summary>
        /// Given an Inventory, return the number of stacks it's contents would coalesce into.
        /// The inventory is checked if it does allows stacking or not.
        /// </summary>
        public static readonly Func<Inventory, int> apiGetStackCountInventory = GetStackCountOfInventory;

        /// <summary>
        /// Given a sequence of WorldObjects, a maximum stack count and an optional item group id, return true if
        /// the number of stacks the sequence would coalesce (including the item) would
        /// be more than the maximum stack count.
        /// </summary>
        public static readonly Func<IEnumerable<WorldObject>, int, string, bool> apiIsFullStacked = IsFullStacked;

        /// <summary>
        /// Given an Inventory and an optional item group id, return true if
        /// the number of stacks the contents would coalesce (including the item) would
        /// be more than the maximum stack count for that inventory.
        /// The inventory is checked if it does allows stacking or not.
        /// </summary>
        public static readonly Func<Inventory, string, bool> apiIsFullStackedInventory = IsFullStackedOfInventory;

        /// <summary>
        /// Given an Inventory, return the maximum number of homogeneous items it can hold,
        /// subject to if said inventory is allowed to stack or not.
        /// </summary>
        public static readonly Func<Inventory, int> apiGetCapacityInventory = GetCapacityOfInventory;

        /// <summary>
        /// Checks if the given inventory, identified by its id, is allowed to stack items, depending
        /// on the mod's settings.
        /// It also checks if any player's backpack is allowed to stack or not.
        /// </summary>
        public static readonly Func<int, bool> apiCanStack = CanStack;

        /// <summary>
        /// Overwrite this function from other mod(s) to indicate that mod
        /// is ready to provide a reasonable result in the <see cref="FindInventoryForGroupID"/> call.
        /// I.e., if the mod's functionality is currently disabled, return false here
        /// and <see cref="FindInventoryForGroupID"/> won't be called. In the relevant methods,
        /// the default inventory for that particular case will be used instead.
        /// </summary>
        public static Func<bool> IsFindInventoryForGroupIDEnabled;

        /// <summary>
        /// Overwrite this function from other mod(s) to modify the deposition logic
        /// of MachineGenerator::GenerateAnObject. Return null if no inventory can be found.
        /// Use <see cref="IsFindInventoryForGroupIDEnabled"/> to indicate if this method
        /// should be called at all or not.
        /// </summary>
        public static Func<string, Inventory> FindInventoryForGroupID;

        // -------------------------------------------------------------------------------------------------------------
        // API: pointed to by the delegates. Please use the delegates instead of doing reflective method calls on these.
        // -------------------------------------------------------------------------------------------------------------

        /// <summary>
        /// Returns the number of stacks in the given list (inventory content).
        /// </summary>
        /// <param name="items">The list of items to count</param>
        /// <returns>The number of stacks occupied by the list of items.</returns>
        static int GetStackCount(IEnumerable<WorldObject> items)
        {
            Dictionary<string, int> groupCounts = [];

            int n = stackSize.Value;
            int stacks = 0;
            foreach (WorldObject worldObject in items)
            {
                AddToStack(worldObject.GetGroup().GetId(), groupCounts, n, ref stacks);
            }

            return stacks;
        }

        /// <summary>
        /// Checks if the given list of WorldObjects, representing an inventory,
        /// is full or not if one tries to add the optional item indicated by
        /// its group id.
        /// </summary>
        /// <param name="worldObjectsInInventory">The list of world objects already in the inventory.</param>
        /// <param name="inventorySize">The inventory size in number of stacks.</param>
        /// <param name="gid">The optional item group id to check if it can be added or not.</param>
        /// <returns>True if the Inventory would occupy more slots than inventorySize.</returns>
        static bool IsFullStacked(IEnumerable<WorldObject> worldObjectsInInventory, int inventorySize, string gid = null)
        {
            Dictionary<string, int> groupCounts = [];

            int n = stackSize.Value;
            int stacks = 0;

            foreach (WorldObject worldObject in worldObjectsInInventory)
            {
                AddToStack(worldObject.GetGroup().GetId(), groupCounts, n, ref stacks);
            }

            if (gid != null)
            {
                AddToStack(gid, groupCounts, n, ref stacks);
            }

            return stacks > inventorySize;
        }

        /// <summary>
        /// Returns the number of stacks in the given inventory.
        /// </summary>
        /// <param name="inventory">The target inventory</param>
        /// <returns>The number of stacks occupied by the list of items.</returns>
        static int GetStackCountOfInventory(Inventory inventory)
        {
            if (!CanStack(inventory.GetId()))
            {
                return inventory.GetInsideWorldObjects().Count;
            }
            return GetStackCount(inventory.GetInsideWorldObjects());
        }

        /// <summary>
        /// Checks if the given inventory is full or not if one tries to add the optional item indicated by
        /// its group id.
        /// </summary>
        /// <param name="inventory">The target inventory.</param>
        /// <param name="gid">The optional item group id to check if it can be added or not.</param>
        /// <returns>True if the list is full.</returns>
        static bool IsFullStackedOfInventory(Inventory inventory, string gid = null)
        {
            if (!CanStack(inventory.GetId()))
            {
                int count = inventory.GetInsideWorldObjects().Count;
                if (gid != null)
                {
                    count++;
                }
                return count > inventory.GetSize();
            }

            return IsFullStacked(inventory.GetInsideWorldObjects(), inventory.GetSize(), gid);
        }

        /// <summary>
        /// Returns the total item capacity of the given inventory considering the stacking settings.
        /// </summary>
        /// <param name="inventory">The inventory to get the capacity of.</param>
        /// <returns>The capacity.</returns>
        static int GetCapacityOfInventory(Inventory inventory)
        {
            if (!CanStack(inventory.GetId()) || stackSize.Value <= 1)
            {
                return inventory.GetSize();
            }
            return inventory.GetSize() * stackSize.Value;
        }

        /// <summary>
        /// Checks if the given inventory (identified by its id) is allowed to stack items.
        /// Does not check if stackSize > 1 on its own!
        /// </summary>
        /// <param name="inventoryId">The target inventory to check.</param>
        /// <returns>True if the target inventory can stack.</returns>
        static bool CanStack(int inventoryId)
        {
            if (_noStackingInventories.Contains(inventoryId))
            {
                return false;
            }
            // In multiplayer, players can come and go, so we must check their dynamic backpack ids.
            // Similarly, the host's backpack id is no longer constant (1).
            // Not as snappy as checking the hashset from before, but we do this only if
            // backpack stacking was explicitly disabled. Usually it won't be for most players.

            if (!_stackBackpack.Value)
            {
                // We cache the PlayersManager here.
                if (playersManager == null)
                {
                    playersManager = Managers.GetManager<PlayersManager>();
                }
                if (playersManager != null)
                {
                    foreach (var player in playersManager.playersControllers)
                    {
                        if (player != null && player.GetPlayerBackpack().GetInventory().GetId() == inventoryId)
                        {
                            return false;
                        }
                    }

                    // FIXME I don't know if playersControllers does include the active controller or not
                    var apc = playersManager.GetActivePlayerController();
                    if (apc != null && apc.GetPlayerBackpack().GetInventory().GetId() == inventoryId)
                    {
                        return false;
                    }
                }
                // FIXME So if the playersManager is not available, does it mean stacking is not really relevant
                // because we are outside a world?
            }
            return true;
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
        // Patches
        // --------------------------------------------------------------------------------------------------------

        [HarmonyPrefix]
        [HarmonyPatch(typeof(Inventory), nameof(Inventory.IsFull))]
        static bool Inventory_IsFull(
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
        static bool Inventory_DropObjectsIfNotEnoughSpace(
            Inventory __instance,
            ref List<WorldObject> __result, 
            List<WorldObject> ____worldObjectsInInventory, 
            int ____inventorySize,
            Vector3 _dropPosition,
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
                        WorldObjectsHandler.Instance.DropOnFloor(worldObject, _dropPosition, 0f);
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
        static void Inventory_AddItemInInventory(WorldObject _worldObject)
        {
            expectedGroupIdToAdd = _worldObject.GetGroup().GetId();
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(InventoryDisplayer), nameof(InventoryDisplayer.TrueRefreshContent))]
        static bool InventoryDisplayer_TrueRefreshContent(
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

                GroupInfosDisplayerBlocksSwitches groupInfosDisplayerBlocksSwitches = new GroupInfosDisplayerBlocksSwitches 
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

                __instance.groupSelector.gameObject.SetActive(Application.isEditor || Managers.GetManager<GameSettingsHandler>().GetCurrentGameSettings().GetFreeCraft());

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
                            GameObject countBackground = new GameObject();
                            countBackground.transform.SetParent(component.transform, false);

                            Image image = countBackground.AddComponent<Image>();
                            image.color = new Color(0.25f, 0.25f, 0.25f, 0.8f);

                            rectTransform = image.GetComponent<RectTransform>();
                            rectTransform.localPosition = new Vector3(0, 0, 0);
                            rectTransform.sizeDelta = new Vector2(2 * fs, fs + 5);

                            GameObject count = new GameObject();
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
        static bool InventoryDisplayer_OnImageClicked(EventTriggerCallbackData eventTriggerCallbackData, Inventory ____inventory)
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

        [HarmonyPrefix]
        [HarmonyPatch(typeof(ActionMinable), nameof(ActionMinable.OnAction))]
        static bool ActionMinable_OnAction(ActionMinable __instance)
        {
            if (stackSize.Value > 1)
            {
                WorldObjectAssociated woa = __instance.GetComponent<WorldObjectAssociated>();
                if (woa != null)
                {
                    WorldObject wo = woa.GetWorldObject();
                    if (wo != null)
                    {
                        expectedGroupIdToAdd = wo.GetGroup().GetId();
                    }
                }
            }
            return true;
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(ActionGrabable), nameof(ActionGrabable.OnAction))]
        static bool ActionGrabable_OnAction(ActionGrabable __instance)
        {
            if (stackSize.Value > 1)
            {
                WorldObjectAssociated woa = __instance.GetComponent<WorldObjectAssociated>();
                if (woa != null)
                {
                    WorldObject wo = woa.GetWorldObject();
                    if (wo != null)
                    {
                        expectedGroupIdToAdd = wo.GetGroup().GetId();
                    }
                }
            }
            return true;
        }

        /// <summary>
        /// Vanilla creates the object in the player's backpack
        /// or replaces the equipment inplace.
        /// It calls Inventory::IsFull 0 to 2 times so we can't sneak in the groupId.
        /// </summary>
        [HarmonyPrefix]
        [HarmonyPatch(typeof(CraftManager), nameof(CraftManager.TryToCraftInInventory))]
        static bool CraftManager_TryToCraftInInventory(
            GroupItem groupItem, 
            PlayerMainController playerController, 
            ActionCrafter sourceCrafter, 
            ref bool ____crafting,
            ref int ____totalCraft, 
            int ____tempSpaceInInventory,
            ref bool __result
        )
        {
            if (stackSize.Value > 1)
            {
                var ingredients = groupItem.GetRecipe().GetIngredientsGroupInRecipe();
                var backpack = playerController.GetPlayerBackpack();
                var backpackInventory = backpack.GetInventory();
                var equipment = playerController.GetPlayerEquipment();
                var equipmentInventory = equipment.GetInventory();

                var isFreeCraft = Managers.GetManager<GameSettingsHandler>().GetCurrentGameSettings().GetFreeCraft();

                var useFromEquipment = new List<Group>();
                
                // Should allow Craft From Containers to work on its own by overriding ItemsContainsStatus.
                var availableBackpack = backpackInventory.ItemsContainsStatus(ingredients);
                
                if (availableBackpack.Contains(item: false))
                {
                    var availableEquipment = equipmentInventory.ItemsContainsStatus(ingredients);

                    for (int i = 0; i < availableEquipment.Count; i++)
                    {
                        if (!availableBackpack[i] && availableEquipment[i])
                        {
                            availableBackpack[i] = true;
                            useFromEquipment.Add(ingredients[i]);
                        }
                    }
                }
                if (!availableBackpack.Contains(item: false) || isFreeCraft)
                {

                    if (ingredients.Count == 0 && IsFullStackedOfInventory(backpackInventory, groupItem.id))
                    {
                        Managers.GetManager<BaseHudHandler>().DisplayCursorText("UI_InventoryFull", 2f);
                        __result = false;
                        return false;
                    }
                    if (isFreeCraft && IsFullStackedOfInventory(backpackInventory, groupItem.id))
                    {
                        Managers.GetManager<BaseHudHandler>().DisplayCursorText("UI_InventoryFull", 2f);
                        __result = false;
                        return false;
                    }

                    ____crafting = true;

                    sourceCrafter?.CraftAnimation(groupItem);

                    if (useFromEquipment.Count != 0)
                    {
                        InventoriesHandler.Instance.SetInventorySize(equipmentInventory, ____tempSpaceInInventory, Vector3.zero);
                        InventoriesHandler.Instance.SetInventorySize(backpackInventory, ____tempSpaceInInventory, Vector3.zero);
                    }

                    InventoriesHandler.Instance.RemoveItemsFromInventory(ingredients, backpackInventory, true, true);

                    equipment.DestroyItemsFromEquipment(useFromEquipment, success =>
                    {
                        InventoriesHandler.Instance.AddItemToInventory(groupItem, backpackInventory, (success, id) =>
                        {
                            fCraftManagerCrafting(null) = false;

                            if (!success)
                            {
                                if (id > 0)
                                {
                                    WorldObjectsHandler.Instance.DestroyWorldObject(id);
                                }
                                RestoreInventorySizes(backpackInventory, equipment, 
                                    -____tempSpaceInInventory, useFromEquipment.Count != 0);
                            }
                            else
                            {
                                if (useFromEquipment.Count != 0 && groupItem.GetEquipableType() != DataConfig.EquipableType.Null)
                                {
                                    equipment.WatchForModifications(enabled: true);
                                    var wo = WorldObjectsHandler.Instance.GetWorldObjectViaId(id);

                                    InventoriesHandler.Instance.TransferItem(backpackInventory, equipmentInventory, 
                                        wo, _ => RestoreInventorySizes(backpackInventory, equipment, 
                                                    -____tempSpaceInInventory, useFromEquipment.Count != 0)
                                    );
                                }
                                else
                                {
                                    RestoreInventorySizes(backpackInventory, equipment, 
                                        -____tempSpaceInInventory, useFromEquipment.Count != 0);
                                }
                            }

                        });
                    });

                    ____totalCraft++;
                    __result = true;
                    return false;
                }
                __result = false;
                return false;
            }
            return true;
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
        [HarmonyPatch(typeof(ActionDeconstructible), nameof(ActionDeconstructible.RetrieveResources))]
        static bool ActionDeconstructible_RetrieveResources(
            GameObject ___gameObjectRoot,
            Inventory playerInventory, 
            out List<int> dropped, 
            out List<int> stored)
        {
            if (stackSize.Value > 1)
            {
                dropped = [];
                stored = [];

                var woa = ___gameObjectRoot.GetComponent<WorldObjectAssociated>();

                if (woa == null || woa.GetWorldObject() == null || woa.GetWorldObject().GetGroup() == null)
                {
                    return false;
                }

                if (Managers.GetManager<GameSettingsHandler>().GetCurrentGameSettings().GetFreeCraft())
                {
                    return false;
                }

                var ingredients = new List<Group>(woa.GetWorldObject().GetGroup().GetRecipe().GetIngredientsGroupInRecipe());

                var panels = ___gameObjectRoot.GetComponentsInChildren<Panel>();
                foreach (var panel in panels)
                {
                    var pconstr = panel.GetPanelGroupConstructible();
                    if (pconstr != null)
                    {
                        ingredients.AddRange(pconstr.GetRecipe().GetIngredientsGroupInRecipe());
                    }
                }

                foreach (var gr in ingredients)
                {
                    if (playerInventory == null || IsFullStackedOfInventory(playerInventory, gr.id))
                    {
                        WorldObjectsHandler.Instance.CreateAndDropOnFloor(gr, ___gameObjectRoot.transform.position + new Vector3(0f, 1f, 0f));
                        dropped.Add(gr.stableHashCode);
                    }
                    else
                    {
                        InventoriesHandler.Instance.AddItemToInventory(gr, playerInventory);
                        stored.Add(gr.stableHashCode);
                    }
                }

                return false;
            }
            dropped = null;
            stored = null;
            return true;
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(MachineGenerator), "GenerateAnObject")]
        static bool MachineGenerator_GenerateAnObject(
            Inventory ___inventory,
            List<GroupData> ___groupDatas,
            bool ___setGroupsDataViaLinkedGroup,
            WorldObject ___worldObject,
            List<GroupData> ___groupDatasTerraStage,
            ref WorldUnitsHandler ___worldUnitsHandler,
            TerraformStage ___terraStage
        )
        {
            if (stackSize.Value > 1)
            {
                // TODO these below are mostly duplicated within (Cheat) Machine Deposit Into Remote Containers
                //      eventually it would be great to get it factored out in some fashion...
                log("GenerateAnObject start");

                if (___worldUnitsHandler == null)
                {
                    ___worldUnitsHandler = Managers.GetManager<WorldUnitsHandler>();
                }
                if (___worldUnitsHandler == null)
                {
                    return false;
                }

                log("    begin ore search");

                Group group = null;
                if (___groupDatas.Count != 0)
                {
                    List<GroupData> list = new(___groupDatas);
                    if (___groupDatasTerraStage.Count != 0 && ___worldUnitsHandler.IsWorldValuesAreBetweenStages(___terraStage, null))
                    {
                        list.AddRange(___groupDatasTerraStage);
                    }
                    group = GroupsHandler.GetGroupViaId(list[UnityEngine.Random.Range(0, list.Count)].id);
                }
                if (___setGroupsDataViaLinkedGroup)
                {
                    if (___worldObject.GetLinkedGroups() != null && ___worldObject.GetLinkedGroups().Count > 0)
                    {
                        group = ___worldObject.GetLinkedGroups()[UnityEngine.Random.Range(0, ___worldObject.GetLinkedGroups().Count)];
                    }
                    else
                    {
                        group = null;
                    }
                }

                // deposit the ore

                if (group != null)
                {
                    string oreId = group.id;

                    log("    ore: " + oreId);

                    var inventory = ___inventory;
                    if ((IsFindInventoryForGroupIDEnabled?.Invoke() ?? false) && FindInventoryForGroupID != null)
                    {
                        inventory = FindInventoryForGroupID(oreId);
                    }

                    if (inventory != null)
                    {
                        InventoriesHandler.Instance.AddItemToInventory(group, inventory, (success, id) =>
                        {
                            if (!success)
                            {
                                log("GenerateAnObject: Machine " + ___worldObject.GetId() + " could not add " + oreId + " to inventory " + inventory.GetId());
                                if (id != 0)
                                {
                                    WorldObjectsHandler.Instance.DestroyWorldObject(id);
                                }
                            }
                        });
                    }
                    else
                    {
                        log("    No suitable inventory found, ore ignored");
                    }
                }
                else
                {
                    log("    ore: none");
                }

                log("GenerateAnObject end");
                return false;
            }
            return true;
        }

        /// <summary>
        /// The vanilla method uses isFull which might be unreliable with stacking enabled,
        /// thus we have to replace the coroutine with our fully rewritten one.
        /// 
        /// Consequently, the MachineAutoCrafter::CraftIfPossible consumes resources and
        /// does not verify the crafted item can be deposited into the local inventory, thus
        /// wasting the ingredients. Another reason to rewrite.
        /// </summary>
        /// <param name="__instance">The underlying MachineAutoCrafter instance to get public values from.</param>
        /// <param name="timeRepeat">How often to craft?</param>
        /// <param name="__result">The overridden coroutine</param>
        /// <returns>false when running with stack size > 1 and not multiplayer, true otherwise.</returns>
        [HarmonyPrefix]
        [HarmonyPatch(typeof(MachineAutoCrafter), "TryToCraft")]
        static bool MachineAutoCrafter_TryToCraft_Patch(MachineAutoCrafter __instance, float timeRepeat, ref IEnumerator __result)
        {
            if (stackSize.Value > 1 && !Chainloader.PluginInfos.ContainsKey(featMultiplayerGuid))
            {
                __result = MachineAutoCrafter_TryToCraft_Override(__instance, timeRepeat);
                return false;
            }
            return true;
        }

        static IEnumerator MachineAutoCrafter_TryToCraft_Override(MachineAutoCrafter __instance, float timeRepeat)
        {
            var hasEnergyField = AccessTools.Field(typeof(MachineAutoCrafter), "hasEnergy");
            var autoCrafterInventoryField = AccessTools.Field(typeof(MachineAutoCrafter), "autoCrafterInventory");
            for (; ; )
            {
                var inv = (Inventory)autoCrafterInventoryField.GetValue(__instance);
                if ((bool)hasEnergyField.GetValue(__instance) && inv != null)
                {
                    var machineWo = __instance.GetComponent<WorldObjectAssociated>().GetWorldObject();
                    if (machineWo != null)
                    {
                        var linkedGroups = machineWo.GetLinkedGroups();
                        if (linkedGroups != null && linkedGroups.Count != 0)
                        {
                            MachineAutoCrafter_CraftIfPossible_Override(__instance, autoCrafterInventoryField, linkedGroups[0]);
                        }
                    }
                }
                yield return new WaitForSeconds(timeRepeat);
            }
        }

        static List<WorldObject> autocrafterCandidateWorldObjects = new();

        static void MachineAutoCrafter_CraftIfPossible_Override(
            MachineAutoCrafter __instance, FieldInfo autoCrafterInventoryField, Group linkedGroup)
        {
            var range = __instance.range;
            var thisPosition = __instance.gameObject.transform.position;

            var outputInventory = __instance.GetComponent<InventoryAssociated>().GetInventory();
            autoCrafterInventoryField.SetValue(__instance, outputInventory);

            // Stopwatch sw = Stopwatch.StartNew();
            // LogAlways("Auto Crafter Telemetry: " + __instance.GetComponent<WorldObjectAssociated>()?.GetWorldObject()?.GetId());

            var recipe = linkedGroup.GetRecipe().GetIngredientsGroupInRecipe();

            var recipeSet = new HashSet<string>();
            foreach (var ingr in recipe)
            {
                recipeSet.Add(ingr.id);
            }

            autocrafterCandidateWorldObjects.Clear();

            foreach (var wo in WorldObjectsHandler.GetAllWorldObjects())
            {
                var pos = wo.GetPosition();
                if (pos != Vector3.zero && Vector3.Distance(pos, thisPosition) < range)
                {
                    var invId = wo.GetLinkedInventoryId();
                    if (invId != 0)
                    {
                        var inv = InventoriesHandler.GetInventoryById(invId);
                        if (inv != null)
                        {
                            foreach (var woi in inv.GetInsideWorldObjects())
                            {
                                if (recipeSet.Contains(woi.GetGroup().GetId()))
                                {
                                    autocrafterCandidateWorldObjects.Add(woi);
                                }
                            }
                        }
                    }
                    else if (wo.GetGroup() is GroupItem gi && recipeSet.Contains(gi.id))
                    {
                        autocrafterCandidateWorldObjects.Add(wo);
                    }
                }
            }

            // LogAlways(string.Format("    Range search: {0:0.000} ms", sw.ElapsedTicks / 10000d));
            // sw.Restart();

            List<WorldObject> toConsume = new();

            int ingredientFound = 0;

            for (int i = 0; i < recipe.Count; i++)
            {
                var recipeGid = recipe[i].GetId();

                for (int j = 0; j < autocrafterCandidateWorldObjects.Count; j++)
                {
                    WorldObject lo = autocrafterCandidateWorldObjects[j];
                    if (lo != null && lo.GetGroup().GetId() == recipeGid)
                    {
                        toConsume.Add(lo);
                        autocrafterCandidateWorldObjects[j] = null;
                        ingredientFound++;
                        break;
                    }
                }
            }

            //LogAlways(string.Format("    Ingredient search: {0:0.000} ms", sw.ElapsedTicks / 10000d));
            //sw.Restart();

            if (ingredientFound == recipe.Count)
            {
                var craftedWo = WorldObjectsHandler.CreateNewWorldObject(linkedGroup);
                if (outputInventory.AddItem(craftedWo))
                {
                    WorldObjectsHandler.DestroyWorldObjects(toConsume, true);
                    // LogAlways(string.Format("    Ingredient destroy: {0:0.000} ms", sw.ElapsedTicks / 10000d));
                    __instance.CraftAnimation((GroupItem)linkedGroup);
                }
                else
                {
                    WorldObjectsHandler.DestroyWorldObject(craftedWo);
                }
            }
        }


        [HarmonyPrefix]
        [HarmonyPatch(typeof(LogisticManager), "SetLogisticTasks")]
        static bool LogisticManager_SetLogisticTasks_Pre(
            bool ___hasLogisticsEnabled,
            Dictionary<int, LogisticTask> ___allLogisticTasks,
            List<MachineDroneStation> ___allDroneStations,
            List<Drone> ___droneFleet,
            List<Inventory> ___supplyInventories,
            List<Inventory> ___demandInventories
        )
        {
            var n = stackSize.Value;
            if (n <= 1)
            {
                return true;
            }

            if (!___hasLogisticsEnabled)
            {
                return false;
            }
            if (getMultiplayerMode != null && getMultiplayerMode() == "CoopClient")
            {
                return false;
            }
                
            // logger.LogInfo("LogisticManager::SetLogisticTasks");
            Dictionary<int, WorldObject> inventoryParent = new();

            SetLogisticTask_SupplyDemand(___supplyInventories, ___demandInventories, ___allLogisticTasks, inventoryParent);
            SetLogisticTask_RemoveDone(___allLogisticTasks);

            var nextNonAttributedTasks = ___allLogisticTasks.Values.Where(t => t.GetTaskState() == LogisticData.TaskState.NotAttributed).GetEnumerator();

            SetLogisticTask_AssignDrones(___droneFleet, nextNonAttributedTasks);
            SetLogisticTask_ReleaseDrones(___allDroneStations, nextNonAttributedTasks);

            return false;
        }

        static void SetLogisticTask_SupplyDemand(
            List<Inventory> ___supplyInventories,
            List<Inventory> ___demandInventories,
            Dictionary<int, LogisticTask> ___allLogisticTasks,
            Dictionary<int, WorldObject> inventoryParent
            )
        {
            var pickablesByDronesWorldObjects = WorldObjectsHandler.GetPickablesByDronesWorldObjects();

            ___demandInventories.Sort((Inventory x, Inventory y) => y.GetLogisticEntity().GetPriority().CompareTo(x.GetLogisticEntity().GetPriority()));
            
            foreach (Inventory demandInventory in ___demandInventories)
            {
                var f = demandInventory.IsFull();
                if (!f)
                {
                    var capacity = demandInventory.GetSize();
                    if (!noStackingInventories.Contains(demandInventory.GetId()))
                    {
                        capacity *= stackSize.Value;
                    }

                    foreach (Group demandGroup in demandInventory.GetLogisticEntity().GetDemandGroups())
                    {
                        foreach (Inventory supplyInventory in ___supplyInventories)
                        {
                            if (supplyInventory != demandInventory)
                            {
                                foreach (Group supplyGroup in supplyInventory.GetLogisticEntity().GetSupplyGroups())
                                {
                                    if (demandGroup == supplyGroup)
                                    {
                                        foreach (WorldObject worldObject in supplyInventory.GetWorldObjectsOfGroup(supplyGroup))
                                        {
                                            if (demandInventory.GetInsideWorldObjects().Count + demandInventory.GetLogisticEntity().waitingDemandSlots < capacity)
                                            {
                                                // logger.LogInfo("Creating task for " + worldObject.GetGroup().GetId());
                                                CreateNewTaskForWorldObject(___allLogisticTasks, supplyInventory, demandInventory, worldObject, inventoryParent);
                                            }
                                        }
                                    }
                                }
                            }
                        }
                        foreach (WorldObject worldObject2 in pickablesByDronesWorldObjects)
                        {
                            if (worldObject2.GetGroup() == demandGroup 
                                && !(worldObject2.GetGameObject() == null) 
                                && !(worldObject2.GetPosition() == Vector3.zero) 
                                && !(worldObject2.GetGameObject().GetComponent<ActionGrabable>() == null) 
                                && worldObject2.GetGameObject().GetComponent<ActionGrabable>().GetCanGrab() 
                                && demandInventory.GetInsideWorldObjects().Count + demandInventory.GetLogisticEntity().waitingDemandSlots < capacity)
                            {
                                CreateNewTaskForWorldObjectForSpawnedObject(___allLogisticTasks, demandInventory, worldObject2, inventoryParent);
                            }
                        }
                    }
                }
            }
        }

        static LogisticTask CreateNewTaskForWorldObject(
            Dictionary<int, LogisticTask> ___allLogisticTasks,
            Inventory _supplyInventory, Inventory _demandInventory, 
            WorldObject _worldObject,
            Dictionary<int, WorldObject> inventoryParent)
        {
            if (___allLogisticTasks.ContainsKey(_worldObject.GetId()))
            {
                return null;
            }
            WorldObject woSupply = GetInventoryParent(_supplyInventory, inventoryParent);
            WorldObject woDemand = GetInventoryParent(_demandInventory, inventoryParent);
            if (woDemand == null || woSupply == null)
            {
                return null;
            }
            LogisticTask logisticTask = new LogisticTask(_worldObject, _supplyInventory, _demandInventory, woSupply, woDemand, false);
            ___allLogisticTasks[_worldObject.GetId()] = logisticTask;
            return logisticTask;
        }

        static LogisticTask CreateNewTaskForWorldObjectForSpawnedObject(
            Dictionary<int, LogisticTask> ___allLogisticTasks,
            Inventory _demandInventory, 
            WorldObject _worldObject,
            Dictionary<int, WorldObject> inventoryParent)
        {
            if (___allLogisticTasks.ContainsKey(_worldObject.GetId()))
            {
                return null;
            }
            WorldObject woDemand = GetInventoryParent(_demandInventory, inventoryParent);
            if (woDemand == null)
            {
                return null;
            }
            LogisticTask logisticTask = new LogisticTask(_worldObject, null, _demandInventory, null, woDemand, true);
            ___allLogisticTasks[_worldObject.GetId()] = logisticTask;
            return logisticTask;
        }


        static WorldObject GetInventoryParent(Inventory inv, Dictionary<int, WorldObject> lookup)
        {
            if (lookup.TryGetValue(inv.GetId(), out var wo))
            {
                return wo;
            }
            wo = WorldObjectsHandler.GetWorldObjectForInventory(inv);
            if (wo != null)
            {
                lookup.Add(inv.GetId(), wo);
            }
            return wo;
        }

        static void SetLogisticTask_RemoveDone(
            Dictionary<int, LogisticTask> ___allLogisticTasks
        )
        {
            var list = new List<int>();
            var isFull = new Dictionary<int, bool>();
            foreach (KeyValuePair<int, LogisticTask> keyValuePair in ___allLogisticTasks)
            {
                Inventory inv = keyValuePair.Value.GetDemandInventory();
                bool f;
                if (isFull.ContainsKey(inv.GetId()))
                {
                    f = isFull[inv.GetId()];
                }
                else
                {
                    f = inv.IsFull();
                    isFull[inv.GetId()] = f;
                }
                if (f)
                {
                    keyValuePair.Value.SetTaskState(LogisticData.TaskState.Done);
                    list.Add(keyValuePair.Key);
                }
                else if (keyValuePair.Value.GetTaskState() == LogisticData.TaskState.Done)
                {
                    list.Add(keyValuePair.Key);
                }
            }

            foreach (int num in list)
            {
                ___allLogisticTasks.Remove(num);
            }

        }

        static void SetLogisticTask_AssignDrones(
                List<Drone> ___droneFleet,
                IEnumerator<LogisticTask> nextNonAttributedTasks
        )
        {
            foreach (var drone in ___droneFleet)
            {
                if (drone.GetLogisticTask() == null)
                { 
                    if (nextNonAttributedTasks.MoveNext())
                    {
                        // logger.LogInfo("Assign task to drone");
                        drone.SetLogisticTask(nextNonAttributedTasks.Current);
                    }
                    else
                    {
                        break;
                    }
                }
            }
        }

        static void SetLogisticTask_ReleaseDrones(
            List<MachineDroneStation> ___allDroneStations,
            IEnumerator<LogisticTask> nextNonAttributedTasks
            )
        {
            var list2 = new List<LogisticStationDistanceToTask>();
            while (nextNonAttributedTasks.MoveNext())
            {
                var logisticTask = nextNonAttributedTasks.Current;
                // logger.LogInfo("Try releasing more drones");
                list2.Clear();
                foreach (var machineDroneStation in ___allDroneStations)
                {
                    if (logisticTask.GetIsSpawnedObject() || logisticTask.GetSupplyInventoryWorldObject() != null)
                    {
                        Vector3 vector = logisticTask.GetIsSpawnedObject() ? logisticTask.GetWorldObjectToMove().GetPosition() : logisticTask.GetSupplyInventoryWorldObject().GetPosition();
                        int num2 = Mathf.RoundToInt(Vector3.Distance(machineDroneStation.gameObject.transform.position, vector));
                        list2.Add(new LogisticStationDistanceToTask(machineDroneStation, (float)num2));
                    }
                }
                list2.Sort();
                foreach (var logisticStationDistanceToTask in list2)
                {
                    GameObject gameObject = logisticStationDistanceToTask.GetMachineDroneStation().TryToReleaseOneDrone();
                    if (gameObject != null && gameObject.GetComponent<Drone>())
                    {
                        gameObject.GetComponent<Drone>().SetLogisticTask(logisticTask);
                        break;
                    }
                }
            }
        }

        /// <summary>
        /// Conditionally disallow stacking in trade rockets.
        /// </summary>
        /// <param name="_inventory"></param>
        [HarmonyPostfix]
        [HarmonyPatch(typeof(MachineTradePlatform), nameof(MachineTradePlatform.SetInventoryTradePlatform))]
        static void MachineTradePlatform_SetInventoryTradePlatform(Inventory _inventory)
        {
            if (!stackTradeRockets.Value)
            {
                noStackingInventories.Add(_inventory.GetId());
            }
        }

        /// <summary>
        /// Disallow stacking in growers.
        /// </summary>
        /// <param name="_inventory"></param>
        [HarmonyPostfix]
        [HarmonyPatch(typeof(MachineGrower), nameof(MachineGrower.SetGrowerInventory))]
        static void MachineGrower_SetGrowerInventory(Inventory _inventory)
        {
            noStackingInventories.Add(_inventory.GetId());
        }

        /// <summary>
        /// Disallow stacking in outside growers.
        /// </summary>
        /// <param name="_inventory"></param>
        [HarmonyPostfix]
        [HarmonyPatch(typeof(MachineOutsideGrower), nameof(MachineOutsideGrower.SetGrowerInventory))]
        static void MachineOutsideGrower_SetGrowerInventory(Inventory _inventory)
        {
            noStackingInventories.Add(_inventory.GetId());
        }

        /// <summary>
        /// Disallow stacking in DNA/Incubator.
        /// </summary>
        /// <param name="_inventory"></param>
        [HarmonyPostfix]
        [HarmonyPatch(typeof(UiWindowGenetics), nameof(UiWindowGenetics.SetGeneticsData))]
        static void UiWindowGenetics_SetGeneticsData(Inventory ___inventoryRight)
        {
            noStackingInventories.Add(___inventoryRight.GetId());
        }

        /// <summary>
        /// Disallow stacking in butterfly farms.
        /// </summary>
        /// <param name="_inventory"></param>
        [HarmonyPostfix]
        [HarmonyPatch(typeof(MachineFlockSpawner), nameof(MachineFlockSpawner.SetSpawnerInventory))]
        static void MachineFlockSpawner_SetSpawnerInventory(MachineFlockSpawner __instance, Inventory _inventory)
        {
            if (__instance.GetComponent<MachineGenerator>() == null)
            {
                noStackingInventories.Add(_inventory.GetId());
            }
        }

        /// <summary>
        /// Conditionally disallow stacking in shredders.
        /// </summary>
        /// <param name="_inventory"></param>
        [HarmonyPostfix]
        [HarmonyPatch(typeof(MachineDestructInventoryIfFull), nameof(MachineDestructInventoryIfFull.SetDestructInventoryInventory))]
        static void MachineDestructInventoryIfFull_SetDestructInventoryInventory(Inventory _inventory)
        {
            if (!stackShredder.Value)
            {
                noStackingInventories.Add(_inventory.GetId());
            }
        }

        /// <summary>
        /// Conditionally disallow stackingin optimizers.
        /// </summary>
        /// <param name="_inventory"></param>
        [HarmonyPostfix]
        [HarmonyPatch(typeof(MachineOptimizer), nameof(MachineOptimizer.SetOptimizerInventory))]
        static void MachineOptimizer_SetOptimizerInventory(Inventory _inventory)
        {
            if (!stackOptimizer.Value)
            {
                noStackingInventories.Add(_inventory.GetId());
            }
        }

        /// <summary>
        /// Conditionally disallow stacking in Ore Extractors, Water and Atmosphere generators.
        /// </summary>
        /// <param name="__instance">The current component used to find the world object's group id</param>
        /// <param name="_inventory">The inventory of the machine being set.</param>
        [HarmonyPostfix]
        [HarmonyPatch(typeof(MachineGenerator), nameof(MachineGenerator.SetGeneratorInventory))]
        static void MachineGenerator_SetGeneratorInventory(MachineGenerator __instance, Inventory _inventory)
        {
            var wo = __instance.GetComponent<WorldObjectAssociated>()?.GetWorldObject();
            if (wo != null)
            {
                var gid = wo.GetGroup().id;
                if (gid.StartsWith("OreExtractor"))
                {
                    if (!stackOreExtractors.Value)
                    {
                        noStackingInventories.Add(_inventory.GetId());
                    }
                }
                else if (gid.StartsWith("WaterCollector"))
                {
                    if (!stackWaterCollectors.Value)
                    {
                        noStackingInventories.Add(_inventory.GetId());
                    }
                }
                else if (gid.StartsWith("GasExtractor"))
                {
                    if (!stackGasExtractors.Value)
                    {
                        noStackingInventories.Add(_inventory.GetId());
                    }
                }
                else if (gid.StartsWith("Beehive"))
                {
                    if (!stackBeehives.Value)
                    {
                        noStackingInventories.Add(_inventory.GetId());
                    }
                }
                else if (gid.StartsWith("Biodome"))
                {
                    if (!stackBiodomes.Value)
                    {
                        noStackingInventories.Add(_inventory.GetId());
                    }
                }
            }
        }

        /// <summary>
        /// Conditionally stack in AutoCrafters.
        /// </summary>
        /// <param name="_inventory">The inventory of the AutoCrafter.</param>
        [HarmonyPostfix]
        [HarmonyPatch(typeof(MachineAutoCrafter), nameof(MachineAutoCrafter.SetAutoCrafterInventory))]
        static void MachineAutoCrafter_SetAutoCrafterInventory(Inventory _autoCrafterInventory)
        {
            if (!stackAutoCrafters.Value)
            {
                noStackingInventories.Add(_autoCrafterInventory.GetId());
            }
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(InventoriesHandler), nameof(InventoriesHandler.DestroyInventory))]
        static void InventoriesHandler_DestroyInventory(int _inventoryId)
        {
            if (!defaultNoStackingInventories.Contains(_inventoryId))
            {
                noStackingInventories.Remove(_inventoryId);
            }
        }


        /// <summary>
        /// When we quit, we clear the custom inventory ids that should not be stacked.
        /// </summary>
        [HarmonyPostfix]
        [HarmonyPatch(typeof(UiWindowPause), nameof(UiWindowPause.OnQuit))]
        static void UiWindowPause_OnQuit()
        {
            noStackingInventories = new(defaultNoStackingInventories);
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(MachineTradePlatform), "OnTradeInventoryModified")]
        static bool MachineTradePlatform_OnTradeInventoryModified(
            MachineTradePlatform __instance, 
            WorldObject ___worldObject,
            Inventory ___inventory)
        {
            // In multiplayer mode, don't do the stuff below
            if (getMultiplayerMode != null && getMultiplayerMode() == "CoopClient")
            {
                return false;
            }
            if (stackTradeRockets.Value && stackSize.Value > 1)
            {
                if (___worldObject != null 
                    && ___worldObject.GetSetting() == 1 
                    && ___inventory.GetSize() * stackSize.Value <= ___inventory.GetInsideWorldObjects().Count) {
                    __instance.SendTradeRocket();
                }
                return false;
            }
            return true;
        }

        // prevent the reentrancy upon deleting an overfilled shredder.
        static bool suppressTryToCleanInventory;

        [HarmonyPrefix]
        [HarmonyPatch(typeof(MachineDestructInventoryIfFull), "TryToCleanInventory")]
        static bool MachineDestructInventoryIfFull_TryToCleanInventory(
            MachineDestructInventoryIfFull __instance,
            WorldObject ___worldObject,
            Inventory ___inventory)
        {
            // In multiplayer mode, don't do the stuff below
            if (getMultiplayerMode != null && getMultiplayerMode() == "CoopClient")
            {
                return false;
            }
            if (stackShredder.Value && stackSize.Value > 1)
            {
                if (!suppressTryToCleanInventory)
                {
                    try
                    {
                        suppressTryToCleanInventory = true;

                        if (___worldObject != null
                            && ___worldObject.GetSetting() == 1
                            && ___inventory.GetSize() * stackSize.Value <= ___inventory.GetInsideWorldObjects().Count)
                        {
                            ___inventory.DestroyAllItemsInside();
                            __instance.actionnableInteractiveToAction?.OnActionInteractive();
                        }
                    } 
                    finally
                    {
                        suppressTryToCleanInventory = false;
                    }
                }
                return false;
            }
            return true;
        }
    }
}
