using BepInEx;
using HarmonyLib;
using SpaceCraft;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace FeatMultiplayer
{
    public partial class Plugin : BaseUnityPlugin
    {
        static GameObject playerLocatorOverlay;

        void OverlaySetup()
        {
            EnsureKeyboard(playerLocatorKey);
            playerLocatorAction = new InputAction(name: "PlayerLocator", binding: playerLocatorKey.Value);
            playerLocatorAction.Enable();
        }

        void HandleOverlays()
        {
            HandlePlayerLocator();
        }

        void HandlePlayerLocator()
        {
            if (playerLocatorOverlay == null)
            {
                playerLocatorOverlay = new GameObject("FeatMultiplayer_PlayerLocatorOverlay");
                var canvas = playerLocatorOverlay.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = 100;
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
            var n = _clientConnections.Count;
            if (updateMode == MultiplayerMode.CoopClient)
            {
                n++;
            }
            while (playerLocatorOverlay.transform.childCount < n)
            {
                var textGo = new GameObject("FeatMultiplayer_PlayerLocator_" + playerLocatorOverlay.transform.childCount);
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
            var player = GetPlayerMainController();
            var pos = player.transform.position;

            int j = 0;
            if (updateMode == MultiplayerMode.CoopClient)
            {
                UpdatePlayerLocation(_towardsHost, playerLocatorOverlay.transform.GetChild(0).gameObject, pos, true);
                j++;
            }

            foreach (var cc in _clientConnections.Values)
            {
                UpdatePlayerLocation(cc, playerLocatorOverlay.transform.GetChild(j).gameObject, pos);
                j++;
            }

            for (; j < playerLocatorOverlay.transform.childCount; j++)
            {
                UpdatePlayerLocation(null, playerLocatorOverlay.transform.GetChild(j).gameObject, pos);
            }
        }

        void UpdatePlayerLocation(ClientConnection conn, GameObject otherPlayer, Vector3 localPlayerPos, bool isHost = false)
        {
            var text = otherPlayer.GetComponent<Text>();

            if (conn != null)
            {
                var pos = playerAvatars[conn.clientName].rawPosition;

                text.text = "<b>" + (isHost ? "<Host>" : conn.clientName) + "\n(" + ((int)Vector3.Distance(pos, localPlayerPos)) + " m)</b>";

                var xy = Camera.main.WorldToScreenPoint(pos) - new Vector3(Screen.width / 2, Screen.height / 2, 0);

                var rect = text.GetComponent<RectTransform>();

                rect.localPosition = xy; // + new Vector3(text.preferredWidth / 2, - text.preferredHeight / 2);

                var x = rect.localPosition.x;
                var y = rect.localPosition.y;
                var w = text.preferredWidth;
                var h = text.preferredHeight;

                var minX = -Screen.width / 2 + w / 2;
                var maxX = Screen.width / 2 - w / 2;
                var minY = -Screen.height / 2 + h / 2;
                var maxY = Screen.height / 2 - h / 2;

                x = Mathf.Clamp(x, minX, maxX);
                y = Mathf.Clamp(y, minY, maxY);
                rect.localPosition = new Vector2(x, y);
            }
            else
            {
                text.text = "";
            }
        }
    }
}
