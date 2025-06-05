// Copyright (c) 2022-2025, David Karnok & Contributors
// Licensed under the Apache License, Version 2.0

using BepInEx;
using SpaceCraft;
using HarmonyLib;
using System;
using System.Collections.Generic;
using BepInEx.Bootstrap;
using System.Reflection;
using BepInEx.Configuration;

namespace UIShowContainerInfo
{
    [BepInPlugin("akarnokd.theplanetcraftermods.uishowcontainerinfo", "(UI) Show Container Content Info", PluginInfo.PLUGIN_VERSION)]
    [BepInDependency(modInventoryStackingGuid, BepInDependency.DependencyFlags.SoftDependency)]
    public class Plugin : BaseUnityPlugin
    {
        const string modInventoryStackingGuid = "akarnokd.theplanetcraftermods.cheatinventorystacking";
        /// <summary>
        /// If the CheatInventoryStacking is installed, consider the stack counts when displaying information.
        /// </summary>
        static Func<IEnumerable<WorldObject>, int> getStackCount;

        static ConfigEntry<int> stackSizeConfig;
        static Func<int, bool> apiCanStack;

        static MethodInfo mActionableHandleHoverMaterial;
        static AccessTools.FieldRef<Actionnable, bool> fActionableHovering;

        private void Awake()
        {
            LibCommon.BepInExLoggerFix.ApplyFix();
            
            // Plugin startup logic
            Logger.LogInfo($"Plugin is loaded!");

            if (Chainloader.PluginInfos.TryGetValue(modInventoryStackingGuid, out BepInEx.PluginInfo pi))
            {
                Logger.LogInfo("Mod " + modInventoryStackingGuid + " found, considering stacking in various inventories");

                Type modType = pi.Instance.GetType();

                getStackCount = (Func<IEnumerable<WorldObject>, int>)AccessTools.Field(modType, "apiGetStackCount").GetValue(null);
                stackSizeConfig = (ConfigEntry<int>)AccessTools.Field(modType, "stackSize").GetValue(null);
                apiCanStack = (Func<int, bool>)AccessTools.Field(modType, "apiCanStack").GetValue(null);
            }
            else
            {
                Logger.LogInfo("Mod " + modInventoryStackingGuid + " not found");
            }

            mActionableHandleHoverMaterial = AccessTools.Method(typeof(Actionnable), "HandleHoverMaterial", [typeof(bool)]);
            fActionableHovering = AccessTools.FieldRefAccess<Actionnable, bool>("_hovering");

            LibCommon.HarmonyIntegrityCheck.Check(typeof(Plugin));
            Harmony.CreateAndPatchAll(typeof(Plugin));
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(ActionOpenable), nameof(ActionOpenable.OnHover))]
        static bool ActionOpenable_OnHover(
            ActionOpenable __instance, 
            BaseHudHandler ____hudHandler)
        {
            var inventoryAssoc = __instance.GetComponent<InventoryAssociated>();
            if (inventoryAssoc != null)
            {
                inventoryAssoc.GetInventory(inv => OnInventory(inv, __instance, ____hudHandler));
                return false;
            }

            var inventoryAssocProxy = __instance.GetComponent<InventoryAssociatedProxy>();
            if (inventoryAssocProxy != null)
            {
                inventoryAssocProxy.GetInventory((inv, _) => OnInventory(inv, __instance, ____hudHandler));
                return false;
            }

            inventoryAssoc = __instance.GetComponentInParent<InventoryAssociated>();
            if (inventoryAssoc != null)
            {
                inventoryAssoc.GetInventory(inv => OnInventory(inv, __instance, ____hudHandler));
                return false;
            }

            inventoryAssocProxy = __instance.GetComponentInParent<InventoryAssociatedProxy>();
            if (inventoryAssocProxy != null)
            {
                inventoryAssocProxy.GetInventory((inv, _) => OnInventory(inv, __instance, ____hudHandler));
                return false;
            }

            return true;
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(ActionGroupSelector), nameof(ActionOpenable.OnHover))]
        static bool ActionGroupSelector_OnHover(
            ActionOpenable __instance,
            BaseHudHandler ____hudHandler)
        {
            var inventoryAssoc = __instance.GetComponent<InventoryAssociated>();
            if (inventoryAssoc != null)
            {
                inventoryAssoc.GetInventory(inv => OnInventory(inv, __instance, ____hudHandler));
                return false;
            }

            var inventoryAssocProxy = __instance.GetComponent<InventoryAssociatedProxy>();
            if (inventoryAssocProxy != null)
            {
                inventoryAssocProxy.GetInventory((inv, _) => OnInventory(inv, __instance, ____hudHandler));
                return false;
            }

            inventoryAssoc = __instance.GetComponentInParent<InventoryAssociated>();
            if (inventoryAssoc != null)
            {
                inventoryAssoc.GetInventory(inv => OnInventory(inv, __instance, ____hudHandler));
                return false;
            }

            inventoryAssocProxy = __instance.GetComponentInParent<InventoryAssociatedProxy>();
            if (inventoryAssocProxy != null)
            {
                inventoryAssocProxy.GetInventory((inv, _) => OnInventory(inv, __instance, ____hudHandler));
                return false;
            }

            return true;
        }

        static void OnInventory(Inventory inventory, Actionnable __instance,
            BaseHudHandler ____hudHandler)
        {
            string custom = "";
            WorldObjectText woText = __instance.GetComponent<WorldObjectText>();
            if (woText != null && woText.GetTextIsSet())
            {
                custom = " \"" + woText.GetText() + "\" ";
            }

            var containerGroup = default(Group);

            var woa = __instance.GetComponentInParent<WorldObjectAssociated>()
                        ?? __instance.GetComponentInChildren<WorldObjectAssociated>();
            if (woa != null && woa.GetWorldObject() != null)
            {
                containerGroup = woa.GetWorldObject().GetGroup();
            }
            else if (__instance.TryGetComponent<ConstructibleProxy>(out var cp))
            {
                containerGroup = cp.GetGroup();
            }

            string text = containerGroup != null ? Readable.GetGroupName(containerGroup) : "";

            var inv = inventory.GetInsideWorldObjects();
            int count = inv.Count;
            int size = inventory.GetSize();

            if (getStackCount != null && apiCanStack(inventory.GetId()))
            {
                int stacks = getStackCount(inv);
                int slotSize = stackSizeConfig.Value;
                text += custom + "  [  " + stacks + "  /  " + size + "  --  (  " + count + "  /  " + (size * slotSize) + "  )]  ";
                if (count >= size * slotSize)
                {
                    text += "  --- FULL ---  ";
                }
            }
            else
            {
                text += custom + "  [  " + count + "  /  " + size + "  ]  ";
                if (count >= size)
                {
                    text += "  --- FULL ---  ";
                }
            }

            if (count > 0)
            {
                text += Readable.GetGroupName(inv[0].GetGroup());
            }


            GamepadConfig.Instance.SetGamepadHintButtonVisible("Open", visible: true);
            if (!GamepadConfig.Instance.GetIsUsingController())
            {
                ____hudHandler.DisplayCursorText("UI_Open", 0f, text);
            }


            // base.OnHover() => Actionable.OnHover()
            if (__instance.TryGetComponent<ActionnableInteractive>(out var ai))
            {
                ai.OnHoverInteractive();
            }
            // this.HandleHoverMaterial(true, null);

            mActionableHandleHoverMaterial.Invoke(__instance, [true]);

            // this._hovering = true;
            fActionableHovering.Invoke(__instance) = true;
        }
    }
}
