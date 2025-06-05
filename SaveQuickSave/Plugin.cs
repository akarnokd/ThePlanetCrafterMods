// Copyright (c) 2022-2025, David Karnok & Contributors
// Licensed under the Apache License, Version 2.0

using BepInEx;
using SpaceCraft;
using BepInEx.Logging;
using BepInEx.Configuration;
using UnityEngine.InputSystem;
using Unity.Netcode;

namespace SaveQuickSave
{
    [BepInPlugin("akarnokd.theplanetcraftermods.savequicksave", "(Save) Quick Save", PluginInfo.PLUGIN_VERSION)]
    public class Plugin : BaseUnityPlugin
    {
        static ManualLogSource logger;

        static ConfigEntry<bool> modEnabled;

        static ConfigEntry<string> shortcutKey;

        static InputAction quickSaveAction;

        public void Awake()
        {
            LibCommon.BepInExLoggerFix.ApplyFix();

            // Plugin startup logic
            Logger.LogInfo($"Plugin is loaded!");

            logger = Logger;

            modEnabled = Config.Bind("General", "Enabled", true, "Is this mod enabled?");
            shortcutKey = Config.Bind("General", "ShortcutKey", "F5", "The shortcut key for quick saving.");

            UpdateKeyBindings();
        }

        public void Update()
        {
            if (modEnabled.Value && quickSaveAction.WasPressedThisFrame())
            {
                logger.LogInfo("Quick Save Action");
                if (NetworkManager.Singleton != null && !NetworkManager.Singleton.IsServer)
                {
                    var bh = Managers.GetManager<BaseHudHandler>();
                    if (bh != null)
                    {
                        bh.DisplayCursorText("", 3f, "Can't save on the client in multiplayer!");
                    }
                    return;
                }
                PlayersManager p = Managers.GetManager<PlayersManager>();
                if (p != null && p.GetActivePlayerController() != null)
                {
                    var sdh = Managers.GetManager<SavedDataHandler>();
                    if (sdh == null)
                    {
                        logger.LogWarning("Unable to find the SavedDataHandler; can't auto save");
                    }
                    else
                    {
                        if (!sdh.IsSaving())
                        {
                            sdh.SaveWorldData(null);
                            Managers.GetManager<BaseHudHandler>().DisplayCursorText("", 3f, "Quick Save Success");
                        }
                    }
                }
            }
        }

        static void UpdateKeyBindings()
        {
            if (!shortcutKey.Value.StartsWith("<"))
            {
                shortcutKey.Value = "<Keyboard>/" + shortcutKey.Value;
            }
            quickSaveAction = new InputAction(name: "QuickSave", binding: shortcutKey.Value);
            quickSaveAction.Enable();
        }

        public static void OnModConfigChanged(ConfigEntryBase _)
        {
            UpdateKeyBindings();
        }
    }
}
