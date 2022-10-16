using BepInEx.Bootstrap;
using HarmonyLib;
using SpaceCraft;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace FeatMultiplayer
{
    /// <summary>
    /// Convenience class to discover the <c>(Feat) Multiplayer</c>
    /// mod's API, get the relevant delegates and allow calling them
    /// from your code.
    /// 
    /// I recommend taking this class by source and include it in your
    /// own mod, under a different namespace of course.
    /// </summary>
    public class FeatMultiplayerApi
    {
        /// <summary>
        /// The Guid of the <c>(Feat) Multiplayer</c> mod.
        /// </summary>
        public const string modFeatMultiplayerGuid = "akarnokd.theplanetcraftermods.featmultiplayer";

        private Func<string> apiGetMultiplayerMode;
        private Func<string, int> apiCountByGroupId;
        private Action<Func<int, string, ConcurrentQueue<object>, bool>> apiAddMessageParser;
        private Action<Func<object, bool>> apiAddMessageReceiver;
        private Action<int, object> apiSend;
        private Action<int> apiSignal;
        private Action<int, WorldObject> apiSendWorldObject;
        private Action<object, Action<object>> apiSuppressInventoryChangeWhile;
        private Func<int, Inventory> apiGetClientBackpack;
        private Func<int, Inventory> apiGetClientEquipment;
        private Action<int, Inventory> apiSendInventory;
        private Action<object> apiLogDebug;
        private Action<object> apiLogInfo;
        private Action<object> apiLogWarning;
        private Action<object> apiLogError;
        private Func<string, string> apiClientGetData;
        private Action<string, string> apiClientSetData;
        private Action<Action> apiClientRegisterDataReady;
        private Func<string> apiGetHostState;

        /// <summary>
        /// Enumeration for the state of the multiplayer mod.
        /// </summary>
        public enum MultiplayerState
        {
            /// <summary>
            /// Some unsupported state.
            /// </summary>
            Unknown,
            /// <summary>
            /// Still in the main menu.
            /// </summary>
            MainMenu,
            /// <summary>
            /// Playing as single player.
            /// </summary>
            SinglePlayer,
            /// <summary>
            /// Playing as a host of a multiplayer game.
            /// </summary>
            CoopHost,
            /// <summary>
            /// Playing as a client of a multiplayer game.
            /// </summary>
            CoopClient
        }

        /// <summary>
        /// For the client, it represents the connection progress towards the client.
        /// </summary>
        public enum HostState
        {
            /// <summary>
            /// Not yet connected
            /// </summary>
            None,
            /// <summary>
            /// Connected, waiting for login success
            /// </summary>
            Connected,
            /// <summary>
            /// Login success, messages can be safely sent.
            /// </summary>
            Active,
            /// <summary>
            /// Disconnected from the host.
            /// </summary>
            Disconnected,
            /// <summary>
            /// When called on the host side
            /// </summary>
            Host,
        }

        /// <summary>
        /// Creates a new FeatMultiplayerApi instance by locating the
        /// <c>(Feat) Multiplayer</c> mod and retrieving its enpoint infos.
        /// 
        /// Use <see cref="IsAvailable"/> to check if it succeeded.
        /// </summary>
        /// <returns>The new FeatMultiplayerApi instance.</returns>
        public static FeatMultiplayerApi Create()
        {
            var result = new FeatMultiplayerApi();
            if (Chainloader.PluginInfos.TryGetValue(modFeatMultiplayerGuid, out BepInEx.PluginInfo pi))
            {

                result.apiGetMultiplayerMode = GetApi<Func<string>>(pi, "apiGetMultiplayerMode");

                result.apiCountByGroupId = GetApi<Func<string, int>>(pi, "apiCountByGroupId");

                result.apiAddMessageParser
                      = GetApi<Action<Func<int, string, ConcurrentQueue<object>, bool>>>(pi, "apiAddMessageParser");

                result.apiAddMessageReceiver
                      = GetApi<Action<Func<object, bool>>>(pi, "apiAddMessageReceiver");

                result.apiSend = GetApi<Action<int, object>>(pi, "apiSend");

                result.apiSignal = GetApi<Action<int>>(pi, "apiSignal");

                result.apiSendWorldObject = GetApi<Action<int, WorldObject>>(pi, "apiSendWorldObject");

                result.apiSuppressInventoryChangeWhile
                      = GetApi<Action<object, Action<object>>>(pi, "apiSuppressInventoryChangeWhile");

                result.apiGetClientBackpack
                      = GetApi<Func<int, Inventory>>(pi, "apiGetClientBackpack");

                result.apiGetClientEquipment
                      = GetApi<Func<int, Inventory>>(pi, "apiGetClientEquipment");

                result.apiSendInventory
                      = GetApi<Action<int, Inventory>>(pi, "apiSendInventory");

                result.apiLogDebug = GetApi<Action<object>>(pi, "apiLogDebug");

                result.apiLogInfo = GetApi<Action<object>>(pi, "apiLogInfo");

                result.apiLogWarning = GetApi<Action<object>>(pi, "apiLogWarning");

                result.apiLogError = GetApi<Action<object>>(pi, "apiLogError");

                result.apiClientGetData = GetApi<Func<string, string>>(pi, "apiClientGetData");

                result.apiClientSetData = GetApi<Action<string, string>>(pi, "apiClientSetData");

                result.apiClientRegisterDataReady = GetApi<Action<Action>>(pi, "apiClientRegisterDataReady");

                result.apiGetHostState = GetApi<Func<string>>(pi, "apiGetHostState");

            }
            return result;
        }

        /// <summary>
        /// Returns true if the <c>(Feat) Multiplayer</c> mod was found and this Api
        /// class was initialized successfully.
        /// </summary>
        /// <returns>True if the Apis are available, false otherwise.</returns>
        public bool IsAvailable()
        {
            return apiGetMultiplayerMode != null;
        }

        /// <summary>
        /// Returns the current multiplayer state.
        /// </summary>
        /// <returns>The current multiplayer state.</returns>
        public MultiplayerState GetState()
        {
            ThrowIfUnavailable();
            return apiGetMultiplayerMode() switch
            {
                "MainMenu" => MultiplayerState.MainMenu,
                "SinglePlayer" => MultiplayerState.SinglePlayer,
                "CoopHost" => MultiplayerState.CoopHost,
                "CoopClient" => MultiplayerState.CoopClient,
                _ => MultiplayerState.Unknown
            };
        }

        public int GetGroupCount(string groupId)
        {
            ThrowIfUnavailable();
            return apiCountByGroupId(groupId);
        }

        public void AddMessageParser(Func<int, string, ConcurrentQueue<object>, bool> parser)
        {
            ThrowIfUnavailable();
            apiAddMessageParser(parser);
        }

        public void AddMessageReceiver(Func<object, bool> receiver)
        {
            ThrowIfUnavailable();
            apiAddMessageReceiver(receiver);
        }

        public void Send(object obj, int clientId = 0)
        {
            ThrowIfUnavailable();
            apiSend(clientId, obj);
        }

        public void Signal(int clientId = 0)
        {
            ThrowIfUnavailable();
            apiSignal(clientId);
        }

        public void SendWorldObject(WorldObject worldObject, int clientId = 0)
        {
            ThrowIfUnavailable();
            apiSendWorldObject(clientId, worldObject);
        }


        public void SendInventory(Inventory inventory, int clientId = 0)
        {
            ThrowIfUnavailable();
            apiSendInventory(clientId, inventory);
        }

        public void SuppressInventoryChangeWhile(Action action)
        {
            SuppressInventoryChangeWhile(null, obj => action());
        }

        public void SuppressInventoryChangeWhile(object parameter, Action<object> actionWithParameter)
        {
            ThrowIfUnavailable();
            apiSuppressInventoryChangeWhile(parameter, actionWithParameter);
        }

        public Inventory GetClientBackpack(int clientId = 0)
        {
            ThrowIfUnavailable();
            return apiGetClientBackpack(clientId);
        }

        public Inventory GetClientEquipment(int clientId = 0)
        {
            ThrowIfUnavailable();
            return apiGetClientEquipment(clientId);
        }

        public void LogDebug(object obj)
        {
            ThrowIfUnavailable();
            apiLogDebug(obj);
        }

        public void LogInfo(object obj)
        {
            ThrowIfUnavailable();
            apiLogInfo(obj);
        }

        public void LogWarning(object obj)
        {
            ThrowIfUnavailable();
            apiLogWarning(obj);
        }

        public void LogError(object obj)
        {
            ThrowIfUnavailable();
            apiLogError(obj);
        }

        public string GetClientData(string key)
        {
            ThrowIfUnavailable();
            return apiClientGetData(key);
        }

        public void SetClientData(string key, string value)
        {
            ThrowIfUnavailable();
            apiClientSetData(key, value);
        }

        public void RegisterClientDataReady(Action action)
        {
            ThrowIfUnavailable();
            apiClientRegisterDataReady(action);
        }

        public HostState GetHostState()
        {
            ThrowIfUnavailable();
            return apiGetHostState() switch
            {
                "Connected" => HostState.Connected,
                "Active" => HostState.Active,
                "Disconnected" => HostState.Disconnected,
                "Host" => HostState.Host,
                _ => HostState.None
            };
        }

        private void ThrowIfUnavailable()
        {
            if (!IsAvailable())
            {
                throw new InvalidOperationException("(Feat) Multiplayer not found or API not available");
            }
        }

        private static T GetApi<T>(BepInEx.PluginInfo pi, string name)
        {
            var fi = AccessTools.Field(pi.Instance.GetType(), name);
            if (fi == null)
            {
                throw new NullReferenceException("Missing field " + pi.Instance.GetType() + "." + name);
            }
            return (T)fi.GetValue(null);
        }
    }
}
