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

        static void LogInternal(object message, int level)
        {
            if (updateMode == MultiplayerMode.SinglePlayer || updateMode == MultiplayerMode.MainMenu)
            {
                switch (level)
                {
                    case 1:
                        {
                            theLogger.LogInfo(message);
                            break;
                        }
                    case 2:
                        {
                            theLogger.LogWarning(message);
                            break;
                        }
                    case 3:
                        {
                            theLogger.LogError(message);
                            break;
                        }
                    default:
                        {
                            theLogger.LogDebug(message);
                            break;
                        }
                }
            }
            else
            {
                string fileName = Application.persistentDataPath + "\\Player_";
                int logLevel;
                if (updateMode == MultiplayerMode.CoopClient)
                {
                    if (clientJoinName != null)
                    {
                        fileName += "Client_" + clientJoinName + ".log";
                    }
                    else
                    {
                        fileName += "Client.log";
                    }
                    logLevel = clientLogLevel.Value;
                } else
                {
                    fileName += "Host.log";
                    logLevel = hostLogLevel.Value;
                }
                if (level >= logLevel)
                {
                    string prefix = level switch
                    {
                        1 => "[Info:    (Feat) Multiplayer @ ",
                        2 => "[Warning: (Feat) Multiplayer @ ",
                        3 => "[Error:   (Feat) Multiplayer @ ",
                        4 => "[Fatal:   (Feat) Multiplayer @ ",
                        5 => "[Always:  (Feat) Multiplayer @ ",
                        _ => "[Debug:   (Feat) Multiplayer @ "
                    };

                    prefix += DateTimeOffset.Now.ToString("HH:mm:ss.fff") + " ] ";
                    lock (logLock)
                    {
                        File.AppendAllText(fileName, prefix + message + "\r\n");
                    }
                }
            }

        }

        internal static void ClearLogs()
        {
            string fileName = Application.persistentDataPath + "\\Player_";
            if (updateMode == MultiplayerMode.CoopClient)
            {
                if (clientJoinName != null)
                {
                    fileName += "Client_" + clientJoinName + ".log";
                }
                else
                {
                    fileName += "Client.log";
                }
            }
            else
            {
                fileName += "Host.log";
            }

            File.Delete(fileName);
        }

        internal static void LogDebug(object message)
        {
            LogInternal(message, 0);
        }

        internal static void LogInfo(object message)
        {
            LogInternal(message, 1);
        }

        internal static void LogError(object message)
        {
            LogInternal(message, 3);
        }
        internal static void LogWarning(object message)
        {
            LogInternal(message, 2);
        }
        internal static void LogAlways(object message)
        {
            LogInternal(message, 5);
        }
    }
}
