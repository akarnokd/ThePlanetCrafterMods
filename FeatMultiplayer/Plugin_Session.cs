using BepInEx;
using HarmonyLib;
using MijuTools;
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
                LaunchStuckRockets();
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

            inventorySpawning.Clear();

            worldObjectById.Clear();
            countByGroupId.Clear();
            
            inventoryById.Clear();

            rocketsInFlight.Clear();

            hiddenRocketInventories.Clear();

            larvaeGroupIds.Clear();

            cellsInCircle.Clear();
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

    }
}
