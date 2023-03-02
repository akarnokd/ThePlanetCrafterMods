using BepInEx;
using BepInEx.Configuration;
using SpaceCraft;
using HarmonyLib;

namespace UISaveOnQuit
{
    [BepInPlugin("akarnokd.theplanetcraftermods.saveonquit", "(UI) Save When Quitting", "1.0.0.4")]
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
        [HarmonyPatch(typeof(UiWindowPause), nameof(UiWindowPause.OnQuit))]
        static void UiWindowPause_OnQuit(UiWindowPause __instance)
        {
            if (modEnabled.Value)
            {
                __instance.OnSave();
            }
        }
    }
}
