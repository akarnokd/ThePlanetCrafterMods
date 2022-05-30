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
    /// <summary>
    /// Multiplayer mod.
    /// </summary>
    [BepInPlugin("akarnokd.theplanetcraftermods.featmultiplayer", "(Feat) Multiplayer", "1.0.0.0")]
    [BepInDependency("akarnokd.theplanetcraftermods.cheatinventorystacking", BepInDependency.DependencyFlags.SoftDependency)]
    public partial class Plugin : BaseUnityPlugin
    {

        /// <summary>
        /// The current game mode.
        /// </summary>
        static volatile MultiplayerMode updateMode = MultiplayerMode.MainMenu;

        /// <summary>
        /// The available game modes.
        /// </summary>
        enum MultiplayerMode
        {
            MainMenu,
            SinglePlayer,
            CoopHost,
            CoopClient
        }

        /// <summary>
        /// Last time the small updates were synced.
        /// </summary>
        static float lastNeworkSync;
        /// <summary>
        /// Last time when the full state has been synced.
        /// </summary>
        static float lastHostSync;

        /// <summary>
        /// The player's avatar, also doubles as the other player presence indicator.
        /// </summary>
        internal static PlayerAvatar otherPlayer;

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
        /// The main update, called by Unity on each frame.
        /// </summary>
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

        /// <summary>
        /// Exchanges messages between this and the other party.
        /// </summary>
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
                        UIDispatchMessage(message);
                    }
                    catch (Exception ex)
                    {
                        LogError(ex);
                    }
                }
            }
        }
    }
}
