using BepInEx;
using MijuTools;
using SpaceCraft;
using HarmonyLib;
using TMPro;
using UnityEngine;
using System;
using System.Collections.Generic;
using BepInEx.Bootstrap;
using System.Reflection;
using BepInEx.Configuration;

namespace UIShowContainerInfo
{
    [BepInPlugin("akarnokd.theplanetcraftermods.uishowcontainerinfo", "(UI) Show Container Content Info", "1.0.0.1")]
    [BepInDependency(modInventoryStackingGuid, BepInDependency.DependencyFlags.SoftDependency)]
    public class Plugin : BaseUnityPlugin
    {
        const string modInventoryStackingGuid = "akarnokd.theplanetcraftermods.cheatinventorystacking";
        /// <summary>
        /// If the CheatInventoryStacking is installed, consider the stack counts when displaying information.
        /// </summary>
        static Func<List<WorldObject>, int> getStackCount;

        static ConfigEntry<int> stackSize;

        private void Awake()
        {
            // Plugin startup logic
            Logger.LogInfo($"Plugin is loaded!");

            if (Chainloader.PluginInfos.TryGetValue(modInventoryStackingGuid, out BepInEx.PluginInfo pi))
            {
                MethodInfo mi = AccessTools.Method(pi.Instance.GetType(), "GetStackCount", new Type[] { typeof(List<WorldObject>) });
                getStackCount = AccessTools.MethodDelegate<Func<List<WorldObject>, int>>(mi, null);

                stackSize = (ConfigEntry<int>)AccessTools.Field(pi.Instance.GetType(), "stackSize").GetValue(null);
            }

            Harmony.CreateAndPatchAll(typeof(Plugin));
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(ActionOpenable), nameof(ActionOpenable.OnHover))]
        static bool ActionOpenable_OnHover(ActionOpenable __instance, BaseHudHandler ___hudHandler)
        {
            string custom = "";
            WorldObjectText woText = __instance.GetComponent<WorldObjectText>();
            if (woText != null && woText.GetTextIsSet())
            {
                custom = " \"" + woText.GetText() + "\" ";
            }
            string text = Readable.GetGroupName(Components.GetComponentOnGameObjectOrInParent<WorldObjectAssociated>(__instance.gameObject).GetWorldObject().GetGroup());
            InventoryAssociated componentOnGameObjectOrInParent = Components.GetComponentOnGameObjectOrInParent<InventoryAssociated>(__instance.gameObject);
            if (componentOnGameObjectOrInParent != null)
            {
                Inventory inventory = componentOnGameObjectOrInParent.GetInventory();
                List<WorldObject> inv = inventory.GetInsideWorldObjects();
                int count = inv.Count;
                int size = inventory.GetSize();

                if (getStackCount != null)
                {
                    int stacks = getStackCount(inv);
                    int slotSize = stackSize.Value;
                    text += custom + "  [  " + stacks + "  /  " + size + "  (  " + count + "  /  " + (size * slotSize) + "  )]  ";
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
                    text += inventory.GetInsideWorldObjects()[0].GetGroup().GetId();
                }
            }
            ___hudHandler.DisplayCursorText("UI_Open", 0f, text);

            // base.OnHover() => Actionable.OnHover()
            ActionnableInteractive ai = __instance.GetComponent<ActionnableInteractive>();
            if (ai != null)
            {
                ai.OnHoverInteractive();
            }
            // this.HandleHoverMaterial(true, null);
            System.Reflection.MethodInfo mi = AccessTools.Method(typeof(Actionnable), "HandleHoverMaterial", new System.Type[] { typeof(bool), typeof(GameObject) });
            mi.Invoke(__instance, new object[] { true, null });

            return false;
        }
    }
}
