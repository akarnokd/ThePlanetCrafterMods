using BepInEx;
using SpaceCraft;
using HarmonyLib;
using UnityEngine;
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
        static Func<List<WorldObject>, int> getStackCount;

        static ConfigEntry<int> stackSizeConfig;
        static HashSet<int> noStackingInventories;

        private void Awake()
        {
            // Plugin startup logic
            Logger.LogInfo($"Plugin is loaded!");

            if (Chainloader.PluginInfos.TryGetValue(modInventoryStackingGuid, out BepInEx.PluginInfo pi))
            {
                MethodInfo mi = AccessTools.Method(pi.Instance.GetType(), "GetStackCount", new Type[] { typeof(List<WorldObject>) });
                getStackCount = AccessTools.MethodDelegate<Func<List<WorldObject>, int>>(mi, null);

                stackSizeConfig = (ConfigEntry<int>)AccessTools.Field(pi.Instance.GetType(), "stackSize").GetValue(null);
                noStackingInventories = (HashSet<int>)AccessTools.Field(pi.Instance.GetType(), "noStackingInventories").GetValue(null);
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
            string text = Readable.GetGroupName(__instance.GetComponentInParent<WorldObjectAssociated>().GetWorldObject().GetGroup());
            InventoryAssociated componentOnGameObjectOrInParent = __instance.GetComponentInParent<InventoryAssociated>();
            if (componentOnGameObjectOrInParent != null)
            {
                Inventory inventory = componentOnGameObjectOrInParent.GetInventory();
                List<WorldObject> inv = inventory.GetInsideWorldObjects();
                int count = inv.Count;
                int size = inventory.GetSize();

                if (getStackCount != null && !noStackingInventories.Contains(inventory.GetId()))
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
                    text += Readable.GetGroupName(inventory.GetInsideWorldObjects()[0].GetGroup());
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
