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
using System.Collections.Concurrent;
using System.Text;
using System.IO;
using MijuTools;
using UnityEngine.SceneManagement;
using System.Globalization;

namespace FeatMultiplayer
{
    [BepInPlugin("akarnokd.theplanetcraftermods.featmultiplayer", "(Feat) Multiplayer", "1.0.0.0")]
    public class Plugin : BaseUnityPlugin
    {

        internal static ManualLogSource logger;

        static ConfigEntry<int> port;
        static ConfigEntry<int> networkFrequency;
        static ConfigEntry<int> fullSyncDelay;

        static ConfigEntry<bool> hostMode;
        static ConfigEntry<bool> useUPnP;
        static ConfigEntry<string> hostAcceptName;
        static ConfigEntry<string> hostAcceptPassword;


        // client side properties
        static ConfigEntry<string> hostAddress;
        static ConfigEntry<string> clientName;
        static ConfigEntry<string> clientPassword;

        static ConfigEntry<int> fontSize;

        internal static Texture2D astronautFront;
        internal static Texture2D astronautBack;

        private void Awake()
        {
            // Plugin startup logic
            Logger.LogInfo($"Plugin is loaded!");

            logger = Logger;

            port = Config.Bind("General", "Port", 22526, "The port where the host server is running.");
            fontSize = Config.Bind("General", "FontSize", 20, "The font size used");
            networkFrequency = Config.Bind("General", "Frequency", 20, "The frequency of checking the network for messages.");
            fullSyncDelay = Config.Bind("General", "SyncDelay", 3000, "Delay between full sync from the host to the client, in milliseconds");

            hostMode = Config.Bind("Host", "Host", false, "If true, loading a save will also host it as a multiplayer game.");
            useUPnP = Config.Bind("Host", "UseUPnP", false, "If behind NAT, use UPnP to manually map the HostPort to the external IP address?");
            hostAcceptName = Config.Bind("Host", "Name", "Buddy,Dude", "Comma separated list of client names the host will accept.");
            hostAcceptPassword = Config.Bind("Host", "Password", "password,wordpass", "Comma separated list of the plaintext(!) passwords accepted by the host, in pair with the Host/Name list.");

            hostAddress = Config.Bind("Client", "HostAddress", "", "The IP address where the Host can be located from the client.");
            clientName = Config.Bind("Client", "Name", "Buddy", "The name show to the host when a client joins.");
            clientPassword = Config.Bind("Client", "Password", "password", "The plaintext(!) password presented to the host when joining their game.");

            Assembly me = Assembly.GetExecutingAssembly();
            string dir = Path.GetDirectoryName(me.Location);

            astronautFront = LoadPNG(Path.Combine(dir, "Astronaut_Front.png"));
            astronautBack = LoadPNG(Path.Combine(dir, "Astronaut_Back.png"));

            Harmony.CreateAndPatchAll(typeof(Plugin));
        }

        static Texture2D LoadPNG(string filename)
        {
            Texture2D tex = new Texture2D(100, 200);
            tex.LoadImage(File.ReadAllBytes(filename));

            return tex;
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

        static volatile string externalIP;

        static readonly Color interactiveColor = new Color(1f, 0.75f, 0f, 1f);
        static readonly Color interactiveColorHighlight = new Color(1f, 0.85f, 0.5f, 1f);
        static readonly Color defaultColor = new Color(1f, 1f, 1f, 1f);

        static volatile MultiplayerMode updateMode = MultiplayerMode.MainMenu;

        static readonly ConcurrentQueue<object> sendQueue = new ConcurrentQueue<object>();
        static readonly AutoResetEvent sendQueueBlock = new AutoResetEvent(false);
        static readonly ConcurrentQueue<object> receiveQueue = new ConcurrentQueue<object>();

        static CancellationTokenSource stopNetwork;

        static float lastNeworkSync;
        static float lastHostSync;

        static volatile bool clientConnected;
        static PlayerAvatar otherPlayer;

        static string multiplayerFilename = "Survival-9999999.json";

        enum MultiplayerMode
        {
            MainMenu,
            SinglePlayer,
            CoopHost,
            CoopClient
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(Intro), "Start")]
        static void Intro_Start()
        {
            logger.LogInfo("Intro_Start");
            updateMode = MultiplayerMode.MainMenu;

            parent = new GameObject();
            Canvas canvas = parent.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            int fs = fontSize.Value;
            int dx = -50;
            int dy = -Screen.height / 2 + 8 * (fs + 10) + 10;

            RectTransform rectTransform;

            // -------------------------

            hostModeCheckbox = CreateText(GetHostModeString(), fs, true);

            rectTransform = hostModeCheckbox.GetComponent<Text>().GetComponent<RectTransform>();
            rectTransform.localPosition = new Vector2(dx, dy);
            rectTransform.sizeDelta = new Vector2(300, fs + 5);

            dy -= fs + 10;

            hostLocalIPText = CreateText("    Local Address = " + GetMainIPv4() + ":" + port.Value, fs);
            rectTransform = hostLocalIPText.GetComponent<Text>().GetComponent<RectTransform>();
            rectTransform.localPosition = new Vector2(dx, dy);
            rectTransform.sizeDelta = new Vector2(300, fs + 5);

            dy -= fs + 10;

            upnpCheckBox = CreateText(GetUPnPString(), fs, true);

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

            clientJoinButton = CreateText("  [ Click Here to Join Game ] ", fs, true);

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
                Task.Run(async () =>
                {
                    try
                    {
                        var discoverer = new NatDiscoverer();
                        logger.LogInfo("Begin NAT Discovery");
                        var device = await discoverer.DiscoverDeviceAsync().ConfigureAwait(false);
                        logger.LogInfo(device.ToString());
                        logger.LogInfo("Begin Get External IP");
                        // The following hangs indefinitely, not sure why
                        var ip = await device.GetExternalIPAsync().ConfigureAwait(false);
                        logger.LogInfo("External IP = " + ip);
                        externalIP = ip.ToString();
                    }
                    catch (Exception ex)
                    {
                        logger.LogInfo(ex);
                        externalIP = "    External Address = error";
                    }
                });

                return "    External Address = checking";
            }
            return "    External Address = N/A";
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(SaveFilesSelector), nameof(SaveFilesSelector.SelectedSaveFile))]
        static void SaveFilesSelector_SelectedSaveFile(string _fileName)
        {
            parent.SetActive(false);
            if (hostMode.Value)
            {
                updateMode = MultiplayerMode.CoopHost;
            }
            else
            {
                updateMode = MultiplayerMode.SinglePlayer;
            }
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(SessionController), "Start")]
        static void SessionController_Start()
        {
            sendQueue.Clear();
            receiveQueue.Clear();

            if (updateMode == MultiplayerMode.CoopHost)
            {
                logger.LogInfo("Entering world as Host");
                StartAsHost();
            }
            else if (updateMode == MultiplayerMode.CoopClient)
            {
                logger.LogInfo("Entering world as Client");
                StartAsClient();
            }
            else
            {
                logger.LogInfo("Entering world as Solo");
            }
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(GaugesConsumptionHandler), nameof(GaugesConsumptionHandler.GetThirstConsumptionRate))]
        static bool GaugesConsumptionHandler_GetThirstConsumptionRate(ref float __result)
        {
            __result = -0.0001f;
            return false;
        }
        [HarmonyPrefix]
        [HarmonyPatch(typeof(GaugesConsumptionHandler), nameof(GaugesConsumptionHandler.GetOxygenConsumptionRate))]
        static bool GaugesConsumptionHandler_GetOxygenConsumptionRate(ref float __result)
        {
            __result = -0.0001f;
            return false;
        }
        [HarmonyPrefix]
        [HarmonyPatch(typeof(GaugesConsumptionHandler), nameof(GaugesConsumptionHandler.GetHealthConsumptionRate))]
        static bool GaugesConsumptionHandler_GetHealthConsumptionRate(ref float __result)
        {
            __result = -0.0001f;
            return false;
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(ActionMinable), "FinishMining")]
        static void ActionMinable_FinishMining(ActionMinable __instance,
            PlayerMainController ___playerSource, float ___timeMineStarted, float ___timeMineStoped)
        {
            if (___timeMineStarted - ___timeMineStoped > ___playerSource.GetMultitool().GetMultiToolMine().GetMineTime())
            {
                WorldObjectAssociated woa = __instance.GetComponent<WorldObjectAssociated>();
                if (woa != null)
                {
                    WorldObject worldObject = woa.GetWorldObject();
                    if (worldObject != null)
                    {
                        sendQueue.Enqueue("Mined|" + worldObject.GetId());
                        sendQueueBlock.Set();
                    }
                }
            }
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(UiWindowPause), nameof(UiWindowPause.OnQuit))]
        static void UiWindowPause_OnQuit()
        {
            if (updateMode == MultiplayerMode.CoopClient)
            {
                File.Delete(Application.persistentDataPath + "/" + multiplayerFilename);
            }
            stopNetwork?.Cancel();
            updateMode = MultiplayerMode.MainMenu;
            sendQueue.Clear();
            receiveQueue.Clear();
            otherPlayer?.Destroy();
            otherPlayer = null;
        }

        void Update()
        {
            if (updateMode == MultiplayerMode.MainMenu)
            {
                DoMainMenuUpdate();
            }
            if (updateMode == MultiplayerMode.CoopHost || updateMode == MultiplayerMode.CoopClient)
            {
                DoMultiplayerUpdate();
            }
            else
            {
                /*
                if (Keyboard.current[Key.P].wasPressedThisFrame)
                {
                    otherPlayer?.Destroy();
                    otherPlayer = PlayerAvatar.CreateAvatar();
                    PlayersManager p = Managers.GetManager<PlayersManager>();
                    if (p != null)
                    {
                        PlayerMainController pm = p.GetActivePlayerController();
                        if (pm != null)
                        {
                            Transform player = pm.transform;
                            otherPlayer.SetPosition(player.position, player.rotation);
                        }
                    }
                }
                */
            }
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
                }
                if (IsWithin(clientJoinButton, mouse))
                {
                    hostMode.Value = false;
                    hostModeCheckbox.GetComponent<Text>().text = GetHostModeString();
                    updateMode = MultiplayerMode.CoopClient;

                    CreateMultiplayerSaveAndEnter();
                }
            }
            hostModeCheckbox.GetComponent<Text>().color = IsWithin(hostModeCheckbox, mouse) ? interactiveColorHighlight : interactiveColor;
            upnpCheckBox.GetComponent<Text>().color = IsWithin(upnpCheckBox, mouse) ? interactiveColorHighlight : interactiveColor;
            clientJoinButton.GetComponent<Text>().color = IsWithin(clientJoinButton, mouse) ? interactiveColorHighlight : interactiveColor;

            var eip = externalIP;
            if (eip != null)
            {
                externalIP = null;
                hostExternalIPText.GetComponent<Text>().text = eip;

            }
        }

        void CreateMultiplayerSaveAndEnter()
        {
            File.Delete(Application.persistentDataPath + "/" + multiplayerFilename);

            Managers.GetManager<StaticDataHandler>().LoadStaticData();
            JSONExport.CreateNewSaveFile(multiplayerFilename, DataConfig.GameSettingMode.Chill, DataConfig.GameSettingStartLocation.Standard);

            Managers.GetManager<SavedDataHandler>().SetSaveFileName(multiplayerFilename);
            SceneManager.LoadScene("OpenWorldTest");

            logger.LogInfo("Find SaveFilesSelector");
            var selector = UnityEngine.Object.FindObjectOfType<SaveFilesSelector>();
            selector.gameObject.SetActive(false);

            logger.LogInfo("Find ShowLoading");
            var mi = AccessTools.Method(typeof(SavedDataHandler), "ShowLoading", new Type[] { typeof(bool) });
            mi.Invoke(selector, new object[] { true });
        }

        void DoMultiplayerUpdate()
        {
            var now = Time.realtimeSinceStartup;

            if (updateMode == MultiplayerMode.CoopHost && otherPlayer != null)
            {
                if (now - lastHostSync >= fullSyncDelay.Value / 1000f)
                {
                    lastHostSync = now;
                    SendFullState();
                }
            }

            if (now - lastNeworkSync >= 1f / networkFrequency.Value)
            {
                lastNeworkSync = now;
                // TODO send out state messages
                if (otherPlayer != null)
                {
                    SendPlayerLocation();
                }

                // Receive and apply commands
                while (receiveQueue.TryDequeue(out var message))
                {
                    switch (message)
                    {
                        case NotifyUserMessage num:
                            {
                                NotifyUser(num.message, num.duration);
                                break;
                            }
                        case MessagePlayerPosition mpp:
                            {
                                ReceivePlayerLocation(mpp);
                                break;
                            }
                        case MessageLogin ml:
                            {
                                ReceiveLogin(ml);
                                break;
                            }
                        case MessageConstructs mc:
                            {
                                ReceiveMessageConstructs(mc);
                                break;
                            }
                        case MessageMinables mm:
                            {
                                ReceiveMessageMinables(mm);
                                break;
                            }
                        case string s: {
                                if (s == "Welcome")
                                {
                                    otherPlayer?.Destroy();
                                    otherPlayer = PlayerAvatar.CreateAvatar();
                                }
                                else if (s == "Disconnected")
                                {
                                    logger.LogInfo("Client disconnected");
                                    otherPlayer?.Destroy();
                                    clientConnected = false;
                                }
                                break;
                            }
                        default:
                            {
                                logger.LogInfo(message.GetType().ToString());
                                break;
                            }
                            // TODO dispatch on message type
                    }
                }
            }
        }

        void SendPlayerLocation()
        {
            PlayersManager p = Managers.GetManager<PlayersManager>();
            if (p != null)
            {
                PlayerMainController pm = p.GetActivePlayerController();
                if (pm != null)
                {
                    Transform player = pm.transform;
                    MessagePlayerPosition mpp = new MessagePlayerPosition
                    {
                        position = player.position,
                        rotation = player.rotation
                    };
                    sendQueue.Enqueue(mpp);
                    sendQueueBlock.Set();
                }
            }
        }

        static void ReceivePlayerLocation(MessagePlayerPosition mpp)
        {
            otherPlayer?.SetPosition(mpp.position, mpp.rotation);
        }

        static void ReceiveLogin(MessageLogin ml)
        {
            if (updateMode == MultiplayerMode.CoopHost)
            {
                string[] users = hostAcceptName.Value.Split(',');
                string[] passwords = hostAcceptPassword.Value.Split(',');

                for (int i = 0; i < Math.Min(users.Length, passwords.Length); i++)
                {
                    if (users[i] == ml.user && passwords[i] == ml.password)
                    {
                        logger.LogInfo("User login success: " + ml.user);
                        NotifyUser("User joined: " + ml.user);
                        otherPlayer?.Destroy();
                        otherPlayer = PlayerAvatar.CreateAvatar();
                        sendQueue.Enqueue("Welcome\n");
                        sendQueueBlock.Set();
                        lastHostSync = Time.realtimeSinceStartup;
                        SendFullState();
                        return;
                    }
                }

                logger.LogInfo("User login failed: " + ml.user);
                sendQueue.Enqueue(EAccessDenied);
                sendQueueBlock.Set();
            }
        }

        static void SendFullState()
        {
            logger.LogInfo("Begin syncing the entire game state to the client");
            StringBuilder sb = new StringBuilder();
            sb.Append("Constructs");
            foreach (WorldObject wo in WorldObjectsHandler.GetConstructedWorldObjects())
            {
                sb.Append("|");
                MessageConstructs.AppendWorldObject(sb, wo);
            }
            sb.Append('\n');
            sendQueue.Enqueue(sb.ToString());

            sb = new StringBuilder();
            sb.Append("Minables");
            foreach (WorldObject wo in WorldObjectsHandler.GetAllWorldObjects())
            {
                if (WorldObjectsIdHandler.IsWorldObjectFromScene(wo.GetId()) && !wo.GetIsPlaced())
                {
                    sb.Append('|');
                    sb.Append(wo.GetId());
                }
            }
            sb.Append('\n');

            sendQueue.Enqueue(sb.ToString());
            sendQueueBlock.Set();
        }

        static void ReceiveMessageConstructs(MessageConstructs mc)
        {
            logger.LogInfo("Received all constructs");
        }

        static void ReceiveMessageMinables(MessageMinables mm)
        {
            logger.LogInfo("Received all minables");
        }


        static void StartAsHost()
        {
            stopNetwork = new CancellationTokenSource();
            Task.Factory.StartNew(HostAcceptor, stopNetwork.Token, TaskCreationOptions.LongRunning, TaskScheduler.Default);
        }

        static void StartAsClient()
        {
            stopNetwork = new CancellationTokenSource();
            Task.Run(() =>
            {
                logger.LogInfo("Client connecting to " + hostAddress.Value + ":" + port.Value);
                try
                {
                    TcpClient client = new TcpClient(hostAddress.Value, port.Value);
                    stopNetwork.Token.Register(() =>
                    {
                        client.Close();
                    });
                    Task.Factory.StartNew(SenderLoop, client, stopNetwork.Token, TaskCreationOptions.LongRunning, TaskScheduler.Default);
                    Task.Factory.StartNew(ReceiveLoop, client, stopNetwork.Token, TaskCreationOptions.LongRunning, TaskScheduler.Default);

                    sendQueue.Enqueue(new MessageLogin
                    {
                        user = clientName.Value,
                        password = clientPassword.Value
                    });
                    sendQueueBlock.Set();
                }
                catch (Exception ex)
                {
                    logger.LogError(ex);
                    NotifyUserFromBackground("Error: could not connect to Host");
                }
            });
        }

        static void NotifyUser(string message, float duration = 5f)
        {
            Managers.GetManager<BaseHudHandler>().DisplayCursorText("", duration, message);
        }

        static void NotifyUserFromBackground(string message, float duration = 5f)
        {
            var msg = new NotifyUserMessage
            {
                message = message,
                duration = duration
            };
            receiveQueue.Enqueue(msg);
        }

        static void HostAcceptor()
        {
            logger.LogInfo("Starting HostAcceptor on port " + port.Value);
            try
            {
                TcpListener listener = new TcpListener(IPAddress.Any, port.Value);
                listener.Start();
                stopNetwork.Token.Register(() =>
                {
                    listener.Stop();
                });
                try
                {
                    while (!stopNetwork.IsCancellationRequested)
                    {
                        var client = listener.AcceptTcpClient();
                        ManageClient(client);
                    }
                }
                finally
                {
                    listener.Stop();
                    logger.LogInfo("Stopping HostAcceptor on port " + port.Value);
                }
            } catch (Exception ex)
            {
                if (!stopNetwork.IsCancellationRequested)
                {
                    logger.LogError(ex);
                }
            }
        }

        static void ManageClient(TcpClient client)
        {
            if (clientConnected)
            {
                logger.LogInfo("A client already connected");
                try {
                    try
                    {
                        var stream = client.GetStream();
                        try
                        {
                            stream.Write(ENoClientSlotBytes);
                            stream.Flush();
                        }
                        finally
                        {
                            stream.Close();
                        }
                    }
                    finally
                    {
                        client.Close();
                    }
                }
                catch (Exception ex)
                {
                    logger.LogError(ex);
                }
            } 
            else
            {
                logger.LogInfo("New Client from " + client.Client.RemoteEndPoint);
                clientConnected = true;
                Task.Factory.StartNew(SenderLoop, client, stopNetwork.Token, TaskCreationOptions.LongRunning, TaskScheduler.Default);
                Task.Factory.StartNew(ReceiveLoop, client, stopNetwork.Token, TaskCreationOptions.LongRunning, TaskScheduler.Default);
            }
        }

        static void SenderLoop(object clientObj)
        {
            logger.LogInfo("SenderLoop begin");
            var client = (TcpClient)clientObj;
            try
            {
                try
                {
                    var stream = client.GetStream();
                    try
                    {
                        logger.LogInfo("SenderLoop loop");
                        while (!stopNetwork.IsCancellationRequested)
                        {
                            if (sendQueue.TryDequeue(out var message))
                            {
                                switch (message)
                                {
                                    case string s:
                                        {
                                            stream.Write(s);
                                            stream.Flush();
                                            break;
                                        }
                                    case byte[] b:
                                        {
                                            stream.Write(b);
                                            stream.Flush();
                                            break;
                                        }
                                    case MessageStringProvider msp:
                                        {
                                            stream.Write(msp.GetString());
                                            stream.Flush();
                                            break;
                                        }
                                    case MessageBytesProvider msp:
                                        {
                                            stream.Write(msp.GetBytes());
                                            stream.Flush();
                                            break;
                                        }
                                }
                            }
                            else
                            {
                                sendQueueBlock.WaitOne(1000);
                            }
                        }
                    }
                    finally
                    {
                        stream.Close();
                    }
                }
                finally
                {
                    client.Close();
                }
                logger.LogInfo("SenderLoop stop");
            } catch (Exception ex)
            {
                logger.LogError(ex);
            }
            receiveQueue.Enqueue("Disconnected");
        }

        static void ReceiveLoop(object clientObj)
        {
            logger.LogInfo("ReceiverLoop start");
            var client = (TcpClient)clientObj;
            try
            {
                try
                {
                    var stream = client.GetStream();
                    var reader = new StreamReader(stream, Encoding.UTF8);
                    try
                    {
                        logger.LogInfo("ReceiverLoop loop");
                        while (!stopNetwork.IsCancellationRequested)
                        {
                            var message = reader.ReadLine();
                            if (message != null)
                            {
                                ParseMessage(message);
                            }
                            else
                            {
                                break;
                            }
                        }
                    }
                    finally
                    {
                        stream.Close();
                    }
                }
                finally
                {
                    client.Close();
                }
                logger.LogInfo("ReceiverLoop stop");
            }
            catch (Exception ex)
            {
                logger.LogError(ex);
            }
            receiveQueue.Enqueue("Disconnected");
        }

        static void ParseMessage(string message)
        {
            if (MessagePlayerPosition.TryParse(message, out var mpp))
            {
                receiveQueue.Enqueue(mpp);
            } 
            else
            if (MessageLogin.TryParse(message, out var ml))
            {
                logger.LogInfo("Login attempt: " + ml.user);
                receiveQueue.Enqueue(ml);
            }
            else
            if (MessageConstructs.TryParse(message, out var mc))
            {
                receiveQueue.Enqueue(mc);
            }
            else
            if (message == "ENoClientSlot" && updateMode == MultiplayerMode.CoopClient)
            {
                NotifyUserFromBackground("Host full");
            }
            else
            if (message == "EAccessDenied" && updateMode == MultiplayerMode.CoopClient)
            {
                NotifyUserFromBackground("Host access denied (check user and password settings)");
            }
            else
            if (message == "Welcome" && updateMode == MultiplayerMode.CoopClient)
            {
                receiveQueue.Enqueue("Welcome");
            }
            else
            {
                logger.LogInfo("Unknown message?\r\n" + message);
            }
            // TODO other messages
        }

        static readonly byte[] ENoClientSlotBytes = Encoding.UTF8.GetBytes("ENoClientSlot\n");
        static readonly byte[] EAccessDenied = Encoding.UTF8.GetBytes("EAccessDenied\n");

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

        public static bool IsIPv4(IPAddress ipa) => ipa.AddressFamily == AddressFamily.InterNetwork;

        public static IPAddress GetMainIPv4() => NetworkInterface.GetAllNetworkInterfaces()
            .Select((ni) => ni.GetIPProperties())
            .Where((ip) => ip.GatewayAddresses.Where((ga) => IsIPv4(ga.Address)).Count() > 0)
            .FirstOrDefault()?.UnicastAddresses?
            .Where((ua) => IsIPv4(ua.Address))?.FirstOrDefault()?.Address;
    }
}
