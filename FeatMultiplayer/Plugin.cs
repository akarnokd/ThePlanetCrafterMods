using BepInEx;
using UnityEngine;
using System;
using UnityEngine.InputSystem;
using SpaceCraft;

namespace FeatMultiplayer
{
    /// <summary>
    /// Multiplayer mod.
    /// </summary>
    [BepInPlugin(modFeatMultiplayerGuid, "(Feat) Multiplayer", PluginInfo.PLUGIN_VERSION)]
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
        /// The main update, called by Unity on each frame.
        /// </summary>
        void Update()
        {
            if (updateMode == MultiplayerMode.MainMenu)
            {
                DoMainMenuUpdate();
                CheckLogKeys();
            }
            if (updateMode == MultiplayerMode.CoopHost || updateMode == MultiplayerMode.CoopClient)
            {
                DoMultiplayerUpdate();
                var ap = GetPlayerMainController();
                WindowsHandler wh = Managers.GetManager<WindowsHandler>();
                if (ap != null && wh != null && !wh.GetHasUiOpen() && ap.GetPlayerInputDispatcher().IsPressingAccessibilityKey())
                {
                    /*
                    FIXME Which player?
                    if (otherPlayer != null && Keyboard.current.tKey.wasPressedThisFrame)
                    {
                        LogInfo("Teleporting to the other player");
                        var apc = GetPlayerMainController();
                        apc.SetPlayerPlacement(otherPlayer.avatar.transform.position, apc.transform.rotation);
                    }
                     */
                    if (updateMode == MultiplayerMode.CoopHost && Keyboard.current.iKey.wasPressedThisFrame)
                    {
                        SetupHostInventory();
                    }
                    if (updateMode == MultiplayerMode.CoopHost && Keyboard.current.pKey.wasPressedThisFrame)
                    {
                        LaunchAllMeteorEvents();
                    }

                    if (Keyboard.current.hKey.wasPressedThisFrame)
                    {
                        ToggleConsumption();
                    }
                    CheckLogKeys();
                }
                HandleEmoting();

                HandleOverlays();
            }
            else
            {
            }
        }

        static void CheckLogKeys()
        {
            if (Keyboard.current.lKey.wasPressedThisFrame)
            {
                if (Keyboard.current.shiftKey.IsPressed())
                {
                    ClearLogs();
                    Managers.GetManager<BaseHudHandler>()?.DisplayCursorText("", 2f, "Log cleared");
                }
                else
                {
                    if (updateMode == MultiplayerMode.CoopHost)
                    {
                        hostLogLevel.Value = hostLogLevel.Value == 1 ? 2 : 1;
                        Managers.GetManager<BaseHudHandler>()?.DisplayCursorText("", 2f, "Host Loglevel set to " + hostLogLevel.Value);
                    }
                    if (updateMode == MultiplayerMode.CoopClient)
                    {
                        clientLogLevel.Value = clientLogLevel.Value == 1 ? 2 : 1;
                        Managers.GetManager<BaseHudHandler>()?.DisplayCursorText("", 2f, "Client Loglevel set to " + clientLogLevel.Value);
                    }
                }
            }
        }

        /// <summary>
        /// Exchanges messages between this and the other party.
        /// </summary>
        void DoMultiplayerUpdate()
        {
            var now = Time.realtimeSinceStartup;

            if (updateMode == MultiplayerMode.CoopHost && !_clientConnections.IsEmpty)
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
                SendPlayerLocation();

                // Receive and apply commands
                while (_receiveQueue.TryDequeue(out var message))
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
