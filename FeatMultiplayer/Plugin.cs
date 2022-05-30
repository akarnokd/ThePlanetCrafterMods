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
using System.Collections;
using TMPro;
using HSVPicker;

namespace FeatMultiplayer
{
    [BepInPlugin("akarnokd.theplanetcraftermods.featmultiplayer", "(Feat) Multiplayer", "1.0.0.0")]
    [BepInDependency("akarnokd.theplanetcraftermods.cheatinventorystacking", BepInDependency.DependencyFlags.SoftDependency)]
    public class Plugin : BaseUnityPlugin
    {

        internal static ManualLogSource theLogger;

        static ConfigEntry<int> port;
        static ConfigEntry<int> networkFrequency;
        static ConfigEntry<int> fullSyncDelay;

        static ConfigEntry<bool> hostMode;
        static ConfigEntry<bool> useUPnP;
        static ConfigEntry<string> hostAcceptName;
        static ConfigEntry<string> hostAcceptPassword;
        static ConfigEntry<string> hostColor;


        // client side properties
        static ConfigEntry<string> hostAddress;
        static ConfigEntry<string> clientName;
        static ConfigEntry<string> clientPassword;
        static ConfigEntry<string> clientColor;

        static ConfigEntry<int> fontSize;

        internal static Texture2D astronautFront;
        internal static Texture2D astronautBack;

        internal static Texture2D astronautFrontHost;
        internal static Texture2D astronautBackHost;

        static readonly object logLock = new object();

        private void Awake()
        {
            // Plugin startup logic
            Logger.LogInfo($"Plugin is loaded!");

            theLogger = Logger;

            port = Config.Bind("General", "Port", 22526, "The port where the host server is running.");
            fontSize = Config.Bind("General", "FontSize", 20, "The font size used");
            networkFrequency = Config.Bind("General", "Frequency", 20, "The frequency of checking the network for messages.");
            fullSyncDelay = Config.Bind("General", "SyncDelay", 3000, "Delay between full sync from the host to the client, in milliseconds");

            hostMode = Config.Bind("Host", "Host", false, "If true, loading a save will also host it as a multiplayer game.");
            useUPnP = Config.Bind("Host", "UseUPnP", false, "If behind NAT, use UPnP to manually map the HostPort to the external IP address?");
            hostAcceptName = Config.Bind("Host", "Name", "Buddy,Dude", "Comma separated list of client names the host will accept.");
            hostAcceptPassword = Config.Bind("Host", "Password", "password,wordpass", "Comma separated list of the plaintext(!) passwords accepted by the host, in pair with the Host/Name list.");
            hostColor = Config.Bind("Host", "Color", "1,1,1,1", "The color of the host avatar as comma-separated RGBA floats");

            hostAddress = Config.Bind("Client", "HostAddress", "", "The IP address where the Host can be located from the client.");
            clientName = Config.Bind("Client", "Name", "Buddy", "The name show to the host when a client joins.");
            clientPassword = Config.Bind("Client", "Password", "password", "The plaintext(!) password presented to the host when joining their game.");
            clientColor = Config.Bind("Client", "Color", "0.75,0.75,1,1", "The color of the client avatar as comma-separated RGBA floats");

            Assembly me = Assembly.GetExecutingAssembly();
            string dir = Path.GetDirectoryName(me.Location);

            astronautFront = LoadPNG(Path.Combine(dir, "Astronaut_Front.png"));
            astronautBack = LoadPNG(Path.Combine(dir, "Astronaut_Back.png"));

            astronautFrontHost = LoadPNG(Path.Combine(dir, "Astronaut_Front_Host.png"));
            astronautBackHost = LoadPNG(Path.Combine(dir, "Astronaut_Back_Host.png"));

            File.Delete(Application.persistentDataPath + "\\Player_Client.log");
            File.Delete(Application.persistentDataPath + "\\Player_Host.log");

            InitFieldAccessors();

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
        static readonly Color interactiveColor2 = new Color(0.75f, 1, 0f, 1f);
        static readonly Color interactiveColorHighlight2 = new Color(0.85f, 1, 0.5f, 1f);
        static readonly Color defaultColor = new Color(1f, 1f, 1f, 1f);

        static volatile MultiplayerMode updateMode = MultiplayerMode.MainMenu;

        static readonly ConcurrentQueue<object> _sendQueue = new ConcurrentQueue<object>();
        static readonly AutoResetEvent _sendQueueBlock = new AutoResetEvent(false);
        static readonly ConcurrentQueue<object> receiveQueue = new ConcurrentQueue<object>();

        static CancellationTokenSource stopNetwork;

        static float lastNeworkSync;
        static float lastHostSync;

        static volatile bool clientConnected;
        static PlayerAvatar otherPlayer;

        static string multiplayerFilename = "Survival-9999999";

        /// <summary>
        /// If true, operations affecting the inventory won't send out messages,
        /// avoiding message ping-pong between the parties.
        /// </summary>
        static bool suppressInventoryChange;

        static volatile bool networkConnected;

        /// <summary>
        /// The vanilla game uses linear lookup for its <code>WorldObjectsHandler.GetWorldObjectViaId()</code>
        /// which is slow for big worlds. This is a shadow map of all world objects by their id
        /// and is maintained by patching the relevant methods that manipulated the underlying list
        /// of <code>WorldObjectsHandler.allWorldObjects</code>.
        /// </summary>
        static readonly Dictionary<int, WorldObject> worldObjectById = new Dictionary<int, WorldObject>();

        static int shadowInventoryWorldId = 50;
        static int shadowInventoryId;
        static int shadowEquipmentWorldId = 51;
        static int shadowEquipmentId;

        /// <summary>
        /// The map from WorldObjects to GameObjects.
        /// Obtained via reflection from the private field <code>WorldObjectsHandler.worldObjects</code>.
        /// <code>WorldObjectsHandler.GetGameObjectViaWorldObject</code> crashes if
        /// the WorldObject is not in the map. We need the dictionary to run TryGetValue/ContainsKey on it.
        /// </summary>
        static Dictionary<WorldObject, GameObject> gameObjectByWorldObject;

        enum MultiplayerMode
        {
            MainMenu,
            SinglePlayer,
            CoopHost,
            CoopClient
        }

        static FieldInfo worldUnitCurrentTotalValue;
        static FieldInfo worldUnitsPositioningWorldUnitsHandler;
        static FieldInfo worldUnitsPositioningHasMadeFirstInit;
        static FieldInfo playerMultitoolCanUseLight;
        static FieldInfo worldObjectTextWorldObject;
        static FieldInfo worldObjectColorWorldObject;

        static void InitFieldAccessors()
        {
            gameObjectByWorldObject = (Dictionary<WorldObject, GameObject>)(AccessTools.Field(typeof(WorldObjectsHandler), "worldObjects").GetValue(null));
            worldUnitCurrentTotalValue = AccessTools.Field(typeof(WorldUnit), "currentTotalValue");
            worldUnitsPositioningWorldUnitsHandler = AccessTools.Field(typeof(WorldUnitPositioning), "worldUnitsHandler");
            worldUnitsPositioningHasMadeFirstInit = AccessTools.Field(typeof(WorldUnitPositioning), "hasMadeFirstInit");
            playerMultitoolCanUseLight = AccessTools.Field(typeof(PlayerMultitool), "canUseLight");
            worldObjectTextWorldObject = AccessTools.Field(typeof(WorldObjectText), "worldObject");
            worldObjectColorWorldObject = AccessTools.Field(typeof(WorldObjectColor), "worldObject");
        }

        #region -Start Menu-
        [HarmonyPostfix]
        [HarmonyPatch(typeof(Intro), "Start")]
        static void Intro_Start()
        {
            LogInfo("Intro_Start");
            updateMode = MultiplayerMode.MainMenu;

            parent = new GameObject();
            Canvas canvas = parent.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            int fs = fontSize.Value;
            int dx = Screen.width / 2 - 200;
            int dy = Screen.height / 2 - 4 * (fs + 10) + 10;

            RectTransform rectTransform;

            // -------------------------

            var background = new GameObject();
            background.transform.parent = parent.transform;

            var img = background.AddComponent<Image>();
            img.color = new Color(0f, 0f, 0f, 0.95f);

            rectTransform = img.GetComponent<RectTransform>();
            rectTransform.localPosition = new Vector2(dx - 5, dy - 4 * (fs + 10) + 5);
            rectTransform.sizeDelta = new Vector2(300, 8 * (fs + 10) + 10);

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
                        LogInfo("Begin NAT Discovery");
                        var device = await discoverer.DiscoverDeviceAsync().ConfigureAwait(false);
                        LogInfo(device.ToString());
                        LogInfo("Begin Get External IP");
                        // The following hangs indefinitely, not sure why
                        var ip = await device.GetExternalIPAsync().ConfigureAwait(false);
                        LogInfo("External IP = " + ip);
                        externalIP = ip.ToString();
                    }
                    catch (Exception ex)
                    {
                        LogInfo(ex);
                        externalIP = "    External Address = error";
                    }
                });

                return "    External Address = checking";
            }
            return "    External Address = N/A";
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
                    hostModeCheckbox.GetComponent<Text>().text = GetHostModeString();
                    updateMode = MultiplayerMode.CoopClient;
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
        }

        #region - "Multiplayer Menu"
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

        #endregion - "Multiplayer Menu"

        #endregion -Start Menu-

        #region - Setup TCP -

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


        static void Send(object message)
        {
            if (otherPlayer != null)
            {
                _sendQueue.Enqueue(message);
            }
        }
        static void Signal()
        {
            _sendQueueBlock.Set();
        }
        static void StartAsHost()
        {
            stopNetwork = new CancellationTokenSource();
            Task.Factory.StartNew(HostAcceptor, stopNetwork.Token, TaskCreationOptions.LongRunning, TaskScheduler.Default);
        }

        static void StartAsClient()
        {
            NotifyUser("Connecting to Host...");
            stopNetwork = new CancellationTokenSource();
            Task.Run(() =>
            {
                LogInfo("Client connecting to " + hostAddress.Value + ":" + port.Value);
                try
                {
                    TcpClient client = new TcpClient(hostAddress.Value, port.Value);
                    NotifyUser("Connecting to Host...Success");
                    networkConnected = true;
                    stopNetwork.Token.Register(() =>
                    {
                        networkConnected = false;
                        client.Close();
                    });
                    Task.Factory.StartNew(SenderLoop, client, stopNetwork.Token, TaskCreationOptions.LongRunning, TaskScheduler.Default);
                    Task.Factory.StartNew(ReceiveLoop, client, stopNetwork.Token, TaskCreationOptions.LongRunning, TaskScheduler.Default);

                    _sendQueue.Enqueue(new MessageLogin
                    {
                        user = clientName.Value,
                        password = clientPassword.Value
                    });
                    Signal();
                }
                catch (Exception ex)
                {
                    LogError(ex);
                    NotifyUserFromBackground("Error: could not connect to Host");
                }
            });
        }

        static void HostAcceptor()
        {
            LogInfo("Starting HostAcceptor on port " + port.Value);
            try
            {
                TcpListener listener = new TcpListener(IPAddress.Any, port.Value);
                listener.Start();
                stopNetwork.Token.Register(() =>
                {
                    networkConnected = false;
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
                    LogInfo("Stopping HostAcceptor on port " + port.Value);
                }
            }
            catch (Exception ex)
            {
                if (!stopNetwork.IsCancellationRequested)
                {
                    LogError(ex);
                }
            }
        }

        static void ManageClient(TcpClient client)
        {
            if (clientConnected)
            {
                LogInfo("A client already connected");
                try
                {
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
                    LogError(ex);
                }
            }
            else
            {
                LogInfo("New Client from " + client.Client.RemoteEndPoint);
                clientConnected = true;
                _sendQueue.Clear();
                receiveQueue.Clear();
                Task.Factory.StartNew(SenderLoop, client, stopNetwork.Token, TaskCreationOptions.LongRunning, TaskScheduler.Default);
                Task.Factory.StartNew(ReceiveLoop, client, stopNetwork.Token, TaskCreationOptions.LongRunning, TaskScheduler.Default);
            }
        }

        static void SenderLoop(object clientObj)
        {
            LogInfo("SenderLoop begin");
            var client = (TcpClient)clientObj;
            try
            {
                try
                {
                    var stream = client.GetStream();
                    try
                    {
                        LogInfo("SenderLoop loop");
                        while (!stopNetwork.IsCancellationRequested)
                        {
                            if (_sendQueue.TryDequeue(out var message))
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
                                    default:
                                        {
                                            LogWarning("Can't serialize message: " + message);
                                            break;
                                        }
                                }
                            }
                            else
                            {
                                _sendQueueBlock.WaitOne(1000);
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
                LogInfo("SenderLoop stop");
            }
            catch (Exception ex)
            {
                if (!stopNetwork.IsCancellationRequested)
                {
                    LogError(ex);
                }
            }
            receiveQueue.Enqueue("Disconnected");
            networkConnected = false;
        }

        static void ReceiveLoop(object clientObj)
        {
            LogInfo("ReceiverLoop start");
            var client = (TcpClient)clientObj;
            try
            {
                try
                {
                    var stream = client.GetStream();
                    var reader = new StreamReader(stream, Encoding.UTF8, true, 64 * 1024);
                    try
                    {
                        LogInfo("ReceiverLoop loop");
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
                LogInfo("ReceiverLoop stop");
            }
            catch (Exception ex)
            {
                if (!stopNetwork.IsCancellationRequested)
                {
                    LogError(ex);
                }
            }
            receiveQueue.Enqueue("Disconnected");
            networkConnected = false;
        }

        static readonly byte[] ENoClientSlotBytes = Encoding.UTF8.GetBytes("ENoClientSlot\n");
        static readonly byte[] EAccessDenied = Encoding.UTF8.GetBytes("EAccessDenied\n");

        #endregion -Setup TCP-

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
            _sendQueue.Clear();
            receiveQueue.Clear();

            if (updateMode == MultiplayerMode.CoopHost)
            {
                LogInfo("Entering world as Host");
                StartAsHost();
            }
            else if (updateMode == MultiplayerMode.CoopClient)
            {
                LogInfo("Entering world as Client");
                // we don't need the savefile
                File.Delete(Application.persistentDataPath + "/" + multiplayerFilename + ".json");
                StartAsClient();
            }
            else
            {
                LogInfo("Entering world as Solo");
            }
        }

        static void SetupInitialInventory()
        {
            LogInfo("SetupInitialInventory");
            var inv = InventoriesHandler.GetInventoryById(shadowInventoryId);
            Dictionary<string, int> itemsToAdd = new()
            {
                {
                    "MultiBuild",
                    1
                },
                {
                    "MultiDeconstruct",
                    1
                },
                {
                    "MultiToolLight",
                    1
                },
                {
                    "Iron",
                    10
                },
                {
                    "Magnesium",
                    10
                },
                {
                    "Silicon",
                    10
                },
                {
                    "Titanium",
                    10
                },
                {
                    "Cobalt",
                    10
                },
                {
                    "BlueprintT1",
                    10
                },
                {
                    "OxygenCapsule1",
                    10
                }
                ,
                {
                    "WaterBottle1",
                    10
                },
                {
                    "astrofood",
                    10
                }
            };

            foreach (var kv in itemsToAdd)
            {
                var gr = GroupsHandler.GetGroupViaId(kv.Key);
                if (gr != null)
                {
                    for (int i = 0; i < kv.Value; i++)
                    {
                        var wo = WorldObjectsHandler.CreateNewWorldObject(gr);
                        inv.AddItem(wo);
                    }
                }
                else
                {
                    LogWarning("SetupInitialInventory: Unknown groupId " + kv.Key);
                }
            }
        }
        static bool TryGetGameObject(WorldObject wo, out GameObject go)
        {
            return gameObjectByWorldObject.TryGetValue(wo, out go);
        }
        static bool TryRemoveGameObject(WorldObject wo)
        {
            return gameObjectByWorldObject.Remove(wo);
        }

        #region - Disable Consumption -

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

        #endregion - Disable Consumption -

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
                        LogInfo("Mined: " + worldObject.GetId() + " of " + worldObject.GetGroup().GetId() + " at " + worldObject.GetPosition());
                        Send("Mined|" + worldObject.GetId() + "\n");
                        Signal();
                    }
                }
            }
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(UiWindowPause), nameof(UiWindowPause.OnQuit))]
        static void UiWindowPause_OnQuit()
        {
            stopNetwork?.Cancel();
            Signal();
            updateMode = MultiplayerMode.MainMenu;
            otherPlayer?.Destroy();
            otherPlayer = null;
            _sendQueue.Clear();
            receiveQueue.Clear();
            worldObjectById.Clear();
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(UiWindowTextInput), nameof(UiWindowTextInput.OnChangeText))]
        static bool UiWindowTextInput_OnChangeText(TMP_InputField ___inputField, WorldObjectText ___worldObjectText)
        {
            if (updateMode != MultiplayerMode.SinglePlayer)
            {
                WorldObject wo = (WorldObject)worldObjectTextWorldObject.GetValue(___worldObjectText);
                var mut = new MessageUpdateText()
                {
                    id = wo.GetId(),
                    text = ___inputField.text
                };
                Send(mut);
                Signal();
                // Do not change the client but wait for the server update
                return updateMode != MultiplayerMode.CoopClient;
            }
            return true;
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(UiWindowColorPicker), nameof(UiWindowColorPicker.SetColorWorldObject))]
        static bool UiWindowColorPicker_SetColorWorldObject(ColorPicker ___colorPicker, 
            WorldObjectColor _worldObjectColor)
        {
            if (updateMode != MultiplayerMode.SinglePlayer)
            {
                ___colorPicker.onValueChanged.RemoveAllListeners();
                ___colorPicker.onValueChanged.AddListener(color =>
                {
                    color.a = 1f;
                    // Do not change the client but wait for the server update
                    if (updateMode != MultiplayerMode.CoopClient)
                    {
                        _worldObjectColor.SetColor(color);
                    }

                    WorldObject wo = (WorldObject)worldObjectColorWorldObject.GetValue(_worldObjectColor);
                    var mut = new MessageUpdateColor()
                    {
                        id = wo.GetId(),
                        color = color
                    };
                    Send(mut);
                    Signal();
                });
                return false;
            }
            return true;
        }

        #region - Patch WorldObjectsHandler -

        [HarmonyPostfix]
        [HarmonyPatch(typeof(WorldObjectsHandler), "StoreNewWorldObject")]
        static void WorldObjectsHandler_StoreNewWorldObject(WorldObject _worldObject)
        {
            worldObjectById[_worldObject.GetId()] = _worldObject;
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(WorldObjectsHandler), nameof(WorldObjectsHandler.DestroyWorldObject))]
        static void WorldObjectsHandler_DestroyWorldObject(WorldObject _worldObject)
        {
            var id = _worldObject.GetId();
            if (worldObjectById.ContainsKey(id) && 
                !WorldObjectsIdHandler.IsWorldObjectFromScene(id))
            {
                worldObjectById.Remove(id);
            }
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(WorldObjectsHandler), nameof(WorldObjectsHandler.GetWorldObjectViaId))]
        static bool WorldObjectsHandler_GetWorldObjectViaId(int _id, ref WorldObject __result)
        {
            worldObjectById.TryGetValue(_id, out __result);
            return false;
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(WorldObjectsHandler), nameof(WorldObjectsHandler.SetAllWorldObjects))]
        static void WorldObjectsHandler_SetAllWorldObjects(List<WorldObject> _allWorldObjects)
        {
            worldObjectById.Clear();
            foreach (WorldObject wo in _allWorldObjects)
            {
                worldObjectById[wo.GetId()] = wo;
            }
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(WorldObjectsHandler), nameof(WorldObjectsHandler.DropOnFloor))]
        static void WorldObjectsHandler_DropOnFloor(WorldObject _worldObject, 
            Vector3 _position, float _dropSize = 0f)
        {
            if (updateMode == MultiplayerMode.CoopHost)
            {
                LogInfo("Dropping: " + DebugWorldObject(_worldObject));
                SendWorldObject(_worldObject);
                if (TryGetGameObject(_worldObject, out var go))
                {
                    go.GetComponent<WorldObjectAssociated>()
                    .StartCoroutine(WorldObjectsHandler_DropOnFloor_Tracker(_worldObject));
                }
            }
            else
            if (updateMode == MultiplayerMode.CoopClient)
            {
                // Let the host animate it
                if (TryGetGameObject(_worldObject, out var go)) {
                    UnityEngine.Object.Destroy(go);
                    _worldObject.ResetPositionAndRotation();
                }

                LogInfo("Dropping: " + DebugWorldObject(_worldObject));
                MessageDropWorldObject msg = new MessageDropWorldObject()
                {
                    id = _worldObject.GetId(),
                    position = _position,
                    dropSize = _dropSize
                };
                Send(msg);
                Signal();
            }
        }

        static IEnumerator WorldObjectsHandler_DropOnFloor_Tracker(WorldObject _worldObject)
        {
            float delay = 0.5f / networkFrequency.Value; // sample twice the network frequency
            int maxLoop = (int)(26 / delay);
            for (int i = 0; i < maxLoop; i++)
            {
                if (TryGetGameObject(_worldObject, out var go))
                {
                    if (go != null && _worldObject.GetIsPlaced())
                    {
                        var messageSetTransform = new MessageSetTransform()
                        {
                            id = _worldObject.GetId(),
                            position = go.transform.position,
                            rotation = go.transform.rotation,
                            mode = MessageSetTransform.Mode.GameObjectOnly
                        };
                        Send(messageSetTransform);
                        Signal();
                    }
                    else
                    {
                        yield break;
                    }
                }
                else
                {
                    yield break;
                }
                yield return new WaitForSeconds(delay);
            }

        }

        #endregion - Patch WorldObjectsHandler -

        [HarmonyPrefix]
        [HarmonyPatch(typeof(WorldObjectsIdHandler), nameof(WorldObjectsIdHandler.GetNewWorldObjectIdForDb))]
        static bool WorldObjectsIdHandler_GetNewWorldObjectIdForDb(ref int __result)
        {
            if (updateMode == MultiplayerMode.CoopClient)
            {
                int randomId = 300000000 + UnityEngine.Random.Range(1000000, 9999999);
                int max = -1;
                bool duplicate = false;
                foreach (WorldObject wo in WorldObjectsHandler.GetAllWorldObjects())
                {
                    int id = wo.GetId();
                    if (id == randomId)
                    {
                        duplicate = true;
                    }
                    max = Math.Max(max, id);
                }
                if (duplicate)
                {
                    __result = max + 1;
                }
                else
                {
                    __result = randomId;
                }
                return false;
            }
            return true;
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(Inventory), nameof(Inventory.AddItem))]
        static void Inventory_AddItem(bool __result, List<WorldObject> ___worldObjectsInInventory, 
            int ___inventoryId, WorldObject _worldObject)
        {
            if (__result && updateMode != MultiplayerMode.SinglePlayer && !suppressInventoryChange)
            {
                int iid = ___inventoryId;

                if (updateMode == MultiplayerMode.CoopHost)
                {
                    if (!TryConvertHostToClientInventoryId(iid, out iid))
                    {
                        return;
                    }
                }
                var mia = new MessageInventoryAdded()
                {
                    inventoryId = iid,
                    itemId = _worldObject.GetId(),
                    groupId = _worldObject.GetGroup().GetId()
                };
                LogInfo("InventorAddItem: " + iid + ", " + _worldObject.GetId() + ", " + _worldObject.GetGroup().GetId());
                Send(mia);
                Signal();

                // remove from the client's inventory so that only the Host can add it back
                if (updateMode == MultiplayerMode.CoopClient)
                {
                    ___worldObjectsInInventory.Remove(_worldObject);
                }
            }
        }

        static bool TryConvertHostToClientInventoryId(int hostInventoryId, out int clientInventoryId)
        {
            // Ignore the Host's own backpack and equipment
            if (hostInventoryId == 1 || hostInventoryId == 2)
            {
                clientInventoryId = 0;
                return false;
            }
            // If it is the shadow inventory/equipment, retarget it to the standard ids for the client
            if (hostInventoryId == shadowInventoryId)
            {
                clientInventoryId = 1;
            }
            else
            if (hostInventoryId == shadowEquipmentId)
            {
                clientInventoryId = 2;
            }
            else
            {
                clientInventoryId = hostInventoryId;
            }
            return true;
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(Inventory), nameof(Inventory.RemoveItem))]
        static bool Inventory_RemoveItem_Pre(int ___inventoryId, WorldObject _worldObject, bool _destroyWorldObject)
        {
            if (updateMode == MultiplayerMode.CoopClient && !suppressInventoryChange)
            {
                var mir = new MessageInventoryRemoved()
                {
                    inventoryId = ___inventoryId,
                    itemId = _worldObject.GetId(),
                    destroy = _destroyWorldObject
                };
                LogInfo("InventoryRemoveItemPre: " + ___inventoryId + ", " + _worldObject.GetId() + ", " + _worldObject.GetGroup().GetId());
                Send(mir);
                Signal();
                return false;
            }
            return true;
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(Inventory), nameof(Inventory.RemoveItem))]
        static void Inventory_RemoveItem(int ___inventoryId, WorldObject _worldObject, bool _destroyWorldObject)
        {
            if (updateMode == MultiplayerMode.CoopHost && !suppressInventoryChange)
            {
                int iid = ___inventoryId;
                if (updateMode == MultiplayerMode.CoopHost)
                {
                    if (!TryConvertHostToClientInventoryId(iid, out iid))
                    {
                        return;
                    }
                }

                var mir = new MessageInventoryRemoved()
                {
                    inventoryId = iid,
                    itemId = _worldObject.GetId(),
                    destroy = _destroyWorldObject
                };
                LogInfo("InventoryRemoveItemPost: " + iid + ", " + _worldObject.GetId() + ", " + _worldObject.GetGroup().GetId());
                Send(mir);
                Signal();
            }
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(Inventory), nameof(Inventory.AutoSort))]
        static void Inventory_AutoSort(int ___inventoryId)
        {
            if (!suppressInventoryChange)
            {
                var msi = new MessageSortInventory()
                {
                    inventoryId = ___inventoryId
                };
                Send(msi);
                Signal();
            }
        }

        static bool cancelBuildAfterPlace;

        [HarmonyPrefix]
        [HarmonyPatch(typeof(ConstructibleGhost), nameof(ConstructibleGhost.Place))]
        static bool ConstructibleGhost_Place(ConstructibleGhost __instance, 
            ref GameObject __result, GroupConstructible ___groupConstructible)
        {
            cancelBuildAfterPlace = false;
            if (updateMode == MultiplayerMode.CoopClient)
            {
                bool positioningStatus = __instance.gameObject.GetComponent<GhostPlacementChecker>().GetPositioningStatus();
                if (positioningStatus)
                {
                    ConstraintSamePanel component = __instance.gameObject.GetComponent<ConstraintSamePanel>();
                    if (component != null)
                    {
                        var aimPanel = component.GetAimedAtPanel();
                        var newPanelType = component.GetAssociatedSubPanelType();
                        var assoc = aimPanel.GetWorldObjectAssociated();

                        int idx = 0;
                        var panels = assoc.gameObject.GetComponentsInChildren<Panel>();
                        foreach (Panel panel in panels)
                        {
                            if (panel == aimPanel)
                            {

                                MessagePanelChanged pc = new MessagePanelChanged()
                                {
                                    itemId = assoc.GetWorldObject().GetId(),
                                    panelId = idx,
                                    panelType = (int)newPanelType,
                                    panelGroupId = ___groupConstructible.GetId()
                                };
                                PlayBuildGhost();
                                LogInfo("Place: Change Panel " + pc.itemId + ", " + idx + ", " + newPanelType);
                                Send(pc);
                                Signal();
                                break;
                            }
                            idx++;
                        }
                        if (idx == panels.Length)
                        {
                            LogWarning("Place: Panel not found");
                        }
                    }
                    else
                    {
                        var mpc = new MessagePlaceConstructible()
                        {
                            groupId = ___groupConstructible.GetId(), 
                            position = __instance.gameObject.transform.position, 
                            rotation = __instance.gameObject.transform.rotation
                        };
                        PlayBuildGhost();
                        LogInfo("Place: Construct " + mpc.groupId + "; " + mpc.position + "; " + mpc.rotation);
                        Send(mpc);
                        Signal();
                    }
                    __result = null;
                    cancelBuildAfterPlace = true;
                    return false;
                }
            }
            return true;
        }
        [HarmonyPostfix]
        [HarmonyPatch(typeof(ConstructibleGhost), nameof(ConstructibleGhost.Place))]
        static void ConstructibleGhost_Place_Post(ConstructibleGhost __instance,
            GameObject __result, GroupConstructible ___groupConstructible)
        {
            if (updateMode == MultiplayerMode.CoopHost)
            {
                if (__result != null)
                {
                    var woa = __result.GetComponent<WorldObjectAssociated>();
                    if (woa == null)
                    {
                        woa = __result.GetComponentInParent<WorldObjectAssociated>();
                    }
                    if (woa != null)
                    {
                        var wo = woa.GetWorldObject();
                        if (wo != null)
                        {
                            SendWorldObject(wo);
                            return;
                        }
                    }
                    LogWarning("Place: WorldObjectAssociated not found");
                }
            }
        }

        static PlayerMainController GetPlayerMainController()
        {
            PlayersManager p = Managers.GetManager<PlayersManager>();
            if (p != null)
            {
                return p.GetActivePlayerController();
            }
            return null;
        }

        static void PlayBuildGhost()
        {
            PlayerMainController pm = GetPlayerMainController();
            if (pm != null)
            {
                pm.GetPlayerAudio().PlayBuildGhost();

                var anims = pm.GetAnimations();
                anims.AnimateConstruct(true);
                anims.StartCoroutine(StopConstructAnim(anims));
            }
        }

        static IEnumerator StopConstructAnim(PlayerAnimations pa)
        {
            yield return new WaitForSeconds(0.5f);
            pa.AnimateConstruct(false);
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(PlayerBuilder), nameof(PlayerBuilder.InputOnAction))]
        static void PlayerBuilder_InputOnAction(PlayerBuilder __instance, 
            bool _isPressingAccessibilityKey, ref ConstructibleGhost ___ghost)
        {
            if (cancelBuildAfterPlace && !_isPressingAccessibilityKey)
            {
                __instance.InputOnCancelAction();
                ___ghost = null;
                cancelBuildAfterPlace = false;
            }
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(DoorsHandler), "Start")]
        static void DoorsHandler_Start(DoorsHandler __instance, Door ___door1, Door ___door2)
        {
            __instance.StartCoroutine(DoorsReaction(___door1, ___door2));
        }

        static IEnumerator DoorsReaction(Door door1, Door door2)
        {
            bool entered = false;
            var d1 = door1.transform.position;
            var d2 = door2.transform.position;
            var d3 = d1 + (d2 - d1) / 2;
            for (; ; )
            {
                if (otherPlayer != null)
                {
                    var pos = otherPlayer.avatar.transform.position;


                    var distance = 2f;

                    if (Vector3.Distance(pos, d3) <= distance)
                    {
                        if (!entered)
                        {
                            entered = true;
                            door1.OpenDoor();
                            door2.OpenDoor();
                        }
                    }
                    else
                    if (entered)
                    {
                        entered = false;
                        door1.CloseDoor();
                        door2.CloseDoor();
                    }
                } else
                {
                    if (entered)
                    {
                        entered = false;
                        door1.CloseDoor();
                        door2.CloseDoor();
                    }
                }
                yield return new WaitForSeconds(0.05f);
            }
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(UiWindowBlueprints), nameof(UiWindowBlueprints.DecodeBlueprint))]
        static bool UiWindowBlueprints_DecodeBlueprint(UiWindowBlueprints __instance, Group ___groupChip)
        {
            if (updateMode != MultiplayerMode.SinglePlayer)
            {
                __instance.containerNeverUnlocked.SetActive(false);
                __instance.containerList.SetActive(false);
                if (updateMode == MultiplayerMode.CoopClient)
                {
                    Send(new MessageMicrochipUnlock());
                    Signal();
                }
                else
                {
                    var unlocked = Managers.GetManager<UnlockingHandler>().GetUnlockableGroupAndUnlock();
                    if (unlocked != null)
                    {
                        Managers.GetManager<UnlockingHandler>().PlayAudioOnDecode();
                        GetPlayerMainController().GetPlayerBackpack().GetInventory()
                            .RemoveItems(new List<Group> { ___groupChip }, true, true);

                        Send(new MessageMicrochipUnlock()
                        {
                            groupId = unlocked.GetId()
                        });
                        Signal();
                    }
                    else
                    {
                        Managers.GetManager<BaseHudHandler>().DisplayCursorText("UI_warn_no_more_chip_to_unlock", 3f, "");
                    }
                }
                __instance.CloseAll();
                return false;
            }
            return true;
        }

        void OnApplicationQuit()
        {
            LogInfo("Application quit");
            stopNetwork?.Cancel();
            Signal();
            for (int i = 0; i < 20 && networkConnected; i++)
            {
                Thread.Sleep(100);
            }
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
                /* */
            }
        }

        void CreateMultiplayerSaveAndEnter()
        {
            File.Delete(Application.persistentDataPath + "/" + multiplayerFilename);

            Managers.GetManager<StaticDataHandler>().LoadStaticData();

            // avoid random positioning
            List<PositionAndRotation> backupStartingPositions = new();
            backupStartingPositions.AddRange(GameConfig.positionsForEscapePod);
            try
            {
                GameConfig.positionsForEscapePod.RemoveRange(1, GameConfig.positionsForEscapePod.Count - 1);
                JSONExport.CreateNewSaveFile(multiplayerFilename, DataConfig.GameSettingMode.Chill, DataConfig.GameSettingStartLocation.Standard);
            }
            finally
            {
                GameConfig.positionsForEscapePod.Clear();
                GameConfig.positionsForEscapePod.AddRange(backupStartingPositions);
            }

            Managers.GetManager<SavedDataHandler>().SetSaveFileName(multiplayerFilename);
            SceneManager.LoadScene("OpenWorldTest");

            LogInfo("Find SaveFilesSelector");
            var selector = UnityEngine.Object.FindObjectOfType<SaveFilesSelector>();
            if (selector != null)
            {
                selector.gameObject.SetActive(false);

                LogInfo("Find ShowLoading");
                var mi = AccessTools.Method(typeof(SaveFilesSelector), "ShowLoading", new Type[] { typeof(bool) });
                mi.Invoke(selector, new object[] { true });
            }
            else
            {
                LogInfo("SaveFilesSelector not found");
            }

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
                    try
                    {
                        DispatchMessage(message);
                    }
                    catch (Exception ex)
                    {
                        LogError(ex);
                    }
                }
            }
        }

        void SendPlayerLocation()
        {
            PlayerMainController pm = GetPlayerMainController();
            if (pm != null)
            {
                Transform player = pm.transform;
                MessagePlayerPosition mpp = new MessagePlayerPosition
                {
                    position = player.position,
                    rotation = player.rotation
                };
                Send(mpp);
                Signal();
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
                        LogInfo("User login success: " + ml.user);
                        NotifyUser("User joined: " + ml.user);
                        otherPlayer?.Destroy();
                        Color color = Color.white;
                        try
                        {
                            color = MessageHelper.StringToColor(clientColor.Value);
                        }
                        catch (Exception)
                        {

                        }
                        PrepareHiddenChests();
                        otherPlayer = PlayerAvatar.CreateAvatar(color, false);
                        Send("Welcome\n");
                        Signal();
                        lastHostSync = Time.realtimeSinceStartup;
                        SendFullState();
                        return;
                    }
                }

                LogInfo("User login failed: " + ml.user);
                _sendQueue.Enqueue(EAccessDenied);
                Signal();
            }
        }

        static void PrepareHiddenChests()
        {
            // The other player's shadow inventory
            if (TryPrepareHiddenChest(shadowInventoryWorldId, ref shadowInventoryId))
            {
                SetupInitialInventory();
            }
            TryPrepareHiddenChest(shadowEquipmentWorldId, ref shadowEquipmentId);
        }

        static bool TryPrepareHiddenChest(int id, ref int inventoryId)
        {
            WorldObject wo = WorldObjectsHandler.GetWorldObjectViaId(id);
            if (wo == null)
            {
                LogInfo("Creating special inventory " + id);

                wo = WorldObjectsHandler.CreateNewWorldObject(GroupsHandler.GetGroupViaId("GROUP_DESC_Container2"), id);
                wo.SetPositionAndRotation(new Vector3(-500, -500, -450), Quaternion.identity);
                WorldObjectsHandler.InstantiateWorldObject(wo, false);
                Inventory inv = InventoriesHandler.CreateNewInventory(1000, 0);
                int invId = inv.GetId();
                inventoryId = invId;
                wo.SetLinkedInventoryId(invId);
                return true;
            }
            return false;
        }

        static void ReceiveMessageInventoryAdded(MessageInventoryAdded mia)
        {
            int targetId = mia.inventoryId;
            if (targetId == 1 && shadowInventoryId != 0)
            {
                targetId = shadowInventoryId;
            }
            else
            if (targetId == 2 && shadowEquipmentId != 0)
            {
                targetId = shadowEquipmentId;
            }
            if (targetId == 2 && updateMode == MultiplayerMode.CoopClient)
            {

                suppressInventoryChange = true;
                try
                {
                    WorldObject wo = WorldObjectsHandler.GetWorldObjectViaId(mia.itemId);
                    if (wo != null)
                    {
                        var _playerController = GetPlayerMainController();
                        _playerController.GetPlayerEquipment()
                            .AddItemInEquipment(wo);
                        LogError("ReceiveMessageInventoryAdded: Add Equipment " + mia.itemId + ", " + mia.groupId);

                        _playerController.GetPlayerEquipment()
                            .GetInventory()
                            .RefreshDisplayerContent();
                    }
                    else
                    {
                        LogError("ReceiveMessageInventoryAdded: Add Equipment missing WorldObject " + mia.itemId + ", " + mia.groupId);
                    }
                } 
                finally
                {
                    suppressInventoryChange = false;
                }
                return;
            }
            var inv = InventoriesHandler.GetInventoryById(targetId);
            if (inv != null)
            {
                WorldObject wo = WorldObjectsHandler.GetWorldObjectViaId(mia.itemId);
                if (wo == null)
                {
                    wo = WorldObjectsHandler.CreateNewWorldObject(GroupsHandler.GetGroupViaId(mia.groupId), mia.itemId);
                }
                suppressInventoryChange = updateMode == MultiplayerMode.CoopClient;
                try
                {
                    inv.AddItem(wo);
                    LogInfo("ReceiveMessageInventoryAdded: " + mia.inventoryId + " <= " + mia.itemId + ", " + mia.groupId);
                    inv.RefreshDisplayerContent();
                } 
                finally
                {
                    suppressInventoryChange = false;
                }
            }
            else
            {
                LogInfo("ReceiveMessageInventoryAdded: Uknown inventory " + mia.inventoryId + " <= " + mia.itemId + ", " + mia.groupId);
            }
        }

        static void ReceiveMessageInventoryRemoved(MessageInventoryRemoved mia)
        {
            LogInfo("ReceiveMessageInventoryRemoved - Begin");
            int targetId = mia.inventoryId;
            if (targetId == 1 && shadowInventoryId != 0)
            {
                targetId = shadowInventoryId;
            }
            else
            if (targetId == 2 && shadowEquipmentId != 0)
            {
                targetId = shadowEquipmentId;
            }
            if (targetId == 2 && updateMode == MultiplayerMode.CoopClient)
            {

                suppressInventoryChange = true;
                try
                {
                    WorldObject wo = WorldObjectsHandler.GetWorldObjectViaId(mia.itemId);
                    if (wo != null)
                    {
                        var _playerController = GetPlayerMainController();
                        _playerController.GetPlayerEquipment()
                            .RemoveItemFromEquipment(wo);
                        LogInfo("ReceiveMessageInventoryRemoved: Remove Equipment " + mia.itemId + ", " + wo.GetGroup().GetId());
                    }
                    else
                    {
                        LogError("ReceiveMessageInventoryRemoved: Remove Equipment missing WorldObject " + mia.itemId);
                    }
                }
                finally
                {
                    suppressInventoryChange = false;
                }
            }
            else
            {
                var inv = InventoriesHandler.GetInventoryById(targetId);
                if (inv != null)
                {
                    WorldObject wo = WorldObjectsHandler.GetWorldObjectViaId(mia.itemId);
                    if (wo != null)
                    {
                        suppressInventoryChange = updateMode == MultiplayerMode.CoopClient;
                        try
                        {
                            inv.RemoveItem(wo, mia.destroy);
                            LogInfo("ReceiveMessageInventoryRemoved: " + mia.inventoryId + " <= " + mia.itemId + ", " + wo.GetGroup().GetId());
                        }
                        finally
                        {
                            suppressInventoryChange = false;
                        }
                    }
                    else
                    {
                        LogWarning("ReceiveMessageInventoryRemoved: Unknown item " + mia.inventoryId + " <= " + mia.itemId);
                    }
                }
                else
                {
                    LogWarning("ReceiveMessageInventoryRemoved: Unknown inventory " + mia.inventoryId + " <= " + mia.itemId);
                }
            }
            LogInfo("ReceiveMessageInventoryRemoved - End");
        }

        static void SendFullState()
        {
            //LogInfo("Begin syncing the entire game state to the client");

            // =========================================================

            MessageUnlocks mu = new MessageUnlocks();
            List<Group> grs = GroupsHandler.GetUnlockedGroups();
            mu.groupIds = new List<string>(grs.Count + 1);
            foreach (Group g in grs) {
                mu.groupIds.Add(g.GetId());
            }
            _sendQueue.Enqueue(mu);

            // =========================================================

            MessageTerraformState mts = new MessageTerraformState();

            WorldUnitsHandler wuh = Managers.GetManager<WorldUnitsHandler>();
            mts.oxygen = wuh.GetUnit(DataConfig.WorldUnitType.Oxygen).GetValue();
            mts.heat = wuh.GetUnit(DataConfig.WorldUnitType.Heat).GetValue();
            mts.pressure = wuh.GetUnit(DataConfig.WorldUnitType.Pressure).GetValue();
            mts.biomass = wuh.GetUnit(DataConfig.WorldUnitType.Biomass).GetValue();

            _sendQueue.Enqueue(mts);

            // =========================================================

            StringBuilder sb = new StringBuilder();
            sb.Append("AllObjects");
            foreach (WorldObject wo in WorldObjectsHandler.GetAllWorldObjects())
            {
                if (!wo.GetDontSaveMe())
                {
                    int id = wo.GetId();
                    if (id != shadowInventoryWorldId && id != shadowEquipmentWorldId)
                    {
                        sb.Append("|");
                        MessageAllObjects.AppendWorldObject(sb, ';', wo);
                        //LogInfo("FullSync> " + DebugWorldObject(wo));
                    }
                }
            }
            sb.Append('\n');
            _sendQueue.Enqueue(sb.ToString());

            // =========================================================

            sb = new StringBuilder();
            sb.Append("Inventories");

            // Send player equimpent first

            Inventory inv = InventoriesHandler.GetInventoryById(shadowEquipmentId);
            sb.Append("|");
            MessageInventories.Append(sb, inv, 2);

            // Send player inventory next

            inv = InventoriesHandler.GetInventoryById(shadowInventoryId);
            sb.Append("|");
            MessageInventories.Append(sb, inv, 1);

            // Send all the other inventories after

            foreach (Inventory inv2 in InventoriesHandler.GetAllInventories())
            {
                int id = inv2.GetId();
                // Ignore Host's own inventory/equipment
                if (id != 1 && id != 2 && id != shadowInventoryId && id != shadowEquipmentId)
                {
                    sb.Append("|");
                    MessageInventories.Append(sb, inv2, id);
                }
            }
            sb.Append('\n');
            _sendQueue.Enqueue(sb.ToString());

            // -----------------------------------------------------

            Signal();
        }

        static void UpdateWorldObject(MessageWorldObject mwo)
        {
            bool isNew = false;
            if (!worldObjectById.TryGetValue(mwo.id, out var wo))
            {
                Group gr = GroupsHandler.GetGroupViaId(mwo.groupId);
                wo = WorldObjectsHandler.CreateNewWorldObject(gr, mwo.id);
                LogInfo("UpdateWorldObject: Creating new WorldObject " + mwo.id + " - " + mwo.groupId);
                isNew = true;
            }
            bool wasPlaced = wo.GetIsPlaced();
            var oldPosition = wo.GetPosition();
            var oldRotation = wo.GetRotation();
            var oldColor = wo.GetColor();
            var oldText = wo.GetText();

            wo.SetPositionAndRotation(mwo.position, mwo.rotation);
            bool doPlace = wo.GetIsPlaced();
            wo.SetColor(mwo.color);
            wo.SetText(mwo.text);
            wo.SetGrowth(mwo.growth);

            List<int> beforePanelIds = wo.GetPanelsId();
            bool doUpdatePanels = (beforePanelIds == null && mwo.panelIds.Count != 0) || (beforePanelIds != null && !beforePanelIds.SequenceEqual(mwo.panelIds));
            wo.SetPanelsId(mwo.panelIds);
            wo.SetDontSaveMe(false);

            List<Group> groups = new List<Group>();
            foreach (var gid in mwo.groupIds)
            {
                groups.Add(GroupsHandler.GetGroupViaId(gid));
            }
            wo.SetLinkedGroups(groups);

            if (mwo.inventoryId > 0)
            {
                wo.SetLinkedInventoryId(mwo.inventoryId);
                Inventory inv = InventoriesHandler.GetInventoryById(mwo.inventoryId);
                if (inv == null)
                {
                    LogInfo("Creating default inventory " + mwo.inventoryId 
                        + " of WorldObject " + DebugWorldObject(wo));
                    InventoriesHandler.CreateNewInventory(1, mwo.inventoryId);
                }
            }
            else
            {
                wo.SetLinkedInventoryId(0);
            }

            bool hasGameObject = TryGetGameObject(wo, out var go) && go != null;

            bool dontUpdatePosition = false;
            if (!wasPlaced && doPlace)
            {
                WorldObjectsHandler.InstantiateWorldObject(wo, true);
                LogInfo("UpdateWorldObject: Placing GameObject for WorldObject " + DebugWorldObject(wo));
                dontUpdatePosition = true;
            }
            else
            if (wasPlaced && !doPlace)
            {
                if (hasGameObject)
                {
                    LogInfo("UpdateWorldObject: WorldObject " + wo.GetId() + " GameObject destroyed: not placed");
                    UnityEngine.Object.Destroy(go);
                    TryRemoveGameObject(wo);
                }
                /*
                else
                {
                    LogInfo("WorldObject " + wo.GetId() + " has no associated GameObject");
                }
                */
            }
            if (doPlace && !dontUpdatePosition)
            {
                if (IsChanged(oldPosition, mwo.position) || IsChanged(oldRotation, mwo.rotation))
                {
                    if (hasGameObject)
                    {
                        if (go != null)
                        {
                            LogInfo("UpdateWorldObject: Placement " + wo.GetId() + ", " + wo.GetGroup().GetId() + ", position=" + mwo.position + ", rotation=" + mwo.rotation);
                            go.transform.position = mwo.position;
                            go.transform.rotation = mwo.rotation;
                        }
                    }
                }
            }
            if (doUpdatePanels)
            {
                LogInfo("UpdateWorldObject: Panels " + wo.GetId() + ", " + wo.GetGroup().GetId());
                UpdatePanelsOn(wo);
            }
            if (!string.Equals(oldText, mwo.text) && hasGameObject)
            {
                var wot = go.GetComponentInChildren<WorldObjectText>();
                if (wot != null)
                {
                    wot.SetText(mwo.text);
                }
            }
            if (!IsChanged(oldColor, mwo.color) && hasGameObject)
            {
                var woc = go.GetComponentInChildren<WorldObjectColor>();
                if (woc != null)
                {
                    woc.SetColor(mwo.color);
                }
            }
            // remove already mined objects
            if (isNew && WorldObjectsIdHandler.IsWorldObjectFromScene(wo.GetId()) && !doPlace)
            {
                DeleteActionMinableForId(wo.GetId());
            }
        }

        static float epsilon = 0.001f;

        static bool IsChanged(Vector3 vec1, Vector3 vec2)
        {
            var v3 = vec1 - vec2;
            return Mathf.Abs(v3.x) > epsilon
                || Mathf.Abs(v3.y) > epsilon
                || Mathf.Abs(v3.z) > epsilon;
        }

        static bool IsChanged(Quaternion q1, Quaternion q2)
        {
            return Mathf.Abs(q1.x - q2.x) > epsilon
                || Mathf.Abs(q1.y - q2.y) > epsilon
                || Mathf.Abs(q1.z - q2.z) > epsilon
                || Mathf.Abs(q1.w - q2.w) > epsilon;
        }
        static bool IsChanged(Color q1, Color q2)
        {
            return Mathf.Abs(q1.r - q2.r) > epsilon
                || Mathf.Abs(q1.g - q2.g) > epsilon
                || Mathf.Abs(q1.b - q2.b) > epsilon
                || Mathf.Abs(q1.a - q2.a) > epsilon;
        }

        static bool DeleteActionMinableForId(int id)
        {
            foreach (ActionMinable am in UnityEngine.Object.FindObjectsOfType<ActionMinable>())
            {
                var woa = am.GetComponent<WorldObjectAssociated>();
                if (woa != null)
                {
                    var wo1 = woa.GetWorldObject();
                    if (wo1 != null)
                    {
                        if (wo1.GetId() == id)
                        {

                            LogInfo("DeleteActionMinableForId: ActionMinable deleted: " + id);
                            UnityEngine.Object.Destroy(am.gameObject);
                            return true;
                        }
                    }
                }
            }


            LogWarning("DeleteActionMinableForId: ActionMinable or WorldObjectAssociated found for " + id);
            return false;
        }

        static void UpdatePanelsOn(WorldObject wo)
        {
            if (TryGetGameObject(wo, out var go))
            {
                var panelIds = wo.GetPanelsId();
                if (panelIds != null && panelIds.Count > 0)
                {
                    Panel[] componentsInChildren = go.GetComponentsInChildren<Panel>();
                    int num = 0;
                    foreach (Panel panel in componentsInChildren)
                    {
                        if (num < panelIds.Count)
                        {
                            try
                            {
                                DataConfig.BuildPanelSubType subPanelType = (DataConfig.BuildPanelSubType)panelIds[num];
                                panel.ChangePanel(subPanelType);
                            }
                            catch (Exception ex)
                            {
                                LogError(ex);
                            }
                        }
                        num++;
                    }
                    LogInfo("UpdatePanelsOn: Updating panels on " + wo.GetId() + " success | " + panelIds);
                }
                else
                {
                    LogInfo("UpdatePanelsOn: Updating panels: No panel details on " + wo.GetId());
                }
            }
            else
            {
                LogInfo("UpdatePanelsOn: Updating panels: Game object not found of " + wo.GetId());
            }
        }

        static void ReceiveMessageAllObjects(MessageAllObjects mc)
        {
            if (updateMode == MultiplayerMode.CoopClient)
            {
                // LogInfo("Received all constructs: " + mc.worldObjects.Count);
                HashSet<int> toDelete = new HashSet<int>();

                foreach (var kv in worldObjectById)
                {
                    toDelete.Add(kv.Key);
                }                

                foreach (MessageWorldObject mwo in mc.worldObjects)
                {
                    //LogInfo("WorldObject " + mwo.id + " - " + mwo.groupId + " at " + mwo.position);
                    toDelete.Remove(mwo.id);

                    UpdateWorldObject(mwo);
                }

                foreach (int id in toDelete)
                {
                    //LogInfo("WorldObject " + id + " destroyed: " + DebugWorldObject(id));
                    if (worldObjectById.TryGetValue(id, out var wo))
                    {
                        if (TryGetGameObject(wo, out var go))
                        {
                            LogInfo("WorldObject " + id + " GameObject destroyed: no longer exists");
                            UnityEngine.Object.Destroy(go);
                            TryRemoveGameObject(wo);
                        }
                        WorldObjectsHandler.DestroyWorldObject(wo);
                    }
                }
            }
        }

        static string DebugWorldObject(int id)
        {
            var wo = WorldObjectsHandler.GetWorldObjectViaId(id);
            if (wo == null)
            {
                return "null";
            }
            return DebugWorldObject(wo);
        }

        static string DebugWorldObject(WorldObject wo)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("{ id=").Append(wo.GetId())
            .Append(", groupId=");
            if (wo.GetGroup() != null)
            {
                sb.Append(wo.GetGroup().GetId());
            }
            else
            {
                sb.Append("null");
            }
            sb.Append(", position=").Append(wo.GetPosition());

            sb.Append(" }");
            return sb.ToString();
        }

        static void ReceiveMessageInventories(MessageInventories minv)
        {
            if (updateMode == MultiplayerMode.CoopClient)
            {
                LogInfo("Received all inventories");

                HashSet<int> toDelete = new HashSet<int>();
                Dictionary<int, Inventory> localInventories = new Dictionary<int, Inventory>();
                foreach (Inventory inv in InventoriesHandler.GetAllInventories())
                {
                    int id = inv.GetId();
                    if (!localInventories.ContainsKey(id))
                    {
                        localInventories[id] = inv;
                        toDelete.Add(id);
                    }
                }

                suppressInventoryChange = true;
                try
                {
                    foreach (WorldInventory wi in minv.inventories)
                    {
                        toDelete.Remove(wi.id);
                        localInventories.TryGetValue(wi.id, out var inv);
                        if (inv == null)
                        {
                            LogInfo("ReceiveMessageInventories: Creating new inventory " + wi.id + " of size " + wi.size);
                            inv = InventoriesHandler.CreateNewInventory(wi.size, wi.id);
                        } 
                        else 
                        {
                            // Don't resize the player's inventory/equipment
                            if (wi.id > 2)
                            {
                                inv.SetSize(wi.size);
                            }
                            LogInfo("ReceiveMessageInventories: Updating inventory " + wi.id + ", " + wi.size + ", " + wi.itemIds.Count);
                        }

                        List<WorldObject> worldObjects = inv.GetInsideWorldObjects();

                        // see if there was an inventory composition change
                        HashSet<int> currentIds = new HashSet<int>();
                        foreach (WorldObject obj in worldObjects)
                        {
                            currentIds.Add(obj.GetId());
                        }
                        bool changed;
                        if (currentIds.Count == wi.itemIds.Count)
                        {
                            foreach (int id in wi.itemIds)
                            {
                                currentIds.Remove(id);
                            }
                            changed = currentIds.Count != 0;
                        }
                        else
                        {
                            changed = true;
                        }

                        if (changed)
                        {
                            worldObjects.Clear();
                            foreach (int id in wi.itemIds)
                            {
                                if (worldObjectById.TryGetValue(id, out var wo))
                                {
                                    worldObjects.Add(wo);
                                }
                            }
                            if (wi.id == 2)
                            {
                                var _playerController = GetPlayerMainController();
                                // Reset some equipment stats:
                                var mt = _playerController.GetMultitool();

                                // Apply equipment stats
                                HashSet<DataConfig.EquipableType> equipTypes = new HashSet<DataConfig.EquipableType>();
                                foreach (WorldObject wo in worldObjects)
                                {
                                    TryApplyEquipment(wo, _playerController, equipTypes);
                                }

                                // unequip
                                if (!equipTypes.Contains(DataConfig.EquipableType.MultiToolBuild))
                                {
                                    if (mt.HasEnabledState(DataConfig.MultiToolState.Build))
                                    {
                                        mt.RemoveEnabledState(DataConfig.MultiToolState.Build);
                                    }
                                }
                                if (!equipTypes.Contains(DataConfig.EquipableType.MultiToolDeconstruct))
                                {
                                    if (mt.HasEnabledState(DataConfig.MultiToolState.Deconstruct))
                                    {
                                        mt.RemoveEnabledState(DataConfig.MultiToolState.Deconstruct);
                                    }
                                }
                                if (!equipTypes.Contains(DataConfig.EquipableType.MultiToolLight))
                                {
                                    if ((bool)playerMultitoolCanUseLight.GetValue(mt))
                                    {
                                        mt.SetCanUseLight(false, 1);
                                    }
                                }
                                if (!equipTypes.Contains(DataConfig.EquipableType.CompassHUD))
                                {
                                    var cc = Managers.GetManager<CanvasCompass>();
                                    cc.SetStatus(false);
                                }
                            }
                            inv.RefreshDisplayerContent();
                        }
                    }
                }
                finally
                {
                    suppressInventoryChange = false;
                }

                List<Inventory> inventories = InventoriesHandler.GetAllInventories();
                for (int i = inventories.Count - 1; i >= 0; i--)
                {
                    Inventory inv = inventories[i];
                    if (toDelete.Contains(inv.GetId()))
                    {
                        LogInfo("ReceiveMessageInventories: Removing inventory " + inv.GetId());
                        inventories.RemoveAt(i);
                    }
                }

                LogInfo("Received all inventories - Done");
            }
        }

        static void TryApplyEquipment(WorldObject wo, 
            PlayerMainController _playerController,
            ICollection<DataConfig.EquipableType> equippables)
        {
            if (wo.GetGroup() is GroupItem groupItem)
            {
                var equipType = groupItem.GetEquipableType();
                if (!equippables.Contains(equipType))
                {
                    equippables.Add(equipType);
                }
                switch (equipType)
                {
                    case DataConfig.EquipableType.BackpackIncrease:
                        {
                            _playerController.GetPlayerBackpack()
                                .GetInventory()
                                .SetSize(12 + groupItem.GetGroupValue());
                            break;
                        }
                    case DataConfig.EquipableType.EquipmentIncrease:
                        {
                            _playerController.GetPlayerEquipment()
                                .GetInventory()
                                .SetSize(4 + groupItem.GetGroupValue());
                            break;
                        }
                    case DataConfig.EquipableType.OxygenTank:
                        {
                            _playerController.GetPlayerGaugesHandler()
                                .UpdateGaugesDependingOnEquipment(
                                    _playerController.GetPlayerEquipment()
                                    .GetInventory()
                                );
                            break;
                        }
                    case DataConfig.EquipableType.MultiToolMineSpeed:
                        {
                            _playerController.GetMultitool()
                                .GetMultiToolMine()
                                .SetMineTimeReducer(groupItem.GetGroupValue());
                            break;
                        }
                    case DataConfig.EquipableType.BootsSpeed:
                        {
                            _playerController.GetPlayerMovable()
                                .SetMoveSpeedChangePercentage((float)groupItem.GetGroupValue());
                            break;
                        }
                    case DataConfig.EquipableType.Jetpack:
                        {
                            _playerController.GetPlayerMovable()
                                .SetJetpackFactor((float)groupItem.GetGroupValue() / 100f);
                            break;
                        }
                    case DataConfig.EquipableType.MultiToolLight:
                        {
                            _playerController.GetMultitool()
                                .SetCanUseLight(true, groupItem.GetGroupValue());
                            break;
                        }

                    case DataConfig.EquipableType.MultiToolDeconstruct:
                        {
                            _playerController.GetMultitool().AddEnabledState(DataConfig.MultiToolState.Deconstruct);
                            break;
                        }
                    case DataConfig.EquipableType.MultiToolBuild:
                        {
                            _playerController.GetMultitool().AddEnabledState(DataConfig.MultiToolState.Build);
                            break;
                        }
                    case DataConfig.EquipableType.CompassHUD:
                        {
                            Managers.GetManager<CanvasCompass>().SetStatus(true);
                            break;
                        }
                }
            }
        }

        static void ReceiveMessageMined(MessageMined mm)
        {
            LogInfo("ReceiveMessageMined: OtherPlayer mined " + mm.id);

            WorldObject wo1 = WorldObjectsHandler.GetWorldObjectViaId(mm.id);
            if (wo1 != null && wo1.GetIsPlaced())
            {
                LogInfo("ReceiveMessageMined: Hiding WorldObject " + mm.id);
                wo1.ResetPositionAndRotation();
                wo1.SetDontSaveMe(false);

                if (TryGetGameObject(wo1, out var go))
                {
                    TryRemoveGameObject(wo1);
                    UnityEngine.Object.Destroy(go);
                }
                else
                {
                    LogWarning("ReceiveMessageMined: GameObject not found");
                }
                return;
            }

            DeleteActionMinableForId(mm.id);
        }

        static void ReceiveMessagePlaceConstructible(MessagePlaceConstructible mpc)
        {
            GroupConstructible gc = GroupsHandler.GetGroupViaId(mpc.groupId) as GroupConstructible;
            if (gc != null)
            {
                LogInfo("ReceiveMessagePlaceConstructible: " + mpc.groupId + ", " + mpc.position + ", " + mpc.rotation);
                WorldObject worldObject = WorldObjectsHandler.CreateNewWorldObject(gc, WorldObjectsIdHandler.GetNewWorldObjectIdForDb());
                worldObject.SetPositionAndRotation(mpc.position, mpc.rotation);
                WorldObjectsHandler.InstantiateWorldObject(worldObject, _fromDb: false);

                SendWorldObject(worldObject);

                ClientConsumeRecipe(gc);
            }
            else
            {
                LogInfo("ReceiveMessagePlaceConstructible: Unknown constructible " + mpc.groupId + ", " + mpc.position + ", " + mpc.rotation);
            }
        }

        static void ClientConsumeRecipe(GroupConstructible gc)
        {
            var inv = InventoriesHandler.GetInventoryById(shadowInventoryId);

            var toRemove = new List<Group>() { gc };
            if (inv.ContainsItems(toRemove))
            {
                inv.RemoveItems(toRemove, true, false);
            }
            else
            {
                toRemove.Clear();
                toRemove.AddRange(gc.GetRecipe().GetIngredientsGroupInRecipe());
                inv.RemoveItems(toRemove, true, false);
            }
        }

        static void SendWorldObject(WorldObject worldObject)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("UpdateWorldObject|");
            MessageAllObjects.AppendWorldObject(sb, '|', worldObject);

            LogInfo("Sending> " + sb.ToString());

            sb.Append("\r");
            Send(sb.ToString());
            Signal();
        }

        static void ReceiveMessageUpdateWorldObject(MessageUpdateWorldObject mc)
        {
            LogInfo("ReceiveMessageUpdateWorldObject: " + mc.worldObject.id + ", " + mc.worldObject.groupId);
            UpdateWorldObject(mc.worldObject);
        }

        static void ReceiveMessagePanelChanged(MessagePanelChanged mpc)
        {
            WorldObject wo = WorldObjectsHandler.GetWorldObjectViaId(mpc.itemId);
            if (wo != null)
            {
                if (TryGetGameObject(wo, out GameObject go))
                {
                    LogWarning("ReceiveMessagePanelChanged: " + mpc.itemId + ", " + mpc.panelId + ", " + mpc.panelType);
                    var panelIds = wo.GetPanelsId();
                    if (panelIds == null)
                    {
                        panelIds = new List<int>();
                    }
                    while (panelIds.Count <= mpc.panelId)
                    {
                        panelIds.Add(1);
                    }
                    panelIds[mpc.panelId] = mpc.panelType;
                    wo.SetPanelsId(panelIds);
                    UpdatePanelsOn(wo);

                    GroupConstructible gc = (GroupConstructible)GroupsHandler.GetGroupViaId(mpc.panelGroupId);
                    ClientConsumeRecipe(gc);

                    SendWorldObject(wo);
                } else
                {
                    LogWarning("ReceiveMessagePanelChanged: GameObject not found for: " + mpc.itemId + ", " + mpc.panelId + ", " + mpc.panelType);
                }
            }
            else
            {
                LogWarning("ReceiveMessagePanelChanged: Unknown item: " + mpc.itemId + ", " + mpc.panelId + ", " + mpc.panelType);
            }
        }

        static void ReceiveMessageSetTransform(MessageSetTransform st)
        {
            if (worldObjectById.TryGetValue(st.id, out var wo))
            {
                if (st.mode != MessageSetTransform.Mode.GameObjectOnly)
                {
                    wo.SetPositionAndRotation(st.position, st.rotation);
                }
                if (st.mode != MessageSetTransform.Mode.WorldObjectOnly 
                    && TryGetGameObject(wo, out var go) && go != null)
                {
                    go.transform.position = st.position;
                    go.transform.rotation = st.rotation;
                }
            }
        }

        static void ReceiveMessageDropWorldObject(MessageDropWorldObject mdwo)
        {
            if (updateMode == MultiplayerMode.CoopHost)
            {
                if (worldObjectById.TryGetValue(mdwo.id, out var wo))
                {
                    WorldObjectsHandler.DropOnFloor(wo, mdwo.position, mdwo.dropSize);
                    SendWorldObject(wo);
                    if (TryGetGameObject(wo, out var go))
                    {
                        go.GetComponent<WorldObjectAssociated>()
                            .StartCoroutine(WorldObjectsHandler_DropOnFloor_Tracker(wo));
                    }
                }
            }
        }

        static void ReceiveMessageUnlocks(MessageUnlocks mu)
        {
            if (updateMode == MultiplayerMode.CoopClient)
            {
                List<Group> grs = new List<Group>();
                foreach (string gid in mu.groupIds)
                {
                    Group gr = GroupsHandler.GetGroupViaId(gid);
                    if (gr != null)
                    {
                        grs.Add(gr);
                    }
                    else
                    {
                        LogWarning("ReceiveMessageUnlocks: Unknown groupId " + gid);
                    }
                }
                GroupsHandler.SetUnlockedGroups(grs);
            }
        }

        static void ReceiveTerraformState(MessageTerraformState mts)
        {
            if (updateMode == MultiplayerMode.CoopClient)
            {
                WorldUnitsHandler wuh = Managers.GetManager<WorldUnitsHandler>();
                foreach (WorldUnit wu in wuh.GetAllWorldUnits())
                {
                    if (wu.GetUnitType() == DataConfig.WorldUnitType.Oxygen)
                    {
                        worldUnitCurrentTotalValue.SetValue(wu, mts.oxygen);
                    }
                    if (wu.GetUnitType() == DataConfig.WorldUnitType.Heat)
                    {
                        worldUnitCurrentTotalValue.SetValue(wu, mts.heat);
                    }
                    if (wu.GetUnitType() == DataConfig.WorldUnitType.Pressure)
                    {
                        worldUnitCurrentTotalValue.SetValue(wu, mts.pressure);
                    }
                    if (wu.GetUnitType() == DataConfig.WorldUnitType.Biomass)
                    {
                        worldUnitCurrentTotalValue.SetValue(wu, mts.biomass);
                    }
                    if (wu.GetUnitType() == DataConfig.WorldUnitType.Terraformation)
                    {
                        worldUnitCurrentTotalValue.SetValue(wu, mts.oxygen + mts.heat + mts.pressure + mts.biomass);
                    }
                }

                List<GameObject> allWaterVolumes = Managers.GetManager<WaterHandler>().GetAllWaterVolumes();
                //LogInfo("allWaterVolumes.Count = " + allWaterVolumes.Count);
                foreach (GameObject go in allWaterVolumes)
                {
                    var wup = go.GetComponent<WorldUnitPositioning>();

                    //LogInfo("WorldUnitPositioning-Before: " + wup.transform.position);

                    worldUnitsPositioningWorldUnitsHandler.SetValue(wup, wuh);
                    worldUnitsPositioningHasMadeFirstInit.SetValue(wup, false);
                    wup.UpdateEvolutionPositioning();

                    //LogInfo("WorldUnitPositioning-After: " + wup.transform.position);
                }
            }
        }

        static void ReceiveMessageUpdateText(MessageUpdateText mut) 
        { 
            if (worldObjectById.TryGetValue(mut.id, out var wo))
            {
                wo.SetText(mut.text);
                if (TryGetGameObject(wo, out var go) && go != null)
                {
                    var wot = go.GetComponentInChildren<WorldObjectText>();
                    if (wot != null)
                    {
                        wot.SetText(mut.text);
                        
                        // Signal back the client immediately
                        if (updateMode == MultiplayerMode.CoopHost)
                        {
                            Send(mut);
                            Signal();
                        }
                    }
                }
            }
        }

        static void ReceiveMessageUpdateColor(MessageUpdateColor muc)
        {
            if (worldObjectById.TryGetValue(muc.id, out var wo))
            {
                wo.SetColor(muc.color);
                if (TryGetGameObject(wo, out var go) && go != null)
                {
                    var woc = go.GetComponentInChildren<WorldObjectColor>();
                    if (woc != null)
                    {
                        woc.SetColor(muc.color);

                        // Signal back the client immediately
                        if (updateMode == MultiplayerMode.CoopHost)
                        {
                            Send(muc);
                            Signal();
                        }
                    }
                }
            }
        }

        static void ReceiveMessageMicrochipUnlock(MessageMicrochipUnlock mmu)
        {
            // Signal back the client immediately
            if (updateMode == MultiplayerMode.CoopHost)
            {
                var gr = Managers.GetManager<UnlockingHandler>().GetUnlockableGroupAndUnlock();
                if (gr != null)
                {
                    mmu.groupId = gr.GetId();
                    var inv = InventoriesHandler.GetInventoryById(shadowInventoryId);
                    var microGroup = GroupsHandler.GetGroupViaId("BlueprintT1");
                    inv.RemoveItems(new List<Group>() { microGroup }, true, false);
                }
                else
                {
                    mmu.groupId = "";
                }
                Send(mmu);
                Signal();
            } 
            else
            {
                if (mmu.groupId != "")
                {
                    var unlocked = GroupsHandler.GetGroupViaId(mmu.groupId);
                    if (unlocked != null)
                    {
                        LogInfo("ReceiveMessageMicrochipUnlock: Unlock " + mmu.groupId);
                        GroupsHandler.UnlockGroupGlobally(unlocked);
                        Managers.GetManager<UnlockingHandler>().PlayAudioOnDecode();
                    }
                    else
                    {
                        LogError("ReceiveMessageMicrochipUnlock: Unknown group " + mmu.groupId);
                    }
                } 
                else
                {
                    Managers.GetManager<BaseHudHandler>().DisplayCursorText("UI_warn_no_more_chip_to_unlock", 3f, "");
                }
            }
        }

        static void ReceiveMessageSortInventory(MessageSortInventory msi)
        {
            int targetId = msi.inventoryId;
            if (targetId != 2)
            {
                // retarget shadow inventory
                if (targetId == 1 && updateMode == MultiplayerMode.CoopHost)
                {
                    targetId = shadowInventoryId;
                }
                Inventory inv = InventoriesHandler.GetInventoryById(targetId);
                if (inv != null) {
                    suppressInventoryChange = true;
                    try
                    {
                        inv.AutoSort();
                    }
                    finally
                    {
                        suppressInventoryChange = false;
                    }
                }
            }
        }

        static void ReceiveWelcome()
        {
            otherPlayer?.Destroy();
            Color color = Color.white;
            try
            {
                color = MessageHelper.StringToColor(hostColor.Value);
            }
            catch (Exception)
            {

            }
            otherPlayer = PlayerAvatar.CreateAvatar(color, true);
            NotifyUserFromBackground("Joined the host.");
        }

        static void ReceiveDisconnected()
        {
            LogInfo("Disconnected");
            otherPlayer?.Destroy();
            otherPlayer = null;
            clientConnected = false;
            _sendQueue.Clear();
            receiveQueue.Clear();
            if (updateMode == MultiplayerMode.CoopHost)
            {
                NotifyUserFromBackground("Client disconnected");
            }
            else
            {
                NotifyUserFromBackground("Host disconnected");
            }
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
                LogInfo("Login attempt: " + ml.user);
                receiveQueue.Enqueue(ml);
            }
            else
            if (MessageAllObjects.TryParse(message, out var mc))
            {
                //LogInfo(message);
                receiveQueue.Enqueue(mc);
            }
            else
            if (MessageMined.TryParse(message, out var mm1))
            {
                receiveQueue.Enqueue(mm1);
            }
            else
            if (MessageInventoryAdded.TryParse(message, out var mim))
            {
                receiveQueue.Enqueue(mim);
            }
            else
            if (MessageInventoryRemoved.TryParse(message, out var mir))
            {
                receiveQueue.Enqueue(mir);
            }
            else
            if (MessageInventories.TryParse(message, out var minv))
            {
                receiveQueue.Enqueue(minv);
            }
            else
            if (MessagePlaceConstructible.TryParse(message, out var mpc))
            {
                receiveQueue.Enqueue(mpc);
            }
            else
            if (MessageUpdateWorldObject.TryParse(message, out var mc1))
            {
                receiveQueue.Enqueue(mc1);
            }
            else
            if (MessagePanelChanged.TryParse(message, out var mpc1))
            {
                receiveQueue.Enqueue(mpc1);
            }
            else
            if (MessageSetTransform.TryParse(message, out var mst))
            {
                receiveQueue.Enqueue(mst);
            }
            else
            if (MessageDropWorldObject.TryParse(message, out var mdwo))
            {
                receiveQueue.Enqueue(mdwo);
            }
            else
            if (MessageUnlocks.TryParse(message, out var mu))
            {
                receiveQueue.Enqueue(mu);
            }
            else
            if (MessageTerraformState.TryParse(message, out var mts))
            {
                receiveQueue.Enqueue(mts);
            }
            else
            if (MessageUpdateText.TryParse(message, out var mut1))
            {
                receiveQueue.Enqueue(mut1);
            }
            else
            if (MessageUpdateColor.TryParse(message, out var muc1))
            {
                receiveQueue.Enqueue(muc1);
            }
            else
            if (MessageMicrochipUnlock.TryParse(message, out var mmu))
            {
                receiveQueue.Enqueue(mmu);
            }
            else
            if (MessageSortInventory.TryParse(message, out var msi))
            {
                receiveQueue.Enqueue(msi);
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
                LogInfo("Unknown message?\r\n" + message);
            }
            // TODO other messages
        }

        void DispatchMessage(object message)
        {
            if (otherPlayer == null)
            {
                if (updateMode == MultiplayerMode.CoopHost)
                {
                    if (message is MessageLogin ml)
                    {
                        ReceiveLogin(ml);
                    }
                    else
                    {
                        LogWarning("MessageLogin not yet received: " + message);
                    }
                }
                else
                if (updateMode == MultiplayerMode.CoopClient)
                {
                    if (message is string s)
                    {
                        DispatchMessageString(s);
                    }
                    else
                    {
                        LogWarning("Welcome not yet received: " + message);
                    }
                }
                return;
            }
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
                case MessageAllObjects mc:
                    {
                        ReceiveMessageAllObjects(mc);
                        break;
                    }
                case MessageMined mm1:
                    {
                        ReceiveMessageMined(mm1);
                        break;
                    }
                case MessageInventoryAdded mia:
                    {
                        ReceiveMessageInventoryAdded(mia);
                        break;
                    }
                case MessageInventoryRemoved mir:
                    {
                        ReceiveMessageInventoryRemoved(mir);
                        break;
                    }
                case MessageInventories minv:
                    {
                        ReceiveMessageInventories(minv);
                        break;
                    }
                case MessagePlaceConstructible mpc:
                    {
                        ReceiveMessagePlaceConstructible(mpc);
                        break;
                    }
                case MessageUpdateWorldObject mc1:
                    {
                        ReceiveMessageUpdateWorldObject(mc1);
                        break;
                    }
                case MessagePanelChanged mpc1:
                    {
                        ReceiveMessagePanelChanged(mpc1);
                        break;
                    }
                case MessageSetTransform mst:
                    {
                        ReceiveMessageSetTransform(mst);
                        break;
                    }
                case MessageDropWorldObject mdwo:
                    {
                        ReceiveMessageDropWorldObject(mdwo);
                        break;
                    }
                case MessageUnlocks mu:
                    {
                        ReceiveMessageUnlocks(mu);
                        break;
                    }
                case MessageTerraformState mts:
                    {
                        ReceiveTerraformState(mts);
                        break;
                    }
                case MessageUpdateText mut1:
                    {
                        ReceiveMessageUpdateText(mut1);
                        break;
                    }
                case MessageUpdateColor muc:
                    {
                        ReceiveMessageUpdateColor(muc);
                        break;
                    }
                case MessageMicrochipUnlock mmu:
                    {
                        ReceiveMessageMicrochipUnlock(mmu);
                        break;
                    }
                case MessageSortInventory msi:
                    {
                        ReceiveMessageSortInventory(msi);
                        break;
                    }
                case string s:
                    {
                        DispatchMessageString(s);
                        break;
                    }
                default:
                    {
                        LogInfo(message.GetType().ToString());
                        break;
                    }
                    // TODO dispatch on message type
            }
        }

        static void DispatchMessageString(string s)
        {
            if (s == "Welcome")
            {
                ReceiveWelcome();
            }
            else if (s == "Disconnected")
            {
                ReceiveDisconnected();
            }
        }

        #region - Logging -
        internal static void LogInfo(object message)
        {
            if (updateMode == MultiplayerMode.CoopClient)
            {
                lock (logLock)
                {
                    File.AppendAllText(Application.persistentDataPath + "\\Player_Client.log", "[Info   :(Feat) Multiplayer] " + message + "\r\n");
                }
            }
            else
            if (updateMode == MultiplayerMode.CoopHost)
            {
                lock (logLock)
                {
                    File.AppendAllText(Application.persistentDataPath + "\\Player_Host.log", "[Info   :(Feat) Multiplayer] " + message + "\r\n");
                }
            }
            else
            {
                theLogger.LogInfo(message);
            }
        }

        internal static void LogError(object message)
        {
            if (updateMode == MultiplayerMode.CoopClient)
            {
                lock (logLock)
                {
                    File.AppendAllText(Application.persistentDataPath + "\\Player_Client.log", "[Error  :(Feat) Multiplayer] " + message + "\r\n");
                }
            }
            else
            if (updateMode == MultiplayerMode.CoopHost)
            {
                lock (logLock)
                {
                    File.AppendAllText(Application.persistentDataPath + "\\Player_Host.log", "[Error  :(Feat) Multiplayer] " + message + "\r\n");
                }
            }
            else
            {
                theLogger.LogInfo(message);
            }
        }
        internal static void LogWarning(object message)
        {
            if (updateMode == MultiplayerMode.CoopClient)
            {
                lock (logLock)
                {
                    File.AppendAllText(Application.persistentDataPath + "\\Player_Client.log", "[Warning:(Feat) Multiplayer] " + message + "\r\n");
                }
            }
            else
            if (updateMode == MultiplayerMode.CoopHost)
            {
                lock (logLock)
                {
                    File.AppendAllText(Application.persistentDataPath + "\\Player_Host.log", "[Warning:(Feat) Multiplayer] " + message + "\r\n");
                }
            }
            else
            {
                theLogger.LogInfo(message);
            }
        }

        #endregion - Logging -
    }
}
