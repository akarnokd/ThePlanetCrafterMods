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

        private void Awake()
        {
            // Plugin startup logic
            Logger.LogInfo($"Plugin is loaded!");

            logger = Logger;

            modEnabled = Config.Bind("General", "Enabled", true, "Is this mod enabled?");
            fontSize = Config.Bind("General", "FontSize", 20, "The font size");
            testMode = Config.Bind("General", "TestMode", false, "Press F12 to generate a crash log entry.");

            backgroundTask = Task.Factory.StartNew(o => ErrorChecker(), null, cancel.Token, TaskCreationOptions.LongRunning, TaskScheduler.Default);
        }

        void OnDestroy()
        {
            cancel.Cancel();
            backgroundTask.Wait(2000);
        }

        void OnGUI()
        {
            oncePerFrame = !oncePerFrame;

            if (oncePerFrame && modEnabled.Value && !testMode.Value && Keyboard.current[Key.F12].wasPressedThisFrame)
            {
                logger.LogInfo("Turning off error log display.");
                modEnabled.Value = false;
                return;
            }
            if (oncePerFrame && !modEnabled.Value && !testMode.Value && Keyboard.current[Key.F12].wasPressedThisFrame)
            {
                logger.LogInfo("Turning on error log display.");
                modEnabled.Value = true;
                return;
            }

            if (!modEnabled.Value)
            {
                return;
            }

            if (oncePerFrame && testMode.Value && !panelVisible && Keyboard.current[Key.F12].wasPressedThisFrame)
            {
                logger.LogInfo("Testing error log detection");
                try
                {
                    throw new InvalidOperationException("F12 pressed?! @ " + DateTime.Now.ToString());
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

            GUIStyle title = new GUIStyle("label");
            title.fontSize = fontSize.Value * 2;
            title.alignment = TextAnchor.MiddleLeft;

            GUI.Label(new Rect(px + 50, py + fontSize.Value, pw - 100, 2 * fontSize.Value + 10), "Crash!!! [ESC] to close this panel", title);

            var dy = px + 3 * (fontSize.Value + 10);

            GUIStyle labelStyle = new GUIStyle("label");
            labelStyle.fontSize = fontSize.Value;
            labelStyle.alignment = TextAnchor.UpperLeft;

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

        readonly Dictionary<string, int> logClearedUpTo = new();

        void ProcessLog(string file)
        {
            //logger.LogInfo("Processing " + Path.GetFileName(file));
            try
            {
                List<string> data = new();

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
                    if (line.Contains("Exception"))
                    {
                        //logger.LogInfo("  Found on line " + i + "(" + Path.GetFileName(file) + ")");
                        List<string> errorLines = new();
                        errorLines.Add(Path.GetFileName(file) + " [" + (i + 1) + "]");
                        errorLines.Add("");
                        errorLines.Add(line);
                        for (int j = i + 1; j < data.Count; j++)
                        {
                            var er = data[j];
                            if (er.Trim().StartsWith("at ") || er.Trim().StartsWith("--- "))
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

            if (_staticRectStyle == null)
            {
                _staticRectStyle = new GUIStyle();
                _staticRectStyle.alignment = TextAnchor.UpperLeft;
            }

            _staticRectTexture.SetPixel(0, 0, color);
            _staticRectTexture.Apply();

            _staticRectStyle.normal.background = _staticRectTexture;

            GUI.Box(position, GUIContent.none, _staticRectStyle);


        }
    }
}
