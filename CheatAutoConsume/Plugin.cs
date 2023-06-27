using BepInEx;
using SpaceCraft;
using HarmonyLib;
using BepInEx.Configuration;

namespace CheatAutoConsume
{
    [BepInPlugin("akarnokd.theplanetcraftermods.cheatautoconsume", "(Cheat) Auto Consume Oxygen-Water-Food", PluginInfo.PLUGIN_VERSION)]
    public class Plugin : BaseUnityPlugin
    {

        private static ConfigEntry<int> threshold;

        static bool oxygenWarning;
        static bool waterWarning;
        static bool foodWarning;

        private void Awake()
        {
            // Plugin startup logic
            Logger.LogInfo($"Plugin is loaded!");

            threshold = Config.Bind("General", "Threshold", 9, "The percentage for which below food/water/oxygen is consumed.");

            Harmony.CreateAndPatchAll(typeof(Plugin));
        }

        static bool FindAndConsume(DataConfig.UsableType type)
        {
            PlayerMainController activePlayerController = Managers.GetManager<PlayersManager>().GetActivePlayerController();
            Inventory inv = Managers.GetManager<PlayersManager>().GetActivePlayerController().GetPlayerBackpack().GetInventory();
            PlayerGaugesHandler gh = activePlayerController.GetGaugesHandler();
            foreach (WorldObject _worldObject in inv.GetInsideWorldObjects())
            {
                if (_worldObject.GetGroup() is GroupItem)
                {
                    GroupItem groupItem = (GroupItem)_worldObject.GetGroup();
                    int groupValue = groupItem.GetGroupValue();
                    if (groupItem.GetUsableType() == type)
                    {
                        if ((type == DataConfig.UsableType.Eatable && gh.Eat(groupValue))
                                || (type == DataConfig.UsableType.Breathable && gh.Breath(groupValue))
                                || (type == DataConfig.UsableType.Drinkable && gh.Drink(groupValue))
                                ) {

                            if (groupItem.GetEffectOnPlayer() != null)
                            {
                                activePlayerController.GetPlayerEffects().ActivateEffect(groupItem.GetEffectOnPlayer());
                            }

                            inv.RemoveItem(_worldObject, true);
                            return true;
                        }
                    }
                }
            }
            return false;
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(PlayerGaugeOxygen), "GaugeVerifications")]
        static void PlayerGaugeOxygen_GaugeVerifications(float ___gaugeValue, bool ___isInited)
        {
            if (!___isInited)
            {
                return;
            }
            if (___gaugeValue >= threshold.Value)
            {
                oxygenWarning = false;
            }
            if (___gaugeValue < threshold.Value && !oxygenWarning)
            {
                if (!FindAndConsume(DataConfig.UsableType.Breathable))
                {
                    Managers.GetManager<BaseHudHandler>().DisplayCursorText("", 3f, "No Oxygen In Inventory!");
                    oxygenWarning = true;
                }
            }
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(PlayerGaugeThirst), "GaugeVerifications")]
        static void PlayerGaugeThirst_GaugeVerifications_Pre(float ___gaugeValue, bool ___isInited)
        {
            if (!___isInited)
            {
                return;
            }
            if (___gaugeValue >= threshold.Value)
            {
                waterWarning = false;
            }
            if (___gaugeValue < threshold.Value && !waterWarning)
            {
                if (!FindAndConsume(DataConfig.UsableType.Drinkable))
                {
                    Managers.GetManager<BaseHudHandler>().DisplayCursorText("", 3f, "No Water In Inventory!");
                    waterWarning = true;
                }
            }
        }


        [HarmonyPrefix]
        [HarmonyPatch(typeof(PlayerGaugeHealth), "GaugeVerifications")]
        static void PlayerGaugeHealth_GaugeVerifications_Pre(float ___gaugeValue, bool ___isInited)
        {
            if (!___isInited)
            {
                return;
            }
            if (___gaugeValue >= threshold.Value)
            {
                foodWarning = false;
            }
            if (___gaugeValue < threshold.Value && !foodWarning)
            {
                if (!FindAndConsume(DataConfig.UsableType.Eatable))
                {
                    Managers.GetManager<BaseHudHandler>().DisplayCursorText("", 3f, "No Food In Inventory!");
                    foodWarning = true;
                }
            }
        }
    }
}
