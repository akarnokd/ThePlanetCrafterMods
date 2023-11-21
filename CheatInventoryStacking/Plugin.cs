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
    [BepInDependency(LibCommon.CraftHelper.modCraftFromContainersGuid, BepInDependency.DependencyFlags.SoftDependency)]
    public class Plugin : BaseUnityPlugin
    {
        const string featMultiplayerGuid = "akarnokd.theplanetcraftermods.featmultiplayer";
        const string cheatMachineRemoteDepositGuid = "akarnokd.theplanetcraftermods.cheatmachineremotedeposit";

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
        static HashSet<int> noStackingInventories = new(defaultNoStackingInventories);

        public static Func<string> getMultiplayerMode;

        /*
        static double invokeSumTime;
        static int invokeCount;
        static float invokeLast;
        */

        private void Awake()
        {
            // Plugin startup logic
            Logger.LogInfo($"Plugin is loaded!");
            logger = Logger;

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

            if (!stackBackpack.Value)
            {
                defaultNoStackingInventories.Add(1);
                noStackingInventories.Add(1);
            }

            LibCommon.CraftHelper.Init(Logger);

            var harmony = Harmony.CreateAndPatchAll(typeof(Plugin));
            LibCommon.SaveModInfo.Patch(harmony);
            LibCommon.GameVersionCheck.Patch(harmony, "(Cheat) Inventory Stacking - v" + PluginInfo.PLUGIN_VERSION);
        }

        // --------------------------------------------------------------------------------------------------------
        // Support for other mods wishing to know about stacked inventory counts
        // --------------------------------------------------------------------------------------------------------

        /// <summary>
        /// Returns the number of stacks in the given list (inventory content).
        /// </summary>
        /// <param name="items">The list of items to count</param>
        /// <returns>The number of stacks occupied by the list of items.</returns>
        public static int GetStackCount(List<WorldObject> items)
        {
            Dictionary<string, int> groupCounts = new Dictionary<string, int>();

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
        /// <returns>True if the list is full.</returns>
        public static bool IsFullStacked(List<WorldObject> worldObjectsInInventory, int inventorySize, string gid = null)
        {
            Dictionary<string, int> groupCounts = new Dictionary<string, int>();

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
        public static int GetStackCount(Inventory inventory)
        {
            if (noStackingInventories.Contains(inventory.GetId()))
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
        public static bool IsFullStacked(Inventory inventory, string gid = null)
        {
            if (noStackingInventories.Contains(inventory.GetId()))
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
        public static int GetInventoryCapacity(Inventory inventory)
        {
            if (noStackingInventories.Contains(inventory.GetId()) || stackSize.Value <= 1)
            {
                return inventory.GetSize();
            }
            return inventory.GetSize() * stackSize.Value;
        }

        // --------------------------------------------------------------------------------------------------------
        // Helper Methods
        // --------------------------------------------------------------------------------------------------------

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

        static Action<EventTriggerCallbackData> CreateMouseCallback(string name, InventoryDisplayer __instance)
        {
            MethodInfo mi = AccessTools.Method(typeof(InventoryDisplayer), name, new Type[] { typeof(EventTriggerCallbackData) });
            return AccessTools.MethodDelegate<Action<EventTriggerCallbackData>>(mi, __instance);
        }
        static Action<WorldObject, Group, int> CreateGamepadCallback(string name, InventoryDisplayer __instance)
        {
            MethodInfo mi = AccessTools.Method(typeof(InventoryDisplayer), name, new Type[] { typeof(WorldObject), typeof(Group), typeof(int) });
            return AccessTools.MethodDelegate<Action<WorldObject, Group, int>>(mi, __instance);
        }

        static List<List<WorldObject>> CreateInventorySlots(List<WorldObject> worldObjects, int n)
        {
            Dictionary<string, List<WorldObject>> currentSlot = new Dictionary<string, List<WorldObject>>();
            List<List<WorldObject>> slots = new List<List<WorldObject>>();


            foreach (WorldObject worldObject in worldObjects)
            {
                string gid = worldObject.GetGroup().GetId();

                if (currentSlot.TryGetValue(gid, out List<WorldObject> slot))
                {
                    slot.Add(worldObject);
                    if (slot.Count == n)
                    {
                        currentSlot.Remove(gid);
                    }
                }
                else
                {
                    slot = new List<WorldObject>();
                    slot.Add(worldObject);
                    slots.Add(slot);
                    currentSlot[gid] = slot;
                }
            }

            return slots;
        }

        // --------------------------------------------------------------------------------------------------------
        // Patches
        // --------------------------------------------------------------------------------------------------------

        [HarmonyPrefix]
        [HarmonyPatch(typeof(Inventory), nameof(Inventory.IsFull))]
        static bool Inventory_IsFull(ref bool __result, List<WorldObject> ___worldObjectsInInventory, int ___inventorySize, int ___inventoryId)
        {
            if (stackSize.Value > 1 && !noStackingInventories.Contains(___inventoryId))
            {
                string gid = expectedGroupIdToAdd;
                expectedGroupIdToAdd = null;
                __result = IsFullStacked(___worldObjectsInInventory, ___inventorySize, gid);
                return false;
            }
            return true;
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(Inventory), nameof(Inventory.DropObjectsIfNotEnoughSpace))]
        static bool Inventory_DropObjectsIfNotEnoughSpace(
            ref List<WorldObject> __result, Vector3 _dropPosition,
            Inventory __instance,
            List<WorldObject> ___worldObjectsInInventory, int ___inventorySize)
        {
            if (stackSize.Value > 1)
            {
                List<WorldObject> toDrop = new List<WorldObject>();

                bool refresh = false;

                while (IsFullStacked(___worldObjectsInInventory, ___inventorySize))
                {
                    int lastIdx = ___worldObjectsInInventory.Count - 1;
                    WorldObject worldObject = ___worldObjectsInInventory[lastIdx];
                    toDrop.Add(worldObject);

                    WorldObjectsHandler.DropOnFloor(worldObject, _dropPosition, 0f);
                    ___worldObjectsInInventory.RemoveAt(lastIdx);

                    __instance.inventoryContentModified?.Invoke(worldObject, false);

                    refresh = true;
                }

                if (refresh)
                {
                    __instance.RefreshDisplayerContent();
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
            LogisticManager ___logisticManager,
            Inventory ___inventory, GridLayoutGroup ___grid, int ___selectionIndex,
            ref Vector2 ___originalSizeDelta, Inventory ___inventoryInteracting)
        {
            int n = stackSize.Value;
            if (n > 1 && !noStackingInventories.Contains(___inventory.GetId()))
            {
                /*
                var sw = new Stopwatch();
                sw.Start();
                */

                int fs = fontSize.Value;

                GameObjects.DestroyAllChildren(___grid.gameObject, false);

                WindowsGamepadHandler manager = Managers.GetManager<WindowsGamepadHandler>();

                GroupInfosDisplayerBlocksSwitches groupInfosDisplayerBlocksSwitches = new GroupInfosDisplayerBlocksSwitches();
                groupInfosDisplayerBlocksSwitches.showActions = true;
                groupInfosDisplayerBlocksSwitches.showDescription = true;
                groupInfosDisplayerBlocksSwitches.showMultipliers = true;
                groupInfosDisplayerBlocksSwitches.showInfos = true;

                VisualsResourcesHandler manager2 = Managers.GetManager<VisualsResourcesHandler>();
                GameObject inventoryBlock = manager2.GetInventoryBlock();

                bool showDropIconAtAll = Managers.GetManager<PlayersManager>().GetActivePlayerController().GetPlayerBackpack().GetInventory() == ___inventory;

                List<Group> authorizedGroups = ___inventory.GetAuthorizedGroups();
                Sprite authorizedGroupIcon = (authorizedGroups.Count > 0) ? manager2.GetGroupItemCategoriesSprite(authorizedGroups[0]) : null;

                bool logisticFlag = ___logisticManager.GetGlobalLogisticsEnabled() && ___inventory.GetLogisticEntity().HasDemandOrSupplyGroups();

                __instance.groupSelector.gameObject.SetActive(Application.isEditor || Managers.GetManager<GameSettingsHandler>().GetCurrentGameSettings().GetFreeCraft());

                Action<EventTriggerCallbackData> onImageClickedDelegate = CreateMouseCallback("OnImageClicked", __instance);
                Action<EventTriggerCallbackData> onDropClickedDelegate = CreateMouseCallback("OnDropClicked", __instance);

                Action<WorldObject, Group, int> onActionViaGamepadDelegate = CreateGamepadCallback("OnActionViaGamepad", __instance);
                Action<WorldObject, Group, int> onConsumeViaGamepadDelegate = CreateGamepadCallback("OnConsumeViaGamepad", __instance);
                Action<WorldObject, Group, int> onDropViaGamepadDelegate = CreateGamepadCallback("OnDropViaGamepad", __instance);

                List<List<WorldObject>> slots = CreateInventorySlots(___inventory.GetInsideWorldObjects(), n);

                for (int i = 0; i < ___inventory.GetSize(); i++)
                {
                    GameObject gameObject = UnityEngine.Object.Instantiate<GameObject>(inventoryBlock, ___grid.transform);
                    InventoryBlock component = gameObject.GetComponent<InventoryBlock>();
                    component.SetAuthorizedGroupIcon(authorizedGroupIcon);

                    if (slots.Count > i)
                    {
                        List<WorldObject> slot = slots[i];
                        WorldObject worldObject = slot[slot.Count - 1];

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

                            gameObject.AddComponent<EventGamepadAction>().SetEventGamepadAction(
                                onActionViaGamepadDelegate,
                                worldObject.GetGroup(), worldObject, i,
                                onConsumeViaGamepadDelegate,
                                onDropViaGamepadDelegate
                            );
                        }

                        if (logisticFlag)
                        {
                            component.SetLogisticStatus(___logisticManager.WorldObjectIsInTasks(worldObject));
                        }

                    }
                    gameObject.SetActive(true);
                    if (i == ___selectionIndex && (___inventoryInteracting == null || ___inventoryInteracting == ___inventory))
                    {
                        manager.SelectForController(gameObject, true);
                    }
                }
                if (___originalSizeDelta == Vector2.zero)
                {
                    ___originalSizeDelta = __instance.GetComponent<RectTransform>().sizeDelta;
                }
                if (___inventory.GetSize() > 35)
                {
                    __instance.GetComponent<RectTransform>().sizeDelta = new Vector2(___originalSizeDelta.x + 70f, ___originalSizeDelta.y);
                    GridLayoutGroup componentInChildren = __instance.GetComponentInChildren<GridLayoutGroup>();
                    componentInChildren.cellSize = new Vector2(76f, 76f);
                    componentInChildren.spacing = new Vector2(3f, 3f);
                }
                else if (___inventory.GetSize() > 28)
                {
                    __instance.GetComponent<RectTransform>().sizeDelta = new Vector2(___originalSizeDelta.x + 50f, ___originalSizeDelta.y);
                }
                else
                {
                    __instance.GetComponent<RectTransform>().sizeDelta = ___originalSizeDelta;
                }
                __instance.SetIconsPositionRelativeToGrid();

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
        [HarmonyPatch(typeof(InventoryDisplayer), nameof(InventoryDisplayer.OnMoveAll))]
        static bool InventoryDisplayer_OnMoveAll(Inventory ___inventory)
        {
            if (stackSize.Value > 1 && !noStackingInventories.Contains(___inventory.GetId()))
            {
                // The original always tries to move the 0th inventory item so
                // when that has no room in the target, the 1st, 2nd etc items wouldn't move either
                // so I have to rewrite part of the method
                DataConfig.UiType openedUi = Managers.GetManager<WindowsHandler>().GetOpenedUi();
                if (openedUi == DataConfig.UiType.Container || openedUi == DataConfig.UiType.Genetics)
                {
                    Inventory otherInventory = ((UiWindowContainer)Managers.GetManager<WindowsHandler>().GetWindowViaUiId(openedUi)).GetOtherInventory(___inventory);
                    if (___inventory != null && otherInventory != null)
                    {
                        List<WorldObject> sourceList = ___inventory.GetInsideWorldObjects();
                        for (int i = sourceList.Count - 1; i >= 0; i--)
                        {
                            WorldObject obj = sourceList[i];
                            if (!obj.GetIsLockedInInventory() && otherInventory.AddItem(obj))
                            {
                                ___inventory.RemoveItem(obj);
                            }
                        }
                    }
                    return false;
                }
            }
            return true;
        }
        [HarmonyPrefix]
        [HarmonyPatch(typeof(InventoryDisplayer), "OnImageClicked")]
        static bool InventoryDisplayer_OnImageClicked(EventTriggerCallbackData _eventTriggerCallbackData, Inventory ___inventory)
        {
            var n = stackSize.Value;
            if (n > 1 && !noStackingInventories.Contains(___inventory.GetId()))
            {
                if (_eventTriggerCallbackData.pointerEventData.button == PointerEventData.InputButton.Left)
                {
                    if (Keyboard.current[Key.LeftShift].isPressed)
                    {
                        WorldObject wo = _eventTriggerCallbackData.worldObject;
                        DataConfig.UiType openedUi = Managers.GetManager<WindowsHandler>().GetOpenedUi();
                        if (openedUi == DataConfig.UiType.Container || openedUi == DataConfig.UiType.GroupSelector)
                        {
                            Inventory otherInventory = ((UiWindowContainer)Managers.GetManager<WindowsHandler>().GetWindowViaUiId(openedUi)).GetOtherInventory(___inventory);
                            if (___inventory != null && otherInventory != null)
                            {
                                List<List<WorldObject>> slots = CreateInventorySlots(___inventory.GetInsideWorldObjects(), n);

                                foreach (List<WorldObject> wos in slots)
                                {
                                    if (wos.Contains(wo))
                                    {
                                        foreach (WorldObject tomove in wos)
                                        {
                                            if (otherInventory.AddItem(tomove))
                                            {
                                                ___inventory.RemoveItem(tomove);
                                            }
                                            else
                                            {
                                                break;
                                            }
                                        }

                                        Managers.GetManager<BaseHudHandler>().DisplayCursorText("", 3f, "Transfer " + wo.GetGroup().GetId() + " x " + wos.Count);
                                        break;
                                    }
                                }

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

        [HarmonyPrefix]
        [HarmonyPatch(typeof(CraftManager), nameof(CraftManager.TryToCraftInInventory))]
        static bool CraftManager_TryToCraftInInventory(GroupItem groupItem, PlayerMainController _playerController, 
            ActionCrafter _sourceCrafter, ref int ___totalCraft, ref bool __result)
        {
            if (stackSize.Value > 1)
            {
                // In multiplayer mode, don't do the stuff below
                if (getMultiplayerMode == null || getMultiplayerMode() == "SinglePlayer")
                {
                    Inventory backpack = _playerController.GetPlayerBackpack().GetInventory();
                    Inventory equipment = _playerController.GetPlayerEquipment().GetInventory();

                    __result = LibCommon.CraftHelper.TryCraftInventory(
                        groupItem,
                        _playerController.transform.position,
                        backpack,
                        equipment,
                        Managers.GetManager<GameSettingsHandler>().GetCurrentGameSettings().GetFreeCraft(),
                        true,
                        (inv, gr) => !IsFullStacked(inv, gr.GetId()),
                        null,
                        wo =>
                        {
                            _playerController.GetPlayerEquipment().AddItemInEquipment(wo);

                            // Undo the temporary excess storage due to possible exoskeleton/backpack upgrades
                            backpack.SetSize(backpack.GetSize() - 50);
                            equipment.SetSize(equipment.GetSize() - 50);
                        },
                        grs =>
                        {
                            // Since this is called if the equipment is upgraded inplace,
                            // removing the exoskeleton or backpack would decrease the capacities temporarily
                            // and would spill or drop the excess items from both, before the new mod could make room for them
                            // again. Thus, we artificially increase the capacities temporarily to bridge this temporary reduction.
                            // Of course, we have to then remove the excess storage once the new mods are added above.
                            backpack.SetSize(backpack.GetSize() + 50);
                            equipment.SetSize(equipment.GetSize() + 50);

                            _playerController.GetPlayerEquipment().DestroyItemsFromEquipment(grs);
                        },
                        true
                    );
                    if (__result)
                    {
                        _sourceCrafter?.CraftAnimation(groupItem);
                        ___totalCraft++;
                    }
                    return false;
                }
            }
            return true;
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(ActionDeconstructible), "FinalyDestroy")]
        static bool ActionDeconstructible_FinalyDestroy(ref Inventory ___playerInventory, ActionDeconstructible __instance, GameObject ___gameObjectRoot)
        {
            if (stackSize.Value > 1)
            {
                // In multiplayer mode, don't do the stuff below
                if (getMultiplayerMode != null && getMultiplayerMode() == "CoopClient")
                {
                    return true;
                }

                // Unfortunately, We have to rewrite it in its entirety
                // foreach (Group group in list)
                // {
                //                                                <-----------------------------
                //    if (this.playerInventory.IsFull())

                if (___playerInventory == null)
                {
                    __instance.Start();
                }
                WorldObjectAssociated component = ___gameObjectRoot.GetComponent<WorldObjectAssociated>();
                List<Group> list = new List<Group>(component.GetWorldObject().GetGroup().GetRecipe().GetIngredientsGroupInRecipe());
                InformationsDisplayer informationsDisplayer = Managers.GetManager<DisplayersHandler>().GetInformationsDisplayer();
                float lifeTime = 2.5f;
                Panel[] componentsInChildren = ___gameObjectRoot.GetComponentsInChildren<Panel>();
                for (int i = 0; i < componentsInChildren.Length; i++)
                {
                    GroupConstructible panelGroupConstructible = componentsInChildren[i].GetPanelGroupConstructible();
                    if (panelGroupConstructible != null)
                    {
                        foreach (Group item in panelGroupConstructible.GetRecipe().GetIngredientsGroupInRecipe())
                        {
                            list.Add(item);
                        }
                    }
                }
                if (!Managers.GetManager<GameSettingsHandler>().GetCurrentGameSettings().GetFreeCraft())
                {
                    foreach (Group group in list)
                    {
                        if (IsFullStacked(___playerInventory.GetInsideWorldObjects(), ___playerInventory.GetSize(), group.GetId()))
                        {
                            WorldObject worldObject = WorldObjectsHandler.CreateAndDropOnFloor(group, ___gameObjectRoot.transform.position + new Vector3(0f, 1f, 0f), 0f);
                            informationsDisplayer.AddInformation(lifeTime, Readable.GetGroupName(worldObject.GetGroup()), DataConfig.UiInformationsType.DropOnFloor, worldObject.GetGroup().GetImage());
                        }
                        else
                        {
                            WorldObject worldObject2 = WorldObjectsHandler.CreateNewWorldObject(group, 0);
                            ___playerInventory.AddItem(worldObject2);
                            informationsDisplayer.AddInformation(lifeTime, Readable.GetGroupName(worldObject2.GetGroup()), DataConfig.UiInformationsType.InInventory, worldObject2.GetGroup().GetImage());
                        }
                    }
                }
                Managers.GetManager<PlayersManager>().GetActivePlayerController().GetAnimations().AnimateRecolt(false);
                if (___gameObjectRoot.GetComponent<WorldObjectFromScene>() != null)
                {
                    component.GetWorldObject().SetDontSaveMe(false);
                }
                WorldObjectsHandler.DestroyWorldObject(component.GetWorldObject());
                UnityEngine.Object.Destroy(___gameObjectRoot);

                return false;
            }
            return true;
        }

        static Group GenerateOre(
            List<GroupData> ___groupDatas,
            bool ___setGroupsDataViaLinkedGroup,
            WorldObject ___worldObject,
            List<GroupData> ___groupDatasTerraStage,
            WorldUnitsHandler ___worldUnitsHandler,
            TerraformStage ___terraStage)
        {
            // Since 0.6.001
            if (___setGroupsDataViaLinkedGroup)
            {
                var linkedGroups = ___worldObject.GetLinkedGroups();
                if (linkedGroups != null && linkedGroups.Count != 0)
                {
                    return linkedGroups[UnityEngine.Random.Range(0, linkedGroups.Count)];
                }
                return null;
            }
            if (___groupDatas.Count != 0)
            {
                // Since 0.7.001
                var groupDatasCopy = new List<GroupData>(___groupDatas);
                if (___groupDatasTerraStage.Count != 0 
                    && ___worldUnitsHandler.IsWorldValuesAreBetweenStages(___terraStage, null))
                {
                    groupDatasCopy.AddRange(___groupDatasTerraStage);
                }

                return GroupsHandler.GetGroupViaId(
                            groupDatasCopy[UnityEngine.Random.Range(0, groupDatasCopy.Count)].id);
            }
            return null;
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(MachineGenerator), "GenerateAnObject")]
        static bool MachineGenerator_GenerateAnObject(Inventory ___inventory, 
            List<GroupData> ___groupDatas, 
            bool ___setGroupsDataViaLinkedGroup,
            WorldObject ___worldObject,
            List<GroupData> ___groupDatasTerraStage,
            WorldUnitsHandler ___worldUnitsHandler,
            TerraformStage ___terraStage)
        {
            if (!Chainloader.PluginInfos.ContainsKey(cheatMachineRemoteDepositGuid)
                && !Chainloader.PluginInfos.ContainsKey(featMultiplayerGuid))
            {
                Group group = GenerateOre(___groupDatas, ___setGroupsDataViaLinkedGroup, ___worldObject,
                    ___groupDatasTerraStage, ___worldUnitsHandler, ___terraStage);

                if (group != null)
                {
                    WorldObject worldObject = WorldObjectsHandler.CreateNewWorldObject(group, 0);

                    if (!___inventory.AddItem(worldObject))
                    {
                        WorldObjectsHandler.DestroyWorldObject(worldObject);
                    }
                }
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
