// Copyright (c) 2022-2025, David Karnok & Contributors
// Licensed under the Apache License, Version 2.0

using BepInEx;
using SpaceCraft;
using HarmonyLib;
using BepInEx.Configuration;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using System.Collections.Generic;
using Unity.Netcode;

namespace MultiPlayerLocator
{
    [BepInPlugin(modMultiPlayerLocator, "(Multi) Player Locator", PluginInfo.PLUGIN_VERSION)]
    public class Plugin : BaseUnityPlugin
    {
        const string modMultiPlayerLocator = "akarnokd.theplanetcraftermods.multiplayerlocator";

        static ConfigEntry<bool> modEnabled;
        static ConfigEntry<string> toggleKey;
        static ConfigEntry<int> fontSize;
        
        static InputAction playerLocatorAction;
        static GameObject playerLocatorOverlay;

        static GameObject playerListOverlay;
        static Transform healthGaugeTransform;
        static Transform waterGaugeTransform;
        static readonly List<GameObject> playerListGameObjects = [];

        static Font font;

        const int sortingOrder = 1;

        static bool introPlayedOnLoad;

        public void Awake()
        {
            LibCommon.BepInExLoggerFix.ApplyFix();

            // Plugin startup logic
            Logger.LogInfo($"Plugin is loaded!");

            modEnabled = Config.Bind("General", "Enabled", true, "Enable this mod");
            toggleKey = Config.Bind("General", "Key", "H", "The input action shortcut to toggle the player locator overlay.");
            fontSize = Config.Bind("General", "FontSize", 20, "The font size used");

            UpdateKeyBindings();

            font = Resources.GetBuiltinResource<Font>("Arial.ttf");

            LibCommon.HarmonyIntegrityCheck.Check(typeof(Plugin));
            Harmony.CreateAndPatchAll(typeof(Plugin));
        }

        public void Update()
        {
            var pm = Managers.GetManager<PlayersManager>();
            if (pm == null)
            {
                return;
            }
            var ac = pm.GetActivePlayerController();
            if (ac == null)
            {
                return;
            }
            /*
            var backpack = ac.GetPlayerBackpack();
            if (backpack == null)
            {
                return;
            }
            if (backpack.GetInventory() == null)
            {
                return;
            }
            */

            if (Managers.GetManager<SavedDataHandler>().GetSavedDataPlayedIntro())
            {
                introPlayedOnLoad = true;
            }
            if (!introPlayedOnLoad)
            {
                return;
            }

            if (modEnabled.Value)
            {
                HandlePlayerLocator();

                HandlePlayerList();
            }
            else
            {
                if (playerLocatorOverlay != null)
                {
                    playerLocatorOverlay.SetActive(false);
                }
                if (playerListOverlay != null)
                { 
                    playerListOverlay.SetActive(false); 
                }
            }
        }

        void HandlePlayerLocator()
        {
            if (playerLocatorOverlay == null)
            {
                playerLocatorOverlay = new GameObject("MultiPlayerLocator_PlayerLocatorOverlay");
                var canvas = playerLocatorOverlay.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = sortingOrder;
                playerLocatorOverlay.SetActive(false);
            }

            WindowsHandler wh = Managers.GetManager<WindowsHandler>();
            if (playerLocatorAction.WasPressedThisFrame() && !wh.GetHasUiOpen())
            {
                playerLocatorOverlay.SetActive(!playerLocatorOverlay.activeSelf);
            }

            if (!playerLocatorOverlay.activeSelf)
            {
                return;
            }

            PlayerLocator_PrepareChildren();

            PlayerLocator_UpdateChildren();
        }

        void PlayerLocator_PrepareChildren()
        {
            var n = 0;

            var pm = Managers.GetManager<PlayersManager>();
            foreach (var p in pm.playersControllers)
            {
                if (p != pm.GetActivePlayerController() && p != null)
                {
                    n++;
                }
            }

            while (playerLocatorOverlay.transform.childCount < n)
            {
                var textGo = new GameObject("MultiPlayerLocator_" + playerLocatorOverlay.transform.childCount);
                textGo.transform.SetParent(playerLocatorOverlay.transform, false);

                var text = textGo.AddComponent<Text>();
                text.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
                text.text = "";
                text.color = Color.white;
                text.fontSize = fontSize.Value;
                text.resizeTextForBestFit = false;
                text.verticalOverflow = VerticalWrapMode.Overflow;
                text.horizontalOverflow = HorizontalWrapMode.Overflow;
                text.alignment = TextAnchor.MiddleCenter;
                text.supportRichText = true;

                var outline = textGo.AddComponent<Outline>();
                outline.effectColor = Color.black;
                outline.effectDistance = new Vector2(1, -1);
            }
        }

        void PlayerLocator_UpdateChildren()
        {
            var pm = Managers.GetManager<PlayersManager>();
            var player = pm.GetActivePlayerController();
            var pos = player.transform.position;

            int j = 0;

            foreach (var cc in pm.playersControllers)
            {
                if (cc != player && cc != null)
                {
                    UpdatePlayerLocation(cc, playerLocatorOverlay.transform.GetChild(j).gameObject, pos, cc.OwnerClientId == NetworkManager.ServerClientId);
                    j++;
                }
            }

            for (; j < playerLocatorOverlay.transform.childCount; j++)
            {
                UpdatePlayerLocation(null, playerLocatorOverlay.transform.GetChild(j).gameObject, pos);
            }
        }

        void UpdatePlayerLocation(PlayerMainController conn, GameObject otherPlayer, Vector3 localPlayerPos, bool isHost = false)
        {
            var text = otherPlayer.GetComponent<Text>();

            if (conn != null)
            {
                var pos = conn.transform.position;
                var clientName = conn.playerName;
                if (isHost)
                {
                    clientName += " <Host>";
                }

                text.text = "<b>" + clientName + "\n(" + ((int)Vector3.Distance(pos, localPlayerPos)) + " m)</b>";

                var xy = Camera.main.WorldToScreenPoint(pos) - new Vector3(Screen.width / 2, Screen.height / 2, 0);
                var heading = pos - Camera.main.transform.position;
                var behind = Vector3.Dot(Camera.main.transform.forward, heading) < 0;

                var rect = text.GetComponent<RectTransform>();

                var x = xy.x;
                var y = xy.y;
                var w = text.preferredWidth;
                var h = text.preferredHeight;

                if (behind)
                {
                    x = -x;
                }

                var minX = -Screen.width / 2 + w / 2;
                var maxX = Screen.width / 2 - w / 2;
                var minY = -Screen.height / 2 + h / 2;
                var maxY = Screen.height / 2 - h / 2;

                x = Mathf.Clamp(x, minX, maxX);
                y = Mathf.Clamp(y, minY, maxY);


                if (behind)
                {
                    if (Mathf.Abs(y - minY) > Mathf.Abs(maxY - y))
                    {
                        y = minY;
                    }
                    else
                    {
                        y = maxY;
                    }
                }

                rect.localPosition = new Vector2(x, y);
            }
            else
            {
                text.text = "";
            }
        }

        void HandlePlayerList()
        {
            if (healthGaugeTransform == null)
            {
                var gau = FindAnyObjectByType<PlayerGaugeHealth>();
                if (gau == null)
                {
                    return;
                }
                healthGaugeTransform = gau.transform;

                waterGaugeTransform = FindAnyObjectByType<PlayerGaugeThirst>().transform;
            }

            if (playerListOverlay == null)
            {
                playerListOverlay = new GameObject("MultiPlayerLocator_PlayerListOverlay");
                var canvas = playerListOverlay.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = sortingOrder + 1;
                playerListGameObjects.Clear();
            }

            var pm = Managers.GetManager<PlayersManager>();
            var ac = pm.GetActivePlayerController();

            while (playerListGameObjects.Count < pm.playersControllers.Count)
            {
                var go = new GameObject("Player_" + playerListGameObjects.Count);
                go.transform.SetParent(playerListOverlay.transform, false);

                var text = go.AddComponent<Text>();
                text.font = font;
                text.text = "";
                text.color = Color.white;
                text.fontSize = fontSize.Value;
                text.resizeTextForBestFit = false;
                text.verticalOverflow = VerticalWrapMode.Overflow;
                text.horizontalOverflow = HorizontalWrapMode.Overflow;
                text.alignment = TextAnchor.MiddleCenter;

                var outline = go.AddComponent<Outline>();
                outline.effectColor = Color.black;
                outline.effectDistance = new Vector2(1, -1);

                playerListGameObjects.Add(go);
            }

            var gd = Mathf.Abs(healthGaugeTransform.position.y - waterGaugeTransform.position.y);

            var ddy = fontSize.Value + 5;
            var dx = -Screen.width / 2 + healthGaugeTransform.position.x;
            var dy = -Screen.height / 2 + healthGaugeTransform.position.y + gd + ddy / 2;

            int i = 0;

            foreach (var player in pm.playersControllers)
            {
                var go = playerListGameObjects[i];

                go.SetActive(true);

                var txt = go.GetComponent<Text>();

                if (player == ac)
                {
                    txt.color = Color.yellow;
                    txt.fontStyle = FontStyle.Bold;
                }
                if (player.OwnerClientId == NetworkManager.ServerClientId)
                {
                    txt.text = player.playerName + " <Host>";
                }
                else
                {
                    txt.text = player.playerName;
                }
                var rect = go.GetComponent<RectTransform>();
                rect.localPosition = new Vector3(dx + txt.preferredWidth / 2, dy, 0);


                dy += ddy;

                i++;
            }

            while (i < playerListGameObjects.Count)
            {
                playerListGameObjects[i].SetActive(false);
                i++;
            }

        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(IntroVideoPlayer), "EndReached")]
        static void IntroVideoPlayer_EndReached()
        {
            introPlayedOnLoad = true;
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(UiWindowPause), nameof(UiWindowPause.OnQuit))]
        static void UiWindowPause_OnQuit()
        {
            Destroy(playerListOverlay);
            playerListOverlay = null;
            Destroy(playerLocatorOverlay);
            playerLocatorOverlay = null;
            introPlayedOnLoad = false;
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(BlackScreen), nameof(BlackScreen.DisplayLogoStudio))]
        static void BlackScreen_DisplayLogoStudio()
        {
            UiWindowPause_OnQuit();
        }


        [HarmonyPostfix]
        [HarmonyPatch(typeof(VisualsToggler), nameof(VisualsToggler.ToggleUi))]
        static void VisualsToggler_ToggleUi(List<GameObject> ___uisToHide)
        {
            bool active = ___uisToHide[0].activeSelf;
            playerListOverlay?.SetActive(active);
            playerLocatorOverlay?.SetActive(active);
        }

        static void UpdateKeyBindings()
        {
            if (!toggleKey.Value.Contains("<"))
            {
                toggleKey.Value = "<Keyboard>/" + toggleKey.Value;
            }
            playerLocatorAction = new InputAction(name: "Toggle Player Locator", binding: toggleKey.Value);
            playerLocatorAction.Enable();
        }

        public static void OnModConfigChanged(ConfigEntryBase _)
        {
            UpdateKeyBindings();
        }
    }
}
