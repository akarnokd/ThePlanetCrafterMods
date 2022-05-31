using BepInEx;
using UnityEngine;
using System;

namespace FeatMultiplayer
{
    /// <summary>
    /// Multiplayer mod.
    /// </summary>
    [BepInPlugin(modFeatMultiplayerGuid, "(Feat) Multiplayer", "0.1.0.0")]
    [BepInDependency(modCheatInventoryStackingGuid, BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency(modCheatMachineRemoteDepositGuid, BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency(modCheatAutoHarvestGuid, BepInDependency.DependencyFlags.SoftDependency)]
    public partial class Plugin : BaseUnityPlugin
    {
        const string modFeatMultiplayerGuid = "akarnokd.theplanetcraftermods.featmultiplayer";
        const string modCheatInventoryStackingGuid = "akarnokd.theplanetcraftermods.cheatinventorystacking";
        const string modCheatMachineRemoteDepositGuid = "akarnokd.theplanetcraftermods.cheatmachineremotedeposit";
        const string modCheatAutoHarvestGuid = "akarnokd.theplanetcraftermods.cheatautoharvest";

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
        static float lastFullSync;
        /// <summary>
        /// Time when the last small sync happened.
        /// </summary>
        static float lastSmallSync;

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
                try
                {
                    if (now - lastFullSync >= fullSyncDelay.Value / 1000f)
                    {
                        lastFullSync = now;
                        SendFullState();
                    }
                    if (now - lastSmallSync >= smallSyncDelay.Value / 1000f)
                    {
                        lastSmallSync = now;
                        SendPeriodicState();
                    }
                } catch (Exception ex)
                {
                    LogError(ex);
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
