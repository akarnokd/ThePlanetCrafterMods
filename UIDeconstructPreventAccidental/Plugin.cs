using BepInEx;
using BepInEx.Configuration;
using SpaceCraft;
using HarmonyLib;

namespace UIDeconstructPreventAccidental
{
    [BepInPlugin("akarnokd.theplanetcraftermods.uideconstructpreventaccidental", "(UI) Prevent Accidental Deconstruct", PluginInfo.PLUGIN_VERSION)]
    public class Plugin : BaseUnityPlugin
    {
        static ConfigEntry<bool> modEnabled;

        private void Awake()
        {
            // Plugin startup logic
            Logger.LogInfo($"Plugin is loaded!");

            modEnabled = Config.Bind("General", "Enabled", true, "Is the mod enabled?");

            Harmony.CreateAndPatchAll(typeof(Plugin));
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(ActionDeconstructible), nameof(ActionDeconstructible.OnAction))]
        static bool ActionDeconstructible_OnAction(BaseHudHandler ___hudHandler)
        {
            var ap = Managers.GetManager<PlayersManager>().GetActivePlayerController();
            if (modEnabled.Value 
                && ap.GetMultitool().GetState() == DataConfig.MultiToolState.Deconstruct
                && !ap.GetPlayerInputDispatcher().IsPressingAccessibilityKey()
            )
            {
                ___hudHandler.DisplayCursorText("", 3, "Hold the <Accessibility Key> to safely deconstruct.");
                return false;
            }
            return true;
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(ActionPanelDeconstruct), nameof(ActionDeconstructible.OnAction))]
        static bool ActionPanelDeconstruct_OnAction(BaseHudHandler ___hudHandler)
        {
            var ap = Managers.GetManager<PlayersManager>().GetActivePlayerController();
            if (modEnabled.Value
                && ap.GetMultitool().GetState() == DataConfig.MultiToolState.Deconstruct
                && !ap.GetPlayerInputDispatcher().IsPressingAccessibilityKey()
            )
            {
                ___hudHandler.DisplayCursorText("", 3, "Hold the <Accessibility Key> to safely deconstruct.");
                return false;
            }
            return true;
        }
    }
}
