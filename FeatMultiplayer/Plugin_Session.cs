using BepInEx;
using HarmonyLib;
using SpaceCraft;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FeatMultiplayer
{
    public partial class Plugin : BaseUnityPlugin
    {
        static string multiplayerFilename = "Survival-9999999";

        static string clientJoinName;
        static string clientJoinPassword;

        static Dictionary<string, PlayerAvatar> playerAvatars = new();

        void CreateMultiplayerSaveAndEnter()
        {

            File.Delete(Application.persistentDataPath + "/" + multiplayerFilename + ".json");

            Managers.GetManager<StaticDataHandler>().LoadStaticData();

            // avoid random positioning
            List<PositionAndRotation> backupStartingPositions = new();
            backupStartingPositions.AddRange(GameConfig.positionsForEscapePod);
            try
            {
                GameConfig.positionsForEscapePod.RemoveRange(1, GameConfig.positionsForEscapePod.Count - 1);
                JsonableGameState gs = new JsonableGameState();
                // FIXME 0.9.x introduced a lot more game settings
                gs.gameMode = DataConfig.GameSettingMode.Chill;
                gs.gameStartLocation = DataConfig.GameSettingStartLocation.Standard;
                JSONExport.CreateNewSaveFile(multiplayerFilename, gs);
            }
            finally
            {
                GameConfig.positionsForEscapePod.Clear();
                GameConfig.positionsForEscapePod.AddRange(backupStartingPositions);
            }

            Managers.GetManager<SavedDataHandler>().SetSaveFileName(multiplayerFilename);
            SceneManager.LoadScene(GameConfig.mainSceneName);

            LogInfo("Find SaveFilesSelector");
            var selector = FindFirstObjectByType<SaveFilesSelector>();
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

        [HarmonyPostfix]
        [HarmonyPatch(typeof(SessionController), "Start")]
        static void SessionController_Start()
        {
            _receiveQueue.Clear();

            if (updateMode == MultiplayerMode.CoopHost)
            {
                clientJoinName = null;
                clientJoinPassword = null;
                LogInfo("Entering world as Host");
                Managers.GetManager<PlanetLoader>().planetIsLoaded += EnterHostAsync;
            }
            else if (updateMode == MultiplayerMode.CoopClient)
            {
                LogInfo("Entering world as Client");
                // we don't need the savefile
                File.Delete(Application.persistentDataPath + "/" + multiplayerFilename + ".json");

                Managers.GetManager<PlanetLoader>().planetIsLoaded += EnterClientAsync;
            }
            else
            {
                LogInfo("Entering world as Solo");
            }
        }

        static void EnterHostAsync()
        {
            LogInfo("Starting Host network Listener");
            StartAsHost();
            LaunchStuckRockets();
        }

        static void EnterClientAsync()
        {
            LogInfo("Initiating connection to host");
            StartAsClient();
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(PlanetLoader), "HandleDataAfterLoad")]
        static void PlanetLoader_HandleDataAfterLoad(ref PlanetIsLoaded ___planetIsLoaded)
        {
            ___planetIsLoaded = null;
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(UiWindowPause), nameof(UiWindowPause.OnQuit))]
        static void UiWindowPause_OnQuit()
        {
            stopNetwork?.Cancel();
            UnblockNetwork();

            updateMode = MultiplayerMode.MainMenu;

            DestroyAvatars();

            _receiveQueue.Clear();

            inventorySpawning.Clear();

            worldObjectById.Clear();
            countByGroupId.Clear();
            
            inventoryById.Clear();

            rocketsInFlight.Clear();

            hiddenRocketInventories.Clear();

            larvaeGroupIds.Clear();

            cellsInCircle?.Clear();

            droneTargetCache.Clear();

            droneSupplyCount = 0;
            droneDemandCount = 0;

            playerListGameObjects.Clear();
            healthGaugeTransform = null;
            waterGaugeTransform = null;

            clientJoinName = null;
            clientJoinPassword = null;
        }

        void OnApplicationQuit()
        {
            LogInfo("Application quit");
            stopNetwork?.Cancel();
            UnblockNetwork();
            WaitForNetwork();
        }

        static void UnblockNetwork()
        {
            if (updateMode == MultiplayerMode.CoopHost)
            {
                SignalAllClients();
            }
            else if (updateMode == MultiplayerMode.CoopClient)
            {
                SignalHost();
            }
        }

        static void WaitForNetwork()
        {
            for (int i = 0; i < 20; i++)
            {
                if (updateMode == MultiplayerMode.CoopHost)
                {
                    if (_clientConnections.IsEmpty)
                    {
                        break;
                    }
                }
                else if (updateMode == MultiplayerMode.CoopClient)
                {
                    if (_towardsHost == null)
                    {
                        break;
                    }
                }
                else
                {
                    break;
                }
                Thread.Sleep(100);
            }
        }

        static void DestroyAvatars()
        {
            foreach (var otherPlayer in playerAvatars)
            {
                otherPlayer.Value.Destroy();
            }
            playerAvatars.Clear();
        }
    }
}
