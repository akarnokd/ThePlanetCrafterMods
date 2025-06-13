// Copyright (c) 2022-2025, David Karnok & Contributors
// Licensed under the Apache License, Version 2.0

using HarmonyLib;
using SpaceCraft;
using System;
using Unity.Netcode;

namespace CheatInventoryStacking
{
    public partial class Plugin
    {
        /// <summary>
        /// Disallow stacking in DNA/Incubator.
        /// </summary>
        /// <param name="_inventory"></param>
        [HarmonyPostfix]
        [HarmonyPatch(typeof(MachineConvertRecipe), nameof(MachineConvertRecipe.SetConverterInventory))]
        static void Patch_MachineConvertRecipe_SetConverterInventory(Inventory inventory)
        {
            noStackingInventories.Add(inventory.GetId());
        }

        /// <summary>
        /// Disallow stacking in butterfly farms.
        /// </summary>
        /// <param name="_inventory"></param>
        [HarmonyPostfix]
        [HarmonyPatch(typeof(MachineFlockSpawner), "Start")]
        static void Patch_MachineFlockSpawner_Start(
            MachineFlockSpawner __instance)
        {
            if (__instance.GetComponent<MachineGenerator>() == null)
            {
                if (__instance.TryGetComponent<InventoryAssociatedProxy>(out var iap))
                {
                    iap.GetInventory((inv, _) => noStackingInventories.Add(inv.GetId()));
                }
            }
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(InventoryShowContent), "RegisterToInventory")]
        static void Patch_InventoryShowContent_RegisterToInventory(
            InventoryShowContent __instance, 
            Inventory inventory)
        {
            if (__instance.GetComponent<MachineOptimizer>() != null)
            {
                if (!stackOptimizer.Value)
                {
                    noStackingInventories.Add(inventory.GetId());
                }
            }
            else
            {
                noStackingInventories.Add(inventory.GetId());
            }
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(InventoryLockContent), "FirstInventoryCheck")]
        static void Patch_InventoryLockContent_FirstInventoryCheck(Inventory ____inventory)
        {
            noStackingInventories.Add(____inventory.GetId());
        }

        static bool overrideBufferSizeInRpc;

        [HarmonyPrefix]
        [HarmonyPatch(typeof(InventoriesHandler), "RetrieveInventoryClientRpc")]
        static void Patch_InventoriesHandler_RetrieveInventoryClientRpc_Pre()
        {
            overrideBufferSizeInRpc = stackSize.Value > 1;
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(InventoriesHandler), "RetrieveInventoryClientRpc")]
        static void Patch_InventoriesHandler_RetrieveInventoryClientRpc_Post()
        {
            overrideBufferSizeInRpc = false;
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(InventoriesHandler), "DirtyInventoryClientRpc")]
        static void Patch_InventoriesHandler_DirtyInventoryClientRpc_Pre()
        {
            overrideBufferSizeInRpc = stackSize.Value > 1;
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(InventoriesHandler), "DirtyInventoryClientRpc")]
        static void Patch_InventoriesHandler_DirtyInventoryClientRpc_Post()
        {
            overrideBufferSizeInRpc = false;
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(NetworkBehaviour), "__beginSendClientRpc")]
        static bool Patch_NetworkBehaviour___beginSendClientRpc(ref FastBufferWriter __result)
        {
            if (overrideBufferSizeInRpc)
            {
                var n = networkBufferScaling.Value * 1L * stackSize.Value + 65536;
                if (n > 128L * 1024 * 1024 || n < 65536L)
                {
                    n = -1L;
                }
                __result = new FastBufferWriter(1024, Unity.Collections.Allocator.Temp, (int)n);
                return false;
            }
            return true;
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(InventorySpawnContent), "RegisterToInventory")]
        static void Patch_InventorySpawnContent_RegisterToInventory(
            Inventory inventory)
        {
            noStackingInventories.Add(inventory.GetId());
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(ActionOpenable), "Start")]
        static void Patch_ActionOpenable_Start(ActionOpenable __instance)
        {
            if ((__instance.gameObject.name.StartsWith("AnimalFeeder", System.StringComparison.InvariantCulture)
                && !stackAnimalFeeder.Value)
                || 
                (__instance.transform.parent != null
                && __instance.transform.parent.gameObject.name.Contains("Vehicle", System.StringComparison.InvariantCulture)
                && !stackVehicle.Value))
            {
                var ia = __instance.GetComponentInParent<InventoryAssociated>();
                var iap = __instance.GetComponentInParent<InventoryAssociatedProxy>();

                if (ia != null && (iap == null || NetworkManager.Singleton.IsServer))
                {
                    ia.GetInventory(inv => noStackingInventories.Add(inv.GetId()));
                }
                else if (iap != null)
                {
                    iap.GetInventory((inv, wo) => noStackingInventories.Add(inv.GetId()));
                }
            }
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(UiWindowContainer), nameof(UiWindowContainer.SetInventories))]
        static void Patch_UiWindowContainer_OnOpen(UiWindowContainer __instance, Inventory inventoryRight)
        {
            if (__instance is UiWindowDNAExtractor)
            {
                noStackingInventories.Add(inventoryRight.GetId());
            }
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(InventoryChangeMaterial), "CheckInventory")]
        static void Patch_InventoryChangeMaterial_CheckInventory(Inventory inventory)
        {
            noStackingInventories.Add(inventory.GetId());
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(InventoryAssociated), nameof(InventoryAssociated.SetInventory))]
        static void Patch_InventoryAssociated_SetInventory(
            InventoryAssociated __instance,
            Inventory inventory
        )
        {
            if (__instance == null 
                || __instance.gameObject ==  null 
                || inventory == null
                || NetworkManager.Singleton == null
                || !NetworkManager.Singleton.IsServer)
            {
                return;
            }
            if (!stackPlanetaryDepots.Value 
                && __instance.gameObject.name.StartsWith("PlanetaryDeliveryDepot", StringComparison.Ordinal))
            {
                noStackingInventories.Add(inventory.GetId());
            }
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(SessionController), "Start")]
        static void Patch_SessionController_Start()
        {
            foreach (var gr in GroupsHandler.GetAllGroups())
            {
                gr.id = string.Intern(gr.id);
            }
        }
    }
}
