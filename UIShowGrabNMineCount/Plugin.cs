using BepInEx;
using SpaceCraft;
using HarmonyLib;
using BepInEx.Configuration;

namespace UIShowGrabNMineCount
{
    [BepInPlugin("akarnokd.theplanetcraftermods.uishowgrabnminecount", "(UI) Show Grab N Mine Count", PluginInfo.PLUGIN_VERSION)]
    public class Plugin : BaseUnityPlugin
    {
        private static ConfigEntry<bool> isEnabled;

        void Awake()
        {
            LibCommon.BepInExLoggerFix.ApplyFix();

            // Plugin startup logic
            Logger.LogInfo($"Plugin is loaded!");

            isEnabled = Config.Bind("General", "Enabled", true, "Is the visual notification enabled?");

            Harmony.CreateAndPatchAll(typeof(Plugin));
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(ActionMinable), "FinishMining")]
        static bool ActionMinable_FinishMining(
            ActionMinable __instance, 
            PlayerMainController ___playerSource,
            ref bool ___firstReduceDelay, 
            PlayerAnimations ___playerAnimations,
            ItemWorldDislpayer ___itemWorldDisplayer, 
            float ___timeMineStarted, 
            float ___timeMineStoped,
            ref bool ____mining) 
        {
            if (isEnabled.Value)
            {
                __instance.StopAllCoroutines();
                if (___timeMineStarted - ___timeMineStoped > ___playerSource.GetMultitool().GetMultiToolMine().GetMineTime())
                {
                    ___firstReduceDelay = false;
                    if (____mining)
                    {
                        ___playerSource.GetMultitool().GetMultiToolMine().StopMining(miningComplete: true);
                        ___playerSource.GetPlayerShareState().StopMining();
                        ___itemWorldDisplayer.Hide();
                        ___playerAnimations.AnimateRecolt(isPlaying: false);
                    }

                    ____mining = false;

                    // This also should fix two potential NPEs with quickly disappearing ores
                    var wo = __instance.GetComponent<WorldObjectAssociated>()?.GetWorldObject();
                    if (wo != null)
                    {
                        var inv = ___playerSource.GetPlayerBackpack().GetInventory();
                        InventoriesHandler.Instance.AddWorldObjectToInventory(
                            wo,
                            inv,
                            success =>
                            {
                                if (success)
                                {
                                    ShowInventoryAdded(wo, inv);
                                }
                            }
                        );

                    }
                    else
                    {
                        Destroy(__instance.gameObject);
                    }
                }

                return false;
            }
            return true;
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(ActionGrabable), "AddToInventory")]
        static bool ActionGrabable_AddToInventory(
            ActionGrabable __instance,
            WorldObject worldObject, 
            PlayerMainController ___playerSource)
        {
            if (isEnabled.Value)
            {
                var inv = ___playerSource.GetPlayerBackpack().GetInventory();

                InventoriesHandler.Instance.AddWorldObjectToInventory(worldObject, inv, success =>
                {
                    if (success)
                    {
                        ___playerSource.GetPlayerAudio().PlayGrab();
                        Managers.GetManager<DisplayersHandler>()?.GetItemWorldDisplayer()?.Hide();
                        try
                        {
                            __instance.grabedEvent?.Invoke(worldObject);
                            __instance.grabedEvent = null;
                        }
                        finally
                        {
                            ShowInventoryAdded(worldObject, inv);
                        }
                    }
                });


                return false;
            }
            return true;
        }

        static void ShowInventoryAdded(WorldObject worldObject, Inventory inventory)
        {
            int c = 0;
            Group group = worldObject.GetGroup();
            string gid = group.GetId();
            foreach (WorldObject wo in inventory.GetInsideWorldObjects())
            {
                if (wo.GetGroup().GetId() == gid)
                {
                    c++;
                }
            }

            string text = Readable.GetGroupName(group) + " + 1  (  " + c + "  )";
            InformationsDisplayer informationsDisplayer = Managers.GetManager<DisplayersHandler>().GetInformationsDisplayer();
            informationsDisplayer.AddInformation(2f, text, DataConfig.UiInformationsType.InInventory, group.GetImage());
        }

    }
}
