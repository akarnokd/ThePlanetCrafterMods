using BepInEx;
using BepInEx.Bootstrap;
using BepInEx.Logging;
using HarmonyLib;
using System;
using System.Linq;
using System.Reflection;
using System.Xml.Linq;
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
                Debug.Log("");
                Debug.LogWarning("Fixing BepInEx Logging");

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

                    WriteStringToUnityLog = (Action<string>)Delegate.CreateDelegate(typeof(Action<string>), methodInfo);
                    break;
                }

                if (WriteStringToUnityLog == null)
                {
                    Debug.LogWarning("   Unable to fix BepInEx Logging");
                }
                else
                {
                    field.SetValue(null, WriteStringToUnityLog);
                    
                    var ver = typeof(Paths).Assembly.GetName().Version;
                    Debug.Log("  BepInEx version   : " + ver);
                    Debug.Log("  Application       : " + Application.productName + " (" + Application.version + ")");
                    Debug.Log("  Unity version     : " + Application.unityVersion);
                    Debug.Log("  Runtime version   : " + System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription);
                    Debug.Log("  CLR version       : " + Environment.Version);
                    Debug.Log("  System & Platform : " + System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture + ", " + System.Runtime.InteropServices.RuntimeInformation.OSDescription);
                    Debug.Log("  Processor         : " + wmic("cpu get name") ?? "Unknown");
                    var mem = wmic("ComputerSystem get TotalPhysicalMemory");
                    var memgb = "Unknown";
                    if (mem != null && long.TryParse(mem, out var gb))
                    {
                        memgb = (gb / 1024.0 / 1024.0 / 1024.0).ToString("#,##0");
                    }
                    Debug.Log("  Cores & Memory    : " + Environment.ProcessorCount + " threads, " + memgb + " GB RAM");
                    Debug.Log("  Plugins to load   : " + Chainloader.PluginInfos.Count);
                }
                Debug.Log("");
            }
        }

        private static string wmic(string query)
        {
            try
            {
                System.Diagnostics.ProcessStartInfo startinfo = new();
                startinfo.FileName = @"wmic";
                startinfo.Arguments = query;

                System.Diagnostics.Process process = new();
                process.StartInfo = startinfo;
                process.StartInfo.UseShellExecute = false;
                process.StartInfo.RedirectStandardInput = true;
                process.StartInfo.RedirectStandardOutput = true;
                process.Start();
                var str = process.StandardOutput.ReadToEnd().Split('\n');

                return str[1].Replace("\n", "").Replace("\r", "");
            } 
            catch
            {
                return null;
            }
        }
    }
}
