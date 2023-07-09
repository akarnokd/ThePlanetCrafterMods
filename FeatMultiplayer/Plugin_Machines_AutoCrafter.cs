using BepInEx;
using BepInEx.Bootstrap;
using HarmonyLib;
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
using FeatMultiplayer.MessageTypes;
using System.Diagnostics;

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

        static List<WorldObject> autocrafterCandidateWorldObjects = new();
        
        static void MachineAutoCrafter_CraftIfPossible_Override(MachineAutoCrafter __instance, FieldInfo autoCrafterInventoryField, 
            Group linkedGroup)
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
                if (TryCreateInInventoryAndNotify(linkedGroup, outputInventory, null, out _))
                {
                    WorldObjectsHandler.DestroyWorldObjects(toConsume, true);
                    // LogAlways(string.Format("    Ingredient destroy: {0:0.000} ms", sw.ElapsedTicks / 10000d));
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
        [HarmonyPostfix]
        [HarmonyPatch(typeof(UiWindowGroupSelector), "OnGroupSelected")]
        static void UiWindowGroupSelector_OnGroupSelected(WorldObject ___worldObject)
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
                var msg = new MessageSetLinkedGroups { id = ___worldObject.GetId(), groupIds = groupIds };
                if (updateMode == MultiplayerMode.CoopHost)
                {
                    SendAllClients(msg, true);
                }
                else
                {
                    SendHost(msg, true);
                }
            }
        }
    }
}
