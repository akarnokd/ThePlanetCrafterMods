// Copyright (c) 2022-2024, David Karnok & Contributors
// Licensed under the Apache License, Version 2.0

using BepInEx;
using SpaceCraft;
using HarmonyLib;
using BepInEx.Configuration;
using UnityEngine.InputSystem;
using UnityEngine;

// Reimplemented with permission
// https://github.com/TysonCodes/PlanetCrafterPlugins/tree/master/AutoMove
// >>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>
namespace LathreyAutoMove
{
    [BepInPlugin("akarnokd.theplanetcraftermods.lathreyautomove", "(Lathrey) Auto Move", PluginInfo.PLUGIN_VERSION)]
    public class Plugin : BaseUnityPlugin
    {
        private ConfigEntry<Key> configToggleAutoMoveModifierKey;
        private ConfigEntry<Key> configToggleAutoMoveKey;

        private static bool autoMoveEnabled = false;

        public void Awake()
        {
            LibCommon.BepInExLoggerFix.ApplyFix();

            Logger.LogInfo($"Plugin is loaded!");

            configToggleAutoMoveModifierKey = Config.Bind("General", "Toggle_Auto_Move_Modifier_Key", Key.None,
                "Pick the modifier key to use in combination with the key to toggle auto move off/on.");
            configToggleAutoMoveKey = Config.Bind("General", "Toggle_Auto_Move_Key", Key.CapsLock,
                "Pick the key to use in combination with the modifier key to toggle auto move off/on.");

            LibCommon.HarmonyIntegrityCheck.Check(typeof(Plugin));
            Harmony.CreateAndPatchAll(typeof(Plugin));
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(PlayerMovable), "InputOnMove")]
        private static void PlayerMovable_InputOnMove__Postfix()
        {
            autoMoveEnabled = false;
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(BaseHudHandler), "UpdateHud")]
        private static void BaseHudHandler_UpdateHud_Postfix(BaseHudHandler __instance)
        {
            if (!CanMove())
            {
                return;
            }
            if (autoMoveEnabled)
            {
                __instance.textPositionDecoration.text += " - Auto Move";
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

        public void Update()
        {
            bool modifierPressed = configToggleAutoMoveModifierKey.Value == Key.None || Keyboard.current[configToggleAutoMoveModifierKey.Value].isPressed;
            bool toggleKeyPressed = Keyboard.current[configToggleAutoMoveKey.Value].wasPressedThisFrame;
            if (modifierPressed && toggleKeyPressed)
            {
                bool newAutoMove = !autoMoveEnabled;
                PlayerMovable playerMovable = Managers.GetManager<PlayersManager>().GetActivePlayerController().GetPlayerMovable();
                playerMovable.InputOnMove(newAutoMove ? Vector2.up : Vector2.zero);
                autoMoveEnabled = newAutoMove;
                Logger.LogInfo($"AutoMove is now {!autoMoveEnabled}");
            }
        }
    }
}
