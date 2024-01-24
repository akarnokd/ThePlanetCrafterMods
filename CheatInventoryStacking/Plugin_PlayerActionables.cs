using HarmonyLib;
using SpaceCraft;
using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace CheatInventoryStacking
{
    public partial class Plugin
    {

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
    }
}
