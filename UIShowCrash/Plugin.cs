// Copyright (c) 2022-2025, David Karnok & Contributors
// Licensed under the Apache License, Version 2.0

using BepInEx;
using System.Collections.Generic;
using BepInEx.Logging;
using BepInEx.Configuration;
using System.IO;
using System;
using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using System.Reflection;

namespace UIShowCrash
{
    [BepInPlugin("akarnokd.theplanetcraftermods.uishowcrash", "(UI) Show Crash", PluginInfo.PLUGIN_VERSION)]
    public class Plugin : BaseUnityPlugin
    {

        static ManualLogSource logger;

        static ConfigEntry<bool> modEnabled;
        static ConfigEntry<int> fontSize;
        static ConfigEntry<bool> testMode;

        static volatile bool panelVisible;

        static readonly ConcurrentQueue<List<string>> errorQueue = new();

        static List<string> lines;

        static readonly CancellationTokenSource cancel = new();

        Task backgroundTask;

        static bool oncePerFrame;

        public void Awake()
        {
            LibCommon.BepInExLoggerFix.ApplyFix();

            // Plugin startup logic
            Logger.LogInfo($"Plugin is loaded!");

            logger = Logger;

            modEnabled = Config.Bind("General", "Enabled", true, "Is this mod enabled?");
            fontSize = Config.Bind("General", "FontSize", 20, "The font size");
            testMode = Config.Bind("General", "TestMode", false, "Press F11 to generate a crash log entry.");

            backgroundTask = Task.Factory.StartNew(o => ErrorChecker(), null, cancel.Token, TaskCreationOptions.LongRunning, TaskScheduler.Default);
        }

        public void OnDestroy()
        {
            cancel.Cancel();
            backgroundTask.Wait(2000);
        }

        public void OnGUI()
        {
            oncePerFrame = !oncePerFrame;

            if (oncePerFrame && modEnabled.Value && !testMode.Value
                && Keyboard.current[Key.F11].wasPressedThisFrame && Keyboard.current[Key.LeftCtrl].isPressed)
            {
                errorQueue.Clear();
                return;
            }
            if (oncePerFrame && modEnabled.Value && !testMode.Value && Keyboard.current[Key.F11].wasPressedThisFrame)
            {
                logger.LogInfo("Turning off error log display.");
                modEnabled.Value = false;
                return;
            }
            if (oncePerFrame && !modEnabled.Value && !testMode.Value && Keyboard.current[Key.F11].wasPressedThisFrame)
            {
                logger.LogInfo("Turning on error log display.");
                modEnabled.Value = true;
                return;
            }
            if (!modEnabled.Value)
            {
                return;
            }

            if (oncePerFrame && testMode.Value && !panelVisible && Keyboard.current[Key.F11].wasPressedThisFrame)
            {
                logger.LogInfo("Testing error log detection");
                try
                {
                    throw new InvalidOperationException("F11 pressed?! @ " + DateTime.Now.ToString());
                }
                catch (Exception ex)
                {
                    logger.LogError(ex);
                }
                return;
            }

            if (Keyboard.current[Key.Escape].wasPressedThisFrame)
            {
                panelVisible = false;
                return;
            }

            if (!panelVisible && !errorQueue.IsEmpty)
            {
                if (!errorQueue.TryDequeue(out lines))
                {
                    return;
                }
                panelVisible = true;
            }

            if (!panelVisible)
            {
                return;
            }

            GUI.depth = -100;

            var px = 50;
            var py = 50;
            var ph = Screen.height - 100;
            var pw = Screen.width - 100;

            var panel = new Rect(px, py, pw, ph);

            GUIDrawRect(panel, new Color(0.7f, 0.2f, 0.2f));

            GUI.color = Color.white;

            GUIStyle title = new("label")
            {
                fontSize = fontSize.Value * 2,
                alignment = TextAnchor.MiddleLeft
            };

            GUI.Label(new Rect(px + 50, py + fontSize.Value, pw - 100, 2 * fontSize.Value + 10), "Crash!!! [ESC] - close, [F11] - toggle checks", title);

            var dy = px + 3 * (fontSize.Value + 10);

            GUIStyle labelStyle = new("label")
            {
                fontSize = fontSize.Value,
                alignment = TextAnchor.UpperLeft
            };

            GUI.Label(new Rect(px + 25, dy, pw - 50, ph - 4 * (fontSize.Value + 10)), string.Join("\n", lines), labelStyle);

        }

        void ErrorChecker()
        {
            logger.LogInfo("Begin log watching");
            while (!cancel.Token.IsCancellationRequested)
            {
                cancel.Token.WaitHandle.WaitOne(1000);

                if (errorQueue.IsEmpty)
                {
                    foreach (var file in Directory.GetFiles(Application.persistentDataPath, "Player*.log"))
                    {
                        if (!Path.GetFileName(file).ToLower().StartsWith("player-prev"))
                        {
                            ProcessLog(file);
                        }
                    }
                }
            }
            logger.LogInfo("Quit log watching");
        }

        readonly Dictionary<string, int> logClearedUpTo = [];

        void ProcessLog(string file)
        {
            //logger.LogInfo("Processing " + Path.GetFileName(file));
            try
            {
                List<string> data = [];

                var fs = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                using (var sr = new StreamReader(fs))
                {
                    for (; ; )
                    {
                        var r = sr.ReadLine();
                        if (r == null)
                        {
                            break;
                        }
                        data.Add(r);
                    }
                }

                if (!logClearedUpTo.TryGetValue(file, out var idx))
                {
                    idx = 0;
                }
                //logger.LogInfo("  Resuming file at line " + idx + " (" + Path.GetFileName(file) + ")");


                for (int i = idx; i < data.Count; i++)
                {
                    var line = data[i];
                    if (
                        line.Contains("Exception")
                        || line.StartsWith("[Warning:  HarmonyX]")
                        || line.StartsWith("[Error  :  HarmonyX]")
                    )
                    {
                        //logger.LogInfo("  Found on line " + i + "(" + Path.GetFileName(file) + ")");
                        List<string> errorLines = [
                            "Game version: " + Application.version + FileCheck(),
                            "",
                            Path.GetFileName(file) + " [" + (i + 1) + "]", "", 
                            line];

                        for (int j = i + 1; j < data.Count; j++)
                        {
                            var er = data[j].Trim();
                            if (er.StartsWith("at ") 
                                || er.StartsWith("Rethrow as ")
                                || er.Length == 0
                                || er.StartsWith("Unity.")
                                || er.StartsWith("BepInEx.")
                                || er.StartsWith("UnityEngine.")
                                || er.StartsWith("SpaceCraft.")
                                || er.StartsWith("UnityEngineInternal.")
                                || er.StartsWith("--- ")
                                || er.Contains("Exception")
                                || er.StartsWith("Parameter name:")
                            )
                            {
                                errorLines.Add(er);
                            }
                            else
                            {
                                logClearedUpTo[file] = j;
                                errorQueue.Enqueue(errorLines);
                                return;
                            }
                        }
                        logClearedUpTo[file] = data.Count;
                        errorQueue.Enqueue(errorLines);
                        return;
                    }
                }

                logClearedUpTo[file] = data.Count;
            } 
            catch (Exception ex)
            {
                logger.LogError(ex);
            }
        }

        private static Texture2D _staticRectTexture;
        private static GUIStyle _staticRectStyle;

        // Note that this function is only meant to be called from OnGUI() functions.
        public static void GUIDrawRect(Rect position, Color color)
        {
            if (_staticRectTexture == null)
            {
                _staticRectTexture = new Texture2D(1, 1);
            }

            _staticRectStyle ??= new GUIStyle
                {
                    alignment = TextAnchor.UpperLeft
                };

            _staticRectTexture.SetPixel(0, 0, color);
            _staticRectTexture.Apply();

            _staticRectStyle.normal.background = _staticRectTexture;

            GUI.Box(position, GUIContent.none, _staticRectStyle);
        }

        static string FileCheck()
        {
            var loc = Assembly.GetExecutingAssembly().Location;
            var dir = loc.LastIndexOf("BepInEx");
            if (dir != -1)
            {
                var target = loc[..dir] + "/Planet Crafter_Data/Plugins/" + LibCommon.BepInExLoggerFix.OfArchitecture() + "/" + LibCommon.BepInExLoggerFix.OfPlatform();
                var fi = new FileInfo(target);
                if (fi.Exists && fi.Length / 1024 < 300)
                {
                    return "!";
                }
                return "?";
            }
            return "×";
        }
    }
}
