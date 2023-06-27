using BepInEx;
using SpaceCraft;
using HarmonyLib;
using TMPro;

namespace UIShowPlayerTooltipItemCount
{
    [BepInPlugin("akarnokd.theplanetcraftermods.uishowplayertooltipitemcount", "(UI) Show Player Tooltip Item Count", PluginInfo.PLUGIN_VERSION)]
    public class Plugin : BaseUnityPlugin
    {

        private void Awake()
        {
            // Plugin startup logic
            Logger.LogInfo($"Plugin is loaded!");

            Harmony.CreateAndPatchAll(typeof(Plugin));
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(GroupInfosDisplayer), nameof(GroupInfosDisplayer.Show))]
        static void GroupInfosDisplayer_Show(Group _group, TextMeshProUGUI ___nameText)
        {
            string gname = Readable.GetGroupName(_group);
            Inventory inventory = Managers.GetManager<PlayersManager>().GetActivePlayerController().GetPlayerBackpack().GetInventory();
            int cnt = 0;
            foreach (WorldObject wo in inventory.GetInsideWorldObjects())
            {
                if (wo.GetGroup().GetId() == _group.GetId())
                {
                    cnt++;
                }
            }
            if (cnt > 0)
            {
                gname += "    x " + cnt;
            }
            ___nameText.text = gname;
        }
    }
}
