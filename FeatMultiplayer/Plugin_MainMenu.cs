using BepInEx;
using HarmonyLib;
using MijuTools;
using Open.Nat;
using SpaceCraft;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace FeatMultiplayer
{
    public partial class Plugin : BaseUnityPlugin
    {
        static GameObject parent;

        static GameObject hostModeCheckbox;
        static GameObject hostLocalIPText;
        static GameObject upnpCheckBox;
        static GameObject hostExternalIPText;
        static GameObject hostExternalMappingText;

        static GameObject clientModeText;
        static GameObject clientTargetAddressText;
        static GameObject clientNameText;
        static GameObject clientJoinButton;

        static volatile string externalIP;
        static volatile string externalMap;

        static readonly Color interactiveColor = new Color(1f, 0.75f, 0f, 1f);
        static readonly Color interactiveColorHighlight = new Color(1f, 0.85f, 0.5f, 1f);
        static readonly Color interactiveColor2 = new Color(0.75f, 1, 0f, 1f);
        static readonly Color interactiveColorHighlight2 = new Color(0.85f, 1, 0.5f, 1f);
        static readonly Color defaultColor = new Color(1f, 1f, 1f, 1f);

        [HarmonyPostfix]
        [HarmonyPatch(typeof(Intro), "Start")]
        static void Intro_Start()
        {
            LogInfo("Intro_Start");
            updateMode = MultiplayerMode.MainMenu;

            int rows = 9;

            parent = new GameObject();
            Canvas canvas = parent.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            int fs = fontSize.Value;
            int dx = Screen.width / 2 - 200;
            float dy = Screen.height / 2 - rows / 2f * (fs + 10) + 10;
            int dw = 300;
            int backgroundX = Screen.width / 2 - (200 + dw / 2) / 2 - 10;

            RectTransform rectTransform;

            // -------------------------

            var background = new GameObject();
            background.transform.parent = parent.transform;

            var img = background.AddComponent<Image>();
            img.color = new Color(0f, 0f, 0f, 0.95f);

            rectTransform = img.GetComponent<RectTransform>();
            rectTransform.localPosition = new Vector2(backgroundX, dy - rows / 2f * (fs + 10) + 10);
            rectTransform.sizeDelta = new Vector2(350, rows * (fs + 10) + 20);

            hostModeCheckbox = CreateText(GetHostModeString(), fs, true);

            rectTransform = hostModeCheckbox.GetComponent<Text>().GetComponent<RectTransform>();
            rectTransform.localPosition = new Vector2(dx, dy);
            rectTransform.sizeDelta = new Vector2(dw, fs + 5);

            dy -= fs + 10;

            hostLocalIPText = CreateText("    Local Address = " + GetMainIPv4() + ":" + port.Value, fs);
            rectTransform = hostLocalIPText.GetComponent<Text>().GetComponent<RectTransform>();
            rectTransform.localPosition = new Vector2(dx, dy);
            rectTransform.sizeDelta = new Vector2(dw, fs + 5);

            LogInfo(GetMainIPv6());

            dy -= fs + 10;

            upnpCheckBox = CreateText(GetUPnPString(), fs, true);

            rectTransform = upnpCheckBox.GetComponent<Text>().GetComponent<RectTransform>();
            rectTransform.localPosition = new Vector2(dx, dy);
            rectTransform.sizeDelta = new Vector2(dw, fs + 5);

            dy -= fs + 10;

            hostExternalIPText = CreateText(GetExternalAddressString(), fs);

            rectTransform = hostExternalIPText.GetComponent<Text>().GetComponent<RectTransform>();
            rectTransform.localPosition = new Vector2(dx, dy);
            rectTransform.sizeDelta = new Vector2(dw, fs + 5);

            dy -= fs + 10;

            hostExternalMappingText = CreateText(GetExternalMappingString(), fs);

            rectTransform = hostExternalMappingText.GetComponent<Text>().GetComponent<RectTransform>();
            rectTransform.localPosition = new Vector2(dx, dy);
            rectTransform.sizeDelta = new Vector2(dw, fs + 5);

            dy -= fs + 20;

            clientModeText = CreateText("--- Client Mode ---", fs);

            rectTransform = clientModeText.GetComponent<Text>().GetComponent<RectTransform>();
            rectTransform.localPosition = new Vector2(dx, dy);
            rectTransform.sizeDelta = new Vector2(dw, fs + 5);

            dy -= fs + 10;

            clientTargetAddressText = CreateText("    Host Address = " + hostAddress.Value + ":" + port.Value, fs);

            rectTransform = clientTargetAddressText.GetComponent<Text>().GetComponent<RectTransform>();
            rectTransform.localPosition = new Vector2(dx, dy);
            rectTransform.sizeDelta = new Vector2(dw, fs + 5);

            dy -= fs + 10;

            clientNameText = CreateText("    Client Name = " + clientName.Value, fs);

            rectTransform = clientNameText.GetComponent<Text>().GetComponent<RectTransform>();
            rectTransform.localPosition = new Vector2(dx, dy);
            rectTransform.sizeDelta = new Vector2(dw, fs + 5);

            dy -= fs + 10;

            clientJoinButton = CreateText("  [ Click Here to Join Game ] ", fs, true);

            rectTransform = clientJoinButton.GetComponent<Text>().GetComponent<RectTransform>();
            rectTransform.localPosition = new Vector2(dx, dy);
            rectTransform.sizeDelta = new Vector2(dw, fs + 5);
        }

        static string GetHostModeString()
        {
            return "( " + (hostMode.Value ? "X" : " ") + " ) Host a multiplayer game";
        }

        static string GetUPnPString()
        {
            return "( " + (useUPnP.Value ? "X" : " ") + " ) Use UPnP";
        }

        static string GetExternalAddressString()
        {
            if (useUPnP.Value)
            {
                var portNum = port.Value;

                Task.Run(async () =>
                {
                    try
                    {
                        var discoverer = new NatDiscoverer();
                        LogInfo("Begin NAT Discovery");
                        var device = await discoverer.DiscoverDeviceAsync().ConfigureAwait(false);
                        LogInfo(device.ToString());
                        LogInfo("Begin Get External IP");
                        // The following hangs indefinitely, not sure why
                        var ip = await device.GetExternalIPAsync().ConfigureAwait(false);
                        LogInfo("External IP = " + ip);
                        externalIP = "    External Address = " + ip.ToString();

                        try
                        {
                            var mapping = await device.GetSpecificMappingAsync(Protocol.Tcp, portNum).ConfigureAwait(false);
                            LogInfo("Current Mapping = " + mapping);

                            await device.CreatePortMapAsync(new Mapping(Protocol.Tcp, portNum, portNum, "The Planet Crafter Multiplayer")).ConfigureAwait(false);
                            externalMap = "    External Mapping = ok";
                        }
                        catch (Exception ex)
                        {
                            LogInfo(ex);
                            externalMap = "    External Mapping = error";
                        }

                    }
                    catch (Exception ex)
                    {
                        LogInfo(ex);
                        externalIP = "    External Address = error";
                        externalMap = "    External Mapping = error";
                    }
                });

                return "    External Address = checking";
            }
            return "    External Address = N/A";
        }

        static string GetExternalMappingString()
        {
            if (useUPnP.Value)
            {
                return "    External Mapping = checking";
            }
            return "    External Mapping = N/A";
        }

        void DoMainMenuUpdate()
        {
            var mouse = Mouse.current.position.ReadValue();
            if (Mouse.current.leftButton.wasPressedThisFrame)
            {
                if (IsWithin(hostModeCheckbox, mouse))
                {
                    hostMode.Value = !hostMode.Value;
                    hostModeCheckbox.GetComponent<Text>().text = GetHostModeString();
                }
                if (IsWithin(upnpCheckBox, mouse))
                {
                    useUPnP.Value = !useUPnP.Value;
                    upnpCheckBox.GetComponent<Text>().text = GetUPnPString();

                    hostExternalIPText.GetComponent<Text>().text = GetExternalAddressString();
                    hostExternalMappingText.GetComponent<Text>().text = GetExternalMappingString();
                }
                if (IsWithin(clientJoinButton, mouse))
                {
                    hostModeCheckbox.GetComponent<Text>().text = GetHostModeString();
                    updateMode = MultiplayerMode.CoopClient;
                    File.Delete(Application.persistentDataPath + "\\Player_Client.log");
                    clientJoinButton.GetComponent<Text>().text = " !!! Joining a game !!!";
                    CreateMultiplayerSaveAndEnter();
                }
            }
            hostModeCheckbox.GetComponent<Text>().color = IsWithin(hostModeCheckbox, mouse) ? interactiveColorHighlight : interactiveColor;
            upnpCheckBox.GetComponent<Text>().color = IsWithin(upnpCheckBox, mouse) ? interactiveColorHighlight : interactiveColor;
            clientJoinButton.GetComponent<Text>().color = IsWithin(clientJoinButton, mouse) ? interactiveColorHighlight2 : interactiveColor2;

            var eip = externalIP;
            if (eip != null)
            {
                externalIP = null;
                hostExternalIPText.GetComponent<Text>().text = eip;
            }
            var emp = externalMap;
            if (emp != null)
            {
                externalMap = null;
                hostExternalMappingText.GetComponent<Text>().text = emp;
            }
        }

        static GameObject CreateText(string txt, int fs, bool highlight = false)
        {
            var result = new GameObject();
            result.transform.parent = parent.transform;

            Text text = result.AddComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            text.text = txt;
            text.color = highlight ? interactiveColor : defaultColor;
            text.fontSize = (int)fs;
            text.resizeTextForBestFit = false;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.alignment = TextAnchor.MiddleLeft;
            text.raycastTarget = true;

            return result;
        }

        static bool IsWithin(GameObject go, Vector2 mouse)
        {
            RectTransform rect = go.GetComponent<Text>().GetComponent<RectTransform>();

            var lp = rect.localPosition;
            lp.x += Screen.width / 2 - rect.sizeDelta.x / 2;
            lp.y += Screen.height / 2 - rect.sizeDelta.y / 2;

            return mouse.x >= lp.x && mouse.y >= lp.y
                && mouse.x <= lp.x + rect.sizeDelta.x && mouse.y <= lp.y + rect.sizeDelta.y;
        }

        static bool IsIPv4(IPAddress ipa) => ipa.AddressFamily == AddressFamily.InterNetwork;

        static bool IsIPv6(IPAddress ipa) => ipa.AddressFamily == AddressFamily.InterNetworkV6;

        static IPAddress GetMainIPv4() => NetworkInterface.GetAllNetworkInterfaces()
            .Select((ni) => ni.GetIPProperties())
            .Where((ip) => ip.GatewayAddresses.Where((ga) => IsIPv4(ga.Address)).Count() > 0)
            .FirstOrDefault()?.UnicastAddresses?
            .Where((ua) => IsIPv4(ua.Address))?.FirstOrDefault()?.Address;

        static IPAddress GetMainIPv6() => NetworkInterface.GetAllNetworkInterfaces()
            .Select((ni) => ni.GetIPProperties())
            .Where((ip) => ip.GatewayAddresses.Where((ga) => IsIPv6(ga.Address)).Count() > 0)
            .FirstOrDefault()?.UnicastAddresses?
            .Where((ua) => IsIPv6(ua.Address))?.FirstOrDefault()?.Address;

        [HarmonyPrefix]
        [HarmonyPatch(typeof(SaveFilesSelector), nameof(SaveFilesSelector.SelectedSaveFile))]
        static void SaveFilesSelector_SelectedSaveFile(string _fileName)
        {
            parent.SetActive(false);
            if (hostMode.Value)
            {
                updateMode = MultiplayerMode.CoopHost;
                File.Delete(Application.persistentDataPath + "\\Player_Host.log");
            }
            else
            {
                updateMode = MultiplayerMode.SinglePlayer;
            }
        }
    }
}
