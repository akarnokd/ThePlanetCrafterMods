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

namespace CheatInventoryStacking
{
    [BepInPlugin("akarnokd.theplanetcraftermods.cheatinventorystacking", "(Cheat) Inventory Stacking", "1.0.0.16")]
    public class Plugin : BaseUnityPlugin
    {
        const string featMultiplayerGuid = "akarnokd.theplanetcraftermods.featmultiplayer";
        const string cheatMachineRemoteDepositGuid = "akarnokd.theplanetcraftermods.cheatmachineremotedeposit";

        static ConfigEntry<int> stackSize;
        static ConfigEntry<int> fontSize;

        static string expectedGroupIdToAdd;

        static Dictionary<int, List<GameObject>> inventoryCountGameObjects = new Dictionary<int, List<GameObject>>();

        static ManualLogSource logger;

        static readonly HashSet<int> noStackingInventories = new()
        {
            109441691, // altar at the door in the mushroom cave
            108035701, // altar in the super alloy cave
            109811866, // altar at the entrance to paradise canyon with 3 slots
            101767269, // altar at the entrance to the last building in the city with 5 slots
        };

        public static Func<string> getMultiplayerMode;

        private void Awake()
        {
            // Plugin startup logic
            Logger.LogInfo($"Plugin is loaded!");

            stackSize = Config.Bind("General", "StackSize", 10, "The stack size of all item types in the inventory");
            fontSize = Config.Bind("General", "FontSize", 25, "The font size for the stack amount");

            logger = Logger;

            Harmony.CreateAndPatchAll(typeof(Plugin));
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
                int fs = fontSize.Value;

                if (inventoryCountGameObjects.TryGetValue(___inventory.GetId(), out List<GameObject> inventoryGO))
                {
                    foreach (GameObject go in inventoryGO)
                    {
                        UnityEngine.Object.Destroy(go);
                    }
                    inventoryGO.Clear();
                }
                else
                {
                    inventoryGO = new List<GameObject>();
                    inventoryCountGameObjects[___inventory.GetId()] = inventoryGO;
                }

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

                __instance.groupSelector.gameObject.SetActive(Application.isEditor || Managers.GetManager<PlayModeHandler>().GetIsFreePlay());

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
                            inventoryGO.Add(countBackground);
                            countBackground.transform.parent = component.transform;

                            Image image = countBackground.AddComponent<Image>();
                            image.color = new Color(0.25f, 0.25f, 0.25f, 0.8f);

                            rectTransform = image.GetComponent<RectTransform>();
                            rectTransform.localPosition = new Vector3(0, 0, 0);
                            rectTransform.sizeDelta = new Vector2(2 * fs, fs + 5);

                            GameObject count = new GameObject();
                            inventoryGO.Add(count);
                            count.transform.parent = component.transform;
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
                if (___inventory.GetSize() > 28)
                {
                    __instance.GetComponent<RectTransform>().sizeDelta = new Vector2(___originalSizeDelta.x + 50f, ___originalSizeDelta.y);
                }
                else
                {
                    __instance.GetComponent<RectTransform>().sizeDelta = ___originalSizeDelta;
                }
                __instance.SetIconsPositionRelativeToGrid();
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
        static bool CraftManager_TryToCraftInInventory(GroupItem groupItem, PlayerMainController _playerController, ref bool __result)
        {
            if (stackSize.Value > 1)
            {
                // FIXME Note, there are IsFull check but ingredient count would be never zero
                // if (ingredientsGroupInRecipe.Count < 1 && inventory.IsFull())
                // freeCraft true could be true?
                // if (freeCraft && inventory.IsFull())

                // UICraftEquipmentInplace: make sure there is only one relevant IsFull check early on
                if (Chainloader.PluginInfos.ContainsKey("akarnokd.theplanetcraftermods.uicraftequipmentinplace"))
                {
                    // If we have the UICraftEquipmentInplace, it does properly check IsFull,
                    // but for equippable types only
                    DataConfig.EquipableType equipType = groupItem.GetEquipableType();
                    if (equipType == DataConfig.EquipableType.OxygenTank
                        || equipType == DataConfig.EquipableType.BackpackIncrease
                        || equipType == DataConfig.EquipableType.EquipmentIncrease
                        || equipType == DataConfig.EquipableType.MultiToolMineSpeed
                        || equipType == DataConfig.EquipableType.BootsSpeed
                        || equipType == DataConfig.EquipableType.Jetpack
                        || equipType == DataConfig.EquipableType.AirFilter
                        || equipType == DataConfig.EquipableType.MultiToolCleanConstruction)
                    {
                        expectedGroupIdToAdd = groupItem.GetId();
                        return true;
                    }
                }

                // otherwise, we have to manually prefix the vanilla TryToCraftInInventory with a fullness check ourselves
                Inventory inventory = _playerController.GetPlayerBackpack().GetInventory();
                if (IsFullStacked(inventory.GetInsideWorldObjects(), inventory.GetSize(), groupItem.GetId()))
                {
                    Managers.GetManager<BaseHudHandler>().DisplayCursorText("UI_InventoryFull", 2f, "");
                    __result = false;
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

        static Group GenerateOre(List<GroupData> ___groupDatas,
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

        static void MachineAutoCrafter_CraftIfPossible_Override(
            MachineAutoCrafter __instance, FieldInfo autoCrafterInventoryField, Group linkedGroup)
        {
            var range = __instance.range;
            var thisPosition = __instance.gameObject.transform.position;

            var outputInventory = __instance.GetComponent<InventoryAssociated>().GetInventory();
            autoCrafterInventoryField.SetValue(__instance, outputInventory);

            List<WorldObject> candidateWorldObjects = new();

            foreach (var wo in WorldObjectsHandler.GetAllWorldObjects())
            {
                if (Vector3.Distance(wo.GetPosition(), thisPosition) < range)
                {
                    var invId = wo.GetLinkedInventoryId();
                    if (invId != 0)
                    {
                        var inv = InventoriesHandler.GetInventoryById(invId);
                        if (inv != null)
                        {
                            candidateWorldObjects.AddRange(inv.GetInsideWorldObjects());
                        }
                    }
                    else if (wo.GetGroup() is GroupItem)
                    {
                        candidateWorldObjects.Add(wo);
                    }
                }
            }

            List<WorldObject> toConsume = new();

            List<Group> recipe = new(linkedGroup.GetRecipe().GetIngredientsGroupInRecipe());

            int ingredientFound = 0;

            for (int i = 0; i < recipe.Count; i++)
            {
                var recipeGid = recipe[i].GetId();

                for (int j = 0; j < candidateWorldObjects.Count; j++)
                {
                    WorldObject lo = candidateWorldObjects[j];
                    if (lo != null && lo.GetGroup().GetId() == recipeGid)
                    {
                        toConsume.Add(lo);
                        candidateWorldObjects[j] = null;
                        ingredientFound++;
                        break;
                    }
                }
            }

            if (ingredientFound == recipe.Count)
            {
                var craftedWo = WorldObjectsHandler.CreateNewWorldObject(linkedGroup);
                if (outputInventory.AddItem(craftedWo))
                {
                    WorldObjectsHandler.DestroyWorldObjects(toConsume, true);
                    __instance.CraftAnimation((GroupItem)linkedGroup);
                }
                else
                {
                    WorldObjectsHandler.DestroyWorldObject(craftedWo);
                }
            }
        }
    }
}
