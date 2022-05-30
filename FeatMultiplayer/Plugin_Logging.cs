using BepInEx;
using BepInEx.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace FeatMultiplayer
{
    public partial class Plugin : BaseUnityPlugin
    {
        /// <summary>
        /// The global logger. Avoid using it because if the game is run on the same machine multiple times,
        /// each of them will try to write to the same log file and end up not logging or breaking the log file.
        /// 
        /// Use the <see cref="LogInfo(object)"/>, <see cref="LogWarning(object)"/> or <see cref="LogError(object)"/>
        /// which will instead work on a separate host/client log file.
        /// </summary>
        internal static ManualLogSource theLogger;

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
