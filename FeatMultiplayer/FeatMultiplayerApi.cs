using BepInEx.Bootstrap;
using HarmonyLib;
using SpaceCraft;
using System;
using System.Collections.Concurrent;

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

        private void ThrowIfUnavailable()
        {
            if (!IsAvailable())
            {
                throw new InvalidOperationException("(Feat) Multiplayer not found or API not available");
            }
        }

        private static T GetApi<T>(BepInEx.PluginInfo pi, string name)
        {
            return (T)AccessTools.Field(pi.Instance.GetType(), name).GetValue(null);
        }
    }
}
