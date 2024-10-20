// Copyright (c) 2022-2024, David Karnok & Contributors
// Licensed under the Apache License, Version 2.0

using HarmonyLib;
using SpaceCraft;
using System;
using Unity.Netcode;

namespace CheatInventoryStacking
{
    public partial class Plugin
    {
        [HarmonyPrefix]
        [HarmonyPatch(typeof(InventoryAssociatedProxy), nameof(InventoryAssociated.GetInventory))]
        static void Patch_InventoryAssociatedProxy_GetInventory(
            InventoryAssociatedProxy __instance,
            ref Action<Inventory, WorldObject> callback
        )
        {
            if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer)
            {
                return;
            }
            
            var old = callback;

            callback = (inventory, wo) =>
            {
                try
                {
                    if (__instance.TryGetComponent<MachineGrower>(out _))
                    {
                        Patch_MachineGrower_SetGrowerInventory(inventory);
                    }
                    if (__instance.TryGetComponent<MachineOutsideGrower>(out _))
                    {
                        Patch_MachineOutsideGrower_SetGrowerInventory(inventory);
                    }
                    if (__instance.TryGetComponent<MachineGenerator>(out var mgn))
                    {
                        Patch_MachineGenerator_SetGeneratorInventory(mgn, inventory);
                    }
                    if (__instance.TryGetComponent<MachineConvertRecipe>(out _))
                    {
                        Patch_MachineConvertRecipe_SetConverterInventory(inventory);
                    }
                    if (__instance.TryGetComponent<MachineDroneStation>(out _))
                    {
                        Patch_MachineDroneStation_SetDroneStationInventory(inventory);
                    }
                    if (__instance.TryGetComponent<MachineAutoCrafter>(out _))
                    {
                        Patch_MachineAutoCrafter_SetAutoCrafterInventory(inventory);
                    }
                    if (__instance.TryGetComponent<MachineTradePlatform>(out _))
                    {
                        Patch_MachineTradePlatform_SetInventoryTradePlatform(inventory);
                    }
                    if (__instance.TryGetComponent<MachineDestructInventoryIfFull>(out _))
                    {
                        Patch_MachineDestructInventoryIfFull_SetDestructInventoryInventory(inventory);
                    }
                    if (__instance.TryGetComponent<MachineOptimizer>(out _))
                    {
                        Patch_MachineOptimizer_SetOptimizerInventory(inventory);
                    }
                    if (__instance.TryGetComponent<MachineDisintegrator>(out _))
                    {
                        Patch_MachineDisintegrator_SetDisintegratorInventory(inventory);
                    }
                }
                finally
                {
                    old?.Invoke(inventory, wo);
                }
            };
        }
    }
}
