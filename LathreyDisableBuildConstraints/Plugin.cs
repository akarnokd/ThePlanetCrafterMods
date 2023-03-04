using BepInEx;
using SpaceCraft;
using HarmonyLib;
using BepInEx.Configuration;
using UnityEngine.InputSystem;

// Reimplemented with permission
// https://github.com/TysonCodes/PlanetCrafterPlugins/tree/master/DisableBuildConstraints
// >>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>
namespace LathreyDisableBuildConstraints
{
    [BepInPlugin("akarnokd.theplanetcraftermods.lathreydisablebuildconstraints", "(Lathrey) Disable Build Constraints", "1.0.0.0")]
    public class Plugin : BaseUnityPlugin
    {
        private ConfigEntry<Key> configToggleBuildConstraintsModifierKey;
        private ConfigEntry<Key> configToggleBuildConstraintsKey;

        private static bool constraintsDisabled = false;

        private void Awake()
        {
            Logger.LogInfo($"Plugin is loaded!");

            configToggleBuildConstraintsModifierKey = Config.Bind("General", "Toggle_Build_Constraints_Modifier_Key", Key.LeftCtrl,
                "Pick the modifier key to use in combination with the key to toggle building constraints off/on.");
            configToggleBuildConstraintsKey = Config.Bind("General", "Toggle_Build_Constraints_Key", Key.G,
                "Pick the key to use in combination with the modifier key to toggle building constraints off/on.");

            Harmony.CreateAndPatchAll(typeof(Plugin));

        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(BuildConstraint), "GetIsRespected")]
        private static void BuildConstraint_GetIsRespected_Postfix(ref bool __result)
        {
            if (constraintsDisabled)
            {
                __result = true;
            }
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(BaseHudHandler), "UpdateHud")]
        private static void BaseHudHandler_UpdateHud_Postfix(BaseHudHandler __instance)
        {
            if (!CanMove())
            {
                return;
            }
            if (constraintsDisabled)
            {
                __instance.textPositionDecoration.text += " - No Build Constraints";
            }
        }

        private static bool CanMove()
        {
            PlayersManager playersManager = Managers.GetManager<PlayersManager>();
            if (playersManager != null)
            {
                return playersManager.GetActivePlayerController().GetPlayerCanAct().GetCanMove();
            }
            return false;
        }

        private void Update()
        {
            if (Keyboard.current[configToggleBuildConstraintsModifierKey.Value].isPressed && Keyboard.current[configToggleBuildConstraintsKey.Value].wasPressedThisFrame)
            {
                constraintsDisabled = !constraintsDisabled;
                Logger.LogInfo($"Building constraints are now {!constraintsDisabled}");
            }
        }
    }
}
