using BepInEx;
using System;
using SpaceCraft;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Collections.Concurrent;

namespace FeatMultiplayer
{
    public partial class Plugin : BaseUnityPlugin
    {
        /// <summary>
        /// Returns the current game mode to be used by other mods that
        /// are sensitive of being in a multiplayer or sensitive to be
        /// on the host or client side.
        /// </summary>
        /// <returns>The current mode: <c>MainMenu</c>, <c>SinglePlayer</c>, <c>CoopHost</c> or <c>CoopClient</c> </returns>
        public static string GetMultiplayerMode()
        {
            return updateMode switch
            {
                MultiplayerMode.MainMenu => "MainMenu",
                MultiplayerMode.SinglePlayer => "SinglePlayer",
                MultiplayerMode.CoopHost => "CoopHost",
                MultiplayerMode.CoopClient => "CoopClient",
                _ => "Unknown",
            };
        }

        /// <summary>
        /// Returns the current game mode to be used by other mods that
        /// are sensitive of being in a multiplayer or sensitive to be
        /// on the host or client side.
        /// Returns <c>MainMenu</c>, <c>SinglePlayer</c>, <c>CoopHost</c> or <c>CoopClient</c>.
        /// </summary>
        public static Func<string> apiGetMultiplayerMode;

        /// <summary>
        /// Returns the number of currently existing world objects of
        /// a given group identifier (as returned by <see cref="Group.GetId"/>).
        /// </summary>
        public static Func<string, int> apiCountByGroupId;

        /// <summary>
        /// Allows registering a function callback for when a non-standard message is
        /// received on the background thread.
        /// The function submitted will receive the client id, message string, the queue where to
        /// post an object to be processed on the UI thread, and should return true
        /// if the message was processed.
        /// </summary>
        public static Action<Func<int, string, ConcurrentQueue<object>, bool>> apiAddMessageParser;

        /// <summary>
        /// Allows registering a function callback for when a non-standard object is
        /// being processed on the UI thread.
        /// The function will receive this object and should return true if
        /// the object has been handled successfully.
        /// </summary>
        public static Action<Func<object, bool>> apiAddMessageReceiver;

        /// <summary>
        /// Queue up an non-standard object to be sent to a specific client.
        /// To send a <see cref="WorldObject"/>, use <see cref="apiSendWorldObject"/>.
        /// 
        /// The object should implement a proper <see cref="object.ToString"/>
        /// that includes an identifiable prefix and ends with a \n character.
        /// The object will be converted to a string on the background thread so
        /// it should not access any UI state at that point.
        ///
        /// Use 0 to send to all clients on the host.
        /// Use 0 to send to the host on the client.
        /// 
        /// The message might not be sent immediately, use <see cref="apiSignal"/>
        /// to tell the network to send it now.
        /// It is possible to queue up many messages and then signal for them at once.
        /// </summary>
        public static Action<int, object> apiSend;

        /// <summary>
        /// Signal that any queued up messages can be sent over the network.
        ///
        /// Use 0 to signal to all clients on the host.
        /// Use 0 to signal to the host on the client.
        /// 
        /// </summary>
        public static Action<int> apiSignal;

        /// <summary>
        /// Sends the given <see cref="WorldObject"/>'s state to the specific client.
        ///
        /// Use 0 to send to all clients on the host.
        /// Use 0 to send to the host on the client.
        /// 
        /// The call signals immediately.
        /// </summary>
        public static Action<int, WorldObject> apiSendWorldObject;

        /// <summary>
        /// Executes the given action while having the inventory changes propagation
        /// suppressed.
        /// The first parameter object is handed to the second action upon executing.
        /// </summary>
        public static Action<object, Action<object>> apiSuppressInventoryChangeWhile;

        /// <summary>
        /// Returns the shadow backpack inventory of the specified 
        /// client id or null if not found.
        /// </summary>
        public static Func<int, Inventory> apiGetClientBackpack;

        /// <summary>
        /// Returns the shadow equipment inventory of the specified 
        /// client id or null if not found.
        /// </summary>
        public static Func<int, Inventory> apiGetClientEquipment;

        /// <summary>
        /// Sends the entire inventory, including its size and content
        /// to the given client.
        /// </summary>
        public static Action<int, Inventory> apiSendInventory;

        /// <summary>
        /// Logs the given object to the appropriate host/client log file.
        /// </summary>
        public static Action<object> apiLogDebug;

        /// <summary>
        /// Logs the given object to the appropriate host/client log file.
        /// </summary>
        public static Action<object> apiLogInfo;

        /// <summary>
        /// Logs the given object to the appropriate host/client log file.
        /// </summary>
        public static Action<object> apiLogWarning;

        /// <summary>
        /// Logs the given object to the appropriate host/client log file.
        /// </summary>
        public static Action<object> apiLogError;

        #region - Api Setup -

        /// <summary>
        /// Prepare delegates to let other mods interact with this mod.
        /// </summary>
        static void ApiSetup()
        {
            apiGetMultiplayerMode = GetMultiplayerMode;
            apiCountByGroupId = ApiGetCountByGroupId;
            apiAddMessageParser = ApiAddMessageParser;
            apiAddMessageReceiver = ApiAddMessageReceiver;
            apiSend = ApiSend;
            apiSignal = ApiSignal;
            apiSendWorldObject = ApiSendWorldObject;
            apiSuppressInventoryChangeWhile = ApiSuppressInventoryChangeWhile;
            apiGetClientBackpack = ApiGetClientBackpack;
            apiGetClientEquipment = ApiGetClientEquipment;
            apiSendInventory = ApiSendInventory;
            apiLogDebug = LogDebug;
            apiLogInfo = LogInfo;
            apiLogWarning = LogWarning;
            apiLogError = LogError;
        }

        static int ApiGetCountByGroupId(string groupId)
        {
            countByGroupId.TryGetValue(groupId, out var c);
            return c;
        }

        static void ApiAddMessageParser(Func<int, string, ConcurrentQueue<object>, bool> parser)
        {
            if (parser == null)
            {
                throw new ArgumentNullException(nameof(parser));
            }
            messageParsers.Add(parser);
        } 

        static void ApiAddMessageReceiver(Func<object, bool> receiver)
        {
            if (receiver == null)
            {
                throw new ArgumentNullException(nameof(receiver));
            }
            messageReceivers.Add(receiver);
        }

        static void ApiSend(int clientId, object o)
        {
            if (o == null)
            {
                throw new ArgumentNullException(nameof(o));
            }
            if (clientId == 0)
            {
                if (updateMode == MultiplayerMode.CoopHost)
                {
                    SendAllClients(new MessageCustom() { o = o });
                }
                else
                {
                    SendHost(new MessageCustom() { o = o });
                }
            }
            else
            {
                SendClient(clientId, o);
            }
        }

        static void ApiSignal(int clientId)
        {
            if (clientId == 0)
            {
                if (updateMode == MultiplayerMode.CoopHost)
                {
                    SignalAllClients();
                }
                else
                {
                    SignalHost();
                }
            }
            else
            {
                SignalClient(clientId);
            }
        }

        static void ApiSendWorldObject(int clientId, WorldObject wo)
        {
            if (wo == null)
            {
                throw new ArgumentNullException(nameof(wo));
            }
            if (clientId == 0)
            {
                if (updateMode == MultiplayerMode.CoopHost)
                {
                    SendWorldObjectToClients(wo, false);
                }
                else
                {
                    SendWorldObjectToHost(wo, false);
                }
            }
        }

        static void ApiSuppressInventoryChangeWhile(object o, Action<object> action)
        {
            suppressInventoryChange = true;
            try
            {
                action(o);
            }
            finally
            {
                suppressInventoryChange = false;
            }
        }

        static Inventory ApiGetClientBackpack(int clientId)
        {
            if (_clientConnections.TryGetValue(clientId, out var cc))
            {
                return cc.shadowBackpack;
            }
            return null;
        }

        static Inventory ApiGetClientEquipment(int clientId)
        {
            if (_clientConnections.TryGetValue(clientId, out var cc))
            {
                return cc.shadowEquipment;
            }
            return null;
        }

        static void ApiSendInventory(int clientId, Inventory inventory)
        {
            // FIXME use the client id to send to the proper client.
            if (clientId == 0)
            {
                if (updateMode == MultiplayerMode.CoopHost)
                {
                    SendAllClients(new MessageInventorySize()
                    {
                        inventoryId = inventory.GetId(),
                        size = inventory.GetSize()
                    });
                }
                else
                {
                    SendHost(new MessageInventorySize()
                    {
                        inventoryId = inventory.GetId(),
                        size = inventory.GetSize()
                    });
                }
            }
            else
            {
                SendClient(clientId, new MessageInventorySize()
                {
                    inventoryId = inventory.GetId(),
                    size = inventory.GetSize()
                });
            }
            // FIXME send inventory content too
        }

        #endregion - Api Setup -
    }
}
