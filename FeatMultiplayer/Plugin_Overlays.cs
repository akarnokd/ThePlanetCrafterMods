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

        static GameObject playerListOverlay;
        static Transform healthGaugeTransform;
        static Transform waterGaugeTransform;
        static List<GameObject> playerListGameObjects = new();

        void OverlaySetup()
        {
            EnsureKeyboard(playerLocatorKey);
            playerLocatorAction = new InputAction(name: "PlayerLocator", binding: playerLocatorKey.Value);
            playerLocatorAction.Enable();
        }

        void HandleOverlays()
        {
            HandlePlayerLocator();

            HandlePlayerList();
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

            if (conn != null && conn.clientName != null && playerAvatars.TryGetValue(conn.clientName, out var avatar))
            {
                var pos = avatar.rawPosition;

                text.text = "<b>" + (isHost ? "<Host>" : conn.clientName) + "\n(" + ((int)Vector3.Distance(pos, localPlayerPos)) + " m)</b>";

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
                playerListOverlay = new GameObject("FeatMultiplayer_PlayerListOverlay");
                var canvas = playerListOverlay.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = 95;
            }

            while (playerListGameObjects.Count < playerAvatars.Count + 1)
            {
                var go = new GameObject("FeatMultiplayer_PlayerList_" + playerListGameObjects.Count);
                go.transform.SetParent(playerListOverlay.transform, false);

                var text = go.AddComponent<Text>();
                text.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
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

            var namesList = new List<string>(playerAvatars.Values.Select(p => p.name));

            if (updateMode == MultiplayerMode.CoopHost)
            {
                var hn = hostDisplayName.Value;
                if (string.IsNullOrEmpty(hn))
                {
                    namesList.Insert(0, "< Host >");
                }
                else
                {
                    namesList.Insert(0, hn);
                }
            }
            else
            {
                namesList.Insert(0, clientJoinName);
            }

            foreach (var player in namesList)
            {
                var go = playerListGameObjects[i];

                go.SetActive(true);

                var txt = go.GetComponent<Text>();
                txt.text = player;

                if (i == 0)
                {
                    txt.color = Color.yellow;
                    txt.fontStyle = FontStyle.Bold;
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
        
    }
}
