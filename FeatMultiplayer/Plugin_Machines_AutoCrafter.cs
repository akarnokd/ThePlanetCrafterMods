using BepInEx;
using BepInEx.Bootstrap;
using HarmonyLib;
using MijuTools;
using SpaceCraft;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace FeatMultiplayer
{
    public partial class Plugin : BaseUnityPlugin
    {
        /// <summary>
        /// The vanilla starts a coroutine that periodically crafts something if
        /// the ingredients are there and there is space in the inventory.
        /// 
        /// On the host, we have to intercept the item creation and send it to the client.
        /// 
        /// On the client, we do nothing and let the host generate items.
        /// </summary>
        /// <param name="__instance">The underlying MachineAutoCrafter instance to get public values from.</param>
        /// <param name="timeRepeat">How often to craft?</param>
        /// <param name="__result">The overridden coroutine</param>
        /// <returns>Always false</returns>
        [HarmonyPrefix]
        [HarmonyPatch(typeof(MachineAutoCrafter), "TryToCraft")]
        static bool MachineAutoCrafter_TryToCraft_Patch(MachineAutoCrafter __instance, float timeRepeat, ref IEnumerator __result)
        {
            __result = MachineAutoCrafter_TryToCraft_Override(__instance, timeRepeat);
            return false;
        }

        static IEnumerator MachineAutoCrafter_TryToCraft_Override(MachineAutoCrafter __instance, float timeRepeat)
        {
            var hasEnergyField = AccessTools.Field(typeof(MachineAutoCrafter), "hasEnergy");
            var autoCrafterInventoryField = AccessTools.Field(typeof(MachineAutoCrafter), "autoCrafterInventory");
            for (; ; )
            {
                if (updateMode != MultiplayerMode.CoopClient)
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
                }
                yield return new WaitForSeconds(timeRepeat);
            }
        }

        static void MachineAutoCrafter_CraftIfPossible_Override(MachineAutoCrafter __instance, FieldInfo autoCrafterInventoryField, Group linkedGroup)
        {
            // step 1, locate all inventories within range

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
                    else
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
                if (TryCreateInInventoryAndNotify(linkedGroup, outputInventory, out _))
                {
                    WorldObjectsHandler.DestroyWorldObjects(toConsume, true);
                    __instance.CraftAnimation((GroupItem)linkedGroup);
                }
                else
                {
                    LogWarning("MachineAutoCrafter_CraftIfPossible_Override: " + linkedGroup.GetId() + ", success = false, reason = inventory full");
                }
            }
        }

        /// <summary>
        /// The vanilla method just sets the linked groups to this single group.
        /// 
        /// In multiplayer, we have to notify the other party about the change
        /// in the world object.
        /// </summary>
        /// <param name="___worldObject">The target world object.</param>
        /// <param name="_group">The new group selected or null if it was cleared.</param>
        [HarmonyPostfix]
        [HarmonyPatch(typeof(UiWindowGroupSelector), "OnGroupSelected")]
        static void UiWindowGroupSelector_OnGroupSelected(WorldObject ___worldObject, Group _group)
        {
            if (updateMode != MultiplayerMode.SinglePlayer)
            {
                List<string> groupIds = null;
                var groups = ___worldObject.GetLinkedGroups();
                if (groups != null && groups.Count != 0)
                {
                    groupIds = new();
                    foreach (var gr in groups)
                    {
                        groupIds.Add(gr.GetId());
                    }
                }
                Send(new MessageSetLinkedGroups { id = ___worldObject.GetId(), groupIds = groupIds });
                Signal();
            }
        }
    }
}
