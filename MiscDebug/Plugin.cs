using BepInEx;
using SpaceCraft;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;
using System.Reflection;
using System.IO;
using System;
using System.Globalization;
using BepInEx.Logging;

namespace MiscDebug
{
    [BepInPlugin("akarnokd.theplanetcraftermods.miscdebug", "(Misc) Debug", PluginInfo.PLUGIN_VERSION)]
    public class Plugin : BaseUnityPlugin
    {

        private void Awake()
        {
            FixBepInExLogging();
        }

        void FixBepInExLogging()
        {
            // Plugin startup logic
            Logger.LogInfo("Checking UnityLogWriter for a string method");

            var WriteStringToUnityLog = default(Action<string>);

            MethodInfo[] methods = typeof(UnityLogWriter).GetMethods(BindingFlags.Public | BindingFlags.Static);
            foreach (MethodInfo methodInfo in methods)
            {
                try
                {
                    methodInfo.Invoke(null, new object[1] { "" });
                }
                catch
                {
                    continue;
                }

                Logger.LogInfo("Found " + methodInfo.Name);
                WriteStringToUnityLog = (Action<string>)Delegate.CreateDelegate(typeof(Action<string>), methodInfo);
                break;
            }

            if (WriteStringToUnityLog == null)
            {
                Logger.LogError("Unable to start Unity log writer");
            }
            else
            {
                var fa = AccessTools.DeclaredField(typeof(UnityLogListener), "WriteStringToUnityLog");
                fa.SetValue(null, WriteStringToUnityLog);
            }
        }
    }
}
