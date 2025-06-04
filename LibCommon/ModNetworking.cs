// Copyright (c) 2022-2025, David Karnok & Contributors
// Licensed under the Apache License, Version 2.0

using BepInEx.Logging;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Netcode;
using SpaceCraft;

namespace LibCommon
{
    /// <summary>
    /// Utility class to send messages to the host or client(s) and host
    /// per function callbacks to handle received messages.
    /// <para>
    /// Usage:
    /// Call <see cref="Init(string, ManualLogSource)"/> and <see cref="Patch(Harmony)"/>,
    /// then call <see cref="RegisterFunction(string, Action{ulong, string})"/> with as many
    /// calls as you want, all from within your mods Awake() method still.
    /// </para>
    /// <para>
    /// Register a function with name <see cref="FunctionClientConnected"/> and/or
    /// <see cref="FunctionClientDisconnected"/> to be notified when a connection
    /// has been established or torn down.
    /// </para>
    /// </summary>
    public sealed class ModNetworking
    {
        static string _modGuid;

        static ManualLogSource _logger;

        public static bool _debugMode;

        static readonly Dictionary<string, Action<ulong, string>> functionCallbacks = [];

        // -----------------------------------------------------------------------------------------
        // Public API
        // -----------------------------------------------------------------------------------------

        /// <summary>
        /// When a client is connected, the callback named as this function will be called with the client id
        /// and user id as argument.
        /// </summary>
        public const string FunctionClientConnected = "ClientConnected";

        /// <summary>
        /// When a client is disconnected, the callback named as this function will be called with the client id
        /// and user id as argument.
        /// </summary>
        public const string FunctionClientDisconnected = "ClientDisconnected";

        /// <summary>
        /// Register a callback for a specific function name.
        /// </summary>
        /// <param name="functionName"></param>
        /// <param name="callback"></param>
        /// <exception cref="ArgumentNullException"></exception>
        public static void RegisterFunction(string functionName, Action<ulong, string> callback)
        {
            if (functionName == null)
            {
                throw new ArgumentNullException(nameof(functionName));
            }
            if (callback == null)
            {
                throw new ArgumentNullException(nameof(callback));
            }
            if (_debugMode)
            {
                _logger.LogInfo("Registering ModNetworking Function: " + functionName);
            }

            functionCallbacks.TryGetValue(functionName, out var fre);
            if (fre == null)
            {
                functionCallbacks[functionName] = callback;
            }
            else
            {
                functionCallbacks[functionName] += callback;
            }
        }

        /// <summary>
        /// Initialize this utility. 
        /// Call it from your mod's Awake() method.
        /// </summary>
        /// <param name="modGuid"></param>
        /// <param name="logger"></param>
        public static void Init(string modGuid, ManualLogSource logger)
        {
            _modGuid = modGuid;
            _logger = logger;
        }

        /// <summary>
        /// Patch one of the game's known network behavior to detect when
        /// networking becomes available.
        /// Call it from your mod's Awake() method.
        /// </summary>
        /// <param name="harmony"></param>
        public static void Patch(Harmony harmony)
        {
            harmony.PatchAll(typeof(ModNetworking));
        }

        /// <summary>
        /// Call a function with the given arguments on all clients.
        /// </summary>
        /// <param name="function"></param>
        /// <param name="arguments"></param>
        public static void SendAllClients(string function, string arguments)
        {
            var nm = RequireServer(function);
            var cmm = nm.CustomMessagingManager;

            using FastBufferWriter writer = CreateFastBufferWriter(function, arguments);
            writer.WriteValueSafe(function);
            writer.WriteValueSafe(arguments);

            if (_debugMode)
            {
                _logger.LogInfo("SendAllClients.connectedClientIds: " + string.Join(", ", nm.ConnectedClientsIds));
            }
            var list = nm.ConnectedClientsIds.Where(id => id != NetworkManager.ServerClientId).ToList().AsReadOnly();

            cmm.SendNamedMessage(_modGuid, list, writer);
        }

        /// <summary>
        /// Call a function with the given arguments for one specific client.
        /// </summary>
        /// <param name="function"></param>
        /// <param name="arguments"></param>
        public static void SendClient(ulong clientId, string function, string arguments)
        {
            var nm = RequireServer(function);
            var cmm = nm.CustomMessagingManager;

            using FastBufferWriter writer = CreateFastBufferWriter(function, arguments);
            writer.WriteValueSafe(function);
            writer.WriteValueSafe(arguments);

            cmm.SendNamedMessage(_modGuid, clientId, writer);
        }

        /// <summary>
        /// Call a function with the given arguments for all clients except one specific client.
        /// </summary>
        /// <param name="function"></param>
        /// <param name="arguments"></param>
        public static void SendAllClientsExcept(ulong clientId, string function, string arguments)
        {
            var nm = RequireServer(function);
            var cmm = nm.CustomMessagingManager;

            using FastBufferWriter writer = CreateFastBufferWriter(function, arguments);
            writer.WriteValueSafe(function);
            writer.WriteValueSafe(arguments);

            var list = nm.ConnectedClientsIds.Where(id => id != clientId).ToList().AsReadOnly();

            cmm.SendNamedMessage(_modGuid, list, writer);
        }

        /// <summary>
        /// Call a function with the given arguments on the host.
        /// </summary>
        /// <param name="function"></param>
        /// <param name="arguments"></param>
        public static void SendHost(string function, string arguments)
        {
            var nm = RequireClient(function);
            var cmm = nm.CustomMessagingManager;

            using FastBufferWriter writer = CreateFastBufferWriter(function, arguments);
            writer.WriteValueSafe(function);
            writer.WriteValueSafe(arguments);

            cmm.SendNamedMessage(_modGuid, NetworkManager.ServerClientId, writer);
        }

        /// <summary>
        /// Returns true if networking is available and the caller is on the client side.
        /// </summary>
        /// <returns></returns>
        public static bool CanSendToHost()
        {
            var nm = NetworkManager.Singleton;
            if (nm == null)
            {
                return false;
            }
            return !nm.IsServer;
        }

        /// <summary>
        /// Returns true if networking is available and the caller is on the host side.
        /// </summary>
        /// <returns></returns>
        public static bool CanSendToAnyClient()
        {
            var nm = NetworkManager.Singleton;
            if (nm == null)
            {
                return false;
            }
            return nm.IsServer;
        }

        // ------------------------------------------------------------------------------------
        // Internals
        // ------------------------------------------------------------------------------------

        static void Attach()
        {
            var nm = NetworkManager.Singleton;

            if (_debugMode)
            {
                _logger.LogInfo("ModNetworking Attaching [" + nm.LocalClientId + "]");
            }
            var cmm = nm.CustomMessagingManager;

            cmm.RegisterNamedMessageHandler(_modGuid, HandleMessage);

            var pm = Managers.GetManager<PlayersManager>();

            pm.RegisterToPlayerStarted(pmc =>
            {
                DoCallback(pmc.OwnerClientId, FunctionClientConnected, pmc.playerName);
            });
            pm.RegisterToPlayerStopped(pmc =>
            {
                /*
                if (_debugMode)
                {
                    _logger.LogInfo(Environment.StackTrace);
                }
                */
                DoCallback(pmc.OwnerClientId, FunctionClientDisconnected, pmc.playerName);
            });

            nm.OnClientConnectedCallback += id =>
            {
                if (_debugMode)
                {
                    _logger.LogInfo("ModNetworking.OnClientConnectedCallback [" + nm.LocalClientId + "] : " + id);
                }
            };
            nm.OnClientDisconnectCallback += id =>
            {
                if (_debugMode)
                {
                    _logger.LogInfo("ModNetworking.OnClientDisconnectedCallback [" + nm.LocalClientId + "] : " + id);
                }
            };
        }

        static void Detach()
        {
            var nm = NetworkManager.Singleton;
            if (nm != null)
            {
                if (_debugMode)
                {
                    _logger.LogInfo("ModNetworking Detaching [" + nm.LocalClientId + "]");
                }
                var cmm = nm.CustomMessagingManager;
                if (cmm != null)
                {
                    cmm.UnregisterNamedMessageHandler(_modGuid);
                }
            }
        }

        static NetworkManager RequireServer(string function)
        {
            var nm = NetworkManager.Singleton ?? throw new InvalidOperationException("Unable to call function " + function + ". NetworkManager is not running.");

            if (!nm.IsServer)
            {
                throw new InvalidOperationException("Unable to call function " + function + ". You are not on the server.");
            }
            return nm;
        }

        static NetworkManager RequireClient(string function)
        {
            var nm = NetworkManager.Singleton ?? throw new InvalidOperationException("Unable to call function " + function + ". NetworkManager is not running.");

            if (nm.IsServer)
            {
                throw new InvalidOperationException("Unable to call function " + function + ". You are not on the client.");
            }
            return nm;
        }

        static FastBufferWriter CreateFastBufferWriter(string function, string arguments)
        {
            return new(FastBufferWriter.GetWriteSize(function) + FastBufferWriter.GetWriteSize(arguments), 
                Unity.Collections.Allocator.Temp);
        }

        // ------------------------------------------------------------------------------------
        // Patches
        // ------------------------------------------------------------------------------------

        [HarmonyPostfix]
        [HarmonyPatch(typeof(PlayersDataManager), nameof(PlayersDataManager.OnNetworkSpawn))]
        static void Patch_PlayersDataManager_OnNetworkSpawn()
        {
            Attach();
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(PlayersDataManager), nameof(PlayersDataManager.OnDestroy))]
        static void Patch_PlayersDataManager_OnDestroy(PlayersDataManager __instance)
        {
            if (__instance == PlayersDataManager.Instance)
            {
                Detach();
            }
        }

        // --------------------------------------------------------------------------------
        // Callback handlers for the NetworkManager/CustomMessageManager
        // --------------------------------------------------------------------------------

        static void HandleMessage(ulong senderClientId, FastBufferReader messagePayload) 
        {
            messagePayload.ReadValueSafe(out string function);
            messagePayload.ReadValueSafe(out string arguments);

            DoCallback(senderClientId, function, arguments);
        }

        static void DoCallback(ulong senderClientId, string function, string arguments) 
        {
            if (_debugMode)
            {
                _logger.LogInfo("ModNetworking Incoming: [" + NetworkManager.Singleton.LocalClientId + "] " + senderClientId + " " + function + " " + arguments);    
            }
            if (functionCallbacks.TryGetValue(function, out var callback))
            {
                try
                {
                    callback(senderClientId, arguments);
                }
                catch (Exception ex)
                {
                    _logger.LogError("[" + NetworkManager.Singleton.LocalClientId + "] Crash while handling message from "
                        + senderClientId + " with function " + function + " (argument.Length = " + arguments.Length + ")"
                        + Environment.NewLine + ex
                        );
                }
            }
            else
            {
                if (_debugMode)
                {
                    _logger.LogWarning("[" + NetworkManager.Singleton.LocalClientId + "] No callback for received message from " + senderClientId + " with function " + function + " (argument.Length = " + arguments.Length + ")");
                }
            }
        }
    }
}
