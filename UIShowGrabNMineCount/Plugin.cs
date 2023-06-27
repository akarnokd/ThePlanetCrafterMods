using BepInEx;
using SpaceCraft;
using HarmonyLib;
using BepInEx.Configuration;

namespace UIShowGrabNMineCount
{
    [BepInPlugin("akarnokd.theplanetcraftermods.uishowgrabnminecount", "(UI) Show Grab N Mine Count", PluginInfo.PLUGIN_VERSION)]
    [BepInDependency("akarnokd.theplanetcraftermods.featmultiplayer", BepInDependency.DependencyFlags.SoftDependency)]
    public class Plugin : BaseUnityPlugin
    {
        private static ConfigEntry<bool> isEnabled;

        private void Awake()
        {
            // Plugin startup logic
            Logger.LogInfo($"Plugin is loaded!");

            isEnabled = Config.Bind("General", "Enabled", true, "Is the visual notification enabled?");

            Harmony.CreateAndPatchAll(typeof(Plugin));
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(ActionMinable), "FinishMining")]
        static bool ActionMinable_FinishMining(ActionMinable __instance, 
            PlayerMainController ___playerSource,
            ref bool ___firstReduceDelay, PlayerAnimations ___playerAnimations,
            ItemWorldDislpayer ___itemWorldDisplayer, float ___timeMineStarted, float ___timeMineStoped) 
        {
            __instance.StopAllCoroutines();
            if (___timeMineStarted - ___timeMineStoped > ___playerSource.GetMultitool().GetMultiToolMine().GetMineTime())
            {
                // This also should fix two potential NPEs with quickly disappearing ores
                WorldObjectAssociated woa = __instance.GetComponent<WorldObjectAssociated>();
                if (woa != null) 
                {
                    WorldObject worldObject = woa.GetWorldObject();
                    if (worldObject != null) 
                    {
                        worldObject.SetDontSaveMe(false);

                        ___playerSource.GetPlayerBackpack().GetInventory().AddItem(worldObject);

                        if (isEnabled.Value)
                        {
                            ShowInventoryAdded(worldObject, ___playerSource.GetPlayerBackpack().GetInventory());
                        }
                     }
                }
                // make sure the multitool and animations are always turned off
                ___playerSource.GetMultitool().GetMultiToolMine().Mine(false, true);
                ___playerAnimations.AnimateRecolt(false);
                ___itemWorldDisplayer.Hide();
                ___firstReduceDelay = false;
                UnityEngine.Object.Destroy(__instance.gameObject);
            }

            return false;
        }

        static WorldObject toGrab;

        [HarmonyPrefix]
        [HarmonyPatch(typeof(ActionGrabable), "Grab")]
        static bool ActionGrabable_Grab(ActionGrabable __instance)
        {
            if (isEnabled.Value)
            {
                toGrab = null;
                WorldObjectAssociated woa = __instance.GetComponent<WorldObjectAssociated>();
                if (woa != null)
                {
                    toGrab = woa.GetWorldObject();
                }
            }
            return true;
        }
        [HarmonyPostfix]
        [HarmonyPatch(typeof(ActionGrabable), "Grab")]
        static void ActionGrabable_Grab_Post(bool ___canGrab, PlayerMainController ___playerSource)
        {
            if (isEnabled.Value)
            {
                WorldObject worldObject = toGrab;
                toGrab = null;

                if (worldObject != null && ___canGrab)
                {
                    ShowInventoryAdded(worldObject, ___playerSource.GetPlayerBackpack().GetInventory());
                }
            }
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
