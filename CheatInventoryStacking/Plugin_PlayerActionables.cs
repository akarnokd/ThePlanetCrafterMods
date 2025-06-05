﻿// Copyright (c) 2022-2025, David Karnok & Contributors
// Licensed under the Apache License, Version 2.0

using HarmonyLib;
using SpaceCraft;
using UnityEngine;

namespace CheatInventoryStacking
{
    public partial class Plugin
    {

        [HarmonyPrefix]
        [HarmonyPatch(typeof(ActionMinable), nameof(ActionMinable.OnAction))]
        static bool Patch_ActionMinable_OnAction(ActionMinable __instance)
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
        static bool Patch_ActionGrabable_OnAction(ActionGrabable __instance)
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
        [HarmonyPatch(typeof(WorldObjectsHandler), "RetrieveResourcesFromDeconstructionServerRpc")]
        static void Patch_WorldObjectsHandler_RetrieveResourcesFromDeconstructionServerRpc_Pre()
        {
            isLastSlotOccupiedMode = true;
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(WorldObjectsHandler), "RetrieveResourcesFromDeconstructionServerRpc")]
        static void Patch_WorldObjectsHandler_RetrieveResourcesFromDeconstructionServerRpc_Post()
        {
            isLastSlotOccupiedMode = false;
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(ActionPanelDeconstruct), "Deconstruct")]
        static bool Patch_ActionPanelDeconstruct_Deconstruct(
            ActionPanelDeconstruct __instance,
            ref bool ___isDestroying,
            PlayerMultitool ___playerMultitool,
            ref Inventory ___playerInventory,
            Panel ___panel
        )
        {
            if (stackSize.Value <= 1 || !stackBackpack.Value)
            {
                return true;
            }

            if (___playerMultitool.GetState() != DataConfig.MultiToolState.Deconstruct 
                || ___isDestroying)
            {
                return false;
            }

            ___isDestroying = true;


            var apc = Managers.GetManager<PlayersManager>().GetActivePlayerController();

            apc.GetPlayerAudio().PlayDeconstruct();
            apc.GetPlayerShareState().StartDeconstructing();
            apc.GetAnimations().AnimateRecolt(true, 1f);

            if (___playerInventory == null)
            {
                __instance.Start();
            }

            var panelGroupConstructible = ___panel.GetPanelGroupConstructible();
            if (panelGroupConstructible == null)
            {
                return false;
            }

            var informationsDisplayer = Managers.GetManager<DisplayersHandler>().GetInformationsDisplayer();
            var informationTime = 2.5f;

            foreach (var gr in panelGroupConstructible.GetRecipe().GetIngredientsGroupInRecipe())
            {
                if (IsFullStackedOfInventory(___playerInventory, gr.id))
                {
                    WorldObjectsHandler.Instance.CreateAndDropOnFloor(gr, ___playerMultitool.gameObject.transform.position + new Vector3(0f, 1f, 0f));
                    informationsDisplayer.AddInformation(informationTime, Readable.GetGroupName(gr), DataConfig.UiInformationsType.DropOnFloor, gr.GetImage());
                }
                else
                {
                    InventoriesHandler.Instance.AddItemToInventory(gr, ___playerInventory, (success, id) => 
                    {
                        informationsDisplayer.AddInformation(informationTime, Readable.GetGroupName(gr), DataConfig.UiInformationsType.InInventory, gr.GetImage());
                    });
                }
            }

            if (___panel.GetPanelType() == DataConfig.BuildPanelType.Floor)
            {
                if (___panel.GetIsCeiling())
                {
                    ___panel.ChangePanel(DataConfig.BuildPanelSubType.FloorLight, refreshIds: true, disolve: true);
                }
                else
                {
                    ___panel.ChangePanel(DataConfig.BuildPanelSubType.FloorPlain, refreshIds: true, disolve: true);
                }
            }
            else if (___panel.GetPanelType() == DataConfig.BuildPanelType.Wall)
            {
                if (___panel.GetContingousPanels(2f) != null)
                {
                    ___panel.ChangePanel(DataConfig.BuildPanelSubType.WallCorridor, refreshIds: true, disolve: true);
                }
                else
                {
                    ___panel.ChangePanel(DataConfig.BuildPanelSubType.WallPlain, refreshIds: true, disolve: true);
                }
            }

            return false;
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(PetProxy), "ActionPetServerRpc")]
        static void Patch_PetProxy_ActionPetServerRpc_Pre()
        {
            isLastSlotOccupiedMode = true;
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(PetProxy), "ActionPetServerRpc")]
        static void Patch_PetProxy_ActionPetServerRpc_Post()
        {
            isLastSlotOccupiedMode = false;
        }
    }
}
