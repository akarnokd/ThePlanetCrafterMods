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
using System.Collections.ObjectModel;

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
        static Func<ReadOnlyCollection<WorldObject>, int> getStackCount;
        static ConfigEntry<int> stackSize;
        static ConfigEntry<bool> stackBackpack;

        private void Awake()
        {
            LibCommon.BepInExLoggerFix.ApplyFix();

            // Plugin startup logic
            Logger.LogInfo($"Plugin is loaded!");

            if (Chainloader.PluginInfos.TryGetValue(modInventoryStackingGuid, out BepInEx.PluginInfo pi))
            {
                Logger.LogInfo("Mod " + modInventoryStackingGuid + " found, considering stacking in backpack");

                MethodInfo mi = AccessTools.Method(pi.Instance.GetType(), "GetStackCount", [typeof(ReadOnlyCollection<WorldObject>)]);
                getStackCount = AccessTools.MethodDelegate<Func<ReadOnlyCollection<WorldObject>, int>>(mi, null);
                stackSize = (ConfigEntry<int>)AccessTools.Field(pi.Instance.GetType(), "stackSize").GetValue(null);
                stackBackpack = (ConfigEntry<bool>)AccessTools.Field(pi.Instance.GetType(), "stackBackpack").GetValue(null);
            }
            else
            {
                Logger.LogInfo("Mod " + modInventoryStackingGuid + " not found");
            }

            Harmony.CreateAndPatchAll(typeof(Plugin));
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(BaseHudHandler), nameof(BaseHudHandler.UpdateHud))]
        static void BaseHudHandler_UpdateHud(
            TextMeshProUGUI ___textPositionDecoration)
        {
            Inventory inventory = Managers.GetManager<PlayersManager>().GetActivePlayerController().GetPlayerBackpack().GetInventory();
            var inv = inventory.GetInsideWorldObjects();
            int cnt = inv.Count;
            int max = inventory.GetSize();

            string prefix = "    <[  ";
            string postfix = "  )]>";
            string addition;

            if (getStackCount != null && stackBackpack != null && stackBackpack.Value)
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
