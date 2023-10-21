using BepInEx;
using HarmonyLib;
using LibCommon;
using Open.Nat;
using SpaceCraft;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Reflection;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using static UnityEngine.UIElements.UIRAtlasAllocator;

namespace FeatMultiplayer
{
    public partial class Plugin : BaseUnityPlugin
    {
        static GameObject parent;

        static GameObject modTitle;
        static GameObject hostModeCheckbox;
        static GameObject hostLocalIPText;
        static GameObject upnpCheckBox;
        static GameObject hostExternalIPText;
        static GameObject hostExternalMappingText;

        static GameObject clientModeText;
        static GameObject clientTargetAddressText;
        static readonly List<GameObject> clientJoinButtons = new();
        static readonly List<string> playerNames = new();
        static readonly List<string> playerPasswords = new();

        static volatile string externalIP;
        static volatile string externalMap;

        static readonly Color interactiveColor = new Color(1f, 0.75f, 0f, 1f);
        static readonly Color interactiveColorHighlight = new Color(1f, 0.85f, 0.5f, 1f);
        static readonly Color interactiveColor2 = new Color(0.75f, 1, 0f, 1f);
        static readonly Color interactiveColorHighlight2 = new Color(0.85f, 1, 0.5f, 1f);
        static readonly Color defaultColor = new Color(1f, 1f, 1f, 1f);

        static Intro introInstance;
        static readonly List<GameObject> mpRows = new();
        static GameObject mainmenuBackground;
        static GameObject mainmenuTitleBackground;

        [HarmonyPostfix]
        [HarmonyPatch(typeof(Intro), "Start")]
        static void Intro_Start(Intro __instance)
        {
            LogInfo("Intro_Start");
            updateMode = MultiplayerMode.MainMenu;
            introInstance = __instance;

            int rows = 9;

            mpRows.Clear();
            clientJoinButtons.Clear();

            playerNames.Clear();
            playerNames.AddRange(clientName.Value.Split(','));
            playerPasswords.Clear();
            playerPasswords.AddRange(clientPassword.Value.Split(','));

            parent = new GameObject("MultiplayerMenu");
            Canvas canvas = parent.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            int fs = fontSize.Value;
            int dx = Screen.width / 2 - 200;
            float dy = Screen.height / 2 - rows / 2f * (fs + 10) + 10;
            int dw = 300;
            int backgroundX = Screen.width / 2 - (200 + dw / 2) / 2 - 10;

            RectTransform rectTransform;

            // -------------------------

            mainmenuTitleBackground = new GameObject("MultiplayerMenu_Background2");
            mainmenuTitleBackground.transform.parent = parent.transform;

            var img0 = mainmenuTitleBackground.AddComponent<Image>();
            img0.color = new Color(0.20f, 0.20f, 0.20f, 0.99f);

            rectTransform = img0.GetComponent<RectTransform>();
            rectTransform.localPosition = new Vector2(backgroundX, dy + fs + 10);
            rectTransform.sizeDelta = new Vector2(350, fs + 20);

            modTitle = CreateText("(Feat) Multiplayer Mod v" + PluginInfo.PLUGIN_VERSION, fs, false);

            rectTransform = modTitle.GetComponent<Text>().GetComponent<RectTransform>();
            rectTransform.localPosition = new Vector2(backgroundX, dy + fs + 10);
            rectTransform.sizeDelta = new Vector2(350, fs + 5);

            Text modTitleText = modTitle.GetComponent<Text>();
            modTitleText.fontStyle = FontStyle.Bold;

            // -------------------------

            mainmenuBackground = new GameObject("MultiplayerMenu_Background");
            mainmenuBackground.transform.parent = parent.transform;

            var img = mainmenuBackground.AddComponent<Image>();
            img.color = new Color(0f, 0f, 0f, 0.95f);

            rectTransform = img.GetComponent<RectTransform>();
            rectTransform.localPosition = new Vector2(backgroundX, dy - rows / 2f * (fs + 10) + 10);
            rectTransform.sizeDelta = new Vector2(350, rows * (fs + 10) + 20);

            hostModeCheckbox = CreateText(GetHostModeString(), fs, true);

            rectTransform = hostModeCheckbox.GetComponent<Text>().GetComponent<RectTransform>();
            rectTransform.localPosition = new Vector2(dx, dy);
            rectTransform.sizeDelta = new Vector2(dw, fs + 5);

            mpRows.Add(hostModeCheckbox);
            dy -= fs + 10;

            var localAddrStr = GetHostLocalAddress() + ":" + port.Value;
            if (streamerMode.Value)
            {
                localAddrStr = "<redacted>";
            }
            hostLocalIPText = CreateText("    Local Address = " + localAddrStr, fs);
            rectTransform = hostLocalIPText.GetComponent<Text>().GetComponent<RectTransform>();
            rectTransform.localPosition = new Vector2(dx, dy);
            rectTransform.sizeDelta = new Vector2(dw, fs + 5);

            LogInfo(GetMainIPv6());

            mpRows.Add(hostLocalIPText);
            dy -= fs + 10;

            upnpCheckBox = CreateText(GetUPnPString(), fs, true);

            rectTransform = upnpCheckBox.GetComponent<Text>().GetComponent<RectTransform>();
            rectTransform.localPosition = new Vector2(dx, dy);
            rectTransform.sizeDelta = new Vector2(dw, fs + 5);

            mpRows.Add(upnpCheckBox);
            dy -= fs + 10;

            hostExternalIPText = CreateText(GetExternalAddressString(), fs);

            rectTransform = hostExternalIPText.GetComponent<Text>().GetComponent<RectTransform>();
            rectTransform.localPosition = new Vector2(dx, dy);
            rectTransform.sizeDelta = new Vector2(dw, fs + 5);

            mpRows.Add(hostExternalIPText);
            dy -= fs + 10;

            hostExternalMappingText = CreateText(GetExternalMappingString(), fs);

            rectTransform = hostExternalMappingText.GetComponent<Text>().GetComponent<RectTransform>();
            rectTransform.localPosition = new Vector2(dx, dy);
            rectTransform.sizeDelta = new Vector2(dw, fs + 5);

            mpRows.Add(hostExternalMappingText);
            dy -= fs + 20;

            clientModeText = CreateText("--- Client Mode ---", fs);

            rectTransform = clientModeText.GetComponent<Text>().GetComponent<RectTransform>();
            rectTransform.localPosition = new Vector2(dx, dy);
            rectTransform.sizeDelta = new Vector2(dw, fs + 5);

            mpRows.Add(clientModeText);
            dy -= fs + 10;

            var hostAddrStr = hostAddress.Value + ":" + port.Value;
            if (streamerMode.Value)
            {
                hostAddrStr = "<redacted>";
            }
            clientTargetAddressText = CreateText("    Host Address = " + hostAddrStr, fs);

            rectTransform = clientTargetAddressText.GetComponent<Text>().GetComponent<RectTransform>();
            rectTransform.localPosition = new Vector2(dx, dy);
            rectTransform.sizeDelta = new Vector2(dw, fs + 5);

            mpRows.Add(clientTargetAddressText);
            dy -= fs + 10;

            foreach (var clientName in playerNames) {

                var joinBtn = CreateText("  [ Join as " + clientName + " ] ", fs, true);

                rectTransform = joinBtn.GetComponent<Text>().GetComponent<RectTransform>();
                rectTransform.localPosition = new Vector2(dx, dy);
                rectTransform.sizeDelta = new Vector2(dw, fs + 5);

                clientJoinButtons.Add(joinBtn);
                mpRows.Add(joinBtn);

                dy -= fs + 10;
            }

            CheckBepInEx();
        }

        static string GetHostModeString()
        {
            return "( " + (hostMode.Value ? "X" : "  ") + " ) Host a multiplayer game";
        }

        static string GetUPnPString()
        {
            return "( " + (useUPnP.Value ? "X" : "  ") + " ) Use UPnP";
        }

        static string GetHostLocalAddress()
        {
            var hostIp = hostServiceAddress.Value;
            IPAddress hostIPAddress = IPAddress.Any;
            if (hostIp == "default")
            {
                hostIPAddress = GetMainIPv4();
            }
            else
            if (hostIp == "defaultv6")
            {
                hostIPAddress = GetMainIPv6();
            }
            else
            {
                hostIPAddress = IPAddress.Parse(hostIp);
            }
            return hostIPAddress.ToString();
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

                        // PMPNatDevice doesn't support this call apparently
                        try
                        {
                            var mapping = await device.GetSpecificMappingAsync(Protocol.Tcp, portNum).ConfigureAwait(false);
                            LogInfo("Current Mapping = " + mapping);
                        }
                        catch (Exception ex)
                        {
                            // Ignore it for now
                            LogInfo("Current Mapping = error");
                            LogInfo(ex);
                        }
                        try
                        {
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
            var noMenu = true;
            if (introInstance != null)
            {
                if (introInstance.optionsMenu.activeSelf)
                {
                    noMenu = false;
                }
                if (introInstance.saveFilesContainer.activeSelf)
                {
                    noMenu = false;
                }
            }
            if (parent != null && noMenu)
            {
                float maxWidth = 0;
                int fs = fontSize.Value;

                foreach (var go in mpRows)
                {
                    var w = go.GetComponent<Text>().preferredWidth;
                    maxWidth = Math.Max(maxWidth, w);
                }

                maxWidth = Math.Max(maxWidth, modTitle.GetComponent<Text>().preferredWidth);

                var dx = (Screen.width - maxWidth) / 2 - 20;
                float dy = Screen.height / 2 - mpRows.Count / 2f * (fs + 10) + 10;

                var rtb = mainmenuBackground.GetComponent<RectTransform>();
                rtb.localPosition = new Vector3(dx, Screen.height / 2 - mpRows.Count * (fs + 10) + 10 + (fs + 10) / 2, 0);
                rtb.sizeDelta = new Vector2(maxWidth + 20, mpRows.Count * (fs + 10) + 20);

                var rtb2 = mainmenuTitleBackground.GetComponent<RectTransform>();
                rtb2.localPosition = rtb.localPosition + new Vector3(0, rtb.sizeDelta.y / 2 + fs / 2 + 10);
                rtb2.sizeDelta = new Vector2(maxWidth + 20, fs + 20);

                var tt = modTitle.GetComponent<RectTransform>();
                tt.localPosition = rtb2.localPosition + new Vector3(10, 0, 0);
                tt.sizeDelta = rtb2.sizeDelta;

                foreach (var go in mpRows)
                {
                    var rt = go.GetComponent<RectTransform>();
                    rt.localPosition = new Vector3(dx, dy, 0);
                    rt.sizeDelta = new Vector2(maxWidth, fs + 5);
                    dy -= fs + 10;
                }

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
                    for (int i = 0; i < clientJoinButtons.Count; i++)
                    {
                        GameObject joinBtn = clientJoinButtons[i];
                        if (IsWithin(joinBtn, mouse))
                        {
                            clientJoinName = playerNames[i];
                            clientJoinPassword = playerPasswords[i];

                            hostModeCheckbox.GetComponent<Text>().text = GetHostModeString();
                            updateMode = MultiplayerMode.CoopClient;
                            File.Delete(Application.persistentDataPath + "/Player_Client.log");
                            File.Delete(Application.persistentDataPath + "/Player_Client_" + clientJoinName + ".log");
                            joinBtn.GetComponent<Text>().text = " !!! Joining a game !!!";
                            CreateMultiplayerSaveAndEnter();
                        }
                    }
                }
                hostModeCheckbox.GetComponent<Text>().color = IsWithin(hostModeCheckbox, mouse) ? interactiveColorHighlight : interactiveColor;
                upnpCheckBox.GetComponent<Text>().color = IsWithin(upnpCheckBox, mouse) ? interactiveColorHighlight : interactiveColor;
                foreach (var joinBtn in clientJoinButtons)
                {
                    joinBtn.GetComponent<Text>().color = IsWithin(joinBtn, mouse) ? interactiveColorHighlight2 : interactiveColor2;
                }

                var eip = externalIP;
                if (eip != null)
                {
                    externalIP = null;
                    if (streamerMode.Value)
                    {
                        hostExternalIPText.GetComponent<Text>().text = "<redacted>";
                    }
                    else
                    {
                        hostExternalIPText.GetComponent<Text>().text = eip;
                    }
                }
                var emp = externalMap;
                if (emp != null)
                {
                    externalMap = null;
                    hostExternalMappingText.GetComponent<Text>().text = emp;
                }
            }
        }

        static GameObject CreateText(string txt, int fs, bool highlight = false)
        {
            var result = new GameObject("MultiplayerMenu_Text");
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
            MainMenuContinue();
        }

        /// <summary>
        /// Sets the update mode when loading as save or clicking on continue.
        /// </summary>
        public static void MainMenuContinue()
        {
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

        static void CheckBepInEx()
        {
            if (!BepInExConfigCheck.Check(Assembly.GetExecutingAssembly(), theLogger))
            {
                NotifyUser(BepInExConfigCheck.DefaultMessage, 30);
            }
        }
    }
}
