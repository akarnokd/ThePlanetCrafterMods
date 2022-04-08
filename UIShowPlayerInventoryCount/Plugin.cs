using BepInEx;
using MijuTools;
using SpaceCraft;
using HarmonyLib;
using TMPro;
using UnityEngine;

namespace UIShowPlayerInventoryCount
{
    [BepInPlugin("akarnokd.theplanetcraftermods.uishowplayerinventorycount", "(UI) Show Player Inventory Counts", "1.0.0.0")]
    public class Plugin : BaseUnityPlugin
    {

        private void Awake()
        {
            // Plugin startup logic
            Logger.LogInfo($"Plugin is loaded!");

            Harmony.CreateAndPatchAll(typeof(Plugin));
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(BaseHudHandler), nameof(BaseHudHandler.UpdateHud))]
        static void BaseHudHandler_UpdateHud(TextMeshProUGUI ___textPositionDecoration, GameObject ___subjectPositionDecoration, PlayerCanAct ___playerCanAct)
        {
            Inventory inventory = Managers.GetManager<PlayersManager>().GetActivePlayerController().GetPlayerBackpack().GetInventory();
            int cnt = inventory.GetInsideWorldObjects().Count;
            int max = inventory.GetSize();

            string prefix = "    <[  ";
            string postfix = "  )]>";
            string addition = prefix + cnt + "  /  " + max + "  (  " + (cnt - max) + postfix;
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
