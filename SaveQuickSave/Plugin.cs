using BepInEx;
using SpaceCraft;
using HarmonyLib;
using System.Collections.Generic;
using System.Globalization;
using BepInEx.Logging;
using BepInEx.Configuration;
using System.IO;
using System;
using System.IO.Compression;
using System.Text;
using System.Collections;
using UnityEngine;
using BepInEx.Bootstrap;
using UnityEngine.InputSystem;

namespace SaveQuickSave
{
    [BepInPlugin("akarnokd.theplanetcraftermods.savequicksave", "(Save) Quick Save", PluginInfo.PLUGIN_VERSION)]
    [BepInDependency(modFeatMultiplayerGuid, BepInDependency.DependencyFlags.SoftDependency)]
    public class Plugin : BaseUnityPlugin
    {

        const string modFeatMultiplayerGuid = "akarnokd.theplanetcraftermods.featmultiplayer";

        static ManualLogSource logger;

        static ConfigEntry<bool> modEnabled;

        static ConfigEntry<string> shortcutKey;

        static InputAction quickSaveAction;

        static Func<string> multiplayerMode;

        private void Awake()
        {
            // Plugin startup logic
            Logger.LogInfo($"Plugin is loaded!");

            logger = Logger;

            modEnabled = Config.Bind("General", "Enabled", true, "Is this mod enabled?");
            shortcutKey = Config.Bind("General", "ShortcutKey", "F5", "The shortcut key for quick saving.");

            if (!shortcutKey.Value.StartsWith("<Keyboard>/"))
            {
                shortcutKey.Value = "<Keyboard>/" + shortcutKey.Value;
            }
            quickSaveAction = new InputAction(name: "QuickSave", binding: shortcutKey.Value);
            quickSaveAction.Enable();

            if (Chainloader.PluginInfos.TryGetValue(modFeatMultiplayerGuid, out var pi))
            {
                multiplayerMode = (Func<string>)AccessTools.Field(pi.Instance.GetType(), "apiGetMultiplayerMode").GetValue(null);
            }
        }

        void Update()
        {
            if (modEnabled.Value && quickSaveAction.WasPressedThisFrame())
            {
                logger.LogInfo("Quick Save Action");
                if (multiplayerMode != null && multiplayerMode() == "CoopClient")
                {
                    Managers.GetManager<BaseHudHandler>().DisplayCursorText("", 3f, "Multiplayer Client mode not supported");
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
                        sdh.SaveWorldData(null);
                        Managers.GetManager<BaseHudHandler>().DisplayCursorText("", 3f, "Quick Save Success");
                    }
                }
            }
        }
    }
}
