using BepInEx;
using MijuTools;
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

namespace CheatInventoryStacking
{
    [BepInPlugin("akarnokd.theplanetcraftermods.cheatinventorystacking", "(Cheat) Inventory Stacking", "1.0.0.0")]
    public class Plugin : BaseUnityPlugin
    {

        static ConfigEntry<int> stackSize;
        static ConfigEntry<int> fontSize;

        static ManualLogSource logger;

        private void Awake()
        {
            // Plugin startup logic
            Logger.LogInfo($"Plugin is loaded!");

            stackSize = Config.Bind("General", "StackSize", 10, "The stack size of all item types in the inventory");
            fontSize = Config.Bind("General", "FontSize", 25, "The font size for the stack amount");

            logger = Logger;

            Harmony.CreateAndPatchAll(typeof(Plugin));
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(Inventory), nameof(Inventory.IsFull))]
        static bool Inventory_IsFull(ref bool __result, List<WorldObject> ___worldObjectsInInventory, int ___inventorySize)
        {
            __result = IsFullStacked(___worldObjectsInInventory, ___inventorySize);

            return false;
        }

        static bool IsFullStacked(List<WorldObject> ___worldObjectsInInventory, int ___inventorySize)
        {
            Dictionary<string, int> groupCounts = new Dictionary<string, int>();

            int n = stackSize.Value;
            int stacks = 0;

            foreach (var worldObject in ___worldObjectsInInventory)
            {
                string gid = worldObject.GetGroup().GetId();
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

            return stacks >= ___inventorySize;
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(Inventory), nameof(Inventory.DropObjectsIfNotEnoughSpace))]
        static bool Inventory_DropObjectsIfNotEnoughSpace(
            ref List<WorldObject> __result, Vector3 _dropPosition,
            Inventory __instance,
            List<WorldObject> ___worldObjectsInInventory, int ___inventorySize)
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

        static Dictionary<int, List<GameObject>> inventoryCountGameObjects = new Dictionary<int, List<GameObject>>();

        [HarmonyPrefix]
        [HarmonyPatch(typeof(InventoryDisplayer), nameof(InventoryDisplayer.TrueRefreshContent))]
        static bool InventoryDisplayer_TrueRefreshContent(
            InventoryDisplayer __instance,
            Inventory ___inventory, GridLayoutGroup ___grid, int ___selectionIndex,
            ref Vector2 ___originalSizeDelta, Inventory ___inventoryInteracting)
        {
            int fs = fontSize.Value;
            int n = stackSize.Value;

            if (inventoryCountGameObjects.TryGetValue(___inventory.GetId(), out var inventoryGO))
            {
                foreach (GameObject go in inventoryGO)
                {
                    UnityEngine.Object.Destroy(go);
                }
                inventoryGO.Clear();
            } else
            {
                inventoryGO = new List<GameObject>();
                inventoryCountGameObjects[___inventory.GetId()] = inventoryGO;
            }


            inventoryCountGameObjects.Clear();

            GameObjects.DestroyAllChildren(___grid.gameObject, false);

            WindowsGamepadHandler manager = Managers.GetManager<WindowsGamepadHandler>();
            
            GroupInfosDisplayerBlocksSwitches groupInfosDisplayerBlocksSwitches = new GroupInfosDisplayerBlocksSwitches();
            groupInfosDisplayerBlocksSwitches.showActions = true;
            groupInfosDisplayerBlocksSwitches.showDescription = true;
            groupInfosDisplayerBlocksSwitches.showMultipliers = true;
            groupInfosDisplayerBlocksSwitches.showInfos = true;

            VisualsResourcesHandler manager2 = Managers.GetManager<VisualsResourcesHandler>();
            GameObject inventoryBlock = manager2.GetInventoryBlock();

            bool showDropIcon = Managers.GetManager<PlayersManager>().GetActivePlayerController().GetPlayerBackpack().GetInventory() == ___inventory;

            List<Group> authorizedGroups = ___inventory.GetAuthorizedGroups();
            Sprite authorizedGroupIcon = (authorizedGroups.Count > 0) ? manager2.GetGroupItemCategoriesSprite(authorizedGroups[0]) : null;

            Action<EventTriggerCallbackData> onImageClickedDelegate = CreateMouseCallback("OnImageClicked", __instance);
            Action<EventTriggerCallbackData> onDropClickedDelegate = CreateMouseCallback("OnDropClicked", __instance);

            Action<WorldObject, Group, int> onActionViaGamepadDelegate = CreateGamepadCallback("OnActionViaGamepad", __instance);
            Action<WorldObject, Group, int> onConsumeViaGamepadDelegate = CreateGamepadCallback("OnConsumeViaGamepad", __instance);
            Action<WorldObject, Group, int> onDropViaGamepadDelegate = CreateGamepadCallback("OnDropViaGamepad", __instance);

            Dictionary<string, InventorySlot> currentSlot = new Dictionary<string, InventorySlot>();
            List<InventorySlot> slots = new List<InventorySlot>();

            foreach (WorldObject worldObject in ___inventory.GetInsideWorldObjects())
            {
                string gid = worldObject.GetGroup().GetId();

                if (currentSlot.TryGetValue(gid, out InventorySlot slot))
                {
                    if (++slot.count == n)
                    {
                        currentSlot.Remove(gid);
                    }
                } else
                {
                    slot = new InventorySlot();
                    slot.worldObject = worldObject;
                    slot.count = 1;
                    slots.Add(slot);
                    currentSlot[gid] = slot;
                }
            }

            for (int i = 0; i < ___inventory.GetSize(); i++)
            {
                GameObject gameObject = UnityEngine.Object.Instantiate<GameObject>(inventoryBlock, ___grid.transform);
                InventoryBlock component = gameObject.GetComponent<InventoryBlock>();
                component.SetAuthorizedGroupIcon(authorizedGroupIcon);

                if (slots.Count > i)
                {
                    InventorySlot slot = slots[i];
                    WorldObject worldObject = slot.worldObject;

                    component.SetDisplay(worldObject, groupInfosDisplayerBlocksSwitches, showDropIcon);

                    RectTransform rectTransform;

                    if (slot.count > 1)
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
                        text.text = slot.count.ToString();
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

        class InventorySlot
        {
            internal WorldObject worldObject;
            internal int count;
        }
    }
}
