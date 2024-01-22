using BepInEx.Logging;
using HarmonyLib;
using System;
using System.Reflection;
using UnityEngine;

namespace LibCommon
{
    /// <summary>
    /// Workaround for a problem with BepInEx 5.4.22 and Unity 2023.2.4f1 (or any 2023 versions)
    /// in which the BepInEx logs do not show up in the Player.log because the UnityLogListener
    /// of BepInEx can't find the corret Unity logging method, maybe because it runs too early.
    /// </summary>
    public static class BepInExLoggerFix
    {
        /// <summary>
        /// Apply the logging fix if not already working (i.e., UnityLogListener.WriteStringToUnity is still null).
        /// </summary>
        public static void ApplyFix()
        {
            var field = AccessTools.DeclaredField(typeof(UnityLogListener), "WriteStringToUnityLog");
            if (field.GetValue(null) == null)
            {
                Debug.LogWarning("WriteStringToUnityLog is not set, trying to fix that.");

                var WriteStringToUnityLog = default(Action<string>);

                MethodInfo[] methods = typeof(UnityLogWriter).GetMethods(BindingFlags.Public | BindingFlags.Static);
                foreach (MethodInfo methodInfo in methods)
                {
                    try
                    {
                        methodInfo.Invoke(null, [""]);
                    }
                    catch
                    {
                        continue;
                    }

                    Debug.Log("WriteStringToUnityLog Found as " + methodInfo.ToString());
                    WriteStringToUnityLog = (Action<string>)Delegate.CreateDelegate(typeof(Action<string>), methodInfo);
                    break;
                }

                if (WriteStringToUnityLog == null)
                {
                    Debug.LogWarning("WriteStringToUnityLog not found");
                }
                else
                {
                    field.SetValue(null, WriteStringToUnityLog);
                }
            }
        }
    }
}
