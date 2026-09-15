// Copyright (c) 2022-2026, David Karnok & Contributors
// Licensed under the Apache License, Version 2.0

using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using SpaceCraft;
using System.Collections.Generic;
using System.Text;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace UIUnlockHistory
{
    [BepInPlugin("akarnokd.theplanetcraftermods.uiunlockhistory", "(UI) Unlock History", PluginInfo.PLUGIN_VERSION)]
    public class Plugin : BaseUnityPlugin
    {
        static readonly int shadowContainerId = 900009150;

        static ConfigEntry<string> toggleKey;

        static ConfigEntry<int> panelWidth;
        static ConfigEntry<int> panelLines;
        static ConfigEntry<int> fontSize;
        static ConfigEntry<int> iconSize;


        static InputAction toggleAction;

        static GameObject panel;

        private void Awake()
        {
            LibCommon.BepInExLoggerFix.ApplyFix();
            LibCommon.HarmonyIntegrityCheck.Check(typeof(Plugin));
            LibCommon.GameVersionCheck.Patch(new Harmony(PluginInfo.PLUGIN_GUID + "_Ver"), PluginInfo.PLUGIN_NAME + " - v" + PluginInfo.PLUGIN_VERSION);
            if (LibCommon.ModVersionCheck.Check(this, Logger.LogInfo, out var hashError, out var repoURL))
            {
                LibCommon.ModVersionCheck.NotifyUser(this, hashError, repoURL, Logger.LogInfo);
            }

            // Plugin startup logic
            Logger.LogInfo($"Plugin is loaded!");

            toggleKey = Config.Bind("General", "ToggleKey", "<Keyboard>/F3", "Toggle the history display.");
            panelWidth = Config.Bind("General", "PanelWidth", 800, "Width of the panel");
            panelLines = Config.Bind("General", "Lines", 10, "Number of entries to show");
            fontSize = Config.Bind("General", "FontSize", 20, "Font size of the panel text");
            iconSize = Config.Bind("General", "IconSize", 60, "Icon size of the item");

            UpdateKeyBindings();

            Harmony.CreateAndPatchAll(typeof(Plugin));
        }

        void Update()
        {
            if (!toggleAction.enabled)
            {
                toggleAction.Enable();
                return;
            }
            PlayersManager playersManager = Managers.GetManager<PlayersManager>();
            if (playersManager != null)
            {
                PlayerMainController player = playersManager.GetActivePlayerController();
                if (player != null)
                {
                    Setup();
                    UpdateRender();
                    return;
                }
            }
            Teardown();
        }

        void Setup()
        {
            if (panel != null)
            {
                return;
            }

            panel = new GameObject("UnlockHistoryPanel");
            var canvas = panel.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 510;

            RestoreUnlockHistory();
        }

        void UpdateRender()
        {

        }

        static void Teardown()
        {
            Destroy(panel);
            panel = null;
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(PopupsHandler), nameof(PopupsHandler.PopNewUnlockable))]
        static void PopupsHandler_PopNewUnlockable(Group _group, string planetId)
        {
            SaveUnlockHistory();
        }

        static WorldObject EnsureHiddenContainer()
        {
            var wo = WorldObjectsHandler.Instance.GetWorldObjectViaId(shadowContainerId);
            if (wo == null)
            {
                wo = WorldObjectsHandler.Instance.CreateNewWorldObject(GroupsHandler.GetGroupViaId("Container2"), shadowContainerId);
                wo.SetText("");
            }
            wo.SetDontSaveMe(false);
            return wo;
        }

        static void SaveUnlockHistory()
        {
            if (NetworkManager.Singleton?.IsServer ?? true)
            {
                var str = new StringBuilder();

                var wo = EnsureHiddenContainer();
                wo.SetText(str.ToString());
            }
        }

        static void RestoreUnlockHistory()
        {
            if (NetworkManager.Singleton?.IsServer ?? true)
            {
                var wo = EnsureHiddenContainer();
                var str = wo.GetText();
            }
        }

        static void OnModConfigChanged(ConfigEntryBase _)
        {
            UpdateKeyBindings();
            Teardown();
        }

        static void UpdateKeyBindings()
        {
            toggleAction = new InputAction(name: "Toggle Unlock History", binding: toggleKey.Value);
        }
    }
}