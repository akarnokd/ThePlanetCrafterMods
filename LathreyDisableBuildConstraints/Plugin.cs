using BepInEx;
using SpaceCraft;
using HarmonyLib;
using BepInEx.Configuration;
using UnityEngine.InputSystem;
using System.Collections.Generic;
using System.Linq;

// Reimplemented with permission
// https://github.com/TysonCodes/PlanetCrafterPlugins/tree/master/DisableBuildConstraints
// >>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>
namespace LathreyDisableBuildConstraints
{
    [BepInPlugin("akarnokd.theplanetcraftermods.lathreydisablebuildconstraints", "(Lathrey) Disable Build Constraints", PluginInfo.PLUGIN_VERSION)]
    public class Plugin : BaseUnityPlugin
    {
        private ConfigEntry<Key> configToggleBuildConstraintsModifierKey;
        private ConfigEntry<Key> configToggleBuildConstraintsKey;
        private ConfigEntry<Key> configToggleBuildSnappingKey;

        private static bool constraintsDisabled = false;
        private static bool snappingDisabled = false;

        private static List<string> modes = new List<string>
            {
                "No Build Constraints",
                "No Snapping"
            };
        private void Awake()
        {
            Logger.LogInfo($"Plugin is loaded!");

            configToggleBuildConstraintsModifierKey = Config.Bind("General", "Toggle_Build_Constraints_Modifier_Key", Key.LeftCtrl,
                "Pick the modifier key to use in combination with the key to toggle building constraints off/on.");
            configToggleBuildConstraintsKey = Config.Bind("General", "Toggle_Build_Constraints_Key", Key.G,
                "Pick the key to use in combination with the modifier key to toggle building constraints off/on.");
            configToggleBuildSnappingKey = Config.Bind("General", "Toggle_Build_Snap_Key", Key.J,
                "Pick the key to use in combination with the modifier key to toggle building snapping off/on.");

            Harmony.CreateAndPatchAll(typeof(Plugin));

        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(BuildConstraint), "GetIsRespected")]
        private static bool BuildConstraint_GetIsRespected_Prefix(ref bool __result)
        {
            if (constraintsDisabled)
            {
                __result = true;
                return false;
            }
            return true;
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(SnapPoint), "OnTriggerEnter")]

        private static bool SnapPoint_OnTriggerEnter_Prefix()
        {
            return !snappingDisabled;
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(ConstraintAgainstPanel), "LateUpdate")]

        private static bool ConstraintAgainstPanel_LateUpdate_Prefix()
        {
            return !snappingDisabled;
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(BaseHudHandler), "UpdateHud")]
        private static void BaseHudHandler_UpdateHud_Postfix(BaseHudHandler __instance)
        {
            if (!CanMove())
            {
                return;
            }

            if (constraintsDisabled || snappingDisabled)
            {
                __instance.textPositionDecoration.text += " - " + string.Join(", ", modes
                    .Where((mode, id) => id == 0 && constraintsDisabled || id == 1 && snappingDisabled)
                    );
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
            if (Keyboard.current[configToggleBuildConstraintsModifierKey.Value].isPressed && Keyboard.current[configToggleBuildSnappingKey.Value].wasPressedThisFrame)
            {
                snappingDisabled = !snappingDisabled;
                Logger.LogInfo($"Building snapping is now {!snappingDisabled}");
            }
        }
    }
}
