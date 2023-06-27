using BepInEx;
using SpaceCraft;
using HarmonyLib;
using TMPro;
using UnityEngine;
using System;
using System.Collections.Generic;
using BepInEx.Bootstrap;
using System.Reflection;
using BepInEx.Configuration;

namespace UIShowPlayerInventoryCount
{
    [BepInPlugin("akarnokd.theplanetcraftermods.uishowplayerinventorycount", "(UI) Show Player Inventory Counts", PluginInfo.PLUGIN_VERSION)]
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

        [HarmonyPostfix]
        [HarmonyPatch(typeof(BaseHudHandler), nameof(BaseHudHandler.UpdateHud))]
        static void BaseHudHandler_UpdateHud(TextMeshProUGUI ___textPositionDecoration, GameObject ___subjectPositionDecoration, PlayerCanAct ___playerCanAct)
        {
            Inventory inventory = Managers.GetManager<PlayersManager>().GetActivePlayerController().GetPlayerBackpack().GetInventory();
            List<WorldObject> inv = inventory.GetInsideWorldObjects();
            int cnt = inv.Count;
            int max = inventory.GetSize();

            string prefix = "    <[  ";
            string postfix = "  )]>";
            string addition;

            if (getStackCount != null)
            {
                int stack = getStackCount(inv); // fine, player inventory always stacks
                addition = prefix + stack + " / " + max + "  (  " + cnt + "  /  " + (max * stackSize.Value) + postfix;
            } 
            else
            {
                addition = prefix + cnt + "  /  " + max + "  (  " + (cnt - max) + postfix;
            }


            string text = ___textPositionDecoration.text;

            // Try to preserve other changes to the ___textPositionDecoration.text
            int idx = text.IndexOf(prefix);
            if (idx < 0) {
                ___textPositionDecoration.text = text + addition;
            } else
            {
                int jdx = text.IndexOf(postfix);
                if (jdx < 0)
                {
                    ___textPositionDecoration.text = text.Substring(0, idx) + addition;
                }
                else
                {
                    ___textPositionDecoration.text = text.Substring(0, idx) + addition + text.Substring(jdx + postfix.Length);
                }
            }
        }
    }
}
