// Copyright (c) 2022-2024, David Karnok & Contributors
// Licensed under the Apache License, Version 2.0

using BepInEx;
using BepInEx.Bootstrap;
using BepInEx.Logging;
using HarmonyLib;
using System;
using System.IO;
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
                    Debug.Log("");
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
                    Debug.Log("  Processor         : " + RunWMIC("cpu get name") ?? "Unknown");
                    var mem = RunWMIC("ComputerSystem get TotalPhysicalMemory");
                    var memgb = "Unknown";
                    if (mem != null && long.TryParse(mem, out var gb))
                    {
                        memgb = (gb / 1024.0 / 1024.0 / 1024.0).ToString("#,##0");
                    }
                    Debug.Log("  Cores & Memory    : " + Environment.ProcessorCount + " threads, " + memgb + " GB RAM");
                    
                    ApplyAchievementWorkaround();
                    
                    Debug.Log("");
                    foreach (var mod in Chainloader.PluginInfos.Values)
                    {
                        Debug.Log("[Info   :   BepInEx] Loading [" + mod.Metadata.Name + " " + mod.Metadata.Version + "]");
                    }
                }
            }
        }

        private static string RunWMIC(string query)
        {
            try
            {
                System.Diagnostics.ProcessStartInfo startinfo = new()
                {
                    FileName = @"wmic",
                    Arguments = query
                };

                System.Diagnostics.Process process = new()
                {
                    StartInfo = startinfo
                };
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

        static void ApplyAchievementWorkaround()
        {
            var loc = Assembly.GetExecutingAssembly().Location;
            var dir = loc.LastIndexOf("BepInEx");
            if (dir != -1)
            {
                var target = loc[..dir] + "/Planet Crafter_Data/Plugins/" + OfArchitecture() + "/" + OfPlatform();
                var fi = new FileInfo(target);
                if (fi.Exists && fi.Length / 1024 < 300)
                {
                    Debug.Log("  Achievements      : Enabled");
                }
                else
                {
                    Debug.Log("  Acheivements      : Active");
                }
            }
            else
            {
                Debug.Log("  Achievements      : Disabled");
            }
        }

        internal static string OfArchitecture()
        {
            return "x86_" + OfSubArchitecture();
        }

        static string OfSubArchitecture()
        {
            return "64";
        }

        internal static string OfPlatform()
        {
            return "steam_" + OfBits();
        }
        static string OfBits()
        {
            return "api64" + OfContainer();
        }
        static string OfContainer()
        {
            return ".dll";
        }
    }
}
