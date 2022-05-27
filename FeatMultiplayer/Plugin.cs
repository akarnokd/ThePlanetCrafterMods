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

        internal static ManualLogSource theLogger;

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

        static int shadowInventoryWorldId = 50;
        static int shadowInventoryId;
        static int shadowEquipmentWorldId = 51;
        static int shadowEquipmentId;

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

            hostAddress = Config.Bind("Client", "HostAddress", "", "The IP address where the Host can be located from the client.");
            clientName = Config.Bind("Client", "Name", "Buddy", "The name show to the host when a client joins.");
            clientPassword = Config.Bind("Client", "Password", "password", "The plaintext(!) password presented to the host when joining their game.");

            Assembly me = Assembly.GetExecutingAssembly();
            string dir = Path.GetDirectoryName(me.Location);

            astronautFront = LoadPNG(Path.Combine(dir, "Astronaut_Front.png"));
            astronautBack = LoadPNG(Path.Combine(dir, "Astronaut_Back.png"));

            File.Delete(Application.persistentDataPath + "\\Player_Client.log");
            File.Delete(Application.persistentDataPath + "\\Player_Host.log");

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

        static string multiplayerFilename = "Survival-9999999";

        static bool suppressInventoryChange;

        enum MultiplayerMode
        {
            MainMenu,
            SinglePlayer,
            CoopHost,
            CoopClient
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

        #endregion -Start Menu-

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
                LogInfo("Entering world as Host");
                StartAsHost();
            }
            else if (updateMode == MultiplayerMode.CoopClient)
            {
                LogInfo("Entering world as Client");
                StartAsClient();
            }
            else
            {
                LogInfo("Entering world as Solo");
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
                        sendQueue.Enqueue("Mined|" + worldObject.GetId() + "\n");
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
                File.Delete(Application.persistentDataPath + "/" + multiplayerFilename + ".json");
            }
            stopNetwork?.Cancel();
            updateMode = MultiplayerMode.MainMenu;
            sendQueue.Clear();
            receiveQueue.Clear();
            otherPlayer?.Destroy();
            otherPlayer = null;
        }

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
        static void Inventory_AddItem(bool __result, int ___inventoryId, WorldObject _worldObject)
        {
            if (__result && updateMode != MultiplayerMode.SinglePlayer && !suppressInventoryChange)
            {
                if (updateMode == MultiplayerMode.CoopHost 
                    && (___inventoryId == 1 
                    || ___inventoryId == 2
                    || ___inventoryId == shadowInventoryId 
                    || ___inventoryId == shadowEquipmentId))
                {
                    return;
                }
                var mia = new MessageInventoryAdded()
                {
                    inventoryId = ___inventoryId,
                    itemId = _worldObject.GetId(),
                    groupId = _worldObject.GetGroup().GetId()
                };
                sendQueue.Enqueue(mia);
                sendQueueBlock.Set();
            }
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(Inventory), nameof(Inventory.RemoveItem))]
        static void Inventory_RemoveItem(int ___inventoryId, WorldObject _worldObject, bool _destroyWorldObject)
        {
            if (updateMode != MultiplayerMode.SinglePlayer && !suppressInventoryChange)
            {
                if (updateMode == MultiplayerMode.CoopHost
                    && (___inventoryId == 1
                    || ___inventoryId == 2
                    || ___inventoryId == shadowInventoryId
                    || ___inventoryId == shadowEquipmentId))
                {
                    return;
                }
                var mir = new MessageInventoryRemoved()
                {
                    inventoryId = ___inventoryId,
                    itemId = _worldObject.GetId(),
                    destroy = _destroyWorldObject
                };
                sendQueue.Enqueue(mir);
                sendQueueBlock.Set();
            }
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(Inventory), nameof(Inventory.RemoveItems))]
        static bool Inventory_RemoveItems(int ___inventoryId, 
            List<Group> _groups, bool _destroyWorldObjects, bool _displayInformation,
            Inventory __instance,
            List<WorldObject> ___worldObjectsInInventory)
        {
            if (updateMode != MultiplayerMode.SinglePlayer)
            {
                if (updateMode == MultiplayerMode.CoopHost
                    && (___inventoryId == 1
                    || ___inventoryId == 2
                    || ___inventoryId == shadowInventoryId
                    || ___inventoryId == shadowEquipmentId))
                {
                    return true;
                }
                List<WorldObject> list = new List<WorldObject>();
                foreach (Group _group in _groups)
                {
                    for (int num = ___worldObjectsInInventory.Count - 1; num > -1; num--)
                    {
                        if (___worldObjectsInInventory[num].GetGroup() == _group)
                        {
                            list.Add(___worldObjectsInInventory[num]);
                            __instance.RemoveItem(___worldObjectsInInventory[num], _destroyWorldObjects);
                            break;
                        }
                    }
                }

                if (!_displayInformation)
                {
                    return false;
                }

                InformationsDisplayer informationsDisplayer = Managers.GetManager<DisplayersHandler>().GetInformationsDisplayer();
                foreach (WorldObject item in list)
                {
                    informationsDisplayer.AddInformation(2f, Readable.GetGroupName(item.GetGroup()), DataConfig.UiInformationsType.OutInventory, item.GetGroup().GetImage());
                }
                return false;
            }
            return true;
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
                    if (otherPlayer != null)
                    {
                        PlayersManager p = Managers.GetManager<PlayersManager>();
                        if (p != null)
                        {
                            PlayerMainController pm = p.GetActivePlayerController();
                            if (pm != null)
                            {
                                Transform player = pm.transform;
                                player.SetPositionAndRotation(otherPlayer.avatar.transform.position, Quaternion.identity);
                            }
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
                        case string s: {
                                if (s == "Welcome")
                                {
                                    otherPlayer?.Destroy();
                                    otherPlayer = PlayerAvatar.CreateAvatar();
                                }
                                else if (s == "Disconnected")
                                {
                                    LogInfo("Client disconnected");
                                    otherPlayer?.Destroy();
                                    clientConnected = false;
                                }
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
                        LogInfo("User login success: " + ml.user);
                        NotifyUser("User joined: " + ml.user);
                        otherPlayer?.Destroy();
                        otherPlayer = PlayerAvatar.CreateAvatar();
                        PrepareHiddenChests();
                        sendQueue.Enqueue("Welcome\n");
                        sendQueueBlock.Set();
                        lastHostSync = Time.realtimeSinceStartup;
                        SendFullState();
                        return;
                    }
                }

                LogInfo("User login failed: " + ml.user);
                sendQueue.Enqueue(EAccessDenied);
                sendQueueBlock.Set();
            }
        }

        static void PrepareHiddenChests()
        {
            // The other player's shadow inventory
            PrepareHiddenChest(shadowInventoryWorldId, ref shadowInventoryId);
            PrepareHiddenChest(shadowEquipmentWorldId, ref shadowEquipmentId);
        }

        static void PrepareHiddenChest(int id, ref int inventoryId)
        {
            WorldObject wo = WorldObjectsHandler.GetWorldObjectViaId(id);
            if (wo == null)
            {
                LogInfo("Creating special inventory " + id);

                wo = WorldObjectsHandler.CreateNewWorldObject(GroupsHandler.GetGroupViaId("GROUP_DESC_Container2"), id);
                wo.SetPositionAndRotation(new Vector3(-500, -500, -450), Quaternion.identity);
                WorldObjectsHandler.InstantiateWorldObject(wo, false);
                var inv = InventoriesHandler.CreateNewInventory(1000, 0);
                int invId = inv.GetId();
                inventoryId = invId;
                wo.SetLinkedInventoryId(invId);
            }
        }

        static void ReceiveMessageInventoryAdded(MessageInventoryAdded mia)
        {
            int targetId = mia.inventoryId;
            if (targetId == 1)
            {
                targetId = shadowInventoryId;
            }
            else
            if (targetId == 2)
            {
                targetId = shadowEquipmentId;
            }
            var inv = InventoriesHandler.GetInventoryById(targetId);
            if (inv != null)
            {
                WorldObject wo = WorldObjectsHandler.GetWorldObjectViaId(mia.itemId);
                if (wo == null)
                {
                    wo = WorldObjectsHandler.CreateNewWorldObject(GroupsHandler.GetGroupViaId(mia.groupId), mia.itemId);
                }
                suppressInventoryChange = true;
                try
                {
                    inv.AddItem(wo);
                } 
                finally
                {
                    suppressInventoryChange = false;
                }
            }
            else
            {
                LogWarning("Unknown inventory added: " + mia.GetString());
            }
        }

        static void ReceiveMessageInventoryRemoved(MessageInventoryRemoved mia)
        {
            int targetId = mia.inventoryId;
            if (targetId == 1)
            {
                targetId = shadowInventoryId;
            }
            else
            if (targetId == 2)
            {
                targetId = shadowEquipmentId;
            }
            var inv = InventoriesHandler.GetInventoryById(targetId);
            if (inv != null)
            {
                WorldObject wo = WorldObjectsHandler.GetWorldObjectViaId(mia.itemId);
                if (wo != null) 
                {
                    suppressInventoryChange = true;
                    try
                    {
                        inv.RemoveItem(wo, mia.destroy);
                    }
                    finally
                    {
                        suppressInventoryChange = false;
                    }
                }
                else
                {
                    LogWarning("Unknown item removed: " + mia.GetString());
                }
            }
            else
            {
                LogWarning("Unknown inventory removed: " + mia.GetString());
            }
        }

        static void SendFullState()
        {
            LogInfo("Begin syncing the entire game state to the client");
            StringBuilder sb = new StringBuilder();
            sb.Append("Constructs");
            foreach (WorldObject wo in WorldObjectsHandler.GetAllWorldObjects())
            {
                if (!wo.GetDontSaveMe())
                {
                    int id = wo.GetId();
                    if (id != shadowInventoryWorldId && id != shadowEquipmentWorldId)
                    {
                        sb.Append("|");
                        MessageConstructs.AppendWorldObject(sb, wo);
                    }
                }
            }
            sb.Append('\n');
            sendQueue.Enqueue(sb.ToString());

            sb = new StringBuilder();
            sb.Append("Inventories");
            foreach (Inventory inv in InventoriesHandler.GetAllInventories())
            {
                int id = inv.GetId();
                if (id != 1 && id != 2)
                {
                    sb.Append("|");
                    MessageInventories.Append(sb, inv, shadowInventoryId, shadowEquipmentId);
                }
            }
            sb.Append('\n');
            sendQueue.Enqueue(sb.ToString());

            sendQueueBlock.Set();
        }

        static void ReceiveMessageConstructs(MessageConstructs mc)
        {
            if (updateMode == MultiplayerMode.CoopClient)
            {
                LogInfo("Received all constructs");
                Dictionary<int, WorldObject> localConstructs = new Dictionary<int, WorldObject>();
                HashSet<int> toDelete = new HashSet<int>();
                foreach (WorldObject wo in WorldObjectsHandler.GetAllWorldObjects())
                {
                    int id = wo.GetId();
                    if (!localConstructs.ContainsKey(id))
                    {
                        localConstructs[id] = wo;
                        toDelete.Add(id);
                    }
                }

                foreach (MessageWorldObject mwo in mc.worldObjects)
                {
                    toDelete.Remove(mwo.id);

                    bool doPlace = false;
                    if (!localConstructs.TryGetValue(mwo.id, out var wo))
                    {
                        Group gr = GroupsHandler.GetGroupViaId(mwo.groupId);
                        wo = WorldObjectsHandler.CreateNewWorldObject(gr, mwo.id);
                        LogInfo("Creating new WorldObject " + mwo.id + " - " + mwo.groupId);
                    } 
                    wo.SetPositionAndRotation(mwo.position, mwo.rotation);
                    doPlace = wo.GetIsPlaced();
                    wo.SetColor(mwo.color);
                    wo.SetText(mwo.text);
                    wo.SetGrowth(mwo.growth);
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
                            InventoriesHandler.CreateNewInventory(100, mwo.inventoryId);
                        }
                    }
                    else
                    {
                        wo.SetLinkedInventoryId(0);
                        // FIXME delete inventory?
                    }

                    if (doPlace)
                    {
                        WorldObjectsHandler.InstantiateWorldObject(wo, true);
                    }
                    else
                    {
                        var go = WorldObjectsHandler.GetGameObjectViaWorldObject(wo);
                        UnityEngine.Object.Destroy(go);
                    }
                }

                foreach (int id in toDelete)
                {
                    WorldObjectsHandler.DestroyWorldObject(localConstructs[id]);
                }
            }
        }

        static void ReceiveMessageInventories(MessageInventories minv)
        {
            /*
            if (updateMode == MultiplayerMode.CoopClient)
            {
                LogInfo("Received all inventories");
                Dictionary<int, WorldObject> localConstructs = new Dictionary<int, WorldObject>();
                foreach (WorldObject wo in WorldObjectsHandler.GetAllWorldObjects())
                {
                    int id = wo.GetId();
                    if (!localConstructs.ContainsKey(id))
                    {
                        localConstructs[id] = wo;
                    }
                }

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
                        localInventories.TryGetValue(wi.id, out var inv);
                        if (inv == null)
                        {
                            inv = InventoriesHandler.CreateNewInventory(wi.size, wi.id);
                        } 
                        else 
                        {
                            inv.SetSize(wi.size);
                        }
                        inv.GetInsideWorldObjects().Clear();
                        foreach (int id in wi.itemIds)
                        {
                            if (localConstructs.TryGetValue(id, out var wo))
                            {
                                inv.GetInsideWorldObjects().Add(wo);
                            }
                        }
                    }
                }
                finally
                {
                    suppressInventoryChange = false;
                }
            }
            */
        }

        static void ReceiveMessageMined(MessageMined mm)
        {
            LogInfo("OtherPlayer mined " + mm.id);

            foreach (ActionMinable am in UnityEngine.Object.FindObjectsOfType<ActionMinable>())
            {
                var woa = am.GetComponent<WorldObjectAssociated>();
                if (woa != null)
                {
                    var wo = woa.GetWorldObject();
                    if (wo != null)
                    {
                        if (wo.GetId() == mm.id)
                        {

                            UnityEngine.Object.Destroy(am);
                            return;
                        }
                    }
                }
            }

            LogInfo("OtherPlayer mined " + mm.id + " but not found???");
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


        #region -Setup TCP-
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
                LogInfo("Client connecting to " + hostAddress.Value + ":" + port.Value);
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
            } catch (Exception ex)
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
                    LogError(ex);
                }
            } 
            else
            {
                LogInfo("New Client from " + client.Client.RemoteEndPoint);
                clientConnected = true;
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
                LogInfo("SenderLoop stop");
            } catch (Exception ex)
            {
                LogError(ex);
            }
            receiveQueue.Enqueue("Disconnected");
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
                    var reader = new StreamReader(stream, Encoding.UTF8);
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
                LogError(ex);
            }
            receiveQueue.Enqueue("Disconnected");
        }

        static readonly byte[] ENoClientSlotBytes = Encoding.UTF8.GetBytes("ENoClientSlot\n");
        static readonly byte[] EAccessDenied = Encoding.UTF8.GetBytes("EAccessDenied\n");

        #endregion -Setup TCP-

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
            if (MessageConstructs.TryParse(message, out var mc))
            {
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

        #region -Multiplayer Menu-
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

        #endregion -Multiplayer Menu-

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
    }
}
