// Copyright (c) 2022-2024, David Karnok & Contributors
// Licensed under the Apache License, Version 2.0

using BepInEx;
using SpaceCraft;
using HarmonyLib;
using TMPro;
using System;
using System.Collections.Generic;
using BepInEx.Bootstrap;
using BepInEx.Configuration;
using UnityEngine;

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
        static Func<IEnumerable<WorldObject>, int> getStackCount;
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

                Type modType = pi.Instance.GetType();

                getStackCount = (Func<IEnumerable<WorldObject>, int>)AccessTools.Field(modType, "apiGetStackCount").GetValue(null);
                stackSize = (ConfigEntry<int>)AccessTools.Field(pi.Instance.GetType(), "stackSize").GetValue(null);
                stackBackpack = (ConfigEntry<bool>)AccessTools.Field(pi.Instance.GetType(), "stackBackpack").GetValue(null);
            }
            else
            {
                Logger.LogInfo("Mod " + modInventoryStackingGuid + " not found");
            }

            LibCommon.HarmonyIntegrityCheck.Check(typeof(Plugin));
            Harmony.CreateAndPatchAll(typeof(Plugin));
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(BaseHudHandler), nameof(BaseHudHandler.UpdateHud))]
        static void BaseHudHandler_UpdateHud(
            TextMeshProUGUI ___textPositionDecoration,
            GameObject ___subjectPositionDecoration)
        {
            // In multiplayer, we may not have inventory yet
            if (___subjectPositionDecoration == null)
            {
                return;
            }

            Inventory inventory = Managers.GetManager<PlayersManager>().GetActivePlayerController()?.GetPlayerBackpack()?.GetInventory();
            if (inventory == null)
            {
                return;
            }
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
                    ___textPositionDecoration.text = text[..idx] + addition;
                }
                else
                {
                    ___textPositionDecoration.text = text[..idx] + addition + text[(jdx + postfix.Length)..];
                }
            }
        }
    }
}
