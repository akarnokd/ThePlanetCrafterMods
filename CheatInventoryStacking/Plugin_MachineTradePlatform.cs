using HarmonyLib;
using SpaceCraft;
using System;
using System.Collections.Generic;
using System.Text;

namespace CheatInventoryStacking
{
    public partial class Plugin
    {
        /// <summary>
        /// Conditionally disallow stacking in trade rockets.
        /// </summary>
        /// <param name="_inventory"></param>
        [HarmonyPostfix]
        [HarmonyPatch(typeof(MachineTradePlatform), nameof(MachineTradePlatform.SetInventoryTradePlatform))]
        static void MachineTradePlatform_SetInventoryTradePlatform(Inventory _inventory)
        {
            if (!stackTradeRockets.Value)
            {
                _noStackingInventories.Add(_inventory.GetId());
            }
        }


        [HarmonyPrefix]
        [HarmonyPatch(typeof(MachineTradePlatform), "OnTradeInventoryModified")]
        static bool MachineTradePlatform_OnTradeInventoryModified(
            MachineTradePlatform __instance,
            WorldObject ___worldObject,
            Inventory ___inventory)
        {
            // In multiplayer mode, don't do the stuff below
            if (getMultiplayerMode != null && getMultiplayerMode() == "CoopClient")
            {
                return false;
            }
            if (stackTradeRockets.Value && stackSize.Value > 1)
            {
                if (___worldObject != null
                    && ___worldObject.GetSetting() == 1
                    && ___inventory.GetSize() * stackSize.Value <= ___inventory.GetInsideWorldObjects().Count)
                {
                    __instance.SendTradeRocket();
                }
                return false;
            }
            return true;
        }

    }
}
