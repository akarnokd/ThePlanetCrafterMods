using BepInEx;
using SpaceCraft;
using HarmonyLib;
using UnityEngine;
using System.Collections.Generic;
using BepInEx.Logging;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using System.Reflection;
using BepInEx.Configuration;
using System.Net;
using System.Net.Sockets;
using System.Net.NetworkInformation;
using System.Linq;
using Open.Nat;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace FeatMultiplayer
{
    [BepInPlugin("akarnokd.theplanetcraftermods.featmultiplayer", "(Feat) Multiplayer", "1.0.0.0")]
    public class Plugin : BaseUnityPlugin
    {

        static ManualLogSource logger;

        static ConfigEntry<int> port;
        static ConfigEntry<bool> hostMode;
        static ConfigEntry<bool> useUPnP;
        static ConfigEntry<string> hostAcceptName;
        static ConfigEntry<string> hostAcceptPassword;

        // client side properties
        static ConfigEntry<string> hostAddress;
        static ConfigEntry<string> clientName;
        static ConfigEntry<string> clientPassword;

        static ConfigEntry<int> fontSize;

        private void Awake()
        {
            // Plugin startup logic
            Logger.LogInfo($"Plugin is loaded!");

            logger = Logger;

            port = Config.Bind("General", "Port", 22526, "The port where the host server is running.");
            fontSize = Config.Bind("General", "FontSize", 20, "The font size used");

            hostMode = Config.Bind("Host", "Host", false, "If true, loading a save will also host it as a multiplayer game.");
            useUPnP = Config.Bind("Host", "UseUPnP", false, "If behind NAT, use UPnP to manually map the HostPort to the external IP address?");
            hostAcceptName = Config.Bind("Host", "Name", "Buddy,Dude", "Comma separated list of client names the host will accept.");
            hostAcceptPassword = Config.Bind("Host", "Password", "password,wordpass", "Comma separated list of the plaintext(!) passwords accepted by the host, in pair with the Host/Name list.");

            hostAddress = Config.Bind("Client", "HostAddress", "", "The IP address where the Host can be located from the client.");
            clientName = Config.Bind("Client", "Name", "Buddy", "The name show to the host when a client joins.");
            clientPassword = Config.Bind("Client", "Password", "password", "The plaintext(!) password presented to the host when joining their game.");


            Harmony.CreateAndPatchAll(typeof(Plugin));
        }

        static GameObject parent;

        static GameObject hostModeCheckbox;
        static GameObject hostLocalIPText;
        static GameObject upnpCheckBox;
        static GameObject hostExternalIPText;

        static GameObject clientModeText;
        static GameObject clientTargetAddressText;
        static GameObject clientNameText;
        static GameObject clientJoinButton;

        static bool mainMenu;

        [HarmonyPostfix]
        [HarmonyPatch(typeof(Intro), "Start")]
        static void Intro_Start()
        {
            logger.LogInfo("Intro_Start");
            mainMenu = true;

            parent = new GameObject();
            Canvas canvas = parent.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            int fs = fontSize.Value;
            int dx = -50;
            int dy = -Screen.height / 2 + 8 * (fs + 10) + 10;

            RectTransform rectTransform;

            // -------------------------

            hostModeCheckbox = CreateText(GetHostModeString(), fs);

            rectTransform = hostModeCheckbox.GetComponent<Text>().GetComponent<RectTransform>();
            rectTransform.localPosition = new Vector2(dx, dy);
            rectTransform.sizeDelta = new Vector2(300, fs + 5);

            dy -= fs + 10;

            hostLocalIPText = CreateText("    Local Address = " + GetMainIPv4() + ":" + port.Value, fs);
            rectTransform = hostLocalIPText.GetComponent<Text>().GetComponent<RectTransform>();
            rectTransform.localPosition = new Vector2(dx, dy);
            rectTransform.sizeDelta = new Vector2(300, fs + 5);

            dy -= fs + 10;

            upnpCheckBox = CreateText(GetUPnPString(), fs);

            rectTransform = upnpCheckBox.GetComponent<Text>().GetComponent<RectTransform>();
            rectTransform.localPosition = new Vector2(dx, dy);
            rectTransform.sizeDelta = new Vector2(300, fs + 5);

            dy -= fs + 10;

            hostExternalIPText = CreateText(GetExternalAddressString(), fs);

            rectTransform = hostExternalIPText.GetComponent<Text>().GetComponent<RectTransform>();
            rectTransform.localPosition = new Vector2(dx, dy);
            rectTransform.sizeDelta = new Vector2(300, fs + 5);

            dy -= fs + 20;

            clientModeText = CreateText("--- Client Mode ---", fs);

            rectTransform = clientModeText.GetComponent<Text>().GetComponent<RectTransform>();
            rectTransform.localPosition = new Vector2(dx, dy);
            rectTransform.sizeDelta = new Vector2(300, fs + 5);

            dy -= fs + 10;

            clientTargetAddressText = CreateText("    Host Address = " + hostAddress.Value + ":" + port.Value, fs);

            rectTransform = clientTargetAddressText.GetComponent<Text>().GetComponent<RectTransform>();
            rectTransform.localPosition = new Vector2(dx, dy);
            rectTransform.sizeDelta = new Vector2(300, fs + 5);

            dy -= fs + 10;

            clientNameText = CreateText("    Client Name = " + clientName.Value, fs);

            rectTransform = clientNameText.GetComponent<Text>().GetComponent<RectTransform>();
            rectTransform.localPosition = new Vector2(dx, dy);
            rectTransform.sizeDelta = new Vector2(300, fs + 5);

            dy -= fs + 10;

            clientJoinButton = CreateText("  [ Click Here to Join Game ] ", fs);

            rectTransform = clientJoinButton.GetComponent<Text>().GetComponent<RectTransform>();
            rectTransform.localPosition = new Vector2(dx, dy);
            rectTransform.sizeDelta = new Vector2(300, fs + 5);
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
            if (useUPnP.Value) {
                try
                {
                    Task.Run(async () =>
                    {
                        var cts = new CancellationTokenSource(10000);

                        var discoverer = new NatDiscoverer();
                        logger.LogInfo("Begin NAT Discovery");
                        var device = await discoverer.DiscoverDeviceAsync(PortMapper.Upnp, cts).ConfigureAwait(false);
                        logger.LogInfo(device.ToString());
                        logger.LogInfo("Begin Get External IP");
                        // The following hangs indefinitely, not sure why
                        var ip = await device.GetExternalIPAsync().ConfigureAwait(false);
                        logger.LogInfo("External IP = " + ip);
                    });
                    
                    return "    External Address = ???";
                }
                catch (Exception ex)
                {
                    logger.LogInfo(ex);
                }
            }
            return "    External Address = N/A";
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(SaveFilesSelector), nameof(SaveFilesSelector.SelectedSaveFile))]
        static void SaveFilesSelector_SelectedSaveFile(string _fileName)
        {
            mainMenu = false;
            parent.SetActive(false);
        }

        void Update()
        {
            if (mainMenu)
            {
                if (Mouse.current.leftButton.wasPressedThisFrame)
                {
                    var mouse = Mouse.current.position.ReadValue();
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
                    }
                    if (IsWithin(clientJoinButton, mouse))
                    {
                        logger.LogInfo("TODO Join Button clicked");
                    }
                }
            }
        }

        static GameObject CreateText(string txt, int fs)
        {
            var result = new GameObject();
            result.transform.parent = parent.transform;

            Text text = result.AddComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            text.text = txt;
            text.color = new Color(1f, 1f, 1f, 1f);
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

        public static bool IsIPv4(IPAddress ipa) => ipa.AddressFamily == AddressFamily.InterNetwork;

        public static IPAddress GetMainIPv4() => NetworkInterface.GetAllNetworkInterfaces()
            .Select((ni) => ni.GetIPProperties())
            .Where((ip) => ip.GatewayAddresses.Where((ga) => IsIPv4(ga.Address)).Count() > 0)
            .FirstOrDefault()?.UnicastAddresses?
            .Where((ua) => IsIPv4(ua.Address))?.FirstOrDefault()?.Address;
    }
}
