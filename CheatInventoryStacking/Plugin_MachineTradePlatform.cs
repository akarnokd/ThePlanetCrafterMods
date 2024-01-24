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
            WorldObject ____worldObject,
            Inventory ____inventory)
        {
            if (stackTradeRockets.Value && stackSize.Value > 1)
            {
                if (____worldObject != null
                    && ____worldObject.GetSetting() == 1
                    && ____inventory.GetSize() * stackSize.Value <= ____inventory.GetInsideWorldObjects().Count)
                {
                    __instance.SendTradeRocket();
                }
                return false;
            }
            return true;
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(UiWindowTrade), "OnClickButtons")]
        static bool UiWindowTrade_OnClickButtons(
            UiWindowTrade __instance,
            UiGroupLine uiGroupLine, 
            Group group, 
            int changeOfValue)
        {
            if (stackSize.Value > 1 && stackTradeRockets.Value)
            {
                // TODO implement
            }
            return true;
        }

    }
}
